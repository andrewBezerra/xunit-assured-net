using System;
using System.Collections.Generic;
using System.Net;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

using XUnitAssured.Core.DSL;
using XUnitAssured.Http.Extensions;
using XUnitAssured.Http.Results;

namespace XUnitAssured.Tests.HttpTests;

[Trait("Category", "Http")]
[Trait("Component", "Validation")]
/// <summary>
/// Cada asserção do HTTP escrita antes do <c>await</c>, na Task que a execução devolve, contra uma
/// resposta de verdade do servidor de mentira.
///
/// <para>
/// <c>AssercaoAntesDoAwaitTests</c> garante que todo método do builder tem o par na Task; aqui o
/// par é exercido, e cada um precisa fazer o que o método do builder faz: passar quando a resposta
/// confere e falhar, com a mensagem do builder, quando não confere.
/// </para>
/// </summary>
public class AssercoesEncadeadasNaTaskTests
{
	private const string Corpo =
		"{\"id\":1,\"nome\":\"Ana\",\"apagado\":null,\"tags\":[\"a\",\"b\"],\"itens\":[{\"preco\":10},{\"preco\":20}]}";

	private sealed class Cliente
	{
		public int Id { get; set; }

		public string Nome { get; set; } = string.Empty;
	}

	// O contrato com os nomes como a resposta os escreve: o esquema gerado compara nomes com
	// diferença de maiúsculas e não aceita campo a mais.
	private sealed class ContratoDoCliente
	{
		[JsonPropertyName("id")]
		public int Id { get; set; }

		[JsonPropertyName("nome")]
		public string Nome { get; set; } = string.Empty;

		[JsonPropertyName("apagado")]
		public string? Apagado { get; set; }

		[JsonPropertyName("tags")]
		public List<string> Tags { get; set; } = new();

		[JsonPropertyName("itens")]
		public List<ItemDoContrato> Itens { get; set; } = new();
	}

	private sealed class ItemDoContrato
	{
		[JsonPropertyName("preco")]
		public int Preco { get; set; }
	}

	private sealed class Exigente
	{
		public int Id { get; set; }

		[System.ComponentModel.DataAnnotations.Required]
		public string CampoQueNaoVem { get; set; } = string.Empty;
	}

	// O Should.ThrowAsync do Shouldly não devolve uma ShouldAssertException lançada dentro da Task:
	// ele a relança como falha do próprio teste. Daqui a captura é explícita.
	private static async Task<ShouldAssertException> FalhaDeAssercao(Func<Task> acao)
	{
		try
		{
			await acao();
		}
		catch (ShouldAssertException erro)
		{
			return erro;
		}

		throw new ShouldAssertException("The assertion was expected to fail, and it passed.");
	}

	private static ServidorDeMentira Servidor(HttpStatusCode status = HttpStatusCode.OK, string corpo = Corpo) =>
		ServidorDeMentira.Sempre(ServidorDeMentira.Responder(
			status, corpo, "application/json",
			("X-Origem", "teste"),
			("Set-Cookie", "tema=escuro; Path=/"),
			("Set-Cookie", "antigo=; Max-Age=0; Path=/")));

	private static Task<HttpValidationBuilder> Executar(ServidorDeMentira servidor) =>
		ScenarioDsl.Given(new ProvedorDeMentira(servidor))
			.ApiResource("/clientes/1")
			.WithHeader("Cookie", "sessao=abc")
			.WithHeader("X-Correlacao", "c-1")
			.Get()
			.ExecuteAsync();

