using System.IO;
using System.Text.Json;

using Microsoft.Playwright;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using XUnitAssured.Playwright.Abstractions;
using XUnitAssured.Playwright.Configuration;
using XUnitAssured.Playwright.Extensions;
using XUnitAssured.Playwright.Locators;
using XUnitAssured.Playwright.Results;
using XUnitAssured.Playwright.Steps;

namespace XUnitAssured.Tests.PlaywrightTests;

[Trait("Category", "Playwright")]
[Trait("Component", "Steps")]
/// <summary>
/// O <see cref="PlaywrightStep"/> executando de verdade, contra uma página falsa: cada verbo da
/// DSL tem de chegar ao Playwright como a chamada certa, com as opções certas, e o resultado do
/// passo tem de contar o que aconteceu -- URL, título, console, requisições, capturas de tela e,
/// quando algo falha, o erro original.
/// </summary>
public class ExecucaoDoPassoComPaginaFalsaTests
{
	private readonly PaginaFalsa _pagina = new();

	private static async Task<PlaywrightStepResult> Executar(IBrowserScenario cadeia)
	{
		await cadeia.ExecuteAsync();
		return cadeia.CurrentStep!.Result.ShouldBeOfType<PlaywrightStepResult>();
	}

	// ──────────────────────────────────────────────
	// Resultado
	// ──────────────────────────────────────────────

	[Fact(DisplayName = "A successful step should report the page URL, title and lazily the page content")]
	public async Task Success_Should_Report_Url_Title_And_Lazy_Content()
	{
		_pagina.Page.ContentAsync().Returns("<html><body>Painel</body></html>");

		var resultado = await Executar(_pagina.Cenario().NavigateTo("https://app.test/inicio"));

		resultado.Success.ShouldBeTrue();
		resultado.Url.ShouldBe("https://app.test/inicio");
		resultado.Title.ShouldBe("Início");
		await _pagina.Page.DidNotReceive().ContentAsync();
		resultado.PageContent.ShouldBe("<html><body>Painel</body></html>");
	}

	[Fact(DisplayName = "PageContent should be null, not an exception, once the page is closed")]
	public async Task PageContent_Should_Be_Null_When_Page_Closed()
	{
		_pagina.Page.ContentAsync().ThrowsAsync(new PlaywrightException("Target page has been closed"));

		var resultado = await Executar(_pagina.Cenario().NavigateTo("https://app.test/inicio"));

		resultado.PageContent.ShouldBeNull();
	}

	[Fact(DisplayName = "The step should record console messages and requests made while it ran, and stop listening after")]
	public async Task Should_Record_Console_And_Requests_While_Running()
	{
		var mensagem = Substitute.For<IConsoleMessage>();
		mensagem.Type.Returns("error");
		mensagem.Text.Returns("Falha ao carregar");
		var requisicao = Substitute.For<IRequest>();
		requisicao.Url.Returns("https://api.test/v1/pedidos");

		_pagina.Locator.ClickAsync(Arg.Any<LocatorClickOptions?>()).Returns(_ =>
		{
			_pagina.Page.Console += Raise.Event<EventHandler<IConsoleMessage>>(_pagina.Page, mensagem);
			_pagina.Page.Request += Raise.Event<EventHandler<IRequest>>(_pagina.Page, requisicao);
			return Task.CompletedTask;
		});

		var cadeia = _pagina.Cenario().Click("#carregar");
		var resultado = await Executar(cadeia);

		resultado.ConsoleLogs.ShouldBe(new[] { "[error] Falha ao carregar" });
		resultado.Requests.ShouldBe(new[] { "https://api.test/v1/pedidos" });

		// Depois do passo, o que a página fizer não é mais dele.
		_pagina.Page.Console += Raise.Event<EventHandler<IConsoleMessage>>(_pagina.Page, mensagem);
		new PlaywrightValidationBuilder(cadeia).AssertConsoleLogCount(1).AssertRequestedOnce("/v1/pedidos");
	}

	[Fact(DisplayName = "A step with no page anywhere should fail saying how to provide one")]
	public async Task Without_Page_Should_Fail_With_Guidance()
	{
		var cadeia = XUnitAssured.Core.DSL.ScenarioDsl.Given().NavigateTo("https://app.test");

		var resultado = await Executar(cadeia);

		resultado.Success.ShouldBeFalse();
		resultado.Errors.ShouldHaveSingleItem().ShouldContain("No Playwright page available");
		resultado.Url.ShouldBeNull();
	}

