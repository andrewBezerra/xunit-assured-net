using XUnitAssured.Core.Abstractions;

namespace XUnitAssured.Kafka.Extensions;

/// <summary>
/// Kafka-specific BDD extension methods for ITestScenario.
/// Provides convenient Execute() method that returns KafkaValidationBuilder for Kafka testing scenarios.
/// </summary>
public static class KafkaBddExtensions
{
	/// <summary>
	/// Executes the Kafka step synchronously and returns a Kafka-specific validation builder.
	/// This is a convenience method that automatically casts to KafkaStepResult.
	/// </summary>
	/// <param name="scenario">The test scenario containing the Kafka step to execute</param>
	/// <returns>A KafkaValidationBuilder for fluent Kafka-specific validation/assertion chains</returns>
	/// <example>
	/// <code>
	/// Given()
	///     .Topic("my-topic")
	///     .Produce(message)
	///     .WithBootstrapServers(servers)
	/// .When()
	///     .Execute()
	/// .Then()
	///     .AssertSuccess()
	///     .AssertTopic("my-topic");
	/// </code>
	/// </example>
	public static KafkaValidationBuilder Execute(this ITestScenario scenario)
	{
		// Execute the Kafka step asynchronously and block until completion
		scenario.ExecuteCurrentStepAsync().GetAwaiter().GetResult();

		// Return a Kafka-specific validation builder
		return new KafkaValidationBuilder(scenario);
	}
}
