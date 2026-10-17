using XUnitAssured.Kafka.DSL;
using XUnitAssured.Kafka.Abstractions;
using System;
using XUnitAssured.Core.Abstractions;
using XUnitAssured.Kafka.Configuration;
using XUnitAssured.Kafka.Steps;

namespace XUnitAssured.Kafka.Extensions;

/// <summary>
/// Extension methods for configuring Kafka authentication in the fluent DSL.
/// </summary>
public static class KafkaAuthenticationExtensions
{
	/// <summary>
	/// Helper method to apply authentication config to current Kafka step.
	/// </summary>
	private static ITestScenario ApplyAuthConfig(ITestScenario scenario, KafkaAuthConfig authConfig)
	{
		// Pelo construtor de cópia de cada passo. Montar o passo campo a campo copiava a propriedade
		// BootstrapServers (e o GroupId), e o passo passava a ignorar o broker e o grupo da fixture.
		ITestStep newStep = scenario.CurrentStep switch
		{
			KafkaConsumeStep consumeStep => new KafkaConsumeStep(consumeStep) { AuthConfig = authConfig },
			KafkaProduceStep produceStep => new KafkaProduceStep(produceStep) { AuthConfig = authConfig },
			KafkaBatchConsumeStep batchConsumeStep => new KafkaBatchConsumeStep(batchConsumeStep) { AuthConfig = authConfig },
			KafkaBatchProduceStep batchProduceStep => new KafkaBatchProduceStep(batchProduceStep) { AuthConfig = authConfig },
			_ => throw new InvalidOperationException("Current step is not a Kafka step. Call Topic().Consume() or Topic().Produce() first.")
		};
		scenario.SetCurrentStep(newStep);

		return scenario;
	}

	/// <summary>
	/// Configures SASL/PLAIN authentication from testsettings.json.
	/// Automatically loads username and password from settings.
	/// Usage: .WithSaslPlain()
	/// </summary>
	/// <param name="scenario">Test scenario</param>
	/// <returns>Test scenario for chaining</returns>
	/// <exception cref="InvalidOperationException">When SASL/PLAIN is not configured in settings</exception>
	public static IKafkaScenario WithSaslPlain(this ITestScenario scenario)
	{
		ArgumentNullException.ThrowIfNull(scenario);

		if (scenario.CurrentStep is not KafkaConsumeStep && scenario.CurrentStep is not KafkaProduceStep)
			throw new InvalidOperationException("Current step is not a Kafka step. Call Topic().Consume() or Topic().Produce() first.");

		// Load settings
		var settings = KafkaSettings.Load();

		// Validate authentication configuration
		if (settings.Authentication?.SaslPlain == null)
			throw new InvalidOperationException(
				"SASL/PLAIN authentication is not configured in the 'kafka' section of testsettings.json. " +
				"Please add 'authentication.saslPlain' section with 'username' and 'password'.");

		// Create auth config from settings
		var authConfig = new KafkaAuthConfig
		{
			Type = settings.Authentication.Type,
			SaslPlain = settings.Authentication.SaslPlain
		};

		return KafkaScenario.De(ApplyAuthConfig(scenario, authConfig));
	}

	/// <summary>
	/// Configures SASL/PLAIN authentication with explicit credentials.
	/// Usage: .WithSaslPlain("username", "password")
	/// </summary>
	/// <param name="scenario">Test scenario</param>
	/// <param name="username">SASL username</param>
	/// <param name="password">SASL password</param>
	/// <param name="useSsl">Use SSL/TLS (default: true, recommended)</param>
	/// <returns>Test scenario for chaining</returns>
	public static IKafkaScenario WithSaslPlain(this ITestScenario scenario, string username, string password, bool useSsl = true)
	{
		ArgumentNullException.ThrowIfNull(scenario);

		// Create auth config
		var authConfig = new KafkaAuthConfig();
		authConfig.UseSaslPlain(username, password, useSsl);

		return KafkaScenario.De(ApplyAuthConfig(scenario, authConfig));
	}

	/// <summary>
	/// Configures custom Kafka authentication using a configuration action.
	/// Usage: .WithKafkaAuth(config => config.UseSaslPlain("user", "pass"))
	/// </summary>
	/// <param name="scenario">Test scenario</param>
	/// <param name="configure">Configuration action</param>
	/// <returns>Test scenario for chaining</returns>
	public static IKafkaScenario WithKafkaAuth(this ITestScenario scenario, Action<KafkaAuthConfig> configure)
	{
		ArgumentNullException.ThrowIfNull(scenario);

		ArgumentNullException.ThrowIfNull(configure);

		// Create and configure auth config
		var authConfig = new KafkaAuthConfig();
		configure(authConfig);

		return KafkaScenario.De(ApplyAuthConfig(scenario, authConfig));
	}

