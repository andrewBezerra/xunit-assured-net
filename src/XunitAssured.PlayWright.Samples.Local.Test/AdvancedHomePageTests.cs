using Microsoft.Playwright;
using Shouldly;

using XUnitAssured.Playwright.Extensions;
using XUnitAssured.Playwright.Locators;

namespace XunitAssured.PlayWright.Samples.Local.Test;

[Trait("Category", "E2E")]
[Trait("Page", "Home")]
[Trait("Feature", "Advanced")]
/// <summary>
/// Advanced E2E tests for the Home page demonstrating latest XUnitAssured.Playwright features.
/// Covers: PageContent assertions, console error checks, CSS class assertions,
/// in-viewport checks, Expect (auto-retry) assertions, URL/Title regex matching,
/// and accessible name/role assertions.
/// </summary>
public class AdvancedHomePageTests : PlaywrightSamplesTestBase, IClassFixture<PlaywrightSamplesFixture>
{
	public AdvancedHomePageTests(PlaywrightSamplesFixture fixture) : base(fixture) { }

	// ──────────────────────────────────────────────
	// Page Content Assertions
	// ──────────────────────────────────────────────

	[Fact(DisplayName = "Home page HTML should contain the Hello world heading")]
	public void Should_Contain_Hello_World_In_PageContent()
	{
		Given()
			.NavigateTo("/")
		.When()
			.Execute()
		.Then()
			.AssertPageContains("Hello, world!");
	}

	[Fact(DisplayName = "Home page HTML should contain navigation links markup")]
	public void Should_Contain_Navigation_Markup()
	{
		Given()
			.NavigateTo("/")
		.When()
			.Execute()
		.Then()
			.AssertPageContains("nav-link")
			.AssertPageContains("SampleWebApp");
	}

	// ──────────────────────────────────────────────
	// Console Error Assertions
	// ──────────────────────────────────────────────

	[Fact(DisplayName = "Home page should have no browser console errors")]
	public void Should_Have_No_Console_Errors()
	{
		Given()
			.NavigateTo("/")
		.When()
			.Execute()
		.Then()
			.AssertNoConsoleErrors();
	}

	// ──────────────────────────────────────────────
	// CSS Class Assertions
	// ──────────────────────────────────────────────

	[Fact(DisplayName = "Navbar brand should have navbar-brand CSS class")]
	public void Should_Have_Navbar_Brand_Class()
	{
		Given()
			.NavigateTo("/")
		.When()
			.Execute()
		.Then()
			.AssertContainsClass(".navbar-brand", "navbar-brand");
	}

	// ──────────────────────────────────────────────
	// In-Viewport Assertions
	// ──────────────────────────────────────────────

	[Fact(DisplayName = "Hello world heading should be in viewport on page load")]
	public void Should_Have_Heading_In_Viewport()
	{
		Given()
			.NavigateTo("/")
		.When()
			.Execute()
		.Then()
			.AssertInViewport("h1");
	}

	// ──────────────────────────────────────────────
	// Attached Assertions
	// ──────────────────────────────────────────────

	[Fact(DisplayName = "Navigation menu should be attached to the DOM")]
	public void Should_Have_Nav_Menu_Attached()
	{
		Given()
			.NavigateTo("/")
		.When()
			.Execute()
		.Then()
			.AssertAttached(".nav-scrollable");
	}

	// ──────────────────────────────────────────────
	// URL Regex Matching
	// ──────────────────────────────────────────────

	[Fact(DisplayName = "Home page URL should match HTTP protocol pattern")]
	public void Should_Match_Url_Pattern()
	{
		Given()
			.NavigateTo("/")
		.When()
			.Execute()
		.Then()
			.AssertUrlMatches(@"^https?://");
	}

	// ──────────────────────────────────────────────
	// Text Assertions
	// ──────────────────────────────────────────────

	[Fact(DisplayName = "Home page h1 should have exact text Hello world")]
	public void Should_Have_Correct_H1_Text()
	{
		Given()
			.NavigateTo("/")
		.When()
			.Execute()
		.Then()
			.AssertText("h1", "Hello, world!");
	}