	[Fact(DisplayName = "A failing action should fail the step with the original error and the page URL")]
	public async Task Failing_Action_Should_Keep_Original_Error()
	{
		_pagina.Locator.ClickAsync(Arg.Any<LocatorClickOptions?>())
			.ThrowsAsync(new TimeoutException("Timeout 30000ms exceeded waiting for #enviar"));

		var resultado = await Executar(_pagina.Cenario().Click("#enviar").Fill("#nome", "Ana"));

		resultado.Success.ShouldBeFalse();
		resultado.Errors.ShouldContain(e => e.Contains("Timeout 30000ms exceeded"));
		resultado.Url.ShouldBe("https://app.test/inicio");
		resultado.Screenshots.ShouldBeEmpty();
		await _pagina.Locator.DidNotReceive().FillAsync(Arg.Any<string>(), Arg.Any<LocatorFillOptions?>());
	}

	[Fact(DisplayName = "ScreenshotOnFailure should capture the failing page and add it to the result")]
	public async Task ScreenshotOnFailure_Should_Capture_Failing_Page()
	{
		var pasta = Path.Combine(Path.GetTempPath(), "xua-pw-" + Guid.NewGuid().ToString("N"));
		try
		{
			_pagina.Locator.ClickAsync(Arg.Any<LocatorClickOptions?>()).ThrowsAsync(new PlaywrightException("detached"));
			var settings = new PlaywrightSettings { ScreenshotOnFailure = true, ScreenshotPath = pasta };

			var resultado = await Executar(_pagina.Cenario(settings).Click("#enviar"));

			resultado.Success.ShouldBeFalse();
			var captura = resultado.Screenshots.ShouldHaveSingleItem();
			Path.GetDirectoryName(captura).ShouldBe(pasta);
			Path.GetFileName(captura).ShouldStartWith("failure_");
			Directory.Exists(pasta).ShouldBeTrue();
			await _pagina.Page.Received().ScreenshotAsync(Arg.Is<PageScreenshotOptions?>(o => o!.Path == captura && o.FullPage == true));
		}
		finally
		{
			if (Directory.Exists(pasta))
				Directory.Delete(pasta, recursive: true);
		}
	}

	[Fact(DisplayName = "A failing failure screenshot should not hide the original error, nor a closed page its URL")]
	public async Task Failure_Screenshot_Error_Should_Not_Hide_Original()
	{
		var pasta = Path.Combine(Path.GetTempPath(), "xua-pw-" + Guid.NewGuid().ToString("N"));
		try
		{
			_pagina.Locator.ClickAsync(Arg.Any<LocatorClickOptions?>()).ThrowsAsync(new InvalidOperationException("erro original"));
			_pagina.Page.ScreenshotAsync(Arg.Any<PageScreenshotOptions?>()).ThrowsAsync(new PlaywrightException("page closed"));
			_pagina.Page.Url.Returns(_ => throw new PlaywrightException("page closed"));
			var settings = new PlaywrightSettings { ScreenshotOnFailure = true, ScreenshotPath = pasta };

			var resultado = await Executar(_pagina.Cenario(settings).Click("#enviar"));

			resultado.Errors.ShouldContain("erro original");
			resultado.Screenshots.ShouldBeEmpty();
			resultado.Url.ShouldBeNull();
		}
		finally
		{
			if (Directory.Exists(pasta))
				Directory.Delete(pasta, recursive: true);
		}
	}

	[Fact(DisplayName = "Validate before the step runs should say the step has not been executed")]
	public void Validate_Before_Execution_Should_Throw()
	{
		var passo = new PlaywrightStep();

		Should.Throw<InvalidOperationException>(() => passo.Validate(_ => { }))
			.Message.ShouldContain("Step has not been executed");
	}

	[Fact(DisplayName = "A Page set on the step should take precedence over the one in the context")]
	public async Task Step_Page_Should_Take_Precedence()
	{
		var outra = new PaginaFalsa(url: "https://outra.test/", titulo: "Outra");
		var passo = new PlaywrightStep { Page = outra.Page };
		var contexto = _pagina.Cenario().Context;

		var resultado = (PlaywrightStepResult)await passo.ExecuteAsync(contexto);

		resultado.Url.ShouldBe("https://outra.test/");
		resultado.Title.ShouldBe("Outra");
		passo.IsExecuted.ShouldBeTrue();
		passo.IsValid.ShouldBeTrue();
	}

	// ──────────────────────────────────────────────
	// Navegação
	// ──────────────────────────────────────────────

