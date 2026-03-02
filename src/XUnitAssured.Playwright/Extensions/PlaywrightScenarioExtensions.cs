using System;
using Microsoft.Playwright;
using XUnitAssured.Core.Abstractions;
using XUnitAssured.Playwright.Locators;
using XUnitAssured.Playwright.Steps;

namespace XUnitAssured.Playwright.Extensions;

/// <summary>
/// Extension methods for Playwright UI testing in the fluent DSL.
/// Provides NavigateTo, Click/ClickBy*, Fill/FillBy*, Select, Check, Hover, and more.
/// </summary>
public static class PlaywrightScenarioExtensions
{
	// ──────────────────────────────────────────────
	// Navigation
	// ──────────────────────────────────────────────

	/// <summary>
	/// Navigates to the specified URL.
	/// If a BaseUrl is configured, relative paths are resolved against it.
	/// Usage: Given().NavigateTo("/login") or Given().NavigateTo("https://example.com")
	/// </summary>
	public static ITestScenario NavigateTo(this ITestScenario scenario, string url)
	{
		if (scenario == null) throw new ArgumentNullException(nameof(scenario));
		if (string.IsNullOrWhiteSpace(url)) throw new ArgumentException("URL cannot be null or empty.", nameof(url));

		var step = GetOrCreateStep(scenario);
		step.AddAction(new PageAction
		{
			ActionType = PageActionType.Navigate,
			Value = url
		});

		return scenario;
	}

	// ──────────────────────────────────────────────
	// Click (CSS + By* variants)
	// ──────────────────────────────────────────────

	/// <summary>
	/// Clicks an element using a CSS selector.
	/// Usage: .Click("#login-btn") or .Click("button.submit")
	/// </summary>
	public static ITestScenario Click(this ITestScenario scenario, string cssSelector)
	{
		return AddLocatorAction(scenario, PageActionType.Click, LocatorStrategy.Css(cssSelector));
	}

	/// <summary>
	/// Clicks an element by its ARIA role and optional accessible name.
	/// Usage: .ClickByRole(AriaRole.Button, "Submit")
	/// </summary>
	public static ITestScenario ClickByRole(this ITestScenario scenario, AriaRole role, string? name = null)
	{
		return AddLocatorAction(scenario, PageActionType.Click, LocatorStrategy.ByRole(role, name));
	}

	/// <summary>
	/// Clicks an element by its visible text content.
	/// Usage: .ClickByText("Sign in")
	/// </summary>
	public static ITestScenario ClickByText(this ITestScenario scenario, string text, bool exact = false)
	{
		return AddLocatorAction(scenario, PageActionType.Click, LocatorStrategy.ByText(text, exact));
	}

	/// <summary>
	/// Clicks an element by its associated label text.
	/// Usage: .ClickByLabel("Remember me")
	/// </summary>
	public static ITestScenario ClickByLabel(this ITestScenario scenario, string label, bool exact = false)
	{
		return AddLocatorAction(scenario, PageActionType.Click, LocatorStrategy.ByLabel(label, exact));
	}

	/// <summary>
	/// Clicks an element by its test ID attribute (data-testid by default).
	/// Usage: .ClickByTestId("submit-btn")
	/// </summary>
	public static ITestScenario ClickByTestId(this ITestScenario scenario, string testId)
	{
		return AddLocatorAction(scenario, PageActionType.Click, LocatorStrategy.ByTestId(testId));
	}

	/// <summary>
	/// Clicks an element by its title attribute.
	/// Usage: .ClickByTitle("Close dialog")
	/// </summary>
	public static ITestScenario ClickByTitle(this ITestScenario scenario, string title, bool exact = false)
	{
		return AddLocatorAction(scenario, PageActionType.Click, LocatorStrategy.ByTitle(title, exact));
	}

	// ──────────────────────────────────────────────
	// Double Click (CSS + By* variants)
	// ──────────────────────────────────────────────

	/// <summary>
	/// Double-clicks an element using a CSS selector.
	/// </summary>
	public static ITestScenario DoubleClick(this ITestScenario scenario, string cssSelector)
	{
		return AddLocatorAction(scenario, PageActionType.DoubleClick, LocatorStrategy.Css(cssSelector));
	}

	/// <summary>
	/// Double-clicks an element by its ARIA role.
	/// </summary>
	public static ITestScenario DoubleClickByRole(this ITestScenario scenario, AriaRole role, string? name = null)
	{
		return AddLocatorAction(scenario, PageActionType.DoubleClick, LocatorStrategy.ByRole(role, name));
	}

	/// <summary>
	/// Double-clicks an element by its visible text content.
	/// </summary>
	public static ITestScenario DoubleClickByText(this ITestScenario scenario, string text, bool exact = false)
	{
		return AddLocatorAction(scenario, PageActionType.DoubleClick, LocatorStrategy.ByText(text, exact));
	}

	/// <summary>
	/// Double-clicks an element by its label text.
	/// </summary>
	public static ITestScenario DoubleClickByLabel(this ITestScenario scenario, string label, bool exact = false)
	{
		return AddLocatorAction(scenario, PageActionType.DoubleClick, LocatorStrategy.ByLabel(label, exact));
	}

