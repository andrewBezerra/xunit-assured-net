using System.Text.Json;

using Microsoft.Playwright;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using XUnitAssured.Playwright.Abstractions;
using XUnitAssured.Playwright.Extensions;
using XUnitAssured.Playwright.Results;
using XUnitAssured.Playwright.Steps;

namespace XUnitAssured.Tests.PlaywrightTests;

[Trait("Category", "Playwright")]
[Trait("Component", "Network")]
/// <summary>
/// InterceptRoute, Reload, FetchFromPage e ExpectFetchStatus sem navegador: o que a cadeia
/// registra, o que o passo pede ao Playwright e o que o resultado conta depois.
///
/// <para>
/// O comportamento com navegador de verdade (SameSite, CORS) está em
/// <see cref="RedeDaPaginaTests"/>, que só roda onde há navegador. Estes rodam em qualquer lugar,
/// inclusive no filtro de CI.
/// </para>
/// </summary>
public class RedeDaPaginaSemNavegadorTests
{
	private readonly PaginaFalsa _pagina = new();

	private static async Task<PlaywrightStepResult> Executar(IBrowserScenario cadeia)
	{
		await cadeia.ExecuteAsync();
		return cadeia.CurrentStep!.Result.ShouldBeOfType<PlaywrightStepResult>();
	}

	private static PageAction AcaoUnica(IBrowserScenario cadeia) =>
		cadeia.CurrentStep.ShouldBeOfType<PlaywrightStep>().Actions.ShouldHaveSingleItem();

	// ──────────────────────────────────────────────
	// O que a cadeia registra
	// ──────────────────────────────────────────────

	[Fact(DisplayName = "InterceptRoute should record the pattern, status, times and body")]
	public void InterceptRoute_Should_Record_Action()
	{
		var acao = AcaoUnica(XUnitAssured.Core.DSL.ScenarioDsl.Given()
			.InterceptRoute("**/api/**", status: 401, times: 3, body: "{\"erro\":\"expirado\"}"));

		acao.ActionType.ShouldBe(PageActionType.InterceptRoute);
		acao.Value.ShouldBe("**/api/**");
		acao.Status.ShouldBe(401);
		acao.Times.ShouldBe(3);
		acao.SecondValue.ShouldBe("{\"erro\":\"expirado\"}");
	}

