using Microsoft.Playwright;
using Shouldly;

using XUnitAssured.Playwright.Extensions;
using XUnitAssured.Playwright.Locators;

namespace XunitAssured.PlayWright.Samples.Local.Test;

[Trait("Category", "E2E")]
[Trait("Page", "Counter")]
[Trait("Feature", "Advanced")]
/// <summary>
/// Advanced E2E tests for the Counter page demonstrating latest XUnitAssured.Playwright features.
/// Covers: Expect auto-retry assertions for interactive components, enabled/disabled state checks,
/// text regex matching, element count by role, focus assertions, hover actions,
/// scroll into view, and role assertions.
/// </summary>
public class AdvancedCounterPageTests : PlaywrightSamplesTestBase, IClassFixture<PlaywrightSamplesFixture>
{
	public AdvancedCounterPageTests(PlaywrightSamplesFixture fixture) : base(fixture) { }

	// ──────────────────────────────────────────────
	// Expect (Auto-Retry) Assertions
	// ──────────────────────────────────────────────

	[Fact(DisplayName = "ExpectVisible should find Counter heading with auto-retry")]
	public void Should_ExpectVisible_Counter_Heading()
	{
		Given()
			.NavigateTo("/counter")
		.When()
			.Execute()
		.Then()
			.ExpectVisible(LocatorStrategy.ByRole(AriaRole.Heading, "Counter"));
	}

	[Fact(DisplayName = "ExpectText should verify initial count text with auto-retry")]
	public void Should_ExpectText_Initial_Count()
	{
		Given()
			.NavigateTo("/counter")
		.When()
			.Execute()
		.Then()
			.ExpectContainsText("[role='status']", "Current count: 0");
	}

	[Fact(DisplayName = "ExpectContainsText should verify count after click with auto-retry")]
	public void Should_ExpectContainsText_After_Click()
	{
		Given()
			.NavigateTo("/counter")
			.Wait(500)
			.ClickByRole(AriaRole.Button, "Click me")
		.When()
			.Execute()
		.Then()
			.ExpectContainsText("[role='status']", "Current count: 1");
	}

	// ──────────────────────────────────────────────
	// Enabled State Assertions
	// ──────────────────────────────────────────────

	[Fact(DisplayName = "Click me button should be enabled")]
	public void Should_Have_Enabled_Button()
	{
		Given()
			.NavigateTo("/counter")
			.Wait(500)
		.When()
			.Execute()
		.Then()
			.AssertEnabledByRole(AriaRole.Button, "Click me");
	}

	[Fact(DisplayName = "ExpectEnabled should auto-retry for Click me button")]
	public void Should_ExpectEnabled_Button()
	{
		Given()
			.NavigateTo("/counter")
			.Wait(500)
		.When()
			.Execute()
		.Then()
			.ExpectEnabled(LocatorStrategy.ByRole(AriaRole.Button, "Click me"));
	}

	// ──────────────────────────────────────────────
	// Text Regex Matching
	// ──────────────────────────────────────────────

	[Fact(DisplayName = "Counter status text should match regex pattern for count")]
	public void Should_Match_Count_Pattern()
	{
		Given()
			.NavigateTo("/counter")
		.When()
			.Execute()
		.Then()
			.AssertTextMatches("[role='status']", @"Current count: \d+");
	}

	[Fact(DisplayName = "Counter status should match regex after multiple clicks")]
	public void Should_Match_Count_Pattern_After_Clicks()
	{
		Given()
			.NavigateTo("/counter")
			.Wait(500)
			.ClickByRole(AriaRole.Button, "Click me")
			.ClickByRole(AriaRole.Button, "Click me")
			.ClickByRole(AriaRole.Button, "Click me")
			.ClickByRole(AriaRole.Button, "Click me")
			.ClickByRole(AriaRole.Button, "Click me")
		.When()
			.Execute()
		.Then()
			.AssertTextMatches("[role='status']", @"Current count: 5");
	}

	// ──────────────────────────────────────────────
	// Element Count by Role
	// ──────────────────────────────────────────────

	[Fact(DisplayName = "Counter page should have exactly 1 heading")]
	public void Should_Have_Single_Heading()
	{
		Given()
			.NavigateTo("/counter")
		.When()
			.Execute()
		.Then()
			.AssertElementCountByRole(AriaRole.Heading, 1, "Counter");
	}

	// ──────────────────────────────────────────────
	// Role Assertions
	// ──────────────────────────────────────────────

	[Fact(DisplayName = "Counter status paragraph should have status ARIA role")]
	public void Should_Have_Status_Role()
	{
		Given()
			.NavigateTo("/counter")
		.When()
			.Execute()
		.Then()
			.AssertRole("[role='status']", AriaRole.Status);
	}

	// ──────────────────────────────────────────────
	// CSS Class Assertions on Button
	// ──────────────────────────────────────────────

	[Fact(DisplayName = "Click me button should contain btn-primary CSS class")]
	public void Should_Have_Primary_Button_Class()
	{
		Given()
			.NavigateTo("/counter")
			.Wait(500)
		.When()
			.Execute()
		.Then()
			.AssertContainsClass("button.btn", "btn-primary");
	}

	// ──────────────────────────────────────────────
	// In-Viewport Assertions
	// ──────────────────────────────────────────────

	[Fact(DisplayName = "Counter button should be in viewport")]
	public void Should_Have_Button_In_Viewport()
	{
		Given()
			.NavigateTo("/counter")
			.Wait(500)
		.When()
			.Execute()
		.Then()
			.AssertInViewportByRole(AriaRole.Button, "Click me");
	}

	// ──────────────────────────────────────────────
	// Hover Action
	// ──────────────────────────────────────────────

	[Fact(DisplayName = "Should be able to hover over Click me button")]
	public void Should_Hover_Over_Button()
	{
		Given()
			.NavigateTo("/counter")
			.Wait(500)
			.HoverByRole(AriaRole.Button, "Click me")
		.When()
			.Execute()
		.Then()
			.AssertSuccess()
			.AssertVisibleByRole(AriaRole.Button, "Click me");
	}

	// ──────────────────────────────────────────────
	// No Console Errors
	// ──────────────────────────────────────────────

	[Fact(DisplayName = "Counter page should have no console errors after interactions")]
	public void Should_Have_No_Console_Errors_After_Clicks()
	{
		Given()
			.NavigateTo("/counter")
			.Wait(1500)
			.ClickByRole(AriaRole.Button, "Click me")
		.When()
			.Execute()
		.Then()
			.AssertNoConsoleErrors();
	}

	// ──────────────────────────────────────────────
	// Fluent chain with Expect and Assert mixed
	// ──────────────────────────────────────────────

	[Fact(DisplayName = "Should chain Expect and Assert assertions after counter interaction")]
	public void Should_Chain_Expect_And_Assert()
	{
		Given()
			.NavigateTo("/counter")
			.Wait(500)
			.ClickByRole(AriaRole.Button, "Click me")
		.When()
			.Execute()
		.Then()
			.AssertSuccess()
			.AssertNoConsoleErrors()
			.AssertUrlEndsWith("/counter")
			.AssertEnabledByRole(AriaRole.Button, "Click me")
			.AssertTextMatches("[role='status']", @"Current count: 1")
			.ExpectVisible(LocatorStrategy.ByRole(AriaRole.Button, "Click me"))
			.ExpectContainsText("[role='status']", "Current count: 1");
	}
}
