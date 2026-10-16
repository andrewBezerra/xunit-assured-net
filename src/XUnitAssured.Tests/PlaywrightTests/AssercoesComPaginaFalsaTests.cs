using System.IO;

using Microsoft.Playwright;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using XUnitAssured.Playwright.Configuration;
using XUnitAssured.Playwright.Extensions;
using XUnitAssured.Playwright.Locators;

namespace XUnitAssured.Tests.PlaywrightTests;

[Trait("Category", "Playwright")]
[Trait("Component", "Validation")]
/// <summary>
/// As asserções do <see cref="PlaywrightValidationBuilder"/> que leem a página viva -- visível,
/// texto, atributo, marcado, habilitado, editável, vazio, classe, CSS, id, cookies e local
/// storage --, contra uma página falsa.
///
/// <para>
/// O que se verifica é o lado do pacote: qual localizador a asserção usa, o que ela pergunta ao
/// Playwright, quando passa, quando falha e o que a mensagem de falha diz ao usuário. Cada família
/// tem o caso que passa e o que falha; a falha é o que importa num teste de asserção.
/// </para>
/// </summary>
public class AssercoesComPaginaFalsaTests
{
	private readonly PaginaFalsa _pagina = new();

	// ──────────────────────────────────────────────
	// Visível / oculto
	// ──────────────────────────────────────────────

	[Fact(DisplayName = "AssertVisible variants should pass when the element is visible and resolve the right locator")]
	public void AssertVisible_Variants_Should_Pass_When_Visible()
	{
		_pagina.Locator.IsVisibleAsync(Arg.Any<LocatorIsVisibleOptions?>()).Returns(true);

		_pagina.Construtor()
			.AssertVisible("#menu")
			.AssertVisibleByRole(AriaRole.Button, "Salvar")
			.AssertVisibleByTestId("painel")
			.AssertVisibleByText("Bem-vindo", exact: true)
			.AssertVisibleByAltText("Logo");

		_pagina.Page.Received().Locator("#menu", Arg.Any<PageLocatorOptions?>());
		_pagina.Page.Received().GetByRole(AriaRole.Button, Arg.Is<PageGetByRoleOptions?>(o => o!.Name == "Salvar"));
		_pagina.Page.Received().GetByTestId("painel");
		_pagina.Page.Received().GetByText("Bem-vindo", Arg.Is<PageGetByTextOptions?>(o => o!.Exact == true));
		_pagina.Page.Received().GetByAltText("Logo", Arg.Is<PageGetByAltTextOptions?>(o => o!.Exact == false));
	}

	[Fact(DisplayName = "AssertVisible should fail naming the locator when the element is not visible")]
	public void AssertVisible_Should_Fail_When_Not_Visible()
	{
		_pagina.Locator.IsVisibleAsync(Arg.Any<LocatorIsVisibleOptions?>()).Returns(false);
		var construtor = _pagina.Construtor();

		Should.Throw<ShouldAssertException>(() => construtor.AssertVisible("#menu"))
			.Message.ShouldContain("Expected element 'css=#menu' to be visible");
		Should.Throw<ShouldAssertException>(() => construtor.AssertVisibleByRole(AriaRole.Button, "Salvar"))
			.Message.ShouldContain("role=Button[name=\"Salvar\"]");
		Should.Throw<ShouldAssertException>(() => construtor.AssertVisibleByTestId("painel"))
			.Message.ShouldContain("testid=painel");
	}

	[Fact(DisplayName = "AssertHidden variants should pass when the element is not visible")]
	public void AssertHidden_Variants_Should_Pass_When_Not_Visible()
	{
		_pagina.Locator.IsVisibleAsync(Arg.Any<LocatorIsVisibleOptions?>()).Returns(false);

		_pagina.Construtor()
			.AssertHidden("#modal")
			.AssertHiddenByTestId("aviso")
			.AssertHiddenByRole(AriaRole.Dialog);

		_pagina.Page.Received().GetByRole(AriaRole.Dialog, Arg.Is<PageGetByRoleOptions?>(o => o!.Name == null && o.Exact == false));
	}

	[Fact(DisplayName = "AssertHidden should fail when the element is visible")]
	public void AssertHidden_Should_Fail_When_Visible()
	{
		_pagina.Locator.IsVisibleAsync(Arg.Any<LocatorIsVisibleOptions?>()).Returns(true);
		var construtor = _pagina.Construtor();

		Should.Throw<ShouldAssertException>(() => construtor.AssertHidden("#modal"))
			.Message.ShouldContain("Expected element 'css=#modal' to be hidden");
		Should.Throw<ShouldAssertException>(() => construtor.AssertHiddenByTestId("aviso"));
		Should.Throw<ShouldAssertException>(() => construtor.AssertHiddenByRole(AriaRole.Dialog));
	}

	// ──────────────────────────────────────────────
	// Texto
	// ──────────────────────────────────────────────

	[Fact(DisplayName = "AssertText variants should pass when the text content matches exactly")]
	public void AssertText_Variants_Should_Pass_When_Equal()
	{
		_pagina.Locator.TextContentAsync(Arg.Any<LocatorTextContentOptions?>()).Returns("Pedido 42 confirmado");

		_pagina.Construtor()
			.AssertText("h1", "Pedido 42 confirmado")
			.AssertTextByRole(AriaRole.Heading, "Pedido 42 confirmado", "Confirmação")
			.AssertTextByTestId("titulo", "Pedido 42 confirmado");
	}

