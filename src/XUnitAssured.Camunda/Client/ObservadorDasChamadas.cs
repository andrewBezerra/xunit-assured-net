using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace XUnitAssured.Camunda.Client;

/// <summary>
/// Avisa cada pedido HTTP à operação que o fez (spec do conexoes-do-camunda, D6): o método, o caminho, o status ou a falta
/// de resposta, e a duração. Fica embaixo da autenticação, para ver o pedido do token e a chamada repetida com o token
/// novo, e nunca lê o cabeçalho de autenticação.
/// </summary>
internal sealed class ObservadorDasChamadas : DelegatingHandler
{
	private readonly string _prefixo;
	private readonly TimeProvider _tempo;

	/// <param name="interno">O próximo handler.</param>
	/// <param name="prefixo">O caminho do endereço REST cadastrado (vazio para <c>http://host:8080</c>), tirado do caminho avisado.</param>
	/// <param name="tempo">O relógio da duração.</param>
	public ObservadorDasChamadas(HttpMessageHandler interno, string prefixo, TimeProvider tempo) : base(interno)
	{
		_prefixo = prefixo.TrimEnd('/');
		_tempo = tempo;
	}

	protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
	{
		var escopo = EscopoDaChamada.Atual;
		var uri = request.RequestUri!;
		var token = request.Options.TryGetValue(EscopoDaChamada.PedidoDeToken, out var ehToken) && ehToken;
		var endereco = uri.GetLeftPart(UriPartial.Path);
		var caminho = !token && _prefixo.Length > 0 && uri.AbsolutePath.StartsWith(_prefixo + "/", StringComparison.Ordinal)
			? uri.AbsolutePath[_prefixo.Length..]
			: uri.AbsolutePath;
		if (escopo is not null && !token)
		{
			escopo.UltimoCaminho = caminho;
			escopo.UltimoEndereco = endereco;
		}

		var inicio = _tempo.GetTimestamp();
		int? status = null;
		try
		{
			var resposta = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
			status = (int)resposta.StatusCode;
			return resposta;
		}
		finally
		{
			escopo?.Observe?.Invoke(new CamundaCall(
				token ? CamundaCallKind.Token : CamundaCallKind.Api,
				request.Method.Method,
				caminho,
				endereco,
				status,
				_tempo.GetElapsedTime(inicio),
				request.Options.TryGetValue(EscopoDaChamada.ComTokenNovo, out var novo) && novo));
		}
	}
}
