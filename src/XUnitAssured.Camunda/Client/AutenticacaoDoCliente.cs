using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace XUnitAssured.Camunda.Client;

/// <summary>Os dados da autenticação, já resolvidos: a estratégia e o que ela pede.</summary>
internal sealed record DadosDaAutenticacao(
	string Estrategia, string? Usuario, string? Senha, string? ClientId, string? ClientSecret, string? EnderecoDoToken, string? Audience, string? Scope);

/// <summary>
/// A autenticação do cliente, embaixo do SDK (spec do conexoes-do-camunda, D5): o basic, ou o token OAuth por client
/// credentials, guardado até perto de vencer, pedido uma vez só para chamadas concorrentes (o voo único) e trocado uma vez
/// quando o Camunda recusa o guardado.
/// </summary>
/// <remarks>
/// <para>
/// Não é o <c>OAuthManager</c> do SDK porque ele pede o token num cliente HTTP dele (medido na 10.0.3: com o
/// <c>HttpMessageHandler</c> do pacote, o pedido do token foi à rede, e não ao handler), fora da conexão reaproveitada e do
/// aviso das chamadas, e a recusa do provedor sai sem o status nem a descrição dele. Com o <c>HttpClient</c> do pacote, o
/// SDK não autentica nada: é aqui que a credencial entra.
/// </para>
/// <para>
/// O 401 e o 403 da API voltam como resposta, e não como exceção: o cliente os traduz na recusa da credencial, e a DSL,
/// que chama o SDK direto, continua vendo a recusa do SDK como antes.
/// </para>
/// </remarks>
internal sealed class AutenticacaoDoCliente : DelegatingHandler
{
	/// <summary>Quanto antes de vencer o token guardado é trocado.</summary>
	internal static readonly TimeSpan Margem = TimeSpan.FromSeconds(30);

	// O prazo do token sem o expires_in na resposta do provedor.
	private static readonly TimeSpan PrazoSemExpiresIn = TimeSpan.FromMinutes(5);

	private readonly DadosDaAutenticacao _dados;
	private readonly TimeProvider _tempo;
	private readonly SemaphoreSlim _umPedido = new(1, 1);
	private (string Token, DateTimeOffset Vence)? _guardado;

	public AutenticacaoDoCliente(HttpMessageHandler interno, DadosDaAutenticacao dados, TimeProvider tempo) : base(interno)
	{
		_dados = dados;
		_tempo = tempo;
	}

	/// <summary>O endereço do token sem a query, como as mensagens o citam.</summary>
	public string EnderecoDoToken => Uri.TryCreate(_dados.EnderecoDoToken, UriKind.Absolute, out var uri)
		? uri.GetLeftPart(UriPartial.Path)
		: _dados.EnderecoDoToken ?? string.Empty;

	protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
	{
		switch (_dados.Estrategia)
		{
			case "BASIC":
				request.Headers.Authorization = new AuthenticationHeaderValue("Basic",
					Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_dados.Usuario}:{_dados.Senha}")));
				return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
			case "OAUTH":
				return await ComTokenAsync(request, cancellationToken).ConfigureAwait(false);
			default:
				return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
		}
	}

	private async Task<HttpResponseMessage> ComTokenAsync(HttpRequestMessage request, CancellationToken cancellationToken)
	{
		// O corpo é lido antes, para a chamada poder ir de novo com o token novo.
		var corpo = request.Content is null ? null : await request.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
		var token = await TokenAsync(recusado: null, cancellationToken).ConfigureAwait(false);
		request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
		var resposta = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
		if (resposta.StatusCode is not (HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden))
			return resposta;

		// O guardado pode ter sido revogado antes de vencer: um novo, uma vez só.
		resposta.Dispose();
		var novo = await TokenAsync(recusado: token, cancellationToken).ConfigureAwait(false);
		using var denovo = Copia(request, corpo);
		denovo.Headers.Authorization = new AuthenticationHeaderValue("Bearer", novo);
		denovo.Options.Set(EscopoDaChamada.ComTokenNovo, true);
		return await base.SendAsync(denovo, cancellationToken).ConfigureAwait(false);
	}

