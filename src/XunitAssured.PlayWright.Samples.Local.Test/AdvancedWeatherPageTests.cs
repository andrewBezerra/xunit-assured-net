using Microsoft.Playwright;
using Shouldly;

using XUnitAssured.Playwright.Extensions;
using XUnitAssured.Playwright.Locators;

namespace XunitAssured.PlayWright.Samples.Local.Test;

[Trait("Category", "E2E")]
[Trait("Page", "Weather")]
[Trait("Feature", "Advanced")]
/// <summary>
/// Advanced E2E tests for the Weather page demonstrating latest XUnitAssured.Playwright features.
/// Covers: Expect auto-retry for streaming/dynamic content, attached assertions,
/// text contains, role assertions, element count with ExpectCount,
/// not-empty assertions, CSS class on table, accessible descriptions,
/// and scroll into view for elements.
/// </summary>
public class AdvancedWeatherPageTests : PlaywrightSamplesTestBase, IClassFixture<PlaywrightSamplesFixture>
{
	public AdvancedWeatherPageTests(PlaywrightSamplesFixture fixture) : base(fixture) { }

	// ──────────────────────────────────────────────
	// Expect (Auto-Retry) for Streaming Content
	// ──────────────────────────────────────────────

	[Fact(DisplayName = "ExpectVisible should auto-retry until table is rendered after streaming")]
	public void Should_ExpectVisible_Table_After_Streaming()
	{
		Given()
			.NavigateTo("/weather")
		.When()
			.Execute()
		.Then()
			.ExpectVisible("table.table");
	}

	[Fact(DisplayName = "ExpectCount should auto-retry until 5 data rows are present")]
	public void Should_ExpectCount_Five_Rows()
	{
		Given()
			.NavigateTo("/weather")
		.When()
			.Execute()
		.Then()
			.ExpectCount("table.table tbody tr", 5);
	}

	[Fact(DisplayName = "ExpectContainsText should find Date header with auto-retry")]
	public void Should_ExpectContainsText_DateHeader()
	{
		Given()
			.NavigateTo("/weather")
		.When()
			.Execute()
		.Then()
			.ExpectContainsText("table.table thead", "Date");
	}

	[Fact(DisplayName = "ExpectContainsText should find Summary header with auto-retry")]
	public void Should_ExpectContainsText_SummaryHeader()
	{
		Given()
			.NavigateTo("/weather")
		.When()
			.Execute()
		.Then()
			.ExpectContainsText("table.table thead", "Summary");
	}

	[Fact(DisplayName = "ExpectHidden should verify Loading text is gone after data loads")]
	public void Should_ExpectHidden_Loading()
	{
		Given()
			.NavigateTo("/weather")
			.WaitForSelector("table.table")
		.When()
			.Execute()
		.Then()
			.ExpectNotVisible("text=Loading...");
	}

	[Fact(DisplayName = "ExpectTitle should auto-retry for Weather page title")]
	public void Should_ExpectTitle_Weather()
	{
		Given()
			.NavigateTo("/weather")
		.When()
			.Execute()
		.Then()
			.ExpectTitle("Weather");
	}

	// ──────────────────────────────────────────────
	// Attached Assertions
	// ──────────────────────────────────────────────

	[Fact(DisplayName = "Weather table should be attached to the DOM after loading")]
	public void Should_Have_Table_Attached()
	{
		Given()
			.NavigateTo("/weather")
			.WaitForSelector("table.table")
		.When()
			.Execute()
		.Then()
			.AssertAttached("table.table");
	}

	[Fact(DisplayName = "ExpectAttached should auto-retry for table body")]
	public void Should_ExpectAttached_TableBody()
	{
		Given()
			.NavigateTo("/weather")
		.When()
			.Execute()
		.Then()
			.ExpectAttached("table.table tbody");
	}

	// ──────────────────────────────────────────────
	// Text Contains Assertions
	// ──────────────────────────────────────────────

	[Fact(DisplayName = "Weather description should contain expected text")]
	public void Should_Contain_Description_Text()
	{
		Given()
			.NavigateTo("/weather")
		.When()
			.Execute()
		.Then()
			.AssertTextContains("article", "demonstrates showing data");
	}

	// ──────────────────────────────────────────────
	// Not Empty Assertions
	// ──────────────────────────────────────────────

