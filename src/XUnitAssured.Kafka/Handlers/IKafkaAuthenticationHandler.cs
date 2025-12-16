using Confluent.Kafka;
using XUnitAssured.Kafka.Configuration;

namespace XUnitAssured.Kafka.Handlers;

/// <summary>
/// Interface for Kafka authentication handlers.
/// Handlers apply authentication configuration to Kafka producer and consumer configs.
/// </summary>
public interface IKafkaAuthenticationHandler
{
	/// <summary>
	/// Gets the authentication type this handler supports.
	/// </summary>
	KafkaAuthenticationType Type { get; }

	/// <summary>
	/// Applies authentication to a Kafka ConsumerConfig.
	/// </summary>
	/// <param name="config">Consumer configuration to modify</param>
	void ApplyAuthentication(ConsumerConfig config);

	/// <summary>
	/// Applies authentication to a Kafka ProducerConfig.
	/// </summary>
	/// <param name="config">Producer configuration to modify</param>
	void ApplyAuthentication(ProducerConfig config);

	/// <summary>
	/// Determines if this handler can handle the specified authentication configuration.
	/// </summary>
	/// <param name="authConfig">Authentication configuration</param>
	/// <returns>True if this handler can process the configuration</returns>
	bool CanHandle(KafkaAuthConfig authConfig);
}
