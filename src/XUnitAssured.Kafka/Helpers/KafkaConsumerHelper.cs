using System;
using System.Collections.Generic;
using System.Threading;

using Confluent.Kafka;

namespace XUnitAssured.Kafka.Helpers;

/// <summary>
/// Helper class for Kafka consumer operations in tests.
/// Provides convenient methods for consuming messages with various configurations.
/// </summary>
public class KafkaConsumerHelper<TKey, TValue> : IDisposable
{
	private readonly IConsumer<TKey, TValue> _consumer;
	private bool _disposed;

	/// <summary>
	/// Creates a new KafkaConsumerHelper with the specified configuration.
	/// </summary>
	/// <param name="config">Consumer configuration</param>
	public KafkaConsumerHelper(ConsumerConfig config)
	{
		_consumer = new ConsumerBuilder<TKey, TValue>(config).Build();
	}

	/// <summary>
	/// Creates a new KafkaConsumerHelper from KafkaSettings.
	/// </summary>
	/// <param name="settings">Kafka settings</param>
	/// <param name="groupId">Optional consumer group ID</param>
	public KafkaConsumerHelper(KafkaSettings settings, string? groupId = null)
		: this(settings.ToConsumerConfig(groupId))
	{
	}

	/// <summary>
	/// Subscribes to one or more topics.
	/// </summary>
	/// <param name="topics">Topics to subscribe to</param>
	public void Subscribe(params string[] topics)
	{
		_consumer.Subscribe(topics);
	}

	/// <summary>
	/// Consumes a single message with timeout.
	/// </summary>
	/// <param name="timeout">Timeout for consume operation</param>
	/// <returns>Consumed message or null if timeout</returns>
	public ConsumeResult<TKey, TValue>? ConsumeOne(TimeSpan? timeout = null)
	{
		try
		{
			return _consumer.Consume(timeout ?? TimeSpan.FromSeconds(30));
		}
		catch (ConsumeException)
		{
			return null;
		}
	}

	/// <summary>
	/// Consumes multiple messages up to the specified count or timeout.
	/// </summary>
	/// <param name="maxMessages">Maximum number of messages to consume</param>
	/// <param name="timeout">Timeout for each consume operation</param>
	/// <param name="cancellationToken">Cancellation token</param>
	/// <returns>List of consumed messages</returns>
	public List<ConsumeResult<TKey, TValue>> ConsumeMany(
		int maxMessages,
		TimeSpan? timeout = null,
		CancellationToken cancellationToken = default)
	{
		var results = new List<ConsumeResult<TKey, TValue>>(maxMessages);
		var consumeTimeout = timeout ?? TimeSpan.FromSeconds(5);

		for (int i = 0; i < maxMessages && !cancellationToken.IsCancellationRequested; i++)
		{
			try
			{
				var result = _consumer.Consume(consumeTimeout);
				if (result != null)
				{
					results.Add(result);
				}
				else
				{
					break; // No more messages
				}
			}
			catch (ConsumeException)
			{
				break; // Error or timeout
			}
		}

		return results;
	}

	/// <summary>
	/// Consumes all available messages with timeout.
	/// </summary>
	/// <param name="maxWaitTime">Maximum time to wait for messages</param>
	/// <param name="cancellationToken">Cancellation token</param>
	/// <returns>List of consumed messages</returns>
	public List<ConsumeResult<TKey, TValue>> ConsumeAll(
		TimeSpan? maxWaitTime = null,
		CancellationToken cancellationToken = default)
	{
		var results = new List<ConsumeResult<TKey, TValue>>();
		var startTime = DateTime.UtcNow;
		var waitTime = maxWaitTime ?? TimeSpan.FromSeconds(30);
		var consumeTimeout = TimeSpan.FromMilliseconds(500);

		while ((DateTime.UtcNow - startTime) < waitTime && !cancellationToken.IsCancellationRequested)
		{
			try
			{
				var result = _consumer.Consume(consumeTimeout);
				if (result != null)
				{
					results.Add(result);
				}
			}
			catch (ConsumeException)
			{
				break;
			}
		}

		return results;
	}

	/// <summary>
	/// Commits the current offsets.
	/// </summary>
	public void Commit()
	{
		_consumer.Commit();
	}

	/// <summary>
	/// Commits the specified message offset.
	/// </summary>
	/// <param name="result">Message to commit</param>
	public void Commit(ConsumeResult<TKey, TValue> result)
	{
		_consumer.Commit(result);
	}

	/// <summary>
	/// Seeks to a specific offset for a topic partition.
	/// </summary>
	/// <param name="topicPartitionOffset">Topic, partition, and offset to seek to</param>
	public void Seek(TopicPartitionOffset topicPartitionOffset)
	{
		_consumer.Seek(topicPartitionOffset);
	}

	/// <summary>
	/// Closes the consumer and commits offsets if needed.
	/// </summary>
	public void Close()
	{
		_consumer.Close();
	}

	public void Dispose()
	{
		if (_disposed)
			return;

		_consumer?.Dispose();
		_disposed = true;
		GC.SuppressFinalize(this);
	}
}
