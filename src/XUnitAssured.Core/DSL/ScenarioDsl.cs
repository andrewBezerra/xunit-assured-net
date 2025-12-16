using XUnitAssured.Core.Abstractions;

namespace XUnitAssured.Core.DSL;

/// <summary>
/// Static entry point for the fluent test DSL.
/// Provides the Given() method to start test scenarios.
/// </summary>
public static class ScenarioDsl
{
	/// <summary>
	/// Starts a new test scenario.
	/// This is the entry point for the fluent DSL.
	/// Usage: Given().ApiResource(...).Post(...).Validate(...)
	/// </summary>
	/// <returns>A new test scenario</returns>
	public static ITestScenario Given()
	{
		return new TestScenario();
	}

	/// <summary>
	/// Starts a new test scenario with a custom context.
	/// </summary>
	/// <param name="context">Custom test context</param>
	/// <returns>A new test scenario with the specified context</returns>
	public static ITestScenario Given(ITestContext context)
	{
		return new TestScenario(context);
	}
}
