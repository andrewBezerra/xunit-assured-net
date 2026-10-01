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
	/// Runs everything the chain has described, in order, and hands back the assertions.
	/// A chain only describes until this point, so this is where the I/O happens.
	/// </summary>
	/// <typeparam name="TResult">The type of ITestStepResult to validate (e.g., HttpStepResult, KafkaStepResult)</typeparam>
	/// <param name="scenario">The test scenario holding the steps to run</param>
	/// <param name="cancellationToken">Stops the run between steps and reaches the step itself</param>
	/// <returns>A ValidationBuilder for fluent validation/assertion chains</returns>
	/// <exception cref="InvalidOperationException">Thrown when there is no step to run</exception>
	/// <example>
	/// <code>
	/// var assertions = await Given()
	///     .ApiResource("/api/products")
	///     .Get()
	/// .When()
	///     .ExecuteAsync&lt;HttpStepResult&gt;();
	///
	/// assertions.Then().AssertSuccess();
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
	/// <typeparam name="TResult">The type of ITestStepResult to validate</typeparam>
	/// <param name="scenario">The test scenario holding the steps to run</param>
	/// <returns>A ValidationBuilder for fluent validation/assertion chains</returns>
	public static ValidationBuilder<TResult> Execute<TResult>(this ITestScenario scenario)
		where TResult : class, ITestStepResult
		=> scenario.ExecuteAsync<TResult>().GetAwaiter().GetResult();
}
