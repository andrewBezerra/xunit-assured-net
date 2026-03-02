using Microsoft.Playwright;
using Shouldly;

using XUnitAssured.Playwright.Extensions;

namespace XunitAssured.PlayWright.Samples.Local.Test;

[Trait("Category", "E2E")]
[Trait("Page", "Login")]
/// <summary>
/// E2E tests for the Login page of SampleWebApp (ASP.NET Identity).
/// Demonstrates testing form elements, fill actions, input value assertions, and form structure.
/// </summary>
public class LoginPageTests : PlaywrightSamplesTestBase, IClassFixture<PlaywrightSamplesFixture>
{
	public LoginPageTests(PlaywrightSamplesFixture fixture) : base(fixture) { }

	[Fact(DisplayName = "Login page should display correct title")]
	public void Should_Display_Login_Title()
	{
		Given()
			.NavigateTo("/Account/Login")
		.When()
			.Execute()
		.Then()
			.AssertTitleContains("Log in");
	}

	[Fact(DisplayName = "Login page should display Log in heading")]
	public void Should_Display_Login_Heading()
	{
		Given()
			.NavigateTo("/Account/Login")
		.When()
			.Execute()
		.Then()
			.AssertVisibleByRole(AriaRole.Heading, "Log in");
	}

	[Fact(DisplayName = "Login page should display Email input field")]
	public void Should_Display_Email_Field()
	{
		Given()
			.NavigateTo("/Account/Login")
		.When()
			.Execute()
		.Then()
			.AssertVisible("label[for='Input.Email']");
	}

	[Fact(DisplayName = "Login page should display Password input field")]
	public void Should_Display_Password_Field()
	{
		Given()
			.NavigateTo("/Account/Login")
		.When()
			.Execute()
		.Then()
			.AssertVisible("label[for='Input.Password']");
	}

	[Fact(DisplayName = "Login page should display Log in submit button")]
	public void Should_Display_Login_Button()
	{
		Given()
			.NavigateTo("/Account/Login")
		.When()
			.Execute()
		.Then()
			.AssertVisibleByRole(AriaRole.Button, "Log in");
	}

	[Fact(DisplayName = "Login page should display Remember me checkbox")]
	public void Should_Display_Remember_Me_Checkbox()
	{
		Given()
			.NavigateTo("/Account/Login")
		.When()
			.Execute()
		.Then()
			.AssertVisibleByText("Remember me");
	}

	[Fact(DisplayName = "Login page should display registration link")]
	public void Should_Display_Register_Link()
	{
		Given()
			.NavigateTo("/Account/Login")
		.When()
			.Execute()
		.Then()
			.AssertVisibleByText("Register as a new user");
	}

	[Fact(DisplayName = "Login page should display forgot password link")]
	public void Should_Display_Forgot_Password_Link()
	{
		Given()
			.NavigateTo("/Account/Login")
		.When()
			.Execute()
		.Then()
			.AssertVisibleByText("Forgot your password?");
	}

	[Fact(DisplayName = "Should fill Email field with value and verify input")]
	public void Should_Fill_Email_Field()
	{
		Given()
			.NavigateTo("/Account/Login")
			.FillByLabel("Email", "test@example.com")
		.When()
			.Execute()
		.Then()
			.AssertInputValueByLabel("Email", "test@example.com");
	}

	[Fact(DisplayName = "Should fill Password field with value and verify input")]
	public void Should_Fill_Password_Field()
	{
		Given()
			.NavigateTo("/Account/Login")
			.FillByLabel("Password", "MySecretP@ss123")
		.When()
			.Execute()
		.Then()
			.AssertInputValueByLabel("Password", "MySecretP@ss123");
	}

	[Fact(DisplayName = "Should fill complete login form using fluent DSL chain")]
	public void Should_Fill_Complete_Login_Form()
	{
		Given()
			.NavigateTo("/Account/Login")
			.FillByLabel("Email", "admin@example.com")
			.FillByLabel("Password", "P@ssw0rd!")
		.When()
			.Execute()
		.Then()
			.AssertInputValueByLabel("Email", "admin@example.com")
			.AssertInputValueByLabel("Password", "P@ssw0rd!");
	}

	[Fact(DisplayName = "Clicking Register link should navigate to registration page")]
	public void Should_Navigate_To_Register()
	{
		Given()
			.NavigateTo("/Account/Login")
			.ClickByText("Register as a new user")
			.WaitForSelector("h2")
		.When()
			.Execute()
		.Then()
			.AssertUrlContains("Account/Register")
			.AssertVisibleByText("Create a new account.");
	}

	[Fact(DisplayName = "Login page should have email placeholder")]
	public void Should_Have_Email_Placeholder()
	{
		Given()
			.NavigateTo("/Account/Login")
		.When()
			.Execute()
		.Then()
			.AssertAttribute("#Input\\.Email", "placeholder", "name@example.com");
	}
}
