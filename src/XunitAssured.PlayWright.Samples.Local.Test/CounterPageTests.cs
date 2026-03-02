using Microsoft.Playwright;
using Shouldly;

using XUnitAssured.Playwright.Extensions;

namespace XunitAssured.PlayWright.Samples.Local.Test;

[Trait("Category", "E2E")]
[Trait("Page", "Counter")]
/// <summary>
/// E2E tests for the Counter page (InteractiveServer Blazor component).
/// Demonstrates testing interactive UI: button clicks, state changes, and element text assertions.
/// </summary>
public class CounterPageTests : PlaywrightSamplesTestBase, IClassFixture<PlaywrightSamplesFixture>
{
	public CounterPageTests(PlaywrightSamplesFixture fixture) : base(fixture) { }

	[Fact(DisplayName = "Counter page should display correct title")]
	public void Should_Display_Counter_Title()
	{
		Given()
			.NavigateTo("/counter")
		.When()
			.Execute()
		.Then()
			.AssertTitleContains("Counter");
	}

	[Fact(DisplayName = "Counter page should display h1 heading")]
	public void Should_Display_Counter_Heading()
	{
		Given()
			.NavigateTo("/counter")
		.When()
			.Execute()
		.Then()
			.AssertVisibleByRole(AriaRole.Heading, "Counter");
	}

	[Fact(DisplayName = "Counter page should display initial count of 0")]
	public void Should_Display_Initial_Count_Zero()
	{
		Given()
			.NavigateTo("/counter")
		.When()
			.Execute()
		.Then()
			.AssertVisibleByText("Current count: 0");
	}

	[Fact(DisplayName = "Counter should display Click me button")]
	public void Should_Display_Click_Button()
	{
		Given()
			.NavigateTo("/counter")
		.When()
			.Execute()
		.Then()
			.AssertVisibleByRole(AriaRole.Button, "Click me");
	}

	[Fact(DisplayName = "Counter should increment to 1 after clicking button once")]
	public void Should_Increment_After_Single_Click()
	{
		Given()
			.NavigateTo("/counter")
			.Wait(500)
			.ClickByRole(AriaRole.Button, "Click me")
		.When()
			.Execute()
		.Then()
			.AssertVisibleByText("Current count: 1");
	}

	[Fact(DisplayName = "Counter should increment to 3 after clicking button three times")]
	public void Should_Increment_After_Multiple_Clicks()
	{
		Given()
			.NavigateTo("/counter")
			.Wait(500)
			.ClickByRole(AriaRole.Button, "Click me")
			.ClickByRole(AriaRole.Button, "Click me")
			.ClickByRole(AriaRole.Button, "Click me")
		.When()
			.Execute()
		.Then()
			.AssertVisibleByText("Current count: 3");
	}

	[Fact(DisplayName = "Counter page URL should end with /counter")]
	public void Should_Have_Correct_Url()
	{
		Given()
			.NavigateTo("/counter")
		.When()
			.Execute()
		.Then()
			.AssertUrlEndsWith("/counter");
	}

	[Fact(DisplayName = "Counter page should open Playwright Inspector for recording")]
	public void Record_and_Pause_Should_Display_Counter()
	{
		Given()
			.NavigateTo("/counter")
			.RecordAndPause()   // Inspector opens — record your actions here
		.When()
			.Execute()
		.Then()
			.AssertSuccess();
	}
}
