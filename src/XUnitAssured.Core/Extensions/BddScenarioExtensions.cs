using System.Threading.Tasks;
using System.Threading;
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
	public static async Task<ValidationBuilder<TResult>> ExecuteAsync<TResult>(
		this ITestScenario scenario,
		CancellationToken cancellationToken = default)
		where TResult : class, ITestStepResult
	{
		await scenario.ExecutePendingAsync(cancellationToken).ConfigureAwait(false);

		return new ValidationBuilder<TResult>(scenario);
	}

	/// <summary>
	/// Runs the chain and waits for it.
	/// </summary>
	/// <remarks>
	/// A thin wrapper over <see cref="ExecuteAsync{TResult}"/>, so that a suite written
	/// against the synchronous DSL keeps working. It blocks the calling thread, which in a
	/// test runner is the thread that would be waiting anyway; xUnit sets no synchronization
	/// context, so the usual deadlock does not apply.
	/// </remarks>
	public static ValidationBuilder<TResult> Execute<TResult>(this ITestScenario scenario)
		where TResult : class, ITestStepResult
		=> scenario.ExecuteAsync<TResult>().GetAwaiter().GetResult();
}
