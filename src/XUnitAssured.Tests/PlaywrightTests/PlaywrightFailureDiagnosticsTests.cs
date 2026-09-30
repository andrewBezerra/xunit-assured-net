using System;
using System.Collections.Generic;

using Shouldly;
using Xunit;

using XUnitAssured.Playwright.Results;

namespace XUnitAssured.Tests.PlaywrightTests;

[Trait("Category", "Playwright")]
[Trait("Component", "StepResult")]
/// <summary>
/// Tests the detail carried by a failed Playwright step result.
/// A UI failure used to surface only the exception message, which for a locator
/// timeout does not say which action in the chain broke or what the page looked like.
/// </summary>
public class PlaywrightFailureDiagnosticsTests
{
	private static Exception CapturedException()
	{
		try
		{
			throw new TimeoutException("Locator.ClickAsync: Timeout 30000ms exceeded.");
		}
		catch (TimeoutException ex)
		{
			// Thrown and caught so the instance carries a real stack trace.
			return ex;
		}
	}

	[Fact(DisplayName = "Failure result should expose the exception type and stack trace")]
	public void Failure_Should_Expose_Exception_Diagnostics()
	{
		var exception = CapturedException();

		var result = PlaywrightStepResult.CreateFailure(
			error: exception.Message,
			url: "https://example.test/login",
			exception: exception);

		result.Success.ShouldBeFalse();
		result.Errors.ShouldHaveSingleItem();
		result.Url.ShouldBe("https://example.test/login");
		result.GetProperty<string>("ExceptionType").ShouldBe(typeof(TimeoutException).FullName);
		result.GetProperty<string>("ExceptionStackTrace").ShouldNotBeNullOrWhiteSpace();
		result.GetProperty<string>("ExceptionDetail").ShouldContain(nameof(TimeoutException));
	}

	[Fact(DisplayName = "Failure result should omit exception properties when no exception is supplied")]
	public void Failure_Should_Omit_Exception_Properties_When_Absent()
	{
		var result = PlaywrightStepResult.CreateFailure(error: "assertion failed");

		result.Success.ShouldBeFalse();
		result.Properties.ContainsKey("ExceptionType").ShouldBeFalse();
		result.Properties.ContainsKey("ExceptionStackTrace").ShouldBeFalse();
	}

	[Fact(DisplayName = "Failure result should retain screenshots captured before the failure")]
	public void Failure_Should_Retain_Screenshots()
	{
		var result = PlaywrightStepResult.CreateFailure(
			error: "boom",
			screenshots: new List<string> { "screenshots/step1.png", "screenshots/failure_x.png" });

		result.Screenshots.Count.ShouldBe(2);
		result.Screenshots.ShouldContain("screenshots/failure_x.png");
	}

	[Fact(DisplayName = "Failure result should retain captured console logs")]
	public void Failure_Should_Retain_Console_Logs()
	{
		var result = PlaywrightStepResult.CreateFailure(
			error: "boom",
			consoleLogs: new List<string> { "[error] Uncaught TypeError" });

		result.ConsoleLogs.ShouldHaveSingleItem();
		result.ConsoleLogs[0].ShouldContain("TypeError");
	}
}