	/// <summary>
	/// Configures SASL/SCRAM authentication from testsettings.json.
	/// Automatically detects SHA-256 or SHA-512 from settings.
	/// Usage: .WithSaslScram()
	/// </summary>
	/// <param name="scenario">Test scenario</param>
	/// <returns>Test scenario for chaining</returns>
	/// <exception cref="InvalidOperationException">When SASL/SCRAM is not configured in settings</exception>
	public static IKafkaScenario WithSaslScram(this ITestScenario scenario)
	{
		ArgumentNullException.ThrowIfNull(scenario);

		if (scenario.CurrentStep is not KafkaConsumeStep && scenario.CurrentStep is not KafkaProduceStep)
			throw new InvalidOperationException("Current step is not a Kafka step. Call Topic().Consume() or Topic().Produce() first.");

		// Load settings
		var settings = KafkaSettings.Load();

		// Validate authentication configuration
		if (settings.Authentication?.SaslScram == null)
			throw new InvalidOperationException(
				"SASL/SCRAM authentication is not configured in the 'kafka' section of testsettings.json. " +
				"Please add 'authentication.saslScram' section with 'username', 'password', and 'mechanism'.");

		// Create auth config from settings
		var authConfig = new KafkaAuthConfig
		{
			Type = settings.Authentication.Type,
			SaslScram = settings.Authentication.SaslScram
		};

		return KafkaScenario.De(ApplyAuthConfig(scenario, authConfig));
	}

	/// <summary>
	/// Configures SASL/SCRAM-SHA-256 authentication with explicit credentials.
	/// Usage: .WithSaslScram256("username", "password")
	/// </summary>
	/// <param name="scenario">Test scenario</param>
	/// <param name="username">SASL username</param>
	/// <param name="password">SASL password</param>
	/// <param name="useSsl">Use SSL/TLS (default: true, recommended)</param>
	/// <returns>Test scenario for chaining</returns>
	public static IKafkaScenario WithSaslScram256(this ITestScenario scenario, string username, string password, bool useSsl = true)
	{
		ArgumentNullException.ThrowIfNull(scenario);

		// Create auth config
		var authConfig = new KafkaAuthConfig();
		authConfig.UseSaslScram256(username, password, useSsl);

		return KafkaScenario.De(ApplyAuthConfig(scenario, authConfig));
	}

	/// <summary>
	/// Configures SASL/OAUTHBEARER with OpenID Connect: the Kafka client asks the token endpoint
	/// for a token with its client credentials, and presents it to the broker (#179).
	/// Usage: .WithOAuthBearer("http://localhost:8080/realms/kafka/protocol/openid-connect/token", "client", "secret")
	/// </summary>
	/// <param name="scenario">Test scenario</param>
	/// <param name="tokenEndpointUrl">The OAuth 2.0 token endpoint</param>
	/// <param name="clientId">The OAuth client id</param>
	/// <param name="clientSecret">The OAuth client secret</param>
	/// <param name="scope">Scopes to ask for, separated by spaces (optional)</param>
	/// <param name="useSsl">Use SSL/TLS to the broker (default: true, recommended)</param>
	/// <returns>Test scenario for chaining</returns>
	public static IKafkaScenario WithOAuthBearer(this ITestScenario scenario, string tokenEndpointUrl, string clientId, string clientSecret, string? scope = null, bool useSsl = true)
	{
		ArgumentNullException.ThrowIfNull(scenario);

		var authConfig = new KafkaAuthConfig();
		authConfig.UseOAuthBearer(tokenEndpointUrl, clientId, clientSecret, scope, useSsl);

		return KafkaScenario.De(ApplyAuthConfig(scenario, authConfig));
	}

