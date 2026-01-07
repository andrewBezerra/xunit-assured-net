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

	// ========== PRODUCE VALIDATIONS ==========

	/// <summary>
	/// Validates that the Kafka produce operation was successful.
	/// Checks that Status == Persisted.
	/// Usage: Produce(value).Then().ValidateProduceSuccess()
	/// </summary>
	public static ITestScenario ValidateProduceSuccess(this ITestScenario scenario)
	{
		return Validate(scenario, result =>
		{
			if (!result.Success)
				throw new InvalidOperationException($"Produce operation failed: {string.Join(", ", result.Errors ?? new System.Collections.Generic.List<string>())}");

			if (result.Status != Confluent.Kafka.PersistenceStatus.Persisted)
				throw new InvalidOperationException($"Message was not persisted. Status: {result.Status}");
		});
	}

	/// <summary>
	/// Validates the partition where the message was produced.
	/// Usage: Produce(value).Then().ValidatePartition(0)
	/// </summary>
	public static ITestScenario ValidatePartition(this ITestScenario scenario, int expectedPartition)
	{
		return Validate(scenario, result =>
		{
			if (result.Partition == null)
				throw new InvalidOperationException("Partition information is not available in the result.");

			if (result.Partition != expectedPartition)
				throw new InvalidOperationException($"Expected partition {expectedPartition} but got {result.Partition}");
		});
	}

	/// <summary>
	/// Validates the offset where the message was produced.
	/// Usage: Produce(value).Then().ValidateOffset(offset => offset > 0)
	/// </summary>
	public static ITestScenario ValidateOffset(this ITestScenario scenario, Func<long, bool> validation)
	{
		if (validation == null)
			throw new ArgumentNullException(nameof(validation));

		return Validate(scenario, result =>
		{
			if (result.Offset == null)
				throw new InvalidOperationException("Offset information is not available in the result.");

			if (!validation(result.Offset.Value))
				throw new InvalidOperationException($"Offset validation failed for offset {result.Offset}");
		});
	}

	/// <summary>
	/// Validates the delivery status of the produced message.
	/// Usage: Produce(value).Then().ValidateDeliveryStatus(PersistenceStatus.Persisted)
	/// </summary>
	public static ITestScenario ValidateDeliveryStatus(this ITestScenario scenario, Confluent.Kafka.PersistenceStatus expectedStatus)
	{
		return Validate(scenario, result =>
		{
			if (result.Status == null)
				throw new InvalidOperationException("Delivery status is not available in the result.");

			if (result.Status != expectedStatus)
				throw new InvalidOperationException($"Expected delivery status {expectedStatus} but got {result.Status}");
		});
	}
}

