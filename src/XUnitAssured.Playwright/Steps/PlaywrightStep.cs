using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.Playwright;

using XUnitAssured.Core.Abstractions;
using XUnitAssured.Playwright.Codegen;
using XUnitAssured.Core.Results;
using XUnitAssured.Playwright.Configuration;
using XUnitAssured.Playwright.Results;

namespace XUnitAssured.Playwright.Steps;

/// <summary>
/// Represents a Playwright UI test step that accumulates page actions and executes them in sequence.
/// Implements <see cref="ITestStep"/> following the same pattern as HttpRequestStep and KafkaProduceStep.
/// </summary>
public class PlaywrightStep : ITestStep
{
	private readonly List<PageAction> _actions = new();
	private readonly ConcurrentQueue<string> _consoleLogs = new();

	/// <inheritdoc />
	public string? Name { get; internal set; }

	/// <inheritdoc />
	public string StepType => "Playwright";

	/// <inheritdoc />
	public ITestStepResult? Result { get; private set; }

	/// <inheritdoc />
	public bool IsExecuted => Result != null;

	/// <inheritdoc />
	public bool IsValid { get; private set; }

	/// <summary>
	/// The Playwright page instance to execute actions on.
	/// Must be set before execution (typically from the test fixture).
	/// </summary>
	public IPage? Page { get; init; }

	/// <summary>
	/// Playwright settings for this step.
	/// </summary>
	public PlaywrightSettings Settings { get; init; } = new();

	/// <summary>
	/// The list of accumulated page actions to execute.
	/// </summary>
	public IReadOnlyList<PageAction> Actions => _actions.AsReadOnly();

	/// <summary>
	/// Adds an action to the step.
	/// </summary>
	internal void AddAction(PageAction action)
	{
		_actions.Add(action ?? throw new ArgumentNullException(nameof(action)));
	}

	/// <inheritdoc />
	public async Task<ITestStepResult> ExecuteAsync(ITestContext context)
	{
		var startTime = DateTimeOffset.UtcNow;
		var screenshots = new List<string>();

		EventHandler<IConsoleMessage>? consoleHandler = null;

		try
		{
			var page = Page
				?? context.GetProperty<IPage>("_PlaywrightPage")
				?? throw new InvalidOperationException(
					"No Playwright page available. Use PlaywrightTestBase or set the page via Given(fixture).");

			// Subscribe to console messages using a named handler so we can unsubscribe later
			consoleHandler = (_, msg) => _consoleLogs.Enqueue($"[{msg.Type}] {msg.Text}");
			page.Console += consoleHandler;

			// Execute each action in sequence
			foreach (var action in _actions)
			{
				await ExecuteActionAsync(page, action, screenshots);
			}

			// Unsubscribe before snapshotting the logs to prevent further mutations
			page.Console -= consoleHandler;
			consoleHandler = null;

			var elapsed = DateTimeOffset.UtcNow - startTime;

			Result = PlaywrightStepResult.CreateSuccess(
				url: page.Url,
				title: await page.TitleAsync(),
				pageContent: await page.ContentAsync(),
				screenshots: screenshots,
				consoleLogs: _consoleLogs.ToList(),
				elapsed: elapsed);

			IsValid = true;
			return Result;
		}
		catch (Exception ex)
		{
			var elapsed = DateTimeOffset.UtcNow - startTime;

			Result = PlaywrightStepResult.CreateFailure(
				error: ex.Message,
				url: null,
				screenshots: screenshots,
				consoleLogs: _consoleLogs.ToList(),
				elapsed: elapsed);

			IsValid = false;
			return Result;
		}
		finally
		{
			// Ensure handler is unsubscribed even on failure
			if (consoleHandler != null)
			{
				var page = Page ?? context.GetProperty<IPage>("_PlaywrightPage");
				if (page != null)
					page.Console -= consoleHandler;
			}
		}
	}

	/// <inheritdoc />
	public void Validate(Action<ITestStepResult> validation)
	{
		if (Result == null)
			throw new InvalidOperationException("Step has not been executed. Call Execute() before Validate().");

		validation(Result);
		IsValid = true;
	}

