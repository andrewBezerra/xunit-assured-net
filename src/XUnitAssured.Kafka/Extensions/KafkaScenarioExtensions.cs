using System;
using System.Collections.Generic;
using System.Linq;
using Confluent.Kafka;
using XUnitAssured.Core.Abstractions;
using XUnitAssured.Kafka.Configuration;
using XUnitAssured.Kafka.Steps;

namespace XUnitAssured.Kafka.Extensions;

/// <summary>
/// Extension methods for Kafka testing in the fluent DSL.
/// </summary>
public static class KafkaScenarioExtensions
{
	/// <summary>
	/// Sets the Kafka topic for the next operation (Consume or Produce).
	/// Usage: Given().Topic("my-topic").Consume() or Given().Topic("my-topic").Produce(value)
	/// </summary>
	public static ITestScenario Topic(this ITestScenario scenario, string topic)
	{
		if (scenario == null)
			throw new ArgumentNullException(nameof(scenario));

		if (string.IsNullOrWhiteSpace(topic))
			throw new ArgumentException("Topic name cannot be null or whitespace.", nameof(topic));

		// Store topic in context for Consume() or Produce() to use
		scenario.Context.SetProperty("_KafkaTopic", topic);
		return scenario;
	}

	/// <summary>
	/// Configures the Kafka step to consume a message.
	/// Must be called after Topic().
	/// </summary>
	public static ITestScenario Consume(this ITestScenario scenario)
	{
		var topic = scenario.Context.GetProperty<string>("_KafkaTopic");

		if (string.IsNullOrWhiteSpace(topic))
			throw new InvalidOperationException("No topic specified. Call Topic() first.");

		var step = new KafkaConsumeStep
		{
			Topic = topic
		};

		scenario.SetCurrentStep(step);
		return scenario;
	}

	/// <summary>
	/// Configures the Kafka step to consume multiple messages from a topic.
	/// Uses a single consumer instance and collects the specified number of messages.
	/// Must be called after Topic().
	/// Usage: Topic("my-topic").ConsumeBatch(5)
	/// </summary>
	public static ITestScenario ConsumeBatch(this ITestScenario scenario, int count)
	{
		if (count <= 0)
			throw new ArgumentOutOfRangeException(nameof(count), "Message count must be greater than zero.");

		var topic = scenario.Context.GetProperty<string>("_KafkaTopic");

		if (string.IsNullOrWhiteSpace(topic))
			throw new InvalidOperationException("No topic specified. Call Topic() first.");

		var step = new KafkaBatchConsumeStep
		{
			Topic = topic,
			MessageCount = count
		};

		scenario.SetCurrentStep(step);
		return scenario;
	}

	/// <summary>
	/// Sets the expected schema type for the consumed message.
	/// Usage: .WithSchema(typeof(MyMessage))
	/// </summary>
	public static ITestScenario WithSchema(this ITestScenario scenario, Type schemaType)
	{
		if (scenario.CurrentStep is KafkaConsumeStep consumeStep)
		{
			var newStep = new KafkaConsumeStep
			{
				Topic = consumeStep.Topic,
				SchemaType = schemaType,
				Timeout = consumeStep.Timeout,
				ConsumerConfig = consumeStep.ConsumerConfig,
				GroupId = consumeStep.GroupId,
				BootstrapServers = consumeStep.BootstrapServers
			};

			scenario.SetCurrentStep(newStep);
		}
		else if (scenario.CurrentStep is KafkaBatchConsumeStep batchConsumeStep)
		{
			var newStep = new KafkaBatchConsumeStep
			{
				Topic = batchConsumeStep.Topic,
				MessageCount = batchConsumeStep.MessageCount,
				SchemaType = schemaType,
				Timeout = batchConsumeStep.Timeout,
				ConsumerConfig = batchConsumeStep.ConsumerConfig,
				GroupId = batchConsumeStep.GroupId,
				BootstrapServers = batchConsumeStep.BootstrapServers
			};

			scenario.SetCurrentStep(newStep);
		}
		else
		{
			throw new InvalidOperationException("Current step is not a Kafka consume step.");
		}

		return scenario;
	}

