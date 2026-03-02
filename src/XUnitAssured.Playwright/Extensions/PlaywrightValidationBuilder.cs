using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Shouldly;
using XUnitAssured.Core.Abstractions;
using XUnitAssured.Core.Extensions;
using XUnitAssured.Playwright.Locators;
using XUnitAssured.Playwright.Results;

namespace XUnitAssured.Playwright.Extensions;

/// <summary>
/// Playwright-specific validation builder that extends the generic ValidationBuilder.
/// Provides convenient methods for UI testing with fluent assertions and multiple locator strategies.
/// </summary>
public class PlaywrightValidationBuilder : ValidationBuilder<PlaywrightStepResult>
{
	private readonly ITestScenario _scenario;

	/// <summary>
	/// Initializes a new instance of the PlaywrightValidationBuilder.
	/// </summary>
	/// <param name="scenario">The test scenario containing the executed Playwright step</param>
	public PlaywrightValidationBuilder(ITestScenario scenario) : base(scenario)
	{
		_scenario = scenario;
	}

	/// <summary>
	/// Marks the transition from "When" (action) to "Then" (assertions) in BDD-style tests.
	/// Returns PlaywrightValidationBuilder to maintain fluent chain type.
	/// </summary>
	public new PlaywrightValidationBuilder Then()
	{
		base.Then();
		return this;
	}

	/// <summary>
	/// Asserts that the step execution was successful.
	/// </summary>
	public new PlaywrightValidationBuilder AssertSuccess()
	{
		base.AssertSuccess();
		return this;
	}

	/// <summary>
	/// Asserts that the step execution failed.
	/// </summary>
	public new PlaywrightValidationBuilder AssertFailure()
	{
		base.AssertFailure();
		return this;
	}

	// ──────────────────────────────────────────────
	// URL Assertions
	// ──────────────────────────────────────────────

	/// <summary>
	/// Asserts that the current page URL matches the expected value exactly.
	/// </summary>
	public PlaywrightValidationBuilder AssertUrl(string expectedUrl)
	{
		Result.Url.ShouldBe(expectedUrl,
			$"Expected URL to be '{expectedUrl}' but was '{Result.Url}'");
		return this;
	}

	/// <summary>
	/// Asserts that the current page URL contains the expected substring.
	/// </summary>
	public PlaywrightValidationBuilder AssertUrlContains(string expectedSubstring)
	{
		Result.Url.ShouldNotBeNull("Page URL is null");
		Result.Url!.ShouldContain(expectedSubstring);
		return this;
	}

	/// <summary>
	/// Asserts that the current page URL ends with the expected path.
	/// Useful for asserting navigation without worrying about the base URL.
	/// </summary>
	public PlaywrightValidationBuilder AssertUrlEndsWith(string expectedSuffix)
	{
		Result.Url.ShouldNotBeNull("Page URL is null");
		Result.Url!.ShouldEndWith(expectedSuffix);
		return this;
	}

	// ──────────────────────────────────────────────
	// Title Assertions
	// ──────────────────────────────────────────────

	/// <summary>
	/// Asserts that the page title matches the expected value.
	/// </summary>
	public PlaywrightValidationBuilder AssertTitle(string expectedTitle)
	{
		Result.Title.ShouldBe(expectedTitle,
			$"Expected page title '{expectedTitle}' but got '{Result.Title}'");
		return this;
	}

	/// <summary>
	/// Asserts that the page title contains the expected substring.
	/// </summary>
	public PlaywrightValidationBuilder AssertTitleContains(string expectedSubstring)
	{
		Result.Title.ShouldNotBeNull("Page title is null");
		Result.Title!.ShouldContain(expectedSubstring);
		return this;
	}

	// ──────────────────────────────────────────────
	// Visibility Assertions (CSS + By* variants)
	// ──────────────────────────────────────────────

	/// <summary>
	/// Asserts that an element matching the CSS selector is visible on the page.
	/// </summary>
	public PlaywrightValidationBuilder AssertVisible(string cssSelector)
	{
		return AssertLocatorVisible(LocatorStrategy.Css(cssSelector));
	}

	/// <summary>
	/// Asserts that an element with the given ARIA role is visible.
	/// </summary>
	public PlaywrightValidationBuilder AssertVisibleByRole(AriaRole role, string? name = null)
	{
		return AssertLocatorVisible(LocatorStrategy.ByRole(role, name));
	}

	/// <summary>
	/// Asserts that an element with the given test ID is visible.
	/// </summary>
	public PlaywrightValidationBuilder AssertVisibleByTestId(string testId)
	{
		return AssertLocatorVisible(LocatorStrategy.ByTestId(testId));
	}

	/// <summary>
	/// Asserts that an element matching the given text is visible.
	/// </summary>
	public PlaywrightValidationBuilder AssertVisibleByText(string text, bool exact = false)
	{
		return AssertLocatorVisible(LocatorStrategy.ByText(text, exact));
	}

	/// <summary>
	/// Asserts that an element with the given alt text is visible.
	/// </summary>
	public PlaywrightValidationBuilder AssertVisibleByAltText(string altText, bool exact = false)
	{
		return AssertLocatorVisible(LocatorStrategy.ByAltText(altText, exact));
	}

	// ──────────────────────────────────────────────
	// Hidden Assertions (CSS + By* variants)
	// ──────────────────────────────────────────────

	/// <summary>
	/// Asserts that an element matching the CSS selector is hidden (not visible).
	/// </summary>
	public PlaywrightValidationBuilder AssertHidden(string cssSelector)
	{
		return AssertLocatorHidden(LocatorStrategy.Css(cssSelector));
	}

	/// <summary>
	/// Asserts that an element with the given test ID is hidden.
	/// </summary>
	public PlaywrightValidationBuilder AssertHiddenByTestId(string testId)
	{
		return AssertLocatorHidden(LocatorStrategy.ByTestId(testId));
	}

	/// <summary>
	/// Asserts that an element with the given ARIA role is hidden.
	/// </summary>
	public PlaywrightValidationBuilder AssertHiddenByRole(AriaRole role, string? name = null)
	{
		return AssertLocatorHidden(LocatorStrategy.ByRole(role, name));
	}

	// ──────────────────────────────────────────────
	// Text Content Assertions (CSS + By* variants)
	// ──────────────────────────────────────────────

	/// <summary>
	/// Asserts the text content of an element matching the CSS selector.
	/// </summary>
	public PlaywrightValidationBuilder AssertText(string cssSelector, string expectedText)
	{
		return AssertLocatorText(LocatorStrategy.Css(cssSelector), expectedText);
	}

	/// <summary>
	/// Asserts the text content of an element with the given ARIA role.
	/// </summary>
	public PlaywrightValidationBuilder AssertTextByRole(AriaRole role, string expectedText, string? name = null)
	{
		return AssertLocatorText(LocatorStrategy.ByRole(role, name), expectedText);
	}

	/// <summary>
	/// Asserts the text content of an element with the given test ID.
	/// </summary>
	public PlaywrightValidationBuilder AssertTextByTestId(string testId, string expectedText)
	{
		return AssertLocatorText(LocatorStrategy.ByTestId(testId), expectedText);
	}

	/// <summary>
	/// Asserts that an element's text contains the expected substring.
	/// </summary>
	public PlaywrightValidationBuilder AssertTextContains(string cssSelector, string expectedSubstring)
	{
		return AssertLocatorTextContains(LocatorStrategy.Css(cssSelector), expectedSubstring);
	}

	/// <summary>
	/// Asserts that an element with the given test ID contains the expected text substring.
	/// </summary>
	public PlaywrightValidationBuilder AssertTextContainsByTestId(string testId, string expectedSubstring)
	{
		return AssertLocatorTextContains(LocatorStrategy.ByTestId(testId), expectedSubstring);
	}

	// ──────────────────────────────────────────────
	// Element Count Assertions
	// ──────────────────────────────────────────────

	/// <summary>
	/// Asserts the number of elements matching the CSS selector.
	/// Usage: .AssertElementCount(".product-card", 12)
	/// </summary>
	public PlaywrightValidationBuilder AssertElementCount(string cssSelector, int expectedCount)
	{
		var page = GetPage();
		var count = page.Locator(cssSelector).CountAsync().GetAwaiter().GetResult();
		count.ShouldBe(expectedCount,
			$"Expected {expectedCount} elements matching '{cssSelector}' but found {count}");
		return this;
	}

	/// <summary>
	/// Asserts the number of elements with the given ARIA role.
	/// </summary>
	public PlaywrightValidationBuilder AssertElementCountByRole(AriaRole role, int expectedCount, string? name = null)
	{
		var page = GetPage();
		var locator = LocatorStrategy.ByRole(role, name).Resolve(page);
		var count = locator.CountAsync().GetAwaiter().GetResult();
		count.ShouldBe(expectedCount,
			$"Expected {expectedCount} elements with role '{role}' but found {count}");
		return this;
	}

	// ──────────────────────────────────────────────
	// Attribute Assertions
	// ──────────────────────────────────────────────

	/// <summary>
	/// Asserts an attribute value of an element matching the CSS selector.
	/// Usage: .AssertAttribute("#email", "placeholder", "Enter email")
	/// </summary>
	public PlaywrightValidationBuilder AssertAttribute(string cssSelector, string attributeName, string expectedValue)
	{
		return AssertLocatorAttribute(LocatorStrategy.Css(cssSelector), attributeName, expectedValue);
	}

	/// <summary>
	/// Asserts an attribute value of an element with the given test ID.
	/// </summary>
	public PlaywrightValidationBuilder AssertAttributeByTestId(string testId, string attributeName, string expectedValue)
	{
		return AssertLocatorAttribute(LocatorStrategy.ByTestId(testId), attributeName, expectedValue);
	}

	// ──────────────────────────────────────────────
	// Input Value Assertions
	// ──────────────────────────────────────────────

