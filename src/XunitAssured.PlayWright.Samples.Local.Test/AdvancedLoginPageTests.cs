using Microsoft.Playwright;
using Shouldly;

using XUnitAssured.Playwright.Extensions;
using XUnitAssured.Playwright.Locators;

namespace XunitAssured.PlayWright.Samples.Local.Test;

[Trait("Category", "E2E")]
[Trait("Page", "Login")]
[Trait("Feature", "Advanced")]
/// <summary>
/// Advanced E2E tests for the Login page demonstrating latest XUnitAssured.Playwright features.
/// Covers: Editable assertions, focus/not-focused, clear input, type (character-by-character),
/// press keyboard key, checked/unchecked state, CSS class assertions,
/// attribute assertions, Expect auto-retry for forms, hidden element checks,
/// and accessible name/description assertions.
/// </summary>
public class AdvancedLoginPageTests : PlaywrightSamplesTestBase, IClassFixture<PlaywrightSamplesFixture>
{
	public AdvancedLoginPageTests(PlaywrightSamplesFixture fixture) : base(fixture) { }

	// ──────────────────────────────────────────────
	// Editable Assertions
	// ──────────────────────────────────────────────

	[Fact(DisplayName = "Email input should be editable")]
	public void Should_Have_Editable_Email_Input()
	{
		Given()
			.NavigateTo("/Account/Login")
		.When()
			.Execute()
		.Then()
			.AssertEditable("#Input\\.Email");
	}

	[Fact(DisplayName = "Password input should be editable")]
	public void Should_Have_Editable_Password_Input()
	{
		Given()
			.NavigateTo("/Account/Login")
		.When()
			.Execute()
		.Then()
			.AssertEditable("#Input\\.Password");
	}

	// ──────────────────────────────────────────────
	// Focus Assertions
	// ──────────────────────────────────────────────

	[Fact(DisplayName = "Email input should be focused after clicking on it")]
	public void Should_Focus_Email_After_Click()
	{
		Given()
			.NavigateTo("/Account/Login")
			.Click("#Input\\.Email")
		.When()
			.Execute()
		.Then()
			.AssertFocused("#Input\\.Email");
	}

	[Fact(DisplayName = "Email input should be focused using FocusByLabel action")]
	public void Should_Focus_Email_By_Label()
	{
		Given()
			.NavigateTo("/Account/Login")
			.FocusByLabel("Email")
		.When()
			.Execute()
		.Then()
			.AssertFocusedByLabel("Email");
	}

	[Fact(DisplayName = "Password input should not be focused when Email is focused")]
	public void Should_Not_Focus_Password_When_Email_Focused()
	{
		Given()
			.NavigateTo("/Account/Login")
			.FocusByLabel("Email")
		.When()
			.Execute()
		.Then()
			.AssertNotFocused("#Input\\.Password");
	}

	// ──────────────────────────────────────────────
	// Clear Input
	// ──────────────────────────────────────────────

	[Fact(DisplayName = "Clear should empty a previously filled Email input")]
	public void Should_Clear_Email_Input()
	{
		Given()
			.NavigateTo("/Account/Login")
			.FillByLabel("Email", "test@example.com")
			.ClearByLabel("Email")
		.When()
			.Execute()
		.Then()
			.AssertInputValueByLabel("Email", "");
	}

	// ──────────────────────────────────────────────
	// TypeText (character-by-character)
	// ──────────────────────────────────────────────

	[Fact(DisplayName = "TypeText should type email character by character")]
	public void Should_Type_Email_Character_By_Character()
	{
		Given()
			.NavigateTo("/Account/Login")
			.Click("#Input\\.Email")
			.TypeText("#Input\\.Email", "user@test.com")
		.When()
			.Execute()
		.Then()
			.AssertInputValue("#Input\\.Email", "user@test.com");
	}

	// ──────────────────────────────────────────────
	// FillByPlaceholder
	// ──────────────────────────────────────────────

	[Fact(DisplayName = "FillByPlaceholder should fill email using placeholder text")]
	public void Should_Fill_By_Placeholder()
	{
		Given()
			.NavigateTo("/Account/Login")
			.FillByPlaceholder("name@example.com", "admin@myapp.com")
		.When()
			.Execute()
		.Then()
			.AssertInputValue("#Input\\.Email", "admin@myapp.com");
	}