	private async Task ExecuteActionAsync(IPage page, PageAction action, List<string> screenshots)
	{
		switch (action.ActionType)
		{
			case PageActionType.Navigate:
				var url = ResolveUrl(action.Value!);
				await page.GotoAsync(url, new PageGotoOptions
				{
					Timeout = Settings.NavigationTimeout
				});
				break;

			case PageActionType.Click:
				var clickLocator = action.Locator!.Resolve(page);
				await clickLocator.ClickAsync(BuildClickOptions(action));
				break;

			case PageActionType.DoubleClick:
				var dblClickLocator = action.Locator!.Resolve(page);
				await dblClickLocator.DblClickAsync(new LocatorDblClickOptions
				{
					Force = action.Force ? true : null,
					Modifiers = ParseModifiers(action.Modifiers)
				});
				break;

			case PageActionType.RightClick:
				var rightClickLocator = action.Locator!.Resolve(page);
				await rightClickLocator.ClickAsync(new LocatorClickOptions
				{
					Button = Microsoft.Playwright.MouseButton.Right,
					Force = action.Force ? true : null,
					Modifiers = ParseModifiers(action.Modifiers)
				});
				break;

			case PageActionType.Fill:
				var fillLocator = action.Locator!.Resolve(page);
				await fillLocator.FillAsync(action.Value ?? string.Empty);
				break;

			case PageActionType.Clear:
				var clearLocator = action.Locator!.Resolve(page);
				await clearLocator.ClearAsync();
				break;

			case PageActionType.Type:
				var typeLocator = action.Locator!.Resolve(page);
				await typeLocator.PressSequentiallyAsync(action.Value ?? string.Empty);
				break;

			case PageActionType.Press:
				var pressLocator = action.Locator!.Resolve(page);
				await pressLocator.PressAsync(action.Value!);
				break;

			case PageActionType.Check:
				var checkLocator = action.Locator!.Resolve(page);
				await checkLocator.CheckAsync();
				break;

			case PageActionType.Uncheck:
				var uncheckLocator = action.Locator!.Resolve(page);
				await uncheckLocator.UncheckAsync();
				break;

			case PageActionType.SetChecked:
				var setCheckedLocator = action.Locator!.Resolve(page);
				var isChecked = string.Equals(action.Value, "true", StringComparison.OrdinalIgnoreCase);
				await setCheckedLocator.SetCheckedAsync(isChecked);
				break;

			case PageActionType.SelectOption:
				var selectLocator = action.Locator!.Resolve(page);
				await selectLocator.SelectOptionAsync(action.Value!);
				break;

			case PageActionType.SelectMultipleOptions:
				var multiSelectLocator = action.Locator!.Resolve(page);
				await multiSelectLocator.SelectOptionAsync(action.Values ?? Array.Empty<string>());
				break;

			case PageActionType.Hover:
				var hoverLocator = action.Locator!.Resolve(page);
				await hoverLocator.HoverAsync(new LocatorHoverOptions
				{
					Force = action.Force ? true : null,
					Modifiers = ParseModifiers(action.Modifiers)
				});
				break;

			case PageActionType.Focus:
				var focusLocator = action.Locator!.Resolve(page);
				await focusLocator.FocusAsync();
				break;

			case PageActionType.UploadFile:
				var uploadLocator = action.Locator!.Resolve(page);
				if (action.FilePaths is { Length: > 1 })
					await uploadLocator.SetInputFilesAsync(action.FilePaths);
				else if (action.FilePaths is { Length: 1 })
					await uploadLocator.SetInputFilesAsync(action.FilePaths[0]);
				else if (action.Value != null)
					await uploadLocator.SetInputFilesAsync(action.Value);
				break;

			case PageActionType.ClearUploadFile:
				var clearUploadLocator = action.Locator!.Resolve(page);
				await clearUploadLocator.SetInputFilesAsync(Array.Empty<string>());
				break;

			case PageActionType.DragTo:
				var dragSource = action.Locator!.Resolve(page);
				var dropTarget = action.TargetLocator!.Resolve(page);
				await dragSource.DragToAsync(dropTarget);
				break;

			case PageActionType.ScrollIntoView:
				var scrollLocator = action.Locator!.Resolve(page);
				await scrollLocator.ScrollIntoViewIfNeededAsync();
				break;

			case PageActionType.DispatchEvent:
				var dispatchLocator = action.Locator!.Resolve(page);
				await dispatchLocator.DispatchEventAsync(action.Value!);
				break;

			case PageActionType.Screenshot:
				var screenshotDir = Settings.ScreenshotPath;
				if (!Directory.Exists(screenshotDir))
					Directory.CreateDirectory(screenshotDir);

				var fileName = action.Value ?? $"screenshot_{DateTimeOffset.UtcNow:yyyyMMdd_HHmmss_fff}.png";
				var filePath = Path.Combine(screenshotDir, fileName);
				await page.ScreenshotAsync(new PageScreenshotOptions { Path = filePath, FullPage = true });
				screenshots.Add(filePath);
				break;

			case PageActionType.Wait:
				if (int.TryParse(action.Value, out var delayMs))
					await Task.Delay(delayMs);
				break;

			case PageActionType.WaitForSelector:
				await page.WaitForSelectorAsync(action.Value!);
				break;

			case PageActionType.Pause:
				Console.WriteLine();
				Console.WriteLine("╔══════════════════════════════════════════════════════════════════════════╗");
				Console.WriteLine("║              🎬  Playwright Inspector — RecordAndPause()               ║");
				Console.WriteLine("╠══════════════════════════════════════════════════════════════════════════╣");
				Console.WriteLine("║                                                                        ║");
				Console.WriteLine("║  1. Click the 🔴 Record button in the Inspector toolbar                ║");
				Console.WriteLine("║  2. Interact with the page (click, type, navigate...)                   ║");
				Console.WriteLine("║  3. Select 'C# Library' in the language dropdown                       ║");
				Console.WriteLine("║  4. Copy the generated C# code from the Inspector panel                ║");
				Console.WriteLine("║  5. Click ▶ Resume to continue the test                                ║");
				Console.WriteLine("║                                                                        ║");
				Console.WriteLine("║  💡 After copying, translate with:                                     ║");
				Console.WriteLine("║     PlaywrightCodeTranslator.Translate(code)                            ║");
				Console.WriteLine("║                                                                        ║");
				Console.WriteLine($"║  📍 URL: {page.Url,-63}║");
				Console.WriteLine("╚══════════════════════════════════════════════════════════════════════════╝");
				Console.WriteLine();
				await page.PauseAsync();

				// After Resume: show example translation
				Console.WriteLine();
				Console.WriteLine("╔══════════════════════════════════════════════════════════════════════════╗");
				Console.WriteLine("║          ✅  Inspector closed — Translate your recorded code           ║");
				Console.WriteLine("╠══════════════════════════════════════════════════════════════════════════╣");
				Console.WriteLine("║                                                                        ║");
				Console.WriteLine("║  Paste your Playwright C# code into:                                   ║");
				Console.WriteLine("║                                                                        ║");
				Console.WriteLine("║    var dsl = PlaywrightCodeTranslator.Translate(@\"                     ║");
				Console.WriteLine("║      await page.GetByRole(...).ClickAsync();                            ║");
				Console.WriteLine("║      await page.GetByLabel(...).FillAsync(...);                         ║");
				Console.WriteLine("║    \");                                                                 ║");
				Console.WriteLine("║                                                                        ║");
				Console.WriteLine("║  Example translation:                                                  ║");
				Console.WriteLine("║    Playwright C#:                                                      ║");
				Console.WriteLine("║      await page.GetByRole(AriaRole.Button,                             ║");
				Console.WriteLine("║        new() { Name = \"Click me\" }).ClickAsync();                     ║");
				Console.WriteLine("║    XUnitAssured DSL:                                                   ║");
				Console.WriteLine("║      .ClickByRole(AriaRole.Button, \"Click me\")                        ║");
				Console.WriteLine("║                                                                        ║");
				Console.WriteLine("╚══════════════════════════════════════════════════════════════════════════╝");
				Console.WriteLine();
				break;

			default:
				throw new NotSupportedException($"Action type '{action.ActionType}' is not supported.");
		}
	}

	private string ResolveUrl(string url)
	{
		if (Uri.IsWellFormedUriString(url, UriKind.Absolute))
			return url;

		if (!string.IsNullOrEmpty(Settings.BaseUrl))
		{
			var baseUrl = Settings.BaseUrl.TrimEnd('/');
			var path = url.StartsWith("/") ? url : "/" + url;
			return baseUrl + path;
		}

		return url;
	}

	private static LocatorClickOptions? BuildClickOptions(PageAction action)
	{
		var hasModifiers = action.Modifiers is { Length: > 0 };
		var hasForce = action.Force;

		if (!hasModifiers && !hasForce)
			return null;

		return new LocatorClickOptions
		{
			Force = hasForce ? true : null,
			Modifiers = ParseModifiers(action.Modifiers)
		};
	}

	private static IEnumerable<KeyboardModifier>? ParseModifiers(string[]? modifiers)
	{
		if (modifiers is not { Length: > 0 })
			return null;

		var result = new List<KeyboardModifier>();
		foreach (var mod in modifiers)
		{
			if (Enum.TryParse<KeyboardModifier>(mod, ignoreCase: true, out var parsed))
				result.Add(parsed);
		}
		return result;
	}
}
