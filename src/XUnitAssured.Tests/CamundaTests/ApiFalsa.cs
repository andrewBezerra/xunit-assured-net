using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace XUnitAssured.Tests.CamundaTests;

/// <summary>
/// O Camunda e o provedor de identidade do outro lado do <c>CamundaApiClient</c>, sem rede: responde
/// pelo método e pelo caminho, em sequência (a última resposta de uma rota se repete), e guarda cada
/// pedido com o cabeçalho de autenticação, para os testes do token.
/// </summary>
internal sealed class ApiFalsa : HttpMessageHandler
{
	private readonly Dictionary<string, Queue<Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>>> _rotas = new();
	private readonly List<PedidoVisto> _pedidos = new();
	private readonly SemaphoreSlim _chegou = new(0);

	/// <summary>Os pedidos, na ordem em que chegaram.</summary>
	public IReadOnlyList<PedidoVisto> Pedidos
	{
		get { lock (_pedidos) return _pedidos.ToList(); }
	}

	/// <summary>Os pedidos a um caminho.</summary>
	public IReadOnlyList<PedidoVisto> A(string caminho) => Pedidos.Where(p => p.Caminho == caminho).ToList();

	public ApiFalsa Responder(HttpMethod metodo, string caminho, string? corpo, HttpStatusCode status = HttpStatusCode.OK, string tipo = "application/json") =>
		Com(metodo, caminho, (pedido, _) => Task.FromResult(Resposta(pedido, status, corpo, tipo)));

	public ApiFalsa Recusar(HttpMethod metodo, string caminho, HttpStatusCode status, string title, string detail) =>
		Responder(metodo, caminho, $$"""{ "type": "about:blank", "title": "{{title}}", "status": {{(int)status}}, "detail": "{{detail}}" }""",
			status, "application/problem+json");

	/// <summary>A rota falha sem resposta, como uma conexão recusada.</summary>
	public ApiFalsa SemResposta(HttpMethod metodo, string caminho) =>
		Com(metodo, caminho, (_, _) => throw new HttpRequestException("Nenhuma conexão pôde ser feita."));

	/// <summary>A rota nunca responde: só o cancelamento a termina.</summary>
	public ApiFalsa Pendurar(HttpMethod metodo, string caminho) =>
		Com(metodo, caminho, async (pedido, ct) =>
		{
			await Task.Delay(Timeout.Infinite, ct).ConfigureAwait(false);
			return Resposta(pedido, HttpStatusCode.OK, "{}", "application/json");
		});

	public ApiFalsa Com(HttpMethod metodo, string caminho, Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> resposta)
	{
		var chave = $"{metodo.Method} {caminho}";
		lock (_rotas)
		{
			if (!_rotas.TryGetValue(chave, out var fila))
				_rotas[chave] = fila = new Queue<Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>>();
			fila.Enqueue(resposta);
		}

		return this;
	}

	/// <summary>Espera até <paramref name="quantos"/> pedidos terem chegado, de verdade (com limite de 10 s).</summary>
	public async Task EsperarPedidos(int quantos)
	{
		using var limite = new CancellationTokenSource(TimeSpan.FromSeconds(10));
		while (Pedidos.Count < quantos)
			await _chegou.WaitAsync(limite.Token).ConfigureAwait(false);
	}

	protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
	{
		var corpo = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
		var caminho = request.RequestUri!.AbsolutePath;
		lock (_pedidos)
			_pedidos.Add(new PedidoVisto(request.Method.Method, request.RequestUri.Host, caminho, request.RequestUri.Query,
				request.Headers.Authorization?.ToString(), request.Content?.Headers.ContentType?.MediaType, corpo));
		_chegou.Release();

		Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>? resposta = null;
		lock (_rotas)
		{
			if (_rotas.TryGetValue($"{request.Method.Method} {caminho}", out var fila))
				resposta = fila.Count > 1 ? fila.Dequeue() : fila.Peek();
		}

		if (resposta is null)
			return Resposta(request, HttpStatusCode.NotFound,
				$$"""{ "type": "about:blank", "title": "NOT_FOUND", "status": 404, "detail": "No route {{caminho}} in the fake" }""",
				"application/problem+json");

		return await resposta(request, cancellationToken).ConfigureAwait(false);
	}

	private static HttpResponseMessage Resposta(HttpRequestMessage pedido, HttpStatusCode status, string? corpo, string tipo)
	{
		var mensagem = new HttpResponseMessage(status) { RequestMessage = pedido };
		if (corpo is not null)
			mensagem.Content = new StringContent(corpo, Encoding.UTF8, tipo);
		return mensagem;
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing)
			_chegou.Dispose();
		base.Dispose(disposing);
	}
}

/// <summary>Um pedido como chegou ao falso.</summary>
internal sealed record PedidoVisto(
	string Metodo, string Host, string Caminho, string Query, string? Autorizacao, string? TipoDoConteudo, string? Corpo)
{
	/// <summary>O corpo, em JSON.</summary>
	public JsonNode Json => JsonNode.Parse(Corpo ?? throw new InvalidOperationException($"{Metodo} {Caminho} has no body."))!;
}
