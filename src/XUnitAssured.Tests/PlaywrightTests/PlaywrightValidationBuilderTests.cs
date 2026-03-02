using System.Text.RegularExpressions;
using Microsoft.Playwright;
using XUnitAssured.Core.Abstractions;
using XUnitAssured.Core.DSL;
using XUnitAssured.Core.Results;
using XUnitAssured.Core.Storage;
using XUnitAssured.Playwright.Extensions;
using XUnitAssured.Playwright.Results;

namespace XUnitAssured.Tests.PlaywrightTests;

[Trait("Category", "Playwright")]
[Trait("Component", "Validation")]
/// <summary>
/// Tests for PlaywrightValidationBuilder assertion methods.
/// Covers URL, Title, PageContent, Console, Screenshot, Success/Failure, and fluent chaining.
/// Methods that require a live IPage (locator-based) are tested for correct error when page is absent.
/// </summary>
public class PlaywrightValidationBuilderTests
{
	// ──────────────────────────────────────────────
	// Helpers
	// ──────────────────────────────────────────────

	/// <summary>
	/// Creates a scenario with a mock step that has the given PlaywrightStepResult already set.
	/// </summary>
	private static (ITestScenario scenario, PlaywrightValidationBuilder builder) CreateBuilder(PlaywrightStepResult result)
	{
		var scenario = ScenarioDsl.Given();
		var mockStep = new MockPlaywrightStep(result);
		scenario.SetCurrentStep(mockStep);
		var builder = new PlaywrightValidationBuilder(scenario);
		return (scenario, builder);
	}

	/// <summary>
	/// Creates a default success result for convenience.
	/// </summary>
	private static PlaywrightStepResult SuccessResult(
		string? url = "https://myapp.com/dashboard",
		string? title = "Dashboard - My App",
		string? pageContent = "<html><body>Welcome</body></html>",
		List<string>? screenshots = null,
		List<string>? consoleLogs = null)
	{
		return PlaywrightStepResult.CreateSuccess(url, title, pageContent, screenshots, consoleLogs);
	}

	// ──────────────────────────────────────────────
	// Then / AssertSuccess / AssertFailure
	// ──────────────────────────────────────────────

	[Fact(DisplayName = "Then should return PlaywrightValidationBuilder for fluent chaining")]
	public void Then_Should_Return_PlaywrightValidationBuilder()
	{
		var (_, builder) = CreateBuilder(SuccessResult());

		var result = builder.Then();

		result.ShouldBeOfType<PlaywrightValidationBuilder>();
		result.ShouldBeSameAs(builder);
	}

	[Fact(DisplayName = "AssertSuccess should pass when result is successful")]
	public void AssertSuccess_Should_Pass_When_Successful()
	{
		var (_, builder) = CreateBuilder(SuccessResult());

		var result = builder.AssertSuccess();

		result.ShouldBeOfType<PlaywrightValidationBuilder>();
	}

	[Fact(DisplayName = "AssertSuccess should throw when result is failure")]
	public void AssertSuccess_Should_Throw_When_Failure()
	{
		var failResult = PlaywrightStepResult.CreateFailure("Something went wrong");
		var (_, builder) = CreateBuilder(failResult);

		Should.Throw<ShouldAssertException>(() => builder.AssertSuccess());
	}

	[Fact(DisplayName = "AssertFailure should pass when result is failure")]
	public void AssertFailure_Should_Pass_When_Failure()
	{
		var failResult = PlaywrightStepResult.CreateFailure("Error occurred");
		var (_, builder) = CreateBuilder(failResult);

		var result = builder.AssertFailure();

		result.ShouldBeOfType<PlaywrightValidationBuilder>();
	}

	[Fact(DisplayName = "AssertFailure should throw when result is success")]
	public void AssertFailure_Should_Throw_When_Success()
	{
		var (_, builder) = CreateBuilder(SuccessResult());

		Should.Throw<ShouldAssertException>(() => builder.AssertFailure());
	}

	// ──────────────────────────────────────────────
	// URL Assertions
	// ──────────────────────────────────────────────

	[Fact(DisplayName = "AssertUrl should pass when URL matches exactly")]
	public void AssertUrl_Should_Pass_When_Match()
	{
		var (_, builder) = CreateBuilder(SuccessResult(url: "https://myapp.com/login"));

		var result = builder.AssertUrl("https://myapp.com/login");

		result.ShouldBeOfType<PlaywrightValidationBuilder>();
	}

	[Fact(DisplayName = "AssertUrl should throw when URL does not match")]
	public void AssertUrl_Should_Throw_When_No_Match()
	{
		var (_, builder) = CreateBuilder(SuccessResult(url: "https://myapp.com/dashboard"));

		Should.Throw<ShouldAssertException>(() => builder.AssertUrl("https://myapp.com/login"));
	}

	[Fact(DisplayName = "AssertUrlContains should pass when URL contains substring")]
	public void AssertUrlContains_Should_Pass_When_Contains()
	{
		var (_, builder) = CreateBuilder(SuccessResult(url: "https://myapp.com/dashboard?tab=users"));

		var result = builder.AssertUrlContains("dashboard");

		result.ShouldBeOfType<PlaywrightValidationBuilder>();
	}

	[Fact(DisplayName = "AssertUrlContains should throw when URL does not contain substring")]
	public void AssertUrlContains_Should_Throw_When_Not_Contains()
	{
		var (_, builder) = CreateBuilder(SuccessResult(url: "https://myapp.com/home"));

		Should.Throw<ShouldAssertException>(() => builder.AssertUrlContains("dashboard"));
	}

	[Fact(DisplayName = "AssertUrlContains should throw when URL is null")]
	public void AssertUrlContains_Should_Throw_When_Null()
	{
		var (_, builder) = CreateBuilder(SuccessResult(url: null));

		Should.Throw<ShouldAssertException>(() => builder.AssertUrlContains("anything"));
	}

	[Fact(DisplayName = "AssertUrlEndsWith should pass when URL ends with suffix")]
	public void AssertUrlEndsWith_Should_Pass_When_Ends_With()
	{
		var (_, builder) = CreateBuilder(SuccessResult(url: "https://myapp.com/products/list"));

		var result = builder.AssertUrlEndsWith("/products/list");

		result.ShouldBeOfType<PlaywrightValidationBuilder>();
	}

	[Fact(DisplayName = "AssertUrlEndsWith should throw when URL does not end with suffix")]
	public void AssertUrlEndsWith_Should_Throw_When_Not_Ends_With()
	{
		var (_, builder) = CreateBuilder(SuccessResult(url: "https://myapp.com/products/detail"));

		Should.Throw<ShouldAssertException>(() => builder.AssertUrlEndsWith("/products/list"));
	}

	[Fact(DisplayName = "AssertUrlEndsWith should throw when URL is null")]
	public void AssertUrlEndsWith_Should_Throw_When_Null()
	{
		var (_, builder) = CreateBuilder(SuccessResult(url: null));

		Should.Throw<ShouldAssertException>(() => builder.AssertUrlEndsWith("/page"));
	}

