using System;
using Shouldly;
using XUnitAssured.Core.Abstractions;
using XUnitAssured.Core.Results;

namespace XUnitAssured.Core.Extensions;

/// <summary>
/// Generic validation builder for fluent test assertions.
/// This is the base class that provides common validation patterns across all result types.
/// Technology-specific builders (HttpValidationBuilder, KafkaValidationBuilder) inherit from this.
/// </summary>
/// <typeparam name="TResult">The type of ITestStepResult being validated</typeparam>
public class ValidationBuilder<TResult> where TResult : class, ITestStepResult
{
	private readonly ITestScenario _scenario;
	private TResult? _result;

	/// <summary>
	/// Initializes a new instance of the ValidationBuilder.
	/// </summary>
	/// <param name="scenario">The test scenario containing the executed step</param>
	public ValidationBuilder(ITestScenario scenario)
	{
		_scenario = scenario ?? throw new ArgumentNullException(nameof(scenario));
	}

	/// <summary>
	/// Gets the step result. Lazily loaded and cached.
	/// </summary>
	/// <exception cref="InvalidOperationException">Thrown when step hasn't been executed or result type doesn't match</exception>
	protected TResult Result
	{
		get
		{
			if (_result == null)
			{
				var step = _scenario.CurrentStep 
					?? throw new InvalidOperationException("No step to validate. Ensure a step has been configured in the scenario.");
				
				var stepResult = step.Result 
					?? throw new InvalidOperationException("Step has not been executed. Call Execute() before validating.");
				
				_result = stepResult as TResult 
					?? throw new InvalidOperationException(
						$"Expected result type {typeof(TResult).Name} but got {stepResult.GetType().Name}. " +
						$"Ensure you're using the correct Execute<T>() method for your step type.");
			}
			return _result;
		}
	}

	/// <summary>
	/// Marks the transition from "When" (action) to "Then" (assertions) in BDD-style tests.
	/// This is a pass-through method for readability in the fluent DSL.
	/// </summary>
	/// <returns>The same validation builder for method chaining</returns>
	/// <example>
	/// <code>
	/// .When()
	///     .Execute&lt;HttpStepResult&gt;()
	/// .Then()
	///     .AssertSuccess();
	/// </code>
	/// </example>
	public ValidationBuilder<TResult> Then()
	{
		// Pass-through for BDD readability
		return this;
	}

	/// <summary>
	/// Asserts that the step execution was successful.
	/// </summary>
	/// <returns>The same validation builder for method chaining</returns>
	/// <exception cref="Shouldly.ShouldAssertException">Thrown when the assertion fails</exception>
	public ValidationBuilder<TResult> AssertSuccess()
	{
		Result.Success.ShouldBeTrue(
			$"Expected step to succeed but it failed with errors: {string.Join(", ", Result.Errors)}");
		return this;
	}

	/// <summary>
	/// Asserts that the step execution failed.
	/// </summary>
	/// <returns>The same validation builder for method chaining</returns>
	/// <exception cref="Shouldly.ShouldAssertException">Thrown when the assertion fails</exception>
	public ValidationBuilder<TResult> AssertFailure()
	{
		Result.Success.ShouldBeFalse(
			"Expected step to fail but it succeeded");
		return this;
	}

	/// <summary>
	/// Asserts that the step has specific error messages.
	/// </summary>
	/// <param name="errorAssertion">Action to assert on the error collection</param>
	/// <returns>The same validation builder for method chaining</returns>
	public ValidationBuilder<TResult> AssertErrors(Action<ITestStepResult> errorAssertion)
	{
		if (errorAssertion == null)
			throw new ArgumentNullException(nameof(errorAssertion));
			
		errorAssertion(Result);
		return this;
	}

	/// <summary>
	/// Asserts a property value from the result's Properties dictionary.
	/// </summary>
	/// <typeparam name="TProperty">The expected type of the property</typeparam>
	/// <param name="propertyKey">The property key to extract</param>
	/// <param name="assertion">Action to assert on the property value</param>
	/// <returns>The same validation builder for method chaining</returns>
	public ValidationBuilder<TResult> AssertProperty<TProperty>(string propertyKey, Action<TProperty?> assertion)
	{
		if (string.IsNullOrWhiteSpace(propertyKey))
			throw new ArgumentException("Property key cannot be null or empty", nameof(propertyKey));
		if (assertion == null)
			throw new ArgumentNullException(nameof(assertion));

		var propertyValue = Result.GetProperty<TProperty>(propertyKey);
		assertion(propertyValue);
		return this;
	}

	/// <summary>
	/// Performs custom validation logic on the result.
	/// This is an escape hatch for complex validation scenarios.
	/// </summary>
	/// <param name="validation">Custom validation action</param>
	/// <returns>The same validation builder for method chaining</returns>
	public ValidationBuilder<TResult> Validate(Action<TResult> validation)
	{
		if (validation == null)
			throw new ArgumentNullException(nameof(validation));
			
		validation(Result);
		return this;
	}

	/// <summary>
	/// Gets the underlying result for advanced scenarios.
	/// Use this when you need direct access to the result beyond the fluent API.
	/// </summary>
	/// <returns>The test step result</returns>
	public TResult GetResult()
	{
		return Result;
	}
}