	// O token guardado enquanto vale; senão, um novo, num pedido só para quem chega ao mesmo tempo. Com o recusado, troca
	// por outro, a não ser que outra chamada já o tenha trocado.
	private async Task<string> TokenAsync(string? recusado, CancellationToken cancellationToken)
	{
		if (Valido() is { } antes && antes != recusado)
			return antes;

		await _umPedido.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			if (Valido() is { } guardado && guardado != recusado)
				return guardado;

			var (token, vence) = await PedirAsync(cancellationToken).ConfigureAwait(false);
			_guardado = (token, vence);
			return token;
		}
		finally
		{
			_umPedido.Release();
		}
	}

	private string? Valido() =>
		_guardado is { } g && g.Vence - Margem > _tempo.GetUtcNow() ? g.Token : null;

	private async Task<(string Token, DateTimeOffset Vence)> PedirAsync(CancellationToken cancellationToken)
	{
		var campos = new Dictionary<string, string>
		{
			["grant_type"] = "client_credentials",
			["client_id"] = _dados.ClientId ?? string.Empty,
			["client_secret"] = _dados.ClientSecret ?? string.Empty,
		};
		if (!string.IsNullOrWhiteSpace(_dados.Audience))
			campos["audience"] = _dados.Audience!.Trim();
		if (!string.IsNullOrWhiteSpace(_dados.Scope))
			campos["scope"] = _dados.Scope!.Trim();

		using var pedido = new HttpRequestMessage(HttpMethod.Post, _dados.EnderecoDoToken) { Content = new FormUrlEncodedContent(campos) };
		pedido.Headers.Accept.ParseAdd("application/json");
		pedido.Options.Set(EscopoDaChamada.PedidoDeToken, true);

		var escopo = EscopoDaChamada.Atual;
		if (escopo is not null)
			escopo.PedindoToken = true;
		HttpResponseMessage resposta;
		string texto;
		try
		{
			resposta = await base.SendAsync(pedido, cancellationToken).ConfigureAwait(false);
			texto = await resposta.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
		}
		catch (HttpRequestException ex)
		{
			throw new CamundaUnavailableException(CamundaEndpoint.IdentityProvider, EnderecoDoToken, null, ex.Message, ex);
		}

		if (escopo is not null)
			escopo.PedindoToken = false;
		using var _ = resposta;
		return Ler(resposta.StatusCode, texto);
	}

	// O token e quando vence; ou a recusa do provedor, sem o secret.
	private (string Token, DateTimeOffset Vence) Ler(HttpStatusCode status, string texto)
	{
		JsonNode? json = null;
		try
		{
			json = JsonNode.Parse(texto);
		}
		catch (JsonException)
		{
			// Uma resposta que não é JSON fica sem a descrição.
		}

		if ((int)status is < 200 or > 299)
		{
			var motivo = Texto(json?["error_description"]) ?? Texto(json?["error"]);
			throw new CamundaCredentialException(CamundaCredentialRefusal.IdentityProvider, (int)status,
				motivo is null ? null : SemOSecret(motivo), EnderecoDoToken);
		}

		if (Texto(json?["access_token"]) is not { Length: > 0 } token)
			throw new CamundaCredentialException(CamundaCredentialRefusal.MissingAccessToken, (int)status, null, EnderecoDoToken);

		var prazo = json?["expires_in"] is JsonValue segundos && double.TryParse(segundos.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var s)
			? TimeSpan.FromSeconds(s)
			: PrazoSemExpiresIn;
		return (token, _tempo.GetUtcNow() + prazo);
	}

	// O que o provedor diz volta a quem chamou: um provedor que ecoasse o secret não o põe na mensagem.
	private string SemOSecret(string texto) =>
		string.IsNullOrEmpty(_dados.ClientSecret) ? texto : texto.Replace(_dados.ClientSecret, "***", StringComparison.Ordinal);

	private static string? Texto(JsonNode? valor) => valor is JsonValue v ? v.ToString() : null;

	// O mesmo pedido, de novo: o método, o endereço, os cabeçalhos, o corpo já lido e as marcas do observador.
	private static HttpRequestMessage Copia(HttpRequestMessage original, byte[]? corpo)
	{
		var copia = new HttpRequestMessage(original.Method, original.RequestUri) { Version = original.Version };
		foreach (var cabecalho in original.Headers)
			if (!cabecalho.Key.Equals("Authorization", StringComparison.OrdinalIgnoreCase))
				copia.Headers.TryAddWithoutValidation(cabecalho.Key, cabecalho.Value);
		if (corpo is not null)
		{
			copia.Content = new ByteArrayContent(corpo);
			foreach (var cabecalho in original.Content!.Headers)
				copia.Content.Headers.TryAddWithoutValidation(cabecalho.Key, cabecalho.Value);
		}

		foreach (var opcao in original.Options)
			((IDictionary<string, object?>)copia.Options)[opcao.Key] = opcao.Value;
		return copia;
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing)
			_umPedido.Dispose();
		base.Dispose(disposing);
	}
}