	[Fact(DisplayName = "AssertUrlMatches with string pattern should pass when URL matches")]
	public void AssertUrlMatches_String_Should_Pass_When_Match()
	{
		var (_, builder) = CreateBuilder(SuccessResult(url: "https://myapp.com/users/123"));

		var result = builder.AssertUrlMatches(@"users/\d+");

		result.ShouldBeOfType<PlaywrightValidationBuilder>();
	}

	[Fact(DisplayName = "AssertUrlMatches with string pattern should throw when URL does not match")]
	public void AssertUrlMatches_String_Should_Throw_When_No_Match()
	{
		var (_, builder) = CreateBuilder(SuccessResult(url: "https://myapp.com/products"));

		Should.Throw<ShouldAssertException>(() => builder.AssertUrlMatches(@"users/\d+"));
	}

	[Fact(DisplayName = "AssertUrlMatches with Regex should pass when URL matches")]
	public void AssertUrlMatches_Regex_Should_Pass_When_Match()
	{
		var (_, builder) = CreateBuilder(SuccessResult(url: "https://myapp.com/Users/42"));

		var result = builder.AssertUrlMatches(new Regex(@"users/\d+", RegexOptions.IgnoreCase));

		result.ShouldBeOfType<PlaywrightValidationBuilder>();
	}

	[Fact(DisplayName = "AssertUrlMatches with Regex should throw when URL does not match")]
	public void AssertUrlMatches_Regex_Should_Throw_When_No_Match()
	{
		var (_, builder) = CreateBuilder(SuccessResult(url: "https://myapp.com/products"));

		Should.Throw<ShouldAssertException>(() =>
			builder.AssertUrlMatches(new Regex(@"users/\d+", RegexOptions.IgnoreCase)));
	}

	[Fact(DisplayName = "AssertUrlMatches with string pattern should throw when URL is null")]
	public void AssertUrlMatches_String_Should_Throw_When_Null()
	{
		var (_, builder) = CreateBuilder(SuccessResult(url: null));

		Should.Throw<ShouldAssertException>(() => builder.AssertUrlMatches(@"any"));
	}

	[Fact(DisplayName = "AssertUrlMatches with Regex should throw when URL is null")]
	public void AssertUrlMatches_Regex_Should_Throw_When_Null()
	{
		var (_, builder) = CreateBuilder(SuccessResult(url: null));

		Should.Throw<ShouldAssertException>(() => builder.AssertUrlMatches(new Regex(@"any")));
	}

	// ──────────────────────────────────────────────
	// Title Assertions
	// ──────────────────────────────────────────────

	[Fact(DisplayName = "AssertTitle should pass when title matches exactly")]
	public void AssertTitle_Should_Pass_When_Match()
	{
		var (_, builder) = CreateBuilder(SuccessResult(title: "Home Page"));

		var result = builder.AssertTitle("Home Page");

		result.ShouldBeOfType<PlaywrightValidationBuilder>();
	}

	[Fact(DisplayName = "AssertTitle should throw when title does not match")]
	public void AssertTitle_Should_Throw_When_No_Match()
	{
		var (_, builder) = CreateBuilder(SuccessResult(title: "Dashboard"));

		Should.Throw<ShouldAssertException>(() => builder.AssertTitle("Home Page"));
	}

	[Fact(DisplayName = "AssertTitleContains should pass when title contains substring")]
	public void AssertTitleContains_Should_Pass_When_Contains()
	{
		var (_, builder) = CreateBuilder(SuccessResult(title: "My Application - Dashboard"));

		var result = builder.AssertTitleContains("Dashboard");

		result.ShouldBeOfType<PlaywrightValidationBuilder>();
	}

	[Fact(DisplayName = "AssertTitleContains should throw when title does not contain substring")]
	public void AssertTitleContains_Should_Throw_When_Not_Contains()
	{
		var (_, builder) = CreateBuilder(SuccessResult(title: "My Application - Home"));

		Should.Throw<ShouldAssertException>(() => builder.AssertTitleContains("Dashboard"));
	}

	[Fact(DisplayName = "AssertTitleContains should throw when title is null")]
	public void AssertTitleContains_Should_Throw_When_Null()
	{
		var (_, builder) = CreateBuilder(SuccessResult(title: null));

		Should.Throw<ShouldAssertException>(() => builder.AssertTitleContains("anything"));
	}

	[Fact(DisplayName = "AssertTitleMatches with string pattern should pass when title matches")]
	public void AssertTitleMatches_String_Should_Pass_When_Match()
	{
		var (_, builder) = CreateBuilder(SuccessResult(title: "Page 42 - Results"));

		var result = builder.AssertTitleMatches(@"Page \d+ - Results");

		result.ShouldBeOfType<PlaywrightValidationBuilder>();
	}

	[Fact(DisplayName = "AssertTitleMatches with string pattern should throw when title does not match")]
	public void AssertTitleMatches_String_Should_Throw_When_No_Match()
	{
		var (_, builder) = CreateBuilder(SuccessResult(title: "Home"));

		Should.Throw<ShouldAssertException>(() => builder.AssertTitleMatches(@"Page \d+"));
	}

	[Fact(DisplayName = "AssertTitleMatches with Regex should pass when title matches")]
	public void AssertTitleMatches_Regex_Should_Pass_When_Match()
	{
		var (_, builder) = CreateBuilder(SuccessResult(title: "DASHBOARD v2.0"));

		var result = builder.AssertTitleMatches(new Regex(@"dashboard v\d+\.\d+", RegexOptions.IgnoreCase));

		result.ShouldBeOfType<PlaywrightValidationBuilder>();
	}

	[Fact(DisplayName = "AssertTitleMatches with Regex should throw when title does not match")]
	public void AssertTitleMatches_Regex_Should_Throw_When_No_Match()
	{
		var (_, builder) = CreateBuilder(SuccessResult(title: "Home"));

		Should.Throw<ShouldAssertException>(() =>
			builder.AssertTitleMatches(new Regex(@"dashboard v\d+", RegexOptions.IgnoreCase)));
	}

	[Fact(DisplayName = "AssertTitleMatches with string pattern should throw when title is null")]
	public void AssertTitleMatches_String_Should_Throw_When_Null()
	{
		var (_, builder) = CreateBuilder(SuccessResult(title: null));

		Should.Throw<ShouldAssertException>(() => builder.AssertTitleMatches(@"any"));
	}

	[Fact(DisplayName = "AssertTitleMatches with Regex should throw when title is null")]
	public void AssertTitleMatches_Regex_Should_Throw_When_Null()
	{
		var (_, builder) = CreateBuilder(SuccessResult(title: null));

		Should.Throw<ShouldAssertException>(() => builder.AssertTitleMatches(new Regex(@"any")));
	}

