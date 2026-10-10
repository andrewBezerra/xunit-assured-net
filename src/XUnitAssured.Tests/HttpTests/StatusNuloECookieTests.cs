using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

using XUnitAssured.Core.DSL;
using XUnitAssured.Http.Extensions;

namespace XUnitAssured.Tests.HttpTests;

/// <summary>
/// Três verificações que uma suíte real escreveu por fora da DSL: mais de um status aceito
/// (403 ou 404, de propósito), um campo presente com null em vez de ausente, e o valor de um
/// cookie que a resposta definiu, para repetir a sessão em outro cliente. O campo null ficou
/// meses no HttpClient cru porque um comentário dizia que a DSL não distinguia null de ausente.
/// </summary>
[Trait("Category", "Http")]
[Trait("Component", "Assertions")]
public class StatusNuloECookieTests
{
	private const string Corpo = """
		{"contactId":null,"name":"Ana","contact":{"phone":null},"items":[{"id":1}]}
		""";

	// ---------- Status aceitos ----------

	[Theory(DisplayName = "AssertStatusCode with several codes accepts any of them")]
	[InlineData(HttpStatusCode.Forbidden)]
	[InlineData(HttpStatusCode.NotFound)]
	public async Task Accepts_Any_Of_The_Codes(HttpStatusCode status)
	{
		var resposta = await Pedir(ServidorDeMentira.Responder(status, "{}"));

		resposta.AssertStatusCode(403, 404);
	}

	[Fact(DisplayName = "AssertStatusCode with several codes lists them when none matches, with the body")]
	public async Task Lists_The_Accepted_Codes()
	{
		var resposta = await Pedir(ServidorDeMentira.Responder(HttpStatusCode.OK, """{"id":"vazou"}"""));

		Should.Throw<ShouldAssertException>(() => resposta.AssertStatusCode(403, 404))
			.Message.ShouldContain("Expected HTTP status code 403 or 404 but got 200. Body: {\"id\":\"vazou\"}");
		Should.Throw<ShouldAssertException>(() => resposta.AssertStatusCode(400, 403, 404))
			.Message.ShouldContain("Expected HTTP status code 400, 403 or 404 but got 200");
	}

	[Fact(DisplayName = "AssertStatusCode with several codes checks every concurrent response")]
	public async Task Concurrent_Responses_Each_Accepted()
	{
		var vez = 0;
		var servidor = new ServidorDeMentira(_ => Interlocked.Increment(ref vez) % 2 == 0
			? ServidorDeMentira.Responder(HttpStatusCode.Forbidden, "{}")
			: ServidorDeMentira.Responder(HttpStatusCode.NotFound, "{}"));

		var resposta = (await ScenarioDsl.Given(new ProvedorDeMentira(servidor))
			.ApiResource("/qualquer").Get().Concurrently(4).ExecuteAsync()).Then();

		resposta.AssertStatusCode(403, 404);
		Should.Throw<ShouldAssertException>(() => resposta.AssertStatusCode(403, 401))
			.Message.ShouldContain("to get 403 or 401");
	}

	// ---------- Null ou ausente ----------

	[Theory(DisplayName = "AssertJsonPathNull passes for a field present with null")]
	[InlineData("$.contactId")]
	[InlineData("$.contact.phone")]
	public async Task Null_Passes(string caminho) =>
		(await Pedir(Json())).AssertJsonPathNull(caminho);

	[Fact(DisplayName = "AssertJsonPathNull on a missing field says it is missing, not null")]
	public async Task Null_On_Missing_Field()
	{
		var erro = Should.Throw<ShouldAssertException>(async () => (await Pedir(Json())).AssertJsonPathNull("$.email"));

		erro.Message.ShouldContain("Expected $.email to be present and null, but the response has no $.email");
		erro.Message.ShouldContain("AssertJsonPathMissing");
	}

	[Fact(DisplayName = "AssertJsonPathNull on a field with a value shows the value")]
	public async Task Null_On_A_Value() =>
		Should.Throw<ShouldAssertException>(async () => (await Pedir(Json())).AssertJsonPathNull("$.name"))
			.Message.ShouldContain("Expected $.name to be null, but it is \"Ana\"");

