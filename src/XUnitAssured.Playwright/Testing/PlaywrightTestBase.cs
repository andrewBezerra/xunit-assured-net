using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Playwright;
using Xunit;
using XUnitAssured.Core.Abstractions;
using XUnitAssured.Core.DSL;
using XUnitAssured.Playwright.Configuration;

namespace XUnitAssured.Playwright.Testing;

/// <summary>
/// Base class for Playwright UI tests using a specific fixture type.
/// Provides a Given() method pre-configured with the fixture's browser page and settings.
/// Each test gets a fresh BrowserContext + Page for isolation.
/// Implements IAsyncLifetime to manage per-test page lifecycle.
/// When tracing is enabled, each test records a Playwright trace viewable via:
///   pwsh bin/Debug/net10.0/playwright.ps1 show-trace traces/TestClassName.MethodName.zip
/// </summary>
/// <typeparam name="TFixture">The fixture type, must extend PlaywrightTestFixture.</typeparam>
public abstract class PlaywrightTestBase<TFixture> : IAsyncLifetime
	where TFixture : PlaywrightTestFixture
{
	private IBrowserContext? _context;
	private IPage? _page;
	private bool _testFailed = true;

	/// <summary>
	/// The test fixture providing browser lifecycle management.
	/// </summary>
	protected TFixture Fixture { get; }

	/// <summary>
	/// The Playwright page for the current test.
	/// A new page is created for each test for isolation.
	/// </summary>
	protected IPage Page => _page
		?? throw new InvalidOperationException("Page not initialized. Ensure InitializeAsync() has completed.");

	/// <summary>
	/// The Playwright settings from the fixture.
	/// </summary>
	protected PlaywrightSettings Settings => Fixture.Settings;

	/// <summary>
	/// Initializes a new instance of PlaywrightTestBase.
	/// </summary>
	/// <param name="fixture">The test fixture</param>
	protected PlaywrightTestBase(TFixture fixture)
	{
		Fixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
	}

	/// <summary>
	/// Creates a new BrowserContext + Page for each test.
	/// If tracing is enabled, starts recording a trace.
	/// </summary>
	public async Task InitializeAsync()
	{
		var (context, page) = await Fixture.CreatePageAsync();
		_context = context;
		_page = page;

		if (Settings.RecordTrace)
		{
			await _context.Tracing.StartAsync(new TracingStartOptions
			{
				Screenshots = Settings.TraceScreenshots,
				Snapshots = Settings.TraceSnapshots,
				Sources = true
			});
		}
	}

	/// <summary>
	/// Disposes the BrowserContext + Page after each test.
	/// If tracing is enabled, saves the trace file.
	/// If ScreenshotOnFailure is enabled and the test failed, captures a screenshot.
	/// </summary>
	public async Task DisposeAsync()
	{
		try
		{
			if (_page != null && _context != null)
			{
				// Capture screenshot on failure if enabled
				if (Settings.ScreenshotOnFailure && _testFailed)
				{
					await CaptureFailureScreenshotAsync();
				}

				// Save trace if recording is active
				if (Settings.RecordTrace)
				{
					var shouldSave = !Settings.TraceOnFailureOnly || _testFailed;
					if (shouldSave)
					{
						await SaveTraceAsync();
					}
					else
					{
						// Stop tracing without saving
						await _context.Tracing.StopAsync();
					}
				}
			}
		}
		finally
		{
			if (_page != null)
			{
				await _page.CloseAsync();
				_page = null;
			}

			if (_context != null)
			{
				await _context.CloseAsync();
				_context = null;
			}
		}
	}

	/// <summary>
	/// Starts a new test scenario pre-configured with the current test's page and settings.
	/// This is the entry point for the fluent DSL.
	/// </summary>
	/// <returns>A new test scenario with the Playwright page set in context</returns>
	protected ITestScenario Given()
	{
		var scenario = ScenarioDsl.Given();

		// Store page and settings in context for steps and validation to use
		scenario.Context.SetProperty("_PlaywrightPage", Page);
		scenario.Context.SetProperty("_PlaywrightSettings", Settings);

		return scenario;
	}

	/// <summary>
	/// Marks the current test as passed so trace is not saved during disposal
	/// when TraceOnFailureOnly is enabled.
	/// Call this at the end of a successful test to skip trace saving.
	/// Note: When using the Given/When/Then DSL, failure is assumed by default
	/// since xUnit catches exceptions after DisposeAsync runs.
	/// </summary>
	protected void MarkTestPassed()
	{
		_testFailed = false;
	}

	private async Task SaveTraceAsync()
	{
		if (_context == null) return;

		var traceDir = Settings.TracePath;
		if (!Directory.Exists(traceDir))
			Directory.CreateDirectory(traceDir);

		var testName = GetTestName();
		var tracePath = Path.Combine(traceDir, $"{testName}.zip");

		await _context.Tracing.StopAsync(new TracingStopOptions
		{
			Path = tracePath
		});
	}

	private async Task CaptureFailureScreenshotAsync()
	{
		if (_page == null) return;

		var screenshotDir = Settings.ScreenshotPath;
		if (!Directory.Exists(screenshotDir))
			Directory.CreateDirectory(screenshotDir);

		var testName = GetTestName();
		var screenshotPath = Path.Combine(screenshotDir, $"{testName}_FAILED.png");

		await _page.ScreenshotAsync(new PageScreenshotOptions
		{
			Path = screenshotPath,
			FullPage = true
		});
	}

	private string GetTestName()
	{
		var typeName = GetType().Name;
		var timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd_HHmmss_fff");
		return $"{typeName}_{timestamp}";
	}
}
