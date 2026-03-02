using System.Collections.Generic;

namespace XUnitAssured.Playwright.Configuration;

/// <summary>
/// Playwright configuration settings that can be loaded from playwrightsettings.json.
/// </summary>
public class PlaywrightSettings
{
	/// <summary>
	/// Base URL for navigation (e.g., "https://myapp.example.com").
	/// When set, NavigateTo("/login") resolves to "https://myapp.example.com/login".
	/// </summary>
	public string? BaseUrl { get; set; }

	/// <summary>
	/// Browser type to use for tests.
	/// Default: Chromium
	/// </summary>
	public BrowserType Browser { get; set; } = BrowserType.Chromium;

	/// <summary>
	/// Whether to run the browser in headless mode.
	/// Default: true
	/// </summary>
	public bool Headless { get; set; } = true;

	/// <summary>
	/// Slow down Playwright operations by the specified amount of milliseconds.
	/// Useful for debugging. Default: 0 (no slowdown).
	/// </summary>
	public int SlowMo { get; set; }

	/// <summary>
	/// Default timeout in milliseconds for Playwright actions (clicks, fills, waits).
	/// Default: 30000 (30 seconds).
	/// </summary>
	public int DefaultTimeout { get; set; } = 30000;

	/// <summary>
	/// Default timeout in milliseconds for navigation operations.
	/// Default: 30000 (30 seconds).
	/// </summary>
	public int NavigationTimeout { get; set; } = 30000;

	/// <summary>
	/// Viewport width in pixels.
	/// Default: 1280
	/// </summary>
	public int ViewportWidth { get; set; } = 1280;

	/// <summary>
	/// Viewport height in pixels.
	/// Default: 720
	/// </summary>
	public int ViewportHeight { get; set; } = 720;

	/// <summary>
	/// Directory path for saving screenshots.
	/// Default: "screenshots"
	/// </summary>
	public string ScreenshotPath { get; set; } = "screenshots";

	/// <summary>
	/// Whether to record traces for debugging.
	/// Default: false
	/// </summary>
	public bool RecordTrace { get; set; }

	/// <summary>
	/// Directory path for saving trace files.
	/// Default: "traces"
	/// </summary>
	public string TracePath { get; set; } = "traces";

	/// <summary>
	/// When true, traces are saved only when a test fails.
	/// When false (and RecordTrace is true), traces are saved for every test.
	/// Default: true
	/// </summary>
	public bool TraceOnFailureOnly { get; set; } = true;

	/// <summary>
	/// Whether to include DOM snapshots in traces.
	/// Enables time-travel debugging in Playwright Trace Viewer.
	/// Default: true
	/// </summary>
	public bool TraceSnapshots { get; set; } = true;

	/// <summary>
	/// Whether to include screenshots in traces.
	/// Default: true
	/// </summary>
	public bool TraceScreenshots { get; set; } = true;

	/// <summary>
	/// Whether to record video of each test execution.
	/// Videos are saved to the VideoPath directory.
	/// Default: false
	/// </summary>
	public bool RecordVideo { get; set; }

	/// <summary>
	/// Directory path for saving video recordings.
	/// Default: "videos"
	/// </summary>
	public string VideoPath { get; set; } = "videos";

	/// <summary>
	/// Whether to capture a screenshot automatically when a test fails.
	/// Default: false
	/// </summary>
	public bool ScreenshotOnFailure { get; set; }

	/// <summary>
	/// Whether to ignore HTTPS errors.
	/// Default: false
	/// </summary>
	public bool IgnoreHttpsErrors { get; set; }

	/// <summary>
	/// Custom HTTP headers to include in every request made by the browser context.
	/// </summary>
	public Dictionary<string, string>? ExtraHttpHeaders { get; set; }

	/// <summary>
	/// Enables the Playwright Inspector for interactive recording and debugging.
	/// When true, sets PWDEBUG=1 and forces Headless=false before launching the browser.
	/// Use with <c>RecordAndPause()</c> to open the Inspector during test execution.
	/// Default: false
	/// </summary>
	public bool EnableInspector { get; set; }

