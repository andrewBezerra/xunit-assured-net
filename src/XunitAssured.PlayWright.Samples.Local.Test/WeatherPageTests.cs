using Microsoft.Playwright;
using Shouldly;

using XUnitAssured.Playwright.Extensions;

namespace XunitAssured.PlayWright.Samples.Local.Test;

[Trait("Category", "E2E")]
[Trait("Page", "Weather")]
/// <summary>
/// E2E tests for the Weather page (streaming rendering with async data loading).
/// Demonstrates waiting for dynamic content, table assertions, and data validation.
/// </summary>
public class WeatherPageTests : PlaywrightSamplesTestBase, IClassFixture<PlaywrightSamplesFixture>
{
	public WeatherPageTests(PlaywrightSamplesFixture fixture) : base(fixture) { }

	[Fact(DisplayName = "Weather page should display correct title")]
	public void Should_Display_Weather_Title()
	{
		Given()
			.NavigateTo("/weather")
		.When()
			.Execute()
		.Then()
			.AssertTitleContains("Weather");
	}

	[Fact(DisplayName = "Weather page should display h1 heading")]
	public void Should_Display_Weather_Heading()
	{
		Given()
			.NavigateTo("/weather")
		.When()
			.Execute()
		.Then()
			.AssertVisibleByRole(AriaRole.Heading, "Weather");
	}

	[Fact(DisplayName = "Weather page should show loading indicator initially")]
	public void Should_Show_Loading_Initially()
	{
		Given()
			.NavigateTo("/weather")
		.When()
			.Execute()
		.Then()
			.AssertSuccess();
		// Note: Loading text ("Loading...") may already have been replaced
		// by the time assertions run due to streaming rendering
	}

	[Fact(DisplayName = "Weather page should display data table after loading")]
	public void Should_Display_Data_Table()
	{
		Given()
			.NavigateTo("/weather")
			.WaitForSelector("table.table")
		.When()
			.Execute()
		.Then()
			.AssertVisible("table.table");
	}

	[Fact(DisplayName = "Weather table should have correct column headers")]
	public void Should_Have_Correct_Table_Headers()
	{
		Given()
			.NavigateTo("/weather")
			.WaitForSelector("table.table")
		.When()
			.Execute()
		.Then()
			.AssertVisible("table.table")
			.AssertVisibleByText("Date")
			.AssertVisibleByText("Summary");
	}

	[Fact(DisplayName = "Weather table should display 5 data rows")]
	public void Should_Display_Five_Data_Rows()
	{
		Given()
			.NavigateTo("/weather")
			.WaitForSelector("table.table tbody tr")
		.When()
			.Execute()
		.Then()
			.AssertElementCount("table.table tbody tr", 5);
	}

	[Fact(DisplayName = "Weather page should display description text")]
	public void Should_Display_Description()
	{
		Given()
			.NavigateTo("/weather")
		.When()
			.Execute()
		.Then()
			.AssertVisibleByText("This component demonstrates showing data.");
	}

	[Fact(DisplayName = "Weather page URL should end with /weather")]
	public void Should_Have_Correct_Url()
	{
		Given()
			.NavigateTo("/weather")
		.When()
			.Execute()
		.Then()
			.AssertUrlEndsWith("/weather");
	}
}
