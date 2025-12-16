using System;
using System.Threading;
using System.Threading.Tasks;

using Confluent.Kafka;

namespace XUnitAssured.Remote.Helpers;

/// <summary>
/// Helper class for Kafka operations in remote environments.
/// Provides producer and consumer operations with retry logic and error handling.
/// </summary>
public class KafkaRemoteHelper : IDisposable
{
	private readonly RemoteKafkaSettings _settings;
	private bool _disposed;

	/// <summary>
	/// Creates a new KafkaRemoteHelper with the specified settings.
	/// </summary>
	/// <param name="settings">Remote Kafka settings</param>
	public KafkaRemoteHelper(RemoteKafkaSettings settings)
	{
		_settings = settings ?? throw new ArgumentNullException(nameof(settings));
	}

	/// <summary>
	/// Creates a producer with the configured settings.
	/// </summary>
	/// <typeparam name="TKey">Message key type</typeparam>
	/// <typeparam name="TValue">Message value type</typeparam>
	/// <param name="clientId">Optional client ID</param>
	/// <returns>Configured Kafka producer</returns>
	public IProducer<TKey, TValue> CreateProducer<TKey, TValue>(string? clientId = null)
	{
		var config = BuildProducerConfig(clientId);
		return new ProducerBuilder<TKey, TValue>(config).Build();
	}

	/// <summary>
	/// Creates a consumer with the configured settings.
	/// </summary>
	/// <typeparam name="TKey">Message key type</typeparam>
	/// <typeparam name="TValue">Message value type</typeparam>
	/// <param name="groupId">Consumer group ID</param>
	/// <returns>Configured Kafka consumer</returns>
	public IConsumer<TKey, TValue> CreateConsumer<TKey, TValue>(string? groupId = null)
	{
		var config = BuildConsumerConfig(groupId);
		return new ConsumerBuilder<TKey, TValue>(config).Build();
	}

	/// <summary>
	/// Creates an admin client with the configured settings.
	/// </summary>
	/// <returns>Configured Kafka admin client</returns>
	public IAdminClient CreateAdminClient()
	{
		var config = BuildClientConfig();
		return new AdminClientBuilder(config).Build();
	}

	/// <summary>
	/// Produces a message to the specified topic with retry logic.
	/// </summary>
	/// <typeparam name="TKey">Message key type</typeparam>
	/// <typeparam name="TValue">Message value type</typeparam>
	/// <param name="topic">Topic name</param>
	/// <param name="key">Message key</param>
	/// <param name="value">Message value</param>
	/// <param name="maxRetries">Maximum retry attempts (default: 3)</param>
	/// <param name="retryDelay">Delay between retries in milliseconds (default: 1000)</param>
	/// <param name="cancellationToken">Cancellation token</param>
	/// <returns>Delivery result</returns>
	public async Task<DeliveryResult<TKey, TValue>> ProduceWithRetryAsync<TKey, TValue>(
		string topic,
		TKey key,
		TValue value,
		int maxRetries = 3,
		int retryDelay = 1000,
		CancellationToken cancellationToken = default)
	{
		using var producer = CreateProducer<TKey, TValue>();
		var message = new Message<TKey, TValue>
		{
			Key = key,
			Value = value,
			Timestamp = Timestamp.Default
		};

		return await ProduceWithRetryInternalAsync(producer, topic, message, maxRetries, retryDelay, cancellationToken);
	}

	/// <summary>
	/// Produces a message with headers to the specified topic with retry logic.
	/// </summary>
	/// <typeparam name="TKey">Message key type</typeparam>
	/// <typeparam name="TValue">Message value type</typeparam>
	/// <param name="topic">Topic name</param>
	/// <param name="key">Message key</param>
	/// <param name="value">Message value</param>
	/// <param name="headers">Message headers</param>
	/// <param name="maxRetries">Maximum retry attempts (default: 3)</param>
	/// <param name="retryDelay">Delay between retries in milliseconds (default: 1000)</param>
	/// <param name="cancellationToken">Cancellation token</param>
	/// <returns>Delivery result</returns>
	public async Task<DeliveryResult<TKey, TValue>> ProduceWithHeadersAndRetryAsync<TKey, TValue>(
		string topic,
		TKey key,
		TValue value,
		Headers headers,
		int maxRetries = 3,
		int retryDelay = 1000,
		CancellationToken cancellationToken = default)
	{
		using var producer = CreateProducer<TKey, TValue>();
		var message = new Message<TKey, TValue>
		{
			Key = key,
			Value = value,
			Headers = headers,
			Timestamp = Timestamp.Default
		};

		return await ProduceWithRetryInternalAsync(producer, topic, message, maxRetries, retryDelay, cancellationToken);
	}

	/// <summary>
	/// Consumes a single message from the specified topic with timeout.
	/// </summary>
	/// <typeparam name="TKey">Message key type</typeparam>
	/// <typeparam name="TValue">Message value type</typeparam>
	/// <param name="topic">Topic name</param>
	/// <param name="groupId">Consumer group ID</param>
	/// <param name="timeoutMs">Timeout in milliseconds (default: 30000)</param>
	/// <param name="cancellationToken">Cancellation token</param>
	/// <returns>Consumed message or null if timeout</returns>
	public ConsumeResult<TKey, TValue>? ConsumeOne<TKey, TValue>(
		string topic,
		string? groupId = null,
		int timeoutMs = 30000,
		CancellationToken cancellationToken = default)
	{
		using var consumer = CreateConsumer<TKey, TValue>(groupId);
		consumer.Subscribe(topic);

		try
		{
			var result = consumer.Consume(TimeSpan.FromMilliseconds(timeoutMs));
			return result;
		}
		catch (ConsumeException)
		{
			return null;
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			return null;
		}
		finally
		{
			consumer.Unsubscribe();
		}
	}