	/// <summary>
	/// Asserts the current value of an input element matching the CSS selector.
	/// </summary>
	public PlaywrightValidationBuilder AssertInputValue(string cssSelector, string expectedValue)
	{
		return AssertLocatorInputValue(LocatorStrategy.Css(cssSelector), expectedValue);
	}

	/// <summary>
	/// Asserts the current value of an input element found by its label.
	/// </summary>
	public PlaywrightValidationBuilder AssertInputValueByLabel(string label, string expectedValue, bool exact = false)
	{
		return AssertLocatorInputValue(LocatorStrategy.ByLabel(label, exact), expectedValue);
	}

	/// <summary>
	/// Asserts the current value of an input element found by its test ID.
	/// </summary>
	public PlaywrightValidationBuilder AssertInputValueByTestId(string testId, string expectedValue)
	{
		return AssertLocatorInputValue(LocatorStrategy.ByTestId(testId), expectedValue);
	}

	// ──────────────────────────────────────────────
	// Checked State Assertions
	// ──────────────────────────────────────────────

	/// <summary>
	/// Asserts that a checkbox matching the CSS selector is checked.
	/// </summary>
	public PlaywrightValidationBuilder AssertChecked(string cssSelector)
	{
		var page = GetPage();
		var isChecked = page.Locator(cssSelector).IsCheckedAsync().GetAwaiter().GetResult();
		isChecked.ShouldBeTrue($"Expected element '{cssSelector}' to be checked");
		return this;
	}

	/// <summary>
	/// Asserts that a checkbox found by label is checked.
	/// </summary>
	public PlaywrightValidationBuilder AssertCheckedByLabel(string label, bool exact = false)
	{
		var page = GetPage();
		var locator = LocatorStrategy.ByLabel(label, exact).Resolve(page);
		var isChecked = locator.IsCheckedAsync().GetAwaiter().GetResult();
		isChecked.ShouldBeTrue($"Expected element with label '{label}' to be checked");
		return this;
	}

	/// <summary>
	/// Asserts that a checkbox matching the CSS selector is not checked.
	/// </summary>
	public PlaywrightValidationBuilder AssertNotChecked(string cssSelector)
	{
		var page = GetPage();
		var isChecked = page.Locator(cssSelector).IsCheckedAsync().GetAwaiter().GetResult();
		isChecked.ShouldBeFalse($"Expected element '{cssSelector}' to not be checked");
		return this;
	}

	// ──────────────────────────────────────────────
	// Enabled/Disabled Assertions
	// ──────────────────────────────────────────────

	/// <summary>
	/// Asserts that an element matching the CSS selector is enabled.
	/// </summary>
	public PlaywrightValidationBuilder AssertEnabled(string cssSelector)
	{
		var page = GetPage();
		var isEnabled = page.Locator(cssSelector).IsEnabledAsync().GetAwaiter().GetResult();
		isEnabled.ShouldBeTrue($"Expected element '{cssSelector}' to be enabled");
		return this;
	}

	/// <summary>
	/// Asserts that an element matching the CSS selector is disabled.
	/// </summary>
	public PlaywrightValidationBuilder AssertDisabled(string cssSelector)
	{
		var page = GetPage();
		var isDisabled = page.Locator(cssSelector).IsDisabledAsync().GetAwaiter().GetResult();
		isDisabled.ShouldBeTrue($"Expected element '{cssSelector}' to be disabled");
		return this;
	}

	/// <summary>
	/// Asserts that an element with the given test ID is disabled.
	/// </summary>
	public PlaywrightValidationBuilder AssertDisabledByTestId(string testId)
	{
		var page = GetPage();
		var locator = LocatorStrategy.ByTestId(testId).Resolve(page);
		var isDisabled = locator.IsDisabledAsync().GetAwaiter().GetResult();
		isDisabled.ShouldBeTrue($"Expected element with test ID '{testId}' to be disabled");
		return this;
	}

	/// <summary>
	/// Asserts that an element with the given ARIA role is disabled.
	/// </summary>
	public PlaywrightValidationBuilder AssertDisabledByRole(AriaRole role, string? name = null)
	{
		var page = GetPage();
		var locator = LocatorStrategy.ByRole(role, name).Resolve(page);
		var isDisabled = locator.IsDisabledAsync().GetAwaiter().GetResult();
		isDisabled.ShouldBeTrue($"Expected element with role '{role}' to be disabled");
		return this;
	}

	/// <summary>
	/// Asserts that an element found by its label text is disabled.
	/// </summary>
	public PlaywrightValidationBuilder AssertDisabledByLabel(string label, bool exact = false)
	{
		var page = GetPage();
		var locator = LocatorStrategy.ByLabel(label, exact).Resolve(page);
		var isDisabled = locator.IsDisabledAsync().GetAwaiter().GetResult();
		isDisabled.ShouldBeTrue($"Expected element with label '{label}' to be disabled");
		return this;
	}

	/// <summary>
	/// Asserts that an element with the given ARIA role is enabled.
	/// </summary>
	public PlaywrightValidationBuilder AssertEnabledByRole(AriaRole role, string? name = null)
	{
		var page = GetPage();
		var locator = LocatorStrategy.ByRole(role, name).Resolve(page);
		var isEnabled = locator.IsEnabledAsync().GetAwaiter().GetResult();
		isEnabled.ShouldBeTrue($"Expected element with role '{role}' to be enabled");
		return this;
	}

	/// <summary>
	/// Asserts that an element with the given test ID is enabled.
	/// </summary>
	public PlaywrightValidationBuilder AssertEnabledByTestId(string testId)
	{
		var page = GetPage();
		var locator = LocatorStrategy.ByTestId(testId).Resolve(page);
		var isEnabled = locator.IsEnabledAsync().GetAwaiter().GetResult();
		isEnabled.ShouldBeTrue($"Expected element with test ID '{testId}' to be enabled");
		return this;
	}

	/// <summary>
	/// Asserts that an element found by its label text is enabled.
	/// </summary>
	public PlaywrightValidationBuilder AssertEnabledByLabel(string label, bool exact = false)
	{
		var page = GetPage();
		var locator = LocatorStrategy.ByLabel(label, exact).Resolve(page);
		var isEnabled = locator.IsEnabledAsync().GetAwaiter().GetResult();
		isEnabled.ShouldBeTrue($"Expected element with label '{label}' to be enabled");
		return this;
	}

	// ──────────────────────────────────────────────
	// Editable Assertions
	// ──────────────────────────────────────────────

	/// <summary>
	/// Asserts that an element matching the CSS selector is editable.
	/// </summary>
	public PlaywrightValidationBuilder AssertEditable(string cssSelector)
	{
		return AssertLocatorEditable(LocatorStrategy.Css(cssSelector));
	}

	/// <summary>
	/// Asserts that an element with the given test ID is editable.
	/// </summary>
	public PlaywrightValidationBuilder AssertEditableByTestId(string testId)
	{
		return AssertLocatorEditable(LocatorStrategy.ByTestId(testId));
	}

	/// <summary>
	/// Asserts that an element with the given ARIA role is editable.
	/// </summary>
	public PlaywrightValidationBuilder AssertEditableByRole(AriaRole role, string? name = null)
	{
		return AssertLocatorEditable(LocatorStrategy.ByRole(role, name));
	}

	/// <summary>
	/// Asserts that an element found by its label text is editable.
	/// </summary>
	public PlaywrightValidationBuilder AssertEditableByLabel(string label, bool exact = false)
	{
		return AssertLocatorEditable(LocatorStrategy.ByLabel(label, exact));
	}

	/// <summary>
	/// Asserts that an element matching the CSS selector is not editable (read-only).
	/// </summary>
	public PlaywrightValidationBuilder AssertNotEditable(string cssSelector)
	{
		return AssertLocatorNotEditable(LocatorStrategy.Css(cssSelector));
	}

	/// <summary>
	/// Asserts that an element with the given test ID is not editable.
	/// </summary>
	public PlaywrightValidationBuilder AssertNotEditableByTestId(string testId)
	{
		return AssertLocatorNotEditable(LocatorStrategy.ByTestId(testId));
	}

	// ──────────────────────────────────────────────
	// Empty Assertions
	// ──────────────────────────────────────────────

	/// <summary>
	/// Asserts that an element matching the CSS selector is empty (has no text content or child elements).
	/// </summary>
	public PlaywrightValidationBuilder AssertEmpty(string cssSelector)
	{
		return AssertLocatorEmpty(LocatorStrategy.Css(cssSelector));
	}

	/// <summary>
	/// Asserts that an element with the given test ID is empty.
	/// </summary>
	public PlaywrightValidationBuilder AssertEmptyByTestId(string testId)
	{
		return AssertLocatorEmpty(LocatorStrategy.ByTestId(testId));
	}

	/// <summary>
	/// Asserts that an element with the given ARIA role is empty.
	/// </summary>
	public PlaywrightValidationBuilder AssertEmptyByRole(AriaRole role, string? name = null)
	{
		return AssertLocatorEmpty(LocatorStrategy.ByRole(role, name));
	}

	/// <summary>
	/// Asserts that an element matching the CSS selector is not empty.
	/// </summary>
	public PlaywrightValidationBuilder AssertNotEmpty(string cssSelector)
	{
		return AssertLocatorNotEmpty(LocatorStrategy.Css(cssSelector));
	}

	/// <summary>
	/// Asserts that an element with the given test ID is not empty.
	/// </summary>
	public PlaywrightValidationBuilder AssertNotEmptyByTestId(string testId)
	{
		return AssertLocatorNotEmpty(LocatorStrategy.ByTestId(testId));
	}

	// ──────────────────────────────────────────────
	// Focused Assertions
	// ──────────────────────────────────────────────

	/// <summary>
	/// Asserts that an element matching the CSS selector is focused.
	/// </summary>
	public PlaywrightValidationBuilder AssertFocused(string cssSelector)
	{
		return AssertLocatorFocused(LocatorStrategy.Css(cssSelector));
	}