	[Theory(DisplayName = "AssertJsonPathMissing passes for a field that is not there")]
	[InlineData("$.email")]
	[InlineData("$.contact.email")]
	[InlineData("$.items[1]")]
	public async Task Missing_Passes(string caminho) =>
		(await Pedir(Json())).AssertJsonPathMissing(caminho);

	[Fact(DisplayName = "AssertJsonPathMissing on a field present with null says so")]
	public async Task Missing_On_Null_Field()
	{
		var erro = Should.Throw<ShouldAssertException>(async () => (await Pedir(Json())).AssertJsonPathMissing("$.contactId"));

		erro.Message.ShouldContain("Expected $.contactId to be missing, but it is there with null");
		erro.Message.ShouldContain("AssertJsonPathNull");
	}

	[Fact(DisplayName = "AssertJsonPathMissing on a field with a value shows it")]
	public async Task Missing_On_A_Value() =>
		Should.Throw<ShouldAssertException>(async () => (await Pedir(Json())).AssertJsonPathMissing("$.contact"))
			.Message.ShouldContain("Expected $.contact to be missing, but it is there: an object with 1 field(s)");

	[Fact(DisplayName = "AssertJsonPathMissing fails when an earlier part of the path is missing too, a likely typo")]
	public async Task Missing_With_A_Typo_In_The_Path() =>
		Should.Throw<ShouldAssertException>(async () => (await Pedir(Json())).AssertJsonPathMissing("$.contatc.email"))
			.Message.ShouldContain("the path already stops at $, which is an object with 4 field(s). Check the path");

	[Fact(DisplayName = "AssertJsonPathNull and AssertJsonPathMissing refuse a path with [*]")]
	public async Task Wildcard_Is_Refused()
	{
		var resposta = await Pedir(Json());

		Should.Throw<InvalidOperationException>(() => resposta.AssertJsonPathNull("$.items[*].id"));
		Should.Throw<InvalidOperationException>(() => resposta.AssertJsonPathMissing("$.items[*].id"));
	}

	// ---------- Cookie definido ----------

	[Fact(DisplayName = "SetCookie reads the cookie the response set, value and attributes")]
	public async Task SetCookie_Reads_The_Cookie()
	{
		var resultado = (await Pedir(ComCookies("sid=abc123; Path=/; HttpOnly", "tema=escuro"))).GetResult();

		var sessao = resultado.SetCookie("sid").ShouldNotBeNull();
		sessao.Value.ShouldBe("abc123");
		sessao.HttpOnly.ShouldBeTrue();
		resultado.SetCookies.Select(c => c.Name).ShouldBe(["sid", "tema"]);
	}

	[Fact(DisplayName = "SetCookie returns the last one when the name is set twice, and null when not set")]
	public async Task SetCookie_Last_Wins_And_Null()
	{
		var resultado = (await Pedir(ComCookies("sid=velho", "sid=novo"))).GetResult();

		resultado.SetCookie("sid")!.Value.ShouldBe("novo");
		resultado.SetCookie("SID").ShouldBeNull();
		resultado.SetCookie("outro").ShouldBeNull();
	}

	// ---------- Apoio ----------

	private static HttpResponseMessage Json() => ServidorDeMentira.Responder(HttpStatusCode.OK, Corpo);

	private static HttpResponseMessage ComCookies(params string[] cookies) =>
		ServidorDeMentira.Responder(HttpStatusCode.OK, "{}", "application/json",
			cookies.Select(c => ("Set-Cookie", c)).ToArray());

	private static async Task<HttpValidationBuilder> Pedir(HttpResponseMessage resposta)
	{
		var execucao = await ScenarioDsl.Given(new ProvedorDeMentira(ServidorDeMentira.Sempre(resposta)))
			.ApiResource("/qualquer")
			.Get()
			.ExecuteAsync();

		return execucao.Then();
	}
}