	[Theory(DisplayName = "NavigateTo should resolve a relative address against BaseUrl and keep absolute ones")]
	[InlineData("https://app.test/", "/pedidos", "https://app.test/pedidos")]
	[InlineData("https://app.test", "pedidos", "https://app.test/pedidos")]
	[InlineData("https://app.test/", "https://outro.test/x", "https://outro.test/x")]
	[InlineData(null, "/pedidos", "/pedidos")]
	public async Task NavigateTo_Should_Resolve_Url(string? baseUrl, string endereco, string esperado)
	{
		var settings = new PlaywrightSettings { BaseUrl = baseUrl, NavigationTimeout = 1234 };

		await Executar(_pagina.Cenario(settings).NavigateTo(endereco));

		await _pagina.Page.Received().GotoAsync(esperado, Arg.Is<PageGotoOptions?>(o => o!.Timeout == 1234));
	}

	[Fact(DisplayName = "NavigateTo with a function should build the address when the step runs")]
	public async Task NavigateTo_Func_Should_Build_At_Execution()
	{
		var id = "1";
		var cadeia = _pagina.Cenario().NavigateTo(() => $"https://app.test/pedidos/{id}");
		id = "42";

		await Executar(cadeia);

		await _pagina.Page.Received().GotoAsync("https://app.test/pedidos/42", Arg.Any<PageGotoOptions?>());
	}

	[Fact(DisplayName = "Reload should wait for the network to settle within the navigation timeout")]
	public async Task Reload_Should_Wait_For_Network_Idle()
	{
		await Executar(_pagina.Cenario(new PlaywrightSettings { NavigationTimeout = 5000 }).Reload());

		await _pagina.Page.Received().ReloadAsync(Arg.Is<PageReloadOptions?>(o =>
			o!.WaitUntil == WaitUntilState.NetworkIdle && o.Timeout == 5000));
	}

	// ──────────────────────────────────────────────
	// Cliques
	// ──────────────────────────────────────────────

	[Fact(DisplayName = "A plain Click should pass no options to Playwright")]
	public async Task Click_Should_Pass_No_Options()
	{
		await Executar(_pagina.Cenario().Click("#ok"));

		await _pagina.Locator.Received().ClickAsync(null);
	}

	[Fact(DisplayName = "ForceClick should click with Force and no modifiers")]
	public async Task ForceClick_Should_Set_Force()
	{
		await Executar(_pagina.Cenario().ForceClick("#ok"));

		await _pagina.Locator.Received().ClickAsync(Arg.Is<LocatorClickOptions?>(o => o!.Force == true && o.Modifiers == null));
	}

	[Fact(DisplayName = "ClickWithModifiers should pass the known modifiers, case-insensitively, and drop unknown ones")]
	public async Task ClickWithModifiers_Should_Parse_Modifiers()
	{
		await Executar(_pagina.Cenario().ClickWithModifiers("#item", "control", "Shift", "Hyper"));

		await _pagina.Locator.Received().ClickAsync(Arg.Is<LocatorClickOptions?>(o =>
			o!.Force == null &&
			o.Modifiers!.SequenceEqual(new[] { KeyboardModifier.Control, KeyboardModifier.Shift })));
	}

	[Fact(DisplayName = "DoubleClick should double-click the located element")]
	public async Task DoubleClick_Should_DblClick()
	{
		await Executar(_pagina.Cenario().DoubleClickByText("Editar"));

		await _pagina.Locator.Received().DblClickAsync(Arg.Is<LocatorDblClickOptions?>(o => o!.Force == null && o.Modifiers == null));
	}

	[Fact(DisplayName = "RightClick should click with the right mouse button")]
	public async Task RightClick_Should_Use_Right_Button()
	{
		await Executar(_pagina.Cenario().RightClickByTestId("linha"));

		await _pagina.Locator.Received().ClickAsync(Arg.Is<LocatorClickOptions?>(o => o!.Button == MouseButton.Right));
	}

	// ──────────────────────────────────────────────
	// Campos
	// ──────────────────────────────────────────────