	// ──────────────────────────────────────────────
	// Page Content Assertions
	// ──────────────────────────────────────────────

	[Fact(DisplayName = "AssertPageContains should pass when page content contains expected string")]
	public void AssertPageContains_Should_Pass_When_Contains()
	{
		var (_, builder) = CreateBuilder(SuccessResult(pageContent: "<html><body><h1>Welcome</h1></body></html>"));

		var result = builder.AssertPageContains("Welcome");

		result.ShouldBeOfType<PlaywrightValidationBuilder>();
	}

	[Fact(DisplayName = "AssertPageContains should throw when page content does not contain expected string")]
	public void AssertPageContains_Should_Throw_When_Not_Contains()
	{
		var (_, builder) = CreateBuilder(SuccessResult(pageContent: "<html><body><h1>Home</h1></body></html>"));

		Should.Throw<ShouldAssertException>(() => builder.AssertPageContains("Dashboard"));
	}

	[Fact(DisplayName = "AssertPageContains should throw when page content is null")]
	public void AssertPageContains_Should_Throw_When_Null()
	{
		var (_, builder) = CreateBuilder(SuccessResult(pageContent: null));

		Should.Throw<ShouldAssertException>(() => builder.AssertPageContains("anything"));
	}

	// ──────────────────────────────────────────────
	// Console Log Assertions
	// ──────────────────────────────────────────────

	[Fact(DisplayName = "AssertNoConsoleErrors should pass when no error logs")]
	public void AssertNoConsoleErrors_Should_Pass_When_No_Errors()
	{
		var logs = new List<string> { "[log] Page loaded", "[info] Data fetched" };
		var (_, builder) = CreateBuilder(SuccessResult(consoleLogs: logs));

		var result = builder.AssertNoConsoleErrors();

		result.ShouldBeOfType<PlaywrightValidationBuilder>();
	}

	[Fact(DisplayName = "AssertNoConsoleErrors should pass when console logs are empty")]
	public void AssertNoConsoleErrors_Should_Pass_When_Empty()
	{
		var (_, builder) = CreateBuilder(SuccessResult(consoleLogs: new List<string>()));

		var result = builder.AssertNoConsoleErrors();

		result.ShouldBeOfType<PlaywrightValidationBuilder>();
	}

	[Fact(DisplayName = "AssertNoConsoleErrors should throw when error logs exist")]
	public void AssertNoConsoleErrors_Should_Throw_When_Errors()
	{
		var logs = new List<string> { "[log] Page loaded", "[error] Failed to fetch data", "[error] Unhandled promise rejection" };
		var (_, builder) = CreateBuilder(SuccessResult(consoleLogs: logs));

		Should.Throw<ShouldAssertException>(() => builder.AssertNoConsoleErrors());
	}

	[Fact(DisplayName = "AssertConsoleLogCount should pass when count matches")]
	public void AssertConsoleLogCount_Should_Pass_When_Match()
	{
		var logs = new List<string> { "[log] One", "[log] Two", "[log] Three" };
		var (_, builder) = CreateBuilder(SuccessResult(consoleLogs: logs));

		var result = builder.AssertConsoleLogCount(3);

		result.ShouldBeOfType<PlaywrightValidationBuilder>();
	}

	[Fact(DisplayName = "AssertConsoleLogCount should throw when count does not match")]
	public void AssertConsoleLogCount_Should_Throw_When_No_Match()
	{
		var logs = new List<string> { "[log] One", "[log] Two" };
		var (_, builder) = CreateBuilder(SuccessResult(consoleLogs: logs));

		Should.Throw<ShouldAssertException>(() => builder.AssertConsoleLogCount(5));
	}

	[Fact(DisplayName = "AssertConsoleLogCount should pass with zero when no logs")]
	public void AssertConsoleLogCount_Should_Pass_With_Zero()
	{
		var (_, builder) = CreateBuilder(SuccessResult(consoleLogs: new List<string>()));

		var result = builder.AssertConsoleLogCount(0);

		result.ShouldBeOfType<PlaywrightValidationBuilder>();
	}

	// ──────────────────────────────────────────────
	// Screenshot Assertions
	// ──────────────────────────────────────────────

	[Fact(DisplayName = "AssertScreenshotTaken should pass when screenshots exist")]
	public void AssertScreenshotTaken_Should_Pass_When_Screenshots_Exist()
	{
		var screenshots = new List<string> { "screenshots/test.png" };
		var (_, builder) = CreateBuilder(SuccessResult(screenshots: screenshots));

		var result = builder.AssertScreenshotTaken();

		result.ShouldBeOfType<PlaywrightValidationBuilder>();
	}

	[Fact(DisplayName = "AssertScreenshotTaken should throw when no screenshots")]
	public void AssertScreenshotTaken_Should_Throw_When_Empty()
	{
		var (_, builder) = CreateBuilder(SuccessResult(screenshots: new List<string>()));

		Should.Throw<ShouldAssertException>(() => builder.AssertScreenshotTaken());
	}

	[Fact(DisplayName = "AssertScreenshotExists should pass when screenshot file is found")]
	public void AssertScreenshotExists_Should_Pass_When_Found()
	{
		var screenshots = new List<string> { "screenshots/login-page.png", "screenshots/dashboard.png" };
		var (_, builder) = CreateBuilder(SuccessResult(screenshots: screenshots));

		var result = builder.AssertScreenshotExists("login-page.png");

		result.ShouldBeOfType<PlaywrightValidationBuilder>();
	}

	[Fact(DisplayName = "AssertScreenshotExists should throw when screenshot file is not found")]
	public void AssertScreenshotExists_Should_Throw_When_Not_Found()
	{
		var screenshots = new List<string> { "screenshots/login-page.png" };
		var (_, builder) = CreateBuilder(SuccessResult(screenshots: screenshots));

		Should.Throw<ShouldAssertException>(() => builder.AssertScreenshotExists("dashboard.png"));
	}

	// ──────────────────────────────────────────────
	// Fluent Chaining
	// ──────────────────────────────────────────────

	[Fact(DisplayName = "Should support fluent chaining of multiple assertions")]
	public void Should_Support_Fluent_Chaining()
	{
		var screenshots = new List<string> { "screenshots/test.png" };
		var consoleLogs = new List<string> { "[log] Page loaded" };
		var (_, builder) = CreateBuilder(SuccessResult(
			url: "https://myapp.com/dashboard",
			title: "Dashboard - My App",
			pageContent: "<html><body>Welcome</body></html>",
			screenshots: screenshots,
			consoleLogs: consoleLogs));

		var result = builder
			.Then()
			.AssertSuccess()
			.AssertUrl("https://myapp.com/dashboard")
			.AssertUrlContains("dashboard")
			.AssertUrlEndsWith("/dashboard")
			.AssertTitle("Dashboard - My App")
			.AssertTitleContains("Dashboard")
			.AssertPageContains("Welcome")
			.AssertNoConsoleErrors()
			.AssertConsoleLogCount(1)
			.AssertScreenshotTaken()
			.AssertScreenshotExists("test.png");

		result.ShouldBeOfType<PlaywrightValidationBuilder>();
	}