	/// <summary>
	/// Double-clicks an element by its test ID.
	/// </summary>
	public static ITestScenario DoubleClickByTestId(this ITestScenario scenario, string testId)
	{
		return AddLocatorAction(scenario, PageActionType.DoubleClick, LocatorStrategy.ByTestId(testId));
	}

	/// <summary>
	/// Double-clicks an element by its title attribute.
	/// </summary>
	public static ITestScenario DoubleClickByTitle(this ITestScenario scenario, string title, bool exact = false)
	{
		return AddLocatorAction(scenario, PageActionType.DoubleClick, LocatorStrategy.ByTitle(title, exact));
	}

	// ──────────────────────────────────────────────
	// Fill (CSS + By* variants)
	// ──────────────────────────────────────────────

	/// <summary>
	/// Fills a text input using a CSS selector.
	/// Clears the existing value before filling.
	/// Usage: .Fill("#email", "user@test.com")
	/// </summary>
	public static ITestScenario Fill(this ITestScenario scenario, string cssSelector, string value)
	{
		return AddLocatorAction(scenario, PageActionType.Fill, LocatorStrategy.Css(cssSelector), value);
	}

	/// <summary>
	/// Fills a text input found by its associated label text.
	/// Usage: .FillByLabel("Email", "user@test.com")
	/// </summary>
	public static ITestScenario FillByLabel(this ITestScenario scenario, string label, string value, bool exact = false)
	{
		return AddLocatorAction(scenario, PageActionType.Fill, LocatorStrategy.ByLabel(label, exact), value);
	}

	/// <summary>
	/// Fills a text input found by its placeholder text.
	/// Usage: .FillByPlaceholder("Enter your email", "user@test.com")
	/// </summary>
	public static ITestScenario FillByPlaceholder(this ITestScenario scenario, string placeholder, string value, bool exact = false)
	{
		return AddLocatorAction(scenario, PageActionType.Fill, LocatorStrategy.ByPlaceholder(placeholder, exact), value);
	}

	/// <summary>
	/// Fills a text input found by its ARIA role and accessible name.
	/// Usage: .FillByRole(AriaRole.Textbox, "Search", "playwright")
	/// </summary>
	public static ITestScenario FillByRole(this ITestScenario scenario, AriaRole role, string name, string value)
	{
		return AddLocatorAction(scenario, PageActionType.Fill, LocatorStrategy.ByRole(role, name), value);
	}

	/// <summary>
	/// Fills a text input found by its test ID attribute.
	/// Usage: .FillByTestId("email-input", "user@test.com")
	/// </summary>
	public static ITestScenario FillByTestId(this ITestScenario scenario, string testId, string value)
	{
		return AddLocatorAction(scenario, PageActionType.Fill, LocatorStrategy.ByTestId(testId), value);
	}

	// ──────────────────────────────────────────────
	// Type (character by character, simulates keyboard)
	// ──────────────────────────────────────────────

	/// <summary>
	/// Types text character by character into an element (simulates real keyboard input).
	/// Useful for inputs with autocomplete or real-time validation.
	/// Usage: .TypeText("#search", "playwright")
	/// </summary>
	public static ITestScenario TypeText(this ITestScenario scenario, string cssSelector, string value)
	{
		return AddLocatorAction(scenario, PageActionType.Type, LocatorStrategy.Css(cssSelector), value);
	}

	/// <summary>
	/// Types text character by character into an element found by label.
	/// </summary>
	public static ITestScenario TypeTextByLabel(this ITestScenario scenario, string label, string value, bool exact = false)
	{
		return AddLocatorAction(scenario, PageActionType.Type, LocatorStrategy.ByLabel(label, exact), value);
	}

	/// <summary>
	/// Types text character by character into an element found by its test ID.
	/// </summary>
	public static ITestScenario TypeTextByTestId(this ITestScenario scenario, string testId, string value)
	{
		return AddLocatorAction(scenario, PageActionType.Type, LocatorStrategy.ByTestId(testId), value);
	}

	/// <summary>
	/// Types text character by character into an element found by its placeholder.
	/// </summary>
	public static ITestScenario TypeTextByPlaceholder(this ITestScenario scenario, string placeholder, string value, bool exact = false)
	{
		return AddLocatorAction(scenario, PageActionType.Type, LocatorStrategy.ByPlaceholder(placeholder, exact), value);
	}

	/// <summary>
	/// Types text character by character into an element found by its ARIA role.
	/// </summary>
	public static ITestScenario TypeTextByRole(this ITestScenario scenario, AriaRole role, string name, string value)
	{
		return AddLocatorAction(scenario, PageActionType.Type, LocatorStrategy.ByRole(role, name), value);
	}

	// ──────────────────────────────────────────────
	// Press (keyboard key/shortcut)
	// ──────────────────────────────────────────────

	/// <summary>
	/// Presses a keyboard key or shortcut on an element.
	/// Usage: .Press("#search", "Enter") or .Press("#editor", "Control+A")
	/// </summary>
	public static ITestScenario Press(this ITestScenario scenario, string cssSelector, string key)
	{
		return AddLocatorAction(scenario, PageActionType.Press, LocatorStrategy.Css(cssSelector), key);
	}

	/// <summary>
	/// Presses a keyboard key on an element found by its test ID.
	/// </summary>
	public static ITestScenario PressByTestId(this ITestScenario scenario, string testId, string key)
	{
		return AddLocatorAction(scenario, PageActionType.Press, LocatorStrategy.ByTestId(testId), key);
	}