	[Fact(DisplayName = "Every assertion chained on the Task should pass against a matching response")]
	public async Task Every_Chained_Assertion_Should_Pass()
	{
		var extraido = (HttpStepResult?)null;

		await Executar(Servidor())
			.Then()
			.AssertSuccess()
			.AssertStatusCode(201, 200)
			.AssertEach(r => r.AssertJsonPath<int>("$.id", id => id == 1))
			.ValidateContract<ContratoDoCliente>()
			.AssertJsonPath<string>("$.nome", nome => nome.ShouldBe("Ana"))
			.AssertJsonPathNull("$.apagado")
			.AssertJsonPathMissing("$.telefone")
			.AssertJsonPathContains("$.tags[*]", "b")
			.AssertJsonPathNotContains("$.tags[*]", "z")
			.AssertJsonPathCount("$.itens", 2)
			.AssertJsonPathAll<int>("$.itens[*].preco", p => p > 0)
			.AssertJsonPathAny<int>("$.itens[*].preco", p => p == 20)
			.AssertHeader("X-Origem", "teste")
			.AssertHeader("x-origem", v => v.StartsWith("tes"))
			.AssertNoHeader("X-Ausente")
			.AssertSetCookie("tema", c => c.Value == "escuro")
			.AssertCookieCleared("antigo")
			.AssertRequestHeader("X-Correlacao", "c-1")
			.AssertRequestHeader("X-Correlacao", v => v.StartsWith("c-"))
			.AssertSentCookie("sessao", v => v == "abc")
			.AssertNoSentCookie("outro")
			.AssertBodyContains("\"Ana\"")
			.AssertBodyNotContains("Bia")
			.AssertErrors(r => r.Errors.ShouldBeEmpty())
			.AssertProperty<int>("StatusCode", s => s.ShouldBe(200))
			.Validate(r => r.ContentType.ShouldNotBeNull())
			.Extract(r => extraido = r);

		extraido.ShouldNotBeNull();
		extraido!.StatusCode.ShouldBe(200);
	}

	[Fact(DisplayName = "Values read from the Task should come from the response")]
	public async Task Values_Read_From_The_Task_Should_Come_From_The_Response()
	{
		var servidor = Servidor();

		var nome = await Executar(servidor).JsonPath<string>("$.nome");
		var precos = await Executar(servidor).JsonPathAll<int>("$.itens[*].preco");
		var resultado = await Executar(servidor).GetResult();

		nome.ShouldBe("Ana");
		precos.ShouldBe(new[] { 10, 20 });
		resultado.StatusCode.ShouldBe(200);
	}

	[Fact(DisplayName = "A problem details response should be asserted on the Task, and AssertFailure should accept it")]
	public async Task Problem_Details_On_The_Task()
	{
		var servidor = ServidorDeMentira.Sempre(ServidorDeMentira.Responder(
			HttpStatusCode.Conflict,
			"{\"type\":\"about:blank\",\"title\":\"Conflito\",\"status\":409,\"detail\":\"Pedido já pago\"}",
			"application/problem+json"));

		await Executar(servidor)
			.AssertFailure()
			.AssertProblemDetails(409, p => p.Title == "Conflito" && p.Detail == "Pedido já pago");
	}

	[Fact(DisplayName = "A chained assertion that does not match should fail with the builder's message")]
	public async Task Mismatched_Chained_Assertion_Should_Fail()
	{
		var servidor = Servidor();

		(await FalhaDeAssercao(() => Executar(servidor).AssertJsonPathContains("$.tags[*]", "z")))
			.Message.ShouldContain("to contain \"z\"");
		(await FalhaDeAssercao(() => Executar(servidor).AssertJsonPathNotContains("$.tags[*]", "a")))
			.Message.ShouldContain("not to contain \"a\"");
		(await FalhaDeAssercao(() => Executar(servidor).AssertJsonPathAll<int>("$.itens[*].preco", p => p > 15)))
			.Message.ShouldContain("These did not: [10]");
		(await FalhaDeAssercao(() => Executar(servidor).AssertJsonPathAny<int>("$.itens[*].preco", p => p > 99)))
			.Message.ShouldContain("None of the 2 did");
		(await FalhaDeAssercao(() => Executar(servidor).AssertJsonPathAny<int>("$.itens[?(@.preco==999)].preco", p => p > 0)))
			.Message.ShouldContain("The path selected no values");
		(await FalhaDeAssercao(() => Executar(servidor).AssertNoHeader("X-Origem")))
			.Message.ShouldContain("Expected no X-Origem header");
		(await FalhaDeAssercao(() => Executar(servidor).AssertSentCookie("outro")))
			.Message.ShouldContain("it carried: sessao");
		(await FalhaDeAssercao(() => Executar(servidor).AssertNoSentCookie("sessao")))
			.Message.ShouldContain("sessao=abc");
		await FalhaDeAssercao(() => Executar(servidor).AssertFailure());
		await FalhaDeAssercao(() => Executar(servidor).AssertBodyContains("Bia"));
		await FalhaDeAssercao(() => Executar(servidor).AssertBodyNotContains("Ana"));
	}

