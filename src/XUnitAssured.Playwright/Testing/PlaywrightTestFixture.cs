using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Playwright;
using Xunit;
using XUnitAssured.Playwright.Configuration;

namespace XUnitAssured.Playwright.Testing;

/// <summary>
/// Base test fixture for Playwright UI tests.
/// Manages the browser lifecycle: Playwright → Browser → BrowserContext → Page.
/// The Browser is created once per test class and shared. Each test gets a fresh BrowserContext + Page.
/// Implements IAsyncLifetime for xUnit async setup/teardown.
/// </summary>
/// <example>
/// <code>
/// public class MyPlaywrightFixture : PlaywrightTestFixture
/// {
///     // Optionally override settings:
///     protected override PlaywrightSettings CreateSettings() => new()
///     {
///         BaseUrl = "https://myapp.example.com",
///         Headless = true,
///         Browser = BrowserType.Chromium
///     };
/// }
/// </code>
/// </example>
public class PlaywrightTestFixture : IAsyncLifetime, IDisposable
{
	private IPlaywright? _playwright;
	private IBrowser? _browser;
	private bool _disposed;

	/// <summary>
	/// The Playwright settings for this fixture.
	/// </summary>
	public PlaywrightSettings Settings { get; private set; } = new();

	/// <summary>
	/// The shared browser instance (one per test class).
	/// </summary>
	public IBrowser? Browser => _browser;

	/// <summary>
	/// Override this method to provide custom PlaywrightSettings.
	/// Default implementation loads from playwrightsettings.json or uses defaults.
	/// </summary>
	protected virtual PlaywrightSettings CreateSettings()
	{
		return PlaywrightSettings.Load();
	}

	/// <summary>
	/// Initializes Playwright and launches the browser.
	/// Called once before any tests in the class run.
	/// </summary>
	public async Task InitializeAsync()
	{
		Settings = CreateSettings();

		// When EnableInspector is true, set PWDEBUG so PauseAsync() opens the Inspector,
		// and force headed mode (Inspector requires a visible browser window).
		if (Settings.EnableInspector)
		{
			Environment.SetEnvironmentVariable("PWDEBUG", "1");
			Settings.Headless = false;
		}

		_playwright = await Microsoft.Playwright.Playwright.CreateAsync();

		var launchOptions = new BrowserTypeLaunchOptions
		{
			Headless = Settings.Headless,
			SlowMo = Settings.SlowMo > 0 ? Settings.SlowMo : null
		};

		_browser = Settings.Browser switch
		{
			Configuration.BrowserType.Firefox => await _playwright.Firefox.LaunchAsync(launchOptions),
			Configuration.BrowserType.WebKit => await _playwright.Webkit.LaunchAsync(launchOptions),
			_ => await _playwright.Chromium.LaunchAsync(launchOptions)
		};
	}

	/// <summary>
	/// Creates a new browser context with settings applied.
	/// Each test should get its own context for isolation.
	/// </summary>
	public async Task<IBrowserContext> CreateContextAsync()
	{
		if (_browser == null)
			throw new InvalidOperationException("Browser not initialized. Ensure InitializeAsync() has completed.");

		var contextOptions = new BrowserNewContextOptions
		{
			ViewportSize = new ViewportSize
			{
				Width = Settings.ViewportWidth,
				Height = Settings.ViewportHeight
			},
			IgnoreHTTPSErrors = Settings.IgnoreHttpsErrors,
			BaseURL = Settings.BaseUrl
		};

		if (Settings.ExtraHttpHeaders != null && Settings.ExtraHttpHeaders.Count > 0)
		{
			contextOptions.ExtraHTTPHeaders = Settings.ExtraHttpHeaders;
		}

		if (Settings.RecordVideo)
		{
			var videoDir = Settings.VideoPath;
			if (!Directory.Exists(videoDir))
				Directory.CreateDirectory(videoDir);

			contextOptions.RecordVideoDir = videoDir;
			contextOptions.RecordVideoSize = new RecordVideoSize
			{
				Width = Settings.ViewportWidth,
				Height = Settings.ViewportHeight
			};
		}

		var context = await _browser.NewContextAsync(contextOptions);
		context.SetDefaultTimeout(Settings.DefaultTimeout);
		context.SetDefaultNavigationTimeout(Settings.NavigationTimeout);

		return context;
	}

	/// <summary>
	/// Creates a new page within a new browser context.
	/// Convenience method that creates both context and page.
	/// </summary>
	public async Task<(IBrowserContext Context, IPage Page)> CreatePageAsync()
	{
		var context = await CreateContextAsync();
		var page = await context.NewPageAsync();
		return (context, page);
	}

	/// <summary>
	/// Closes the browser and disposes Playwright.
	/// Called once after all tests in the class have run.
	/// </summary>
	public async Task DisposeAsync()
	{
		if (_browser != null)
		{
			await _browser.CloseAsync();
			_browser = null;
		}

		_playwright?.Dispose();
		_playwright = null;
	}

	/// <summary>
	/// Synchronous dispose for safety.
	/// </summary>
	public void Dispose()
	{
		if (_disposed) return;
		_disposed = true;

		_browser?.CloseAsync().GetAwaiter().GetResult();
		_playwright?.Dispose();

		GC.SuppressFinalize(this);
	}
}
