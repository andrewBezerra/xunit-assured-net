using System;
using System.Collections.Generic;
using System.Linq;
using Confluent.Kafka;
using XUnitAssured.Core.Results;

namespace XUnitAssured.Kafka.Results;

/// <summary>
/// Specialized result for Kafka message consumption/production test steps.
/// Extends TestStepResult with Kafka-specific properties and helper methods.
/// </summary>
public class KafkaStepResult : TestStepResult
{
	/// <summary>
	/// The consumed or produced Kafka message.
	/// Alias for Data property with more explicit naming for Kafka context.
	/// </summary>
	public object? Message => Data;

	/// <summary>
	/// The topic name where the message was consumed from or produced to.
	/// </summary>
	public string? Topic => GetProperty<string>("Topic");

	/// <summary>
	/// The partition number.
	/// </summary>
	public int? Partition => GetProperty<int?>("Partition");

	/// <summary>
	/// The message offset within the partition.
	/// </summary>
	public long? Offset => GetProperty<long?>("Offset");

	/// <summary>
	/// Message timestamp (UTC).
	/// </summary>
	public DateTime? Timestamp => GetProperty<DateTime?>("Timestamp");

	/// <summary>
	/// The message key.
	/// </summary>
	public object? Key => GetProperty<object>("Key");

	/// <summary>
	/// Kafka message headers.
	/// </summary>
	public Headers? Headers => GetProperty<Headers>("Headers");

	/// <summary>
	/// The delivery/persistence status for produce operations.
	/// </summary>
	public PersistenceStatus? Status => GetProperty<string>("Status") switch
	{
		"Persisted" => PersistenceStatus.Persisted,
		"PossiblyPersisted" => PersistenceStatus.PossiblyPersisted,
		"NotPersisted" => PersistenceStatus.NotPersisted,
		_ => null
	};

	/// <summary>
	/// Gets the message converted to the specified type.
	/// </summary>
	/// <typeparam name="T">Target type for deserialization</typeparam>
	/// <returns>Message as type T</returns>
	public T? GetMessage<T>() => GetData<T>();

	/// <summary>
	/// Gets a specific header value by key.
	/// </summary>
	public byte[]? GetHeaderValue(string key)
	{
		if (Headers == null || string.IsNullOrWhiteSpace(key))
			return null;

		foreach (var header in Headers)
		{
			if (header.Key == key)
				return header.GetValueBytes();
		}

		return null;
	}

	/// <summary>
	/// Creates a successful Kafka consume result.
	/// </summary>
	public static KafkaStepResult CreateKafkaConsumeSuccess<TKey, TValue>(
		ConsumeResult<TKey, TValue> consumeResult)
	{
		var properties = new Dictionary<string, object?>
		{
			["Topic"] = consumeResult.Topic,
			["Partition"] = consumeResult.Partition.Value,
			["Offset"] = consumeResult.Offset.Value,
			["Timestamp"] = consumeResult.Message.Timestamp.UtcDateTime,
			["Key"] = consumeResult.Message.Key,
			["Headers"] = consumeResult.Message.Headers
		};

		return new KafkaStepResult
		{
			Metadata = new StepMetadata
			{
				StartedAt = DateTimeOffset.UtcNow,
				CompletedAt = DateTimeOffset.UtcNow,
				Status = StepStatus.Succeeded
			},
			Success = true,
			Data = consumeResult.Message.Value,
			DataType = consumeResult.Message.Value?.GetType(),
			Properties = properties
		};
	}

	/// <summary>
	/// Creates a successful Kafka produce result.
	/// </summary>
	public static KafkaStepResult CreateKafkaProduceSuccess<TKey, TValue>(
		DeliveryResult<TKey, TValue> deliveryResult)
	{
		var properties = new Dictionary<string, object?>
		{
			["Topic"] = deliveryResult.Topic,
			["Partition"] = deliveryResult.Partition.Value,
			["Offset"] = deliveryResult.Offset.Value,
			["Timestamp"] = deliveryResult.Message.Timestamp.UtcDateTime,
			["Key"] = deliveryResult.Message.Key,
			["Status"] = deliveryResult.Status.ToString()
		};

		return new KafkaStepResult
		{
			Metadata = new StepMetadata
			{
				StartedAt = DateTimeOffset.UtcNow,
				CompletedAt = DateTimeOffset.UtcNow,
				Status = StepStatus.Succeeded
			},
			Success = deliveryResult.Status == PersistenceStatus.Persisted,
			Data = deliveryResult.Message.Value,
			DataType = deliveryResult.Message.Value?.GetType(),
			Properties = properties
		};
	}