	[Fact(DisplayName = "AssertJsonPathNull should tell null from missing, and say where the path stops")]
	public async Task AssertJsonPathNull_Should_Tell_Null_From_Missing()
	{
		var servidor = Servidor();

		(await FalhaDeAssercao(() => Executar(servidor).AssertJsonPathNull("$.telefone")))
			.Message.ShouldContain("use AssertJsonPathMissing");
		(await FalhaDeAssercao(() => Executar(servidor).AssertJsonPathNull("$.nome")))
			.Message.ShouldContain("but it is \"Ana\"");
		(await FalhaDeAssercao(() => Executar(servidor).AssertJsonPathNull("$.nome.x.y")))
			.Message.ShouldContain("the path stops at $.nome, which is \"Ana\"");
		(await FalhaDeAssercao(() => Executar(servidor).AssertJsonPathNull("$.apagado.x.y")))
			.Message.ShouldContain("the path stops at $.apagado, which is null");
		(await FalhaDeAssercao(() => Executar(servidor).AssertJsonPathMissing("$.apagado")))
			.Message.ShouldContain("use AssertJsonPathNull");
		(await FalhaDeAssercao(() => Executar(servidor).AssertJsonPathMissing("$.itens")))
			.Message.ShouldContain("an array of 2 item(s)");
	}

	[Fact(DisplayName = "ValidateContract should fail when a required field is absent, and refuse an empty body")]
	public async Task ValidateContract_Should_Fail_On_Mismatch()
	{
		(await FalhaDeAssercao(() => Executar(Servidor()).ValidateContract<Exigente>()))
			.Message.ShouldContain("Contract validation failed for type Exigente");

		var vazio = ServidorDeMentira.Sempre(ServidorDeMentira.Responder(HttpStatusCode.OK, ""));
		(await Should.ThrowAsync<InvalidOperationException>(() => Executar(vazio).ValidateContract<Cliente>()))
			.Message.ShouldContain("Response body is empty");
	}

	// O README mostra .ValidateContract<Product>() contra uma API comum, e uma API ASP.NET Core
	// responde em camelCase. Até a 6.30.6 o esquema só tinha os nomes em PascalCase, e todo campo de
	// uma resposta camelCase saía como "NoAdditionalPropertiesAllowed".
	[Fact(DisplayName = "ValidateContract should accept a camelCase response for a PascalCase model")]
	public async Task ValidateContract_Should_Accept_CamelCase()
	{
		await Executar(Servidor(corpo: "{\"id\":1,\"nome\":\"Ana\"}")).ValidateContract<Cliente>();
	}

	// Quem já validava uma API PascalCase continua passando.
	[Fact(DisplayName = "ValidateContract should still accept a PascalCase response")]
	public async Task ValidateContract_Should_Accept_PascalCase()
	{
		await Executar(Servidor(corpo: "{\"Id\":1,\"Nome\":\"Ana\"}")).ValidateContract<Cliente>();
	}

	[Fact(DisplayName = "The builder's extract helpers should hand back the result and values")]
	public async Task Builder_Extract_Helpers()
	{
		var validacao = await Executar(Servidor());

		validacao
			.Extract(out HttpStepResult resultado)
			.ExtractJsonPath("$.nome", out string nome)
			.AssertJsonPath<int>("$.id", id => id.ShouldBe(1));

		resultado.StatusCode.ShouldBe(200);
		nome.ShouldBe("Ana");
		Should.Throw<ArgumentNullException>(() => validacao.Extract((Action<HttpStepResult>)null!));
		Should.Throw<ArgumentNullException>(() => validacao.AssertEach(null!));
	}

	[Fact(DisplayName = "A null Task should be refused with the parameter's name")]
	public async Task Null_Task_Should_Be_Refused()
	{
		Task<HttpValidationBuilder> nula = null!;

		(await Should.ThrowAsync<ArgumentNullException>(() => nula.AssertSuccess())).ParamName.ShouldBe("validation");
	}
}
