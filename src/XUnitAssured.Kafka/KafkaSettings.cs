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
	/// <remarks>
	/// The SASL settings can be written here, with the Confluent names, or in
	/// <see cref="Authentication"/>. Every step, fixture and helper reads both; when
	/// <see cref="Authentication"/> has a type other than <c>None</c>, it wins.
	/// </remarks>
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
	/// <remarks>
	/// A relative path is read from the folder of the settings file, so
	/// <c>"docker/secrets/ca.pem"</c> next to <c>testsettings.json</c> works whatever folder the
	/// test runner starts in. So are the paths in <c>authentication.ssl</c> and
	/// <c>schemaRegistry</c>.
	/// </remarks>
	public string? SslCaLocation { get; set; }

	/// <summary>
	/// How the broker's certificate is matched to the host it is reached by. Not set (the
	/// default), the client verifies it. <c>"None"</c> turns that off — for a local broker whose
	/// certificate names another host, e.g. issued for <c>kafka</c> and reached as <c>localhost</c>.
	/// </summary>
	/// <remarks>
	/// Up to 6.16, <c>KafkaClassFixture</c> turned the check off by itself whenever the protocol
	/// was SSL, and the steps did not (#172). Now nothing turns it off unless this says so.
	/// </remarks>
	public SslEndpointIdentificationAlgorithm? SslEndpointIdentificationAlgorithm { get; set; }

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
	/// <paramref name="environment"/>, or <c>TEST_ENV</c> when it is not given, lays
	/// <c>testsettings.{environment}.json</c> over the base file: what it writes in its
	/// <c>kafka</c> section overrides the base, and what it leaves out is kept.
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
	/// <remarks>
	/// The top-level settings, then <see cref="Authentication"/> over them: when both set the
	/// credentials, <see cref="Authentication"/> wins. The steps and <c>KafkaClassFixture</c>
	/// build their configuration the same way (#172).
	/// </remarks>
	/// <returns>ClientConfig with common settings</returns>
	public ClientConfig ToClientConfig()
	{
		var config = ConfiguracaoBase();
		Handlers.AutenticacaoKafka.Aplicar(config, Authentication);
		return config;
	}

	/// <summary>
	/// What the top level of the <c>kafka</c> section says, without <see cref="Authentication"/>:
	/// the steps apply their own, which may come from the step or from a fixture.
	/// </summary>
	internal ClientConfig ConfiguracaoBase()
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

		if (SslEndpointIdentificationAlgorithm is { } algoritmo)
			config.SslEndpointIdentificationAlgorithm = algoritmo;

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
