using System;
using Confluent.Kafka;
using XUnitAssured.Kafka.Configuration;

namespace XUnitAssured.Kafka.Handlers;

/// <summary>
/// Handler for SASL/PLAIN authentication.
/// Supports both SASL/PLAIN (plaintext) and SASL/SSL (over TLS).
/// </summary>
public class SaslPlainHandler : IKafkaAuthenticationHandler
{
	private readonly SaslPlainConfig _config;

	/// <summary>
	/// Creates a new SaslPlainHandler with the specified configuration.
	/// </summary>
	/// <param name="config">SASL/PLAIN configuration</param>
	public SaslPlainHandler(SaslPlainConfig config)
	{
		_config = config ?? throw new ArgumentNullException(nameof(config));
		ValidateConfig();
	}

	/// <inheritdoc />
	public KafkaAuthenticationType Type =>
		_config.UseSsl ? KafkaAuthenticationType.SaslSsl : KafkaAuthenticationType.SaslPlain;

	/// <inheritdoc />
	public void ApplyAuthentication(ConsumerConfig config)
	{
		if (config == null)
			throw new ArgumentNullException(nameof(config));

		ApplyCommonConfig(config);
	}

	/// <inheritdoc />
	public void ApplyAuthentication(ProducerConfig config)
	{
		if (config == null)
			throw new ArgumentNullException(nameof(config));

		ApplyCommonConfig(config);
	}

	/// <inheritdoc />
	public bool CanHandle(KafkaAuthConfig authConfig)
	{
		return authConfig?.Type == KafkaAuthenticationType.SaslPlain ||
		       authConfig?.Type == KafkaAuthenticationType.SaslSsl;
	}

	/// <summary>
	/// Applies common SASL/PLAIN configuration to client config.
	/// </summary>
	private void ApplyCommonConfig(ClientConfig config)
	{
		// Set security protocol
		config.SecurityProtocol = _config.UseSsl
			? SecurityProtocol.SaslSsl
			: SecurityProtocol.SaslPlaintext;

		// Set SASL mechanism
		config.SaslMechanism = SaslMechanism.Plain;

		// Set credentials
		config.SaslUsername = _config.Username;
		config.SaslPassword = _config.Password;
	}

	/// <summary>
	/// Validates the configuration.
	/// </summary>
	private void ValidateConfig()
	{
		if (string.IsNullOrWhiteSpace(_config.Username))
			throw new InvalidOperationException("SASL username is required for SASL/PLAIN authentication.");

		if (string.IsNullOrWhiteSpace(_config.Password))
			throw new InvalidOperationException("SASL password is required for SASL/PLAIN authentication.");
	}
}
