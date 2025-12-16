using System;
using Confluent.Kafka;
using XUnitAssured.Kafka.Configuration;

namespace XUnitAssured.Kafka.Handlers;

/// <summary>
/// Handler for SSL/TLS authentication.
/// Provides encrypted connections to Kafka brokers.
/// Supports both one-way SSL and mutual TLS (mTLS).
/// </summary>
public class SslHandler : IKafkaAuthenticationHandler
{
	private readonly SslConfig _config;

	/// <summary>
	/// Creates a new SslHandler with the specified configuration.
	/// </summary>
	/// <param name="config">SSL/TLS configuration</param>
	public SslHandler(SslConfig config)
	{
		_config = config ?? throw new ArgumentNullException(nameof(config));
		ValidateConfig();
	}

	/// <inheritdoc />
	public KafkaAuthenticationType Type =>
		HasClientCertificate() ? KafkaAuthenticationType.MutualTls : KafkaAuthenticationType.Ssl;

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
		return authConfig?.Type == KafkaAuthenticationType.Ssl ||
		       authConfig?.Type == KafkaAuthenticationType.MutualTls;
	}

	/// <summary>
	/// Applies common SSL/TLS configuration to client config.
	/// </summary>
	private void ApplyCommonConfig(ClientConfig config)
	{
		// Set security protocol to SSL
		config.SecurityProtocol = SecurityProtocol.Ssl;

		// Set CA certificate location
		if (!string.IsNullOrWhiteSpace(_config.SslCaLocation))
		{
			config.SslCaLocation = _config.SslCaLocation;
		}

		// Set certificate verification
		config.EnableSslCertificateVerification = _config.EnableSslCertificateVerification;

		// Set client certificate and key (for mutual TLS)
		if (HasClientCertificate())
		{
			config.SslCertificateLocation = _config.SslCertificateLocation;
			config.SslKeyLocation = _config.SslKeyLocation;

			if (!string.IsNullOrWhiteSpace(_config.SslKeyPassword))
			{
				config.SslKeyPassword = _config.SslKeyPassword;
			}
		}
	}

	/// <summary>
	/// Checks if client certificate is configured (for mutual TLS).
	/// </summary>
	private bool HasClientCertificate()
	{
		return !string.IsNullOrWhiteSpace(_config.SslCertificateLocation) &&
		       !string.IsNullOrWhiteSpace(_config.SslKeyLocation);
	}

	/// <summary>
	/// Validates the configuration.
	/// </summary>
	private void ValidateConfig()
	{
		// CA location is not strictly required by Confluent.Kafka
		// but recommended for production

		// If client cert is provided, key must also be provided
		if (!string.IsNullOrWhiteSpace(_config.SslCertificateLocation) &&
		    string.IsNullOrWhiteSpace(_config.SslKeyLocation))
		{
			throw new InvalidOperationException(
				"SSL key location is required when SSL certificate location is provided.");
		}

		if (!string.IsNullOrWhiteSpace(_config.SslKeyLocation) &&
		    string.IsNullOrWhiteSpace(_config.SslCertificateLocation))
		{
			throw new InvalidOperationException(
				"SSL certificate location is required when SSL key location is provided.");
		}
	}
}