	[Fact(DisplayName = "Home page should contain welcome text in body")]
	public void Should_Contain_Welcome_Text()
	{
		Given()
			.NavigateTo("/")
		.When()
			.Execute()
		.Then()
			.AssertTextContains("article", "Welcome to your new app");
	}

	// ──────────────────────────────────────────────
	// Element Not Empty Assertions
	// ──────────────────────────────────────────────

	[Fact(DisplayName = "Navigation section should not be empty")]
	public void Should_Have_Non_Empty_Navigation()
	{
		Given()
			.NavigateTo("/")
		.When()
			.Execute()
		.Then()
			.AssertNotEmpty("nav");
	}

	// ──────────────────────────────────────────────
	// Expect (Auto-Retry) Assertions
	// ──────────────────────────────────────────────

	[Fact(DisplayName = "ExpectVisible should auto-retry for h1 heading")]
	public void Should_ExpectVisible_H1()
	{
		Given()
			.NavigateTo("/")
		.When()
			.Execute()
		.Then()
			.ExpectVisible("h1");
	}

	[Fact(DisplayName = "ExpectVisible with LocatorStrategy should find heading by role")]
	public void Should_ExpectVisible_By_LocatorStrategy()
	{
		Given()
			.NavigateTo("/")
		.When()
			.Execute()
		.Then()
			.ExpectVisible(LocatorStrategy.ByRole(AriaRole.Heading, "Hello, world!"));
	}

	[Fact(DisplayName = "ExpectText should auto-retry for h1 text content")]
	public void Should_ExpectText_H1()
	{
		Given()
			.NavigateTo("/")
		.When()
			.Execute()
		.Then()
			.ExpectText("h1", "Hello, world!");
	}

	[Fact(DisplayName = "ExpectContainsText should match partial text in article")]
	public void Should_ExpectContainsText_Welcome()
	{
		Given()
			.NavigateTo("/")
		.When()
			.Execute()
		.Then()
			.ExpectContainsText("article", "Welcome");
	}

	[Fact(DisplayName = "ExpectTitle should auto-retry for page title")]
	public void Should_ExpectTitle_Home()
	{
		Given()
			.NavigateTo("/")
		.When()
			.Execute()
		.Then()
			.ExpectTitle("Home");
	}

	[Fact(DisplayName = "ExpectUrl should auto-retry for page URL")]
	public void Should_ExpectUrl_Pattern()
	{
		Given()
			.NavigateTo("/")
		.When()
			.Execute()
		.Then()
			.ExpectUrl(new System.Text.RegularExpressions.Regex(@"/$"));
	}

	[Fact(DisplayName = "ExpectAttached should verify element is in DOM")]
	public void Should_ExpectAttached_Nav()
	{
		Given()
			.NavigateTo("/")
		.When()
			.Execute()
		.Then()
			.ExpectAttached("nav");
	}

	[Fact(DisplayName = "ExpectInViewport should verify element is visible in viewport")]
	public void Should_ExpectInViewport_H1()
	{
		Given()
			.NavigateTo("/")
		.When()
			.Execute()
		.Then()
			.ExpectInViewport("h1");
	}

	// ──────────────────────────────────────────────
	// Fluent chain with multiple assertion types
	// ──────────────────────────────────────────────

	[Fact(DisplayName = "Should chain multiple advanced assertion types fluently")]
	public void Should_Chain_Multiple_Advanced_Assertions()
	{
		Given()
			.NavigateTo("/")
		.When()
			.Execute()
		.Then()
			.AssertSuccess()
			.AssertPageContains("Hello, world!")
			.AssertNoConsoleErrors()
			.AssertInViewport("h1")
			.AssertText("h1", "Hello, world!")
			.AssertNotEmpty("nav")
			.AssertAttached(".navbar-brand")
			.AssertUrlMatches(@"^https?://")
			.ExpectVisible("h1")
			.ExpectTitle("Home");
	}
}
