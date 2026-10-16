using Microsoft.Playwright;

using NSubstitute;

using XUnitAssured.Playwright.Locators;

namespace XUnitAssured.Tests.PlaywrightTests;

[Trait("Category", "Playwright")]
[Trait("Component", "Locators")]
/// <summary>
/// <see cref="LocatorStrategy.Resolve"/>: cada estratégia tem de virar a chamada certa do
/// Playwright, com as opções certas. Os testes de <see cref="LocatorStrategyTests"/> cobrem o que
/// a estratégia guarda; estes, o que ela pede à página.
/// </summary>
public class ResolucaoDoLocalizadorTests
{
	private readonly IPage _page = Substitute.For<IPage>();
	private readonly ILocator _locator = Substitute.For<ILocator>();

	[Fact(DisplayName = "Css should resolve to page.Locator with the selector")]
	public void Css_Should_Resolve_To_Locator()
	{
		_page.Locator("#login", Arg.Any<PageLocatorOptions?>()).Returns(_locator);

		LocatorStrategy.Css("#login").Resolve(_page).ShouldBeSameAs(_locator);
	}

	[Fact(DisplayName = "Role with a name and exact: true should resolve to an exact GetByRole match")]
	public void Role_With_Name_And_Exact_Should_Be_Exact()
	{
		_page.GetByRole(AriaRole.Button, Arg.Is<PageGetByRoleOptions?>(o => o!.Name == "Salvar" && o.Exact == true))
			.Returns(_locator);

		LocatorStrategy.ByRole(AriaRole.Button, "Salvar", exact: true).Resolve(_page).ShouldBeSameAs(_locator);
	}

	// O parâmetro `exact` diz "Whether the name match should be exact", mas Resolve usa
	// `Exact || Name != null`: com um nome, a comparação é sempre exata e `exact: false` é ignorado.
	[Fact(DisplayName = "Role with a name and exact: false should match the name by substring",
		Skip = "Possível bug: LocatorStrategy.Resolve força Exact = true sempre que há Name (LocatorStrategy.cs:153), contrariando o parâmetro exact.")]
	public void Role_With_Name_And_Not_Exact_Should_Not_Be_Exact()
	{
		_page.GetByRole(AriaRole.Button, Arg.Is<PageGetByRoleOptions?>(o => o!.Name == "Salv" && o.Exact == false))
			.Returns(_locator);

		LocatorStrategy.ByRole(AriaRole.Button, "Salv", exact: false).Resolve(_page).ShouldBeSameAs(_locator);
	}

	[Fact(DisplayName = "Role without a name should resolve to a non-exact GetByRole")]
	public void Role_Without_Name_Should_Not_Be_Exact()
	{
		_page.GetByRole(AriaRole.Navigation, Arg.Is<PageGetByRoleOptions?>(o => o!.Name == null && o.Exact == false))
			.Returns(_locator);

		LocatorStrategy.ByRole(AriaRole.Navigation).Resolve(_page).ShouldBeSameAs(_locator);
	}

	[Fact(DisplayName = "Role given as a string should be parsed case-insensitively")]
	public void Role_String_Should_Parse_Ignoring_Case()
	{
		_page.GetByRole(AriaRole.Checkbox, Arg.Any<PageGetByRoleOptions?>()).Returns(_locator);

		LocatorStrategy.ByRole("checkbox").Resolve(_page).ShouldBeSameAs(_locator);
	}

	[Fact(DisplayName = "An unknown role should fail at resolution naming the role")]
	public void Unknown_Role_Should_Throw()
	{
		var estrategia = LocatorStrategy.ByRole("botao");

		var erro = Should.Throw<ArgumentException>(() => estrategia.Resolve(_page));

		erro.Message.ShouldContain("Unknown ARIA role 'botao'");
		erro.ParamName.ShouldBe("role");
	}

	[Theory(DisplayName = "Text-based strategies should pass the exact flag through")]
	[InlineData(true)]
	[InlineData(false)]
	public void Text_Strategies_Should_Pass_Exact(bool exato)
	{
		_page.GetByText("Entrar", Arg.Is<PageGetByTextOptions?>(o => o!.Exact == exato)).Returns(_locator);
		_page.GetByLabel("E-mail", Arg.Is<PageGetByLabelOptions?>(o => o!.Exact == exato)).Returns(_locator);
		_page.GetByPlaceholder("Buscar", Arg.Is<PageGetByPlaceholderOptions?>(o => o!.Exact == exato)).Returns(_locator);
		_page.GetByAltText("Logo", Arg.Is<PageGetByAltTextOptions?>(o => o!.Exact == exato)).Returns(_locator);
		_page.GetByTitle("Fechar", Arg.Is<PageGetByTitleOptions?>(o => o!.Exact == exato)).Returns(_locator);

		LocatorStrategy.ByText("Entrar", exato).Resolve(_page).ShouldBeSameAs(_locator);
		LocatorStrategy.ByLabel("E-mail", exato).Resolve(_page).ShouldBeSameAs(_locator);
		LocatorStrategy.ByPlaceholder("Buscar", exato).Resolve(_page).ShouldBeSameAs(_locator);
		LocatorStrategy.ByAltText("Logo", exato).Resolve(_page).ShouldBeSameAs(_locator);
		LocatorStrategy.ByTitle("Fechar", exato).Resolve(_page).ShouldBeSameAs(_locator);
	}

	[Fact(DisplayName = "TestId should resolve to GetByTestId")]
	public void TestId_Should_Resolve_To_GetByTestId()
	{
		_page.GetByTestId("carrinho").Returns(_locator);

		LocatorStrategy.ByTestId("carrinho").Resolve(_page).ShouldBeSameAs(_locator);
	}
}