	/// <summary>
	/// The attribute name used for test IDs (used by GetByTestId).
	/// Default: "data-testid"
	/// </summary>
	public string TestIdAttribute { get; set; } = "data-testid";

	/// <summary>
	/// Loads settings from playwrightsettings.json file.
	/// </summary>
	/// <param name="customPath">Custom path to settings file (optional)</param>
	/// <returns>Loaded Playwright settings</returns>
	public static PlaywrightSettings Load(string? customPath = null)
	{
		return PlaywrightSettingsLoader.Load(customPath);
	}

	/// <summary>
	/// Merges this settings with another settings instance.
	/// Values from 'other' take precedence when set to non-default values.
	/// </summary>
	public PlaywrightSettings Merge(PlaywrightSettings other)
	{
		if (other == null)
			return this;

		return new PlaywrightSettings
		{
			BaseUrl = other.BaseUrl ?? BaseUrl,
			Browser = other.Browser != BrowserType.Chromium ? other.Browser : Browser,
			Headless = other.Headless,
			SlowMo = other.SlowMo != 0 ? other.SlowMo : SlowMo,
			DefaultTimeout = other.DefaultTimeout != 30000 ? other.DefaultTimeout : DefaultTimeout,
			NavigationTimeout = other.NavigationTimeout != 30000 ? other.NavigationTimeout : NavigationTimeout,
			ViewportWidth = other.ViewportWidth != 1280 ? other.ViewportWidth : ViewportWidth,
			ViewportHeight = other.ViewportHeight != 720 ? other.ViewportHeight : ViewportHeight,
			ScreenshotPath = other.ScreenshotPath != "screenshots" ? other.ScreenshotPath : ScreenshotPath,
			RecordTrace = other.RecordTrace,
			TracePath = other.TracePath != "traces" ? other.TracePath : TracePath,
			TraceOnFailureOnly = other.TraceOnFailureOnly,
			TraceSnapshots = other.TraceSnapshots,
			TraceScreenshots = other.TraceScreenshots,
			RecordVideo = other.RecordVideo,
			VideoPath = other.VideoPath != "videos" ? other.VideoPath : VideoPath,
			ScreenshotOnFailure = other.ScreenshotOnFailure,
			IgnoreHttpsErrors = other.IgnoreHttpsErrors,
			ExtraHttpHeaders = other.ExtraHttpHeaders ?? ExtraHttpHeaders,
			EnableInspector = other.EnableInspector || EnableInspector,
			TestIdAttribute = other.TestIdAttribute != "data-testid" ? other.TestIdAttribute : TestIdAttribute
		};
	}

	/// <summary>
	/// Creates a shallow copy of this settings instance.
	/// Used internally to prevent callers from mutating cached settings.
	/// </summary>
	public PlaywrightSettings Clone()
	{
		return new PlaywrightSettings
		{
			BaseUrl = BaseUrl,
			Browser = Browser,
			Headless = Headless,
			SlowMo = SlowMo,
			DefaultTimeout = DefaultTimeout,
			NavigationTimeout = NavigationTimeout,
			ViewportWidth = ViewportWidth,
			ViewportHeight = ViewportHeight,
			ScreenshotPath = ScreenshotPath,
			RecordTrace = RecordTrace,
			TracePath = TracePath,
			TraceOnFailureOnly = TraceOnFailureOnly,
			TraceSnapshots = TraceSnapshots,
			TraceScreenshots = TraceScreenshots,
			RecordVideo = RecordVideo,
			VideoPath = VideoPath,
			ScreenshotOnFailure = ScreenshotOnFailure,
			IgnoreHttpsErrors = IgnoreHttpsErrors,
			ExtraHttpHeaders = ExtraHttpHeaders != null
				? new Dictionary<string, string>(ExtraHttpHeaders)
				: null,
			EnableInspector = EnableInspector,
			TestIdAttribute = TestIdAttribute
		};
	}
}