	[Fact(DisplayName = "AssertText should fail showing the expected and actual text")]
	public void AssertText_Should_Fail_Showing_Both_Texts()
	{
		_pagina.Locator.TextContentAsync(Arg.Any<LocatorTextContentOptions?>()).Returns("Pedido 41");
		var construtor = _pagina.Construtor();

		var erro = Should.Throw<ShouldAssertException>(() => construtor.AssertText("h1", "Pedido 42"));

		erro.Message.ShouldContain("Expected text 'Pedido 42' for element 'css=h1' but got 'Pedido 41'");
		Should.Throw<ShouldAssertException>(() => construtor.AssertTextByRole(AriaRole.Heading, "Pedido 42"));
		Should.Throw<ShouldAssertException>(() => construtor.AssertTextByTestId("titulo", "Pedido 42"));
	}

	[Fact(DisplayName = "AssertTextContains variants should pass on a substring and fail otherwise")]
	public void AssertTextContains_Variants()
	{
		_pagina.Locator.TextContentAsync(Arg.Any<LocatorTextContentOptions?>()).Returns("Total: R$ 10,00");
		var construtor = _pagina.Construtor();

		construtor.AssertTextContains(".total", "R$ 10").AssertTextContainsByTestId("total", "Total");

		Should.Throw<ShouldAssertException>(() => construtor.AssertTextContains(".total", "R$ 20"));
		Should.Throw<ShouldAssertException>(() => construtor.AssertTextContainsByTestId("total", "Frete"));
	}

	[Fact(DisplayName = "AssertTextContains should fail clearly when the element has no text content")]
	public void AssertTextContains_Should_Fail_When_Text_Is_Null()
	{
		_pagina.Locator.TextContentAsync(Arg.Any<LocatorTextContentOptions?>()).Returns((string?)null);
		var construtor = _pagina.Construtor();

		Should.Throw<ShouldAssertException>(() => construtor.AssertTextContains(".total", "R$"))
			.Message.ShouldContain("Text content for element 'css=.total' is null");
	}

	[Fact(DisplayName = "AssertTextMatches variants should apply the regex to the text content")]
	public void AssertTextMatches_Variants()
	{
		_pagina.Locator.TextContentAsync(Arg.Any<LocatorTextContentOptions?>()).Returns("Pedido #1234");
		var construtor = _pagina.Construtor();

		construtor
			.AssertTextMatches("h1", @"#\d{4}$")
			.AssertTextMatchesByTestId("titulo", @"^Pedido")
			.AssertTextMatchesByRole(AriaRole.Heading, @"\d+", "Pedido");

		Should.Throw<ShouldAssertException>(() => construtor.AssertTextMatches("h1", @"^\d+$"));
		Should.Throw<ShouldAssertException>(() => construtor.AssertTextMatchesByTestId("titulo", "Fatura"));
	}

	[Fact(DisplayName = "AssertTextMatches should fail clearly when the element has no text content")]
	public void AssertTextMatches_Should_Fail_When_Text_Is_Null()
	{
		_pagina.Locator.TextContentAsync(Arg.Any<LocatorTextContentOptions?>()).Returns((string?)null);
		var construtor = _pagina.Construtor();

		Should.Throw<ShouldAssertException>(() => construtor.AssertTextMatchesByRole(AriaRole.Heading, ".*"))
			.Message.ShouldContain("is null");
	}

	// ──────────────────────────────────────────────
	// Contagem
	// ──────────────────────────────────────────────

	[Fact(DisplayName = "AssertElementCount should compare the locator count")]
	public void AssertElementCount_Should_Compare_Count()
	{
		_pagina.Locator.CountAsync().Returns(3);
		var construtor = _pagina.Construtor();

		construtor.AssertElementCount(".card", 3);

		Should.Throw<ShouldAssertException>(() => construtor.AssertElementCount(".card", 12))
			.Message.ShouldContain("Expected 12 elements matching '.card' but found 3");
	}

	[Fact(DisplayName = "AssertElementCountByRole should compare the count of the role locator")]
	public void AssertElementCountByRole_Should_Compare_Count()
	{
		_pagina.Locator.CountAsync().Returns(2);
		var construtor = _pagina.Construtor();

		construtor.AssertElementCountByRole(AriaRole.Row, 2);

		Should.Throw<ShouldAssertException>(() => construtor.AssertElementCountByRole(AriaRole.Row, 5, "Item"))
			.Message.ShouldContain("Expected 5 elements with role 'Row' but found 2");
	}

	// ──────────────────────────────────────────────
	// Atributo, valor de campo, classe, id, CSS
	// ──────────────────────────────────────────────

	[Fact(DisplayName = "AssertAttribute variants should read the named attribute")]
	public void AssertAttribute_Variants()
	{
		_pagina.Locator.GetAttributeAsync("placeholder", Arg.Any<LocatorGetAttributeOptions?>()).Returns("Seu e-mail");
		var construtor = _pagina.Construtor();

		construtor
			.AssertAttribute("#email", "placeholder", "Seu e-mail")
			.AssertAttributeByTestId("email", "placeholder", "Seu e-mail");

		Should.Throw<ShouldAssertException>(() => construtor.AssertAttribute("#email", "placeholder", "E-mail"))
			.Message.ShouldContain("Expected attribute 'placeholder' to be 'E-mail' for element 'css=#email' but got 'Seu e-mail'");
		Should.Throw<ShouldAssertException>(() => construtor.AssertAttributeByTestId("email", "placeholder", "E-mail"));
	}

