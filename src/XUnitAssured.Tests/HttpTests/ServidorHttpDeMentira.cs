using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using XUnitAssured.Core.Abstractions;

namespace XUnitAssured.Tests.HttpTests;

/// <summary>
/// Um servidor de mentira para os testes do HTTP que precisam passar pela DSL inteira sem rede:
/// registra o que recebeu e responde o que o teste mandar.
/// </summary>
internal sealed class ServidorDeMentira(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
{
	private readonly List<Recebido> _recebidos = [];

	public IReadOnlyList<Recebido> Recebidos => _recebidos;

	/// <summary>Um servidor que dá sempre a mesma resposta.</summary>
	public static ServidorDeMentira Sempre(HttpResponseMessage resposta) => new(_ => resposta);

	public static HttpResponseMessage Responder(
		HttpStatusCode status,
		string corpo,
		string tipo = "application/json",
		params (string Nome, string Valor)[] cabecalhos)
	{
		var resposta = new HttpResponseMessage(status) { Content = new StringContent(corpo, Encoding.UTF8, tipo) };

		foreach (var (nome, valor) in cabecalhos)
			resposta.Headers.TryAddWithoutValidation(nome, valor);

		return resposta;
	}

	protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
	{
		_recebidos.Add(new Recebido(
			request.Method.Method,
			request.RequestUri!.AbsolutePath,
			request.RequestUri.Query,
			request.Headers.ToDictionary(h => h.Key, h => string.Join(",", h.Value))));

		return Task.FromResult(responder(request));
	}
}

internal sealed record Recebido(string Metodo, string Caminho, string Consulta, Dictionary<string, string> Cabecalhos);

/// <summary>Entrega à DSL um HttpClient que fala com o servidor de mentira.</summary>
internal sealed class ProvedorDeMentira(HttpMessageHandler servidor) : IHttpClientProvider
{
	public HttpClient CreateClient() =>
		new(servidor, disposeHandler: false) { BaseAddress = new Uri("http://servidor.teste") };
}
