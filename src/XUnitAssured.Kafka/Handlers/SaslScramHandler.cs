using System;
using Confluent.Kafka;
using XUnitAssured.Kafka.Configuration;

namespace XUnitAssured.Kafka.Handlers;

/// <summary>
/// Handler for SASL/SCRAM authentication.
/// Supports both SCRAM-SHA-256 and SCRAM-SHA-512.
/// Commonly used with AWS MSK and self-hosted Kafka clusters.
/// </summary>
public class SaslScramHandler : IKafkaAuthenticationHandler
{
	private readonly SaslScramConfig _config;

	/// <summary>
	/// Creates a new SaslScramHandler with the specified configuration.
	/// </summary>
	/// <param name="config">SASL/SCRAM configuration</param>
	public SaslScramHandler(SaslScramConfig config)
	{
		_config = config ?? throw new ArgumentNullException(nameof(config));
		ValidateConfig();
	}

	/// <inheritdoc />
	public KafkaAuthenticationType Type =>
		_config.Mechanism == SaslMechanism.ScramSha256
			? KafkaAuthenticationType.SaslScram256
			: KafkaAuthenticationType.SaslScram512;

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
		return authConfig?.Type == KafkaAuthenticationType.SaslScram256 ||
		       authConfig?.Type == KafkaAuthenticationType.SaslScram512;
	}

	/// <summary>
	/// Applies common SASL/SCRAM configuration to client config.
	/// </summary>
	private void ApplyCommonConfig(ClientConfig config)
	{
		// Set security protocol
		config.SecurityProtocol = _config.UseSsl
			? SecurityProtocol.SaslSsl
			: SecurityProtocol.SaslPlaintext;

		// Set SASL mechanism
		config.SaslMechanism = _config.Mechanism;

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
			throw new InvalidOperationException("SASL username is required for SASL/SCRAM authentication.");

		if (string.IsNullOrWhiteSpace(_config.Password))
			throw new InvalidOperationException("SASL password is required for SASL/SCRAM authentication.");

		if (_config.Mechanism != SaslMechanism.ScramSha256 && _config.Mechanism != SaslMechanism.ScramSha512)
			throw new InvalidOperationException($"Unsupported SASL mechanism: {_config.Mechanism}. Only SCRAM-SHA-256 and SCRAM-SHA-512 are supported.");
	}
}