	/// <summary>
	/// Asserts that an element with the given test ID is focused.
	/// </summary>
	public PlaywrightValidationBuilder AssertFocusedByTestId(string testId)
	{
		return AssertLocatorFocused(LocatorStrategy.ByTestId(testId));
	}

	/// <summary>
	/// Asserts that an element with the given ARIA role is focused.
	/// </summary>
	public PlaywrightValidationBuilder AssertFocusedByRole(AriaRole role, string? name = null)
	{
		return AssertLocatorFocused(LocatorStrategy.ByRole(role, name));
	}

	/// <summary>
	/// Asserts that an element found by its label text is focused.
	/// </summary>
	public PlaywrightValidationBuilder AssertFocusedByLabel(string label, bool exact = false)
	{
		return AssertLocatorFocused(LocatorStrategy.ByLabel(label, exact));
	}

	/// <summary>
	/// Asserts that an element matching the CSS selector is not focused.
	/// </summary>
	public PlaywrightValidationBuilder AssertNotFocused(string cssSelector)
	{
		return AssertLocatorNotFocused(LocatorStrategy.Css(cssSelector));
	}

	/// <summary>
	/// Asserts that an element with the given test ID is not focused.
	/// </summary>
	public PlaywrightValidationBuilder AssertNotFocusedByTestId(string testId)
	{
		return AssertLocatorNotFocused(LocatorStrategy.ByTestId(testId));
	}

	// ──────────────────────────────────────────────
	// Attached Assertions
	// ──────────────────────────────────────────────

	/// <summary>
	/// Asserts that an element matching the CSS selector is attached to the DOM.
	/// </summary>
	public PlaywrightValidationBuilder AssertAttached(string cssSelector)
	{
		return AssertLocatorAttached(LocatorStrategy.Css(cssSelector));
	}

	/// <summary>
	/// Asserts that an element with the given test ID is attached to the DOM.
	/// </summary>
	public PlaywrightValidationBuilder AssertAttachedByTestId(string testId)
	{
		return AssertLocatorAttached(LocatorStrategy.ByTestId(testId));
	}

	/// <summary>
	/// Asserts that an element with the given ARIA role is attached to the DOM.
	/// </summary>
	public PlaywrightValidationBuilder AssertAttachedByRole(AriaRole role, string? name = null)
	{
		return AssertLocatorAttached(LocatorStrategy.ByRole(role, name));
	}

	/// <summary>
	/// Asserts that an element matching the CSS selector is not attached to the DOM.
	/// </summary>
	public PlaywrightValidationBuilder AssertNotAttached(string cssSelector)
	{
		return AssertLocatorNotAttached(LocatorStrategy.Css(cssSelector));
	}

	/// <summary>
	/// Asserts that an element with the given test ID is not attached to the DOM.
	/// </summary>
	public PlaywrightValidationBuilder AssertNotAttachedByTestId(string testId)
	{
		return AssertLocatorNotAttached(LocatorStrategy.ByTestId(testId));
	}

	// ──────────────────────────────────────────────
	// InViewport Assertions
	// ──────────────────────────────────────────────

	/// <summary>
	/// Asserts that an element matching the CSS selector intersects the viewport.
	/// </summary>
	public PlaywrightValidationBuilder AssertInViewport(string cssSelector)
	{
		return AssertLocatorInViewport(LocatorStrategy.Css(cssSelector));
	}

	/// <summary>
	/// Asserts that an element with the given test ID intersects the viewport.
	/// </summary>
	public PlaywrightValidationBuilder AssertInViewportByTestId(string testId)
	{
		return AssertLocatorInViewport(LocatorStrategy.ByTestId(testId));
	}

	/// <summary>
	/// Asserts that an element with the given ARIA role intersects the viewport.
	/// </summary>
	public PlaywrightValidationBuilder AssertInViewportByRole(AriaRole role, string? name = null)
	{
		return AssertLocatorInViewport(LocatorStrategy.ByRole(role, name));
	}

	// ──────────────────────────────────────────────
	// Additional Checked Variants
	// ──────────────────────────────────────────────

	/// <summary>
	/// Asserts that a checkbox found by its test ID is checked.
	/// </summary>
	public PlaywrightValidationBuilder AssertCheckedByTestId(string testId)
	{
		var page = GetPage();
		var locator = LocatorStrategy.ByTestId(testId).Resolve(page);
		var isChecked = locator.IsCheckedAsync().GetAwaiter().GetResult();
		isChecked.ShouldBeTrue($"Expected element with test ID '{testId}' to be checked");
		return this;
	}

	/// <summary>
	/// Asserts that a checkbox found by its ARIA role is checked.
	/// </summary>
	public PlaywrightValidationBuilder AssertCheckedByRole(AriaRole role, string? name = null)
	{
		var page = GetPage();
		var locator = LocatorStrategy.ByRole(role, name).Resolve(page);
		var isChecked = locator.IsCheckedAsync().GetAwaiter().GetResult();
		isChecked.ShouldBeTrue($"Expected element with role '{role}' to be checked");
		return this;
	}

	/// <summary>
	/// Asserts that a checkbox found by its label is not checked.
	/// </summary>
	public PlaywrightValidationBuilder AssertNotCheckedByLabel(string label, bool exact = false)
	{
		var page = GetPage();
		var locator = LocatorStrategy.ByLabel(label, exact).Resolve(page);
		var isChecked = locator.IsCheckedAsync().GetAwaiter().GetResult();
		isChecked.ShouldBeFalse($"Expected element with label '{label}' to not be checked");
		return this;
	}

	/// <summary>
	/// Asserts that a checkbox found by its test ID is not checked.
	/// </summary>
	public PlaywrightValidationBuilder AssertNotCheckedByTestId(string testId)
	{
		var page = GetPage();
		var locator = LocatorStrategy.ByTestId(testId).Resolve(page);
		var isChecked = locator.IsCheckedAsync().GetAwaiter().GetResult();
		isChecked.ShouldBeFalse($"Expected element with test ID '{testId}' to not be checked");
		return this;
	}

	/// <summary>
	/// Asserts that a checkbox found by its ARIA role is not checked.
	/// </summary>
	public PlaywrightValidationBuilder AssertNotCheckedByRole(AriaRole role, string? name = null)
	{
		var page = GetPage();
		var locator = LocatorStrategy.ByRole(role, name).Resolve(page);
		var isChecked = locator.IsCheckedAsync().GetAwaiter().GetResult();
		isChecked.ShouldBeFalse($"Expected element with role '{role}' to not be checked");
		return this;
	}

	// ──────────────────────────────────────────────
	// Console Log Assertions
	// ──────────────────────────────────────────────

	/// <summary>
	/// Asserts that the browser console has no error messages.
	/// </summary>
	public PlaywrightValidationBuilder AssertNoConsoleErrors()
	{
		var errors = Result.ConsoleLogs
			.Where(l => l.StartsWith("[error]", StringComparison.OrdinalIgnoreCase))
			.ToList();
		errors.Count.ShouldBe(0,
			$"Expected no console errors but found {errors.Count}: {string.Join("; ", errors)}");
		return this;
	}

	/// <summary>
	/// Asserts the number of browser console messages.
	/// </summary>
	public PlaywrightValidationBuilder AssertConsoleLogCount(int expectedCount)
	{
		Result.ConsoleLogs.Count.ShouldBe(expectedCount,
			$"Expected {expectedCount} console messages but got {Result.ConsoleLogs.Count}");
		return this;
	}

	// ──────────────────────────────────────────────
	// Screenshot Assertions
	// ──────────────────────────────────────────────

	/// <summary>
	/// Asserts that at least one screenshot was taken during the step.
	/// </summary>
	public PlaywrightValidationBuilder AssertScreenshotTaken()
	{
		Result.Screenshots.Count.ShouldBeGreaterThan(0, "Expected at least one screenshot to be taken");
		return this;
	}

	/// <summary>
	/// Asserts that a screenshot file exists at the specified path.
	/// </summary>
	public PlaywrightValidationBuilder AssertScreenshotExists(string fileName)
	{
		Result.Screenshots.ShouldContain(s => s.Contains(fileName),
			$"Expected screenshot '{fileName}' was not found. Screenshots taken: {string.Join(", ", Result.Screenshots)}");
		return this;
	}

	// ──────────────────────────────────────────────
	// Page Content Assertions
	// ──────────────────────────────────────────────

	/// <summary>
	/// Asserts that the page HTML content contains the expected substring.
	/// </summary>
	public PlaywrightValidationBuilder AssertPageContains(string expectedContent)
	{
		Result.PageContent.ShouldNotBeNull("Page content is null");
		Result.PageContent!.ShouldContain(expectedContent);
		return this;
	}

	/// <summary>
	/// Takes a screenshot at assertion time (after execution) for verification.
	/// The screenshot is saved using the current settings path.
	/// </summary>
	public PlaywrightValidationBuilder TakeScreenshot(string? fileName = null)
	{
		var page = GetPage();
		var settings = _scenario.Context.GetProperty<Configuration.PlaywrightSettings>("_PlaywrightSettings")
			?? new Configuration.PlaywrightSettings();

		var screenshotDir = settings.ScreenshotPath;
		if (!Directory.Exists(screenshotDir))
			Directory.CreateDirectory(screenshotDir);

		var name = fileName ?? $"assert_{DateTimeOffset.UtcNow:yyyyMMdd_HHmmss_fff}.png";
		var filePath = Path.Combine(screenshotDir, name);
		page.ScreenshotAsync(new PageScreenshotOptions { Path = filePath, FullPage = true }).GetAwaiter().GetResult();

		return this;
	}

	// ──────────────────────────────────────────────
	// CSS Class Assertions
	// ──────────────────────────────────────────────

