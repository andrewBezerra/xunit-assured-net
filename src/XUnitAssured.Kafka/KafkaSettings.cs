using System;
using System.Collections.Generic;

using Confluent.Kafka;
using XUnitAssured.Core.Configuration;
using XUnitAssured.Kafka.Configuration;

namespace XUnitAssured.Kafka;

/// <summary>
/// Kafka settings for integration testing using Confluent.Kafka native types.
/// Configuration section name: "Kafka"
/// </summary>
public class KafkaSettings
{
	/// <summary>
	/// Configuration section name in appsettings.json
	/// </summary>
	public const string SectionName = "Kafka";

	/// <summary>
	/// Comma-separated list of broker addresses (host:port).
	/// Example: "localhost:9092" or "broker1:9092,broker2:9092"
	/// </summary>
	public string BootstrapServers { get; set; } = "localhost:9092";

	/// <summary>
	/// Consumer group ID for Kafka consumers.
	/// Required for consumer operations.
	/// </summary>
	public string? GroupId { get; set; }

	/// <summary>
	/// Security protocol for Kafka connection.
	/// Default is Plaintext (no security).
	/// </summary>
	public SecurityProtocol SecurityProtocol { get; set; } = SecurityProtocol.Plaintext;

	/// <summary>
	/// SASL mechanism for authentication.
	/// Only used when SecurityProtocol is SaslPlaintext or SaslSsl.
	/// </summary>
	public SaslMechanism? SaslMechanism { get; set; }

	/// <summary>
	/// SASL username for authentication.
	/// Required when using SASL authentication.
	/// </summary>
	public string? SaslUsername { get; set; }

	/// <summary>
	/// SASL password for authentication.
	/// Required when using SASL authentication.
	/// </summary>
	public string? SaslPassword { get; set; }

	/// <summary>
	/// Enable SSL certificate verification.
	/// Default is true for security.
	/// </summary>
	public bool EnableSslCertificateVerification { get; set; } = true;

	/// <summary>
	/// Path to SSL CA certificate file.
	/// Optional, used for custom certificate chains.
	/// </summary>
	public string? SslCaLocation { get; set; }

	/// <summary>
	/// Optional Schema Registry settings.
	/// Used for Avro/Protobuf serialization.
	/// </summary>
	public SchemaRegistrySettings? SchemaRegistry { get; set; }

	/// <summary>
	/// Authentication configuration for Kafka connections.
	/// Supports SASL/PLAIN, SASL/SCRAM, SSL, and Mutual TLS.
	/// </summary>
	public KafkaAuthConfig? Authentication { get; set; }

	/// <summary>
	/// Additional Kafka configuration properties.
	/// Use this for any configuration not directly mapped as properties.
	/// </summary>
	public Dictionary<string, string>? AdditionalProperties { get; set; }

	/// <summary>
	/// Default constructor for Options pattern binding.
	/// </summary>
	public KafkaSettings()
	{
	}

	/// <summary>
	/// Loads Kafka settings from the <c>kafka</c> section of <c>testsettings.json</c> —
	/// the same file and section the fixtures use — so a step run without a fixture
	/// sees the same broker and credentials.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The file is located like the rest of the configuration: <c>TESTSETTINGS_PATH</c>
	/// if set, otherwise <c>testsettings.json</c> in the current directory and its
	/// parents. <c>${ENV:NAME}</c> placeholders are replaced. The result is cached;
	/// call <see cref="TestSettingsKafkaExtensions.ClearKafkaSettingsCache"/>
	/// to force a reload.
	/// </para>
	/// <para>
	/// When no file or no <c>kafka</c> section is found, defaults are returned:
	/// <c>localhost:9092</c>, plaintext, no authentication.
	/// </para>
	/// <para>
	/// <paramref name="environment"/> selects <c>testsettings.{environment}.json</c>
	/// for the core settings, but the <c>kafka</c> section is currently read from the
	/// base file regardless. Folding per-environment files into the section loaders
	/// is tracked with the configuration unification work.
	/// </para>
	/// </remarks>
	/// <param name="environment">Optional environment name (e.g., "dev", "staging", "prod"). See remarks.</param>
	/// <returns>The configured settings, or defaults when nothing is configured.</returns>
	public static KafkaSettings Load(string? environment = null)
	{
		return TestSettings.Load(environment).GetKafkaSettings() ?? new KafkaSettings();
	}

	/// <summary>
	/// Converts KafkaSettings to Confluent.Kafka ProducerConfig.
	/// </summary>
	/// <param name="clientId">Optional client ID for the producer</param>
	/// <returns>ProducerConfig ready to use with Kafka producer</returns>
	public ProducerConfig ToProducerConfig(string? clientId = null)
	{
		var config = new ProducerConfig(ToClientConfig())
		{
			ClientId = clientId ?? $"xunitassured-producer-{Guid.NewGuid():N}"
		};

		return config;
	}

	/// <summary>
	/// Converts KafkaSettings to Confluent.Kafka ConsumerConfig.
	/// </summary>
	/// <param name="groupId">Consumer group ID (overrides GroupId property if provided)</param>
	/// <returns>ConsumerConfig ready to use with Kafka consumer</returns>
	public ConsumerConfig ToConsumerConfig(string? groupId = null)
	{
		var config = new ConsumerConfig(ToClientConfig())
		{
			GroupId = groupId ?? GroupId ?? $"xunitassured-consumer-{Guid.NewGuid():N}",
			AutoOffsetReset = AutoOffsetReset.Earliest,
			EnableAutoCommit = false // Manual commit for better control in tests
		};

		return config;
	}

	/// <summary>
	/// Converts KafkaSettings to Confluent.Kafka ClientConfig (base configuration).
	/// </summary>
	/// <returns>ClientConfig with common settings</returns>
	public ClientConfig ToClientConfig()
	{
		var config = new ClientConfig
		{
			BootstrapServers = BootstrapServers,
			SecurityProtocol = SecurityProtocol,
			SaslMechanism = SaslMechanism,
			SaslUsername = SaslUsername,
			SaslPassword = SaslPassword,
			EnableSslCertificateVerification = EnableSslCertificateVerification,
			SslCaLocation = SslCaLocation
		};

		// Apply additional properties if any
		if (AdditionalProperties != null)
		{
			foreach (var kvp in AdditionalProperties)
			{
				config.Set(kvp.Key, kvp.Value);
			}
		}

		return config;
	}
}
