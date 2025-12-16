using System;
using Confluent.Kafka;
using XUnitAssured.Core.Abstractions;
using XUnitAssured.Kafka.Steps;

namespace XUnitAssured.Kafka.Extensions;

/// <summary>
/// Extension methods for Kafka testing in the fluent DSL.
/// </summary>
public static class KafkaScenarioExtensions
{
	/// <summary>
	/// Starts a Kafka consume step with the specified topic.
	/// Usage: Given().Topic("my-topic").Consume()
	/// </summary>
	public static ITestScenario Topic(this ITestScenario scenario, string topic)
	{
		if (scenario == null)
			throw new ArgumentNullException(nameof(scenario));

		if (string.IsNullOrWhiteSpace(topic))
			throw new ArgumentException("Topic name cannot be null or whitespace.", nameof(topic));

		var step = new KafkaConsumeStep
		{
			Topic = topic
		};

		scenario.SetCurrentStep(step);
		return scenario;
	}

	/// <summary>
	/// Configures the Kafka step to consume a message.
	/// Must be called after Topic().
	/// </summary>
	public static ITestScenario Consume(this ITestScenario scenario)
	{
		if (scenario.CurrentStep is not KafkaConsumeStep)
			throw new InvalidOperationException("Current step is not a Kafka consume step. Call Topic() first.");

		// Step is already configured, just return
		return scenario;
	}

	/// <summary>
	/// Sets the expected schema type for the consumed message.
	/// Usage: .WithSchema(typeof(MyMessage))
	/// </summary>
	public static ITestScenario WithSchema(this ITestScenario scenario, Type schemaType)
	{
		if (scenario.CurrentStep is not KafkaConsumeStep currentStep)
			throw new InvalidOperationException("Current step is not a Kafka consume step.");

		// Create new step with updated schema type (immutable pattern)
		var newStep = new KafkaConsumeStep
		{
			Topic = currentStep.Topic,
			SchemaType = schemaType,
			Timeout = currentStep.Timeout,
			ConsumerConfig = currentStep.ConsumerConfig,
			GroupId = currentStep.GroupId,
			BootstrapServers = currentStep.BootstrapServers
		};

		scenario.SetCurrentStep(newStep);
		return scenario;
	}

	/// <summary>
	/// Sets the timeout for consuming a message.
	/// </summary>
	public static ITestScenario WithTimeout(this ITestScenario scenario, TimeSpan timeout)
	{
		if (scenario.CurrentStep is not KafkaConsumeStep currentStep)
			throw new InvalidOperationException("Current step is not a Kafka consume step.");

		// Create new step with updated timeout (immutable pattern)
		var newStep = new KafkaConsumeStep
		{
			Topic = currentStep.Topic,
			SchemaType = currentStep.SchemaType,
			Timeout = timeout,
			ConsumerConfig = currentStep.ConsumerConfig,
			GroupId = currentStep.GroupId,
			BootstrapServers = currentStep.BootstrapServers
		};

		scenario.SetCurrentStep(newStep);
		return scenario;
	}

	/// <summary>
	/// Sets the Kafka bootstrap servers.
	/// Default is "localhost:9092".
	/// </summary>
	public static ITestScenario WithBootstrapServers(this ITestScenario scenario, string bootstrapServers)
	{
		if (scenario.CurrentStep is not KafkaConsumeStep currentStep)
			throw new InvalidOperationException("Current step is not a Kafka consume step.");

		// Create new step with updated bootstrap servers (immutable pattern)
		var newStep = new KafkaConsumeStep
		{
			Topic = currentStep.Topic,
			SchemaType = currentStep.SchemaType,
			Timeout = currentStep.Timeout,
			ConsumerConfig = currentStep.ConsumerConfig,
			GroupId = currentStep.GroupId,
			BootstrapServers = bootstrapServers
		};

		scenario.SetCurrentStep(newStep);
		return scenario;
	}

	/// <summary>
	/// Sets the Kafka consumer group ID.
	/// </summary>
	public static ITestScenario WithGroupId(this ITestScenario scenario, string groupId)
	{
		if (scenario.CurrentStep is not KafkaConsumeStep currentStep)
			throw new InvalidOperationException("Current step is not a Kafka consume step.");

		// Create new step with updated group ID (immutable pattern)
		var newStep = new KafkaConsumeStep
		{
			Topic = currentStep.Topic,
			SchemaType = currentStep.SchemaType,
			Timeout = currentStep.Timeout,
			ConsumerConfig = currentStep.ConsumerConfig,
			GroupId = groupId,
			BootstrapServers = currentStep.BootstrapServers
		};

		scenario.SetCurrentStep(newStep);
		return scenario;
	}

	/// <summary>
	/// Sets custom Kafka consumer configuration.
	/// </summary>
	public static ITestScenario WithConsumerConfig(this ITestScenario scenario, ConsumerConfig config)
	{
		if (scenario.CurrentStep is not KafkaConsumeStep currentStep)
			throw new InvalidOperationException("Current step is not a Kafka consume step.");

		// Create new step with updated config (immutable pattern)
		var newStep = new KafkaConsumeStep
		{
			Topic = currentStep.Topic,
			SchemaType = currentStep.SchemaType,
			Timeout = currentStep.Timeout,
			ConsumerConfig = config,
			GroupId = currentStep.GroupId,
			BootstrapServers = currentStep.BootstrapServers
		};

		scenario.SetCurrentStep(newStep);
		return scenario;
	}

}