	// ──────────────────────────────────────────────
	// GetPage guard (no IPage in context)
	// ──────────────────────────────────────────────

	[Fact(DisplayName = "AssertVisible should throw InvalidOperationException when no page in context")]
	public void AssertVisible_Should_Throw_When_No_Page()
	{
		var (_, builder) = CreateBuilder(SuccessResult());

		Should.Throw<InvalidOperationException>(() => builder.AssertVisible("#element"));
	}

	[Fact(DisplayName = "AssertVisibleByRole should throw InvalidOperationException when no page in context")]
	public void AssertVisibleByRole_Should_Throw_When_No_Page()
	{
		var (_, builder) = CreateBuilder(SuccessResult());

		Should.Throw<InvalidOperationException>(() => builder.AssertVisibleByRole(AriaRole.Button, "Submit"));
	}

	[Fact(DisplayName = "AssertVisibleByTestId should throw InvalidOperationException when no page in context")]
	public void AssertVisibleByTestId_Should_Throw_When_No_Page()
	{
		var (_, builder) = CreateBuilder(SuccessResult());

		Should.Throw<InvalidOperationException>(() => builder.AssertVisibleByTestId("my-element"));
	}

	[Fact(DisplayName = "AssertHidden should throw InvalidOperationException when no page in context")]
	public void AssertHidden_Should_Throw_When_No_Page()
	{
		var (_, builder) = CreateBuilder(SuccessResult());

		Should.Throw<InvalidOperationException>(() => builder.AssertHidden("#element"));
	}

	[Fact(DisplayName = "AssertText should throw InvalidOperationException when no page in context")]
	public void AssertText_Should_Throw_When_No_Page()
	{
		var (_, builder) = CreateBuilder(SuccessResult());

		Should.Throw<InvalidOperationException>(() => builder.AssertText("#element", "expected"));
	}

	[Fact(DisplayName = "AssertElementCount should throw InvalidOperationException when no page in context")]
	public void AssertElementCount_Should_Throw_When_No_Page()
	{
		var (_, builder) = CreateBuilder(SuccessResult());

		Should.Throw<InvalidOperationException>(() => builder.AssertElementCount(".item", 5));
	}

	[Fact(DisplayName = "AssertAttribute should throw InvalidOperationException when no page in context")]
	public void AssertAttribute_Should_Throw_When_No_Page()
	{
		var (_, builder) = CreateBuilder(SuccessResult());

		Should.Throw<InvalidOperationException>(() => builder.AssertAttribute("#input", "placeholder", "Email"));
	}

	[Fact(DisplayName = "AssertInputValue should throw InvalidOperationException when no page in context")]
	public void AssertInputValue_Should_Throw_When_No_Page()
	{
		var (_, builder) = CreateBuilder(SuccessResult());

		Should.Throw<InvalidOperationException>(() => builder.AssertInputValue("#email", "test@test.com"));
	}

	[Fact(DisplayName = "AssertChecked should throw InvalidOperationException when no page in context")]
	public void AssertChecked_Should_Throw_When_No_Page()
	{
		var (_, builder) = CreateBuilder(SuccessResult());

		Should.Throw<InvalidOperationException>(() => builder.AssertChecked("#checkbox"));
	}

	[Fact(DisplayName = "AssertEnabled should throw InvalidOperationException when no page in context")]
	public void AssertEnabled_Should_Throw_When_No_Page()
	{
		var (_, builder) = CreateBuilder(SuccessResult());

		Should.Throw<InvalidOperationException>(() => builder.AssertEnabled("#button"));
	}

	[Fact(DisplayName = "AssertDisabled should throw InvalidOperationException when no page in context")]
	public void AssertDisabled_Should_Throw_When_No_Page()
	{
		var (_, builder) = CreateBuilder(SuccessResult());

		Should.Throw<InvalidOperationException>(() => builder.AssertDisabled("#button"));
	}

	[Fact(DisplayName = "AssertEditable should throw InvalidOperationException when no page in context")]
	public void AssertEditable_Should_Throw_When_No_Page()
	{
		var (_, builder) = CreateBuilder(SuccessResult());

		Should.Throw<InvalidOperationException>(() => builder.AssertEditable("#input"));
	}

	[Fact(DisplayName = "AssertEmpty should throw InvalidOperationException when no page in context")]
	public void AssertEmpty_Should_Throw_When_No_Page()
	{
		var (_, builder) = CreateBuilder(SuccessResult());

		Should.Throw<InvalidOperationException>(() => builder.AssertEmpty("#container"));
	}

	[Fact(DisplayName = "AssertFocused should throw InvalidOperationException when no page in context")]
	public void AssertFocused_Should_Throw_When_No_Page()
	{
		var (_, builder) = CreateBuilder(SuccessResult());

		Should.Throw<InvalidOperationException>(() => builder.AssertFocused("#input"));
	}

	[Fact(DisplayName = "AssertAttached should throw InvalidOperationException when no page in context")]
	public void AssertAttached_Should_Throw_When_No_Page()
	{
		var (_, builder) = CreateBuilder(SuccessResult());

		Should.Throw<InvalidOperationException>(() => builder.AssertAttached("#element"));
	}

	[Fact(DisplayName = "AssertInViewport should throw InvalidOperationException when no page in context")]
	public void AssertInViewport_Should_Throw_When_No_Page()
	{
		var (_, builder) = CreateBuilder(SuccessResult());

		Should.Throw<InvalidOperationException>(() => builder.AssertInViewport("#element"));
	}

	[Fact(DisplayName = "AssertClass should throw InvalidOperationException when no page in context")]
	public void AssertClass_Should_Throw_When_No_Page()
	{
		var (_, builder) = CreateBuilder(SuccessResult());

		Should.Throw<InvalidOperationException>(() => builder.AssertClass("#btn", "active"));
	}

	[Fact(DisplayName = "AssertContainsClass should throw InvalidOperationException when no page in context")]
	public void AssertContainsClass_Should_Throw_When_No_Page()
	{
		var (_, builder) = CreateBuilder(SuccessResult());

		Should.Throw<InvalidOperationException>(() => builder.AssertContainsClass("#btn", "active"));
	}

	[Fact(DisplayName = "AssertCssProperty should throw InvalidOperationException when no page in context")]
	public void AssertCssProperty_Should_Throw_When_No_Page()
	{
		var (_, builder) = CreateBuilder(SuccessResult());

		Should.Throw<InvalidOperationException>(() => builder.AssertCssProperty("#header", "color", "red"));
	}

	[Fact(DisplayName = "AssertId should throw InvalidOperationException when no page in context")]
	public void AssertId_Should_Throw_When_No_Page()
	{
		var (_, builder) = CreateBuilder(SuccessResult());

		Should.Throw<InvalidOperationException>(() => builder.AssertId("#element", "my-id"));
	}

