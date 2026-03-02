using Microsoft.Playwright;
using Shouldly;

using XUnitAssured.Playwright.Extensions;

namespace XunitAssured.PlayWright.Samples.Local.Test;

[Trait("Category", "E2E")]
[Trait("Component", "Navigation")]
/// <summary>
/// E2E tests for navigation behavior in SampleWebApp.
/// Demonstrates testing nav menu links, URL transitions, and 404 handling.
/// </summary>
public class NavigationTests : PlaywrightSamplesTestBase, IClassFixture<PlaywrightSamplesFixture>
{
	public NavigationTests(PlaywrightSamplesFixture fixture) : base(fixture) { }

	[Fact(DisplayName = "Clicking Counter nav link should navigate to /counter")]
	public void Should_Navigate_To_Counter()
	{
		Given()
			.NavigateTo("/")
			.ClickByText("Counter")
		.When()
			.Execute()
		.Then()
			.AssertUrlEndsWith("/counter")
			.AssertVisibleByRole(AriaRole.Heading, "Counter");
	}

	[Fact(DisplayName = "Clicking Weather nav link should navigate to /weather")]
	public void Should_Navigate_To_Weather()
	{
		Given()
			.NavigateTo("/")
			.ClickByText("Weather")
		.When()
			.Execute()
		.Then()
			.AssertUrlEndsWith("/weather")
			.AssertVisibleByRole(AriaRole.Heading, "Weather");
	}

	[Fact(DisplayName = "Clicking Home nav link should navigate back to /")]
	public void Should_Navigate_To_Home()
	{
		Given()
			.NavigateTo("/counter")
			.ClickByText("Home")
		.When()
			.Execute()
		.Then()
			.AssertVisibleByText("Hello, world!");
	}

	[Fact(DisplayName = "Clicking Auth Required nav link should navigate to auth page")]
	public void Should_Navigate_To_Auth_Page()
	{
		Given()
			.NavigateTo("/")
			.ClickByText("Auth Required")
		.When()
			.Execute()
		.Then()
			.AssertUrlContains("auth");
	}

	[Fact(DisplayName = "Non-existent route should display Not Found page")]
	public void Should_Display_Not_Found_Page()
	{
		Given()
			.NavigateTo("/this-page-does-not-exist")
		.When()
			.Execute()
		.Then()
			.AssertVisibleByText("Not Found");
	}

	[Fact(DisplayName = "Clicking SampleWebApp brand should navigate to home")]
	public void Should_Navigate_Home_Via_Brand()
	{
		Given()
			.NavigateTo("/counter")
			.Click(".navbar-brand")
		.When()
			.Execute()
		.Then()
			.AssertVisibleByText("Hello, world!");
	}
}
