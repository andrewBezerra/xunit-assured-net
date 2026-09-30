using System;
using System.Threading;
using System.Collections.Concurrent;

using Confluent.Kafka;

using XUnitAssured.Core.Abstractions;
using XUnitAssured.Core.Configuration;
using XUnitAssured.Kafka.Configuration;
using XUnitAssured.Kafka.Handlers;

namespace XUnitAssured.Kafka.Testing;

/// <summary>
/// Lightweight test fixture for Kafka integration tests.
/// Provides a cached producer (thread-safe, shared across tests) and an optimized consumer factory.
/// Use as IClassFixture&lt;KafkaClassFixture&gt; or extend for custom configuration.
/// </summary>
/// <remarks>
/// The cached producer eliminates TCP connection overhead per test (~1-3s savings each).
/// The consumer factory creates consumers with optimized rebalance settings for test scenarios.
/// 
/// <code>
/// public class MyTests : KafkaTestBase, IClassFixture&lt;KafkaClassFixture&gt;
/// {
///     public MyTests(KafkaClassFixture fixture) : base(fixture) { }
/// }
/// </code>
/// </remarks>
public class KafkaClassFixture : ITestContextSeeder, IDisposable
{
	private readonly Lazy<IProducer<string, string>> _cachedProducer;
	private readonly ConcurrentQueue<string> _sharedProducerErrors = new();
	private bool _disposed;

	public KafkaClassFixture()
	{
		var settings = TestSettings.Load();
		KafkaSettings = settings.GetKafkaSettings() ?? new KafkaSettings();

		if (string.IsNullOrWhiteSpace(KafkaSettings.BootstrapServers))
		{
			throw new InvalidOperationException(
				"Kafka bootstrapServers not configured. Check testsettings.json.");
		}

		BootstrapServers = KafkaSettings.BootstrapServers;

		_cachedProducer = new Lazy<IProducer<string, string>>(
			() => CreateProducerInstance(),
			LazyThreadSafetyMode.ExecutionAndPublication);
	}

	/// <summary>
	/// Bootstrap servers from configuration.
	/// </summary>
	public string BootstrapServers { get; }

	/// <summary>
	/// Default topic for tests. Override to customize.
	/// </summary>
	public virtual string DefaultTopic => "xunit-test-topic";

	/// <summary>
	/// Default consumer group ID from settings.
	/// </summary>
	public string DefaultGroupId => KafkaSettings.GroupId ?? "xunit-test-group";

	/// <summary>
	/// Loaded Kafka settings.
	/// </summary>
	public KafkaSettings KafkaSettings { get; }

	/// <summary>
	/// Cached producer instance. Thread-safe, shared across all tests in the class.
	/// Created lazily on first access and disposed with the fixture.
	/// </summary>
	public IProducer<string, string> SharedProducer => _cachedProducer.Value;

	/// <summary>
	/// Puts this fixture's broker, group, credentials and shared producer into a
	/// scenario's context, so Kafka steps in that scenario use them. This is what
	/// <c>KafkaTestBase.Given()</c> does; exposing it lets the fixture be combined
	/// with others in <c>Given(api, kafka, browser)</c>.
	/// </summary>
	/// <param name="context">The context of the scenario being started.</param>
	public void Seed(ITestContext context)
	{
		if (context == null)
			throw new ArgumentNullException(nameof(context));

		context.SetProperty("_KafkaSharedProducer", SharedProducer);
		context.SetProperty("_KafkaSharedProducerErrors", SharedProducerErrors);
		context.SetProperty("_KafkaBootstrapServers", BootstrapServers);
		context.SetProperty("_KafkaGroupId", DefaultGroupId);
		context.SetProperty("_KafkaAuthConfig", KafkaSettings.Authentication);
	}

	internal ConcurrentQueue<string> SharedProducerErrors => _sharedProducerErrors;

	/// <summary>
	/// Creates a consumer with optimized settings for test scenarios.
	/// Each call returns a NEW consumer (consumers are stateful and not thread-safe).
	/// </summary>
	/// <param name="groupId">Consumer group ID. If null, generates a unique one to avoid rebalance conflicts.</param>
	/// <param name="topic">Topic to subscribe to. If provided, consumer is pre-subscribed.</param>
	/// <returns>A configured consumer. Caller is responsible for disposing.</returns>
	public IConsumer<string, string> CreateConsumer(string? groupId = null, string? topic = null)
	{
		var config = CreateOptimizedConsumerConfig(groupId);
		ApplyAuthentication(config);

		var consumer = new ConsumerBuilder<string, string>(config).Build();

		if (!string.IsNullOrWhiteSpace(topic))
		{
			consumer.Subscribe(topic);
		}

		return consumer;
	}

	/// <summary>
	/// Creates a consumer config optimized for test scenarios with fast rebalance.
	/// </summary>
	protected virtual ConsumerConfig CreateOptimizedConsumerConfig(string? groupId = null)
	{
		var config = new ConsumerConfig
		{
			BootstrapServers = BootstrapServers,
			GroupId = groupId ?? $"xunit-{Guid.NewGuid():N}",
			AutoOffsetReset = AutoOffsetReset.Earliest,
			EnableAutoCommit = false,
			SessionTimeoutMs = 6000,
			HeartbeatIntervalMs = 2000,
			FetchWaitMaxMs = 100,
			EnableSslCertificateVerification = KafkaSettings.EnableSslCertificateVerification
		};

		// Set SslCaLocation if provided
		if (!string.IsNullOrWhiteSpace(KafkaSettings.SslCaLocation))
		{
			config.SslCaLocation = KafkaSettings.SslCaLocation;
		}

		// For SASL/SSL, disable hostname verification to allow connecting to localhost
		if (KafkaSettings.SecurityProtocol == Confluent.Kafka.SecurityProtocol.SaslSsl ||
		    KafkaSettings.SecurityProtocol == Confluent.Kafka.SecurityProtocol.Ssl)
		{
			config.SslEndpointIdentificationAlgorithm = SslEndpointIdentificationAlgorithm.None;
			config.Set("ssl.endpoint.identification.algorithm", "none");
		}

		return config;
	}

