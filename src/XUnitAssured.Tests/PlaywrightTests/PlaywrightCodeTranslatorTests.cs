using XUnitAssured.Playwright.Codegen;

namespace XUnitAssured.Tests.PlaywrightTests;

[Trait("Category", "Playwright")]
[Trait("Component", "Codegen")]
/// <summary>
/// Tests for PlaywrightCodeTranslator.
/// Verifies translation from Playwright C# Library code to XUnitAssured fluent DSL.
/// </summary>
public class PlaywrightCodeTranslatorTests
{
	// ──────────────────────────────────────────────
	// Navigation
	// ──────────────────────────────────────────────

	[Fact(DisplayName = "GotoAsync translates to NavigateTo")]
	public void Should_Translate_GotoAsync()
	{
		var input = @"await page.GotoAsync(""https://example.com/"");";
		var result = PlaywrightCodeTranslator.TranslateLine(input);
		Assert.Equal(@".NavigateTo(""https://example.com/"")", result);
	}

	[Fact(DisplayName = "GotoAsync with relative URL")]
	public void Should_Translate_GotoAsync_Relative()
	{
		var input = @"await page.GotoAsync(""/counter"");";
		var result = PlaywrightCodeTranslator.TranslateLine(input);
		Assert.Equal(@".NavigateTo(""/counter"")", result);
	}

	// ──────────────────────────────────────────────
	// Click + locator variants
	// ──────────────────────────────────────────────

	[Fact(DisplayName = "GetByRole + ClickAsync → ClickByRole")]
	public void Should_Translate_ClickByRole()
	{
		var input = @"await page.GetByRole(AriaRole.Button, new() { Name = ""Click me"" }).ClickAsync();";
		var result = PlaywrightCodeTranslator.TranslateLine(input);
		Assert.Equal(@".ClickByRole(AriaRole.Button, ""Click me"")", result);
	}

	[Fact(DisplayName = "GetByRole without Name + ClickAsync → ClickByRole with role only")]
	public void Should_Translate_ClickByRole_NoName()
	{
		var input = @"await page.GetByRole(AriaRole.Navigation).ClickAsync();";
		var result = PlaywrightCodeTranslator.TranslateLine(input);
		Assert.Equal(@".ClickByRole(AriaRole.Navigation)", result);
	}

	[Fact(DisplayName = "GetByText + ClickAsync → ClickByText")]
	public void Should_Translate_ClickByText()
	{
		var input = @"await page.GetByText(""Submit"").ClickAsync();";
		var result = PlaywrightCodeTranslator.TranslateLine(input);
		Assert.Equal(@".ClickByText(""Submit"")", result);
	}

	[Fact(DisplayName = "GetByLabel + ClickAsync → ClickByLabel")]
	public void Should_Translate_ClickByLabel()
	{
		var input = @"await page.GetByLabel(""Remember me"").ClickAsync();";
		var result = PlaywrightCodeTranslator.TranslateLine(input);
		Assert.Equal(@".ClickByLabel(""Remember me"")", result);
	}

	[Fact(DisplayName = "GetByTestId + ClickAsync → ClickByTestId")]
	public void Should_Translate_ClickByTestId()
	{
		var input = @"await page.GetByTestId(""submit-btn"").ClickAsync();";
		var result = PlaywrightCodeTranslator.TranslateLine(input);
		Assert.Equal(@".ClickByTestId(""submit-btn"")", result);
	}

	[Fact(DisplayName = "GetByTitle + ClickAsync → ClickByTitle")]
	public void Should_Translate_ClickByTitle()
	{
		var input = @"await page.GetByTitle(""Close dialog"").ClickAsync();";
		var result = PlaywrightCodeTranslator.TranslateLine(input);
		Assert.Equal(@".ClickByTitle(""Close dialog"")", result);
	}

	[Fact(DisplayName = "Locator (CSS) + ClickAsync → Click")]
	public void Should_Translate_Click_Css()
	{
		var input = @"await page.Locator(""#login-btn"").ClickAsync();";
		var result = PlaywrightCodeTranslator.TranslateLine(input);
		Assert.Equal(@".Click(""#login-btn"")", result);
	}

	// ──────────────────────────────────────────────
	// Fill + locator variants
	// ──────────────────────────────────────────────

	[Fact(DisplayName = "GetByLabel + FillAsync → FillByLabel")]
	public void Should_Translate_FillByLabel()
	{
		var input = @"await page.GetByLabel(""Email"").FillAsync(""user@test.com"");";
		var result = PlaywrightCodeTranslator.TranslateLine(input);
		Assert.Equal(@".FillByLabel(""Email"", ""user@test.com"")", result);
	}