	[Fact(DisplayName = "AssertInputValue variants should read the current input value")]
	public void AssertInputValue_Variants()
	{
		_pagina.Locator.InputValueAsync(Arg.Any<LocatorInputValueOptions?>()).Returns("ana@exemplo.com");
		var construtor = _pagina.Construtor();

		construtor
			.AssertInputValue("#email", "ana@exemplo.com")
			.AssertInputValueByLabel("E-mail", "ana@exemplo.com", exact: true)
			.AssertInputValueByTestId("email", "ana@exemplo.com");

		_pagina.Page.Received().GetByLabel("E-mail", Arg.Is<PageGetByLabelOptions?>(o => o!.Exact == true));
		Should.Throw<ShouldAssertException>(() => construtor.AssertInputValue("#email", "bia@exemplo.com"))
			.Message.ShouldContain("Expected input value 'bia@exemplo.com' for element 'css=#email' but got 'ana@exemplo.com'");
		Should.Throw<ShouldAssertException>(() => construtor.AssertInputValueByLabel("E-mail", "x"));
		Should.Throw<ShouldAssertException>(() => construtor.AssertInputValueByTestId("email", "x"));
	}

	[Fact(DisplayName = "AssertClass variants should compare the whole class attribute")]
	public void AssertClass_Variants()
	{
		_pagina.Locator.GetAttributeAsync("class", Arg.Any<LocatorGetAttributeOptions?>()).Returns("btn btn-primary");
		var construtor = _pagina.Construtor();

		construtor
			.AssertClass("#salvar", "btn btn-primary")
			.AssertClassByTestId("salvar", "btn btn-primary")
			.AssertClassByRole(AriaRole.Button, "btn btn-primary", "Salvar");

		Should.Throw<ShouldAssertException>(() => construtor.AssertClass("#salvar", "btn"))
			.Message.ShouldContain("Expected class 'btn' for element 'css=#salvar' but got 'btn btn-primary'");
		Should.Throw<ShouldAssertException>(() => construtor.AssertClassByTestId("salvar", "btn"));
		Should.Throw<ShouldAssertException>(() => construtor.AssertClassByRole(AriaRole.Button, "btn"));
	}

	[Fact(DisplayName = "AssertContainsClass variants should accept any subset of the classes, in any order")]
	public void AssertContainsClass_Variants_Should_Accept_Subset()
	{
		_pagina.Locator.GetAttributeAsync("class", Arg.Any<LocatorGetAttributeOptions?>()).Returns("btn  active primary");

		_pagina.Construtor()
			.AssertContainsClass("#salvar", "active")
			.AssertContainsClassByTestId("salvar", "primary btn")
			.AssertContainsClassByRole(AriaRole.Button, " active ", "Salvar");
	}

	[Fact(DisplayName = "AssertContainsClass should fail naming the missing class")]
	public void AssertContainsClass_Should_Fail_Naming_Missing_Class()
	{
		_pagina.Locator.GetAttributeAsync("class", Arg.Any<LocatorGetAttributeOptions?>()).Returns("btn active");
		var construtor = _pagina.Construtor();

		Should.Throw<ShouldAssertException>(() => construtor.AssertContainsClass("#salvar", "btn disabled"))
			.Message.ShouldContain("to contain class 'disabled' but actual classes are 'btn active'");
	}

	[Fact(DisplayName = "AssertContainsClass should fail when the element has no class attribute")]
	public void AssertContainsClass_Should_Fail_Without_Class_Attribute()
	{
		_pagina.Locator.GetAttributeAsync("class", Arg.Any<LocatorGetAttributeOptions?>()).Returns((string?)null);
		var construtor = _pagina.Construtor();

		Should.Throw<ShouldAssertException>(() => construtor.AssertContainsClassByTestId("salvar", "btn"));
	}

	[Fact(DisplayName = "AssertId variants should compare the id attribute")]
	public void AssertId_Variants()
	{
		_pagina.Locator.GetAttributeAsync("id", Arg.Any<LocatorGetAttributeOptions?>()).Returns("form-login");
		var construtor = _pagina.Construtor();

		construtor
			.AssertId("form", "form-login")
			.AssertIdByRole(AriaRole.Form, "form-login", "Entrar")
			.AssertIdByTestId("login", "form-login");

		Should.Throw<ShouldAssertException>(() => construtor.AssertId("form", "form-cadastro"))
			.Message.ShouldContain("Expected id 'form-cadastro' for element 'css=form' but got 'form-login'");
		Should.Throw<ShouldAssertException>(() => construtor.AssertIdByRole(AriaRole.Form, "x"));
		Should.Throw<ShouldAssertException>(() => construtor.AssertIdByTestId("login", "x"));
	}

	[Fact(DisplayName = "AssertCssProperty variants should read the computed style of the named property")]
	public void AssertCssProperty_Variants()
	{
		_pagina.Locator.EvaluateAsync<string>(Arg.Any<string>(), Arg.Any<object?>(), Arg.Any<LocatorEvaluateOptions?>())
			.Returns("rgb(0, 0, 255)");
		var construtor = _pagina.Construtor();

		construtor
			.AssertCssProperty("#titulo", "color", "rgb(0, 0, 255)")
			.AssertCssPropertyByTestId("titulo", "color", "rgb(0, 0, 255)")
			.AssertCssPropertyByRole(AriaRole.Heading, "color", "rgb(0, 0, 255)", "Início");

		_pagina.Locator.Received().EvaluateAsync<string>(
			"el => getComputedStyle(el).getPropertyValue('color')", Arg.Any<object?>(), Arg.Any<LocatorEvaluateOptions?>());
		Should.Throw<ShouldAssertException>(() => construtor.AssertCssProperty("#titulo", "color", "rgb(255, 0, 0)"))
			.Message.ShouldContain("Expected CSS property 'color' to be 'rgb(255, 0, 0)' for element 'css=#titulo' but got 'rgb(0, 0, 255)'");
		Should.Throw<ShouldAssertException>(() => construtor.AssertCssPropertyByTestId("titulo", "color", "x"));
		Should.Throw<ShouldAssertException>(() => construtor.AssertCssPropertyByRole(AriaRole.Heading, "color", "x"));
	}