	/// <summary>
	/// Creates the cached producer instance. Override to customize producer configuration.
	/// </summary>
	protected virtual IProducer<string, string> CreateProducerInstance()
	{
		var config = new ProducerConfig
		{
			BootstrapServers = BootstrapServers,
			ClientId = $"xunitassured-shared-{Guid.NewGuid():N}",
			EnableSslCertificateVerification = KafkaSettings.EnableSslCertificateVerification
		};

		// Set SslCaLocation if provided
		if (!string.IsNullOrWhiteSpace(KafkaSettings.SslCaLocation))
		{
			config.SslCaLocation = KafkaSettings.SslCaLocation;
		}

		// For SASL/SSL, disable hostname verification to allow connecting to localhost
		// even when certificate is issued for "kafka" hostname
		if (KafkaSettings.SecurityProtocol == Confluent.Kafka.SecurityProtocol.SaslSsl ||
		    KafkaSettings.SecurityProtocol == Confluent.Kafka.SecurityProtocol.Ssl)
		{
			config.SslEndpointIdentificationAlgorithm = SslEndpointIdentificationAlgorithm.None;
			// Also try via librdkafka config
			config.Set("ssl.endpoint.identification.algorithm", "none");
		}

		ApplyAuthentication(config);

		return new ProducerBuilder<string, string>(config)
			.SetErrorHandler((_, error) => _sharedProducerErrors.Enqueue(error.ToString()))
			.SetLogHandler((_, log) =>
			{
				if (log.Level <= SyslogLevel.Error)
					_sharedProducerErrors.Enqueue($"{log.Level}: {log.Message}");
			})
			.Build();
	}

	/// <summary>
	/// Applies authentication settings from KafkaSettings to a client config.
	/// Applies authentication settings from KafkaSettings to a client config.
	/// Supports SASL/PLAIN, SASL/SSL, SASL/SCRAM-256, SASL/SCRAM-512, SSL, and Mutual TLS.
	/// </summary>
	private void ApplyAuthentication(ClientConfig config)
	{
		var authConfig = KafkaSettings.Authentication;
		if (authConfig == null || authConfig.Type == KafkaAuthenticationType.None)
			return;

		IKafkaAuthenticationHandler? handler = authConfig.Type switch
		{
			KafkaAuthenticationType.SaslPlain when authConfig.SaslPlain != null
				=> new Handlers.SaslPlainHandler(authConfig.SaslPlain),
			KafkaAuthenticationType.SaslSsl when authConfig.SaslPlain != null
				=> new Handlers.SaslPlainHandler(authConfig.SaslPlain),
			KafkaAuthenticationType.SaslScram256 when authConfig.SaslScram != null
				=> new Handlers.SaslScramHandler(authConfig.SaslScram),
			KafkaAuthenticationType.SaslScram512 when authConfig.SaslScram != null
				=> new Handlers.SaslScramHandler(authConfig.SaslScram),
			KafkaAuthenticationType.Ssl when authConfig.Ssl != null
				=> new Handlers.SslHandler(authConfig.Ssl),
			KafkaAuthenticationType.MutualTls when authConfig.Ssl != null
				=> new Handlers.SslHandler(authConfig.Ssl),
			_ => null
		};

		if (handler != null)
		{
			if (config is ConsumerConfig consumerConfig)
				handler.ApplyAuthentication(consumerConfig);
			else if (config is ProducerConfig producerConfig)
				handler.ApplyAuthentication(producerConfig);
		}

		// Apply additional SSL settings if configured
		// This is important for SASL/SSL scenarios where SSL config is separate from SASL config
		if (authConfig.Ssl != null)
		{
			ApplySslConfiguration(config, authConfig.Ssl);
		}
	}

	/// <summary>
	/// Applies SSL configuration settings to a client config.
	/// Used for additional SSL settings in SASL/SSL scenarios.
	/// </summary>
	private void ApplySslConfiguration(ClientConfig config, Configuration.SslConfig sslConfig)
	{
		if (!string.IsNullOrWhiteSpace(sslConfig.SslCaLocation))
		{
			config.SslCaLocation = sslConfig.SslCaLocation;
		}

		config.EnableSslCertificateVerification = sslConfig.EnableSslCertificateVerification;

		if (!string.IsNullOrWhiteSpace(sslConfig.SslCertificateLocation))
		{
			config.SslCertificateLocation = sslConfig.SslCertificateLocation;
		}

		if (!string.IsNullOrWhiteSpace(sslConfig.SslKeyLocation))
		{
			config.SslKeyLocation = sslConfig.SslKeyLocation;
		}

		if (!string.IsNullOrWhiteSpace(sslConfig.SslKeyPassword))
		{
			config.SslKeyPassword = sslConfig.SslKeyPassword;
		}
	}

	public void Dispose()
	{
		if (_disposed) return;
		_disposed = true;

		if (_cachedProducer.IsValueCreated)
		{
			try
			{
				_cachedProducer.Value.Flush(TimeSpan.FromSeconds(5));
				_cachedProducer.Value.Dispose();
			}
			catch
			{
				// Best-effort cleanup during test teardown
			}
		}

		GC.SuppressFinalize(this);
	}
}