	/// <summary>
	/// Asserts that an element matching the CSS selector has the exact CSS class string.
	/// Corresponds to Playwright's toHaveClass assertion.
	/// </summary>
	public PlaywrightValidationBuilder AssertClass(string cssSelector, string expectedClass)
	{
		return AssertLocatorClass(LocatorStrategy.Css(cssSelector), expectedClass);
	}

	/// <summary>
	/// Asserts that an element with the given test ID has the exact CSS class string.
	/// </summary>
	public PlaywrightValidationBuilder AssertClassByTestId(string testId, string expectedClass)
	{
		return AssertLocatorClass(LocatorStrategy.ByTestId(testId), expectedClass);
	}

	/// <summary>
	/// Asserts that an element with the given ARIA role has the exact CSS class string.
	/// </summary>
	public PlaywrightValidationBuilder AssertClassByRole(AriaRole role, string expectedClass, string? name = null)
	{
		return AssertLocatorClass(LocatorStrategy.ByRole(role, name), expectedClass);
	}

	/// <summary>
	/// Asserts that an element matching the CSS selector contains the specified CSS class(es).
	/// Corresponds to Playwright's toContainClass assertion.
	/// Usage: .AssertContainsClass("#btn", "active") or .AssertContainsClass("#btn", "btn primary")
	/// </summary>
	public PlaywrightValidationBuilder AssertContainsClass(string cssSelector, string expectedClass)
	{
		return AssertLocatorContainsClass(LocatorStrategy.Css(cssSelector), expectedClass);
	}

	/// <summary>
	/// Asserts that an element with the given test ID contains the specified CSS class(es).
	/// </summary>
	public PlaywrightValidationBuilder AssertContainsClassByTestId(string testId, string expectedClass)
	{
		return AssertLocatorContainsClass(LocatorStrategy.ByTestId(testId), expectedClass);
	}

	/// <summary>
	/// Asserts that an element with the given ARIA role contains the specified CSS class(es).
	/// </summary>
	public PlaywrightValidationBuilder AssertContainsClassByRole(AriaRole role, string expectedClass, string? name = null)
	{
		return AssertLocatorContainsClass(LocatorStrategy.ByRole(role, name), expectedClass);
	}

	// ──────────────────────────────────────────────
	// CSS Property Assertions
	// ──────────────────────────────────────────────

	/// <summary>
	/// Asserts the computed CSS property value of an element matching the CSS selector.
	/// Corresponds to Playwright's toHaveCSS assertion.
	/// Usage: .AssertCssProperty("#header", "color", "rgb(0, 0, 255)")
	/// </summary>
	public PlaywrightValidationBuilder AssertCssProperty(string cssSelector, string propertyName, string expectedValue)
	{
		return AssertLocatorCssProperty(LocatorStrategy.Css(cssSelector), propertyName, expectedValue);
	}

	/// <summary>
	/// Asserts the computed CSS property value of an element with the given test ID.
	/// </summary>
	public PlaywrightValidationBuilder AssertCssPropertyByTestId(string testId, string propertyName, string expectedValue)
	{
		return AssertLocatorCssProperty(LocatorStrategy.ByTestId(testId), propertyName, expectedValue);
	}

	/// <summary>
	/// Asserts the computed CSS property value of an element with the given ARIA role.
	/// </summary>
	public PlaywrightValidationBuilder AssertCssPropertyByRole(AriaRole role, string propertyName, string expectedValue, string? name = null)
	{
		return AssertLocatorCssProperty(LocatorStrategy.ByRole(role, name), propertyName, expectedValue);
	}

	// ──────────────────────────────────────────────
	// ID Assertions
	// ──────────────────────────────────────────────

	/// <summary>
	/// Asserts that an element matching the CSS selector has the expected id attribute.
	/// Corresponds to Playwright's toHaveId assertion.
	/// </summary>
	public PlaywrightValidationBuilder AssertId(string cssSelector, string expectedId)
	{
		return AssertLocatorId(LocatorStrategy.Css(cssSelector), expectedId);
	}

	/// <summary>
	/// Asserts that an element with the given ARIA role has the expected id attribute.
	/// </summary>
	public PlaywrightValidationBuilder AssertIdByRole(AriaRole role, string expectedId, string? name = null)
	{
		return AssertLocatorId(LocatorStrategy.ByRole(role, name), expectedId);
	}

	/// <summary>
	/// Asserts that an element with the given test ID has the expected id attribute.
	/// </summary>
	public PlaywrightValidationBuilder AssertIdByTestId(string testId, string expectedId)
	{
		return AssertLocatorId(LocatorStrategy.ByTestId(testId), expectedId);
	}

	// ──────────────────────────────────────────────
	// Accessible Name Assertions
	// ──────────────────────────────────────────────

	/// <summary>
	/// Asserts that an element matching the CSS selector has the expected accessible name.
	/// Corresponds to Playwright's toHaveAccessibleName assertion.
	/// </summary>
	public PlaywrightValidationBuilder AssertAccessibleName(string cssSelector, string expectedName)
	{
		return AssertLocatorAccessibleName(LocatorStrategy.Css(cssSelector), expectedName);
	}

	/// <summary>
	/// Asserts that an element with the given test ID has the expected accessible name.
	/// </summary>
	public PlaywrightValidationBuilder AssertAccessibleNameByTestId(string testId, string expectedName)
	{
		return AssertLocatorAccessibleName(LocatorStrategy.ByTestId(testId), expectedName);
	}

	/// <summary>
	/// Asserts that an element with the given ARIA role has the expected accessible name.
	/// </summary>
	public PlaywrightValidationBuilder AssertAccessibleNameByRole(AriaRole role, string expectedName, string? name = null)
	{
		return AssertLocatorAccessibleName(LocatorStrategy.ByRole(role, name), expectedName);
	}

	// ──────────────────────────────────────────────
	// Accessible Description Assertions
	// ──────────────────────────────────────────────

	/// <summary>
	/// Asserts that an element matching the CSS selector has the expected accessible description.
	/// Corresponds to Playwright's toHaveAccessibleDescription assertion.
	/// </summary>
	public PlaywrightValidationBuilder AssertAccessibleDescription(string cssSelector, string expectedDescription)
	{
		return AssertLocatorAccessibleDescription(LocatorStrategy.Css(cssSelector), expectedDescription);
	}

	/// <summary>
	/// Asserts that an element with the given test ID has the expected accessible description.
	/// </summary>
	public PlaywrightValidationBuilder AssertAccessibleDescriptionByTestId(string testId, string expectedDescription)
	{
		return AssertLocatorAccessibleDescription(LocatorStrategy.ByTestId(testId), expectedDescription);
	}

	/// <summary>
	/// Asserts that an element with the given ARIA role has the expected accessible description.
	/// </summary>
	public PlaywrightValidationBuilder AssertAccessibleDescriptionByRole(AriaRole role, string expectedDescription, string? name = null)
	{
		return AssertLocatorAccessibleDescription(LocatorStrategy.ByRole(role, name), expectedDescription);
	}

	// ──────────────────────────────────────────────
	// Role Assertions
	// ──────────────────────────────────────────────

	/// <summary>
	/// Asserts that an element matching the CSS selector has the expected ARIA role.
	/// Corresponds to Playwright's toHaveRole assertion.
	/// </summary>
	public PlaywrightValidationBuilder AssertRole(string cssSelector, AriaRole expectedRole)
	{
		return AssertLocatorRole(LocatorStrategy.Css(cssSelector), expectedRole);
	}

	/// <summary>
	/// Asserts that an element with the given test ID has the expected ARIA role.
	/// </summary>
	public PlaywrightValidationBuilder AssertRoleByTestId(string testId, AriaRole expectedRole)
	{
		return AssertLocatorRole(LocatorStrategy.ByTestId(testId), expectedRole);
	}

	// ──────────────────────────────────────────────
	// Select Values Assertions (multi-select)
	// ──────────────────────────────────────────────

	/// <summary>
	/// Asserts the selected option values of a multi-select element matching the CSS selector.
	/// Corresponds to Playwright's toHaveValues assertion.
	/// </summary>
	public PlaywrightValidationBuilder AssertValues(string cssSelector, params string[] expectedValues)
	{
		return AssertLocatorValues(LocatorStrategy.Css(cssSelector), expectedValues);
	}

	/// <summary>
	/// Asserts the selected option values of a multi-select element with the given test ID.
	/// </summary>
	public PlaywrightValidationBuilder AssertValuesByTestId(string testId, params string[] expectedValues)
	{
		return AssertLocatorValues(LocatorStrategy.ByTestId(testId), expectedValues);
	}

	/// <summary>
	/// Asserts the selected option values of a multi-select element found by its label.
	/// </summary>
	public PlaywrightValidationBuilder AssertValuesByLabel(string label, bool exact, params string[] expectedValues)
	{
		return AssertLocatorValues(LocatorStrategy.ByLabel(label, exact), expectedValues);
	}

	// ──────────────────────────────────────────────
	// JS Property Assertions
	// ──────────────────────────────────────────────

	/// <summary>
	/// Asserts a JavaScript property value of an element matching the CSS selector.
	/// Corresponds to Playwright's toHaveJSProperty assertion.
	/// Usage: .AssertJSProperty("#elem", "value", "hello")
	/// </summary>
	public PlaywrightValidationBuilder AssertJSProperty(string cssSelector, string propertyName, object expectedValue)
	{
		return AssertLocatorJSProperty(LocatorStrategy.Css(cssSelector), propertyName, expectedValue);
	}

	/// <summary>
	/// Asserts a JavaScript property value of an element with the given test ID.
	/// </summary>
	public PlaywrightValidationBuilder AssertJSPropertyByTestId(string testId, string propertyName, object expectedValue)
	{
		return AssertLocatorJSProperty(LocatorStrategy.ByTestId(testId), propertyName, expectedValue);
	}

	// ──────────────────────────────────────────────
	// URL Regex Assertions
	// ──────────────────────────────────────────────