	// ──────────────────────────────────────────────
	// Checked / Not Checked State (Remember me)
	// ──────────────────────────────────────────────

	[Fact(DisplayName = "Remember me checkbox should not be checked initially")]
	public void Should_Not_Be_Checked_Initially()
	{
		Given()
			.NavigateTo("/Account/Login")
		.When()
			.Execute()
		.Then()
			.AssertNotChecked(".darker-border-checkbox");
	}

	[Fact(DisplayName = "Remember me checkbox should be checked after CheckByLabel")]
	public void Should_Check_Remember_Me()
	{
		Given()
			.NavigateTo("/Account/Login")
			.CheckByLabel("Remember me")
		.When()
			.Execute()
		.Then()
			.AssertChecked(".darker-border-checkbox");
	}

	[Fact(DisplayName = "Should uncheck Remember me after checking it")]
	public void Should_Uncheck_Remember_Me()
	{
		Given()
			.NavigateTo("/Account/Login")
			.CheckByLabel("Remember me")
			.UncheckByLabel("Remember me")
		.When()
			.Execute()
		.Then()
			.AssertNotChecked(".darker-border-checkbox");
	}

	[Fact(DisplayName = "SetChecked should set Remember me to checked state")]
	public void Should_SetChecked_Remember_Me()
	{
		Given()
			.NavigateTo("/Account/Login")
			.SetChecked(".darker-border-checkbox", true)
		.When()
			.Execute()
		.Then()
			.AssertChecked(".darker-border-checkbox");
	}

	// ──────────────────────────────────────────────
	// Enabled / Disabled Assertions
	// ──────────────────────────────────────────────

	[Fact(DisplayName = "Log in button should be enabled")]
	public void Should_Have_Enabled_Login_Button()
	{
		Given()
			.NavigateTo("/Account/Login")
		.When()
			.Execute()
		.Then()
			.AssertEnabled("button.btn-primary[type='submit']");
	}

	// ──────────────────────────────────────────────
	// CSS Class Assertions
	// ──────────────────────────────────────────────

	[Fact(DisplayName = "Log in button should contain btn-primary CSS class")]
	public void Should_Have_Primary_Button_Class()
	{
		Given()
			.NavigateTo("/Account/Login")
		.When()
			.Execute()
		.Then()
			.AssertContainsClass("button.btn-primary[type='submit']", "btn-primary");
	}

	[Fact(DisplayName = "Email input should contain form-control CSS class")]
	public void Should_Have_Form_Control_Class()
	{
		Given()
			.NavigateTo("/Account/Login")
		.When()
			.Execute()
		.Then()
			.AssertContainsClass("#Input\\.Email", "form-control");
	}

	// ──────────────────────────────────────────────
	// Attribute Assertions (aria-required)
	// ──────────────────────────────────────────────

	[Fact(DisplayName = "Email input should have aria-required attribute set to true")]
	public void Should_Have_AriaRequired_On_Email()
	{
		Given()
			.NavigateTo("/Account/Login")
		.When()
			.Execute()
		.Then()
			.AssertAttribute("#Input\\.Email", "aria-required", "true");
	}

	[Fact(DisplayName = "Password input should have autocomplete attribute")]
	public void Should_Have_Autocomplete_On_Password()
	{
		Given()
			.NavigateTo("/Account/Login")
		.When()
			.Execute()
		.Then()
			.AssertAttribute("#Input\\.Password", "autocomplete", "current-password");
	}

	// ──────────────────────────────────────────────
	// ID Assertions
	// ──────────────────────────────────────────────

	[Fact(DisplayName = "Email input should have correct element ID")]
	public void Should_Have_Correct_Email_Id()
	{
		Given()
			.NavigateTo("/Account/Login")
		.When()
			.Execute()
		.Then()
			.AssertId("#Input\\.Email", "Input.Email");
	}

	// ──────────────────────────────────────────────
	// Expect (Auto-Retry) Form Assertions
	// ──────────────────────────────────────────────