	// ──────────────────────────────────────────────
	// Marcado
	// ──────────────────────────────────────────────

	[Fact(DisplayName = "AssertChecked variants should pass when the checkbox is checked")]
	public void AssertChecked_Variants_Should_Pass_When_Checked()
	{
		_pagina.Locator.IsCheckedAsync(Arg.Any<LocatorIsCheckedOptions?>()).Returns(true);

		_pagina.Construtor()
			.AssertChecked("#aceite")
			.AssertCheckedByLabel("Aceito os termos")
			.AssertCheckedByTestId("aceite")
			.AssertCheckedByRole(AriaRole.Checkbox, "Aceito");
	}

	[Fact(DisplayName = "AssertChecked variants should fail naming the element when it is not checked")]
	public void AssertChecked_Variants_Should_Fail_When_Not_Checked()
	{
		_pagina.Locator.IsCheckedAsync(Arg.Any<LocatorIsCheckedOptions?>()).Returns(false);
		var construtor = _pagina.Construtor();

		Should.Throw<ShouldAssertException>(() => construtor.AssertChecked("#aceite"))
			.Message.ShouldContain("Expected element '#aceite' to be checked");
		Should.Throw<ShouldAssertException>(() => construtor.AssertCheckedByLabel("Aceito os termos"))
			.Message.ShouldContain("Expected element with label 'Aceito os termos' to be checked");
		Should.Throw<ShouldAssertException>(() => construtor.AssertCheckedByTestId("aceite"))
			.Message.ShouldContain("Expected element with test ID 'aceite' to be checked");
		Should.Throw<ShouldAssertException>(() => construtor.AssertCheckedByRole(AriaRole.Checkbox))
			.Message.ShouldContain("Expected element with role 'Checkbox' to be checked");
	}

	[Fact(DisplayName = "AssertNotChecked variants should pass when the checkbox is unchecked")]
	public void AssertNotChecked_Variants_Should_Pass_When_Unchecked()
	{
		_pagina.Locator.IsCheckedAsync(Arg.Any<LocatorIsCheckedOptions?>()).Returns(false);

		_pagina.Construtor()
			.AssertNotChecked("#newsletter")
			.AssertNotCheckedByLabel("Receber novidades", exact: true)
			.AssertNotCheckedByTestId("newsletter")
			.AssertNotCheckedByRole(AriaRole.Checkbox, "Receber");
	}

	[Fact(DisplayName = "AssertNotChecked variants should fail when the checkbox is checked")]
	public void AssertNotChecked_Variants_Should_Fail_When_Checked()
	{
		_pagina.Locator.IsCheckedAsync(Arg.Any<LocatorIsCheckedOptions?>()).Returns(true);
		var construtor = _pagina.Construtor();

		Should.Throw<ShouldAssertException>(() => construtor.AssertNotChecked("#newsletter"))
			.Message.ShouldContain("Expected element '#newsletter' to not be checked");
		Should.Throw<ShouldAssertException>(() => construtor.AssertNotCheckedByLabel("Receber novidades"))
			.Message.ShouldContain("label 'Receber novidades'");
		Should.Throw<ShouldAssertException>(() => construtor.AssertNotCheckedByTestId("newsletter"))
			.Message.ShouldContain("test ID 'newsletter'");
		Should.Throw<ShouldAssertException>(() => construtor.AssertNotCheckedByRole(AriaRole.Checkbox))
			.Message.ShouldContain("role 'Checkbox'");
	}

	// ──────────────────────────────────────────────
	// Habilitado / desabilitado
	// ──────────────────────────────────────────────

	[Fact(DisplayName = "AssertEnabled variants should pass when the element is enabled")]
	public void AssertEnabled_Variants_Should_Pass_When_Enabled()
	{
		_pagina.Locator.IsEnabledAsync(Arg.Any<LocatorIsEnabledOptions?>()).Returns(true);

		_pagina.Construtor()
			.AssertEnabled("#enviar")
			.AssertEnabledByRole(AriaRole.Button, "Enviar")
			.AssertEnabledByTestId("enviar")
			.AssertEnabledByLabel("Nome");
	}

	[Fact(DisplayName = "AssertEnabled variants should fail naming the element when it is disabled")]
	public void AssertEnabled_Variants_Should_Fail_When_Disabled()
	{
		_pagina.Locator.IsEnabledAsync(Arg.Any<LocatorIsEnabledOptions?>()).Returns(false);
		var construtor = _pagina.Construtor();

		Should.Throw<ShouldAssertException>(() => construtor.AssertEnabled("#enviar"))
			.Message.ShouldContain("Expected element '#enviar' to be enabled");
		Should.Throw<ShouldAssertException>(() => construtor.AssertEnabledByRole(AriaRole.Button))
			.Message.ShouldContain("role 'Button'");
		Should.Throw<ShouldAssertException>(() => construtor.AssertEnabledByTestId("enviar"))
			.Message.ShouldContain("test ID 'enviar'");
		Should.Throw<ShouldAssertException>(() => construtor.AssertEnabledByLabel("Nome"))
			.Message.ShouldContain("label 'Nome'");
	}