	/// <summary>
	/// Asserts that the current page URL matches the expected regex pattern.
	/// </summary>
	public PlaywrightValidationBuilder AssertUrlMatches(string pattern)
	{
		Result.Url.ShouldNotBeNull("Page URL is null");
		Result.Url!.ShouldMatch(pattern);
		return this;
	}

	/// <summary>
	/// Asserts that the current page URL matches the expected regex.
	/// </summary>
	public PlaywrightValidationBuilder AssertUrlMatches(Regex pattern)
	{
		Result.Url.ShouldNotBeNull("Page URL is null");
		Regex.IsMatch(Result.Url!, pattern.ToString(), pattern.Options)
			.ShouldBeTrue($"Expected URL '{Result.Url}' to match pattern '{pattern}'");
		return this;
	}

	// ──────────────────────────────────────────────
	// Title Regex Assertions
	// ──────────────────────────────────────────────

	/// <summary>
	/// Asserts that the page title matches the expected regex pattern.
	/// </summary>
	public PlaywrightValidationBuilder AssertTitleMatches(string pattern)
	{
		Result.Title.ShouldNotBeNull("Page title is null");
		Result.Title!.ShouldMatch(pattern);
		return this;
	}

	/// <summary>
	/// Asserts that the page title matches the expected regex.
	/// </summary>
	public PlaywrightValidationBuilder AssertTitleMatches(Regex pattern)
	{
		Result.Title.ShouldNotBeNull("Page title is null");
		Regex.IsMatch(Result.Title!, pattern.ToString(), pattern.Options)
			.ShouldBeTrue($"Expected title '{Result.Title}' to match pattern '{pattern}'");
		return this;
	}

	// ──────────────────────────────────────────────
	// Text Regex Assertions
	// ──────────────────────────────────────────────

	/// <summary>
	/// Asserts that an element's text content matches the expected regex pattern.
	/// </summary>
	public PlaywrightValidationBuilder AssertTextMatches(string cssSelector, string pattern)
	{
		return AssertLocatorTextMatches(LocatorStrategy.Css(cssSelector), pattern);
	}

	/// <summary>
	/// Asserts that an element's text content (by test ID) matches the expected regex pattern.
	/// </summary>
	public PlaywrightValidationBuilder AssertTextMatchesByTestId(string testId, string pattern)
	{
		return AssertLocatorTextMatches(LocatorStrategy.ByTestId(testId), pattern);
	}

	/// <summary>
	/// Asserts that an element's text content (by ARIA role) matches the expected regex pattern.
	/// </summary>
	public PlaywrightValidationBuilder AssertTextMatchesByRole(AriaRole role, string pattern, string? name = null)
	{
		return AssertLocatorTextMatches(LocatorStrategy.ByRole(role, name), pattern);
	}

	// ══════════════════════════════════════════════
	// Playwright Expect Assertions (Auto-Retrying)
	// ══════════════════════════════════════════════
	//
	// These methods use Playwright's built-in Assertions.Expect() API
	// which automatically retries until the condition is met or timeout.
	// This is the recommended approach for dynamic web pages.
	// ══════════════════════════════════════════════

	/// <summary>
	/// Returns Playwright's auto-retrying locator assertions for an element matching the CSS selector.
	/// Allows chaining any Playwright assertion directly.
	/// Usage: .ExpectLocator("#btn").ToBeVisibleAsync().GetAwaiter().GetResult()
	/// </summary>
	public ILocatorAssertions ExpectLocator(string cssSelector)
	{
		var page = GetPage();
		return Assertions.Expect(page.Locator(cssSelector));
	}

	/// <summary>
	/// Returns Playwright's auto-retrying locator assertions for an element resolved by the given locator strategy.
	/// </summary>
	public ILocatorAssertions ExpectLocator(LocatorStrategy locator)
	{
		var page = GetPage();
		return Assertions.Expect(locator.Resolve(page));
	}

	/// <summary>
	/// Returns Playwright's auto-retrying page assertions for the current page.
	/// Usage: .ExpectPage().ToHaveTitleAsync("Home").GetAwaiter().GetResult()
	/// </summary>
	public IPageAssertions ExpectPage()
	{
		var page = GetPage();
		return Assertions.Expect(page);
	}

	/// <summary>
	/// Asserts (with auto-retry) that an element matching the CSS selector is visible.
	/// Uses Playwright's Expect API for automatic retrying until timeout.
	/// </summary>
	public PlaywrightValidationBuilder ExpectVisible(string cssSelector)
	{
		var page = GetPage();
		Assertions.Expect(page.Locator(cssSelector)).ToBeVisibleAsync().GetAwaiter().GetResult();
		return this;
	}

	/// <summary>
	/// Asserts (with auto-retry) that an element resolved by the locator strategy is visible.
	/// </summary>
	public PlaywrightValidationBuilder ExpectVisible(LocatorStrategy locator)
	{
		var page = GetPage();
		Assertions.Expect(locator.Resolve(page)).ToBeVisibleAsync().GetAwaiter().GetResult();
		return this;
	}

	/// <summary>
	/// Asserts (with auto-retry) that an element matching the CSS selector is hidden.
	/// </summary>
	public PlaywrightValidationBuilder ExpectHidden(string cssSelector)
	{
		var page = GetPage();
		Assertions.Expect(page.Locator(cssSelector)).ToBeHiddenAsync().GetAwaiter().GetResult();
		return this;
	}

	/// <summary>
	/// Asserts (with auto-retry) that an element resolved by the locator strategy is hidden.
	/// </summary>
	public PlaywrightValidationBuilder ExpectHidden(LocatorStrategy locator)
	{
		var page = GetPage();
		Assertions.Expect(locator.Resolve(page)).ToBeHiddenAsync().GetAwaiter().GetResult();
		return this;
	}

	/// <summary>
	/// Asserts (with auto-retry) that an element matching the CSS selector is enabled.
	/// </summary>
	public PlaywrightValidationBuilder ExpectEnabled(string cssSelector)
	{
		var page = GetPage();
		Assertions.Expect(page.Locator(cssSelector)).ToBeEnabledAsync().GetAwaiter().GetResult();
		return this;
	}

	/// <summary>
	/// Asserts (with auto-retry) that an element resolved by the locator strategy is enabled.
	/// </summary>
	public PlaywrightValidationBuilder ExpectEnabled(LocatorStrategy locator)
	{
		var page = GetPage();
		Assertions.Expect(locator.Resolve(page)).ToBeEnabledAsync().GetAwaiter().GetResult();
		return this;
	}

	/// <summary>
	/// Asserts (with auto-retry) that an element matching the CSS selector is disabled.
	/// </summary>
	public PlaywrightValidationBuilder ExpectDisabled(string cssSelector)
	{
		var page = GetPage();
		Assertions.Expect(page.Locator(cssSelector)).ToBeDisabledAsync().GetAwaiter().GetResult();
		return this;
	}

	/// <summary>
	/// Asserts (with auto-retry) that an element resolved by the locator strategy is disabled.
	/// </summary>
	public PlaywrightValidationBuilder ExpectDisabled(LocatorStrategy locator)
	{
		var page = GetPage();
		Assertions.Expect(locator.Resolve(page)).ToBeDisabledAsync().GetAwaiter().GetResult();
		return this;
	}

	/// <summary>
	/// Asserts (with auto-retry) that a checkbox matching the CSS selector is checked.
	/// </summary>
	public PlaywrightValidationBuilder ExpectChecked(string cssSelector)
	{
		var page = GetPage();
		Assertions.Expect(page.Locator(cssSelector)).ToBeCheckedAsync().GetAwaiter().GetResult();
		return this;
	}

	/// <summary>
	/// Asserts (with auto-retry) that a checkbox resolved by the locator strategy is checked.
	/// </summary>
	public PlaywrightValidationBuilder ExpectChecked(LocatorStrategy locator)
	{
		var page = GetPage();
		Assertions.Expect(locator.Resolve(page)).ToBeCheckedAsync().GetAwaiter().GetResult();
		return this;
	}

	/// <summary>
	/// Asserts (with auto-retry) that an element matching the CSS selector is editable.
	/// </summary>
	public PlaywrightValidationBuilder ExpectEditable(string cssSelector)
	{
		var page = GetPage();
		Assertions.Expect(page.Locator(cssSelector)).ToBeEditableAsync().GetAwaiter().GetResult();
		return this;
	}

	/// <summary>
	/// Asserts (with auto-retry) that an element resolved by the locator strategy is editable.
	/// </summary>
	public PlaywrightValidationBuilder ExpectEditable(LocatorStrategy locator)
	{
		var page = GetPage();
		Assertions.Expect(locator.Resolve(page)).ToBeEditableAsync().GetAwaiter().GetResult();
		return this;
	}

	/// <summary>
	/// Asserts (with auto-retry) that an element matching the CSS selector is empty.
	/// </summary>
	public PlaywrightValidationBuilder ExpectEmpty(string cssSelector)
	{
		var page = GetPage();
		Assertions.Expect(page.Locator(cssSelector)).ToBeEmptyAsync().GetAwaiter().GetResult();
		return this;
	}

	/// <summary>
	/// Asserts (with auto-retry) that an element resolved by the locator strategy is empty.
	/// </summary>
	public PlaywrightValidationBuilder ExpectEmpty(LocatorStrategy locator)
	{
		var page = GetPage();
		Assertions.Expect(locator.Resolve(page)).ToBeEmptyAsync().GetAwaiter().GetResult();
		return this;
	}

	/// <summary>
	/// Asserts (with auto-retry) that an element matching the CSS selector is focused.
	/// </summary>
	public PlaywrightValidationBuilder ExpectFocused(string cssSelector)
	{
		var page = GetPage();
		Assertions.Expect(page.Locator(cssSelector)).ToBeFocusedAsync().GetAwaiter().GetResult();
		return this;
	}

