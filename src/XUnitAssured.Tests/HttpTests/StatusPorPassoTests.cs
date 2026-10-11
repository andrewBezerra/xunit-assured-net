using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

using XUnitAssured.Core.DSL;
using XUnitAssured.Http.Extensions;
using XUnitAssured.Http.Results;

namespace XUnitAssured.Tests.HttpTests;

/// <summary>
/// O status de um passo do meio do cenário.
///
/// <para>
/// Criar e ler de volta é um cenário de dois passos, e o status do primeiro era conferido à mão,
/// num Validate com o corpo anexado à mensagem. Uma suíte real fazia isso em 3 lugares; o exemplo
/// do README nem conferia, e quando o POST falhava o GET ia para /orders/0 e a falha falava do
/// passo errado.
/// </para>
///
/// Sem rede: um servidor de mentira registra o que recebeu.
/// </summary>
[Trait("Category", "Http")]
[Trait("Component", "Assertions")]
public class StatusPorPassoTests
{
	[Fact(DisplayName = "ExpectStatusCode passes and the next step runs")]
	public async Task Passes_And_The_Next_Step_Runs()
	{
		var servidor = Servidor(HttpStatusCode.Created);

		var leitura = await CriarELer(servidor, 201);

		leitura.AssertStatusCode(200).AssertJsonPath<string>("$.item", i => i == "cadeira");
		servidor.Recebidos.Select(r => r.Caminho).ShouldBe(["/orders", "/orders/abc-123"]);
	}

	[Fact(DisplayName = "ExpectStatusCode stops the scenario at its step, naming the request, with the body")]
	public async Task Stops_At_Its_Step()
	{
		var servidor = Servidor(HttpStatusCode.BadRequest);

		var erro = await Falha(() => CriarELer(servidor, 201));

		erro.Message.ShouldContain("""After POST /orders: Expected HTTP status code 201 but got 400. Body: {"code":"ItemRequired"}""");
		servidor.Recebidos.Count.ShouldBe(1, "the read-back must not run after a failed creation");
	}

	[Fact(DisplayName = "ExpectStatusCode accepts any of several codes")]
	public async Task Accepts_Several_Codes()
	{
		await CriarELer(Servidor(HttpStatusCode.OK), 201, 200);

		(await Falha(() => CriarELer(Servidor(HttpStatusCode.Conflict), 201, 200)))
			.Message.ShouldContain("Expected HTTP status code 201 or 200 but got 409");
	}

	[Fact(DisplayName = "ExpectStatusCode checks every response of concurrent requests")]
	public async Task Checks_Every_Concurrent_Response()
	{
		var vez = 0;
		var servidor = new ServidorDeMentira(_ => System.Threading.Interlocked.Increment(ref vez) == 2
			? ServidorDeMentira.Responder(HttpStatusCode.Conflict, "{}")
			: ServidorDeMentira.Responder(HttpStatusCode.Created, "{}"));

		var erro = await Falha(() => ScenarioDsl.Given(new ProvedorDeMentira(servidor))
			.ApiResource("/orders").Post(new { }).Concurrently(3)
			.ExpectStatusCode(201)
			.ExecuteAsync());

		erro.Message.ShouldContain("After POST /orders: Expected every one of the 3 concurrent requests to get 201");
	}

	[Fact(DisplayName = "ExpectStatusCode on the last step is checked when the chain runs")]
	public async Task Works_On_The_Last_Step()
	{
		var erro = await Falha(() => ScenarioDsl.Given(new ProvedorDeMentira(Servidor(HttpStatusCode.BadRequest)))
			.ApiResource("/orders").Post(new { item = "" })
			.ExpectStatusCode(201)
			.ExecuteAsync());

		erro.Message.ShouldContain("After POST /orders: Expected HTTP status code 201 but got 400");
	}

	// ---------- Apoio ----------

	private static Task<HttpValidationBuilder> CriarELer(ServidorDeMentira servidor, int aceito, params int[] tambem)
	{
		string? id = null;

		return ScenarioDsl.Given(new ProvedorDeMentira(servidor))
			.ApiResource("/orders").Post(new { item = "cadeira" })
			.ExpectStatusCode(aceito, tambem)
			.Validate((HttpStepResult criado) => id = criado.JsonPath<string>("$.id"))
		.And()
			.ApiResource(() => $"/orders/{id}").Get()
			.ExecuteAsync();
	}

	// A criação responde com o status pedido; a leitura de volta, com o que foi criado.
	private static ServidorDeMentira Servidor(HttpStatusCode criacao) =>
		new(pedido => pedido.Method == HttpMethod.Post
			? ServidorDeMentira.Responder(criacao, criacao < HttpStatusCode.BadRequest
				? """{"id":"abc-123"}"""
				: """{"code":"ItemRequired"}""")
			: ServidorDeMentira.Responder(HttpStatusCode.OK, """{"id":"abc-123","item":"cadeira"}"""));

	// O Should.ThrowAsync do Shouldly deixa passar a própria ShouldAssertException em vez de
	// capturá-la, então a falha esperada é apanhada aqui.
	private static async Task<ShouldAssertException> Falha(Func<Task> cadeia)
	{
		try
		{
			await cadeia();
		}
		catch (ShouldAssertException ex)
		{
			return ex;
		}

		throw new InvalidOperationException("Expected the chain to fail, and it passed.");
	}
}