	[Fact(DisplayName = "GetByPlaceholder + FillAsync → FillByPlaceholder")]
	public void Should_Translate_FillByPlaceholder()
	{
		var input = @"await page.GetByPlaceholder(""Enter email"").FillAsync(""user@test.com"");";
		var result = PlaywrightCodeTranslator.TranslateLine(input);
		Assert.Equal(@".FillByPlaceholder(""Enter email"", ""user@test.com"")", result);
	}

	[Fact(DisplayName = "GetByRole + FillAsync → FillByRole")]
	public void Should_Translate_FillByRole()
	{
		var input = @"await page.GetByRole(AriaRole.Textbox, new() { Name = ""Search"" }).FillAsync(""playwright"");";
		var result = PlaywrightCodeTranslator.TranslateLine(input);
		Assert.Equal(@".FillByRole(AriaRole.Textbox, ""Search"", ""playwright"")", result);
	}

	[Fact(DisplayName = "GetByTestId + FillAsync → FillByTestId")]
	public void Should_Translate_FillByTestId()
	{
		var input = @"await page.GetByTestId(""email-input"").FillAsync(""user@test.com"");";
		var result = PlaywrightCodeTranslator.TranslateLine(input);
		Assert.Equal(@".FillByTestId(""email-input"", ""user@test.com"")", result);
	}

	[Fact(DisplayName = "Locator (CSS) + FillAsync → Fill")]
	public void Should_Translate_Fill_Css()
	{
		var input = @"await page.Locator(""#email"").FillAsync(""user@test.com"");";
		var result = PlaywrightCodeTranslator.TranslateLine(input);
		Assert.Equal(@".Fill(""#email"", ""user@test.com"")", result);
	}

	// ──────────────────────────────────────────────
	// DoubleClick
	// ──────────────────────────────────────────────

	[Fact(DisplayName = "GetByText + DblClickAsync → DoubleClickByText")]
	public void Should_Translate_DoubleClickByText()
	{
		var input = @"await page.GetByText(""Edit"").DblClickAsync();";
		var result = PlaywrightCodeTranslator.TranslateLine(input);
		Assert.Equal(@".DoubleClickByText(""Edit"")", result);
	}

	// ──────────────────────────────────────────────
	// Check / Uncheck
	// ──────────────────────────────────────────────

	[Fact(DisplayName = "GetByLabel + CheckAsync → CheckByLabel")]
	public void Should_Translate_CheckByLabel()
	{
		var input = @"await page.GetByLabel(""I agree"").CheckAsync();";
		var result = PlaywrightCodeTranslator.TranslateLine(input);
		Assert.Equal(@".CheckByLabel(""I agree"")", result);
	}

	[Fact(DisplayName = "GetByRole + UncheckAsync → UncheckByRole")]
	public void Should_Translate_UncheckByRole()
	{
		var input = @"await page.GetByRole(AriaRole.Checkbox, new() { Name = ""Remember"" }).UncheckAsync();";
		var result = PlaywrightCodeTranslator.TranslateLine(input);
		Assert.Equal(@".UncheckByRole(AriaRole.Checkbox, ""Remember"")", result);
	}

	// ──────────────────────────────────────────────
	// Press / TypeText
	// ──────────────────────────────────────────────

	[Fact(DisplayName = "GetByLabel + PressAsync → PressByLabel")]
	public void Should_Translate_PressByLabel()
	{
		var input = @"await page.GetByLabel(""Search"").PressAsync(""Enter"");";
		var result = PlaywrightCodeTranslator.TranslateLine(input);
		Assert.Equal(@".PressByLabel(""Search"", ""Enter"")", result);
	}

	[Fact(DisplayName = "GetByRole + PressSequentiallyAsync → TypeTextByRole")]
	public void Should_Translate_TypeTextByRole()
	{
		var input = @"await page.GetByRole(AriaRole.Textbox, new() { Name = ""Search"" }).PressSequentiallyAsync(""hello"");";
		var result = PlaywrightCodeTranslator.TranslateLine(input);
		Assert.Equal(@".TypeTextByRole(AriaRole.Textbox, ""Search"", ""hello"")", result);
	}

	// ──────────────────────────────────────────────
	// Hover / Focus
	// ──────────────────────────────────────────────

	[Fact(DisplayName = "GetByText + HoverAsync → HoverByText")]
	public void Should_Translate_HoverByText()
	{
		var input = @"await page.GetByText(""Menu"").HoverAsync();";
		var result = PlaywrightCodeTranslator.TranslateLine(input);
		Assert.Equal(@".HoverByText(""Menu"")", result);
	}

	[Fact(DisplayName = "GetByTestId + FocusAsync → FocusByTestId")]
	public void Should_Translate_FocusByTestId()
	{
		var input = @"await page.GetByTestId(""search-box"").FocusAsync();";
		var result = PlaywrightCodeTranslator.TranslateLine(input);
		Assert.Equal(@".FocusByTestId(""search-box"")", result);
	}

	// ──────────────────────────────────────────────
	// SelectOption
	// ──────────────────────────────────────────────