	[Fact(DisplayName = "AssertDisabled variants should pass when the element is disabled")]
	public void AssertDisabled_Variants_Should_Pass_When_Disabled()
	{
		_pagina.Locator.IsDisabledAsync(Arg.Any<LocatorIsDisabledOptions?>()).Returns(true);

		_pagina.Construtor()
			.AssertDisabled("#enviar")
			.AssertDisabledByTestId("enviar")
			.AssertDisabledByRole(AriaRole.Button, "Enviar")
			.AssertDisabledByLabel("CPF", exact: true);
	}

	[Fact(DisplayName = "AssertDisabled variants should fail naming the element when it is enabled")]
	public void AssertDisabled_Variants_Should_Fail_When_Enabled()
	{
		_pagina.Locator.IsDisabledAsync(Arg.Any<LocatorIsDisabledOptions?>()).Returns(false);
		var construtor = _pagina.Construtor();

		Should.Throw<ShouldAssertException>(() => construtor.AssertDisabled("#enviar"))
			.Message.ShouldContain("Expected element '#enviar' to be disabled");
		Should.Throw<ShouldAssertException>(() => construtor.AssertDisabledByTestId("enviar"))
			.Message.ShouldContain("test ID 'enviar'");
		Should.Throw<ShouldAssertException>(() => construtor.AssertDisabledByRole(AriaRole.Button))
			.Message.ShouldContain("role 'Button'");
		Should.Throw<ShouldAssertException>(() => construtor.AssertDisabledByLabel("CPF"))
			.Message.ShouldContain("label 'CPF'");
	}

	// ──────────────────────────────────────────────
	// Editável
	// ──────────────────────────────────────────────

	[Fact(DisplayName = "AssertEditable variants should pass when the element is editable")]
	public void AssertEditable_Variants_Should_Pass_When_Editable()
	{
		_pagina.Locator.IsEditableAsync(Arg.Any<LocatorIsEditableOptions?>()).Returns(true);

		_pagina.Construtor()
			.AssertEditable("#nome")
			.AssertEditableByTestId("nome")
			.AssertEditableByRole(AriaRole.Textbox, "Nome")
			.AssertEditableByLabel("Nome");
	}

	[Fact(DisplayName = "AssertEditable should fail when the element is read-only")]
	public void AssertEditable_Should_Fail_When_ReadOnly()
	{
		_pagina.Locator.IsEditableAsync(Arg.Any<LocatorIsEditableOptions?>()).Returns(false);
		var construtor = _pagina.Construtor();

		Should.Throw<ShouldAssertException>(() => construtor.AssertEditable("#nome"))
			.Message.ShouldContain("Expected element 'css=#nome' to be editable");
		Should.Throw<ShouldAssertException>(() => construtor.AssertEditableByLabel("Nome"))
			.Message.ShouldContain("label=Nome");
	}

	[Fact(DisplayName = "AssertNotEditable variants should pass on a read-only element and fail on an editable one")]
	public void AssertNotEditable_Variants()
	{
		_pagina.Locator.IsEditableAsync(Arg.Any<LocatorIsEditableOptions?>()).Returns(false);
		_pagina.Construtor().AssertNotEditable("#codigo").AssertNotEditableByTestId("codigo");

		var editavel = new PaginaFalsa();
		editavel.Locator.IsEditableAsync(Arg.Any<LocatorIsEditableOptions?>()).Returns(true);
		var construtor = editavel.Construtor();

		Should.Throw<ShouldAssertException>(() => construtor.AssertNotEditable("#codigo"))
			.Message.ShouldContain("Expected element 'css=#codigo' to not be editable");
		Should.Throw<ShouldAssertException>(() => construtor.AssertNotEditableByTestId("codigo"));
	}

	// ──────────────────────────────────────────────
	// Vazio
	// ──────────────────────────────────────────────

	[Theory(DisplayName = "AssertEmpty variants should treat null and whitespace-only text as empty")]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   \n\t ")]
	public void AssertEmpty_Variants_Should_Treat_Whitespace_As_Empty(string? texto)
	{
		_pagina.Locator.TextContentAsync(Arg.Any<LocatorTextContentOptions?>()).Returns(texto);

		_pagina.Construtor()
			.AssertEmpty("#erros")
			.AssertEmptyByTestId("erros")
			.AssertEmptyByRole(AriaRole.Alert);
	}

	[Fact(DisplayName = "AssertEmpty should fail showing the text found")]
	public void AssertEmpty_Should_Fail_Showing_Text()
	{
		_pagina.Locator.TextContentAsync(Arg.Any<LocatorTextContentOptions?>()).Returns("  Campo obrigatório ");
		var construtor = _pagina.Construtor();

		Should.Throw<ShouldAssertException>(() => construtor.AssertEmpty("#erros"))
			.Message.ShouldContain("Expected element 'css=#erros' to be empty but found text: 'Campo obrigatório'");
		Should.Throw<ShouldAssertException>(() => construtor.AssertEmptyByTestId("erros"));
		Should.Throw<ShouldAssertException>(() => construtor.AssertEmptyByRole(AriaRole.Alert));
	}

