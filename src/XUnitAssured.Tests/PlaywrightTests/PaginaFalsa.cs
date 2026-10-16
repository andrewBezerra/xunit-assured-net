using Microsoft.Playwright;

using NSubstitute;

using XUnitAssured.Core.Abstractions;
using XUnitAssured.Core.DSL;
using XUnitAssured.Playwright.Configuration;
using XUnitAssured.Playwright.Extensions;

namespace XUnitAssured.Tests.PlaywrightTests;

/// <summary>
/// Uma página do Playwright sem navegador: um <see cref="IPage"/> falso do NSubstitute, com um
/// único <see cref="ILocator"/> falso devolvido por toda forma de localizar.
///
/// <para>
/// O que estes testes verificam é o que o pacote faz com o que o navegador responde -- a
/// asserção que passa ou falha, a mensagem que o usuário lê, a chamada que chega ao Playwright.
/// Se o navegador de verdade responde assim é contrato do Playwright, e é coberto pelos testes
/// marcados com <c>Requires=Browser</c>.
/// </para>
/// </summary>
internal sealed class PaginaFalsa
{
	public IPage Page { get; } = Substitute.For<IPage>();

	public ILocator Locator { get; } = Substitute.For<ILocator>();

	public IBrowserContext Contexto { get; } = Substitute.For<IBrowserContext>();

	public PaginaFalsa(string url = "https://app.test/inicio", string titulo = "Início")
	{
		Page.Url.Returns(url);
		Page.TitleAsync().Returns(titulo);
		Page.Context.Returns(Contexto);

		Page.Locator(Arg.Any<string>(), Arg.Any<PageLocatorOptions?>()).Returns(Locator);
		Page.GetByRole(Arg.Any<AriaRole>(), Arg.Any<PageGetByRoleOptions?>()).Returns(Locator);
		Page.GetByText(Arg.Any<string>(), Arg.Any<PageGetByTextOptions?>()).Returns(Locator);
		Page.GetByLabel(Arg.Any<string>(), Arg.Any<PageGetByLabelOptions?>()).Returns(Locator);
		Page.GetByPlaceholder(Arg.Any<string>(), Arg.Any<PageGetByPlaceholderOptions?>()).Returns(Locator);
		Page.GetByAltText(Arg.Any<string>(), Arg.Any<PageGetByAltTextOptions?>()).Returns(Locator);
		Page.GetByTitle(Arg.Any<string>(), Arg.Any<PageGetByTitleOptions?>()).Returns(Locator);
		Page.GetByTestId(Arg.Any<string>()).Returns(Locator);
	}

	/// <summary>
	/// Um cenário com esta página no contexto, como o deixam o PlaywrightTestBase e o
	/// PlaywrightPageSession.
	/// </summary>
	public ITestScenario Cenario(PlaywrightSettings? settings = null)
	{
		var cenario = ScenarioDsl.Given();
		cenario.Context.SetProperty("_PlaywrightPage", Page);
		if (settings != null)
			cenario.Context.SetProperty("_PlaywrightSettings", settings);
		return cenario;
	}

	/// <summary>
	/// O construtor de asserções de um passo que já rodou nesta página.
	/// </summary>
	public PlaywrightValidationBuilder Construtor() =>
		Cenario().NavigateTo("https://app.test/inicio").ExecuteAsync().GetAwaiter().GetResult();
}
