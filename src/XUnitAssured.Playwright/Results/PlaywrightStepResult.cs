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

	/// <summary>
	/// The full HTML content of the page after all actions executed.
	/// </summary>
	public string? PageContent => Data as string;

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
	/// Creates a successful Playwright result.
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
		TimeSpan? elapsed = null)
	{
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
			Properties = new Dictionary<string, object?>
			{
				["Url"] = url,
				["Title"] = null,
				["Screenshots"] = (IReadOnlyList<string>)(screenshots ?? new List<string>()),
				["ConsoleLogs"] = (IReadOnlyList<string>)(consoleLogs ?? new List<string>())
			}
		};
	}
}