	[Fact(DisplayName = "Fill, TypeText, Press and Clear should reach the located field with their values")]
	public async Task Field_Actions_Should_Reach_Locator()
	{
		await Executar(_pagina.Cenario()
			.FillByLabel("Nome", "Ana")
			.TypeTextByPlaceholder("Buscar", "caneta")
			.PressByTestId("busca", "Enter")
			.ClearByRole(AriaRole.Textbox, "Observação"));

		Received.InOrder(() =>
		{
			_pagina.Locator.FillAsync("Ana", Arg.Any<LocatorFillOptions?>());
			_pagina.Locator.PressSequentiallyAsync("caneta", Arg.Any<LocatorPressSequentiallyOptions?>());
			_pagina.Locator.PressAsync("Enter", Arg.Any<LocatorPressOptions?>());
			_pagina.Locator.ClearAsync(Arg.Any<LocatorClearOptions?>());
		});
	}

	[Fact(DisplayName = "Check, Uncheck and SetChecked should set the checkbox state")]
	public async Task Checkbox_Actions_Should_Set_State()
	{
		await Executar(_pagina.Cenario()
			.CheckByLabel("Aceito")
			.UncheckByTestId("newsletter")
			.SetChecked("#a", true)
			.SetCheckedByRole(AriaRole.Checkbox, false, "B"));

		await _pagina.Locator.Received(1).CheckAsync(Arg.Any<LocatorCheckOptions?>());
		await _pagina.Locator.Received(1).UncheckAsync(Arg.Any<LocatorUncheckOptions?>());
		await _pagina.Locator.Received(1).SetCheckedAsync(true, Arg.Any<LocatorSetCheckedOptions?>());
		await _pagina.Locator.Received(1).SetCheckedAsync(false, Arg.Any<LocatorSetCheckedOptions?>());
	}

	[Fact(DisplayName = "SelectOption and SelectMultipleOptions should select the given values")]
	public async Task Select_Actions_Should_Select_Values()
	{
		await Executar(_pagina.Cenario()
			.SelectOption("#uf", "SP")
			.SelectMultipleOptionsByLabel("Cores", new[] { "azul", "verde" }));

		await _pagina.Locator.Received().SelectOptionAsync("SP", Arg.Any<LocatorSelectOptionOptions?>());
		await _pagina.Locator.Received().SelectOptionAsync(
			Arg.Is<IEnumerable<string>>(v => v.SequenceEqual(new[] { "azul", "verde" })), Arg.Any<LocatorSelectOptionOptions?>());
	}

	[Fact(DisplayName = "Hover, Focus, ScrollIntoView and DispatchEvent should reach the located element")]
	public async Task Element_Actions_Should_Reach_Locator()
	{
		await Executar(_pagina.Cenario()
			.HoverByRole(AriaRole.Menuitem, "Arquivo")
			.FocusByLabel("E-mail")
			.ScrollIntoViewByText("Rodapé")
			.DispatchEventByTestId("arrastavel", "dragstart"));

		await _pagina.Locator.Received().HoverAsync(Arg.Is<LocatorHoverOptions?>(o => o!.Force == null));
		await _pagina.Locator.Received().FocusAsync(Arg.Any<LocatorFocusOptions?>());
		await _pagina.Locator.Received().ScrollIntoViewIfNeededAsync(Arg.Any<LocatorScrollIntoViewIfNeededOptions?>());
		await _pagina.Locator.Received().DispatchEventAsync("dragstart", Arg.Any<object?>(), Arg.Any<LocatorDispatchEventOptions?>());
	}

	[Fact(DisplayName = "DragTo should drag the source onto the target locator")]
	public async Task DragTo_Should_Drag_Source_To_Target()
	{
		var origem = Substitute.For<ILocator>();
		var destino = Substitute.For<ILocator>();
		_pagina.Page.Locator("#cartao", Arg.Any<PageLocatorOptions?>()).Returns(origem);
		_pagina.Page.Locator("#coluna-feito", Arg.Any<PageLocatorOptions?>()).Returns(destino);

		await Executar(_pagina.Cenario().DragTo("#cartao", "#coluna-feito"));

		await origem.Received().DragToAsync(destino, Arg.Any<LocatorDragToOptions?>());
	}

	// ──────────────────────────────────────────────
	// Arquivos
	// ──────────────────────────────────────────────