	/// <summary>
	/// Presses a keyboard key on an element found by its label.
	/// </summary>
	public static ITestScenario PressByLabel(this ITestScenario scenario, string label, string key, bool exact = false)
	{
		return AddLocatorAction(scenario, PageActionType.Press, LocatorStrategy.ByLabel(label, exact), key);
	}

	/// <summary>
	/// Presses a keyboard key on an element found by its ARIA role.
	/// </summary>
	public static ITestScenario PressByRole(this ITestScenario scenario, AriaRole role, string key, string? name = null)
	{
		return AddLocatorAction(scenario, PageActionType.Press, LocatorStrategy.ByRole(role, name), key);
	}

	// ──────────────────────────────────────────────
	// Clear
	// ──────────────────────────────────────────────

	/// <summary>
	/// Clears the value of a text input using a CSS selector.
	/// Usage: .Clear("#email")
	/// </summary>
	public static ITestScenario Clear(this ITestScenario scenario, string cssSelector)
	{
		return AddLocatorAction(scenario, PageActionType.Clear, LocatorStrategy.Css(cssSelector));
	}

	/// <summary>
	/// Clears the value of a text input found by its label.
	/// </summary>
	public static ITestScenario ClearByLabel(this ITestScenario scenario, string label, bool exact = false)
	{
		return AddLocatorAction(scenario, PageActionType.Clear, LocatorStrategy.ByLabel(label, exact));
	}

	/// <summary>
	/// Clears the value of a text input found by its test ID.
	/// </summary>
	public static ITestScenario ClearByTestId(this ITestScenario scenario, string testId)
	{
		return AddLocatorAction(scenario, PageActionType.Clear, LocatorStrategy.ByTestId(testId));
	}

	/// <summary>
	/// Clears the value of a text input found by its ARIA role.
	/// </summary>
	public static ITestScenario ClearByRole(this ITestScenario scenario, AriaRole role, string? name = null)
	{
		return AddLocatorAction(scenario, PageActionType.Clear, LocatorStrategy.ByRole(role, name));
	}

	/// <summary>
	/// Clears the value of a text input found by its placeholder.
	/// </summary>
	public static ITestScenario ClearByPlaceholder(this ITestScenario scenario, string placeholder, bool exact = false)
	{
		return AddLocatorAction(scenario, PageActionType.Clear, LocatorStrategy.ByPlaceholder(placeholder, exact));
	}

	// ──────────────────────────────────────────────
	// Check / Uncheck
	// ──────────────────────────────────────────────

	/// <summary>
	/// Checks a checkbox or radio button using a CSS selector.
	/// Usage: .Check("#agree-terms")
	/// </summary>
	public static ITestScenario Check(this ITestScenario scenario, string cssSelector)
	{
		return AddLocatorAction(scenario, PageActionType.Check, LocatorStrategy.Css(cssSelector));
	}

	/// <summary>
	/// Checks a checkbox found by its label text.
	/// Usage: .CheckByLabel("I agree to the terms")
	/// </summary>
	public static ITestScenario CheckByLabel(this ITestScenario scenario, string label, bool exact = false)
	{
		return AddLocatorAction(scenario, PageActionType.Check, LocatorStrategy.ByLabel(label, exact));
	}

	/// <summary>
	/// Checks a checkbox found by its ARIA role.
	/// </summary>
	public static ITestScenario CheckByRole(this ITestScenario scenario, AriaRole role, string? name = null)
	{
		return AddLocatorAction(scenario, PageActionType.Check, LocatorStrategy.ByRole(role, name));
	}

	/// <summary>
	/// Unchecks a checkbox using a CSS selector.
	/// </summary>
	public static ITestScenario Uncheck(this ITestScenario scenario, string cssSelector)
	{
		return AddLocatorAction(scenario, PageActionType.Uncheck, LocatorStrategy.Css(cssSelector));
	}

	/// <summary>
	/// Unchecks a checkbox found by its label text.
	/// </summary>
	public static ITestScenario UncheckByLabel(this ITestScenario scenario, string label, bool exact = false)
	{
		return AddLocatorAction(scenario, PageActionType.Uncheck, LocatorStrategy.ByLabel(label, exact));
	}

	/// <summary>
	/// Unchecks a checkbox found by its ARIA role.
	/// </summary>
	public static ITestScenario UncheckByRole(this ITestScenario scenario, AriaRole role, string? name = null)
	{
		return AddLocatorAction(scenario, PageActionType.Uncheck, LocatorStrategy.ByRole(role, name));
	}

	/// <summary>
	/// Unchecks a checkbox found by its test ID.
	/// </summary>
	public static ITestScenario UncheckByTestId(this ITestScenario scenario, string testId)
	{
		return AddLocatorAction(scenario, PageActionType.Uncheck, LocatorStrategy.ByTestId(testId));
	}

	// ──────────────────────────────────────────────
	// Select Option
	// ──────────────────────────────────────────────

	/// <summary>
	/// Selects an option from a dropdown using a CSS selector.
	/// Usage: .SelectOption("#country", "BR")
	/// </summary>
	public static ITestScenario SelectOption(this ITestScenario scenario, string cssSelector, string value)
	{
		return AddLocatorAction(scenario, PageActionType.SelectOption, LocatorStrategy.Css(cssSelector), value);
	}

