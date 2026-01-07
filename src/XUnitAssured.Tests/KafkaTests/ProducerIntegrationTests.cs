using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Confluent.Kafka;
using XUnitAssured.Kafka.Steps;

namespace XUnitAssured.Tests.KafkaTests;

[Trait("Category", "Kafka")]
[Trait("Component", "Integration")]
/// <summary>
/// Integration tests combining ProducerConfig and JsonOptions together.
/// Tests real-world scenarios with multiple configuration options.
/// </summary>
public class ProducerIntegrationTests
{
	#region Complete Integration Scenarios

	[Fact(DisplayName = "Produce with all ProducerConfig and JsonOptions features should work together")]
	public void Produce_With_All_Features_Combined()
	{
		// Arrange - ProducerConfig
		var producerConfig = new ProducerConfig
		{
			BootstrapServers = "kafka-cluster:9092",
			CompressionType = CompressionType.Gzip,
			CompressionLevel = 9,
			Acks = Acks.All,
			BatchSize = 32768,
			LingerMs = 50,
			MessageSendMaxRetries = 5,
			RetryBackoffMs = 200,
			RequestTimeoutMs = 15000,
			EnableIdempotence = true,
			MessageMaxBytes = 5242880
		};

		// Arrange - JsonOptions
		var jsonOptions = new JsonSerializerOptions
		{
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
			DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
			WriteIndented = false
		};
		jsonOptions.Converters.Add(new CustomDateTimeConverter());

		// Arrange - Message
		var order = new ComplexOrder
		{
			OrderId = "ORD-123",
			CustomerName = "John Doe",
			Amount = 99.99m,
			CreatedAt = DateTime.UtcNow,
			Status = OrderStatus.Pending,
			OptionalNotes = null
		};

		var timestamp = DateTime.UtcNow.AddMinutes(-5);
		var headers = new Headers();
		headers.Add("correlation-id", Encoding.UTF8.GetBytes("CORR-456"));
		headers.Add("event-type", Encoding.UTF8.GetBytes("OrderCreated"));
		headers.Add("source", Encoding.UTF8.GetBytes("test-suite"));

		// Act
		var scenario = Given()
			.Topic("integration-topic")
			.Produce("order-key", order)
			.WithHeader("correlation-id", Encoding.UTF8.GetBytes("CORR-456"))
			.WithHeader("event-type", Encoding.UTF8.GetBytes("OrderCreated"))
			.WithHeader("source", Encoding.UTF8.GetBytes("test-suite"))
			.WithPartition(0)
			.WithTimestamp(timestamp)
			.WithTimeout(TimeSpan.FromSeconds(10))
			.WithProducerConfig(producerConfig)
			.WithJsonOptions(jsonOptions)
			.WithBootstrapServers("kafka-cluster:9092");

		// Assert - Step configuration
		scenario.CurrentStep.ShouldBeOfType<KafkaProduceStep>();
		var step = (KafkaProduceStep)scenario.CurrentStep;

		// ProducerConfig assertions
		step.ProducerConfig.ShouldNotBeNull();
		step.ProducerConfig.BootstrapServers.ShouldBe("kafka-cluster:9092");
		step.ProducerConfig.CompressionType.ShouldBe(CompressionType.Gzip);
		step.ProducerConfig.Acks.ShouldBe(Acks.All);
		step.ProducerConfig.EnableIdempotence.ShouldBe(true);

		// JsonOptions assertions
		step.JsonOptions.ShouldNotBeNull();
		step.JsonOptions.PropertyNamingPolicy.ShouldBe(JsonNamingPolicy.CamelCase);
		step.JsonOptions.DefaultIgnoreCondition.ShouldBe(JsonIgnoreCondition.WhenWritingNull);

		// Other assertions
		step.Headers.ShouldNotBeNull();
		step.Headers.Count.ShouldBe(3);
		step.Partition.ShouldBe(0);
		step.Timestamp.ShouldBe(timestamp);
		step.Timeout.ShouldBe(TimeSpan.FromSeconds(10));
	}