	[Fact(DisplayName = "UploadFile should send one file, UploadFiles several, ClearUploadFile none")]
	public async Task Upload_Actions_Should_Set_Input_Files()
	{
		var umArquivo = Substitute.For<ILocator>();
		var varios = Substitute.For<ILocator>();
		var limpar = Substitute.For<ILocator>();
		_pagina.Page.Locator("#um", Arg.Any<PageLocatorOptions?>()).Returns(umArquivo);
		_pagina.Page.Locator("#varios", Arg.Any<PageLocatorOptions?>()).Returns(varios);
		_pagina.Page.Locator("#limpar", Arg.Any<PageLocatorOptions?>()).Returns(limpar);

		await Executar(_pagina.Cenario()
			.UploadFile("#um", "nota.pdf")
			.UploadFiles("#varios", new[] { "a.png", "b.png" })
			.ClearUploadFile("#limpar"));

		await umArquivo.Received().SetInputFilesAsync("nota.pdf", Arg.Any<LocatorSetInputFilesOptions?>());
		await varios.Received().SetInputFilesAsync(
			Arg.Is<IEnumerable<string>>(f => f.SequenceEqual(new[] { "a.png", "b.png" })), Arg.Any<LocatorSetInputFilesOptions?>());
		await limpar.Received().SetInputFilesAsync(Arg.Is<IEnumerable<string>>(f => !f.Any()), Arg.Any<LocatorSetInputFilesOptions?>());
	}

	[Fact(DisplayName = "UploadFiles with a single path should send it as one file")]
	public async Task UploadFiles_With_One_Path_Should_Send_One_File()
	{
		await Executar(_pagina.Cenario().UploadFilesByTestId("anexo", new[] { "unico.txt" }));

		await _pagina.Locator.Received().SetInputFilesAsync("unico.txt", Arg.Any<LocatorSetInputFilesOptions?>());
	}

	// ──────────────────────────────────────────────
	// Captura de tela, espera, pausa
	// ──────────────────────────────────────────────

	[Fact(DisplayName = "TakeScreenshot should save a full-page screenshot and list it in the result")]
	public async Task TakeScreenshot_Should_Save_And_List()
	{
		var pasta = Path.Combine(Path.GetTempPath(), "xua-pw-" + Guid.NewGuid().ToString("N"));
		try
		{
			var resultado = await Executar(_pagina.Cenario(new PlaywrightSettings { ScreenshotPath = pasta })
				.TakeScreenshot("tela.png")
				.TakeScreenshot());

			resultado.Screenshots.Count.ShouldBe(2);
			resultado.Screenshots[0].ShouldBe(Path.Combine(pasta, "tela.png"));
			Path.GetFileName(resultado.Screenshots[1]).ShouldStartWith("screenshot_");
			Directory.Exists(pasta).ShouldBeTrue();
			await _pagina.Page.Received(2).ScreenshotAsync(Arg.Is<PageScreenshotOptions?>(o => o!.FullPage == true));
		}
		finally
		{
			if (Directory.Exists(pasta))
				Directory.Delete(pasta, recursive: true);
		}
	}

	[Fact(DisplayName = "Wait and WaitForSelector should wait, the latter on the page")]
	public async Task Wait_Actions()
	{
		var resultado = await Executar(_pagina.Cenario().Wait(1).WaitForSelector(".carregado"));

		resultado.Success.ShouldBeTrue();
		await _pagina.Page.Received().WaitForSelectorAsync(".carregado", Arg.Any<PageWaitForSelectorOptions?>());
	}

	[Fact(DisplayName = "RecordAndPause should open the Playwright inspector")]
	public async Task RecordAndPause_Should_Pause_Page()
	{
		var saidaOriginal = Console.Out;
		try
		{
			var saida = new StringWriter();
			Console.SetOut(saida);

			await Executar(_pagina.Cenario().RecordAndPause());

			await _pagina.Page.Received().PauseAsync();
			saida.ToString().ShouldContain("PlaywrightCodeTranslator.Translate");
		}
		finally
		{
			Console.SetOut(saidaOriginal);
		}
	}

	// ──────────────────────────────────────────────
	// Estado do navegador
	// ──────────────────────────────────────────────

	[Fact(DisplayName = "ClearCookies, SetLocalStorage and ClearLocalStorage should act on the browser state")]
	public async Task Browser_State_Actions()
	{
		object? argumento = null;
		_ = _pagina.Page.EvaluateAsync(Arg.Is<string>(s => s.Contains("setItem")), Arg.Do<object?>(a => argumento = a));

		await Executar(_pagina.Cenario()
			.ClearCookies()
			.SetLocalStorage("idioma", "pt-BR")
			.ClearLocalStorage());

		await _pagina.Contexto.Received().ClearCookiesAsync(Arg.Any<BrowserContextClearCookiesOptions?>());
		argumento.ShouldBe(new[] { "idioma", "pt-BR" });
		await _pagina.Page.Received().EvaluateAsync("() => localStorage.clear()", Arg.Any<object?>());
	}
}
