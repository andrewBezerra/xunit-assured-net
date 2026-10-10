using System;
using System.Net;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

using XUnitAssured.Core.DSL;
using XUnitAssured.Http.Extensions;
using XUnitAssured.Http.Results;

namespace XUnitAssured.Tests.HttpTests;

/// <summary>
/// O que a resposta diz além do status e do JSON: cabeçalhos, cookies, Problem Details e texto.
///
/// <para>
/// Numa suíte real estes eram lidos à mão. Um teste de sessão procurava "expires=" no texto do
/// Set-Cookie para provar que o cookie foi apagado -- e uma data no futuro também contém
/// "expires=". Erros RFC 7807 eram conferidos com Contains na string do corpo, em vez do
/// "detail" ou do código nas extensões.
/// </para>
///
/// Pela DSL inteira, sem rede: um servidor de mentira devolve a resposta montada aqui.
/// </summary>
[Trait("Category", "Http")]
[Trait("Component", "Assertions")]
public class AssercoesDeRespostaTests
{
	private const string Sessao = "session=abc; Path=/auth; HttpOnly; Secure; SameSite=Strict";

	// ---------- Cabeçalhos ----------

	[Fact(DisplayName = "AssertHeader matches the name case-insensitively")]
	public async Task Header_Name_Is_Case_Insensitive()
	{
		var resposta = await Pedir(Resposta(HttpStatusCode.Created, ("Location", "/orders/1")));

		resposta.AssertHeader("location", "/orders/1");
		resposta.AssertHeader("LOCATION", v => v.StartsWith("/orders/"));
	}

	[Fact(DisplayName = "AssertHeader on a missing header lists the headers that came")]
	public async Task Missing_Header_Lists_What_Came()
	{
		var resposta = await Pedir(Resposta(HttpStatusCode.OK, ("X-Request-Id", "42")));

		var erro = Should.Throw<ShouldAssertException>(() => resposta.AssertHeader("Location", "/x"));
		erro.Message.ShouldContain("X-Request-Id");
	}

	[Fact(DisplayName = "AssertNoHeader fails when the header is there")]
	public async Task No_Header_Fails_When_Present()
	{
		var resposta = await Pedir(Resposta(HttpStatusCode.OK, ("X-Powered-By", "PHP")));

		resposta.AssertNoHeader("Server");
		Should.Throw<ShouldAssertException>(() => resposta.AssertNoHeader("x-powered-by"));
	}

	// ---------- Cookies ----------

	[Fact(DisplayName = "AssertSetCookie reads the cookie's attributes")]
	public async Task Set_Cookie_Reads_Attributes()
	{
		var resposta = await Pedir(Resposta(HttpStatusCode.OK, ("Set-Cookie", "outro=1; Path=/"), ("Set-Cookie", Sessao)));

		resposta.AssertSetCookie("session", c =>
			c.Value == "abc" && c.HttpOnly && c.Secure && c.Path == "/auth" && c.SameSite == "Strict");
	}

	[Fact(DisplayName = "AssertSetCookie fails when an attribute is missing, and shows the cookie")]
	public async Task Set_Cookie_Fails_On_Missing_Attribute()
	{
		var resposta = await Pedir(Resposta(HttpStatusCode.OK, ("Set-Cookie", "session=abc; Path=/")));

		var erro = Should.Throw<ShouldAssertException>(() => resposta.AssertSetCookie("session", c => c.HttpOnly));
		erro.Message.ShouldContain("HttpOnly = False");
	}

	[Fact(DisplayName = "AssertSetCookie on a cookie that was not set lists the ones that were")]
	public async Task Set_Cookie_Missing_Lists_The_Others()
	{
		var resposta = await Pedir(Resposta(HttpStatusCode.OK, ("Set-Cookie", "outro=1")));

		var erro = Should.Throw<ShouldAssertException>(() => resposta.AssertSetCookie("session"));
		erro.Message.ShouldContain("outro");
	}

	// O Expires tem uma vírgula ("Thu, 01 Jan 1970"). É o caso que já corrompeu cabeçalhos
	// antes (5.0.1): o cookie tem de chegar inteiro para a data ser lida.
	[Fact(DisplayName = "AssertCookieCleared passes for an expiry in the past or Max-Age=0")]
	public async Task Cookie_Cleared_By_Past_Expiry_Or_Max_Age()
	{
		var porData = await Pedir(Resposta(HttpStatusCode.Unauthorized,
			("Set-Cookie", "session=; Path=/auth; Expires=Thu, 01 Jan 1970 00:00:00 GMT; HttpOnly")));
		porData.AssertCookieCleared("session");

		var porMaxAge = await Pedir(Resposta(HttpStatusCode.Unauthorized, ("Set-Cookie", "session=; Max-Age=0")));
		porMaxAge.AssertCookieCleared("session");
	}