	/// <summary>
	/// Selects an option from a dropdown found by its label.
	/// Usage: .SelectOptionByLabel("Country", "Brazil")
	/// </summary>
	public static ITestScenario SelectOptionByLabel(this ITestScenario scenario, string label, string value, bool exact = false)
	{
		return AddLocatorAction(scenario, PageActionType.SelectOption, LocatorStrategy.ByLabel(label, exact), value);
	}

	/// <summary>
	/// Selects an option from a dropdown found by its test ID.
	/// </summary>
	public static ITestScenario SelectOptionByTestId(this ITestScenario scenario, string testId, string value)
	{
		return AddLocatorAction(scenario, PageActionType.SelectOption, LocatorStrategy.ByTestId(testId), value);
	}

	/// <summary>
	/// Selects an option from a dropdown found by its ARIA role.
	/// </summary>
	public static ITestScenario SelectOptionByRole(this ITestScenario scenario, AriaRole role, string value, string? name = null)
	{
		return AddLocatorAction(scenario, PageActionType.SelectOption, LocatorStrategy.ByRole(role, name), value);
	}

	// ──────────────────────────────────────────────
	// Hover
	// ──────────────────────────────────────────────

	/// <summary>
	/// Hovers over an element using a CSS selector.
	/// Usage: .Hover(".dropdown-trigger")
	/// </summary>
	public static ITestScenario Hover(this ITestScenario scenario, string cssSelector)
	{
		return AddLocatorAction(scenario, PageActionType.Hover, LocatorStrategy.Css(cssSelector));
	}

	/// <summary>
	/// Hovers over an element found by its text content.
	/// </summary>
	public static ITestScenario HoverByText(this ITestScenario scenario, string text, bool exact = false)
	{
		return AddLocatorAction(scenario, PageActionType.Hover, LocatorStrategy.ByText(text, exact));
	}

	/// <summary>
	/// Hovers over an element found by its ARIA role.
	/// </summary>
	public static ITestScenario HoverByRole(this ITestScenario scenario, AriaRole role, string? name = null)
	{
		return AddLocatorAction(scenario, PageActionType.Hover, LocatorStrategy.ByRole(role, name));
	}

	/// <summary>
	/// Hovers over an element found by its test ID.
	/// </summary>
	public static ITestScenario HoverByTestId(this ITestScenario scenario, string testId)
	{
		return AddLocatorAction(scenario, PageActionType.Hover, LocatorStrategy.ByTestId(testId));
	}

	// ──────────────────────────────────────────────
	// Focus
	// ──────────────────────────────────────────────

	/// <summary>
	/// Focuses on an element using a CSS selector.
	/// </summary>
	public static ITestScenario Focus(this ITestScenario scenario, string cssSelector)
	{
		return AddLocatorAction(scenario, PageActionType.Focus, LocatorStrategy.Css(cssSelector));
	}

	/// <summary>
	/// Focuses on an element found by its label text.
	/// </summary>
	public static ITestScenario FocusByLabel(this ITestScenario scenario, string label, bool exact = false)
	{
		return AddLocatorAction(scenario, PageActionType.Focus, LocatorStrategy.ByLabel(label, exact));
	}

	/// <summary>
	/// Focuses on an element found by its ARIA role.
	/// </summary>
	public static ITestScenario FocusByRole(this ITestScenario scenario, AriaRole role, string? name = null)
	{
		return AddLocatorAction(scenario, PageActionType.Focus, LocatorStrategy.ByRole(role, name));
	}

	/// <summary>
	/// Focuses on an element found by its test ID.
	/// </summary>
	public static ITestScenario FocusByTestId(this ITestScenario scenario, string testId)
	{
		return AddLocatorAction(scenario, PageActionType.Focus, LocatorStrategy.ByTestId(testId));
	}

	// ──────────────────────────────────────────────
	// Screenshot
	// ──────────────────────────────────────────────

	/// <summary>
	/// Takes a full-page screenshot during execution.
	/// Usage: .TakeScreenshot("before-submit.png")
	/// </summary>
	public static ITestScenario TakeScreenshot(this ITestScenario scenario, string? fileName = null)
	{
		var step = GetOrCreateStep(scenario);
		step.AddAction(new PageAction
		{
			ActionType = PageActionType.Screenshot,
			Value = fileName
		});
		return scenario;
	}

	// ──────────────────────────────────────────────
	// Wait
	// ──────────────────────────────────────────────

	/// <summary>
	/// Waits for the specified duration in milliseconds.
	/// Usage: .Wait(1000) — waits 1 second
	/// </summary>
	public static ITestScenario Wait(this ITestScenario scenario, int milliseconds)
	{
		var step = GetOrCreateStep(scenario);
		step.AddAction(new PageAction
		{
			ActionType = PageActionType.Wait,
			Value = milliseconds.ToString()
		});
		return scenario;
	}

	/// <summary>
	/// Waits for a specific CSS selector to appear on the page.
	/// Usage: .WaitForSelector(".loading-complete")
	/// </summary>
	public static ITestScenario WaitForSelector(this ITestScenario scenario, string cssSelector)
	{
		var step = GetOrCreateStep(scenario);
		step.AddAction(new PageAction
		{
			ActionType = PageActionType.WaitForSelector,
			Value = cssSelector
		});
		return scenario;
	}

