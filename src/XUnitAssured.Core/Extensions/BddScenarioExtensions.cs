using System;
using XUnitAssured.Core.Abstractions;
using XUnitAssured.Core.Results;

namespace XUnitAssured.Core.Extensions;

/// <summary>
/// BDD-style extension methods for ITestScenario.
/// Provides convenient Execute() method that bridges async execution to synchronous fluent API.
/// </summary>
public static class BddScenarioExtensions
{
	/// <summary>
	/// Executes the current step synchronously and returns a generic validation builder.
	/// This method bridges the async step execution with the fluent validation API.
	/// </summary>
	/// <typeparam name="TResult">The type of ITestStepResult to validate (e.g., HttpStepResult, KafkaStepResult)</typeparam>
	/// <param name="scenario">The test scenario containing the step to execute</param>
	/// <returns>A ValidationBuilder for fluent validation/assertion chains</returns>
	/// <exception cref="InvalidOperationException">Thrown when no step exists or step hasn't been executed</exception>
	/// <example>
	/// <code>
	/// Given()
	///     .ApiResource("/api/products")
	///     .Get()
	/// .When()
	///     .Execute&lt;HttpStepResult&gt;()
	/// .Then()
	///     .AssertSuccess();
	/// </code>
	/// </example>
	public static ValidationBuilder<TResult> Execute<TResult>(this ITestScenario scenario) 
		where TResult : class, ITestStepResult
	{
		// Execute the step asynchronously and block until completion
		scenario.ExecuteCurrentStepAsync().GetAwaiter().GetResult();
		
		// Return a validation builder for the result
		return new ValidationBuilder<TResult>(scenario);
	}
}