	// O teste que um Contains("expires=") deixava passar.
	[Fact(DisplayName = "AssertCookieCleared fails for an expiry in the future")]
	public async Task Cookie_Not_Cleared_By_Future_Expiry()
	{
		var resposta = await Pedir(Resposta(HttpStatusCode.OK,
			("Set-Cookie", $"session=abc; Expires={DateTimeOffset.UtcNow.AddDays(30):R}")));

		Should.Throw<ShouldAssertException>(() => resposta.AssertCookieCleared("session"));
	}

	// ---------- Problem Details ----------

	[Fact(DisplayName = "AssertProblemDetails reads the standard fields and top-level extensions")]
	public async Task Problem_Details_With_Top_Level_Extensions()
	{
		var resposta = await Pedir(ServidorDeMentira.Responder(HttpStatusCode.Conflict,
			"""{"type":"about:blank","title":"Conflict","status":409,"detail":"Member already on a shift","code":"ScheduleConflict","conflicts":[{"index":0}]}""",
			"application/problem+json"));

		resposta.AssertProblemDetails(409, p =>
			p.Title == "Conflict"
			&& p.Detail!.Contains("already")
			&& p.Extension<string>("code") == "ScheduleConflict"
			&& p.Extensions["conflicts"].GetArrayLength() == 1);
	}

	[Fact(DisplayName = "AssertProblemDetails also reads extensions nested under 'extensions'")]
	public async Task Problem_Details_With_Nested_Extensions()
	{
		var resposta = await Pedir(ServidorDeMentira.Responder(HttpStatusCode.BadRequest,
			"""{"title":"Bad Request","status":400,"extensions":{"code":"InvalidMonth"}}""",
			"application/problem+json"));

		resposta.AssertProblemDetails(400, p => p.Extension<string>("code") == "InvalidMonth");
	}

	[Fact(DisplayName = "AssertProblemDetails fails on the wrong status, and on a body that does not match")]
	public async Task Problem_Details_Fails_On_Status_Or_Body()
	{
		var resposta = await Pedir(ServidorDeMentira.Responder(HttpStatusCode.BadRequest,
			"""{"title":"Bad Request","status":400,"code":"InvalidMonth"}""", "application/problem+json"));

		Should.Throw<ShouldAssertException>(() => resposta.AssertProblemDetails(409));

		var erro = Should.Throw<ShouldAssertException>(() =>
			resposta.AssertProblemDetails(400, p => p.Extension<string>("code") == "Outro"));
		erro.Message.ShouldContain("InvalidMonth");
	}

	// ---------- Texto do corpo ----------

	[Fact(DisplayName = "AssertBodyNotContains fails when the body leaks the text")]
	public async Task Body_Not_Contains_Catches_A_Leak()
	{
		var resposta = await Pedir(ServidorDeMentira.Responder(HttpStatusCode.NotFound,
			"""{"detail":"No account for ana@example.com"}"""));

		resposta.AssertBodyContains("No account");
		resposta.AssertBodyNotContains("convidado@example.com");
		Should.Throw<ShouldAssertException>(() => resposta.AssertBodyNotContains("ana@example.com"));
	}

	// ---------- SetCookie.Parse ----------

	[Fact(DisplayName = "SetCookie.Parse ignores attribute case and unknown attributes")]
	public void Set_Cookie_Parse()
	{
		var cookie = SetCookie.Parse("id=7; path=/a; HTTPONLY; samesite=lax; Priority=High")!;

		cookie.Name.ShouldBe("id");
		cookie.Value.ShouldBe("7");
		cookie.Path.ShouldBe("/a");
		cookie.HttpOnly.ShouldBeTrue();
		cookie.SameSite.ShouldBe("lax");
		cookie.Secure.ShouldBeFalse();

		SetCookie.Parse("sem-par").ShouldBeNull();
	}

	// ---------- Apoio ----------

	private static System.Net.Http.HttpResponseMessage Resposta(HttpStatusCode status, params (string, string)[] cabecalhos) =>
		ServidorDeMentira.Responder(status, "{}", "application/json", cabecalhos);

	private static async Task<HttpValidationBuilder> Pedir(System.Net.Http.HttpResponseMessage resposta)
	{
		var execucao = await ScenarioDsl.Given(new ProvedorDeMentira(ServidorDeMentira.Sempre(resposta)))
			.ApiResource("/qualquer")
			.Get()
			.ExecuteAsync();

		return execucao.Then();
	}
}
