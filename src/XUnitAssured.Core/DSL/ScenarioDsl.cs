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

	/// <summary>
	/// Starts a new test scenario with an HttpClient provider (e.g., test fixture).
	/// Automatically configures the scenario to use the HttpClient from the provider.
	/// </summary>
	/// <param name="httpClientProvider">Provider that supplies HttpClient instances (e.g., WebApplicationFactory fixture)</param>
	/// <returns>A new test scenario pre-configured with the HttpClient</returns>
	/// <example>
	/// <code>
	/// // Traditional way:
	/// Given()
	///     .WithHttpClient(_fixture.CreateClient())
	///     .ApiResource("/api/products")
	///     .Get()
	/// 
	/// // Simplified way with this overload:
	/// Given(_fixture)
	///     .ApiResource("/api/products")
	///     .Get()
	/// </code>
	/// </example>
	public static ITestScenario Given(IHttpClientProvider httpClientProvider)
	{
		if (httpClientProvider == null)
			throw new System.ArgumentNullException(nameof(httpClientProvider));

		var scenario = new TestScenario();
		
		// Store the HttpClient provider in the context for later use
		scenario.Context.SetProperty("HttpClientProvider", httpClientProvider);
		
		return scenario;
	}
}
