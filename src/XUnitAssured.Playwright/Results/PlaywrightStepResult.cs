using System;
using System.Collections.Generic;
using XUnitAssured.Core.Results;

namespace XUnitAssured.Playwright.Results;

/// <summary>
/// Specialized result for Playwright UI test steps.
/// Extends TestStepResult with browser-specific properties and helper methods.
/// </summary>
public class PlaywrightStepResult : TestStepResult
{
	/// <summary>
	/// The current page URL after all actions executed.
	/// </summary>
	public string? Url => GetProperty<string>("Url");

	/// <summary>
	/// The page title after all actions executed.
	/// </summary>
	public string? Title => GetProperty<string>("Title");

	private readonly Lazy<string?>? _pageContent;

	/// <summary>
	/// Creates an empty result. Used by the factory methods.
	/// </summary>
	public PlaywrightStepResult()
	{
	}

	private PlaywrightStepResult(Func<string?> pageContentProvider)
	{
		_pageContent = new Lazy<string?>(pageContentProvider, isThreadSafe: true);
	}

	/// <summary>
	/// The full HTML content of the page after all actions executed.
	/// </summary>
	/// <remarks>
	/// For a result produced by a step, the content is fetched from the page the
	/// first time this is read and kept from then on. Serialising a large page costs
	/// far more than the step itself (measured: ~106 ms for a 500 KB DOM against
	/// ~5 ms for the rest), and most steps never read it. Read it while the page is
	/// still open; once the page is closed it is <c>null</c>.
	/// </remarks>
	public string? PageContent => Data as string ?? _pageContent?.Value;

	/// <inheritdoc />
	/// <remarks>
	/// Keeps <c>GetData&lt;string&gt;()</c> equivalent to <see cref="PageContent"/>
	/// for results whose content is captured lazily.
	/// </remarks>
	public override T? GetData<T>() where T : default
	{
		if (Data == null && _pageContent != null && typeof(T) == typeof(string))
			return (T?)(object?)_pageContent.Value;

		return base.GetData<T>();
	}

	/// <summary>
	/// List of file paths for screenshots taken during the step.
	/// </summary>
	public IReadOnlyList<string> Screenshots =>
		GetProperty<IReadOnlyList<string>>("Screenshots") ?? Array.Empty<string>();

	/// <summary>
	/// Browser console log messages captured during the step.
	/// </summary>
	public IReadOnlyList<string> ConsoleLogs =>
		GetProperty<IReadOnlyList<string>>("ConsoleLogs") ?? Array.Empty<string>();

	/// <summary>
	/// Creates a successful Playwright result whose page content is fetched only if
	/// something reads <see cref="PageContent"/>.
	/// </summary>
	/// <param name="url">The page URL after the actions ran.</param>
	/// <param name="title">The page title after the actions ran.</param>
	/// <param name="pageContentProvider">
	/// Returns the page HTML on demand. Called at most once, on the first read.
	/// Should return <c>null</c> rather than throw when the page is no longer available.
	/// </param>
	/// <param name="screenshots">Screenshot paths taken during the step.</param>
	/// <param name="consoleLogs">Console messages captured during the step.</param>
	/// <param name="elapsed">How long the step took.</param>
	public static PlaywrightStepResult CreateSuccess(
		string? url,
		string? title,
		Func<string?> pageContentProvider,
		List<string>? screenshots = null,
		List<string>? consoleLogs = null,
		TimeSpan? elapsed = null)
	{
		if (pageContentProvider == null)
			throw new ArgumentNullException(nameof(pageContentProvider));

		return new PlaywrightStepResult(pageContentProvider)
		{
			Success = true,
			Errors = Array.Empty<string>(),
			// Data stays null: the content lives in the lazy holder and is exposed
			// through PageContent and GetData<string>().
			Data = null,
			DataType = typeof(string),
			Metadata = new StepMetadata
			{
				StartedAt = DateTimeOffset.UtcNow - (elapsed ?? TimeSpan.Zero),
				CompletedAt = DateTimeOffset.UtcNow,
				Status = StepStatus.Succeeded
			},
			Properties = new Dictionary<string, object?>
			{
				["Url"] = url,
				["Title"] = title,
				["Screenshots"] = (IReadOnlyList<string>)(screenshots ?? new List<string>()),
				["ConsoleLogs"] = (IReadOnlyList<string>)(consoleLogs ?? new List<string>())
			}
		};
	}

	/// <summary>
	/// Creates a successful Playwright result with the page content already captured.
	/// </summary>
	public static PlaywrightStepResult CreateSuccess(
		string? url,
		string? title,
		string? pageContent,
		List<string>? screenshots = null,
		List<string>? consoleLogs = null,
		TimeSpan? elapsed = null)
	{
		return new PlaywrightStepResult
		{
			Success = true,
			Errors = Array.Empty<string>(),
			Data = pageContent,
			DataType = typeof(string),
			Metadata = new StepMetadata
			{
				StartedAt = DateTimeOffset.UtcNow - (elapsed ?? TimeSpan.Zero),
				CompletedAt = DateTimeOffset.UtcNow,
				Status = StepStatus.Succeeded
			},
			Properties = new Dictionary<string, object?>
			{
				["Url"] = url,
				["Title"] = title,
				["Screenshots"] = (IReadOnlyList<string>)(screenshots ?? new List<string>()),
				["ConsoleLogs"] = (IReadOnlyList<string>)(consoleLogs ?? new List<string>())
			}
		};
	}

	/// <summary>
	/// Creates a failed Playwright result.
	/// </summary>
	public static PlaywrightStepResult CreateFailure(
		string error,
		string? url = null,
		List<string>? screenshots = null,
		List<string>? consoleLogs = null,
		TimeSpan? elapsed = null,
		Exception? exception = null)
	{
		var properties = new Dictionary<string, object?>
		{
			["Url"] = url,
			["Title"] = null,
			["Screenshots"] = (IReadOnlyList<string>)(screenshots ?? new List<string>()),
			["ConsoleLogs"] = (IReadOnlyList<string>)(consoleLogs ?? new List<string>())
		};

		// A UI failure message on its own rarely says which action broke. Keeping the
		// exception detail makes the failing step traceable back to its call site.
		if (exception != null)
		{
			properties["ExceptionType"] = exception.GetType().FullName;
			properties["ExceptionStackTrace"] = exception.StackTrace;
			properties["ExceptionDetail"] = exception.ToString();
		}

		return new PlaywrightStepResult
		{
			Success = false,
			Errors = new[] { error },
			Data = null,
			DataType = typeof(string),
			Metadata = new StepMetadata
			{
				StartedAt = DateTimeOffset.UtcNow - (elapsed ?? TimeSpan.Zero),
				CompletedAt = DateTimeOffset.UtcNow,
				Status = StepStatus.Failed
			},
			Properties = properties
		};
	}
}
