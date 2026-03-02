using System.Collections.Generic;
using XUnitAssured.Playwright.Locators;

namespace XUnitAssured.Playwright.Steps;

/// <summary>
/// Defines the type of page action to perform.
/// </summary>
public enum PageActionType
{
	/// <summary>Navigate to a URL.</summary>
	Navigate,
	/// <summary>Click an element.</summary>
	Click,
	/// <summary>Double-click an element.</summary>
	DoubleClick,
	/// <summary>Fill a text input with a value.</summary>
	Fill,
	/// <summary>Clear a text input.</summary>
	Clear,
	/// <summary>Type text character by character (simulates keyboard input).</summary>
	Type,
	/// <summary>Press a keyboard key or shortcut (e.g., "Enter", "Control+A").</summary>
	Press,
	/// <summary>Check a checkbox or radio button.</summary>
	Check,
	/// <summary>Uncheck a checkbox.</summary>
	Uncheck,
	/// <summary>Select an option from a dropdown.</summary>
	SelectOption,
	/// <summary>Hover over an element.</summary>
	Hover,
	/// <summary>Focus on an element.</summary>
	Focus,
	/// <summary>Take a screenshot of the page.</summary>
	Screenshot,
	/// <summary>Wait for a specified duration in milliseconds.</summary>
	Wait,
	/// <summary>Wait for a specific selector to appear.</summary>
	WaitForSelector,
	/// <summary>Upload one or more files to a file input element.</summary>
	UploadFile,
	/// <summary>Clear the selected files from a file input element.</summary>
	ClearUploadFile,
	/// <summary>Drag an element and drop it onto another element.</summary>
	DragTo,
	/// <summary>Scroll an element into the visible area of the browser window.</summary>
	ScrollIntoView,
	/// <summary>Right-click (context menu) on an element.</summary>
	RightClick,
	/// <summary>Dispatch a DOM event on an element programmatically.</summary>
	DispatchEvent,
	/// <summary>Set the checked state of a checkbox or radio button to a specific value.</summary>
	SetChecked,
	/// <summary>Select multiple options in a multi-select dropdown.</summary>
	SelectMultipleOptions,
	/// <summary>
	/// Pause the page and open the Playwright Inspector for interactive recording.
	/// Requires headed mode (Headless = false). Use this to record actions via Codegen
	/// and then translate them into the XUnitAssured fluent DSL.
	/// </summary>
	Pause
}

/// <summary>
/// Represents a single page action to be executed during a Playwright test step.
/// Actions are accumulated and executed in sequence when the step runs.
/// </summary>
public class PageAction
{
	/// <summary>
	/// The type of action to perform.
	/// </summary>
	public PageActionType ActionType { get; init; }

	/// <summary>
	/// The locator strategy to find the target element.
	/// Null for actions that don't target a specific element (Navigate, Screenshot, Wait).
	/// </summary>
	public LocatorStrategy? Locator { get; init; }

	/// <summary>
	/// The value to use for the action.
	/// For Navigate: the URL.
	/// For Fill/Type: the text value.
	/// For Press: the key(s) to press.
	/// For SelectOption: the option value.
	/// For Screenshot: the file name.
	/// For Wait: the duration in milliseconds as string.
	/// For WaitForSelector: the CSS selector string.
	/// For DispatchEvent: the event type (e.g., "click", "change").
	/// For SetChecked: "true" or "false".
	/// </summary>
	public string? Value { get; init; }

	/// <summary>
	/// The target locator for drag-and-drop operations.
	/// The source element is identified by <see cref="Locator"/>, the drop target by this property.
	/// </summary>
	public LocatorStrategy? TargetLocator { get; init; }

	/// <summary>
	/// File paths for file upload actions.
	/// Used by UploadFile action type.
	/// </summary>
	public string[]? FilePaths { get; init; }

	/// <summary>
	/// Multiple values for actions that support them.
	/// Used by SelectMultipleOptions for selecting multiple options.
	/// </summary>
	public string[]? Values { get; init; }

	/// <summary>
	/// Keyboard modifiers to apply during click actions (e.g., "Shift", "Control", "Alt", "Meta", "ControlOrMeta").
	/// </summary>
	public string[]? Modifiers { get; init; }

	/// <summary>
	/// Whether to force the action, bypassing actionability checks.
	/// Applicable to Click, RightClick, and other pointer actions.
	/// </summary>
	public bool Force { get; init; }

	/// <inheritdoc />
	public override string ToString()
	{
		var parts = new List<string>();

		if (Locator != null)
			parts.Add(Locator.ToString());
		if (TargetLocator != null)
			parts.Add($"target={TargetLocator}");
		if (Value != null)
			parts.Add($"\"{Value}\"");
		if (FilePaths is { Length: > 0 })
			parts.Add($"files=[{string.Join(", ", FilePaths)}]");
		if (Values is { Length: > 0 })
			parts.Add($"values=[{string.Join(", ", Values)}]");
		if (Modifiers is { Length: > 0 })
			parts.Add($"modifiers=[{string.Join("+", Modifiers)}]");
		if (Force)
			parts.Add("force=true");

		return parts.Count > 0
			? $"{ActionType}({string.Join(", ", parts)})"
			: $"{ActionType}()";
	}
}