	[Fact(DisplayName = "AssertAccessibleName should throw InvalidOperationException when no page in context")]
	public void AssertAccessibleName_Should_Throw_When_No_Page()
	{
		var (_, builder) = CreateBuilder(SuccessResult());

		Should.Throw<InvalidOperationException>(() => builder.AssertAccessibleName("#btn", "Submit"));
	}

	[Fact(DisplayName = "AssertAccessibleDescription should throw InvalidOperationException when no page in context")]
	public void AssertAccessibleDescription_Should_Throw_When_No_Page()
	{
		var (_, builder) = CreateBuilder(SuccessResult());

		Should.Throw<InvalidOperationException>(() => builder.AssertAccessibleDescription("#btn", "Submits the form"));
	}

	[Fact(DisplayName = "AssertRole should throw InvalidOperationException when no page in context")]
	public void AssertRole_Should_Throw_When_No_Page()
	{
		var (_, builder) = CreateBuilder(SuccessResult());

		Should.Throw<InvalidOperationException>(() => builder.AssertRole("#element", AriaRole.Button));
	}

	[Fact(DisplayName = "AssertValues should throw InvalidOperationException when no page in context")]
	public void AssertValues_Should_Throw_When_No_Page()
	{
		var (_, builder) = CreateBuilder(SuccessResult());

		Should.Throw<InvalidOperationException>(() => builder.AssertValues("#select", "opt1", "opt2"));
	}

	[Fact(DisplayName = "AssertJSProperty should throw InvalidOperationException when no page in context")]
	public void AssertJSProperty_Should_Throw_When_No_Page()
	{
		var (_, builder) = CreateBuilder(SuccessResult());

		Should.Throw<InvalidOperationException>(() => builder.AssertJSProperty("#elem", "value", "hello"));
	}

	[Fact(DisplayName = "AssertTextMatches should throw InvalidOperationException when no page in context")]
	public void AssertTextMatches_Should_Throw_When_No_Page()
	{
		var (_, builder) = CreateBuilder(SuccessResult());

		Should.Throw<InvalidOperationException>(() => builder.AssertTextMatches("#elem", @"\d+"));
	}

	// ──────────────────────────────────────────────
	// Expect methods guard (no IPage in context)
	// ──────────────────────────────────────────────

	[Fact(DisplayName = "ExpectLocator with CSS should throw InvalidOperationException when no page in context")]
	public void ExpectLocator_Css_Should_Throw_When_No_Page()
	{
		var (_, builder) = CreateBuilder(SuccessResult());

		Should.Throw<InvalidOperationException>(() => builder.ExpectLocator("#elem"));
	}

	[Fact(DisplayName = "ExpectPage should throw InvalidOperationException when no page in context")]
	public void ExpectPage_Should_Throw_When_No_Page()
	{
		var (_, builder) = CreateBuilder(SuccessResult());

		Should.Throw<InvalidOperationException>(() => builder.ExpectPage());
	}

	[Fact(DisplayName = "ExpectVisible should throw InvalidOperationException when no page in context")]
	public void ExpectVisible_Should_Throw_When_No_Page()
	{
		var (_, builder) = CreateBuilder(SuccessResult());

		Should.Throw<InvalidOperationException>(() => builder.ExpectVisible("#elem"));
	}

	[Fact(DisplayName = "ExpectHidden should throw InvalidOperationException when no page in context")]
	public void ExpectHidden_Should_Throw_When_No_Page()
	{
		var (_, builder) = CreateBuilder(SuccessResult());

		Should.Throw<InvalidOperationException>(() => builder.ExpectHidden("#elem"));
	}

	[Fact(DisplayName = "ExpectEnabled should throw InvalidOperationException when no page in context")]
	public void ExpectEnabled_Should_Throw_When_No_Page()
	{
		var (_, builder) = CreateBuilder(SuccessResult());

		Should.Throw<InvalidOperationException>(() => builder.ExpectEnabled("#elem"));
	}

	[Fact(DisplayName = "ExpectDisabled should throw InvalidOperationException when no page in context")]
	public void ExpectDisabled_Should_Throw_When_No_Page()
	{
		var (_, builder) = CreateBuilder(SuccessResult());

		Should.Throw<InvalidOperationException>(() => builder.ExpectDisabled("#elem"));
	}

	[Fact(DisplayName = "ExpectChecked should throw InvalidOperationException when no page in context")]
	public void ExpectChecked_Should_Throw_When_No_Page()
	{
		var (_, builder) = CreateBuilder(SuccessResult());

		Should.Throw<InvalidOperationException>(() => builder.ExpectChecked("#elem"));
	}

	[Fact(DisplayName = "ExpectText should throw InvalidOperationException when no page in context")]
	public void ExpectText_Should_Throw_When_No_Page()
	{
		var (_, builder) = CreateBuilder(SuccessResult());

		Should.Throw<InvalidOperationException>(() => builder.ExpectText("#elem", "text"));
	}

	[Fact(DisplayName = "ExpectTitle should throw InvalidOperationException when no page in context")]
	public void ExpectTitle_Should_Throw_When_No_Page()
	{
		var (_, builder) = CreateBuilder(SuccessResult());

		Should.Throw<InvalidOperationException>(() => builder.ExpectTitle("Title"));
	}

	[Fact(DisplayName = "ExpectUrl should throw InvalidOperationException when no page in context")]
	public void ExpectUrl_Should_Throw_When_No_Page()
	{
		var (_, builder) = CreateBuilder(SuccessResult());

		Should.Throw<InvalidOperationException>(() => builder.ExpectUrl("https://myapp.com"));
	}

	[Fact(DisplayName = "ExpectNotVisible should throw InvalidOperationException when no page in context")]
	public void ExpectNotVisible_Should_Throw_When_No_Page()
	{
		var (_, builder) = CreateBuilder(SuccessResult());

		Should.Throw<InvalidOperationException>(() => builder.ExpectNotVisible("#elem"));
	}

	[Fact(DisplayName = "ExpectNotAttached should throw InvalidOperationException when no page in context")]
	public void ExpectNotAttached_Should_Throw_When_No_Page()
	{
		var (_, builder) = CreateBuilder(SuccessResult());

		Should.Throw<InvalidOperationException>(() => builder.ExpectNotAttached("#elem"));
	}

	[Fact(DisplayName = "ExpectNotChecked should throw InvalidOperationException when no page in context")]
	public void ExpectNotChecked_Should_Throw_When_No_Page()
	{
		var (_, builder) = CreateBuilder(SuccessResult());

		Should.Throw<InvalidOperationException>(() => builder.ExpectNotChecked("#elem"));
	}

	// ──────────────────────────────────────────────
	// By* variant guards (no IPage in context)
	// ──────────────────────────────────────────────