	/// <summary>
	/// Creates a failed Kafka result from an exception.
	/// </summary>
	public static new KafkaStepResult CreateFailure(Exception exception)
	{
		return new KafkaStepResult
		{
			Metadata = new StepMetadata
			{
				StartedAt = DateTimeOffset.UtcNow,
				CompletedAt = DateTimeOffset.UtcNow,
				Status = StepStatus.Failed
			},
			Success = false,
			Errors = new List<string> { exception.Message }
		};
	}

	/// <summary>
	/// Creates a timeout result (no message consumed within timeout period).
	/// </summary>
	public static KafkaStepResult CreateTimeout(string topic, TimeSpan timeout)
	{
		return new KafkaStepResult
		{
			Metadata = new StepMetadata
			{
				StartedAt = DateTimeOffset.UtcNow,
				CompletedAt = DateTimeOffset.UtcNow,
				Status = StepStatus.Failed
			},
			Success = false,
			Errors = new List<string> { $"No message consumed from topic '{topic}' within timeout of {timeout.TotalSeconds}s" },
			Properties = new Dictionary<string, object?>
			{
				["Topic"] = topic
			}
		};
	}

	/// <summary>
	/// Creates a timeout result for produce operation.
	/// </summary>
	public static KafkaStepResult CreateProduceTimeout(string topic, TimeSpan timeout)
	{
		return new KafkaStepResult
		{
			Metadata = new StepMetadata
			{
				StartedAt = DateTimeOffset.UtcNow,
				CompletedAt = DateTimeOffset.UtcNow,
				Status = StepStatus.Failed
			},
			Success = false,
			Errors = new List<string> { $"Failed to produce message to topic '{topic}' within timeout of {timeout.TotalSeconds}s" },
			Properties = new Dictionary<string, object?>
			{
				["Topic"] = topic
			}
		};
	}

	/// <summary>
	/// Creates a successful batch produce result from multiple delivery results.
	/// </summary>
	public static KafkaStepResult CreateBatchProduceSuccess(
		DeliveryResult<string, string>[] deliveryResults)
	{
		var allPersisted = deliveryResults.All(r => r.Status == PersistenceStatus.Persisted);
		var firstResult = deliveryResults[0];

		var properties = new Dictionary<string, object?>
		{
			["Topic"] = firstResult.Topic,
			["BatchSize"] = deliveryResults.Length,
			["DeliveryResults"] = deliveryResults
		};

		return new KafkaStepResult
		{
			Metadata = new StepMetadata
			{
				StartedAt = DateTimeOffset.UtcNow,
				CompletedAt = DateTimeOffset.UtcNow,
				Status = StepStatus.Succeeded
			},
					Success = allPersisted,
						Data = deliveryResults,
						DataType = typeof(DeliveryResult<string, string>[]),
						Properties = properties
					};
				}

				/// <summary>
				/// Creates a successful batch consume result when all requested messages were received.
				/// </summary>
				public static KafkaStepResult CreateBatchConsumeSuccess(
					List<ConsumeResult<string, string>> consumeResults)
				{
					var firstResult = consumeResults[0];

					var properties = new Dictionary<string, object?>
					{
						["Topic"] = firstResult.Topic,
						["BatchSize"] = consumeResults.Count,
						["ConsumeResults"] = consumeResults
					};

					return new KafkaStepResult
					{
						Metadata = new StepMetadata
						{
							StartedAt = DateTimeOffset.UtcNow,
							CompletedAt = DateTimeOffset.UtcNow,
							Status = StepStatus.Succeeded
						},
						Success = true,
						Data = consumeResults,
						DataType = typeof(List<ConsumeResult<string, string>>),
						Properties = properties
					};
				}

				/// <summary>
				/// Creates a partial batch consume result when timeout expired before all messages were received.
				/// </summary>
				public static KafkaStepResult CreateBatchConsumePartial(
					string topic,
					int expectedCount,
					List<ConsumeResult<string, string>> consumeResults,
					TimeSpan timeout)
				{
					var properties = new Dictionary<string, object?>
					{
						["Topic"] = topic,
						["BatchSize"] = consumeResults.Count,
						["ExpectedBatchSize"] = expectedCount,
						["ConsumeResults"] = consumeResults
					};

					return new KafkaStepResult
					{
						Metadata = new StepMetadata
						{
							StartedAt = DateTimeOffset.UtcNow,
							CompletedAt = DateTimeOffset.UtcNow,
							Status = StepStatus.Failed
						},
						Success = false,
						Errors = new List<string>
						{
							$"Consumed {consumeResults.Count} of {expectedCount} messages from topic '{topic}' within timeout of {timeout.TotalSeconds}s"
						},
						Data = consumeResults,
						DataType = typeof(List<ConsumeResult<string, string>>),
						Properties = properties
					};
				}
			}