	/// <summary>
	/// Asserts (with auto-retry) that an element resolved by the locator strategy is focused.
	/// </summary>
	public PlaywrightValidationBuilder ExpectFocused(LocatorStrategy locator)
	{
		var page = GetPage();
		Assertions.Expect(locator.Resolve(page)).ToBeFocusedAsync().GetAwaiter().GetResult();
		return this;
	}

	/// <summary>
	/// Asserts (with auto-retry) that an element matching the CSS selector is attached to the DOM.
	/// </summary>
	public PlaywrightValidationBuilder ExpectAttached(string cssSelector)
	{
		var page = GetPage();
		Assertions.Expect(page.Locator(cssSelector)).ToBeAttachedAsync().GetAwaiter().GetResult();
		return this;
	}

	/// <summary>
	/// Asserts (with auto-retry) that an element resolved by the locator strategy is attached to the DOM.
	/// </summary>
	public PlaywrightValidationBuilder ExpectAttached(LocatorStrategy locator)
	{
		var page = GetPage();
		Assertions.Expect(locator.Resolve(page)).ToBeAttachedAsync().GetAwaiter().GetResult();
		return this;
	}

	/// <summary>
	/// Asserts (with auto-retry) that an element matching the CSS selector is in the viewport.
	/// </summary>
	public PlaywrightValidationBuilder ExpectInViewport(string cssSelector)
	{
		var page = GetPage();
		Assertions.Expect(page.Locator(cssSelector)).ToBeInViewportAsync().GetAwaiter().GetResult();
		return this;
	}

	/// <summary>
	/// Asserts (with auto-retry) that an element resolved by the locator strategy is in the viewport.
	/// </summary>
	public PlaywrightValidationBuilder ExpectInViewport(LocatorStrategy locator)
	{
		var page = GetPage();
		Assertions.Expect(locator.Resolve(page)).ToBeInViewportAsync().GetAwaiter().GetResult();
		return this;
	}

	/// <summary>
	/// Asserts (with auto-retry) that an element matching the CSS selector has the expected text.
	/// </summary>
	public PlaywrightValidationBuilder ExpectText(string cssSelector, string expectedText)
	{
		var page = GetPage();
		Assertions.Expect(page.Locator(cssSelector)).ToHaveTextAsync(expectedText).GetAwaiter().GetResult();
		return this;
	}

	/// <summary>
	/// Asserts (with auto-retry) that an element resolved by the locator strategy has the expected text.
	/// </summary>
	public PlaywrightValidationBuilder ExpectText(LocatorStrategy locator, string expectedText)
	{
		var page = GetPage();
		Assertions.Expect(locator.Resolve(page)).ToHaveTextAsync(expectedText).GetAwaiter().GetResult();
		return this;
	}

	/// <summary>
	/// Asserts (with auto-retry) that an element matching the CSS selector contains the expected text.
	/// </summary>
	public PlaywrightValidationBuilder ExpectContainsText(string cssSelector, string expectedText)
	{
		var page = GetPage();
		Assertions.Expect(page.Locator(cssSelector)).ToContainTextAsync(expectedText).GetAwaiter().GetResult();
		return this;
	}

	/// <summary>
	/// Asserts (with auto-retry) that an element resolved by the locator strategy contains the expected text.
	/// </summary>
	public PlaywrightValidationBuilder ExpectContainsText(LocatorStrategy locator, string expectedText)
	{
		var page = GetPage();
		Assertions.Expect(locator.Resolve(page)).ToContainTextAsync(expectedText).GetAwaiter().GetResult();
		return this;
	}

	/// <summary>
	/// Asserts (with auto-retry) that an element matching the CSS selector has the expected attribute value.
	/// </summary>
	public PlaywrightValidationBuilder ExpectAttribute(string cssSelector, string attributeName, string expectedValue)
	{
		var page = GetPage();
		Assertions.Expect(page.Locator(cssSelector)).ToHaveAttributeAsync(attributeName, expectedValue).GetAwaiter().GetResult();
		return this;
	}

	/// <summary>
	/// Asserts (with auto-retry) that an element resolved by the locator strategy has the expected attribute value.
	/// </summary>
	public PlaywrightValidationBuilder ExpectAttribute(LocatorStrategy locator, string attributeName, string expectedValue)
	{
		var page = GetPage();
		Assertions.Expect(locator.Resolve(page)).ToHaveAttributeAsync(attributeName, expectedValue).GetAwaiter().GetResult();
		return this;
	}

	/// <summary>
	/// Asserts (with auto-retry) that an element matching the CSS selector has the expected input value.
	/// </summary>
	public PlaywrightValidationBuilder ExpectValue(string cssSelector, string expectedValue)
	{
		var page = GetPage();
		Assertions.Expect(page.Locator(cssSelector)).ToHaveValueAsync(expectedValue).GetAwaiter().GetResult();
		return this;
	}

	/// <summary>
	/// Asserts (with auto-retry) that an element resolved by the locator strategy has the expected input value.
	/// </summary>
	public PlaywrightValidationBuilder ExpectValue(LocatorStrategy locator, string expectedValue)
	{
		var page = GetPage();
		Assertions.Expect(locator.Resolve(page)).ToHaveValueAsync(expectedValue).GetAwaiter().GetResult();
		return this;
	}

	/// <summary>
	/// Asserts (with auto-retry) that a multi-select element matching the CSS selector has the expected selected values.
	/// </summary>
	public PlaywrightValidationBuilder ExpectValues(string cssSelector, IEnumerable<string> expectedValues)
	{
		var page = GetPage();
		Assertions.Expect(page.Locator(cssSelector)).ToHaveValuesAsync(expectedValues).GetAwaiter().GetResult();
		return this;
	}

	/// <summary>
	/// Asserts (with auto-retry) that a multi-select element resolved by the locator strategy has the expected selected values.
	/// </summary>
	public PlaywrightValidationBuilder ExpectValues(LocatorStrategy locator, IEnumerable<string> expectedValues)
	{
		var page = GetPage();
		Assertions.Expect(locator.Resolve(page)).ToHaveValuesAsync(expectedValues).GetAwaiter().GetResult();
		return this;
	}

	/// <summary>
	/// Asserts (with auto-retry) that an element matching the CSS selector has the expected CSS class.
	/// </summary>
	public PlaywrightValidationBuilder ExpectClass(string cssSelector, string expectedClass)
	{
		var page = GetPage();
		Assertions.Expect(page.Locator(cssSelector)).ToHaveClassAsync(expectedClass).GetAwaiter().GetResult();
		return this;
	}

	/// <summary>
	/// Asserts (with auto-retry) that an element resolved by the locator strategy has the expected CSS class.
	/// </summary>
	public PlaywrightValidationBuilder ExpectClass(LocatorStrategy locator, string expectedClass)
	{
		var page = GetPage();
		Assertions.Expect(locator.Resolve(page)).ToHaveClassAsync(expectedClass).GetAwaiter().GetResult();
		return this;
	}

	/// <summary>
	/// Asserts (with auto-retry) that an element matching the CSS selector has the expected count.
	/// </summary>
	public PlaywrightValidationBuilder ExpectCount(string cssSelector, int expectedCount)
	{
		var page = GetPage();
		Assertions.Expect(page.Locator(cssSelector)).ToHaveCountAsync(expectedCount).GetAwaiter().GetResult();
		return this;
	}

	/// <summary>
	/// Asserts (with auto-retry) that an element resolved by the locator strategy has the expected count.
	/// </summary>
	public PlaywrightValidationBuilder ExpectCount(LocatorStrategy locator, int expectedCount)
	{
		var page = GetPage();
		Assertions.Expect(locator.Resolve(page)).ToHaveCountAsync(expectedCount).GetAwaiter().GetResult();
		return this;
	}

	/// <summary>
	/// Asserts (with auto-retry) that an element matching the CSS selector has the expected CSS property value.
	/// </summary>
	public PlaywrightValidationBuilder ExpectCssProperty(string cssSelector, string propertyName, string expectedValue)
	{
		var page = GetPage();
		Assertions.Expect(page.Locator(cssSelector)).ToHaveCSSAsync(propertyName, expectedValue).GetAwaiter().GetResult();
		return this;
	}

	/// <summary>
	/// Asserts (with auto-retry) that an element resolved by the locator strategy has the expected CSS property value.
	/// </summary>
	public PlaywrightValidationBuilder ExpectCssProperty(LocatorStrategy locator, string propertyName, string expectedValue)
	{
		var page = GetPage();
		Assertions.Expect(locator.Resolve(page)).ToHaveCSSAsync(propertyName, expectedValue).GetAwaiter().GetResult();
		return this;
	}

	/// <summary>
	/// Asserts (with auto-retry) that an element matching the CSS selector has the expected ID.
	/// </summary>
	public PlaywrightValidationBuilder ExpectId(string cssSelector, string expectedId)
	{
		var page = GetPage();
		Assertions.Expect(page.Locator(cssSelector)).ToHaveIdAsync(expectedId).GetAwaiter().GetResult();
		return this;
	}

	/// <summary>
	/// Asserts (with auto-retry) that an element resolved by the locator strategy has the expected ID.
	/// </summary>
	public PlaywrightValidationBuilder ExpectId(LocatorStrategy locator, string expectedId)
	{
		var page = GetPage();
		Assertions.Expect(locator.Resolve(page)).ToHaveIdAsync(expectedId).GetAwaiter().GetResult();
		return this;
	}

	/// <summary>
	/// Asserts (with auto-retry) that an element matching the CSS selector has the expected accessible name.
	/// </summary>
	public PlaywrightValidationBuilder ExpectAccessibleName(string cssSelector, string expectedName)
	{
		var page = GetPage();
		Assertions.Expect(page.Locator(cssSelector)).ToHaveAccessibleNameAsync(expectedName).GetAwaiter().GetResult();
		return this;
	}