	[Fact(DisplayName = "High-throughput configuration should optimize for performance")]
	public void Produce_With_High_Throughput_Configuration()
	{
		// Arrange - Optimized for throughput
		var producerConfig = new ProducerConfig
		{
			BootstrapServers = "localhost:9092",
			CompressionType = CompressionType.Lz4,  // Fast compression
			Acks = Acks.Leader,                      // Don't wait for all replicas
			BatchSize = 65536,                       // Large batches (64KB)
			LingerMs = 100,                          // Wait longer to batch
			MessageSendMaxRetries = 3,
			EnableIdempotence = false                // Slight performance gain
		};

		var jsonOptions = new JsonSerializerOptions
		{
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
			WriteIndented = false  // No formatting overhead
		};

		var message = new { Id = 1, Data = "bulk-data" };

		// Act
		var scenario = Given()
			.Topic("high-throughput-topic")
			.Produce(message)
			.WithProducerConfig(producerConfig)
			.WithJsonOptions(jsonOptions);

		// Assert
		var step = (KafkaProduceStep)scenario.CurrentStep;
		step.ProducerConfig.ShouldNotBeNull();
		step.ProducerConfig.CompressionType.ShouldBe(CompressionType.Lz4);
		step.ProducerConfig.Acks.ShouldBe(Acks.Leader);
		step.ProducerConfig.BatchSize.ShouldBe(65536);
		step.ProducerConfig.LingerMs.ShouldBe(100);
		step.JsonOptions.WriteIndented.ShouldBe(false);
	}

	[Fact(DisplayName = "Low-latency configuration should optimize for speed")]
	public void Produce_With_Low_Latency_Configuration()
	{
		// Arrange - Optimized for latency
		var producerConfig = new ProducerConfig
		{
			BootstrapServers = "localhost:9092",
			CompressionType = CompressionType.None,  // No compression overhead
			Acks = Acks.Leader,
			BatchSize = 1,                           // Minimal batching
			LingerMs = 0,                            // Send immediately
			MessageSendMaxRetries = 1,
			RequestTimeoutMs = 1000                  // Fast timeout
		};

		var jsonOptions = new JsonSerializerOptions
		{
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
			WriteIndented = false
		};

		var message = new { Id = 1, UrgentData = "real-time" };

		// Act
		var scenario = Given()
			.Topic("low-latency-topic")
			.Produce(message)
			.WithProducerConfig(producerConfig)
			.WithJsonOptions(jsonOptions);

		// Assert
		var step = (KafkaProduceStep)scenario.CurrentStep;
		step.ProducerConfig.ShouldNotBeNull();
		step.ProducerConfig.CompressionType.ShouldBe(CompressionType.None);
		step.ProducerConfig.BatchSize.ShouldBe(1);
		step.ProducerConfig.LingerMs.ShouldBe(0);
		step.ProducerConfig.RequestTimeoutMs.ShouldBe(1000);
	}

	[Fact(DisplayName = "Reliable delivery configuration should ensure data safety")]
	public void Produce_With_Reliable_Delivery_Configuration()
	{
		// Arrange - Optimized for reliability
		var producerConfig = new ProducerConfig
		{
			BootstrapServers = "localhost:9092",
			Acks = Acks.All,                     // Wait for all replicas
			EnableIdempotence = true,            // Exactly-once semantics
			MessageSendMaxRetries = 10,          // Many retries
			RetryBackoffMs = 500,
			RequestTimeoutMs = 30000,
			CompressionType = CompressionType.Gzip
		};

		var jsonOptions = new JsonSerializerOptions
		{
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase
		};

		var criticalOrder = new ComplexOrder
		{
			OrderId = "CRITICAL-001",
			CustomerName = "VIP Customer",
			Amount = 10000m,
			Status = OrderStatus.Pending
		};

		// Act
		var scenario = Given()
			.Topic("critical-topic")
			.Produce("critical-key", criticalOrder)
			.WithProducerConfig(producerConfig)
			.WithJsonOptions(jsonOptions)
			.WithTimeout(TimeSpan.FromSeconds(60));

		// Assert
		var step = (KafkaProduceStep)scenario.CurrentStep;
		step.ProducerConfig.ShouldNotBeNull();
		step.ProducerConfig.Acks.ShouldBe(Acks.All);
		step.ProducerConfig.EnableIdempotence.ShouldBe(true);
		step.ProducerConfig.MessageSendMaxRetries.ShouldBe(10);
		step.Timeout.ShouldBe(TimeSpan.FromSeconds(60));
	}

	[Fact(DisplayName = "Transactional producer configuration should support transactions")]
	public void Produce_With_Transactional_Configuration()
	{
		// Arrange
		var producerConfig = new ProducerConfig
		{
			BootstrapServers = "localhost:9092",
			TransactionalId = "test-transaction-001",
			EnableIdempotence = true,
			Acks = Acks.All,
			MessageSendMaxRetries = int.MaxValue
		};

		var jsonOptions = new JsonSerializerOptions
		{
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase
		};

		var order = new ComplexOrder
		{
			OrderId = "TX-001",
			Amount = 500m,
			Status = OrderStatus.Processing
		};

		// Act
		var scenario = Given()
			.Topic("transactional-topic")
			.Produce(order)
			.WithProducerConfig(producerConfig)
			.WithJsonOptions(jsonOptions);

		// Assert
		var step = (KafkaProduceStep)scenario.CurrentStep;
		step.ProducerConfig.ShouldNotBeNull();
		step.ProducerConfig.TransactionalId.ShouldBe("test-transaction-001");
		step.ProducerConfig.EnableIdempotence.ShouldBe(true);
		step.ProducerConfig.Acks.ShouldBe(Acks.All);
	}

