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
		if (scenario.CurrentStep is KafkaConsumeStep consumeStep)
		{
			var newStep = new KafkaConsumeStep
			{
				Topic = consumeStep.Topic,
				SchemaType = consumeStep.SchemaType,
				Timeout = consumeStep.Timeout,
				ConsumerConfig = consumeStep.ConsumerConfig,
				GroupId = consumeStep.GroupId,
				BootstrapServers = consumeStep.BootstrapServers,
				AuthConfig = authConfig
			};
			scenario.SetCurrentStep(newStep);
		}
		else if (scenario.CurrentStep is KafkaProduceStep produceStep)
		{
			var newStep = new KafkaProduceStep
			{
				Topic = produceStep.Topic,
				Key = produceStep.Key,
				Value = produceStep.Value,
				Headers = produceStep.Headers,
				Partition = produceStep.Partition,
				Timestamp = produceStep.Timestamp,
				Timeout = produceStep.Timeout,
				ProducerConfig = produceStep.ProducerConfig,
				BootstrapServers = produceStep.BootstrapServers,
				AuthConfig = authConfig,
				JsonOptions = produceStep.JsonOptions
			};
			scenario.SetCurrentStep(newStep);
		}
		else
		{
			throw new InvalidOperationException("Current step is not a Kafka step. Call Topic().Consume() or Topic().Produce() first.");
		}

		return scenario;
	}

	/// <summary>
	/// Configures SASL/PLAIN authentication from kafkasettings.json.
	/// Automatically loads username and password from settings.
	/// Usage: .WithSaslPlain()
	/// </summary>
	/// <param name="scenario">Test scenario</param>
	/// <returns>Test scenario for chaining</returns>
	/// <exception cref="InvalidOperationException">When SASL/PLAIN is not configured in settings</exception>
	public static ITestScenario WithSaslPlain(this ITestScenario scenario)
	{
		if (scenario == null)
			throw new ArgumentNullException(nameof(scenario));

		if (scenario.CurrentStep is not KafkaConsumeStep && scenario.CurrentStep is not KafkaProduceStep)
			throw new InvalidOperationException("Current step is not a Kafka step. Call Topic().Consume() or Topic().Produce() first.");

		// Load settings
		var settings = KafkaSettings.Load();

		// Validate authentication configuration
		if (settings.Authentication?.SaslPlain == null)
			throw new InvalidOperationException(
				"SASL/PLAIN authentication is not configured in kafkasettings.json. " +
				"Please add 'authentication.saslPlain' section with 'username' and 'password'.");

		// Create auth config from settings
		var authConfig = new KafkaAuthConfig
		{
			Type = settings.Authentication.Type,
			SaslPlain = settings.Authentication.SaslPlain
		};

		return ApplyAuthConfig(scenario, authConfig);
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
	public static ITestScenario WithSaslPlain(this ITestScenario scenario, string username, string password, bool useSsl = true)
	{
		if (scenario == null)
			throw new ArgumentNullException(nameof(scenario));

		// Create auth config
		var authConfig = new KafkaAuthConfig();
		authConfig.UseSaslPlain(username, password, useSsl);

		return ApplyAuthConfig(scenario, authConfig);
	}

	/// <summary>
	/// Configures custom Kafka authentication using a configuration action.
	/// Usage: .WithKafkaAuth(config => config.UseSaslPlain("user", "pass"))
	/// </summary>
	/// <param name="scenario">Test scenario</param>
	/// <param name="configure">Configuration action</param>
	/// <returns>Test scenario for chaining</returns>
	public static ITestScenario WithKafkaAuth(this ITestScenario scenario, Action<KafkaAuthConfig> configure)
	{
		if (scenario == null)
			throw new ArgumentNullException(nameof(scenario));

		if (configure == null)
			throw new ArgumentNullException(nameof(configure));

		// Create and configure auth config
		var authConfig = new KafkaAuthConfig();
		configure(authConfig);

		return ApplyAuthConfig(scenario, authConfig);
	}

	/// <summary>
	/// Configures SASL/SCRAM authentication from kafkasettings.json.
	/// Automatically detects SHA-256 or SHA-512 from settings.
	/// Usage: .WithSaslScram()
	/// </summary>
	/// <param name="scenario">Test scenario</param>
	/// <returns>Test scenario for chaining</returns>
	/// <exception cref="InvalidOperationException">When SASL/SCRAM is not configured in settings</exception>
	public static ITestScenario WithSaslScram(this ITestScenario scenario)
	{
		if (scenario == null)
			throw new ArgumentNullException(nameof(scenario));

		if (scenario.CurrentStep is not KafkaConsumeStep && scenario.CurrentStep is not KafkaProduceStep)
			throw new InvalidOperationException("Current step is not a Kafka step. Call Topic().Consume() or Topic().Produce() first.");

		// Load settings
		var settings = KafkaSettings.Load();

		// Validate authentication configuration
		if (settings.Authentication?.SaslScram == null)
			throw new InvalidOperationException(
				"SASL/SCRAM authentication is not configured in kafkasettings.json. " +
				"Please add 'authentication.saslScram' section with 'username', 'password', and 'mechanism'.");

		// Create auth config from settings
		var authConfig = new KafkaAuthConfig
		{
			Type = settings.Authentication.Type,
			SaslScram = settings.Authentication.SaslScram
		};

		return ApplyAuthConfig(scenario, authConfig);
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
	public static ITestScenario WithSaslScram256(this ITestScenario scenario, string username, string password, bool useSsl = true)
	{
		if (scenario == null)
			throw new ArgumentNullException(nameof(scenario));

		// Create auth config
		var authConfig = new KafkaAuthConfig();
		authConfig.UseSaslScram256(username, password, useSsl);

		return ApplyAuthConfig(scenario, authConfig);
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
	public static ITestScenario WithSaslScram512(this ITestScenario scenario, string username, string password, bool useSsl = true)
	{
		if (scenario == null)
			throw new ArgumentNullException(nameof(scenario));

		// Create auth config
		var authConfig = new KafkaAuthConfig();
		authConfig.UseSaslScram512(username, password, useSsl);

		return ApplyAuthConfig(scenario, authConfig);
	}

	/// <summary>
	/// Configures SSL/TLS authentication from kafkasettings.json.
	/// Automatically loads SSL configuration from settings.
	/// Usage: .WithSsl()
	/// </summary>
	/// <param name="scenario">Test scenario</param>
	/// <returns>Test scenario for chaining</returns>
	/// <exception cref="InvalidOperationException">When SSL is not configured in settings</exception>
	public static ITestScenario WithSsl(this ITestScenario scenario)
	{
		if (scenario == null)
			throw new ArgumentNullException(nameof(scenario));

		if (scenario.CurrentStep is not KafkaConsumeStep && scenario.CurrentStep is not KafkaProduceStep)
			throw new InvalidOperationException("Current step is not a Kafka step. Call Topic().Consume() or Topic().Produce() first.");

		// Load settings
		var settings = KafkaSettings.Load();

		// Validate authentication configuration
		if (settings.Authentication?.Ssl == null)
			throw new InvalidOperationException(
				"SSL authentication is not configured in kafkasettings.json. " +
				"Please add 'authentication.ssl' section with SSL configuration.");

		// Create auth config from settings
		var authConfig = new KafkaAuthConfig
		{
			Type = settings.Authentication.Type,
			Ssl = settings.Authentication.Ssl
		};

		return ApplyAuthConfig(scenario, authConfig);
	}

	/// <summary>
	/// Configures SSL/TLS authentication with explicit configuration.
	/// Usage: .WithSsl("/path/to/ca-cert.pem")
	/// </summary>
	/// <param name="scenario">Test scenario</param>
	/// <param name="caLocation">Path to CA certificate file (optional)</param>
	/// <param name="enableCertificateVerification">Enable certificate verification (default: true)</param>
	/// <returns>Test scenario for chaining</returns>
	public static ITestScenario WithSsl(this ITestScenario scenario, string? caLocation, bool enableCertificateVerification = true)
	{
		if (scenario == null)
			throw new ArgumentNullException(nameof(scenario));

		// Create auth config
		var authConfig = new KafkaAuthConfig();
		authConfig.UseSsl(caLocation, enableCertificateVerification);

		return ApplyAuthConfig(scenario, authConfig);
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
	public static ITestScenario WithMutualTls(this ITestScenario scenario, string certificateLocation, string keyLocation, string? caLocation = null, string? keyPassword = null)
	{
		if (scenario == null)
			throw new ArgumentNullException(nameof(scenario));

		// Create auth config
		var authConfig = new KafkaAuthConfig();
		authConfig.UseMutualTls(certificateLocation, keyLocation, caLocation, keyPassword);

		return ApplyAuthConfig(scenario, authConfig);
	}

	/// <summary>
	/// Disables authentication for this Kafka connection.
	/// Useful to override global authentication from kafkasettings.json.
	/// Usage: .WithNoKafkaAuth()
	/// </summary>
	/// <param name="scenario">Test scenario</param>
	/// <returns>Test scenario for chaining</returns>
	public static ITestScenario WithNoKafkaAuth(this ITestScenario scenario)
	{
		if (scenario == null)
			throw new ArgumentNullException(nameof(scenario));

		// Create auth config with no authentication
		var authConfig = new KafkaAuthConfig();
		authConfig.UseNoAuth();

		return ApplyAuthConfig(scenario, authConfig);
	}
}