	[Fact(DisplayName = "AssertHiddenByTestId should throw InvalidOperationException when no page in context")]
	public void AssertHiddenByTestId_Should_Throw_When_No_Page()
	{
		var (_, builder) = CreateBuilder(SuccessResult());

		Should.Throw<InvalidOperationException>(() => builder.AssertHiddenByTestId("my-elem"));
	}

	[Fact(DisplayName = "AssertHiddenByRole should throw InvalidOperationException when no page in context")]
	public void AssertHiddenByRole_Should_Throw_When_No_Page()
	{
		var (_, builder) = CreateBuilder(SuccessResult());

		Should.Throw<InvalidOperationException>(() => builder.AssertHiddenByRole(AriaRole.Button));
	}

	[Fact(DisplayName = "AssertTextByRole should throw InvalidOperationException when no page in context")]
	public void AssertTextByRole_Should_Throw_When_No_Page()
	{
		var (_, builder) = CreateBuilder(SuccessResult());

		Should.Throw<InvalidOperationException>(() => builder.AssertTextByRole(AriaRole.Heading, "Title"));
	}

	[Fact(DisplayName = "AssertTextByTestId should throw InvalidOperationException when no page in context")]
	public void AssertTextByTestId_Should_Throw_When_No_Page()
	{
		var (_, builder) = CreateBuilder(SuccessResult());

		Should.Throw<InvalidOperationException>(() => builder.AssertTextByTestId("elem", "expected"));
	}

	[Fact(DisplayName = "AssertTextContainsByTestId should throw InvalidOperationException when no page in context")]
	public void AssertTextContainsByTestId_Should_Throw_When_No_Page()
	{
		var (_, builder) = CreateBuilder(SuccessResult());

		Should.Throw<InvalidOperationException>(() => builder.AssertTextContainsByTestId("elem", "partial"));
	}

	[Fact(DisplayName = "AssertDisabledByTestId should throw InvalidOperationException when no page in context")]
	public void AssertDisabledByTestId_Should_Throw_When_No_Page()
	{
		var (_, builder) = CreateBuilder(SuccessResult());

		Should.Throw<InvalidOperationException>(() => builder.AssertDisabledByTestId("elem"));
	}

	[Fact(DisplayName = "AssertDisabledByRole should throw InvalidOperationException when no page in context")]
	public void AssertDisabledByRole_Should_Throw_When_No_Page()
	{
		var (_, builder) = CreateBuilder(SuccessResult());

		Should.Throw<InvalidOperationException>(() => builder.AssertDisabledByRole(AriaRole.Button));
	}

	[Fact(DisplayName = "AssertDisabledByLabel should throw InvalidOperationException when no page in context")]
	public void AssertDisabledByLabel_Should_Throw_When_No_Page()
	{
		var (_, builder) = CreateBuilder(SuccessResult());

		Should.Throw<InvalidOperationException>(() => builder.AssertDisabledByLabel("Email"));
	}

	[Fact(DisplayName = "AssertEnabledByRole should throw InvalidOperationException when no page in context")]
	public void AssertEnabledByRole_Should_Throw_When_No_Page()
	{
		var (_, builder) = CreateBuilder(SuccessResult());

		Should.Throw<InvalidOperationException>(() => builder.AssertEnabledByRole(AriaRole.Button));
	}

	[Fact(DisplayName = "AssertEnabledByTestId should throw InvalidOperationException when no page in context")]
	public void AssertEnabledByTestId_Should_Throw_When_No_Page()
	{
		var (_, builder) = CreateBuilder(SuccessResult());

		Should.Throw<InvalidOperationException>(() => builder.AssertEnabledByTestId("elem"));
	}

	[Fact(DisplayName = "AssertEnabledByLabel should throw InvalidOperationException when no page in context")]
	public void AssertEnabledByLabel_Should_Throw_When_No_Page()
	{
		var (_, builder) = CreateBuilder(SuccessResult());

		Should.Throw<InvalidOperationException>(() => builder.AssertEnabledByLabel("Email"));
	}

	[Fact(DisplayName = "AssertCheckedByLabel should throw InvalidOperationException when no page in context")]
	public void AssertCheckedByLabel_Should_Throw_When_No_Page()
	{
		var (_, builder) = CreateBuilder(SuccessResult());

		Should.Throw<InvalidOperationException>(() => builder.AssertCheckedByLabel("Remember me"));
	}

	[Fact(DisplayName = "AssertCheckedByTestId should throw InvalidOperationException when no page in context")]
	public void AssertCheckedByTestId_Should_Throw_When_No_Page()
	{
		var (_, builder) = CreateBuilder(SuccessResult());

		Should.Throw<InvalidOperationException>(() => builder.AssertCheckedByTestId("checkbox"));
	}

	[Fact(DisplayName = "AssertCheckedByRole should throw InvalidOperationException when no page in context")]
	public void AssertCheckedByRole_Should_Throw_When_No_Page()
	{
		var (_, builder) = CreateBuilder(SuccessResult());

		Should.Throw<InvalidOperationException>(() => builder.AssertCheckedByRole(AriaRole.Checkbox));
	}

	[Fact(DisplayName = "AssertNotChecked should throw InvalidOperationException when no page in context")]
	public void AssertNotChecked_Should_Throw_When_No_Page()
	{
		var (_, builder) = CreateBuilder(SuccessResult());

		Should.Throw<InvalidOperationException>(() => builder.AssertNotChecked("#checkbox"));
	}

	[Fact(DisplayName = "AssertNotCheckedByLabel should throw InvalidOperationException when no page in context")]
	public void AssertNotCheckedByLabel_Should_Throw_When_No_Page()
	{
		var (_, builder) = CreateBuilder(SuccessResult());

		Should.Throw<InvalidOperationException>(() => builder.AssertNotCheckedByLabel("Remember me"));
	}

	[Fact(DisplayName = "AssertNotCheckedByTestId should throw InvalidOperationException when no page in context")]
	public void AssertNotCheckedByTestId_Should_Throw_When_No_Page()
	{
		var (_, builder) = CreateBuilder(SuccessResult());

		Should.Throw<InvalidOperationException>(() => builder.AssertNotCheckedByTestId("checkbox"));
	}

	[Fact(DisplayName = "AssertNotCheckedByRole should throw InvalidOperationException when no page in context")]
	public void AssertNotCheckedByRole_Should_Throw_When_No_Page()
	{
		var (_, builder) = CreateBuilder(SuccessResult());

		Should.Throw<InvalidOperationException>(() => builder.AssertNotCheckedByRole(AriaRole.Checkbox));
	}

	[Fact(DisplayName = "AssertVisibleByText should throw InvalidOperationException when no page in context")]
	public void AssertVisibleByText_Should_Throw_When_No_Page()
	{
		var (_, builder) = CreateBuilder(SuccessResult());

		Should.Throw<InvalidOperationException>(() => builder.AssertVisibleByText("Click me"));
	}