	/// <summary>
	/// Sets the timeout for consuming or producing a message.
	/// </summary>
	public static ITestScenario WithTimeout(this ITestScenario scenario, TimeSpan timeout)
	{
		if (scenario.CurrentStep is KafkaConsumeStep consumeStep)
		{
			var newStep = new KafkaConsumeStep
			{
				Topic = consumeStep.Topic,
				SchemaType = consumeStep.SchemaType,
				Timeout = timeout,
				ConsumerConfig = consumeStep.ConsumerConfig,
				GroupId = consumeStep.GroupId,
				BootstrapServers = consumeStep.BootstrapServers
			};

			scenario.SetCurrentStep(newStep);
		}
		else if (scenario.CurrentStep is KafkaBatchConsumeStep batchConsumeStep)
		{
			var newStep = new KafkaBatchConsumeStep
			{
				Topic = batchConsumeStep.Topic,
				MessageCount = batchConsumeStep.MessageCount,
				SchemaType = batchConsumeStep.SchemaType,
				Timeout = timeout,
				ConsumerConfig = batchConsumeStep.ConsumerConfig,
				GroupId = batchConsumeStep.GroupId,
				BootstrapServers = batchConsumeStep.BootstrapServers
			};

			scenario.SetCurrentStep(newStep);
		}
		else if (scenario.CurrentStep is KafkaProduceStep produceStep)
		{
			// Create new step with updated timeout (immutable pattern)
			var newStep = new KafkaProduceStep
			{
				Topic = produceStep.Topic,
				Key = produceStep.Key,
				Value = produceStep.Value,
				Headers = produceStep.Headers,
				Partition = produceStep.Partition,
				Timestamp = produceStep.Timestamp,
				Timeout = timeout,
				ProducerConfig = produceStep.ProducerConfig,
				BootstrapServers = produceStep.BootstrapServers,
				AuthConfig = produceStep.AuthConfig,
				JsonOptions = produceStep.JsonOptions
			};

			scenario.SetCurrentStep(newStep);
		}
		else if (scenario.CurrentStep is KafkaBatchProduceStep batchStep)
		{
			var newStep = new KafkaBatchProduceStep
			{
				Topic = batchStep.Topic,
				Messages = batchStep.Messages,
				Headers = batchStep.Headers,
				Timeout = timeout,
				ProducerConfig = batchStep.ProducerConfig,
				BootstrapServers = batchStep.BootstrapServers,
				AuthConfig = batchStep.AuthConfig,
				JsonOptions = batchStep.JsonOptions
			};

			scenario.SetCurrentStep(newStep);
		}
		else
		{
			throw new InvalidOperationException("Current step is not a Kafka step.");
		}

		return scenario;
	}

