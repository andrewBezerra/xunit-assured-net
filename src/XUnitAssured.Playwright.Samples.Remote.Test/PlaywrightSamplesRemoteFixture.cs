using XUnitAssured.Playwright.Configuration;
using XUnitAssured.Playwright.Testing;

namespace XUnitAssured.Playwright.Samples.Remote.Test;

/// <summary>
/// Test fixture for Playwright E2E tests against a remote website.
/// Connects to a publicly available site configured in playwrightsettings.json.
/// No local server is hosted — the browser navigates directly to the remote URL.
/// </summary>
/// <remarks>
/// Configuration is loaded from playwrightsettings.json:
/// <code>
/// {
///   "BaseUrl": "https://demo.playwright.dev/todomvc",
///   "Headless": true,
///   "Browser": "Chromium"
/// }
/// </code>
///
/// Override the target URL via environment variable:
/// <code>
/// SET PLAYWRIGHT_BASE_URL=https://my-staging-app.com
/// dotnet test
/// </code>
/// </remarks>
public class PlaywrightSamplesRemoteFixture : PlaywrightTestFixture
{
	/// <summary>
	/// The base URL of the remote site under test.
	/// </summary>
	public string BaseUrl => Settings.BaseUrl
		?? throw new InvalidOperationException(
			"BaseUrl is not configured. Set 'BaseUrl' in playwrightsettings.json.");

	/// <summary>
	/// Loads PlaywrightSettings from playwrightsettings.json,
	/// then applies environment variable override if present.
	/// </summary>
	protected override PlaywrightSettings CreateSettings()
	{
		PlaywrightSettingsLoader.ClearCache();
		var settings = PlaywrightSettings.Load();

		// Allow environment variable override for CI/CD pipelines
		var envUrl = Environment.GetEnvironmentVariable("PLAYWRIGHT_BASE_URL");
		if (!string.IsNullOrWhiteSpace(envUrl))
		{
			settings.BaseUrl = envUrl;
		}

		return settings;
	}
}