	[Fact(DisplayName = "AssertVisibleByAltText should throw InvalidOperationException when no page in context")]
	public void AssertVisibleByAltText_Should_Throw_When_No_Page()
	{
		var (_, builder) = CreateBuilder(SuccessResult());

		Should.Throw<InvalidOperationException>(() => builder.AssertVisibleByAltText("Logo"));
	}

	[Fact(DisplayName = "AssertEditableByTestId should throw InvalidOperationException when no page in context")]
	public void AssertEditableByTestId_Should_Throw_When_No_Page()
	{
		var (_, builder) = CreateBuilder(SuccessResult());

		Should.Throw<InvalidOperationException>(() => builder.AssertEditableByTestId("input"));
	}

	[Fact(DisplayName = "AssertNotEditable should throw InvalidOperationException when no page in context")]
	public void AssertNotEditable_Should_Throw_When_No_Page()
	{
		var (_, builder) = CreateBuilder(SuccessResult());

		Should.Throw<InvalidOperationException>(() => builder.AssertNotEditable("#input"));
	}

	[Fact(DisplayName = "AssertNotEditableByTestId should throw InvalidOperationException when no page in context")]
	public void AssertNotEditableByTestId_Should_Throw_When_No_Page()
	{
		var (_, builder) = CreateBuilder(SuccessResult());

		Should.Throw<InvalidOperationException>(() => builder.AssertNotEditableByTestId("input"));
	}

	[Fact(DisplayName = "AssertEmptyByTestId should throw InvalidOperationException when no page in context")]
	public void AssertEmptyByTestId_Should_Throw_When_No_Page()
	{
		var (_, builder) = CreateBuilder(SuccessResult());

		Should.Throw<InvalidOperationException>(() => builder.AssertEmptyByTestId("container"));
	}

	[Fact(DisplayName = "AssertNotEmpty should throw InvalidOperationException when no page in context")]
	public void AssertNotEmpty_Should_Throw_When_No_Page()
	{
		var (_, builder) = CreateBuilder(SuccessResult());

		Should.Throw<InvalidOperationException>(() => builder.AssertNotEmpty("#container"));
	}

	[Fact(DisplayName = "AssertNotEmptyByTestId should throw InvalidOperationException when no page in context")]
	public void AssertNotEmptyByTestId_Should_Throw_When_No_Page()
	{
		var (_, builder) = CreateBuilder(SuccessResult());

		Should.Throw<InvalidOperationException>(() => builder.AssertNotEmptyByTestId("container"));
	}

	[Fact(DisplayName = "AssertFocusedByTestId should throw InvalidOperationException when no page in context")]
	public void AssertFocusedByTestId_Should_Throw_When_No_Page()
	{
		var (_, builder) = CreateBuilder(SuccessResult());

		Should.Throw<InvalidOperationException>(() => builder.AssertFocusedByTestId("input"));
	}

	[Fact(DisplayName = "AssertNotFocused should throw InvalidOperationException when no page in context")]
	public void AssertNotFocused_Should_Throw_When_No_Page()
	{
		var (_, builder) = CreateBuilder(SuccessResult());

		Should.Throw<InvalidOperationException>(() => builder.AssertNotFocused("#input"));
	}

	[Fact(DisplayName = "AssertAttachedByTestId should throw InvalidOperationException when no page in context")]
	public void AssertAttachedByTestId_Should_Throw_When_No_Page()
	{
		var (_, builder) = CreateBuilder(SuccessResult());

		Should.Throw<InvalidOperationException>(() => builder.AssertAttachedByTestId("elem"));
	}

	[Fact(DisplayName = "AssertNotAttached should throw InvalidOperationException when no page in context")]
	public void AssertNotAttached_Should_Throw_When_No_Page()
	{
		var (_, builder) = CreateBuilder(SuccessResult());

		Should.Throw<InvalidOperationException>(() => builder.AssertNotAttached("#elem"));
	}

	[Fact(DisplayName = "AssertInViewportByTestId should throw InvalidOperationException when no page in context")]
	public void AssertInViewportByTestId_Should_Throw_When_No_Page()
	{
		var (_, builder) = CreateBuilder(SuccessResult());

		Should.Throw<InvalidOperationException>(() => builder.AssertInViewportByTestId("elem"));
	}

	[Fact(DisplayName = "AssertElementCountByRole should throw InvalidOperationException when no page in context")]
	public void AssertElementCountByRole_Should_Throw_When_No_Page()
	{
		var (_, builder) = CreateBuilder(SuccessResult());

		Should.Throw<InvalidOperationException>(() => builder.AssertElementCountByRole(AriaRole.Listitem, 3));
	}

	[Fact(DisplayName = "AssertAttributeByTestId should throw InvalidOperationException when no page in context")]
	public void AssertAttributeByTestId_Should_Throw_When_No_Page()
	{
		var (_, builder) = CreateBuilder(SuccessResult());

		Should.Throw<InvalidOperationException>(() => builder.AssertAttributeByTestId("input", "placeholder", "Email"));
	}

	[Fact(DisplayName = "AssertInputValueByLabel should throw InvalidOperationException when no page in context")]
	public void AssertInputValueByLabel_Should_Throw_When_No_Page()
	{
		var (_, builder) = CreateBuilder(SuccessResult());

		Should.Throw<InvalidOperationException>(() => builder.AssertInputValueByLabel("Email", "test@test.com"));
	}

	[Fact(DisplayName = "AssertInputValueByTestId should throw InvalidOperationException when no page in context")]
	public void AssertInputValueByTestId_Should_Throw_When_No_Page()
	{
		var (_, builder) = CreateBuilder(SuccessResult());

		Should.Throw<InvalidOperationException>(() => builder.AssertInputValueByTestId("email", "test@test.com"));
	}

	[Fact(DisplayName = "TakeScreenshot should throw InvalidOperationException when no page in context")]
	public void TakeScreenshot_Should_Throw_When_No_Page()
	{
		var (_, builder) = CreateBuilder(SuccessResult());

		Should.Throw<InvalidOperationException>(() => builder.TakeScreenshot("test.png"));
	}

	[Fact(DisplayName = "AssertClassByTestId should throw InvalidOperationException when no page in context")]
	public void AssertClassByTestId_Should_Throw_When_No_Page()
	{
		var (_, builder) = CreateBuilder(SuccessResult());

		Should.Throw<InvalidOperationException>(() => builder.AssertClassByTestId("btn", "active"));
	}

	[Fact(DisplayName = "AssertClassByRole should throw InvalidOperationException when no page in context")]
	public void AssertClassByRole_Should_Throw_When_No_Page()
	{
		var (_, builder) = CreateBuilder(SuccessResult());

		Should.Throw<InvalidOperationException>(() => builder.AssertClassByRole(AriaRole.Button, "active"));
	}

