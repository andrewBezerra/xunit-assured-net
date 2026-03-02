using Microsoft.Playwright;
using Shouldly;

using XUnitAssured.Playwright.Extensions;
using XUnitAssured.Playwright.Locators;

namespace XunitAssured.PlayWright.Samples.Local.Test;

[Trait("Category", "E2E")]
[Trait("Page", "Register")]
[Trait("Feature", "Advanced")]
/// <summary>
/// E2E tests for the Register page demonstrating XUnitAssured.Playwright features.
/// Covers: Form structure assertions, enabled state, attribute checks,
/// placeholder-based fill, Expect assertions, accessible name, hidden elements,
/// and complete registration form interaction chains.
/// </summary>
public class RegisterPageTests : PlaywrightSamplesTestBase, IClassFixture<PlaywrightSamplesFixture>
{
	public RegisterPageTests(PlaywrightSamplesFixture fixture) : base(fixture) { }

	// ──────────────────────────────────────────────
	// Page Structure
	// ──────────────────────────────────────────────

	[Fact(DisplayName = "Register page should display correct title")]
	public void Should_Display_Register_Title()
	{
		Given()
			.NavigateTo("/Account/Register")
		.When()
			.Execute()
		.Then()
			.AssertTitleContains("Register");
	}

	[Fact(DisplayName = "Register page should display Register heading")]
	public void Should_Display_Register_Heading()
	{
		Given()
			.NavigateTo("/Account/Register")
		.When()
			.Execute()
		.Then()
			.ExpectVisible(LocatorStrategy.ByRole(AriaRole.Heading, "Register"));
	}

	[Fact(DisplayName = "Register page should display Create a new account subheading")]
	public void Should_Display_Create_Account_Subheading()
	{
		Given()
			.NavigateTo("/Account/Register")
		.When()
			.Execute()
		.Then()
			.AssertVisibleByText("Create a new account.");
	}

	// ──────────────────────────────────────────────
	// Form Field Assertions
	// ──────────────────────────────────────────────

	[Fact(DisplayName = "Email input should have correct placeholder")]
	public void Should_Have_Email_Placeholder()
	{
		Given()
			.NavigateTo("/Account/Register")
		.When()
			.Execute()
		.Then()
			.AssertAttribute("#Input\\.Email", "placeholder", "name@example.com");
	}

	[Fact(DisplayName = "Password input should have autocomplete new-password")]
	public void Should_Have_Password_Autocomplete()
	{
		Given()
			.NavigateTo("/Account/Register")
		.When()
			.Execute()
		.Then()
			.AssertAttribute("#Input\\.Password", "autocomplete", "new-password");
	}

	[Fact(DisplayName = "Confirm Password input should have aria-required attribute")]
	public void Should_Have_AriaRequired_On_ConfirmPassword()
	{
		Given()
			.NavigateTo("/Account/Register")
		.When()
			.Execute()
		.Then()
			.AssertAttribute("#Input\\.ConfirmPassword", "aria-required", "true");
	}

	[Fact(DisplayName = "All form inputs should be editable")]
	public void Should_Have_Editable_Inputs()
	{
		Given()
			.NavigateTo("/Account/Register")
		.When()
			.Execute()
		.Then()
			.AssertEditable("#Input\\.Email")
			.AssertEditable("#Input\\.Password")
			.AssertEditable("#Input\\.ConfirmPassword");
	}

	[Fact(DisplayName = "Register button should be enabled")]
	public void Should_Have_Enabled_Register_Button()
	{
		Given()
			.NavigateTo("/Account/Register")
		.When()
			.Execute()
		.Then()
			.AssertEnabled("button[type='submit']");
	}

	[Fact(DisplayName = "Register button should contain btn-primary class")]
	public void Should_Have_Primary_Button_Class()
	{
		Given()
			.NavigateTo("/Account/Register")
		.When()
			.Execute()
		.Then()
			.AssertContainsClass("button[type='submit']", "btn-primary");
	}

	// ──────────────────────────────────────────────
	// Input ID Assertions
	// ──────────────────────────────────────────────

	[Fact(DisplayName = "Email input should have correct ID attribute")]
	public void Should_Have_Email_Id()
	{
		Given()
			.NavigateTo("/Account/Register")
		.When()
			.Execute()
		.Then()
			.AssertId("#Input\\.Email", "Input.Email");
	}

	[Fact(DisplayName = "Confirm Password input should have correct ID attribute")]
	public void Should_Have_ConfirmPassword_Id()
	{
		Given()
			.NavigateTo("/Account/Register")
		.When()
			.Execute()
		.Then()
			.AssertId("#Input\\.ConfirmPassword", "Input.ConfirmPassword");
	}