	[Fact(DisplayName = "AssertNotEmpty variants should pass with text and fail with whitespace only")]
	public void AssertNotEmpty_Variants()
	{
		_pagina.Locator.TextContentAsync(Arg.Any<LocatorTextContentOptions?>()).Returns("3 itens");
		_pagina.Construtor().AssertNotEmpty("#carrinho").AssertNotEmptyByTestId("carrinho");

		var vazia = new PaginaFalsa();
		vazia.Locator.TextContentAsync(Arg.Any<LocatorTextContentOptions?>()).Returns("  ");
		var construtor = vazia.Construtor();

		Should.Throw<ShouldAssertException>(() => construtor.AssertNotEmpty("#carrinho"))
			.Message.ShouldContain("Expected element 'css=#carrinho' to not be empty");
		Should.Throw<ShouldAssertException>(() => construtor.AssertNotEmptyByTestId("carrinho"));
	}

	// ──────────────────────────────────────────────
	// Captura de tela no momento da asserção
	// ──────────────────────────────────────────────

	[Fact(DisplayName = "TakeScreenshot should save a full-page screenshot under the configured path, creating it")]
	public async Task TakeScreenshot_Should_Save_Under_Configured_Path()
	{
		var pasta = Path.Combine(Path.GetTempPath(), "xua-pw-" + Guid.NewGuid().ToString("N"));
		try
		{
			var construtor = await _pagina.Cenario(new PlaywrightSettings { ScreenshotPath = pasta })
				.NavigateTo("https://app.test").ExecuteAsync();

			construtor.TakeScreenshot("final.png").ShouldBeSameAs(construtor);

			Directory.Exists(pasta).ShouldBeTrue();
			await _pagina.Page.Received().ScreenshotAsync(Arg.Is<PageScreenshotOptions?>(o =>
				o!.Path == Path.Combine(pasta, "final.png") && o.FullPage == true));
		}
		finally
		{
			if (Directory.Exists(pasta))
				Directory.Delete(pasta, recursive: true);
		}
	}

	[Fact(DisplayName = "TakeScreenshot without a name should generate a timestamped file name")]
	public async Task TakeScreenshot_Without_Name_Should_Generate_Name()
	{
		var pasta = Path.Combine(Path.GetTempPath(), "xua-pw-" + Guid.NewGuid().ToString("N"));
		try
		{
			var construtor = await _pagina.Cenario(new PlaywrightSettings { ScreenshotPath = pasta })
				.NavigateTo("https://app.test").ExecuteAsync();

			construtor.TakeScreenshot();

			await _pagina.Page.Received().ScreenshotAsync(Arg.Is<PageScreenshotOptions?>(o =>
				Path.GetFileName(o!.Path!).StartsWith("assert_") && o.Path!.EndsWith(".png")));
		}
		finally
		{
			if (Directory.Exists(pasta))
				Directory.Delete(pasta, recursive: true);
		}
	}

	// ──────────────────────────────────────────────
	// Cookies e local storage
	// ──────────────────────────────────────────────

	private void ComCookies(params BrowserContextCookiesResult[] cookies)
	{
		_pagina.Contexto.CookiesAsync().Returns(cookies);
	}

	[Fact(DisplayName = "AssertCookie and AssertNoCookie should read the cookies of the browser context")]
	public void AssertCookie_And_AssertNoCookie()
	{
		ComCookies(new BrowserContextCookiesResult { Name = "sessao", HttpOnly = true });
		var construtor = _pagina.Construtor();

		construtor.AssertCookie("sessao").AssertNoCookie("rastreio");

		Should.Throw<ShouldAssertException>(() => construtor.AssertCookie("rastreio"))
			.Message.ShouldContain("No cookie named 'rastreio' in the browser context");
		Should.Throw<ShouldAssertException>(() => construtor.AssertNoCookie("sessao"))
			.Message.ShouldContain("A cookie named 'sessao' is still in the browser context");
	}

	[Fact(DisplayName = "AssertCookieIsHttpOnly should pass only for an existing HttpOnly cookie")]
	public void AssertCookieIsHttpOnly()
	{
		ComCookies(
			new BrowserContextCookiesResult { Name = "tema", HttpOnly = false },
			new BrowserContextCookiesResult { Name = "sessao", HttpOnly = true });
		var construtor = _pagina.Construtor();

		construtor.AssertCookieIsHttpOnly("sessao");

		Should.Throw<ShouldAssertException>(() => construtor.AssertCookieIsHttpOnly("tema"))
			.Message.ShouldContain("Cookie 'tema' is readable from page JavaScript");
		Should.Throw<ShouldAssertException>(() => construtor.AssertCookieIsHttpOnly("ausente"))
			.Message.ShouldContain("No cookie named 'ausente'");
	}

	[Fact(DisplayName = "AssertLocalStorage should read the key from the page and compare the value")]
	public void AssertLocalStorage_Should_Compare_Value()
	{
		_pagina.Page.EvaluateAsync<string?>(Arg.Any<string>(), "idioma").Returns("pt-BR");
		var construtor = _pagina.Construtor();

		construtor.AssertLocalStorage("idioma", "pt-BR");

		_pagina.Page.Received().EvaluateAsync<string?>("chave => localStorage.getItem(chave)", "idioma");
		Should.Throw<ShouldAssertException>(() => construtor.AssertLocalStorage("idioma", "en"))
			.Message.ShouldContain("Local storage key 'idioma' does not hold the expected value");
	}

