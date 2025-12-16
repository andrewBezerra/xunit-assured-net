using System;
using XUnitAssured.Core.Abstractions;
using XUnitAssured.Kafka.Results;

namespace XUnitAssured.Kafka.Extensions;

/// <summary>
/// Extension methods for validating Kafka messages in the fluent DSL.
/// </summary>
public static class KafkaValidationExtensions
{
	/// <summary>
	/// Validates the Kafka message using a custom validation function.
	/// The validation function receives a KafkaStepResult with typed access to Kafka-specific properties.
	/// Usage: .Validate(message => message.Topic.ShouldBe("my-topic"))
	/// </summary>
	public static ITestScenario Validate(this ITestScenario scenario, Action<KafkaStepResult> validation)
	{
		if (scenario == null)
			throw new ArgumentNullException(nameof(scenario));

		if (validation == null)
			throw new ArgumentNullException(nameof(validation));

		// Execute current step if not already executed
		scenario.ExecuteCurrentStepAsync().GetAwaiter().GetResult();

		if (scenario.CurrentStep == null)
			throw new InvalidOperationException("No step to validate. Create a step first.");

		// Validate with the generic ITestStepResult, then cast to KafkaStepResult
		scenario.CurrentStep.Validate(result =>
		{
			if (result is not KafkaStepResult kafkaResult)
				throw new InvalidOperationException($"Expected KafkaStepResult but got {result.GetType().Name}");

			validation(kafkaResult);
		});

		return scenario;
	}

	/// <summary>
	/// Validates the consumed message value with a typed validation function.
	/// Automatically deserializes the message to the specified type.
	/// Usage: .ValidateMessage&lt;MyMessage&gt;(msg => msg.Id.ShouldNotBeEmpty())
	/// </summary>
	public static ITestScenario ValidateMessage<T>(this ITestScenario scenario, Action<T> validation) where T : class
	{
		if (scenario == null)
			throw new ArgumentNullException(nameof(scenario));

		if (validation == null)
			throw new ArgumentNullException(nameof(validation));

		// Execute current step if not already executed
		scenario.ExecuteCurrentStepAsync().GetAwaiter().GetResult();

		if (scenario.CurrentStep == null)
			throw new InvalidOperationException("No step to validate. Create a step first.");

		// Validate with typed message
		scenario.CurrentStep.Validate(result =>
		{
			if (result is not KafkaStepResult kafkaResult)
				throw new InvalidOperationException($"Expected KafkaStepResult but got {result.GetType().Name}");

			var message = kafkaResult.GetMessage<T>();
			if (message == null)
				throw new InvalidOperationException($"Failed to deserialize message to type {typeof(T).Name}");

			validation(message);
		});

		return scenario;
	}
}