	// ──────────────────────────────────────────────
	// Upload Files
	// ──────────────────────────────────────────────

	/// <summary>
	/// Uploads a single file to a file input using a CSS selector.
	/// Usage: .UploadFile("#file-input", @"C:\docs\resume.pdf")
	/// </summary>
	public static ITestScenario UploadFile(this ITestScenario scenario, string cssSelector, string filePath)
	{
		return AddUploadAction(scenario, LocatorStrategy.Css(cssSelector), new[] { filePath });
	}

	/// <summary>
	/// Uploads a single file to a file input found by its label.
	/// Usage: .UploadFileByLabel("Upload file", @"C:\docs\resume.pdf")
	/// </summary>
	public static ITestScenario UploadFileByLabel(this ITestScenario scenario, string label, string filePath, bool exact = false)
	{
		return AddUploadAction(scenario, LocatorStrategy.ByLabel(label, exact), new[] { filePath });
	}

	/// <summary>
	/// Uploads a single file to a file input found by its test ID.
	/// Usage: .UploadFileByTestId("file-upload", @"C:\docs\resume.pdf")
	/// </summary>
	public static ITestScenario UploadFileByTestId(this ITestScenario scenario, string testId, string filePath)
	{
		return AddUploadAction(scenario, LocatorStrategy.ByTestId(testId), new[] { filePath });
	}

	/// <summary>
	/// Uploads a single file to a file input found by its ARIA role.
	/// </summary>
	public static ITestScenario UploadFileByRole(this ITestScenario scenario, AriaRole role, string filePath, string? name = null)
	{
		return AddUploadAction(scenario, LocatorStrategy.ByRole(role, name), new[] { filePath });
	}

	/// <summary>
	/// Uploads multiple files to a file input using a CSS selector.
	/// Usage: .UploadFiles("#file-input", new[] { "file1.txt", "file2.txt" })
	/// </summary>
	public static ITestScenario UploadFiles(this ITestScenario scenario, string cssSelector, string[] filePaths)
	{
		return AddUploadAction(scenario, LocatorStrategy.Css(cssSelector), filePaths);
	}

	/// <summary>
	/// Uploads multiple files to a file input found by its label.
	/// </summary>
	public static ITestScenario UploadFilesByLabel(this ITestScenario scenario, string label, string[] filePaths, bool exact = false)
	{
		return AddUploadAction(scenario, LocatorStrategy.ByLabel(label, exact), filePaths);
	}

	/// <summary>
	/// Uploads multiple files to a file input found by its test ID.
	/// </summary>
	public static ITestScenario UploadFilesByTestId(this ITestScenario scenario, string testId, string[] filePaths)
	{
		return AddUploadAction(scenario, LocatorStrategy.ByTestId(testId), filePaths);
	}

	/// <summary>
	/// Clears the selected files from a file input using a CSS selector.
	/// Usage: .ClearUploadFile("#file-input")
	/// </summary>
	public static ITestScenario ClearUploadFile(this ITestScenario scenario, string cssSelector)
	{
		return AddLocatorAction(scenario, PageActionType.ClearUploadFile, LocatorStrategy.Css(cssSelector));
	}

	/// <summary>
	/// Clears the selected files from a file input found by its label.
	/// </summary>
	public static ITestScenario ClearUploadFileByLabel(this ITestScenario scenario, string label, bool exact = false)
	{
		return AddLocatorAction(scenario, PageActionType.ClearUploadFile, LocatorStrategy.ByLabel(label, exact));
	}

	/// <summary>
	/// Clears the selected files from a file input found by its test ID.
	/// </summary>
	public static ITestScenario ClearUploadFileByTestId(this ITestScenario scenario, string testId)
	{
		return AddLocatorAction(scenario, PageActionType.ClearUploadFile, LocatorStrategy.ByTestId(testId));
	}

	// ──────────────────────────────────────────────
	// Drag and Drop
	// ──────────────────────────────────────────────

	/// <summary>
	/// Drags an element and drops it onto another element using CSS selectors.
	/// Usage: .DragTo("#draggable", "#droppable")
	/// </summary>
	public static ITestScenario DragTo(this ITestScenario scenario, string sourceCssSelector, string targetCssSelector)
	{
		return AddDragAction(scenario, LocatorStrategy.Css(sourceCssSelector), LocatorStrategy.Css(targetCssSelector));
	}

	/// <summary>
	/// Drags an element by test ID and drops it onto another element by test ID.
	/// Usage: .DragToByTestId("item-1", "drop-zone")
	/// </summary>
	public static ITestScenario DragToByTestId(this ITestScenario scenario, string sourceTestId, string targetTestId)
	{
		return AddDragAction(scenario, LocatorStrategy.ByTestId(sourceTestId), LocatorStrategy.ByTestId(targetTestId));
	}

	/// <summary>
	/// Drags an element by text and drops it onto another element by text.
	/// </summary>
	public static ITestScenario DragToByText(this ITestScenario scenario, string sourceText, string targetText, bool exact = false)
	{
		return AddDragAction(scenario, LocatorStrategy.ByText(sourceText, exact), LocatorStrategy.ByText(targetText, exact));
	}

