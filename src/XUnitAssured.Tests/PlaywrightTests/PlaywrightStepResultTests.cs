using XUnitAssured.Core.Results;
using XUnitAssured.Playwright.Results;

namespace XUnitAssured.Tests.PlaywrightTests;

[Trait("Category", "Playwright")]
[Trait("Component", "Results")]
/// <summary>
/// Tests for PlaywrightStepResult factory methods and property accessors.
/// </summary>
public class PlaywrightStepResultTests
{
	[Fact(DisplayName = "CreateSuccess should create result with correct properties")]
	public void CreateSuccess_Should_Set_Properties()
	{
		// Act
		var result = PlaywrightStepResult.CreateSuccess(
			url: "https://myapp.com/dashboard",
			title: "Dashboard - My App",
			pageContent: "<html><body>Welcome</body></html>",
			screenshots: new List<string> { "screenshots/test.png" },
			consoleLogs: new List<string> { "[log] Page loaded" });

		// Assert
		result.Success.ShouldBeTrue();
		result.Errors.ShouldBeEmpty();
		result.Url.ShouldBe("https://myapp.com/dashboard");
		result.Title.ShouldBe("Dashboard - My App");
		result.PageContent.ShouldBe("<html><body>Welcome</body></html>");
		result.Screenshots.Count.ShouldBe(1);
		result.Screenshots[0].ShouldBe("screenshots/test.png");
		result.ConsoleLogs.Count.ShouldBe(1);
		result.ConsoleLogs[0].ShouldBe("[log] Page loaded");
	}

	[Fact(DisplayName = "CreateSuccess should have Succeeded status in metadata")]
	public void CreateSuccess_Should_Have_Succeeded_Status()
	{
		// Act
		var result = PlaywrightStepResult.CreateSuccess(
			url: "https://myapp.com",
			title: "Home",
			pageContent: "<html></html>");

		// Assert
		result.Metadata.ShouldNotBeNull();
		result.Metadata.Status.ShouldBe(StepStatus.Succeeded);
		result.Metadata.StartedAt.ShouldNotBe(default);
		result.Metadata.CompletedAt.ShouldNotBeNull();
	}

	[Fact(DisplayName = "CreateSuccess with null optional parameters should use empty defaults")]
	public void CreateSuccess_With_Nulls_Should_Use_Defaults()
	{
		// Act
		var result = PlaywrightStepResult.CreateSuccess(
			url: null,
			title: null,
			pageContent: null);

		// Assert
		result.Success.ShouldBeTrue();
		result.Url.ShouldBeNull();
		result.Title.ShouldBeNull();
		result.PageContent.ShouldBeNull();
		result.Screenshots.ShouldBeEmpty();
		result.ConsoleLogs.ShouldBeEmpty();
	}

	[Fact(DisplayName = "CreateSuccess should set DataType to string")]
	public void CreateSuccess_Should_Set_DataType()
	{
		// Act
		var result = PlaywrightStepResult.CreateSuccess(
			url: "https://myapp.com",
			title: "Title",
			pageContent: "<html></html>");

		// Assert
		result.DataType.ShouldBe(typeof(string));
	}

	[Fact(DisplayName = "CreateFailure should create result with error")]
	public void CreateFailure_Should_Set_Error()
	{
		// Act
		var result = PlaywrightStepResult.CreateFailure(
			error: "Element '#submit' not found",
			url: "https://myapp.com/login");

		// Assert
		result.Success.ShouldBeFalse();
		result.Errors.Count.ShouldBe(1);
		result.Errors[0].ShouldBe("Element '#submit' not found");
		result.Url.ShouldBe("https://myapp.com/login");
		result.Title.ShouldBeNull();
		result.PageContent.ShouldBeNull();
	}

	[Fact(DisplayName = "CreateFailure should have Failed status in metadata")]
	public void CreateFailure_Should_Have_Failed_Status()
	{
		// Act
		var result = PlaywrightStepResult.CreateFailure(error: "Timeout");

		// Assert
		result.Metadata.ShouldNotBeNull();
		result.Metadata.Status.ShouldBe(StepStatus.Failed);
	}

	[Fact(DisplayName = "CreateFailure should preserve screenshots taken before failure")]
	public void CreateFailure_Should_Preserve_Screenshots()
	{
		// Act
		var result = PlaywrightStepResult.CreateFailure(
			error: "Click failed",
			screenshots: new List<string> { "screenshots/before-error.png" },
			consoleLogs: new List<string> { "[error] Uncaught TypeError" });

		// Assert
		result.Screenshots.Count.ShouldBe(1);
		result.Screenshots[0].ShouldBe("screenshots/before-error.png");
		result.ConsoleLogs.Count.ShouldBe(1);
	}

	[Fact(DisplayName = "CreateFailure with null optional parameters should use empty defaults")]
	public void CreateFailure_With_Nulls_Should_Use_Defaults()
	{
		// Act
		var result = PlaywrightStepResult.CreateFailure(error: "Error");

		// Assert
		result.Url.ShouldBeNull();
		result.Screenshots.ShouldBeEmpty();
		result.ConsoleLogs.ShouldBeEmpty();
	}

	[Fact(DisplayName = "PlaywrightStepResult should extend TestStepResult")]
	public void Should_Extend_TestStepResult()
	{
		// Act
		var result = PlaywrightStepResult.CreateSuccess(
			url: "https://myapp.com",
			title: "Home",
			pageContent: "<html>content</html>");

		// Assert
		result.ShouldBeAssignableTo<TestStepResult>();
		result.ShouldBeAssignableTo<ITestStepResult>();
		result.Data.ShouldBe("<html>content</html>"); // Data == pageContent
	}

	[Fact(DisplayName = "CreateSuccess with elapsed time should set metadata timing")]
	public void CreateSuccess_With_Elapsed_Should_Set_Timing()
	{
		// Arrange
		var elapsed = TimeSpan.FromMilliseconds(1500);

		// Act
		var result = PlaywrightStepResult.CreateSuccess(
			url: "https://myapp.com",
			title: "Home",
			pageContent: null,
			elapsed: elapsed);

		// Assert
		result.Metadata.Duration.TotalMilliseconds.ShouldBeGreaterThanOrEqualTo(1400);
	}

	[Fact(DisplayName = "CreateSuccess with multiple screenshots should store all")]
	public void CreateSuccess_With_Multiple_Screenshots()
	{
		// Arrange
		var screenshots = new List<string>
		{
			"screenshots/step1.png",
			"screenshots/step2.png",
			"screenshots/step3.png"
		};

		// Act
		var result = PlaywrightStepResult.CreateSuccess(
			url: "https://myapp.com",
			title: "Home",
			pageContent: null,
			screenshots: screenshots);

		// Assert
		result.Screenshots.Count.ShouldBe(3);
		result.Screenshots[0].ShouldBe("screenshots/step1.png");
		result.Screenshots[2].ShouldBe("screenshots/step3.png");
	}

	[Fact(DisplayName = "CreateSuccess with multiple console logs should store all")]
	public void CreateSuccess_With_Multiple_ConsoleLogs()
	{
		// Arrange
		var logs = new List<string>
		{
			"[log] App initialized",
			"[warning] Deprecation warning",
			"[error] 404 resource not found"
		};

		// Act
		var result = PlaywrightStepResult.CreateSuccess(
			url: "https://myapp.com",
			title: "Home",
			pageContent: null,
			consoleLogs: logs);

		// Assert
		result.ConsoleLogs.Count.ShouldBe(3);
		result.ConsoleLogs[0].ShouldContain("initialized");
		result.ConsoleLogs[2].ShouldContain("error");
	}
}