	[Fact(DisplayName = "AssertNoLocalStorage should pass when the key is absent and fail when it holds a value")]
	public void AssertNoLocalStorage()
	{
		_pagina.Page.EvaluateAsync<string?>(Arg.Any<string>(), "token").Returns("abc");
		_pagina.Page.EvaluateAsync<string?>(Arg.Any<string>(), "removido").Returns((string?)null);
		var construtor = _pagina.Construtor();

		construtor.AssertNoLocalStorage("removido");

		Should.Throw<ShouldAssertException>(() => construtor.AssertNoLocalStorage("token"))
			.Message.ShouldContain("Local storage key 'token' still holds a value");
	}

	[Fact(DisplayName = "Browser state assertions without a page should say how to provide one")]
	public void Browser_State_Assertions_Without_Page_Should_Explain()
	{
		var cenario = XUnitAssured.Core.DSL.ScenarioDsl.Given();
		cenario.SetCurrentStep(new PassoJaExecutado());
		var construtor = new PlaywrightValidationBuilder(cenario);

		Should.Throw<InvalidOperationException>(() => construtor.AssertCookie("sessao"))
			.Message.ShouldContain("No browser page in this scenario");
		Should.Throw<InvalidOperationException>(() => construtor.AssertLocalStorage("k", "v"))
			.Message.ShouldContain("PlaywrightPageSession");
		Should.Throw<InvalidOperationException>(() => construtor.AssertVisible("#x"))
			.Message.ShouldContain("No Playwright page available in context");
	}

	// As asserções com nova tentativa (Expect*) e as que se apoiam nelas usam Assertions.Expect
	// do Playwright, que exige o localizador concreto da biblioteca: com uma página falsa não há o
	// que exercitar além da guarda. Sem página, todas têm de dizer o mesmo, e não estourar com um
	// NullReferenceException em algum ponto do Playwright.
	private static readonly Dictionary<string, Action<PlaywrightValidationBuilder>> DependemDaPagina = new()
	{
		["ExpectLocator(locator)"] = b => b.ExpectLocator(LocatorStrategy.ByTestId("x")),
		["ExpectVisible(locator)"] = b => b.ExpectVisible(LocatorStrategy.ByTestId("x")),
		["ExpectHidden(locator)"] = b => b.ExpectHidden(LocatorStrategy.ByTestId("x")),
		["ExpectEnabled(locator)"] = b => b.ExpectEnabled(LocatorStrategy.ByTestId("x")),
		["ExpectDisabled(locator)"] = b => b.ExpectDisabled(LocatorStrategy.ByTestId("x")),
		["ExpectChecked(locator)"] = b => b.ExpectChecked(LocatorStrategy.ByTestId("x")),
		["ExpectEditable"] = b => b.ExpectEditable("#x"),
		["ExpectEditable(locator)"] = b => b.ExpectEditable(LocatorStrategy.ByTestId("x")),
		["ExpectEmpty"] = b => b.ExpectEmpty("#x"),
		["ExpectEmpty(locator)"] = b => b.ExpectEmpty(LocatorStrategy.ByTestId("x")),
		["ExpectFocused"] = b => b.ExpectFocused("#x"),
		["ExpectFocused(locator)"] = b => b.ExpectFocused(LocatorStrategy.ByTestId("x")),
		["ExpectAttached"] = b => b.ExpectAttached("#x"),
		["ExpectAttached(locator)"] = b => b.ExpectAttached(LocatorStrategy.ByTestId("x")),
		["ExpectInViewport"] = b => b.ExpectInViewport("#x"),
		["ExpectInViewport(locator)"] = b => b.ExpectInViewport(LocatorStrategy.ByTestId("x")),
		["ExpectText(locator)"] = b => b.ExpectText(LocatorStrategy.ByTestId("x"), "t"),
		["ExpectContainsText"] = b => b.ExpectContainsText("#x", "t"),
		["ExpectContainsText(locator)"] = b => b.ExpectContainsText(LocatorStrategy.ByTestId("x"), "t"),
		["ExpectAttribute"] = b => b.ExpectAttribute("#x", "a", "v"),
		["ExpectAttribute(locator)"] = b => b.ExpectAttribute(LocatorStrategy.ByTestId("x"), "a", "v"),
		["ExpectValue"] = b => b.ExpectValue("#x", "v"),
		["ExpectValue(locator)"] = b => b.ExpectValue(LocatorStrategy.ByTestId("x"), "v"),
		["ExpectValues"] = b => b.ExpectValues("#x", new[] { "v" }),
		["ExpectValues(locator)"] = b => b.ExpectValues(LocatorStrategy.ByTestId("x"), new[] { "v" }),
		["ExpectClass"] = b => b.ExpectClass("#x", "c"),
		["ExpectClass(locator)"] = b => b.ExpectClass(LocatorStrategy.ByTestId("x"), "c"),
		["ExpectCount"] = b => b.ExpectCount("#x", 1),
		["ExpectCount(locator)"] = b => b.ExpectCount(LocatorStrategy.ByTestId("x"), 1),
		["ExpectCssProperty"] = b => b.ExpectCssProperty("#x", "color", "red"),
		["ExpectCssProperty(locator)"] = b => b.ExpectCssProperty(LocatorStrategy.ByTestId("x"), "color", "red"),
		["ExpectId"] = b => b.ExpectId("#x", "x"),
		["ExpectId(locator)"] = b => b.ExpectId(LocatorStrategy.ByTestId("x"), "x"),
		["ExpectAccessibleName"] = b => b.ExpectAccessibleName("#x", "n"),
		["ExpectAccessibleName(locator)"] = b => b.ExpectAccessibleName(LocatorStrategy.ByTestId("x"), "n"),
		["ExpectAccessibleDescription"] = b => b.ExpectAccessibleDescription("#x", "d"),
		["ExpectAccessibleDescription(locator)"] = b => b.ExpectAccessibleDescription(LocatorStrategy.ByTestId("x"), "d"),
		["ExpectRole"] = b => b.ExpectRole("#x", AriaRole.Button),
		["ExpectRole(locator)"] = b => b.ExpectRole(LocatorStrategy.ByTestId("x"), AriaRole.Button),
		["ExpectJSProperty"] = b => b.ExpectJSProperty("#x", "p", 1),
		["ExpectJSProperty(locator)"] = b => b.ExpectJSProperty(LocatorStrategy.ByTestId("x"), "p", 1),
		["ExpectTitle(regex)"] = b => b.ExpectTitle(new System.Text.RegularExpressions.Regex("x")),
		["ExpectUrl(regex)"] = b => b.ExpectUrl(new System.Text.RegularExpressions.Regex("x")),
		["ExpectNotVisible(locator)"] = b => b.ExpectNotVisible(LocatorStrategy.ByTestId("x")),
		["ExpectNotAttached(locator)"] = b => b.ExpectNotAttached(LocatorStrategy.ByTestId("x")),
		["ExpectNotChecked(locator)"] = b => b.ExpectNotChecked(LocatorStrategy.ByTestId("x")),
		["ExpectNotEditable"] = b => b.ExpectNotEditable("#x"),
		["ExpectNotEditable(locator)"] = b => b.ExpectNotEditable(LocatorStrategy.ByTestId("x")),
		["ExpectNotEmpty"] = b => b.ExpectNotEmpty("#x"),
		["ExpectNotEmpty(locator)"] = b => b.ExpectNotEmpty(LocatorStrategy.ByTestId("x")),
		["ExpectNotFocused"] = b => b.ExpectNotFocused("#x"),
		["ExpectNotFocused(locator)"] = b => b.ExpectNotFocused(LocatorStrategy.ByTestId("x")),
		["ExpectNotText"] = b => b.ExpectNotText("#x", "t"),
		["ExpectNotContainsText"] = b => b.ExpectNotContainsText("#x", "t"),
		["AssertFocusedByTestId"] = b => b.AssertFocusedByTestId("x"),
		["AssertFocusedByRole"] = b => b.AssertFocusedByRole(AriaRole.Button),
		["AssertFocusedByLabel"] = b => b.AssertFocusedByLabel("x"),
		["AssertNotFocusedByTestId"] = b => b.AssertNotFocusedByTestId("x"),
		["AssertAttachedByTestId"] = b => b.AssertAttachedByTestId("x"),
		["AssertAttachedByRole"] = b => b.AssertAttachedByRole(AriaRole.Button),
		["AssertNotAttachedByTestId"] = b => b.AssertNotAttachedByTestId("x"),
		["AssertInViewportByTestId"] = b => b.AssertInViewportByTestId("x"),
		["AssertInViewportByRole"] = b => b.AssertInViewportByRole(AriaRole.Button),
		["AssertAccessibleNameByRole"] = b => b.AssertAccessibleNameByRole(AriaRole.Button, "n"),
		["AssertAccessibleDescriptionByRole"] = b => b.AssertAccessibleDescriptionByRole(AriaRole.Button, "d"),
		["AssertRoleByTestId"] = b => b.AssertRoleByTestId("x", AriaRole.Button),
		["AssertValuesByLabel"] = b => b.AssertValuesByLabel("x", false, "v"),
		["AssertJSPropertyByTestId"] = b => b.AssertJSPropertyByTestId("x", "p", 1),
	};