	/// <summary>
	/// Drags an element using a source locator strategy and drops it onto a target locator strategy.
	/// Most flexible overload for advanced drag-and-drop scenarios.
	/// </summary>
	public static ITestScenario DragTo(this ITestScenario scenario, LocatorStrategy source, LocatorStrategy target)
	{
		return AddDragAction(scenario, source, target);
	}

	// ──────────────────────────────────────────────
	// Scroll Into View
	// ──────────────────────────────────────────────

	/// <summary>
	/// Scrolls an element into the visible area using a CSS selector.
	/// Usage: .ScrollIntoView("#footer")
	/// </summary>
	public static ITestScenario ScrollIntoView(this ITestScenario scenario, string cssSelector)
	{
		return AddLocatorAction(scenario, PageActionType.ScrollIntoView, LocatorStrategy.Css(cssSelector));
	}

	/// <summary>
	/// Scrolls an element into view by its text content.
	/// Usage: .ScrollIntoViewByText("Footer text")
	/// </summary>
	public static ITestScenario ScrollIntoViewByText(this ITestScenario scenario, string text, bool exact = false)
	{
		return AddLocatorAction(scenario, PageActionType.ScrollIntoView, LocatorStrategy.ByText(text, exact));
	}

	/// <summary>
	/// Scrolls an element into view by its test ID.
	/// </summary>
	public static ITestScenario ScrollIntoViewByTestId(this ITestScenario scenario, string testId)
	{
		return AddLocatorAction(scenario, PageActionType.ScrollIntoView, LocatorStrategy.ByTestId(testId));
	}

	/// <summary>
	/// Scrolls an element into view by its ARIA role.
	/// </summary>
	public static ITestScenario ScrollIntoViewByRole(this ITestScenario scenario, AriaRole role, string? name = null)
	{
		return AddLocatorAction(scenario, PageActionType.ScrollIntoView, LocatorStrategy.ByRole(role, name));
	}

	// ──────────────────────────────────────────────
	// Right Click (CSS + By* variants)
	// ──────────────────────────────────────────────

	/// <summary>
	/// Right-clicks (context menu) an element using a CSS selector.
	/// Usage: .RightClick("#context-target")
	/// </summary>
	public static ITestScenario RightClick(this ITestScenario scenario, string cssSelector)
	{
		return AddLocatorAction(scenario, PageActionType.RightClick, LocatorStrategy.Css(cssSelector));
	}

	/// <summary>
	/// Right-clicks an element by its ARIA role.
	/// </summary>
	public static ITestScenario RightClickByRole(this ITestScenario scenario, AriaRole role, string? name = null)
	{
		return AddLocatorAction(scenario, PageActionType.RightClick, LocatorStrategy.ByRole(role, name));
	}

	/// <summary>
	/// Right-clicks an element by its text content.
	/// </summary>
	public static ITestScenario RightClickByText(this ITestScenario scenario, string text, bool exact = false)
	{
		return AddLocatorAction(scenario, PageActionType.RightClick, LocatorStrategy.ByText(text, exact));
	}

	/// <summary>
	/// Right-clicks an element by its test ID.
	/// </summary>
	public static ITestScenario RightClickByTestId(this ITestScenario scenario, string testId)
	{
		return AddLocatorAction(scenario, PageActionType.RightClick, LocatorStrategy.ByTestId(testId));
	}

	/// <summary>
	/// Right-clicks an element by its label text.
	/// </summary>
	public static ITestScenario RightClickByLabel(this ITestScenario scenario, string label, bool exact = false)
	{
		return AddLocatorAction(scenario, PageActionType.RightClick, LocatorStrategy.ByLabel(label, exact));
	}

	// ──────────────────────────────────────────────
	// Force Click
	// ──────────────────────────────────────────────

	/// <summary>
	/// Force-clicks an element, bypassing actionability checks.
	/// Use when an element is overlaid by another element.
	/// Usage: .ForceClick("#hidden-btn")
	/// </summary>
	public static ITestScenario ForceClick(this ITestScenario scenario, string cssSelector)
	{
		return AddForceClickAction(scenario, LocatorStrategy.Css(cssSelector));
	}

	/// <summary>
	/// Force-clicks an element by its ARIA role, bypassing actionability checks.
	/// </summary>
	public static ITestScenario ForceClickByRole(this ITestScenario scenario, AriaRole role, string? name = null)
	{
		return AddForceClickAction(scenario, LocatorStrategy.ByRole(role, name));
	}

	/// <summary>
	/// Force-clicks an element by its test ID, bypassing actionability checks.
	/// </summary>
	public static ITestScenario ForceClickByTestId(this ITestScenario scenario, string testId)
	{
		return AddForceClickAction(scenario, LocatorStrategy.ByTestId(testId));
	}

	// ──────────────────────────────────────────────
	// Click With Modifiers
	// ──────────────────────────────────────────────

	/// <summary>
	/// Clicks an element with keyboard modifiers (Shift, Control, Alt, Meta, ControlOrMeta).
	/// Usage: .ClickWithModifiers("#item", "Shift") or .ClickWithModifiers("#item", "Control", "Shift")
	/// </summary>
	public static ITestScenario ClickWithModifiers(this ITestScenario scenario, string cssSelector, params string[] modifiers)
	{
		return AddClickWithModifiersAction(scenario, LocatorStrategy.Css(cssSelector), modifiers);
	}

