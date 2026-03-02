using Microsoft.Playwright;
using Shouldly;

using XUnitAssured.Playwright.Extensions;

namespace XunitAssured.PlayWright.Samples.Local.Test;

[Trait("Category", "E2E")]
[Trait("Page", "Home")]
/// <summary>
/// E2E tests for the Home page of SampleWebApp.
/// Demonstrates basic navigation, title assertion, and content visibility.
/// </summary>
public class HomePageTests : PlaywrightSamplesTestBase, IClassFixture<PlaywrightSamplesFixture>
{
	public HomePageTests(PlaywrightSamplesFixture fixture) : base(fixture) { }

	[Fact(DisplayName = "Home page should display correct title")]
	public void Should_Display_Correct_Title()
	{
		Given()
			.NavigateTo("/")
		.When()
			.Execute()
		.Then()
			.AssertTitleContains("Home");
	}

	[Fact(DisplayName = "Home page should display Hello World heading")]
	public void Should_Display_Hello_World_Heading()
	{
		Given()
			.NavigateTo("/")
		.When()
			.Execute()
		.Then()
			.AssertVisibleByText("Hello, world!", exact: true);
	}

	[Fact(DisplayName = "Home page should have navigation menu with expected links")]
	public void Should_Have_Navigation_Links()
	{
		Given()
			.NavigateTo("/")
		.When()
			.Execute()
		.Then()
			.AssertVisibleByText("Home")
			.AssertVisibleByText("Counter")
			.AssertVisibleByText("Weather");
	}

	[Fact(DisplayName = "Home page should display SampleWebApp brand in navbar")]
	public void Should_Display_App_Brand()
	{
		Given()
			.NavigateTo("/")
		.When()
			.Execute()
		.Then()
			.AssertVisible(".navbar-brand");
	}

	[Fact(DisplayName = "Home page URL should end with /")]
	public void Should_Have_Correct_Url()
	{
		Given()
			.NavigateTo("/")
		.When()
			.Execute()
		.Then()
			.AssertUrlEndsWith("/");
	}
}