	public static TheoryData<string> NomesQueDependemDaPagina()
	{
		var dados = new TheoryData<string>();
		foreach (var nome in DependemDaPagina.Keys)
			dados.Add(nome);
		return dados;
	}

	[Theory(DisplayName = "Auto-retrying assertions without a page should say a Playwright page is needed")]
	[MemberData(nameof(NomesQueDependemDaPagina))]
	public void Auto_Retrying_Assertions_Without_Page_Should_Explain(string asercao)
	{
		var cenario = XUnitAssured.Core.DSL.ScenarioDsl.Given();
		cenario.SetCurrentStep(new PassoJaExecutado());
		var construtor = new PlaywrightValidationBuilder(cenario);

		Should.Throw<InvalidOperationException>(() => DependemDaPagina[asercao](construtor))
			.Message.ShouldBe("No Playwright page available in context. Ensure you are using PlaywrightTestBase.");
	}

	/// <summary>Um passo que já rodou, sem página nenhuma no contexto.</summary>
	private sealed class PassoJaExecutado : XUnitAssured.Core.Abstractions.ITestStep
	{
		public string? Name => null;
		public string StepType => "Playwright";
		public XUnitAssured.Core.Results.ITestStepResult? Result { get; } =
			XUnitAssured.Playwright.Results.PlaywrightStepResult.CreateSuccess("https://app.test", "App", "<html></html>");
		public bool IsExecuted => true;
		public bool IsValid => true;

		public Task<XUnitAssured.Core.Results.ITestStepResult> ExecuteAsync(
			XUnitAssured.Core.Abstractions.ITestContext context, CancellationToken cancellationToken = default) =>
			Task.FromResult(Result!);

		public void Validate(Action<XUnitAssured.Core.Results.ITestStepResult> validation) => validation(Result!);
	}
}
