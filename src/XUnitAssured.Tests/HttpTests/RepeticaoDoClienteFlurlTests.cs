using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Flurl.Http;

using Shouldly;
using Xunit;

using XUnitAssured.Http.Extensions;

namespace XUnitAssured.Tests.HttpTests;

[Trait("Category", "Http")]
[Trait("Component", "Client")]
/// <summary>
/// As extensões de repetição do <c>FlurlClient</c>: quando tentam de novo, quando desistem, e o
/// que dizem ao desistir.
///
/// <para>
/// O servidor é um <see cref="HttpMessageHandler"/> em memória que responde uma sequência de
/// status, então dá para contar as tentativas. A regra que se verifica: erro do cliente (4xx) não
/// se repete, erro do servidor (5xx) e falha de transporte se repetem até o limite, e a exceção
/// final carrega a última causa.
/// </para>
/// </summary>
public class RepeticaoDoClienteFlurlTests
{
	/// <summary>Responde, em ordem, os status pedidos; o último se repete.</summary>
	private sealed class Sequencia(params HttpStatusCode[] status) : HttpMessageHandler
	{
		private int _chamadas;

		public int Chamadas => _chamadas;

		public List<(string Metodo, string Caminho, string? Corpo)> Pedidos { get; } = new();

		protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			var corpo = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
			Pedidos.Add((request.Method.Method, request.RequestUri!.AbsolutePath, corpo));