	/// <summary>
	/// Asserts (with auto-retry) that an element resolved by the locator strategy has the expected accessible name.
	/// </summary>
	public PlaywrightValidationBuilder ExpectAccessibleName(LocatorStrategy locator, string expectedName)
	{
		var page = GetPage();
		Assertions.Expect(locator.Resolve(page)).ToHaveAccessibleNameAsync(expectedName).GetAwaiter().GetResult();
		return this;
	}

	/// <summary>
	/// Asserts (with auto-retry) that an element matching the CSS selector has the expected accessible description.
	/// </summary>
	public PlaywrightValidationBuilder ExpectAccessibleDescription(string cssSelector, string expectedDescription)
	{
		var page = GetPage();
		Assertions.Expect(page.Locator(cssSelector)).ToHaveAccessibleDescriptionAsync(expectedDescription).GetAwaiter().GetResult();
		return this;
	}

	/// <summary>
	/// Asserts (with auto-retry) that an element resolved by the locator strategy has the expected accessible description.
	/// </summary>
	public PlaywrightValidationBuilder ExpectAccessibleDescription(LocatorStrategy locator, string expectedDescription)
	{
		var page = GetPage();
		Assertions.Expect(locator.Resolve(page)).ToHaveAccessibleDescriptionAsync(expectedDescription).GetAwaiter().GetResult();
		return this;
	}

	/// <summary>
	/// Asserts (with auto-retry) that an element matching the CSS selector has the expected ARIA role.
	/// </summary>
	public PlaywrightValidationBuilder ExpectRole(string cssSelector, AriaRole expectedRole)
	{
		var page = GetPage();
		Assertions.Expect(page.Locator(cssSelector)).ToHaveRoleAsync(expectedRole).GetAwaiter().GetResult();
		return this;
	}

	/// <summary>
	/// Asserts (with auto-retry) that an element resolved by the locator strategy has the expected ARIA role.
	/// </summary>
	public PlaywrightValidationBuilder ExpectRole(LocatorStrategy locator, AriaRole expectedRole)
	{
		var page = GetPage();
		Assertions.Expect(locator.Resolve(page)).ToHaveRoleAsync(expectedRole).GetAwaiter().GetResult();
		return this;
	}

	/// <summary>
	/// Asserts (with auto-retry) that an element matching the CSS selector has the expected JS property.
	/// </summary>
	public PlaywrightValidationBuilder ExpectJSProperty(string cssSelector, string propertyName, object expectedValue)
	{
		var page = GetPage();
		Assertions.Expect(page.Locator(cssSelector)).ToHaveJSPropertyAsync(propertyName, expectedValue).GetAwaiter().GetResult();
		return this;
	}

	/// <summary>
	/// Asserts (with auto-retry) that an element resolved by the locator strategy has the expected JS property.
	/// </summary>
	public PlaywrightValidationBuilder ExpectJSProperty(LocatorStrategy locator, string propertyName, object expectedValue)
	{
		var page = GetPage();
		Assertions.Expect(locator.Resolve(page)).ToHaveJSPropertyAsync(propertyName, expectedValue).GetAwaiter().GetResult();
		return this;
	}

	/// <summary>
	/// Asserts (with auto-retry) that the page has the expected title.
	/// </summary>
	public PlaywrightValidationBuilder ExpectTitle(string expectedTitle)
	{
		var page = GetPage();
		Assertions.Expect(page).ToHaveTitleAsync(expectedTitle).GetAwaiter().GetResult();
		return this;
	}

	/// <summary>
	/// Asserts (with auto-retry) that the page title matches the expected regex.
	/// </summary>
	public PlaywrightValidationBuilder ExpectTitle(Regex pattern)
	{
		var page = GetPage();
		Assertions.Expect(page).ToHaveTitleAsync(pattern).GetAwaiter().GetResult();
		return this;
	}

	/// <summary>
	/// Asserts (with auto-retry) that the page has the expected URL.
	/// </summary>
	public PlaywrightValidationBuilder ExpectUrl(string expectedUrl)
	{
		var page = GetPage();
		Assertions.Expect(page).ToHaveURLAsync(expectedUrl).GetAwaiter().GetResult();
		return this;
	}

	/// <summary>
	/// Asserts (with auto-retry) that the page URL matches the expected regex.
	/// </summary>
	public PlaywrightValidationBuilder ExpectUrl(Regex pattern)
	{
		var page = GetPage();
		Assertions.Expect(page).ToHaveURLAsync(pattern).GetAwaiter().GetResult();
		return this;
	}

	// ══════════════════════════════════════════════
	// Negated Expect Assertions (Auto-Retrying)
	// ══════════════════════════════════════════════

	/// <summary>
	/// Asserts (with auto-retry) that an element matching the CSS selector is NOT visible.
	/// Uses Playwright's Expect().Not pattern for automatic retrying.
	/// </summary>
	public PlaywrightValidationBuilder ExpectNotVisible(string cssSelector)
	{
		var page = GetPage();
		Assertions.Expect(page.Locator(cssSelector)).Not.ToBeVisibleAsync().GetAwaiter().GetResult();
		return this;
	}

	/// <summary>
	/// Asserts (with auto-retry) that an element resolved by the locator strategy is NOT visible.
	/// </summary>
	public PlaywrightValidationBuilder ExpectNotVisible(LocatorStrategy locator)
	{
		var page = GetPage();
		Assertions.Expect(locator.Resolve(page)).Not.ToBeVisibleAsync().GetAwaiter().GetResult();
		return this;
	}

	/// <summary>
	/// Asserts (with auto-retry) that an element matching the CSS selector is NOT attached to the DOM.
	/// </summary>
	public PlaywrightValidationBuilder ExpectNotAttached(string cssSelector)
	{
		var page = GetPage();
		Assertions.Expect(page.Locator(cssSelector)).Not.ToBeAttachedAsync().GetAwaiter().GetResult();
		return this;
	}

	/// <summary>
	/// Asserts (with auto-retry) that an element resolved by the locator strategy is NOT attached to the DOM.
	/// </summary>
	public PlaywrightValidationBuilder ExpectNotAttached(LocatorStrategy locator)
	{
		var page = GetPage();
		Assertions.Expect(locator.Resolve(page)).Not.ToBeAttachedAsync().GetAwaiter().GetResult();
		return this;
	}

	/// <summary>
	/// Asserts (with auto-retry) that an element matching the CSS selector is NOT checked.
	/// </summary>
	public PlaywrightValidationBuilder ExpectNotChecked(string cssSelector)
	{
		var page = GetPage();
		Assertions.Expect(page.Locator(cssSelector)).Not.ToBeCheckedAsync().GetAwaiter().GetResult();
		return this;
	}

	/// <summary>
	/// Asserts (with auto-retry) that an element resolved by the locator strategy is NOT checked.
	/// </summary>
	public PlaywrightValidationBuilder ExpectNotChecked(LocatorStrategy locator)
	{
		var page = GetPage();
		Assertions.Expect(locator.Resolve(page)).Not.ToBeCheckedAsync().GetAwaiter().GetResult();
		return this;
	}

	/// <summary>
	/// Asserts (with auto-retry) that an element matching the CSS selector is NOT editable.
	/// </summary>
	public PlaywrightValidationBuilder ExpectNotEditable(string cssSelector)
	{
		var page = GetPage();
		Assertions.Expect(page.Locator(cssSelector)).Not.ToBeEditableAsync().GetAwaiter().GetResult();
		return this;
	}

	/// <summary>
	/// Asserts (with auto-retry) that an element resolved by the locator strategy is NOT editable.
	/// </summary>
	public PlaywrightValidationBuilder ExpectNotEditable(LocatorStrategy locator)
	{
		var page = GetPage();
		Assertions.Expect(locator.Resolve(page)).Not.ToBeEditableAsync().GetAwaiter().GetResult();
		return this;
	}

	/// <summary>
	/// Asserts (with auto-retry) that an element matching the CSS selector is NOT empty.
	/// </summary>
	public PlaywrightValidationBuilder ExpectNotEmpty(string cssSelector)
	{
		var page = GetPage();
		Assertions.Expect(page.Locator(cssSelector)).Not.ToBeEmptyAsync().GetAwaiter().GetResult();
		return this;
	}

	/// <summary>
	/// Asserts (with auto-retry) that an element resolved by the locator strategy is NOT empty.
	/// </summary>
	public PlaywrightValidationBuilder ExpectNotEmpty(LocatorStrategy locator)
	{
		var page = GetPage();
		Assertions.Expect(locator.Resolve(page)).Not.ToBeEmptyAsync().GetAwaiter().GetResult();
		return this;
	}

	/// <summary>
	/// Asserts (with auto-retry) that an element matching the CSS selector is NOT focused.
	/// </summary>
	public PlaywrightValidationBuilder ExpectNotFocused(string cssSelector)
	{
		var page = GetPage();
		Assertions.Expect(page.Locator(cssSelector)).Not.ToBeFocusedAsync().GetAwaiter().GetResult();
		return this;
	}

	/// <summary>
	/// Asserts (with auto-retry) that an element resolved by the locator strategy is NOT focused.
	/// </summary>
	public PlaywrightValidationBuilder ExpectNotFocused(LocatorStrategy locator)
	{
		var page = GetPage();
		Assertions.Expect(locator.Resolve(page)).Not.ToBeFocusedAsync().GetAwaiter().GetResult();
		return this;
	}

	/// <summary>
	/// Asserts (with auto-retry) that an element does NOT have the expected text.
	/// </summary>
	public PlaywrightValidationBuilder ExpectNotText(string cssSelector, string text)
	{
		var page = GetPage();
		Assertions.Expect(page.Locator(cssSelector)).Not.ToHaveTextAsync(text).GetAwaiter().GetResult();
		return this;
	}

	/// <summary>
	/// Asserts (with auto-retry) that an element does NOT contain the expected text.
	/// </summary>
	public PlaywrightValidationBuilder ExpectNotContainsText(string cssSelector, string text)
	{
		var page = GetPage();
		Assertions.Expect(page.Locator(cssSelector)).Not.ToContainTextAsync(text).GetAwaiter().GetResult();
		return this;
	}