	#endregion

	#region Complex Message Scenarios

	[Fact(DisplayName = "Nested object with custom JSON options should serialize correctly")]
	public void Produce_Nested_Object_With_Custom_JsonOptions()
	{
		// Arrange
		var jsonOptions = new JsonSerializerOptions
		{
			PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
			DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
		};
		jsonOptions.Converters.Add(new CustomDateTimeConverter());

		var complexOrder = new ComplexOrder
		{
			OrderId = "NESTED-001",
			CustomerName = "John Doe",
			Amount = 299.99m,
			CreatedAt = DateTime.UtcNow,
			Status = OrderStatus.Shipped,
			ShippingAddress = new Address
			{
				Street = "123 Main St",
				City = "New York",
				PostalCode = "10001",
				Country = null  // Should be ignored
			},
			Items = new List<OrderItem>
			{
				new() { ProductId = "PROD-1", Quantity = 2, Price = 50m },
				new() { ProductId = "PROD-2", Quantity = 1, Price = 199.99m }
			}
		};

		// Act
		var scenario = Given()
			.Topic("nested-topic")
			.Produce(complexOrder)
			.WithJsonOptions(jsonOptions);

		// Assert
		var step = (KafkaProduceStep)scenario.CurrentStep;
		step.JsonOptions.ShouldNotBeNull();
		step.JsonOptions.PropertyNamingPolicy.ShouldBe(JsonNamingPolicy.SnakeCaseLower);
		
		// Verify serialization
		var json = JsonSerializer.Serialize(complexOrder, jsonOptions);
		json.ShouldContain("\"order_id\"");
		json.ShouldContain("\"shipping_address\"");
		json.ShouldContain("\"items\"");
		json.ShouldNotContain("\"country\"");  // Null should be ignored
	}

	[Fact(DisplayName = "Array of messages with batch configuration should be efficient")]
	public void Produce_Array_Messages_With_Batch_Configuration()
	{
		// Arrange
		var producerConfig = new ProducerConfig
		{
			BootstrapServers = "localhost:9092",
			BatchSize = 65536,
			LingerMs = 200,
			CompressionType = CompressionType.Gzip
		};

		var jsonOptions = new JsonSerializerOptions
		{
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase
		};

		var orders = new[]
		{
			new ComplexOrder { OrderId = "BATCH-001", Amount = 100m },
			new ComplexOrder { OrderId = "BATCH-002", Amount = 200m },
			new ComplexOrder { OrderId = "BATCH-003", Amount = 300m }
		};

		// Act
		var scenario = Given()
			.Topic("batch-array-topic")
			.Produce(orders)
			.WithProducerConfig(producerConfig)
			.WithJsonOptions(jsonOptions);

		// Assert
		var step = (KafkaProduceStep)scenario.CurrentStep;
		step.ProducerConfig.ShouldNotBeNull();
		step.ProducerConfig.BatchSize.ShouldBe(65536);
		step.ProducerConfig.CompressionType.ShouldBe(CompressionType.Gzip);
	}

	#endregion

	#region Test Models

	private class ComplexOrder
	{
		public string? OrderId { get; set; }
		public string? CustomerName { get; set; }
		public decimal Amount { get; set; }
		public DateTime CreatedAt { get; set; }
		public OrderStatus Status { get; set; }
		public string? OptionalNotes { get; set; }
		public Address? ShippingAddress { get; set; }
		public List<OrderItem>? Items { get; set; }
	}

	private class Address
	{
		public string? Street { get; set; }
		public string? City { get; set; }
		public string? PostalCode { get; set; }
		public string? Country { get; set; }
	}

	private class OrderItem
	{
		public string? ProductId { get; set; }
		public int Quantity { get; set; }
		public decimal Price { get; set; }
	}

	private enum OrderStatus
	{
		Pending,
		Processing,
		Shipped,
		Delivered,
		Cancelled
	}

	private class CustomDateTimeConverter : JsonConverter<DateTime>
	{
		public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			return DateTime.Parse(reader.GetString()!, System.Globalization.CultureInfo.InvariantCulture);
		}

		public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
		{
			writer.WriteStringValue(value.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture));
		}
	}

	#endregion
}