	/// <summary>
	/// Clicks an element by text with keyboard modifiers.
	/// </summary>
	public static ITestScenario ClickByTextWithModifiers(this ITestScenario scenario, string text, params string[] modifiers)
	{
		return AddClickWithModifiersAction(scenario, LocatorStrategy.ByText(text), modifiers);
	}

	/// <summary>
	/// Clicks an element by role with keyboard modifiers.
	/// </summary>
	public static ITestScenario ClickByRoleWithModifiers(this ITestScenario scenario, AriaRole role, string name, params string[] modifiers)
	{
		return AddClickWithModifiersAction(scenario, LocatorStrategy.ByRole(role, name), modifiers);
	}

	// ──────────────────────────────────────────────
	// Dispatch Event
	// ──────────────────────────────────────────────

	/// <summary>
	/// Dispatches a DOM event on an element using a CSS selector.
	/// Useful for programmatic interactions that bypass user-input simulation.
	/// Usage: .DispatchEvent("#btn", "click")
	/// </summary>
	public static ITestScenario DispatchEvent(this ITestScenario scenario, string cssSelector, string eventType)
	{
		return AddLocatorAction(scenario, PageActionType.DispatchEvent, LocatorStrategy.Css(cssSelector), eventType);
	}

	/// <summary>
	/// Dispatches a DOM event on an element found by its ARIA role.
	/// </summary>
	public static ITestScenario DispatchEventByRole(this ITestScenario scenario, AriaRole role, string eventType, string? name = null)
	{
		return AddLocatorAction(scenario, PageActionType.DispatchEvent, LocatorStrategy.ByRole(role, name), eventType);
	}

	/// <summary>
	/// Dispatches a DOM event on an element found by its test ID.
	/// </summary>
	public static ITestScenario DispatchEventByTestId(this ITestScenario scenario, string testId, string eventType)
	{
		return AddLocatorAction(scenario, PageActionType.DispatchEvent, LocatorStrategy.ByTestId(testId), eventType);
	}

	// ──────────────────────────────────────────────
	// Set Checked (combined check/uncheck)
	// ──────────────────────────────────────────────

	/// <summary>
	/// Sets the checked state of a checkbox or radio button using a CSS selector.
	/// Usage: .SetChecked("#terms", true) or .SetChecked("#terms", false)
	/// </summary>
	public static ITestScenario SetChecked(this ITestScenario scenario, string cssSelector, bool isChecked)
	{
		return AddLocatorAction(scenario, PageActionType.SetChecked, LocatorStrategy.Css(cssSelector), isChecked.ToString().ToLowerInvariant());
	}

	/// <summary>
	/// Sets the checked state of a checkbox found by its label.
	/// </summary>
	public static ITestScenario SetCheckedByLabel(this ITestScenario scenario, string label, bool isChecked, bool exact = false)
	{
		return AddLocatorAction(scenario, PageActionType.SetChecked, LocatorStrategy.ByLabel(label, exact), isChecked.ToString().ToLowerInvariant());
	}

	/// <summary>
	/// Sets the checked state of a checkbox found by its ARIA role.
	/// </summary>
	public static ITestScenario SetCheckedByRole(this ITestScenario scenario, AriaRole role, bool isChecked, string? name = null)
	{
		return AddLocatorAction(scenario, PageActionType.SetChecked, LocatorStrategy.ByRole(role, name), isChecked.ToString().ToLowerInvariant());
	}

	/// <summary>
	/// Sets the checked state of a checkbox found by its test ID.
	/// </summary>
	public static ITestScenario SetCheckedByTestId(this ITestScenario scenario, string testId, bool isChecked)
	{
		return AddLocatorAction(scenario, PageActionType.SetChecked, LocatorStrategy.ByTestId(testId), isChecked.ToString().ToLowerInvariant());
	}

	// ──────────────────────────────────────────────
	// Select Multiple Options
	// ──────────────────────────────────────────────

	/// <summary>
	/// Selects multiple options in a multi-select dropdown using a CSS selector.
	/// Usage: .SelectMultipleOptions("#colors", new[] { "red", "green", "blue" })
	/// </summary>
	public static ITestScenario SelectMultipleOptions(this ITestScenario scenario, string cssSelector, string[] values)
	{
		return AddMultiSelectAction(scenario, LocatorStrategy.Css(cssSelector), values);
	}

	/// <summary>
	/// Selects multiple options in a multi-select dropdown found by its label.
	/// </summary>
	public static ITestScenario SelectMultipleOptionsByLabel(this ITestScenario scenario, string label, string[] values, bool exact = false)
	{
		return AddMultiSelectAction(scenario, LocatorStrategy.ByLabel(label, exact), values);
	}

	/// <summary>
	/// Selects multiple options in a multi-select dropdown found by its test ID.
	/// </summary>
	public static ITestScenario SelectMultipleOptionsByTestId(this ITestScenario scenario, string testId, string[] values)
	{
		return AddMultiSelectAction(scenario, LocatorStrategy.ByTestId(testId), values);
	}

	/// <summary>
	/// Selects multiple options in a multi-select dropdown found by its ARIA role.
	/// </summary>
	public static ITestScenario SelectMultipleOptionsByRole(this ITestScenario scenario, AriaRole role, string[] values, string? name = null)
	{
		return AddMultiSelectAction(scenario, LocatorStrategy.ByRole(role, name), values);
	}