	[Fact(DisplayName = "GetByLabel + SelectOptionAsync → SelectOptionByLabel")]
	public void Should_Translate_SelectOptionByLabel()
	{
		var input = @"await page.GetByLabel(""Country"").SelectOptionAsync(""BR"");";
		var result = PlaywrightCodeTranslator.TranslateLine(input);
		Assert.Equal(@".SelectOptionByLabel(""Country"", ""BR"")", result);
	}

	// ──────────────────────────────────────────────
	// Clear
	// ──────────────────────────────────────────────

	[Fact(DisplayName = "GetByLabel + ClearAsync → ClearByLabel")]
	public void Should_Translate_ClearByLabel()
	{
		var input = @"await page.GetByLabel(""Email"").ClearAsync();";
		var result = PlaywrightCodeTranslator.TranslateLine(input);
		Assert.Equal(@".ClearByLabel(""Email"")", result);
	}

	// ──────────────────────────────────────────────
	// Multi-line / block translation
	// ──────────────────────────────────────────────

	[Fact(DisplayName = "Full Playwright block translates to DSL chain")]
	public void Should_Translate_Full_Block()
	{
		var input = @"
using Microsoft.Playwright;
using System;
using System.Threading.Tasks;

using var playwright = await Playwright.CreateAsync();
await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
{
    Headless = false,
});
var context = await browser.NewContextAsync();

var page = await context.NewPageAsync();
await page.GetByRole(AriaRole.Button, new() { Name = ""Click me"" }).ClickAsync();
await page.GetByRole(AriaRole.Button, new() { Name = ""Click me"" }).ClickAsync();
await page.GetByRole(AriaRole.Button, new() { Name = ""Click me"" }).ClickAsync();
";

		var result = PlaywrightCodeTranslator.Translate(input, indent: "");
		var lines = result.Split('\n', StringSplitOptions.RemoveEmptyEntries);

		Assert.Equal(3, lines.Length);
		Assert.All(lines, line => Assert.Equal(@".ClickByRole(AriaRole.Button, ""Click me"")", line.Trim()));
	}

	// ──────────────────────────────────────────────
	// Boilerplate filtering
	// ──────────────────────────────────────────────

	[Theory(DisplayName = "Boilerplate lines are skipped")]
	[InlineData("using Microsoft.Playwright;")]
	[InlineData("using var playwright = await Playwright.CreateAsync();")]
	[InlineData("await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions")]
	[InlineData("var context = await browser.NewContextAsync();")]
	[InlineData("var page = await context.NewPageAsync();")]
	[InlineData("{")]
	[InlineData("}")]
	[InlineData("};")]
	[InlineData("    Headless = false,")]
	public void Should_Skip_Boilerplate(string input)
	{
		var result = PlaywrightCodeTranslator.Translate(input, indent: "");
		Assert.Empty(result);
	}

	// ──────────────────────────────────────────────
	// Edge cases
	// ──────────────────────────────────────────────

	[Fact(DisplayName = "Empty input returns empty string")]
	public void Should_Handle_Empty_Input()
	{
		Assert.Empty(PlaywrightCodeTranslator.Translate(""));
		Assert.Empty(PlaywrightCodeTranslator.Translate("   "));
	}

	[Fact(DisplayName = "Null input returns empty string")]
	public void Should_Handle_Null_Input()
	{
		Assert.Empty(PlaywrightCodeTranslator.Translate(null!));
	}

	[Fact(DisplayName = "TranslateLine returns null for empty input")]
	public void Should_TranslateLine_Null_For_Empty()
	{
		Assert.Null(PlaywrightCodeTranslator.TranslateLine(""));
		Assert.Null(PlaywrightCodeTranslator.TranslateLine("   "));
	}

	[Fact(DisplayName = "Unrecognized line becomes TODO comment")]
	public void Should_Mark_Unrecognized_As_Todo()
	{
		var input = "var x = 42;";
		var result = PlaywrightCodeTranslator.TranslateLine(input);
		Assert.NotNull(result);
		Assert.StartsWith("// TODO:", result);
	}

	// ──────────────────────────────────────────────
	// TranslateToGivenBlock
	// ──────────────────────────────────────────────

	[Fact(DisplayName = "TranslateToGivenBlock wraps in Given/When/Then")]
	public void Should_Generate_GivenBlock()
	{
		var input = @"await page.GetByRole(AriaRole.Button, new() { Name = ""Submit"" }).ClickAsync();";
		var result = PlaywrightCodeTranslator.TranslateToGivenBlock(input);

		Assert.Contains("Given()", result);
		Assert.Contains(".ClickByRole(AriaRole.Button, \"Submit\")", result);
		Assert.Contains(".When()", result);
		Assert.Contains(".Execute()", result);
		Assert.Contains(".Then()", result);
		Assert.Contains(".AssertSuccess();", result);
	}
}