	[Fact(DisplayName = "AssertContainsClassByTestId should throw InvalidOperationException when no page in context")]
	public void AssertContainsClassByTestId_Should_Throw_When_No_Page()
	{
		var (_, builder) = CreateBuilder(SuccessResult());

		Should.Throw<InvalidOperationException>(() => builder.AssertContainsClassByTestId("btn", "active"));
	}

	[Fact(DisplayName = "AssertContainsClassByRole should throw InvalidOperationException when no page in context")]
	public void AssertContainsClassByRole_Should_Throw_When_No_Page()
	{
		var (_, builder) = CreateBuilder(SuccessResult());

		Should.Throw<InvalidOperationException>(() => builder.AssertContainsClassByRole(AriaRole.Button, "active"));
	}

	[Fact(DisplayName = "AssertCssPropertyByTestId should throw InvalidOperationException when no page in context")]
	public void AssertCssPropertyByTestId_Should_Throw_When_No_Page()
	{
		var (_, builder) = CreateBuilder(SuccessResult());

		Should.Throw<InvalidOperationException>(() => builder.AssertCssPropertyByTestId("header", "color", "red"));
	}

	[Fact(DisplayName = "AssertCssPropertyByRole should throw InvalidOperationException when no page in context")]
	public void AssertCssPropertyByRole_Should_Throw_When_No_Page()
	{
		var (_, builder) = CreateBuilder(SuccessResult());

		Should.Throw<InvalidOperationException>(() => builder.AssertCssPropertyByRole(AriaRole.Button, "color", "red"));
	}

	[Fact(DisplayName = "AssertIdByRole should throw InvalidOperationException when no page in context")]
	public void AssertIdByRole_Should_Throw_When_No_Page()
	{
		var (_, builder) = CreateBuilder(SuccessResult());

		Should.Throw<InvalidOperationException>(() => builder.AssertIdByRole(AriaRole.Button, "submit-btn"));
	}

	[Fact(DisplayName = "AssertIdByTestId should throw InvalidOperationException when no page in context")]
	public void AssertIdByTestId_Should_Throw_When_No_Page()
	{
		var (_, builder) = CreateBuilder(SuccessResult());

		Should.Throw<InvalidOperationException>(() => builder.AssertIdByTestId("elem", "my-id"));
	}

	[Fact(DisplayName = "AssertAccessibleNameByTestId should throw InvalidOperationException when no page in context")]
	public void AssertAccessibleNameByTestId_Should_Throw_When_No_Page()
	{
		var (_, builder) = CreateBuilder(SuccessResult());

		Should.Throw<InvalidOperationException>(() => builder.AssertAccessibleNameByTestId("btn", "Submit"));
	}

	[Fact(DisplayName = "AssertAccessibleNameByRole should throw InvalidOperationException when no page in context")]
	public void AssertAccessibleNameByRole_Should_Throw_When_No_Page()
	{
		var (_, builder) = CreateBuilder(SuccessResult());

		Should.Throw<InvalidOperationException>(() => builder.AssertAccessibleNameByRole(AriaRole.Button, "Submit"));
	}

	[Fact(DisplayName = "AssertAccessibleDescriptionByTestId should throw InvalidOperationException when no page in context")]
	public void AssertAccessibleDescriptionByTestId_Should_Throw_When_No_Page()
	{
		var (_, builder) = CreateBuilder(SuccessResult());

		Should.Throw<InvalidOperationException>(() => builder.AssertAccessibleDescriptionByTestId("btn", "Submits the form"));
	}

	[Fact(DisplayName = "AssertAccessibleDescriptionByRole should throw InvalidOperationException when no page in context")]
	public void AssertAccessibleDescriptionByRole_Should_Throw_When_No_Page()
	{
		var (_, builder) = CreateBuilder(SuccessResult());

		Should.Throw<InvalidOperationException>(() => builder.AssertAccessibleDescriptionByRole(AriaRole.Button, "Submits the form"));
	}

	[Fact(DisplayName = "AssertRoleByTestId should throw InvalidOperationException when no page in context")]
	public void AssertRoleByTestId_Should_Throw_When_No_Page()
	{
		var (_, builder) = CreateBuilder(SuccessResult());

		Should.Throw<InvalidOperationException>(() => builder.AssertRoleByTestId("elem", AriaRole.Button));
	}

	[Fact(DisplayName = "AssertValuesByTestId should throw InvalidOperationException when no page in context")]
	public void AssertValuesByTestId_Should_Throw_When_No_Page()
	{
		var (_, builder) = CreateBuilder(SuccessResult());

		Should.Throw<InvalidOperationException>(() => builder.AssertValuesByTestId("select", "opt1"));
	}

	[Fact(DisplayName = "AssertValuesByLabel should throw InvalidOperationException when no page in context")]
	public void AssertValuesByLabel_Should_Throw_When_No_Page()
	{
		var (_, builder) = CreateBuilder(SuccessResult());

		Should.Throw<InvalidOperationException>(() => builder.AssertValuesByLabel("Country", false, "BR"));
	}

	[Fact(DisplayName = "AssertJSPropertyByTestId should throw InvalidOperationException when no page in context")]
	public void AssertJSPropertyByTestId_Should_Throw_When_No_Page()
	{
		var (_, builder) = CreateBuilder(SuccessResult());

		Should.Throw<InvalidOperationException>(() => builder.AssertJSPropertyByTestId("elem", "value", "hello"));
	}

	[Fact(DisplayName = "AssertTextMatchesByTestId should throw InvalidOperationException when no page in context")]
	public void AssertTextMatchesByTestId_Should_Throw_When_No_Page()
	{
		var (_, builder) = CreateBuilder(SuccessResult());

		Should.Throw<InvalidOperationException>(() => builder.AssertTextMatchesByTestId("elem", @"\d+"));
	}

	[Fact(DisplayName = "AssertTextMatchesByRole should throw InvalidOperationException when no page in context")]
	public void AssertTextMatchesByRole_Should_Throw_When_No_Page()
	{
		var (_, builder) = CreateBuilder(SuccessResult());

		Should.Throw<InvalidOperationException>(() => builder.AssertTextMatchesByRole(AriaRole.Heading, @"\d+"));
	}

	// ──────────────────────────────────────────────
	// Mock step for testing
	// ──────────────────────────────────────────────

	private class MockPlaywrightStep : ITestStep
	{
		public string? Name { get; set; }
		public string StepType => "Playwright";
		public ITestStepResult? Result { get; private set; }
		public bool IsExecuted => Result != null;
		public bool IsValid { get; private set; }

		public MockPlaywrightStep(PlaywrightStepResult result)
		{
			Result = result;
		}

		public Task<ITestStepResult> ExecuteAsync(ITestContext context)
		{
			return Task.FromResult<ITestStepResult>(Result!);
		}

		public void Validate(Action<ITestStepResult> validation)
		{
			if (Result == null)
				throw new InvalidOperationException("Step must be executed before validation.");

			validation(Result);
			IsValid = true;
		}
	}
}
