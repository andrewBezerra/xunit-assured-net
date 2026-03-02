using Microsoft.Playwright;

using XUnitAssured.Playwright.Extensions;
using XUnitAssured.Playwright.Locators;

namespace XUnitAssured.Playwright.Samples.Remote.Test;

[Trait("Category", "E2E")]
[Trait("Environment", "Remote")]
[Trait("Page", "Home")]
/// <summary>
/// E2E tests for the TodoMVC home page on the remote Playwright demo site.
/// Demonstrates basic navigation, title assertion, and initial page state validation.
/// Target: https://demo.playwright.dev/todomvc
/// </summary>
public class HomePageTests : PlaywrightSamplesRemoteTestBase, IClassFixture<PlaywrightSamplesRemoteFixture>
{
	public HomePageTests(PlaywrightSamplesRemoteFixture fixture) : base(fixture) { }

	[Fact(/*Skip = "Remote test - requires internet access",*/ DisplayName = "TodoMVC should display correct page title")]
	public void Should_Display_Correct_Title()
	{
		Given()
			.NavigateTo("/")
		.When()
			.Execute()
		.Then()
			.AssertTitleContains("TodoMVC");
	}

	[Fact(/*Skip = "Remote test - requires internet access",*/ DisplayName = "TodoMVC should display correct page title")]
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

	[Fact(Skip = "Remote test - requires internet access", DisplayName = "TodoMVC should display main heading")]
	public void Should_Display_Main_Heading()
	{
		Given()
			.NavigateTo("/")
		.When()
			.Execute()
		.Then()
			.AssertVisibleByText("todos", exact: true);
	}

	[Fact(Skip = "Remote test - requires internet access", DisplayName = "TodoMVC should have input field with placeholder")]
	public void Should_Have_Input_Field()
	{
		Given()
			.NavigateTo("/")
		.When()
			.Execute()
		.Then()
			.AssertVisible(".new-todo");
	}

	[Fact(Skip = "Remote test - requires internet access", DisplayName = "TodoMVC should start with empty todo list")]
	public void Should_Start_With_Empty_List()
	{
		Given()
			.NavigateTo("/")
		.When()
			.Execute()
		.Then()
			.ExpectCount(".todo-list li", 0);
	}

	[Fact(Skip = "Remote test - requires internet access", DisplayName = "TodoMVC URL should match base URL")]
	public void Should_Have_Correct_Url()
	{
		Given()
			.NavigateTo("/")
		.When()
			.Execute()
		.Then()
			.AssertUrlContains("demo.playwright.dev/todomvc");
	}
}
