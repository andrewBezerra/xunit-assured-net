namespace XUnitAssured.Playwright.Configuration;

/// <summary>
/// Supported browser types for Playwright tests.
/// </summary>
public enum BrowserType
{
	/// <summary>
	/// Chromium-based browser (default). Includes Chrome and Edge.
	/// </summary>
	Chromium = 0,

	/// <summary>
	/// Mozilla Firefox browser.
	/// </summary>
	Firefox = 1,

	/// <summary>
	/// Apple WebKit browser engine (Safari).
	/// </summary>
	WebKit = 2
}
