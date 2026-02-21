using System;
using XUnitAssured.Core.Abstractions;
using XUnitAssured.Kafka.Results;

namespace XUnitAssured.Extensions.Kafka;

/// <summary>
/// BDD-style extensions for Kafka test scenarios.
/// Provides fluent transition from "When" (action) to "Then" (assertions).
/// </summary>
public static class KafkaBddExtensions
{
	/// <summary>
	/// Transitions from "When" (action) to "Then" (assertions) for Kafka test scenarios.
	/// Returns a KafkaValidationBuilder for fluent Kafka-specific assertions.
	/// </summary>
	/// <param name="scenario">The test scenario containing the executed Kafka step</param>
	/// <returns>A KafkaValidationBuilder for method chaining</returns>
	/// <exception cref="ArgumentNullException">Thrown when scenario is null</exception>
	/// <exception cref="InvalidOperationException">Thrown when no step is configured or result is not a KafkaStepResult</exception>
	/// <example>
	/// <code>
	/// Given()
	///     .KafkaProducer("my-topic")
	/// .When()
	///     .Produce(myMessage)
	/// .Then()  // Returns KafkaValidationBuilder
	///     .AssertSuccess()
	///     .AssertTopic("my-topic")
	///     .AssertJsonPath&lt;int&gt;("$.userId", id => id.ShouldBeGreaterThan(0));
	/// </code>
	/// </example>
	public static KafkaValidationBuilder Then(this ITestScenario scenario)
	{
		if (scenario == null)
			throw new ArgumentNullException(nameof(scenario));

		// Execute current step if not already executed
		scenario.ExecuteCurrentStepAsync().GetAwaiter().GetResult();

		if (scenario.CurrentStep == null)
			throw new InvalidOperationException("No step to validate. Create a step first.");

		// Verify that the result is a KafkaStepResult
		if (scenario.CurrentStep.Result is not KafkaStepResult)
		{
			var resultType = scenario.CurrentStep.Result?.GetType().Name ?? "null";
			throw new InvalidOperationException(
				$"Expected KafkaStepResult but got {resultType}. " +
				"Ensure you're using Kafka operations (Produce/Consume) before calling Then().");
		}

		return new KafkaValidationBuilder(scenario);
	}
}