	/// <summary>
	/// Configures SASL/SCRAM-SHA-512 authentication with explicit credentials.
	/// Usage: .WithSaslScram512("username", "password")
	/// </summary>
	/// <param name="scenario">Test scenario</param>
	/// <param name="username">SASL username</param>
	/// <param name="password">SASL password</param>
	/// <param name="useSsl">Use SSL/TLS (default: true, recommended)</param>
	/// <returns>Test scenario for chaining</returns>
	public static IKafkaScenario WithSaslScram512(this ITestScenario scenario, string username, string password, bool useSsl = true)
	{
		ArgumentNullException.ThrowIfNull(scenario);

		// Create auth config
		var authConfig = new KafkaAuthConfig();
		authConfig.UseSaslScram512(username, password, useSsl);

		return KafkaScenario.De(ApplyAuthConfig(scenario, authConfig));
	}

	/// <summary>
	/// Configures SSL/TLS authentication from testsettings.json.
	/// Automatically loads SSL configuration from settings.
	/// Usage: .WithSsl()
	/// </summary>
	/// <param name="scenario">Test scenario</param>
	/// <returns>Test scenario for chaining</returns>
	/// <exception cref="InvalidOperationException">When SSL is not configured in settings</exception>
	public static IKafkaScenario WithSsl(this ITestScenario scenario)
	{
		ArgumentNullException.ThrowIfNull(scenario);

		if (scenario.CurrentStep is not KafkaConsumeStep && scenario.CurrentStep is not KafkaProduceStep)
			throw new InvalidOperationException("Current step is not a Kafka step. Call Topic().Consume() or Topic().Produce() first.");

		// Load settings
		var settings = KafkaSettings.Load();

		// Validate authentication configuration
		if (settings.Authentication?.Ssl == null)
			throw new InvalidOperationException(
				"SSL authentication is not configured in the 'kafka' section of testsettings.json. " +
				"Please add 'authentication.ssl' section with SSL configuration.");

		// Create auth config from settings
		var authConfig = new KafkaAuthConfig
		{
			Type = settings.Authentication.Type,
			Ssl = settings.Authentication.Ssl
		};

		return KafkaScenario.De(ApplyAuthConfig(scenario, authConfig));
	}

	/// <summary>
	/// Configures SSL/TLS authentication with explicit configuration.
	/// Usage: .WithSsl("/path/to/ca-cert.pem")
	/// </summary>
	/// <param name="scenario">Test scenario</param>
	/// <param name="caLocation">Path to CA certificate file (optional)</param>
	/// <param name="enableCertificateVerification">Enable certificate verification (default: true)</param>
	/// <returns>Test scenario for chaining</returns>
	public static IKafkaScenario WithSsl(this ITestScenario scenario, string? caLocation, bool enableCertificateVerification = true)
	{
		ArgumentNullException.ThrowIfNull(scenario);

		// Create auth config
		var authConfig = new KafkaAuthConfig();
		authConfig.UseSsl(caLocation, enableCertificateVerification);

		return KafkaScenario.De(ApplyAuthConfig(scenario, authConfig));
	}

	/// <summary>
	/// Configures mutual TLS (mTLS) authentication with explicit configuration.
	/// Usage: .WithMutualTls("/path/to/client-cert.pem", "/path/to/client-key.pem")
	/// </summary>
	/// <param name="scenario">Test scenario</param>
	/// <param name="certificateLocation">Path to client certificate file</param>
	/// <param name="keyLocation">Path to client private key file</param>
	/// <param name="caLocation">Path to CA certificate file (optional)</param>
	/// <param name="keyPassword">Password for private key (optional)</param>
	/// <returns>Test scenario for chaining</returns>
	public static IKafkaScenario WithMutualTls(this ITestScenario scenario, string certificateLocation, string keyLocation, string? caLocation = null, string? keyPassword = null)
	{
		ArgumentNullException.ThrowIfNull(scenario);

		// Create auth config
		var authConfig = new KafkaAuthConfig();
		authConfig.UseMutualTls(certificateLocation, keyLocation, caLocation, keyPassword);

		return KafkaScenario.De(ApplyAuthConfig(scenario, authConfig));
	}

	/// <summary>
	/// Disables authentication for this Kafka connection.
	/// Useful to override global authentication from testsettings.json.
	/// Usage: .WithNoKafkaAuth()
	/// </summary>
	/// <param name="scenario">Test scenario</param>
	/// <returns>Test scenario for chaining</returns>
	public static IKafkaScenario WithNoKafkaAuth(this ITestScenario scenario)
	{
		ArgumentNullException.ThrowIfNull(scenario);

		// Create auth config with no authentication
		var authConfig = new KafkaAuthConfig();
		authConfig.UseNoAuth();

		return KafkaScenario.De(ApplyAuthConfig(scenario, authConfig));
	}
}


