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

	private PlaywrightStepResult(Lazy<string?>? pageContent)
	{
		_pageContent = pageContent;
	}

	/// <summary>
	/// The addresses a route set up with <c>InterceptRoute</c> answered during the step, in order.
	/// </summary>
	/// <remarks>
	/// What lets a test require that its scenario happened. A route that matches nothing answers
	/// nothing, and a test asserting only the outcome would pass without having provoked it.
	/// </remarks>
	public IReadOnlyList<string> InterceptedRequests =>
		GetProperty<IReadOnlyList<string>>("InterceptedRequests") ?? Array.Empty<string>();

	/// <summary>The requests made with <c>FetchFromPage</c> during the step, in order.</summary>
	public IReadOnlyList<PageFetch> Fetches =>
		GetProperty<IReadOnlyList<PageFetch>>("Fetches") ?? Array.Empty<PageFetch>();

	/// <summary>The last request made with <c>FetchFromPage</c> during the step, or null.</summary>
	public PageFetch? LastFetch => Fetches.Count == 0 ? null : Fetches[Fetches.Count - 1];

	/// <summary>
	/// Uma cópia com o que o passo fez na rede. As fábricas públicas ficam como estão: mudar a
	/// assinatura delas quebraria código já compilado contra o pacote.
	/// </summary>
	internal PlaywrightStepResult ComRede(IReadOnlyList<string> interceptadas, IReadOnlyList<PageFetch> buscas)
	{
		var propriedades = new Dictionary<string, object?>(Properties)
		{
			["InterceptedRequests"] = interceptadas,
			["Fetches"] = buscas
		};

		return new PlaywrightStepResult(_pageContent)
		{
			Success = Success,
			Errors = Errors,
			Data = Data,
			DataType = DataType,
			Metadata = Metadata,
			Properties = propriedades
		};
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
	/// The addresses the page requested while the step ran, in the order they were made.
	/// </summary>
	/// <remarks>
	/// Only what this step caused: the recording starts when the step starts. A request the
	/// page makes on its own after the step has finished is not here, which is what makes
	/// asserting that something was requested exactly once mean anything.
	/// </remarks>
	public IReadOnlyList<string> Requests =>
		GetProperty<IReadOnlyList<string>>("Requests") ?? Array.Empty<string>();

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
	/// <param name="requests">Addresses the page requested during the step.</param>
	/// <param name="elapsed">How long the step took.</param>
	public static PlaywrightStepResult CreateSuccess(
		string? url,
		string? title,
		Func<string?> pageContentProvider,
		List<string>? screenshots = null,
		List<string>? consoleLogs = null,
		List<string>? requests = null,
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
				["ConsoleLogs"] = (IReadOnlyList<string>)(consoleLogs ?? new List<string>()),
				["Requests"] = (IReadOnlyList<string>)(requests ?? new List<string>())
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
		List<string>? requests = null,
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
				["ConsoleLogs"] = (IReadOnlyList<string>)(consoleLogs ?? new List<string>()),
				["Requests"] = (IReadOnlyList<string>)(requests ?? new List<string>())
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
		List<string>? requests = null,
		TimeSpan? elapsed = null,
		Exception? exception = null)
	{
		var properties = new Dictionary<string, object?>
		{
			["Url"] = url,
			["Title"] = null,
			["Screenshots"] = (IReadOnlyList<string>)(screenshots ?? new List<string>()),
			["ConsoleLogs"] = (IReadOnlyList<string>)(consoleLogs ?? new List<string>()),
			["Requests"] = (IReadOnlyList<string>)(requests ?? new List<string>())
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