	[Fact(DisplayName = "Weather table body should not be empty after loading")]
	public void Should_Have_Non_Empty_Table_Body()
	{
		Given()
			.NavigateTo("/weather")
			.WaitForSelector("table.table tbody tr")
		.When()
			.Execute()
		.Then()
			.AssertNotEmpty("table.table tbody");
	}

	[Fact(DisplayName = "ExpectNotEmpty should verify table is populated")]
	public void Should_ExpectNotEmpty_Table()
	{
		Given()
			.NavigateTo("/weather")
			.WaitForSelector("table.table tbody tr")
		.When()
			.Execute()
		.Then()
			.ExpectNotEmpty("table.table tbody");
	}

	// ──────────────────────────────────────────────
	// CSS Class Assertions
	// ──────────────────────────────────────────────

	[Fact(DisplayName = "Weather table should contain table CSS class")]
	public void Should_Have_Table_Css_Class()
	{
		Given()
			.NavigateTo("/weather")
			.WaitForSelector("table.table")
		.When()
			.Execute()
		.Then()
			.AssertContainsClass("table", "table");
	}

	[Fact(DisplayName = "ExpectClass should verify table CSS class")]
	public void Should_ExpectClass_Table()
	{
		Given()
			.NavigateTo("/weather")
			.WaitForSelector("table.table")
		.When()
			.Execute()
		.Then()
			.ExpectClass("table", "table");
	}

	// ──────────────────────────────────────────────
	// Page Content Assertions
	// ──────────────────────────────────────────────

	[Fact(DisplayName = "Weather page HTML should contain table markup after loading")]
	public void Should_Contain_Table_In_PageContent()
	{
		Given()
			.NavigateTo("/weather")
			.WaitForSelector("table.table")
		.When()
			.Execute()
		.Then()
			.AssertPageContains("<table")
			.AssertPageContains("Summary");
	}

	// ──────────────────────────────────────────────
	// In-Viewport Assertions
	// ──────────────────────────────────────────────

	[Fact(DisplayName = "Weather heading should be in viewport")]
	public void Should_Have_Heading_In_Viewport()
	{
		Given()
			.NavigateTo("/weather")
		.When()
			.Execute()
		.Then()
			.AssertInViewportByRole(AriaRole.Heading, "Weather");
	}

	[Fact(DisplayName = "ExpectInViewport should verify heading is visible in viewport")]
	public void Should_ExpectInViewport_Heading()
	{
		Given()
			.NavigateTo("/weather")
		.When()
			.Execute()
		.Then()
			.ExpectInViewport(LocatorStrategy.ByRole(AriaRole.Heading, "Weather"));
	}

	// ──────────────────────────────────────────────
	// Scroll Into View Action
	// ──────────────────────────────────────────────

	[Fact(DisplayName = "Should scroll weather table into view and verify visibility")]
	public void Should_Scroll_Table_Into_View()
	{
		Given()
			.NavigateTo("/weather")
			.WaitForSelector("table.table")
			.ScrollIntoView("table.table")
		.When()
			.Execute()
		.Then()
			.AssertVisible("table.table")
			.AssertInViewport("table.table");
	}

	// ──────────────────────────────────────────────
	// No Console Errors
	// ──────────────────────────────────────────────

	[Fact(DisplayName = "Weather page should have no console errors")]
	public void Should_Have_No_Console_Errors()
	{
		Given()
			.NavigateTo("/weather")
			.WaitForSelector("table.table")
		.When()
			.Execute()
		.Then()
			.AssertNoConsoleErrors();
	}

	// ──────────────────────────────────────────────
	// Full chain with streaming-aware assertions
	// ──────────────────────────────────────────────

	[Fact(DisplayName = "Should demonstrate full Weather page test with Expect auto-retry for streaming")]
	public void Should_Demonstrate_Full_Streaming_Test()
	{
		Given()
			.NavigateTo("/weather")
		.When()
			.Execute()
		.Then()
			.AssertSuccess()
			.AssertUrlEndsWith("/weather")
			.ExpectTitle("Weather")
			.ExpectVisible("table.table")
			.ExpectCount("table.table tbody tr", 5)
			.ExpectContainsText("table.table thead", "Date")
			.ExpectContainsText("table.table thead", "Summary")
			.ExpectNotEmpty("table.table tbody")
			.ExpectAttached("table.table")
			.AssertNoConsoleErrors();
	}
}
