using System;
using System.Threading;
using System.Threading.Tasks;

using Confluent.Kafka;

namespace XUnitAssured.Kafka.Helpers;

/// <summary>
/// Helper class for Kafka producer operations in tests.
/// Provides convenient methods for producing messages with various configurations.
/// </summary>
public class KafkaProducerHelper<TKey, TValue> : IDisposable
{
	private readonly IProducer<TKey, TValue> _producer;
	private bool _disposed;

	/// <summary>
	/// Creates a new KafkaProducerHelper with the specified configuration.
	/// </summary>
	/// <param name="config">Producer configuration</param>
	public KafkaProducerHelper(ProducerConfig config)
	{
		_producer = new ProducerBuilder<TKey, TValue>(config).Build();
	}

	/// <summary>
	/// Creates a new KafkaProducerHelper from KafkaSettings.
	/// </summary>
	/// <param name="settings">Kafka settings</param>
	/// <param name="clientId">Optional client ID</param>
	public KafkaProducerHelper(KafkaSettings settings, string? clientId = null)
		: this(settings.ToProducerConfig(clientId))
	{
	}

	/// <summary>
	/// Produces a message to the specified topic.
	/// </summary>
	/// <param name="topic">Topic name</param>
	/// <param name="key">Message key</param>
	/// <param name="value">Message value</param>
	/// <param name="cancellationToken">Cancellation token</param>
	/// <returns>Delivery result</returns>
	public async Task<DeliveryResult<TKey, TValue>> ProduceAsync(
		string topic,
		TKey key,
		TValue value,
		CancellationToken cancellationToken = default)
	{
		var message = new Message<TKey, TValue>
		{
			Key = key,
			Value = value,
			Timestamp = Timestamp.Default
		};

		return await _producer.ProduceAsync(topic, message, cancellationToken);
	}

	/// <summary>
	/// Produces a message with headers to the specified topic.
	/// </summary>
	/// <param name="topic">Topic name</param>
	/// <param name="key">Message key</param>
	/// <param name="value">Message value</param>
	/// <param name="headers">Message headers</param>
	/// <param name="cancellationToken">Cancellation token</param>
	/// <returns>Delivery result</returns>
	public async Task<DeliveryResult<TKey, TValue>> ProduceWithHeadersAsync(
		string topic,
		TKey key,
		TValue value,
		Headers headers,
		CancellationToken cancellationToken = default)
	{
		var message = new Message<TKey, TValue>
		{
			Key = key,
			Value = value,
			Headers = headers,
			Timestamp = Timestamp.Default
		};

		return await _producer.ProduceAsync(topic, message, cancellationToken);
	}

	/// <summary>
	/// Produces multiple messages to the specified topic in batch.
	/// </summary>
	/// <param name="topic">Topic name</param>
	/// <param name="messages">Messages to produce</param>
	/// <param name="cancellationToken">Cancellation token</param>
	/// <returns>Array of delivery results</returns>
	public async Task<DeliveryResult<TKey, TValue>[]> ProduceBatchAsync(
		string topic,
		Message<TKey, TValue>[] messages,
		CancellationToken cancellationToken = default)
	{
		var tasks = new Task<DeliveryResult<TKey, TValue>>[messages.Length];

		for (int i = 0; i < messages.Length; i++)
		{
			tasks[i] = _producer.ProduceAsync(topic, messages[i], cancellationToken);
		}

		return await Task.WhenAll(tasks);
	}

	/// <summary>
	/// Flushes all pending messages.
	/// </summary>
	/// <param name="timeout">Timeout for flush operation</param>
	public void Flush(TimeSpan? timeout = null)
	{
		_producer.Flush(timeout ?? TimeSpan.FromSeconds(30));
	}

	public void Dispose()
	{
		if (_disposed)
			return;

		_producer?.Dispose();
		_disposed = true;
		GC.SuppressFinalize(this);
	}
}
