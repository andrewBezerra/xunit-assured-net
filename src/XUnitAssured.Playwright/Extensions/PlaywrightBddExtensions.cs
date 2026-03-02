using XUnitAssured.Core.Abstractions;

namespace XUnitAssured.Playwright.Extensions;

/// <summary>
/// Playwright-specific BDD extension methods for ITestScenario.
/// Provides convenient Execute() method that returns PlaywrightValidationBuilder for UI testing scenarios.
/// </summary>
public static class PlaywrightBddExtensions
{
	/// <summary>
	/// Executes the Playwright step synchronously and returns a Playwright-specific validation builder.
	/// This is a convenience method that automatically casts to PlaywrightStepResult.
	/// </summary>
	/// <param name="scenario">The test scenario containing the Playwright step to execute</param>
	/// <returns>A PlaywrightValidationBuilder for fluent UI-specific validation/assertion chains</returns>
	/// <example>
	/// <code>
	/// Given()
	///     .NavigateTo("/login")
	///     .FillByLabel("Email", "user@test.com")
	///     .FillByPlaceholder("Password", "secret")
	///     .ClickByRole(AriaRole.Button, "Sign In")
	/// .When()
	///     .Execute()
	/// .Then()
	///     .AssertUrl("/dashboard")
	///     .AssertVisibleByTestId("welcome-banner");
	/// </code>
	/// </example>
	public static PlaywrightValidationBuilder Execute(this ITestScenario scenario)
	{
		// Execute the Playwright step asynchronously and block until completion
		scenario.ExecuteCurrentStepAsync().GetAwaiter().GetResult();

		// Return a Playwright-specific validation builder
		return new PlaywrightValidationBuilder(scenario);
	}
}