	// ──────────────────────────────────────────────
	// Private helpers
	// ──────────────────────────────────────────────

	private IPage GetPage()
	{
		return _scenario.Context.GetProperty<IPage>("_PlaywrightPage")
			?? throw new InvalidOperationException(
				"No Playwright page available in context. Ensure you are using PlaywrightTestBase.");
	}

	private PlaywrightValidationBuilder AssertLocatorVisible(LocatorStrategy locator)
	{
		var page = GetPage();
		var isVisible = locator.Resolve(page).IsVisibleAsync().GetAwaiter().GetResult();
		isVisible.ShouldBeTrue($"Expected element '{locator}' to be visible");
		return this;
	}

	private PlaywrightValidationBuilder AssertLocatorHidden(LocatorStrategy locator)
	{
		var page = GetPage();
		var isVisible = locator.Resolve(page).IsVisibleAsync().GetAwaiter().GetResult();
		isVisible.ShouldBeFalse($"Expected element '{locator}' to be hidden");
		return this;
	}

	private PlaywrightValidationBuilder AssertLocatorText(LocatorStrategy locator, string expectedText)
	{
		var page = GetPage();
		var actualText = locator.Resolve(page).TextContentAsync().GetAwaiter().GetResult();
		actualText.ShouldBe(expectedText,
			$"Expected text '{expectedText}' for element '{locator}' but got '{actualText}'");
		return this;
	}

	private PlaywrightValidationBuilder AssertLocatorTextContains(LocatorStrategy locator, string expectedSubstring)
	{
		var page = GetPage();
		var actualText = locator.Resolve(page).TextContentAsync().GetAwaiter().GetResult();
		actualText.ShouldNotBeNull($"Text content for element '{locator}' is null");
		actualText!.ShouldContain(expectedSubstring);
		return this;
	}

	private PlaywrightValidationBuilder AssertLocatorAttribute(LocatorStrategy locator, string attributeName, string expectedValue)
	{
		var page = GetPage();
		var actualValue = locator.Resolve(page).GetAttributeAsync(attributeName).GetAwaiter().GetResult();
		actualValue.ShouldBe(expectedValue,
			$"Expected attribute '{attributeName}' to be '{expectedValue}' for element '{locator}' but got '{actualValue}'");
		return this;
	}

	private PlaywrightValidationBuilder AssertLocatorInputValue(LocatorStrategy locator, string expectedValue)
	{
		var page = GetPage();
		var actualValue = locator.Resolve(page).InputValueAsync().GetAwaiter().GetResult();
		actualValue.ShouldBe(expectedValue,
			$"Expected input value '{expectedValue}' for element '{locator}' but got '{actualValue}'");
		return this;
	}

	private PlaywrightValidationBuilder AssertLocatorEditable(LocatorStrategy locator)
	{
		var page = GetPage();
		var isEditable = locator.Resolve(page).IsEditableAsync().GetAwaiter().GetResult();
		isEditable.ShouldBeTrue($"Expected element '{locator}' to be editable");
		return this;
	}

	private PlaywrightValidationBuilder AssertLocatorNotEditable(LocatorStrategy locator)
	{
		var page = GetPage();
		var isEditable = locator.Resolve(page).IsEditableAsync().GetAwaiter().GetResult();
		isEditable.ShouldBeFalse($"Expected element '{locator}' to not be editable");
		return this;
	}

	private PlaywrightValidationBuilder AssertLocatorEmpty(LocatorStrategy locator)
	{
		var page = GetPage();
		var textContent = locator.Resolve(page).TextContentAsync().GetAwaiter().GetResult();
		var innerText = textContent?.Trim() ?? string.Empty;
		innerText.Length.ShouldBe(0, $"Expected element '{locator}' to be empty but found text: '{innerText}'");
		return this;
	}

	private PlaywrightValidationBuilder AssertLocatorNotEmpty(LocatorStrategy locator)
	{
		var page = GetPage();
		var textContent = locator.Resolve(page).TextContentAsync().GetAwaiter().GetResult();
		var innerText = textContent?.Trim() ?? string.Empty;
		innerText.Length.ShouldBeGreaterThan(0, $"Expected element '{locator}' to not be empty");
		return this;
	}

	private PlaywrightValidationBuilder AssertLocatorFocused(LocatorStrategy locator)
	{
		var page = GetPage();
		var resolved = locator.Resolve(page);
		Assertions.Expect(resolved).ToBeFocusedAsync().GetAwaiter().GetResult();
		return this;
	}

	private PlaywrightValidationBuilder AssertLocatorNotFocused(LocatorStrategy locator)
	{
		var page = GetPage();
		var resolved = locator.Resolve(page);
		Assertions.Expect(resolved).Not.ToBeFocusedAsync().GetAwaiter().GetResult();
		return this;
	}

	private PlaywrightValidationBuilder AssertLocatorAttached(LocatorStrategy locator)
	{
		var page = GetPage();
		var resolved = locator.Resolve(page);
		Assertions.Expect(resolved).ToBeAttachedAsync().GetAwaiter().GetResult();
		return this;
	}

	private PlaywrightValidationBuilder AssertLocatorNotAttached(LocatorStrategy locator)
	{
		var page = GetPage();
		var resolved = locator.Resolve(page);
		Assertions.Expect(resolved).Not.ToBeAttachedAsync().GetAwaiter().GetResult();
		return this;
	}

	private PlaywrightValidationBuilder AssertLocatorInViewport(LocatorStrategy locator)
	{
		var page = GetPage();
		var resolved = locator.Resolve(page);
		Assertions.Expect(resolved).ToBeInViewportAsync().GetAwaiter().GetResult();
		return this;
	}

	private PlaywrightValidationBuilder AssertLocatorClass(LocatorStrategy locator, string expectedClass)
	{
		var page = GetPage();
		var actualClass = locator.Resolve(page).GetAttributeAsync("class").GetAwaiter().GetResult();
		actualClass.ShouldBe(expectedClass,
			$"Expected class '{expectedClass}' for element '{locator}' but got '{actualClass}'");
		return this;
	}

	private PlaywrightValidationBuilder AssertLocatorContainsClass(LocatorStrategy locator, string expectedClass)
	{
		var page = GetPage();
		var actualClass = locator.Resolve(page).GetAttributeAsync("class").GetAwaiter().GetResult() ?? string.Empty;
		var actualClasses = actualClass.Split(' ', StringSplitOptions.RemoveEmptyEntries);
		var expectedClasses = expectedClass.Split(' ', StringSplitOptions.RemoveEmptyEntries);
		foreach (var cls in expectedClasses)
		{
			actualClasses.ShouldContain(cls,
				$"Expected element '{locator}' to contain class '{cls}' but actual classes are '{actualClass}'");
		}
		return this;
	}

	private PlaywrightValidationBuilder AssertLocatorCssProperty(LocatorStrategy locator, string propertyName, string expectedValue)
	{
		var page = GetPage();
		var resolved = locator.Resolve(page);
		var actualValue = resolved.EvaluateAsync<string>(
			$"el => getComputedStyle(el).getPropertyValue('{propertyName}')").GetAwaiter().GetResult();
		actualValue.ShouldBe(expectedValue,
			$"Expected CSS property '{propertyName}' to be '{expectedValue}' for element '{locator}' but got '{actualValue}'");
		return this;
	}

	private PlaywrightValidationBuilder AssertLocatorId(LocatorStrategy locator, string expectedId)
	{
		var page = GetPage();
		var actualId = locator.Resolve(page).GetAttributeAsync("id").GetAwaiter().GetResult();
		actualId.ShouldBe(expectedId,
			$"Expected id '{expectedId}' for element '{locator}' but got '{actualId}'");
		return this;
	}

	private PlaywrightValidationBuilder AssertLocatorAccessibleName(LocatorStrategy locator, string expectedName)
	{
		var page = GetPage();
		var resolved = locator.Resolve(page);
		Assertions.Expect(resolved).ToHaveAccessibleNameAsync(expectedName).GetAwaiter().GetResult();
		return this;
	}

	private PlaywrightValidationBuilder AssertLocatorAccessibleDescription(LocatorStrategy locator, string expectedDescription)
	{
		var page = GetPage();
		var resolved = locator.Resolve(page);
		Assertions.Expect(resolved).ToHaveAccessibleDescriptionAsync(expectedDescription).GetAwaiter().GetResult();
		return this;
	}

	private PlaywrightValidationBuilder AssertLocatorRole(LocatorStrategy locator, AriaRole expectedRole)
	{
		var page = GetPage();
		var resolved = locator.Resolve(page);
		Assertions.Expect(resolved).ToHaveRoleAsync(expectedRole).GetAwaiter().GetResult();
		return this;
	}

	private PlaywrightValidationBuilder AssertLocatorValues(LocatorStrategy locator, string[] expectedValues)
	{
		var page = GetPage();
		var resolved = locator.Resolve(page);
		Assertions.Expect(resolved).ToHaveValuesAsync(expectedValues).GetAwaiter().GetResult();
		return this;
	}

	private PlaywrightValidationBuilder AssertLocatorJSProperty(LocatorStrategy locator, string propertyName, object expectedValue)
	{
		var page = GetPage();
		var resolved = locator.Resolve(page);
		Assertions.Expect(resolved).ToHaveJSPropertyAsync(propertyName, expectedValue).GetAwaiter().GetResult();
		return this;
	}

	private PlaywrightValidationBuilder AssertLocatorTextMatches(LocatorStrategy locator, string pattern)
	{
		var page = GetPage();
		var actualText = locator.Resolve(page).TextContentAsync().GetAwaiter().GetResult();
		actualText.ShouldNotBeNull($"Text content for element '{locator}' is null");
		actualText!.ShouldMatch(pattern);
		return this;
	}
}
