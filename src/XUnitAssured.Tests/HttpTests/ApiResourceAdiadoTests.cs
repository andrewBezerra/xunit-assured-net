using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

using XUnitAssured.Core.Abstractions;
using XUnitAssured.Core.DSL;
using XUnitAssured.Http.Extensions;
using XUnitAssured.Http.Results;

namespace XUnitAssured.Tests.HttpTests;

/// <summary>
/// Uma URL que depende do que um passo anterior produziu.
///
/// <para>
/// Desde a 6.0.0 a cadeia descreve primeiro e executa depois, então uma string interpolada ao
/// escrever a cadeia é montada antes de o passo anterior rodar. Só o <c>NavigateTo</c> tinha a
/// sobrecarga com função; para HTTP, a saída documentada era escrever duas cadeias.
/// </para>
///
/// <para>
/// O caso que isso atrapalha é o teste de comportamento mais comum que existe: criar e ler de
/// volta o que foi criado. Conferir só o 201 de uma criação é testar um valor -- uma criação que
/// não grava nada também devolve 201.
/// </para>
///
/// Sem rede: um servidor de mentira registra o que recebeu, e é isso que os testes conferem.
/// </summary>
[Trait("Category", "Http")]
[Trait("Component", "ApiResource")]
public class ApiResourceAdiadoTests
{
	[Fact(DisplayName = "A deferred URL is built when the step runs, not when the chain is written")]
	public async Task Deferred_Url_Is_Built_When_The_Step_Runs()
	{
		var servidor = new ServidorDeMentira(_ => Responder(HttpStatusCode.OK, "{}"));
		var id = "ao-escrever";

		var cadeia = ScenarioDsl.Given(new ProvedorDeMentira(servidor))
			.ApiResource(() => $"/orders/{id}")
			.Get();

		id = "ao-executar";
		await cadeia.ExecuteAsync();

		servidor.Recebidos.ShouldHaveSingleItem().Caminho.ShouldBe("/orders/ao-executar");
	}

	// O contraste, para o teste acima não passar por acaso: a string é fixada ao escrever.
	[Fact(DisplayName = "A string URL is fixed when the chain is written")]
	public async Task String_Url_Is_Fixed_When_The_Chain_Is_Written()
	{
		var servidor = new ServidorDeMentira(_ => Responder(HttpStatusCode.OK, "{}"));
		var id = "ao-escrever";

		var cadeia = ScenarioDsl.Given(new ProvedorDeMentira(servidor))
			.ApiResource($"/orders/{id}")
			.Get();

		id = "ao-executar";
		await cadeia.ExecuteAsync();

		servidor.Recebidos.ShouldHaveSingleItem().Caminho.ShouldBe("/orders/ao-escrever");
	}

	[Fact(DisplayName = "One chain can create a resource and then read it back")]
	public async Task One_Chain_Can_Create_And_Read_Back()
	{
		var servidor = new ServidorDeMentira(pedido => (pedido.Method.Method, pedido.RequestUri!.AbsolutePath) switch
		{
			("POST", "/orders") => Responder(HttpStatusCode.Created, """{"id":"abc-123"}"""),
			("GET", "/orders/abc-123") => Responder(HttpStatusCode.OK, """{"id":"abc-123","item":"cadeira"}"""),
			_ => Responder(HttpStatusCode.NotFound, "{}")
		});

		string? id = null;

		var leitura = await ScenarioDsl.Given(new ProvedorDeMentira(servidor))
			.ApiResource("/orders").Post(new { item = "cadeira" })
			.Validate((HttpStepResult r) => id = r.JsonPath<string>("$.id"))
			.And()
			.ApiResource(() => $"/orders/{id}").Get()
			.ExecuteAsync();

		leitura.Then()
			.AssertStatusCode(200)
			.AssertJsonPath<string>("$.item", item => item == "cadeira", "the created resource should be read back");

		servidor.Recebidos.Select(r => $"{r.Metodo} {r.Caminho}")
			.ShouldBe(["POST /orders", "GET /orders/abc-123"]);
	}

	// Todo verbo que muda um valor reconstrói o passo pelo construtor de cópia. Foi por aí que
	// configuração já se perdeu antes (6.1.0); a URL adiada precisa sobreviver a eles.
	[Fact(DisplayName = "Verbs after a deferred URL keep it")]
	public async Task Verbs_After_A_Deferred_Url_Keep_It()
	{
		var servidor = new ServidorDeMentira(_ => Responder(HttpStatusCode.Created, "{}"));
		var id = "ao-escrever";

		var cadeia = ScenarioDsl.Given(new ProvedorDeMentira(servidor))
			.ApiResource(() => $"/orders/{id}/items")
			.Post(new { quantidade = 1 })
			.WithHeader("X-Teste", "sim")
			.WithQueryParam("origem", "teste")
			.WithTimeout(5);

		id = "ao-executar";
		await cadeia.ExecuteAsync();

		var recebido = servidor.Recebidos.ShouldHaveSingleItem();
		recebido.Metodo.ShouldBe("POST");
		recebido.Caminho.ShouldBe("/orders/ao-executar/items");
		recebido.Consulta.ShouldBe("?origem=teste");
		recebido.Cabecalhos["X-Teste"].ShouldBe("sim");
	}

	[Fact(DisplayName = "An empty deferred URL fails the step and says why")]
	public async Task Empty_Deferred_Url_Fails_The_Step()
	{
		var servidor = new ServidorDeMentira(_ => Responder(HttpStatusCode.OK, "{}"));
		string? id = null;

		var resultado = await ScenarioDsl.Given(new ProvedorDeMentira(servidor))
			.ApiResource(() => id ?? "")
			.Get()
			.ExecuteAsync();

		var passo = resultado.GetResult();
		passo.Success.ShouldBeFalse();
		passo.Errors.ShouldContain(e => e.Contains("URL provider"));
		servidor.Recebidos.ShouldBeEmpty("no request should leave with an empty address");
	}

	[Fact(DisplayName = "A null URL function is refused when the chain is written")]
	public void Null_Url_Function_Is_Refused()
	{
		Should.Throw<ArgumentNullException>(() =>
			ScenarioDsl.Given().ApiResource((Func<string>)null!));
	}

	private static HttpResponseMessage Responder(HttpStatusCode status, string json) =>
		ServidorDeMentira.Responder(status, json);
}