	/// <summary>
	/// Sets the Kafka bootstrap servers.
	/// Default is "localhost:9092".
	/// </summary>
	public static ITestScenario WithBootstrapServers(this ITestScenario scenario, string bootstrapServers)
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
				BootstrapServers = bootstrapServers
			};

			scenario.SetCurrentStep(newStep);
		}
		else if (scenario.CurrentStep is KafkaBatchConsumeStep batchConsumeStep)
		{
			var newStep = new KafkaBatchConsumeStep
			{
				Topic = batchConsumeStep.Topic,
				MessageCount = batchConsumeStep.MessageCount,
				SchemaType = batchConsumeStep.SchemaType,
				Timeout = batchConsumeStep.Timeout,
				ConsumerConfig = batchConsumeStep.ConsumerConfig,
				GroupId = batchConsumeStep.GroupId,
				BootstrapServers = bootstrapServers
			};

			scenario.SetCurrentStep(newStep);
		}
		else if (scenario.CurrentStep is KafkaProduceStep produceStep)
		{
			// Create new step with updated bootstrap servers (immutable pattern)
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
				BootstrapServers = bootstrapServers,
				AuthConfig = produceStep.AuthConfig,
				JsonOptions = produceStep.JsonOptions
			};

			scenario.SetCurrentStep(newStep);
		}
		else if (scenario.CurrentStep is KafkaBatchProduceStep batchStep)
		{
			var newStep = new KafkaBatchProduceStep
			{
				Topic = batchStep.Topic,
				Messages = batchStep.Messages,
				Headers = batchStep.Headers,
				Timeout = batchStep.Timeout,
				ProducerConfig = batchStep.ProducerConfig,
				BootstrapServers = bootstrapServers,
				AuthConfig = batchStep.AuthConfig,
				JsonOptions = batchStep.JsonOptions
			};

			scenario.SetCurrentStep(newStep);
		}
		else
		{
			throw new InvalidOperationException("Current step is not a Kafka step.");
		}

		return scenario;
	}

	/// <summary>
	/// Sets the Kafka consumer group ID.
	/// </summary>
	public static ITestScenario WithGroupId(this ITestScenario scenario, string groupId)
	{
		if (scenario.CurrentStep is KafkaConsumeStep consumeStep)
		{
			var newStep = new KafkaConsumeStep
			{
				Topic = consumeStep.Topic,
				SchemaType = consumeStep.SchemaType,
				Timeout = consumeStep.Timeout,
				ConsumerConfig = consumeStep.ConsumerConfig,
				GroupId = groupId,
				BootstrapServers = consumeStep.BootstrapServers
			};

			scenario.SetCurrentStep(newStep);
		}
		else if (scenario.CurrentStep is KafkaBatchConsumeStep batchConsumeStep)
		{
			var newStep = new KafkaBatchConsumeStep
			{
				Topic = batchConsumeStep.Topic,
				MessageCount = batchConsumeStep.MessageCount,
				SchemaType = batchConsumeStep.SchemaType,
				Timeout = batchConsumeStep.Timeout,
				ConsumerConfig = batchConsumeStep.ConsumerConfig,
				GroupId = groupId,
				BootstrapServers = batchConsumeStep.BootstrapServers
			};

			scenario.SetCurrentStep(newStep);
		}
		else
		{
			throw new InvalidOperationException("Current step is not a Kafka consume step.");
		}

		return scenario;
	}

	/// <summary>
	/// Sets custom Kafka consumer configuration.
	/// </summary>
	public static ITestScenario WithConsumerConfig(this ITestScenario scenario, ConsumerConfig config)
	{
		if (scenario.CurrentStep is KafkaConsumeStep consumeStep)
		{
			var newStep = new KafkaConsumeStep
			{
				Topic = consumeStep.Topic,
				SchemaType = consumeStep.SchemaType,
				Timeout = consumeStep.Timeout,
				ConsumerConfig = config,
				GroupId = consumeStep.GroupId,
				BootstrapServers = consumeStep.BootstrapServers
			};

			scenario.SetCurrentStep(newStep);
		}
		else if (scenario.CurrentStep is KafkaBatchConsumeStep batchConsumeStep)
		{
			var newStep = new KafkaBatchConsumeStep
			{
				Topic = batchConsumeStep.Topic,
				MessageCount = batchConsumeStep.MessageCount,
				SchemaType = batchConsumeStep.SchemaType,
				Timeout = batchConsumeStep.Timeout,
				ConsumerConfig = config,
				GroupId = batchConsumeStep.GroupId,
				BootstrapServers = batchConsumeStep.BootstrapServers
			};

			scenario.SetCurrentStep(newStep);
		}
		else
		{
			throw new InvalidOperationException("Current step is not a Kafka consume step.");
		}

		return scenario;
	}

	// ========== PRODUCE METHODS ==========

	/// <summary>
	/// Produces a message to the Kafka topic with null key.
	/// Must be called after Topic().
	/// Usage: Topic("my-topic").Produce(myObject)
	/// </summary>
	public static ITestScenario Produce(this ITestScenario scenario, object value)
	{
		var topic = scenario.Context.GetProperty<string>("_KafkaTopic");

		if (string.IsNullOrWhiteSpace(topic))
			throw new InvalidOperationException("No topic specified. Call Topic() first.");

		var step = new KafkaProduceStep
		{
			Topic = topic,
			Value = value
		};

		scenario.SetCurrentStep(step);
		return scenario;
	}

	/// <summary>
	/// Produces a message to the Kafka topic with the specified key.
	/// Must be called after Topic().
	/// Usage: Topic("my-topic").Produce(key: "123", value: myObject)
	/// </summary>
	public static ITestScenario Produce(this ITestScenario scenario, object key, object value)
	{
		var topic = scenario.Context.GetProperty<string>("_KafkaTopic");

		if (string.IsNullOrWhiteSpace(topic))
			throw new InvalidOperationException("No topic specified. Call Topic() first.");

		var step = new KafkaProduceStep
		{
			Topic = topic,
			Key = key,
			Value = value
		};

		scenario.SetCurrentStep(step);
		return scenario;
	}

	/// <summary>
	/// Produces multiple messages simultaneously to the Kafka topic with null keys.
	/// Messages are sent in parallel using the same producer instance.
	/// Must be called after Topic().
	/// Usage: Topic("my-topic").ProduceBatch(messages)
	/// </summary>
	public static ITestScenario ProduceBatch(this ITestScenario scenario, IEnumerable<object> values)
	{
		var topic = scenario.Context.GetProperty<string>("_KafkaTopic");

		if (string.IsNullOrWhiteSpace(topic))
			throw new InvalidOperationException("No topic specified. Call Topic() first.");

		var messages = values
			.Select(v => new KeyValuePair<object?, object>(null, v))
			.ToList();

		var step = new KafkaBatchProduceStep
		{
			Topic = topic,
			Messages = messages
		};

		scenario.SetCurrentStep(step);
		return scenario;
	}

	/// <summary>
	/// Produces multiple keyed messages simultaneously to the Kafka topic.
	/// Messages are sent in parallel using the same producer instance.
	/// Must be called after Topic().
	/// Usage: Topic("my-topic").ProduceBatch(keyedMessages)
	/// </summary>
	public static ITestScenario ProduceBatch(this ITestScenario scenario, IEnumerable<KeyValuePair<object, object>> items)
	{
		var topic = scenario.Context.GetProperty<string>("_KafkaTopic");

		if (string.IsNullOrWhiteSpace(topic))
			throw new InvalidOperationException("No topic specified. Call Topic() first.");

		var messages = items
			.Select(kv => new KeyValuePair<object?, object>(kv.Key, kv.Value))
			.ToList();

		var step = new KafkaBatchProduceStep
		{
			Topic = topic,
			Messages = messages
		};

		scenario.SetCurrentStep(step);
		return scenario;
	}

	// ========== KEY CONFIGURATION ==========

	/// <summary>
	/// Sets the message key for a Kafka produce operation.
	/// Alternative to passing key in Produce() method.
	/// Usage: Produce(value).WithKey("my-key")
	/// </summary>
	public static ITestScenario WithKey(this ITestScenario scenario, object key)
	{
		if (scenario.CurrentStep is not KafkaProduceStep produceStep)
			throw new InvalidOperationException("Current step is not a Kafka produce step.");

		// Create new step with updated key (immutable pattern)
		var newStep = new KafkaProduceStep
		{
			Topic = produceStep.Topic,
			Key = key,
			Value = produceStep.Value,
			Headers = produceStep.Headers,
			Partition = produceStep.Partition,
			Timestamp = produceStep.Timestamp,
			Timeout = produceStep.Timeout,
			ProducerConfig = produceStep.ProducerConfig,
			BootstrapServers = produceStep.BootstrapServers,
			AuthConfig = produceStep.AuthConfig,
			JsonOptions = produceStep.JsonOptions
		};

		scenario.SetCurrentStep(newStep);
		return scenario;
	}

	// ========== HEADERS CONFIGURATION ==========

	/// <summary>
	/// Sets the headers for a Kafka message.
	/// Usage: Produce(value).WithHeaders(headers)
	/// </summary>
	public static ITestScenario WithHeaders(this ITestScenario scenario, Headers headers)
	{
		if (scenario.CurrentStep is KafkaProduceStep produceStep)
		{
			var newStep = new KafkaProduceStep
			{
				Topic = produceStep.Topic,
				Key = produceStep.Key,
				Value = produceStep.Value,
				Headers = headers,
				Partition = produceStep.Partition,
				Timestamp = produceStep.Timestamp,
				Timeout = produceStep.Timeout,
				ProducerConfig = produceStep.ProducerConfig,
				BootstrapServers = produceStep.BootstrapServers,
				AuthConfig = produceStep.AuthConfig,
				JsonOptions = produceStep.JsonOptions
			};

			scenario.SetCurrentStep(newStep);
		}
		else if (scenario.CurrentStep is KafkaBatchProduceStep batchStep)
		{
			var newStep = new KafkaBatchProduceStep
			{
				Topic = batchStep.Topic,
				Messages = batchStep.Messages,
				Headers = headers,
				Timeout = batchStep.Timeout,
				ProducerConfig = batchStep.ProducerConfig,
				BootstrapServers = batchStep.BootstrapServers,
				AuthConfig = batchStep.AuthConfig,
				JsonOptions = batchStep.JsonOptions
			};

			scenario.SetCurrentStep(newStep);
		}
		else
		{
			throw new InvalidOperationException("Current step is not a Kafka produce step.");
		}

		return scenario;
	}

	/// <summary>
	/// Adds a single header to the Kafka message.
	/// Usage: Produce(value).WithHeader("correlation-id", Encoding.UTF8.GetBytes("abc"))
	/// </summary>
	public static ITestScenario WithHeader(this ITestScenario scenario, string name, byte[] value)
	{
		if (scenario.CurrentStep is not KafkaProduceStep produceStep)
			throw new InvalidOperationException("Current step is not a Kafka produce step.");

		// Get existing headers or create new
		var headers = produceStep.Headers ?? new Headers();
		headers.Add(name, value);

		// Create new step with updated headers (immutable pattern)
		var newStep = new KafkaProduceStep
		{
			Topic = produceStep.Topic,
			Key = produceStep.Key,
			Value = produceStep.Value,
			Headers = headers,
			Partition = produceStep.Partition,
			Timestamp = produceStep.Timestamp,
			Timeout = produceStep.Timeout,
			ProducerConfig = produceStep.ProducerConfig,
			BootstrapServers = produceStep.BootstrapServers,
			AuthConfig = produceStep.AuthConfig,
			JsonOptions = produceStep.JsonOptions
		};

		scenario.SetCurrentStep(newStep);
		return scenario;
	}

	// ========== PARTITION/TIMESTAMP CONFIGURATION ==========

	/// <summary>
	/// Sets a specific partition for the Kafka message.
	/// Usage: Produce(value).WithPartition(0)
	/// </summary>
	public static ITestScenario WithPartition(this ITestScenario scenario, int partition)
	{
		if (scenario.CurrentStep is not KafkaProduceStep produceStep)
			throw new InvalidOperationException("Current step is not a Kafka produce step.");

		// Create new step with updated partition (immutable pattern)
		var newStep = new KafkaProduceStep
		{
			Topic = produceStep.Topic,
			Key = produceStep.Key,
			Value = produceStep.Value,
			Headers = produceStep.Headers,
			Partition = partition,
			Timestamp = produceStep.Timestamp,
			Timeout = produceStep.Timeout,
			ProducerConfig = produceStep.ProducerConfig,
			BootstrapServers = produceStep.BootstrapServers,
			AuthConfig = produceStep.AuthConfig,
			JsonOptions = produceStep.JsonOptions
		};

		scenario.SetCurrentStep(newStep);
		return scenario;
	}

	/// <summary>
	/// Sets a custom timestamp for the Kafka message.
	/// Usage: Produce(value).WithTimestamp(DateTime.UtcNow)
	/// </summary>
	public static ITestScenario WithTimestamp(this ITestScenario scenario, DateTime timestamp)
	{
		if (scenario.CurrentStep is not KafkaProduceStep produceStep)
			throw new InvalidOperationException("Current step is not a Kafka produce step.");

		// Create new step with updated timestamp (immutable pattern)
		var newStep = new KafkaProduceStep
		{
			Topic = produceStep.Topic,
			Key = produceStep.Key,
			Value = produceStep.Value,
			Headers = produceStep.Headers,
			Partition = produceStep.Partition,
			Timestamp = timestamp,
			Timeout = produceStep.Timeout,
			ProducerConfig = produceStep.ProducerConfig,
			BootstrapServers = produceStep.BootstrapServers,
			AuthConfig = produceStep.AuthConfig,
			JsonOptions = produceStep.JsonOptions
		};

		scenario.SetCurrentStep(newStep);
		return scenario;
	}

	// ========== PRODUCER CONFIGURATION ==========

	/// <summary>
	/// Sets custom Kafka producer configuration.
	/// Usage: Produce(value).WithProducerConfig(config)
	/// </summary>
	public static ITestScenario WithProducerConfig(this ITestScenario scenario, ProducerConfig config)
	{
		if (scenario.CurrentStep is KafkaProduceStep produceStep)
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
				ProducerConfig = config,
				BootstrapServers = produceStep.BootstrapServers,
				AuthConfig = produceStep.AuthConfig,
				JsonOptions = produceStep.JsonOptions
			};

			scenario.SetCurrentStep(newStep);
		}
		else if (scenario.CurrentStep is KafkaBatchProduceStep batchStep)
		{
			var newStep = new KafkaBatchProduceStep
			{
				Topic = batchStep.Topic,
				Messages = batchStep.Messages,
				Headers = batchStep.Headers,
				Timeout = batchStep.Timeout,
				ProducerConfig = config,
				BootstrapServers = batchStep.BootstrapServers,
				AuthConfig = batchStep.AuthConfig,
				JsonOptions = batchStep.JsonOptions
			};

			scenario.SetCurrentStep(newStep);
		}
		else
		{
			throw new InvalidOperationException("Current step is not a Kafka produce step.");
		}

		return scenario;
	}

	// ========== JSON SERIALIZATION ==========

	/// <summary>
	/// Sets custom JSON serialization options for object serialization.
	/// Usage: Produce(value).WithJsonOptions(new JsonSerializerOptions { ... })
	/// </summary>
	public static ITestScenario WithJsonOptions(this ITestScenario scenario, System.Text.Json.JsonSerializerOptions options)
	{
		if (scenario.CurrentStep is KafkaProduceStep produceStep)
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
				AuthConfig = produceStep.AuthConfig,
				JsonOptions = options
			};

			scenario.SetCurrentStep(newStep);
		}
		else if (scenario.CurrentStep is KafkaBatchProduceStep batchStep)
		{
			var newStep = new KafkaBatchProduceStep
			{
				Topic = batchStep.Topic,
				Messages = batchStep.Messages,
				Headers = batchStep.Headers,
				Timeout = batchStep.Timeout,
				ProducerConfig = batchStep.ProducerConfig,
				BootstrapServers = batchStep.BootstrapServers,
				AuthConfig = batchStep.AuthConfig,
				JsonOptions = options
			};

			scenario.SetCurrentStep(newStep);
		}
		else
		{
			throw new InvalidOperationException("Current step is not a Kafka produce step.");
		}

		return scenario;
	}

	// ========== AUTHENTICATION ==========

	/// <summary>
	/// Configures authentication for the Kafka step using a fluent builder action.
	/// Works with all Kafka step types (Produce, Consume, ProduceBatch, ConsumeBatch).
	/// Usage: .WithAuth(auth => auth.UseSaslPlain("user", "pass"))
	/// </summary>
	/// <param name="scenario">The test scenario</param>
	/// <param name="configure">Action to configure the authentication</param>
	/// <returns>The same scenario for method chaining</returns>
	/// <example>
	/// <code>
	/// Given()
	///     .Topic("my-topic")
	///     .Produce("hello")
	///     .WithBootstrapServers("localhost:29093")
	///     .WithAuth(auth => auth.UseSaslPlain("testuser", "testpass", useSsl: false))
	/// .When()
	///     .Execute()
	/// .Then()
	///     .AssertSuccess();
	/// </code>
	/// </example>
	public static ITestScenario WithAuth(this ITestScenario scenario, Action<KafkaAuthConfig> configure)
	{
		if (configure == null)
			throw new ArgumentNullException(nameof(configure));

		var authConfig = new KafkaAuthConfig();
		configure(authConfig);

		if (scenario.CurrentStep is KafkaProduceStep produceStep)
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
		else if (scenario.CurrentStep is KafkaBatchProduceStep batchProduceStep)
		{
			var newStep = new KafkaBatchProduceStep
			{
				Topic = batchProduceStep.Topic,
				Messages = batchProduceStep.Messages,
				Headers = batchProduceStep.Headers,
				Timeout = batchProduceStep.Timeout,
				ProducerConfig = batchProduceStep.ProducerConfig,
				BootstrapServers = batchProduceStep.BootstrapServers,
				AuthConfig = authConfig,
				JsonOptions = batchProduceStep.JsonOptions
			};

			scenario.SetCurrentStep(newStep);
		}
		else if (scenario.CurrentStep is KafkaConsumeStep consumeStep)
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
		else if (scenario.CurrentStep is KafkaBatchConsumeStep batchConsumeStep)
		{
			var newStep = new KafkaBatchConsumeStep
			{
				Topic = batchConsumeStep.Topic,
				MessageCount = batchConsumeStep.MessageCount,
				SchemaType = batchConsumeStep.SchemaType,
				Timeout = batchConsumeStep.Timeout,
				ConsumerConfig = batchConsumeStep.ConsumerConfig,
				GroupId = batchConsumeStep.GroupId,
				BootstrapServers = batchConsumeStep.BootstrapServers,
				AuthConfig = authConfig
			};

			scenario.SetCurrentStep(newStep);
		}
		else
		{
			throw new InvalidOperationException("Current step is not a Kafka step.");
		}

		return scenario;
	}

}




