using System;
using Confluent.Kafka;
using XUnitAssured.Kafka.Configuration;

namespace XUnitAssured.Kafka.Handlers;

/// <summary>
/// Handler for SASL/OAUTHBEARER with OpenID Connect (#179).
/// librdkafka gets the token from the token endpoint with the client credentials, and renews it.
/// </summary>
public class OAuthBearerHandler : IKafkaAuthenticationHandler
{
	private readonly OAuthBearerConfig _config;

	/// <summary>
	/// Creates a new OAuthBearerHandler with the specified configuration.
	/// </summary>
	/// <param name="config">SASL/OAUTHBEARER configuration</param>
	public OAuthBearerHandler(OAuthBearerConfig config)
	{
		_config = config ?? throw new ArgumentNullException(nameof(config));
		ValidateConfig();
	}

	/// <inheritdoc />
	public KafkaAuthenticationType Type => KafkaAuthenticationType.OAuthBearer;

	/// <inheritdoc />
	public void ApplyAuthentication(ConsumerConfig config)
	{
		ArgumentNullException.ThrowIfNull(config);

		ApplyCommonConfig(config);
	}

	/// <inheritdoc />
	public void ApplyAuthentication(ProducerConfig config)
	{
		ArgumentNullException.ThrowIfNull(config);

		ApplyCommonConfig(config);
	}

	/// <inheritdoc />
	public bool CanHandle(KafkaAuthConfig authConfig)
	{
		return authConfig?.Type == KafkaAuthenticationType.OAuthBearer;
	}

	private void ApplyCommonConfig(ClientConfig config)
	{
		config.SecurityProtocol = _config.UseSsl
			? SecurityProtocol.SaslSsl
			: SecurityProtocol.SaslPlaintext;

		config.SaslMechanism = SaslMechanism.OAuthBearer;
		config.SaslOauthbearerMethod = SaslOauthbearerMethod.Oidc;
		config.SaslOauthbearerTokenEndpointUrl = _config.TokenEndpointUrl;
		config.SaslOauthbearerClientId = _config.ClientId;
		config.SaslOauthbearerClientSecret = _config.ClientSecret;

		if (!string.IsNullOrWhiteSpace(_config.Scope))
			config.SaslOauthbearerScope = _config.Scope;
	}

	private void ValidateConfig()
	{
		if (string.IsNullOrWhiteSpace(_config.TokenEndpointUrl))
			throw new InvalidOperationException("The token endpoint URL is required for SASL/OAUTHBEARER authentication.");

		if (string.IsNullOrWhiteSpace(_config.ClientId))
			throw new InvalidOperationException("The client id is required for SASL/OAUTHBEARER authentication.");

		if (string.IsNullOrWhiteSpace(_config.ClientSecret))
			throw new InvalidOperationException("The client secret is required for SASL/OAUTHBEARER authentication.");
	}
}