	// ──────────────────────────────────────────────
	// Codegen / Recording
	// ──────────────────────────────────────────────

	/// <summary>
	/// Pauses the page and opens the Playwright Inspector for interactive recording.
	/// Use this to record user actions via the built-in Codegen tool,
	/// then translate the generated code into the XUnitAssured fluent DSL.
	/// <para>
	/// <b>Requirements:</b> The browser must be running in headed mode (Headless = false).
	/// Set this in your fixture's <c>CreateSettings()</c> or <c>playwrightsettings.json</c>.
	/// </para>
	/// <para>
	/// <b>Usage:</b> Insert <c>.RecordAndPause()</c> at any point in the fluent chain.
	/// When executed, the Playwright Inspector opens and you can interact with the page.
	/// Copy the recorded actions, close the Inspector, and replace this call with
	/// the equivalent XUnitAssured DSL calls (NavigateTo, Fill, Click, etc.).
	/// </para>
	/// </summary>
	/// <example>
	/// <code>
	/// Given()
	///     .NavigateTo("/login")
	///     .RecordAndPause()   // Inspector opens — record your actions here
	/// .When()
	///     .Execute()
	/// .Then()
	///     .AssertSuccess();
	/// </code>
	/// </example>
	public static ITestScenario RecordAndPause(this ITestScenario scenario)
	{
		if (scenario == null) throw new ArgumentNullException(nameof(scenario));

		var step = GetOrCreateStep(scenario);
		step.AddAction(new PageAction
		{
			ActionType = PageActionType.Pause
		});

		return scenario;
	}

	// ──────────────────────────────────────────────
	// Internal helpers
	// ──────────────────────────────────────────────

	private static ITestScenario AddLocatorAction(
		ITestScenario scenario,
		PageActionType actionType,
		LocatorStrategy locator,
		string? value = null)
	{
		if (scenario == null) throw new ArgumentNullException(nameof(scenario));

		var step = GetOrCreateStep(scenario);
		step.AddAction(new PageAction
		{
			ActionType = actionType,
			Locator = locator,
			Value = value
		});

		return scenario;
	}

	private static ITestScenario AddUploadAction(
		ITestScenario scenario,
		LocatorStrategy locator,
		string[] filePaths)
	{
		if (scenario == null) throw new ArgumentNullException(nameof(scenario));
		if (filePaths == null || filePaths.Length == 0)
			throw new ArgumentException("At least one file path must be provided.", nameof(filePaths));

		var step = GetOrCreateStep(scenario);
		step.AddAction(new PageAction
		{
			ActionType = PageActionType.UploadFile,
			Locator = locator,
			FilePaths = filePaths
		});

		return scenario;
	}

	private static ITestScenario AddDragAction(
		ITestScenario scenario,
		LocatorStrategy source,
		LocatorStrategy target)
	{
		if (scenario == null) throw new ArgumentNullException(nameof(scenario));

		var step = GetOrCreateStep(scenario);
		step.AddAction(new PageAction
		{
			ActionType = PageActionType.DragTo,
			Locator = source,
			TargetLocator = target
		});

		return scenario;
	}

	private static ITestScenario AddForceClickAction(
		ITestScenario scenario,
		LocatorStrategy locator)
	{
		if (scenario == null) throw new ArgumentNullException(nameof(scenario));

		var step = GetOrCreateStep(scenario);
		step.AddAction(new PageAction
		{
			ActionType = PageActionType.Click,
			Locator = locator,
			Force = true
		});

		return scenario;
	}

	private static ITestScenario AddClickWithModifiersAction(
		ITestScenario scenario,
		LocatorStrategy locator,
		string[] modifiers)
	{
		if (scenario == null) throw new ArgumentNullException(nameof(scenario));
		if (modifiers == null || modifiers.Length == 0)
			throw new ArgumentException("At least one modifier must be provided.", nameof(modifiers));

		var step = GetOrCreateStep(scenario);
		step.AddAction(new PageAction
		{
			ActionType = PageActionType.Click,
			Locator = locator,
			Modifiers = modifiers
		});

		return scenario;
	}

	private static ITestScenario AddMultiSelectAction(
		ITestScenario scenario,
		LocatorStrategy locator,
		string[] values)
	{
		if (scenario == null) throw new ArgumentNullException(nameof(scenario));
		if (values == null || values.Length == 0)
			throw new ArgumentException("At least one value must be provided.", nameof(values));

		var step = GetOrCreateStep(scenario);
		step.AddAction(new PageAction
		{
			ActionType = PageActionType.SelectMultipleOptions,
			Locator = locator,
			Values = values
		});

		return scenario;
	}

	private static PlaywrightStep GetOrCreateStep(ITestScenario scenario)
	{
		if (scenario.CurrentStep is PlaywrightStep existingStep)
			return existingStep;

		// Retrieve page and settings from context (set by PlaywrightTestBase)
		var page = scenario.Context.GetProperty<IPage>("_PlaywrightPage");
		var settings = scenario.Context.GetProperty<Configuration.PlaywrightSettings>("_PlaywrightSettings")
			?? new Configuration.PlaywrightSettings();

		var step = new PlaywrightStep
		{
			Page = page,
			Settings = settings
		};

		scenario.SetCurrentStep(step);
		return step;
	}
}
