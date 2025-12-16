using System;
using XUnitAssured.Core.Abstractions;
using XUnitAssured.Core.Results;
using XUnitAssured.Http.Results;

namespace XUnitAssured.Http.Extensions;

/// <summary>
/// Extension methods for validating HTTP responses in the fluent DSL.
/// </summary>
public static class HttpValidationExtensions
{
	/// <summary>
	/// Validates the HTTP response using a custom validation function.
	/// The validation function receives an HttpStepResult with typed access to HTTP-specific properties.
	/// Usage: .Validate(response => response.StatusCode.ShouldBe(200))
	/// </summary>
	public static ITestScenario Validate(this ITestScenario scenario, Action<HttpStepResult> validation)
	{
		if (scenario == null)
			throw new ArgumentNullException(nameof(scenario));

		if (validation == null)
			throw new ArgumentNullException(nameof(validation));

		// Execute current step if not already executed
		scenario.ExecuteCurrentStepAsync().GetAwaiter().GetResult();

		if (scenario.CurrentStep == null)
			throw new InvalidOperationException("No step to validate. Create a step first.");

		// Validate with the generic ITestStepResult, then cast to HttpStepResult
		scenario.CurrentStep.Validate(result =>
		{
			if (result is not HttpStepResult httpResult)
				throw new InvalidOperationException($"Expected HttpStepResult but got {result.GetType().Name}");

			validation(httpResult);
		});

		return scenario;
	}

	/// <summary>
	/// Validates the HTTP response using a generic validation function.
	/// The validation function receives the base ITestStepResult.
	/// </summary>
	public static ITestScenario Validate(this ITestScenario scenario, Action<ITestStepResult> validation)
	{
		if (scenario == null)
			throw new ArgumentNullException(nameof(scenario));

		if (validation == null)
			throw new ArgumentNullException(nameof(validation));

		// Execute current step if not already executed
		scenario.ExecuteCurrentStepAsync().GetAwaiter().GetResult();

		if (scenario.CurrentStep == null)
			throw new InvalidOperationException("No step to validate. Create a step first.");

		scenario.CurrentStep.Validate(validation);

		return scenario;
	}
}
