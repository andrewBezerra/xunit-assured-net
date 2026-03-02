using Microsoft.Playwright;

using XUnitAssured.Playwright.Extensions;
using XUnitAssured.Playwright.Locators;

namespace XUnitAssured.Playwright.Samples.Remote.Test;

[Trait("Category", "E2E")]
[Trait("Environment", "Remote")]
[Trait("Component", "TodoCrud")]
/// <summary>
/// E2E tests for TodoMVC CRUD operations on the remote Playwright demo site.
/// Demonstrates creating, completing, filtering, and deleting todo items
/// using the XUnitAssured Playwright fluent DSL.
/// Target: https://demo.playwright.dev/todomvc
/// </summary>
public class TodoCrudTests : PlaywrightSamplesRemoteTestBase, IClassFixture<PlaywrightSamplesRemoteFixture>
{
	public TodoCrudTests(PlaywrightSamplesRemoteFixture fixture) : base(fixture) { }

	[Fact(Skip = "Remote test - requires internet access", DisplayName = "Should add a new todo item")]
	public void Should_Add_Todo()
	{
		Given()
			.NavigateTo("/")
			.Fill(".new-todo", "Buy groceries")
			.Press(".new-todo", "Enter")
		.When()
			.Execute()
		.Then()
			.AssertVisibleByText("Buy groceries");
	}

	[Fact(Skip = "Remote test - requires internet access", DisplayName = "Should add multiple todo items")]
	public void Should_Add_Multiple_Todos()
	{
		Given()
			.NavigateTo("/")
			.Fill(".new-todo", "Buy groceries")
			.Press(".new-todo", "Enter")
			.Fill(".new-todo", "Clean the house")
			.Press(".new-todo", "Enter")
			.Fill(".new-todo", "Write tests")
			.Press(".new-todo", "Enter")
		.When()
			.Execute()
		.Then()
			.AssertVisibleByText("Buy groceries")
			.AssertVisibleByText("Clean the house")
			.AssertVisibleByText("Write tests");
	}

	[Fact(Skip = "Remote test - requires internet access", DisplayName = "Should mark a todo item as completed")]
	public void Should_Complete_Todo()
	{
		Given()
			.NavigateTo("/")
			.Fill(".new-todo", "Learn XUnitAssured")
			.Press(".new-todo", "Enter")
			// Click the toggle checkbox for the first todo
			.Click(".todo-list li .toggle")
		.When()
			.Execute()
		.Then()
			.AssertVisible(".todo-list li.completed");
	}

	[Fact(Skip = "Remote test - requires internet access", DisplayName = "Should display items remaining count")]
	public void Should_Display_Items_Remaining()
	{
		Given()
			.NavigateTo("/")
			.Fill(".new-todo", "First item")
			.Press(".new-todo", "Enter")
			.Fill(".new-todo", "Second item")
			.Press(".new-todo", "Enter")
		.When()
			.Execute()
		.Then()
			.AssertVisibleByText("2 items left");
	}

	[Fact(Skip = "Remote test - requires internet access", DisplayName = "Should filter active todo items")]
	public void Should_Filter_Active_Todos()
	{
		Given()
			.NavigateTo("/")
			.Fill(".new-todo", "Active task")
			.Press(".new-todo", "Enter")
			.Fill(".new-todo", "Completed task")
			.Press(".new-todo", "Enter")
			// Complete the second item
			.Click(".todo-list li:nth-child(2) .toggle")
			// Click the "Active" filter link
			.ClickByText("Active")
		.When()
			.Execute()
		.Then()
			.AssertVisibleByText("Active task")
			.ExpectHidden(LocatorStrategy.ByText("Completed task"));
	}

	[Fact(Skip = "Remote test - requires internet access", DisplayName = "Should filter completed todo items")]
	public void Should_Filter_Completed_Todos()
	{
		Given()
			.NavigateTo("/")
			.Fill(".new-todo", "Active task")
			.Press(".new-todo", "Enter")
			.Fill(".new-todo", "Completed task")
			.Press(".new-todo", "Enter")
			// Complete the second item
			.Click(".todo-list li:nth-child(2) .toggle")
			// Click the "Completed" filter link
			.ClickByText("Completed")
		.When()
			.Execute()
		.Then()
			.AssertVisibleByText("Completed task")
			.ExpectHidden(LocatorStrategy.ByText("Active task"));
	}

	[Fact(Skip = "Remote test - requires internet access", DisplayName = "Should clear completed todo items")]
	public void Should_Clear_Completed()
	{
		Given()
			.NavigateTo("/")
			.Fill(".new-todo", "Task to keep")
			.Press(".new-todo", "Enter")
			.Fill(".new-todo", "Task to clear")
			.Press(".new-todo", "Enter")
			// Complete the second item
			.Click(".todo-list li:nth-child(2) .toggle")
			// Click "Clear completed" button
			.ClickByText("Clear completed")
		.When()
			.Execute()
		.Then()
			.AssertVisibleByText("Task to keep")
			.ExpectHidden(LocatorStrategy.ByText("Task to clear"));
	}

	[Fact(Skip = "Remote test - requires internet access", DisplayName = "Complete workflow should add, complete, filter, and clear todos")]
	public void Complete_Workflow()
	{
		// Step 1: Add todos
		Given()
			.NavigateTo("/")
			.Fill(".new-todo", "Write Playwright tests")
			.Press(".new-todo", "Enter")
			.Fill(".new-todo", "Review pull request")
			.Press(".new-todo", "Enter")
			.Fill(".new-todo", "Deploy to staging")
			.Press(".new-todo", "Enter")
		.When()
			.Execute()
		.Then()
			.AssertVisibleByText("3 items left");

		// Step 2: Complete one todo
		Given()
			.Click(".todo-list li:nth-child(1) .toggle")
		.When()
			.Execute()
		.Then()
			.AssertVisibleByText("2 items left")
			.AssertVisible(".todo-list li.completed");

		// Step 3: Filter to see only active items
		Given()
			.ClickByText("Active")
		.When()
			.Execute()
		.Then()
			.AssertVisibleByText("Review pull request")
			.AssertVisibleByText("Deploy to staging")
			.ExpectHidden(LocatorStrategy.ByText("Write Playwright tests"));

		// Step 4: Show all again
		Given()
			.ClickByText("All")
		.When()
			.Execute()
		.Then()
			.AssertVisibleByText("Write Playwright tests")
			.AssertVisibleByText("Review pull request")
			.AssertVisibleByText("Deploy to staging");
	}
}