	[Theory(DisplayName = "InterceptRoute should reject a blank pattern")]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("  ")]
	public void InterceptRoute_Should_Reject_Blank_Pattern(string? padrao)
	{
		Should.Throw<ArgumentException>(() => XUnitAssured.Core.DSL.ScenarioDsl.Given().InterceptRoute(padrao!, 500))
			.ParamName.ShouldBe("urlPattern");
	}

	[Fact(DisplayName = "InterceptRoute should reject a route that answers no request")]
	public void InterceptRoute_Should_Reject_Zero_Times()
	{
		Should.Throw<ArgumentOutOfRangeException>(() => XUnitAssured.Core.DSL.ScenarioDsl.Given().InterceptRoute("**/*", 500, times: 0))
			.ParamName.ShouldBe("times");
	}

	[Fact(DisplayName = "Reload should record a reload action")]
	public void Reload_Should_Record_Action()
	{
		AcaoUnica(XUnitAssured.Core.DSL.ScenarioDsl.Given().Reload()).ActionType.ShouldBe(PageActionType.Reload);
	}

	[Fact(DisplayName = "FetchFromPage should record the URL, the method and the body")]
	public void FetchFromPage_Should_Record_Action()
	{
		var corpo = new { Usuario = "ana" };

		var acao = AcaoUnica(XUnitAssured.Core.DSL.ScenarioDsl.Given().FetchFromPage("/api/login", "POST", corpo));

		acao.ActionType.ShouldBe(PageActionType.FetchFromPage);
		acao.ResolveValue().ShouldBe("/api/login");
		acao.SecondValue.ShouldBe("POST");
		acao.BodyProvider!().ShouldBeSameAs(corpo);
	}

	[Fact(DisplayName = "FetchFromPage without a body should record no body, and GET by default")]
	public void FetchFromPage_Without_Body_Should_Default_To_Get()
	{
		var acao = AcaoUnica(XUnitAssured.Core.DSL.ScenarioDsl.Given().FetchFromPage("/api/eu"));

		acao.SecondValue.ShouldBe("GET");
		acao.BodyProvider.ShouldBeNull();
	}

	[Fact(DisplayName = "FetchFromPage should reject a blank URL and a null URL builder")]
	public void FetchFromPage_Should_Reject_Missing_Url()
	{
		Should.Throw<ArgumentException>(() => XUnitAssured.Core.DSL.ScenarioDsl.Given().FetchFromPage(" "))
			.ParamName.ShouldBe("url");
		Should.Throw<ArgumentNullException>(() => XUnitAssured.Core.DSL.ScenarioDsl.Given().FetchFromPage((Func<string>)null!))
			.ParamName.ShouldBe("url");
	}

	[Fact(DisplayName = "FetchFromPage with builders should defer the URL and the body to execution")]
	public void FetchFromPage_With_Builders_Should_Defer()
	{
		var token = "antes";

		var acao = AcaoUnica(XUnitAssured.Core.DSL.ScenarioDsl.Given()
			.FetchFromPage(() => $"/api/{token}", "PUT", () => new { token }));
		token = "depois";

		acao.ResolveValue().ShouldBe("/api/depois");
		acao.SecondValue.ShouldBe("PUT");
		JsonSerializer.Serialize(acao.BodyProvider!()).ShouldBe("{\"token\":\"depois\"}");
	}

	[Fact(DisplayName = "ExpectFetchStatus should reject a null scenario")]
	public void ExpectFetchStatus_Should_Reject_Null_Scenario()
	{
		Should.Throw<ArgumentNullException>(() => PageNetworkExtensions.ExpectFetchStatus(null!, 200));
	}

	// ──────────────────────────────────────────────
	// Execução: interceptar
	// ──────────────────────────────────────────────

	[Fact(DisplayName = "An intercepted route should answer the first requests, let the rest through and be removed after the step")]
	public async Task InterceptRoute_Should_Answer_Then_Fall_Back_And_Unroute()
	{
		Func<IRoute, Task>? manipulador = null;
		_pagina.Page.RouteAsync("**/api/**", Arg.Do<Func<IRoute, Task>>(h => manipulador = h), Arg.Any<PageRouteOptions?>())
			.Returns(Task.CompletedTask);

		var rotas = Enumerable.Range(1, 3).Select(i =>
		{
			var rota = Substitute.For<IRoute>();
			var requisicao = Substitute.For<IRequest>();
			requisicao.Url.Returns($"https://api.test/api/{i}");
			rota.Request.Returns(requisicao);
			return rota;
		}).ToList();

		// A página recarrega e faz três pedidos que casam com a rota.
		_pagina.Page.ReloadAsync(Arg.Any<PageReloadOptions?>()).Returns(async _ =>
		{
			foreach (var rota in rotas)
				await manipulador!(rota);
			return (IResponse?)null;
		});

		var cadeia = _pagina.Cenario()
			.InterceptRoute("**/api/**", status: 401, times: 2, body: "{\"erro\":1}")
			.Reload();
		var resultado = await Executar(cadeia);

		resultado.InterceptedRequests.ShouldBe(new[] { "https://api.test/api/1", "https://api.test/api/2" });
		await rotas[0].Received().FulfillAsync(Arg.Is<RouteFulfillOptions?>(o =>
			o!.Status == 401 && o.Body == "{\"erro\":1}" && o.ContentType == "application/json"));
		await rotas[2].DidNotReceive().FulfillAsync(Arg.Any<RouteFulfillOptions?>());
		await rotas[2].Received().FallbackAsync(Arg.Any<RouteFallbackOptions?>());
		await _pagina.Page.Received(1).UnrouteAsync("**/api/**", manipulador!);

		new PlaywrightValidationBuilder(cadeia)
			.AssertIntercepted(atLeast: 2)
			.AssertIntercepted("/api/1")
			.AssertIntercepted("api/*", atLeast: 2);
	}

	[Fact(DisplayName = "An intercepted route without a body should answer an empty body with no content type")]
	public async Task InterceptRoute_Without_Body_Should_Answer_Empty()
	{
		Func<IRoute, Task>? manipulador = null;
		_pagina.Page.RouteAsync(Arg.Any<string>(), Arg.Do<Func<IRoute, Task>>(h => manipulador = h), Arg.Any<PageRouteOptions?>())
			.Returns(Task.CompletedTask);
		var rota = Substitute.For<IRoute>();
		rota.Request.Url.Returns("https://api.test/x");
		_pagina.Page.ReloadAsync(Arg.Any<PageReloadOptions?>()).Returns(async _ =>
		{
			await manipulador!(rota);
			return (IResponse?)null;
		});

		await Executar(_pagina.Cenario().InterceptRoute("**/x", status: 503).Reload());

		await rota.Received().FulfillAsync(Arg.Is<RouteFulfillOptions?>(o =>
			o!.Status == 503 && o.Body == string.Empty && o.ContentType == null));
	}

	[Fact(DisplayName = "AssertIntercepted should fail with a hint about service workers when nothing was answered")]
	public async Task AssertIntercepted_Should_Hint_Service_Workers()
	{
		var cadeia = _pagina.Cenario().InterceptRoute("**/api/**", status: 401).Reload();
		await Executar(cadeia);

		var erro = Should.Throw<ShouldAssertException>(() => new PlaywrightValidationBuilder(cadeia).AssertIntercepted("/api/"));

		erro.Message.ShouldContain("but it answered 0");
		erro.Message.ShouldContain("matching '/api/'");
		erro.Message.ShouldContain("BlockServiceWorkers");
		erro.Message.ShouldContain("The step recorded no requests at all.");
	}

	[Fact(DisplayName = "Routes should be removed even when an action fails, tolerating a page that already closed")]
	public async Task Routes_Should_Be_Removed_On_Failure()
	{
		_pagina.Page.ReloadAsync(Arg.Any<PageReloadOptions?>()).ThrowsAsync(new PlaywrightException("navigation failed"));
		_pagina.Page.UnrouteAsync(Arg.Any<string>(), Arg.Any<Func<IRoute, Task>>())
			.ThrowsAsync(new PlaywrightException("Target page has been closed"));

		var resultado = await Executar(_pagina.Cenario().InterceptRoute("**/api/**", status: 500).Reload());

		resultado.Success.ShouldBeFalse();
		resultado.Errors.ShouldContain("navigation failed");
		await _pagina.Page.Received(1).UnrouteAsync("**/api/**", Arg.Any<Func<IRoute, Task>>());
	}

	// ──────────────────────────────────────────────
	// Execução: requisição de dentro da página
	// ──────────────────────────────────────────────

	[Fact(DisplayName = "FetchFromPage should send the body as camelCase JSON and record the response")]
	public async Task FetchFromPage_Should_Send_CamelCase_And_Record_Response()
	{
		object?[]? argumentos = null;
		var resposta = JsonDocument.Parse("{\"status\":201,\"body\":\"{\\\"accessToken\\\":\\\"abc\\\",\\\"expira\\\":60}\"}").RootElement.Clone();
		_pagina.Page.EvaluateAsync<JsonElement>(Arg.Any<string>(), Arg.Do<object?>(a => argumentos = (object?[]?)a))
			.Returns(resposta);

		var cadeia = _pagina.Cenario().FetchFromPage("https://api.test/auth", "POST", new { NomeDoUsuario = "ana" });
		var resultado = await Executar(cadeia);

		argumentos.ShouldNotBeNull();
		argumentos![0].ShouldBe("https://api.test/auth");
		argumentos[1].ShouldBe("POST");
		argumentos[2].ShouldBe("{\"nomeDoUsuario\":\"ana\"}");

		resultado.LastFetch.ShouldBe(new PageFetch("https://api.test/auth", "POST", 201, "{\"accessToken\":\"abc\",\"expira\":60}"));

		var construtor = new PlaywrightValidationBuilder(cadeia)
			.AssertFetchStatus(201)
			.AssertFetchJsonPath<string>("$.accessToken", t => t == "abc");
		construtor.FetchJsonPath<int>("$.expira").ShouldBe(60);

		Should.Throw<ShouldAssertException>(() => construtor.AssertFetchStatus(200))
			.Message.ShouldContain("Expected POST https://api.test/auth from the page to answer 200, but it answered 201");
		Should.Throw<ShouldAssertException>(() => construtor.AssertFetchJsonPath<int>("$.expira", e => e > 300, "token expira cedo demais"))
			.Message.ShouldContain("token expira cedo demais. Body:");
		Should.Throw<ShouldAssertException>(() => construtor.AssertFetchJsonPath<int>("$.expira", e => e > 300))
			.Message.ShouldContain("$.expira did not satisfy the condition");
	}

	[Fact(DisplayName = "FetchFromPage without a body should send null and a status of 0 should be recorded as is")]
	public async Task FetchFromPage_Without_Body_And_Status_Zero()
	{
		object?[]? argumentos = null;
		var resposta = JsonDocument.Parse("{\"status\":0,\"body\":\"TypeError: Failed to fetch\"}").RootElement.Clone();
		_pagina.Page.EvaluateAsync<JsonElement>(Arg.Any<string>(), Arg.Do<object?>(a => argumentos = (object?[]?)a))
			.Returns(resposta);

		var cadeia = _pagina.Cenario().FetchFromPage("https://outra-origem.test/dados");
		var resultado = await Executar(cadeia);

		argumentos![1].ShouldBe("GET");
		argumentos[2].ShouldBeNull();
		resultado.LastFetch!.Status.ShouldBe(0);
		Should.Throw<ShouldAssertException>(() => new PlaywrightValidationBuilder(cadeia).AssertFetchStatus(200))
			.Message.ShouldContain("Failed to fetch");
	}

	[Fact(DisplayName = "Fetch assertions on a step without FetchFromPage should say there is no response")]
	public async Task Fetch_Assertions_Without_Fetch_Should_Explain()
	{
		var cadeia = _pagina.Cenario().Reload();
		await Executar(cadeia);
		var construtor = new PlaywrightValidationBuilder(cadeia);

		Should.Throw<InvalidOperationException>(() => construtor.AssertFetchStatus(200))
			.Message.ShouldContain("made no request with FetchFromPage");
		Should.Throw<InvalidOperationException>(() => construtor.FetchJsonPath<string>("$.x"));
	}

	// ──────────────────────────────────────────────
	// ExpectFetchStatus: verificação no meio do cenário
	// ──────────────────────────────────────────────

	[Fact(DisplayName = "ExpectFetchStatus should pass when the status is one of the accepted ones")]
	public async Task ExpectFetchStatus_Should_Accept_Any_Listed_Status()
	{
		var resposta = JsonDocument.Parse("{\"status\":204,\"body\":\"\"}").RootElement.Clone();
		_pagina.Page.EvaluateAsync<JsonElement>(Arg.Any<string>(), Arg.Any<object?>()).Returns(resposta);

		var cadeia = _pagina.Cenario().FetchFromPage("/api/sair", "POST").ExpectFetchStatus(200, 204);

		var resultado = await Executar(cadeia);

		resultado.LastFetch!.Status.ShouldBe(204);
	}

	[Fact(DisplayName = "ExpectFetchStatus should stop the scenario listing the accepted codes when the status is another")]
	public async Task ExpectFetchStatus_Should_Fail_Listing_Accepted_Codes()
	{
		var resposta = JsonDocument.Parse("{\"status\":401,\"body\":\"negado\"}").RootElement.Clone();
		_pagina.Page.EvaluateAsync<JsonElement>(Arg.Any<string>(), Arg.Any<object?>()).Returns(resposta);

		var cadeia = _pagina.Cenario().FetchFromPage("/api/renovar", "POST").ExpectFetchStatus(200, 201, 204);

		// Should.ThrowAsync deixa passar uma ShouldAssertException em vez de capturá-la.
		ShouldAssertException? erro = null;
		try
		{
			await cadeia.ExecuteAsync();
		}
		catch (ShouldAssertException e)
		{
			erro = e;
		}

		erro.ShouldNotBeNull("the scenario should stop at the unexpected status");
		erro!.Message.ShouldContain("to answer 200, 201 or 204, but it answered 401. Body: negado");
	}

	[Fact(DisplayName = "ExpectFetchStatus after a step without FetchFromPage should say there is no status to check")]
	public async Task ExpectFetchStatus_Without_Fetch_Should_Explain()
	{
		var cadeia = _pagina.Cenario().Reload().ExpectFetchStatus(200);

		var erro = await Should.ThrowAsync<InvalidOperationException>(() => cadeia.ExecuteAsync());

		erro.Message.ShouldContain("ExpectFetchStatus follows a step that made no request with FetchFromPage");
	}

	[Fact(DisplayName = "ExpectFetchStatus on a step that is not a browser step should name the result type it got")]
	public void ExpectFetchStatus_On_Other_Step_Should_Name_Result_Type()
	{
		var cenario = XUnitAssured.Core.DSL.ScenarioDsl.Given();
		cenario.SetCurrentStep(new PassoDeOutroTipo());

		Should.Throw<InvalidOperationException>(() => cenario.ExpectFetchStatus(200))
			.Message.ShouldContain("Expected PlaywrightStepResult but got TestStepResult");
	}

	/// <summary>Um passo de outro pacote, já executado.</summary>
	private sealed class PassoDeOutroTipo : XUnitAssured.Core.Abstractions.ITestStep
	{
		public string? Name => null;
		public string StepType => "Outro";
		public XUnitAssured.Core.Results.ITestStepResult? Result { get; } =
			new XUnitAssured.Core.Results.TestStepResult { Success = true };
		public bool IsExecuted => true;
		public bool IsValid => true;

		public Task<XUnitAssured.Core.Results.ITestStepResult> ExecuteAsync(
			XUnitAssured.Core.Abstractions.ITestContext context, CancellationToken cancellationToken = default) =>
			Task.FromResult(Result!);

		public void Validate(Action<XUnitAssured.Core.Results.ITestStepResult> validation) => validation(Result!);
	}
}
