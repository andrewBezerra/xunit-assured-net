using System;
using Microsoft.Playwright;

namespace XUnitAssured.Playwright.Locators;

/// <summary>
/// Defines the type of locator strategy used to find elements on a page.
/// </summary>
public enum LocatorType
{
	/// <summary>CSS selector (e.g., "#id", ".class", "input[name='email']").</summary>
	Css,
	/// <summary>ARIA role (e.g., Button, Heading, Link).</summary>
	Role,
	/// <summary>Visible text content.</summary>
	Text,
	/// <summary>Associated label text.</summary>
	Label,
	/// <summary>Placeholder attribute value.</summary>
	Placeholder,
	/// <summary>Test ID attribute (data-testid by default).</summary>
	TestId,
	/// <summary>Alt text (for images).</summary>
	AltText,
	/// <summary>Title attribute.</summary>
	Title
}

/// <summary>
/// Encapsulates a locator strategy for finding elements on a page.
/// Resolves to a Playwright <see cref="ILocator"/> at execution time.
/// </summary>
public class LocatorStrategy
{
	/// <summary>
	/// The type of locator strategy.
	/// </summary>
	public LocatorType Type { get; }

	/// <summary>
	/// The selector or text value used to locate the element.
	/// For CSS: the CSS selector string.
	/// For Role: the role name string (resolved to AriaRole).
	/// For Text/Label/Placeholder/TestId/AltText/Title: the text to match.
	/// </summary>
	public string Selector { get; }

	/// <summary>
	/// Optional name filter for role-based locators (e.g., role=Button with name="Submit").
	/// Also used as an additional filter for other locator types when applicable.
	/// </summary>
	public string? Name { get; }

	/// <summary>
	/// Whether the text match should be exact.
	/// Default: false (substring match).
	/// </summary>
	public bool Exact { get; }

	private LocatorStrategy(LocatorType type, string selector, string? name = null, bool exact = false)
	{
		Type = type;
		Selector = selector ?? throw new ArgumentNullException(nameof(selector));
		Name = name;
		Exact = exact;
	}

	/// <summary>
	/// Creates a CSS selector locator.
	/// </summary>
	/// <param name="selector">CSS selector (e.g., "#login-btn", ".submit", "input[type='email']")</param>
	public static LocatorStrategy Css(string selector)
		=> new(LocatorType.Css, selector);

	/// <summary>
	/// Creates an ARIA role locator.
	/// </summary>
	/// <param name="role">ARIA role name (e.g., "button", "heading", "link")</param>
	/// <param name="name">Accessible name filter (optional)</param>
	/// <param name="exact">Whether the name match should be exact</param>
	public static LocatorStrategy ByRole(string role, string? name = null, bool exact = false)
		=> new(LocatorType.Role, role, name, exact);

	/// <summary>
	/// Creates an ARIA role locator using the AriaRole enum.
	/// </summary>
	/// <param name="role">ARIA role enum value</param>
	/// <param name="name">Accessible name filter (optional)</param>
	/// <param name="exact">Whether the name match should be exact</param>
	public static LocatorStrategy ByRole(AriaRole role, string? name = null, bool exact = false)
		=> new(LocatorType.Role, role.ToString(), name, exact);

	/// <summary>
	/// Creates a text content locator.
	/// </summary>
	/// <param name="text">Text to match</param>
	/// <param name="exact">Whether the match should be exact</param>
	public static LocatorStrategy ByText(string text, bool exact = false)
		=> new(LocatorType.Text, text, exact: exact);

	/// <summary>
	/// Creates a label locator (finds form elements by their label text).
	/// </summary>
	/// <param name="label">Label text to match</param>
	/// <param name="exact">Whether the match should be exact</param>
	public static LocatorStrategy ByLabel(string label, bool exact = false)
		=> new(LocatorType.Label, label, exact: exact);

	/// <summary>
	/// Creates a placeholder locator.
	/// </summary>
	/// <param name="placeholder">Placeholder text to match</param>
	/// <param name="exact">Whether the match should be exact</param>
	public static LocatorStrategy ByPlaceholder(string placeholder, bool exact = false)
		=> new(LocatorType.Placeholder, placeholder, exact: exact);

	/// <summary>
	/// Creates a test ID locator (matches data-testid attribute by default).
	/// </summary>
	/// <param name="testId">Test ID value to match</param>
	public static LocatorStrategy ByTestId(string testId)
		=> new(LocatorType.TestId, testId);

	/// <summary>
	/// Creates an alt text locator (for images).
	/// </summary>
	/// <param name="altText">Alt text to match</param>
	/// <param name="exact">Whether the match should be exact</param>
	public static LocatorStrategy ByAltText(string altText, bool exact = false)
		=> new(LocatorType.AltText, altText, exact: exact);

	/// <summary>
	/// Creates a title attribute locator.
	/// </summary>
	/// <param name="title">Title text to match</param>
	/// <param name="exact">Whether the match should be exact</param>
	public static LocatorStrategy ByTitle(string title, bool exact = false)
		=> new(LocatorType.Title, title, exact: exact);

	/// <summary>
	/// Resolves this locator strategy to a Playwright <see cref="ILocator"/> on the given page.
	/// </summary>
	/// <param name="page">The Playwright page to locate elements on</param>
	/// <returns>A Playwright locator</returns>
	public ILocator Resolve(IPage page)
	{
		return Type switch
		{
			LocatorType.Css => page.Locator(Selector),

			LocatorType.Role => page.GetByRole(
				ParseAriaRole(Selector),
				new PageGetByRoleOptions { Name = Name, Exact = Exact || Name != null }),

			LocatorType.Text => page.GetByText(Selector,
				new PageGetByTextOptions { Exact = Exact }),

			LocatorType.Label => page.GetByLabel(Selector,
				new PageGetByLabelOptions { Exact = Exact }),

			LocatorType.Placeholder => page.GetByPlaceholder(Selector,
				new PageGetByPlaceholderOptions { Exact = Exact }),

			LocatorType.TestId => page.GetByTestId(Selector),

			LocatorType.AltText => page.GetByAltText(Selector,
				new PageGetByAltTextOptions { Exact = Exact }),

			LocatorType.Title => page.GetByTitle(Selector,
				new PageGetByTitleOptions { Exact = Exact }),

			_ => throw new NotSupportedException($"Locator type '{Type}' is not supported.")
		};
	}

	private static AriaRole ParseAriaRole(string role)
	{
		if (Enum.TryParse<AriaRole>(role, ignoreCase: true, out var ariaRole))
			return ariaRole;

		throw new ArgumentException(
			$"Unknown ARIA role '{role}'. Use values from Microsoft.Playwright.AriaRole enum.",
			nameof(role));
	}

	/// <inheritdoc />
	public override string ToString()
	{
		return Type switch
		{
			LocatorType.Css => $"css={Selector}",
			LocatorType.Role when Name != null => $"role={Selector}[name=\"{Name}\"]",
			LocatorType.Role => $"role={Selector}",
			_ => $"{Type.ToString().ToLowerInvariant()}={Selector}"
		};
	}
}
