using XUnitAssured.Core.Abstractions;
using XUnitAssured.Http.Results;

namespace XUnitAssured.Extensions.Http;

/// <summary>
/// HTTP-specific BDD extension methods for ITestScenario.
/// Provides convenient Execute() method that returns HttpValidationBuilder for HTTP testing scenarios.
/// </summary>
public static class HttpBddExtensions
{
	/// <summary>
	/// Executes the HTTP step synchronously and returns an HTTP-specific validation builder.
	/// This is a convenience method that automatically casts to HttpStepResult.
	/// </summary>
	/// <param name="scenario">The test scenario containing the HTTP step to execute</param>
	/// <returns>An HttpValidationBuilder for fluent HTTP-specific validation/assertion chains</returns>
	/// <exception cref="System.InvalidOperationException">Thrown when no step exists, step hasn't been executed, or result is not HttpStepResult</exception>
	/// <example>
	/// <code>
	/// Given()
	///     .ApiResource("/api/products")
	///     .Get()
	/// .When()
	///     .Execute()
	/// .Then()
	///     .AssertStatusCode(200)
	///     .ValidateContract&lt;Product&gt;();
	/// </code>
	/// </example>
	public static HttpValidationBuilder Execute(this ITestScenario scenario)
	{
		// Execute the HTTP step asynchronously and block until completion
		scenario.ExecuteCurrentStepAsync().GetAwaiter().GetResult();
		
		// Return an HTTP-specific validation builder
		return new HttpValidationBuilder(scenario);
	}
}