	[Fact(DisplayName = "ExpectVisible should auto-retry for login form heading")]
	public void Should_ExpectVisible_Login_Heading()
	{
		Given()
			.NavigateTo("/Account/Login")
		.When()
			.Execute()
		.Then()
			.ExpectVisible(LocatorStrategy.ByRole(AriaRole.Heading, "Log in"));
	}

	[Fact(DisplayName = "ExpectEditable should auto-retry for Email input")]
	public void Should_ExpectEditable_Email()
	{
		Given()
			.NavigateTo("/Account/Login")
		.When()
			.Execute()
		.Then()
			.ExpectEditable("#Input\\.Email");
	}

	[Fact(DisplayName = "ExpectEnabled should auto-retry for submit button")]
	public void Should_ExpectEnabled_Submit_Button()
	{
		Given()
			.NavigateTo("/Account/Login")
		.When()
			.Execute()
		.Then()
			.ExpectEnabled("button.btn-primary[type='submit']");
	}

	[Fact(DisplayName = "ExpectNotChecked should verify Remember me is unchecked")]
	public void Should_ExpectNotChecked_RememberMe()
	{
		Given()
			.NavigateTo("/Account/Login")
		.When()
			.Execute()
		.Then()
			.ExpectNotChecked(".darker-border-checkbox");
	}

	[Fact(DisplayName = "ExpectChecked should verify Remember me after checking")]
	public void Should_ExpectChecked_After_Check()
	{
		Given()
			.NavigateTo("/Account/Login")
			.CheckByLabel("Remember me")
		.When()
			.Execute()
		.Then()
			.ExpectChecked(".darker-border-checkbox");
	}

	[Fact(DisplayName = "ExpectValue should verify filled email input")]
	public void Should_ExpectValue_Email()
	{
		Given()
			.NavigateTo("/Account/Login")
			.FillByLabel("Email", "test@example.com")
		.When()
			.Execute()
		.Then()
			.ExpectValue("#Input\\.Email", "test@example.com");
	}

	[Fact(DisplayName = "ExpectAttribute should verify placeholder attribute")]
	public void Should_ExpectAttribute_Placeholder()
	{
		Given()
			.NavigateTo("/Account/Login")
		.When()
			.Execute()
		.Then()
			.ExpectAttribute("#Input\\.Email", "placeholder", "name@example.com");
	}

	[Fact(DisplayName = "ExpectFocused should verify focused element after click")]
	public void Should_ExpectFocused_After_Click()
	{
		Given()
			.NavigateTo("/Account/Login")
			.Click("#Input\\.Email")
		.When()
			.Execute()
		.Then()
			.ExpectFocused("#Input\\.Email");
	}

	[Fact(DisplayName = "ExpectNotFocused should verify non-focused element")]
	public void Should_ExpectNotFocused_Password()
	{
		Given()
			.NavigateTo("/Account/Login")
			.Click("#Input\\.Email")
		.When()
			.Execute()
		.Then()
			.ExpectNotFocused("#Input\\.Password");
	}

	// ──────────────────────────────────────────────
	// Full form interaction chain
	// ──────────────────────────────────────────────

	[Fact(DisplayName = "Should fill and validate complete login form with advanced assertions")]
	public void Should_Fill_And_Validate_Complete_Form()
	{
		Given()
			.NavigateTo("/Account/Login")
			.FillByPlaceholder("name@example.com", "admin@myapp.com")
			.FillByLabel("Password", "S3cureP@ss!")
			.CheckByLabel("Remember me")
		.When()
			.Execute()
		.Then()
			.AssertSuccess()
			.AssertEditable("#Input\\.Email")
			.AssertEditable("#Input\\.Password")
			.AssertInputValueByLabel("Email", "admin@myapp.com")
			.AssertInputValueByLabel("Password", "S3cureP@ss!")
			.AssertChecked(".darker-border-checkbox")
			.AssertEnabled("button.btn-primary[type='submit']")
			.AssertContainsClass("button.btn-primary[type='submit']", "btn-primary")
			.ExpectValue("#Input\\.Email", "admin@myapp.com");
	}
}