	/// <summary>
	/// Consumes multiple messages from the specified topic with timeout.
	/// </summary>
	/// <typeparam name="TKey">Message key type</typeparam>
	/// <typeparam name="TValue">Message value type</typeparam>
	/// <param name="topic">Topic name</param>
	/// <param name="maxMessages">Maximum number of messages to consume</param>
	/// <param name="groupId">Consumer group ID</param>
	/// <param name="timeoutMs">Timeout in milliseconds (default: 30000)</param>
	/// <param name="cancellationToken">Cancellation token</param>
	/// <returns>Array of consumed messages</returns>
	public ConsumeResult<TKey, TValue>[] ConsumeMany<TKey, TValue>(
		string topic,
		int maxMessages,
		string? groupId = null,
		int timeoutMs = 30000,
		CancellationToken cancellationToken = default)
	{
		using var consumer = CreateConsumer<TKey, TValue>(groupId);
		consumer.Subscribe(topic);

		var results = new System.Collections.Generic.List<ConsumeResult<TKey, TValue>>();
		var timeout = TimeSpan.FromMilliseconds(timeoutMs);
		var startTime = DateTime.UtcNow;

		try
		{
			while (results.Count < maxMessages && DateTime.UtcNow - startTime < timeout)
			{
				cancellationToken.ThrowIfCancellationRequested();

				var remainingTime = timeout - (DateTime.UtcNow - startTime);
				if (remainingTime <= TimeSpan.Zero)
					break;

				var result = consumer.Consume(remainingTime);
				if (result != null)
				{
					results.Add(result);
				}
			}
		}
		catch (ConsumeException)
		{
			// Return what we have so far
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			// Return what we have so far
		}
		finally
		{
			consumer.Unsubscribe();
		}

		return results.ToArray();
	}

	/// <summary>
	/// Internal method to produce a message with retry logic.
	/// </summary>
	private async Task<DeliveryResult<TKey, TValue>> ProduceWithRetryInternalAsync<TKey, TValue>(
		IProducer<TKey, TValue> producer,
		string topic,
		Message<TKey, TValue> message,
		int maxRetries,
		int retryDelay,
		CancellationToken cancellationToken)
	{
		int attempts = 0;
		Exception? lastException = null;

		while (attempts < maxRetries)
		{
			try
			{
				cancellationToken.ThrowIfCancellationRequested();
				return await producer.ProduceAsync(topic, message, cancellationToken);
			}
			catch (ProduceException<TKey, TValue> ex)
			{
				lastException = ex;
				
				// Don't retry on fatal errors
				if (ex.Error.IsFatal)
				{
					throw;
				}
			}
			catch (Exception ex) when (ex is not OperationCanceledException)
			{
				lastException = ex;
			}

			attempts++;

			if (attempts < maxRetries)
			{
				await Task.Delay(retryDelay, cancellationToken);
			}
		}

		throw new KafkaException(
			new Error(ErrorCode.Local_TimedOut, 
				$"Failed to produce message after {maxRetries} attempts. Last error: {lastException?.Message}"),
			lastException);
	}

	/// <summary>
	/// Builds a ProducerConfig from RemoteKafkaSettings.
	/// </summary>
	private ProducerConfig BuildProducerConfig(string? clientId = null)
	{
		var config = new ProducerConfig(BuildClientConfig())
		{
			ClientId = clientId ?? $"xunitassured-remote-producer-{Guid.NewGuid():N}",
			Acks = Acks.All,
			MessageTimeoutMs = 30000
		};

		return config;
	}

	/// <summary>
	/// Builds a ConsumerConfig from RemoteKafkaSettings.
	/// </summary>
	private ConsumerConfig BuildConsumerConfig(string? groupId = null)
	{
		var config = new ConsumerConfig(BuildClientConfig())
		{
			GroupId = groupId ?? _settings.GroupId ?? $"xunitassured-remote-consumer-{Guid.NewGuid():N}",
			AutoOffsetReset = AutoOffsetReset.Earliest,
			EnableAutoCommit = false
		};

		return config;
	}

	/// <summary>
	/// Builds a base ClientConfig from RemoteKafkaSettings.
	/// </summary>
	private ClientConfig BuildClientConfig()
	{
		var config = new ClientConfig
		{
			BootstrapServers = _settings.BootstrapServers
		};

		// Parse and set SecurityProtocol
		if (Enum.TryParse<SecurityProtocol>(_settings.SecurityProtocol, out var securityProtocol))
		{
			config.SecurityProtocol = securityProtocol;
		}

		// Parse and set SaslMechanism if provided
		if (!string.IsNullOrWhiteSpace(_settings.SaslMechanism) &&
		    Enum.TryParse<SaslMechanism>(_settings.SaslMechanism, out var saslMechanism))
		{
			config.SaslMechanism = saslMechanism;
		}

		// Set SASL credentials
		if (!string.IsNullOrWhiteSpace(_settings.SaslUsername))
		{
			config.SaslUsername = _settings.SaslUsername;
		}

		if (!string.IsNullOrWhiteSpace(_settings.SaslPassword))
		{
			config.SaslPassword = _settings.SaslPassword;
		}

		// Set SSL certificate verification
		config.EnableSslCertificateVerification = _settings.EnableSslCertificateVerification;

		return config;
	}

	public void Dispose()
	{
		if (_disposed)
			return;

		_disposed = true;
		GC.SuppressFinalize(this);
	}
}