			var indice = Math.Min(Interlocked.Increment(ref _chamadas) - 1, status.Length - 1);
			return new HttpResponseMessage(status[indice])
			{
				Content = new StringContent("{\"pedidoId\":7,\"status\":\"pago\"}", Encoding.UTF8, "application/json")
			};
		}
	}

	private sealed class FalhaDeTransporte : HttpMessageHandler
	{
		public int Chamadas { get; private set; }

		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			Chamadas++;
			throw new HttpRequestException("conexão recusada");
		}
	}

	private sealed class Pedido
	{
		public int PedidoId { get; set; }

		public string? Status { get; set; }
	}

	private static FlurlClient Cliente(HttpMessageHandler servidor) =>
		new(new HttpClient(servidor) { BaseAddress = new Uri("http://servidor.teste") });

	[Fact(DisplayName = "A successful GET should return on the first attempt")]
	public async Task Successful_Get_Should_Return_At_Once()
	{
		var servidor = new Sequencia(HttpStatusCode.OK);

		var resposta = await Cliente(servidor).GetWithRetryAsync("/pedidos/7", retryDelay: 1);

		resposta.StatusCode.ShouldBe(HttpStatusCode.OK);
		servidor.Chamadas.ShouldBe(1);
		servidor.Pedidos[0].ShouldBe(("GET", "/pedidos/7", (string?)null));
	}

	[Fact(DisplayName = "A server error should be retried until the server answers")]
	public async Task Server_Error_Should_Be_Retried()
	{
		var servidor = new Sequencia(HttpStatusCode.ServiceUnavailable, HttpStatusCode.BadGateway, HttpStatusCode.OK);

		var resposta = await Cliente(servidor).GetWithRetryAsync("/pedidos/7", maxRetries: 3, retryDelay: 1);

		resposta.StatusCode.ShouldBe(HttpStatusCode.OK);
		servidor.Chamadas.ShouldBe(3);
	}

	[Fact(DisplayName = "A client error should not be retried: it is thrown on the first attempt")]
	public async Task Client_Error_Should_Not_Be_Retried()
	{
		var servidor = new Sequencia(HttpStatusCode.NotFound, HttpStatusCode.OK);

		var erro = await Should.ThrowAsync<FlurlHttpException>(() =>
			Cliente(servidor).GetWithRetryAsync("/pedidos/9", retryDelay: 1));

		erro.StatusCode.ShouldBe(404);
		servidor.Chamadas.ShouldBe(1, "asking again will not make a 404 exist");
	}

	[Fact(DisplayName = "Giving up should say how many attempts were made and keep the last cause")]
	public async Task Giving_Up_Should_Say_The_Attempts()
	{
		var servidor = new Sequencia(HttpStatusCode.InternalServerError);

		var erro = await Should.ThrowAsync<HttpRequestException>(() =>
			Cliente(servidor).DeleteWithRetryAsync("/pedidos/7", maxRetries: 2, retryDelay: 1));

		erro.Message.ShouldStartWith("Request failed after 2 attempts");
		erro.InnerException.ShouldBeOfType<FlurlHttpException>();
		servidor.Chamadas.ShouldBe(2);
	}

	[Fact(DisplayName = "A transport failure should be retried like a server error")]
	public async Task Transport_Failure_Should_Be_Retried()
	{
		var servidor = new FalhaDeTransporte();

		var erro = await Should.ThrowAsync<HttpRequestException>(() =>
			Cliente(servidor).GetWithRetryAsync("/x", maxRetries: 3, retryDelay: 1));

		erro.Message.ShouldContain("after 3 attempts");
		servidor.Chamadas.ShouldBe(3);
	}

	// Com o Flurl aceitando qualquer status, a resposta chega sem exceção, e a regra é a mesma.
	[Fact(DisplayName = "With any status allowed, a client error should come back as is and a server error should be retried")]
	public async Task Any_Status_Allowed_Should_Follow_The_Same_Rule()
	{
		var naoEncontrado = new Sequencia(HttpStatusCode.NotFound);
		var quebrado = new Sequencia(HttpStatusCode.InternalServerError);
		var clienteA = Cliente(naoEncontrado);
		clienteA.Settings.AllowedHttpStatusRange = "*";
		var clienteB = Cliente(quebrado);
		clienteB.Settings.AllowedHttpStatusRange = "*";

		var resposta = await clienteA.GetWithRetryAsync("/x", retryDelay: 1);
		var erro = await Should.ThrowAsync<HttpRequestException>(() => clienteB.GetWithRetryAsync("/x", maxRetries: 2, retryDelay: 1));

		resposta.StatusCode.ShouldBe(HttpStatusCode.NotFound);
		naoEncontrado.Chamadas.ShouldBe(1);
		erro.Message.ShouldContain("Request failed with status code InternalServerError");
		quebrado.Chamadas.ShouldBe(2);
	}

	[Fact(DisplayName = "A cancelled token should stop before any attempt")]
	public async Task Cancelled_Token_Should_Stop()
	{
		var servidor = new Sequencia(HttpStatusCode.OK);
		using var cancelado = new CancellationTokenSource();
		cancelado.Cancel();

		await Should.ThrowAsync<OperationCanceledException>(() =>
			Cliente(servidor).GetWithRetryAsync("/x", cancellationToken: cancelado.Token));

		servidor.Chamadas.ShouldBe(0);
	}

	[Fact(DisplayName = "POST, PUT and PATCH with retry should send the body as JSON with their verb, and DELETE none")]
	public async Task Body_Verbs_Should_Send_Json()
	{
		var servidor = new Sequencia(HttpStatusCode.OK);
		var cliente = Cliente(servidor);

		await cliente.PostWithRetryAsync("/pedidos", new { status = "novo" }, retryDelay: 1);
		await cliente.PutWithRetryAsync("/pedidos/7", new { status = "pago" }, retryDelay: 1);
		await cliente.PatchWithRetryAsync("/pedidos/7", new { status = "enviado" }, retryDelay: 1);
		var apagado = await cliente.DeleteWithRetryAsync("/pedidos/7", retryDelay: 1);

		apagado.StatusCode.ShouldBe(HttpStatusCode.OK);

		servidor.Pedidos.ShouldBe(new[]
		{
			("POST", "/pedidos", (string?)"{\"status\":\"novo\"}"),
			("PUT", "/pedidos/7", (string?)"{\"status\":\"pago\"}"),
			("PATCH", "/pedidos/7", (string?)"{\"status\":\"enviado\"}"),
			("DELETE", "/pedidos/7", (string?)null)
		});
	}

	[Fact(DisplayName = "ReadAsJsonAsync should read camelCase by default, and use the options given")]
	public async Task ReadAsJson_Should_Deserialize()
	{
		var servidor = new Sequencia(HttpStatusCode.OK);
		var cliente = Cliente(servidor);

		var padrao = await (await cliente.GetWithRetryAsync("/p", retryDelay: 1)).ReadAsJsonAsync<Pedido>();
		var sensivel = await (await cliente.GetWithRetryAsync("/p", retryDelay: 1))
			.ReadAsJsonAsync<Pedido>(new JsonSerializerOptions { PropertyNameCaseInsensitive = false });

		padrao!.PedidoId.ShouldBe(7);
		padrao.Status.ShouldBe("pago");
		sensivel!.PedidoId.ShouldBe(0, "with case-sensitive options, pedidoId does not match PedidoId");
	}

	[Fact(DisplayName = "The auth helpers should put the right header on the request")]
	public void Auth_Helpers_Should_Set_The_Header()
	{
		var bearer = FlurlClientExtensions.WithBearerToken(new FlurlRequest("http://a"), "tk");
		var basica = FlurlClientExtensions.WithBasicAuth(new FlurlRequest("http://a"), "user", "pass");
		var chave = FlurlClientExtensions.WithApiKey(new FlurlRequest("http://a"), "k-1");
		var chaveNomeada = FlurlClientExtensions.WithApiKey(new FlurlRequest("http://a"), "k-2", "X-Chave");

		bearer.Headers.FirstOrDefault("Authorization").ShouldBe("Bearer tk");
		basica.Headers.FirstOrDefault("Authorization").ShouldBe("Basic dXNlcjpwYXNz");
		chave.Headers.FirstOrDefault("X-API-Key").ShouldBe("k-1");
		chaveNomeada.Headers.FirstOrDefault("X-Chave").ShouldBe("k-2");
	}
}