	// ──────────────────────────────────────────────
	// Fill and Validate Form
	// ──────────────────────────────────────────────

	[Fact(DisplayName = "Should fill Email using FillByPlaceholder")]
	public void Should_Fill_Email_By_Placeholder()
	{
		Given()
			.NavigateTo("/Account/Register")
			.FillByPlaceholder("name@example.com", "newuser@test.com")
		.When()
			.Execute()
		.Then()
			.AssertInputValue("#Input\\.Email", "newuser@test.com");
	}

	[Fact(DisplayName = "Should fill complete registration form and validate all inputs")]
	public void Should_Fill_Complete_Registration_Form()
	{
		Given()
			.NavigateTo("/Account/Register")
			.FillByLabel("Email", "newuser@myapp.com")
			.FillByLabel("Password", "Str0ngP@ssw0rd!", exact: true)
			.FillByLabel("Confirm password", "Str0ngP@ssw0rd!")
		.When()
			.Execute()
		.Then()
			.AssertInputValueByLabel("Email", "newuser@myapp.com")
			.AssertInputValueByLabel("Password", "Str0ngP@ssw0rd!", exact: true)
			.AssertInputValueByLabel("Confirm password", "Str0ngP@ssw0rd!");
	}

	[Fact(DisplayName = "Should clear and refill Email input")]
	public void Should_Clear_And_Refill_Email()
	{
		Given()
			.NavigateTo("/Account/Register")
			.FillByLabel("Email", "wrong@email.com")
			.ClearByLabel("Email")
			.FillByLabel("Email", "correct@email.com")
		.When()
			.Execute()
		.Then()
			.AssertInputValueByLabel("Email", "correct@email.com");
	}

	// ──────────────────────────────────────────────
	// Expect (Auto-Retry) Assertions
	// ──────────────────────────────────────────────

	[Fact(DisplayName = "ExpectVisible should find Register button")]
	public void Should_ExpectVisible_Register_Button()
	{
		Given()
			.NavigateTo("/Account/Register")
		.When()
			.Execute()
		.Then()
			.ExpectVisible(LocatorStrategy.ByRole(AriaRole.Button, "Register"));
	}

	[Fact(DisplayName = "ExpectEditable should auto-retry for all inputs")]
	public void Should_ExpectEditable_All_Inputs()
	{
		Given()
			.NavigateTo("/Account/Register")
		.When()
			.Execute()
		.Then()
			.ExpectEditable("#Input\\.Email")
			.ExpectEditable("#Input\\.Password")
			.ExpectEditable("#Input\\.ConfirmPassword");
	}

	[Fact(DisplayName = "ExpectEnabled should auto-retry for Register button")]
	public void Should_ExpectEnabled_Register_Button()
	{
		Given()
			.NavigateTo("/Account/Register")
		.When()
			.Execute()
		.Then()
			.ExpectEnabled("button[type='submit']");
	}

	// ──────────────────────────────────────────────
	// No Console Errors
	// ──────────────────────────────────────────────

	[Fact(DisplayName = "Register page should have no console errors")]
	public void Should_Have_No_Console_Errors()
	{
		Given()
			.NavigateTo("/Account/Register")
		.When()
			.Execute()
		.Then()
			.AssertNoConsoleErrors();
	}

	// ──────────────────────────────────────────────
	// URL Assertion
	// ──────────────────────────────────────────────

	[Fact(DisplayName = "Register page URL should match pattern")]
	public void Should_Have_Correct_Url()
	{
		Given()
			.NavigateTo("/Account/Register")
		.When()
			.Execute()
		.Then()
			.AssertUrlEndsWith("/Account/Register")
			.AssertUrlMatches(@"Account/Register");
	}

	// ──────────────────────────────────────────────
	// Full chain
	// ──────────────────────────────────────────────

	[Fact(DisplayName = "Should demonstrate full registration form test with advanced assertions")]
	public void Should_Demonstrate_Full_Form_Test()
	{
		Given()
			.NavigateTo("/Account/Register")
			.FillByPlaceholder("name@example.com", "demo@test.com")
			.FillByLabel("Password", "D3moP@ss!", exact: true)
			.FillByLabel("Confirm password", "D3moP@ss!")
		.When()
			.Execute()
		.Then()
			.AssertSuccess()
			.AssertNoConsoleErrors()
			.AssertUrlEndsWith("/Account/Register")
			.AssertEditable("#Input\\.Email")
			.AssertEnabled("button[type='submit']")
			.AssertContainsClass("button[type='submit']", "btn-primary")
			.AssertInputValue("#Input\\.Email", "demo@test.com")
			.ExpectValue("#Input\\.Email", "demo@test.com")
			.ExpectEditable("#Input\\.Password");
	}
}
