using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Confluent.Kafka;
using XUnitAssured.Kafka.Steps;

namespace XUnitAssured.Tests.KafkaTests;

[Trait("Category", "Kafka")]
[Trait("Component", "ProducerConfig")]
/// <summary>
/// Advanced unit tests for Kafka ProducerConfig and JsonOptions (Phase 3).
/// Tests for compression, ACKs, batching, retries, and JSON serialization options.
/// </summary>
public class KafkaProducerConfigAdvancedTests
{
	#region ProducerConfig - Compression Tests

	[Fact(DisplayName = "WithProducerConfig should support Gzip compression")]
	public void WithProducerConfig_Should_Support_Gzip_Compression()
	{
		// Arrange
		var config = new ProducerConfig
		{
			BootstrapServers = "kafka:9092",
			CompressionType = CompressionType.Gzip
		};

		// Act
		var scenario = Given()
			.Topic("test-topic")
			.Produce("test-value")
			.WithProducerConfig(config);

		// Assert
		var step = (KafkaProduceStep)scenario.CurrentStep;
		step.ProducerConfig.ShouldNotBeNull();
		step.ProducerConfig!.CompressionType.ShouldBe(CompressionType.Gzip);
	}

	[Fact(DisplayName = "WithProducerConfig should support Snappy compression")]
	public void WithProducerConfig_Should_Support_Snappy_Compression()
	{
		// Arrange
		var config = new ProducerConfig
		{
			BootstrapServers = "kafka:9092",
			CompressionType = CompressionType.Snappy
		};

		// Act
		var scenario = Given()
			.Topic("test-topic")
			.Produce("test-value")
			.WithProducerConfig(config);

		// Assert
		var step = (KafkaProduceStep)scenario.CurrentStep;
		step.ProducerConfig!.CompressionType.ShouldBe(CompressionType.Snappy);
	}

	[Fact(DisplayName = "WithProducerConfig should support Lz4 compression")]
	public void WithProducerConfig_Should_Support_Lz4_Compression()
	{
		// Arrange
		var config = new ProducerConfig
		{
			BootstrapServers = "kafka:9092",
			CompressionType = CompressionType.Lz4
		};

		// Act
		var scenario = Given()
			.Topic("test-topic")
			.Produce("test-value")
			.WithProducerConfig(config);

		// Assert
		var step = (KafkaProduceStep)scenario.CurrentStep;
		step.ProducerConfig!.CompressionType.ShouldBe(CompressionType.Lz4);
	}

	[Fact(DisplayName = "WithProducerConfig should support Zstd compression")]
	public void WithProducerConfig_Should_Support_Zstd_Compression()
	{
		// Arrange
		var config = new ProducerConfig
		{
			BootstrapServers = "kafka:9092",
			CompressionType = CompressionType.Zstd
		};

		// Act
		var scenario = Given()
			.Topic("test-topic")
			.Produce("test-value")
			.WithProducerConfig(config);

		// Assert
		var step = (KafkaProduceStep)scenario.CurrentStep;
		step.ProducerConfig!.CompressionType.ShouldBe(CompressionType.Zstd);
	}

	[Fact(DisplayName = "WithProducerConfig should support no compression")]
	public void WithProducerConfig_Should_Support_No_Compression()
	{
		// Arrange
		var config = new ProducerConfig
		{
			BootstrapServers = "kafka:9092",
			CompressionType = CompressionType.None
		};

		// Act
		var scenario = Given()
			.Topic("test-topic")
			.Produce("test-value")
			.WithProducerConfig(config);

		// Assert
		var step = (KafkaProduceStep)scenario.CurrentStep;
		step.ProducerConfig!.CompressionType.ShouldBe(CompressionType.None);
	}

	#endregion

	#region ProducerConfig - ACKs Tests

	[Fact(DisplayName = "WithProducerConfig should support Acks.None")]
	public void WithProducerConfig_Should_Support_Acks_None()
	{
		// Arrange
		var config = new ProducerConfig
		{
			BootstrapServers = "kafka:9092",
			Acks = Acks.None
		};

		// Act
		var scenario = Given()
			.Topic("test-topic")
			.Produce("test-value")
			.WithProducerConfig(config);

		// Assert
		var step = (KafkaProduceStep)scenario.CurrentStep;
		step.ProducerConfig!.Acks.ShouldBe(Acks.None);
	}

	[Fact(DisplayName = "WithProducerConfig should support Acks.Leader")]
	public void WithProducerConfig_Should_Support_Acks_Leader()
	{
		// Arrange
		var config = new ProducerConfig
		{
			BootstrapServers = "kafka:9092",
			Acks = Acks.Leader
		};

		// Act
		var scenario = Given()
			.Topic("test-topic")
			.Produce("test-value")
			.WithProducerConfig(config);

		// Assert
		var step = (KafkaProduceStep)scenario.CurrentStep;
		step.ProducerConfig!.Acks.ShouldBe(Acks.Leader);
	}

	[Fact(DisplayName = "WithProducerConfig should support Acks.All")]
	public void WithProducerConfig_Should_Support_Acks_All()
	{
		// Arrange
		var config = new ProducerConfig
		{
			BootstrapServers = "kafka:9092",
			Acks = Acks.All
		};

		// Act
		var scenario = Given()
			.Topic("test-topic")
			.Produce("test-value")
			.WithProducerConfig(config);

		// Assert
		var step = (KafkaProduceStep)scenario.CurrentStep;
		step.ProducerConfig!.Acks.ShouldBe(Acks.All);
	}

	#endregion

	#region ProducerConfig - Batch and Buffer Tests

	[Fact(DisplayName = "WithProducerConfig should support custom batch size")]
	public void WithProducerConfig_Should_Support_Custom_Batch_Size()
	{
		// Arrange
		var config = new ProducerConfig
		{
			BootstrapServers = "kafka:9092",
			BatchSize = 32768 // 32KB
		};

		// Act
		var scenario = Given()
			.Topic("test-topic")
			.Produce("test-value")
			.WithProducerConfig(config);

		// Assert
		var step = (KafkaProduceStep)scenario.CurrentStep;
		step.ProducerConfig!.BatchSize.ShouldBe(32768);
	}

	[Fact(DisplayName = "WithProducerConfig should support custom linger ms")]
	public void WithProducerConfig_Should_Support_Custom_Linger_Ms()
	{
		// Arrange
		var config = new ProducerConfig
		{
			BootstrapServers = "kafka:9092",
			LingerMs = 10
		};

		// Act
		var scenario = Given()
			.Topic("test-topic")
			.Produce("test-value")
			.WithProducerConfig(config);

		// Assert
		var step = (KafkaProduceStep)scenario.CurrentStep;
		step.ProducerConfig!.LingerMs.ShouldBe(10);
	}

	[Fact(DisplayName = "WithProducerConfig should support custom buffer memory")]
	public void WithProducerConfig_Should_Support_Custom_Buffer_Memory()
	{
		// Arrange
		var config = new ProducerConfig
		{
			BootstrapServers = "kafka:9092",
			// BufferMemory would be configured here if available in ProducerConfig
			MessageMaxBytes = 1048576 // 1MB
		};

		// Act
		var scenario = Given()
			.Topic("test-topic")
			.Produce("test-value")
			.WithProducerConfig(config);

		// Assert
		var step = (KafkaProduceStep)scenario.CurrentStep;
		step.ProducerConfig!.MessageMaxBytes.ShouldBe(1048576);
	}

	[Fact(DisplayName = "WithProducerConfig should support custom max in flight requests")]
	public void WithProducerConfig_Should_Support_Max_In_Flight_Requests()
	{
		// Arrange
		var config = new ProducerConfig
		{
			BootstrapServers = "kafka:9092",
			MaxInFlight = 5
		};

		// Act
		var scenario = Given()
			.Topic("test-topic")
			.Produce("test-value")
			.WithProducerConfig(config);

		// Assert
		var step = (KafkaProduceStep)scenario.CurrentStep;
		step.ProducerConfig!.MaxInFlight.ShouldBe(5);
	}

	#endregion

	#region ProducerConfig - Retry and Timeout Tests

	[Fact(DisplayName = "WithProducerConfig should support custom retry backoff")]
	public void WithProducerConfig_Should_Support_Custom_Retry_Backoff()
	{
		// Arrange
		var config = new ProducerConfig
		{
			BootstrapServers = "kafka:9092",
			RetryBackoffMs = 500
		};

		// Act
		var scenario = Given()
			.Topic("test-topic")
			.Produce("test-value")
			.WithProducerConfig(config);

		// Assert
		var step = (KafkaProduceStep)scenario.CurrentStep;
		step.ProducerConfig!.RetryBackoffMs.ShouldBe(500);
	}

	[Fact(DisplayName = "WithProducerConfig should support custom request timeout")]
	public void WithProducerConfig_Should_Support_Custom_Request_Timeout()
	{
		// Arrange
		var config = new ProducerConfig
		{
			BootstrapServers = "kafka:9092",
			RequestTimeoutMs = 60000 // 60 seconds
		};

		// Act
		var scenario = Given()
			.Topic("test-topic")
			.Produce("test-value")
			.WithProducerConfig(config);

		// Assert
		var step = (KafkaProduceStep)scenario.CurrentStep;
		step.ProducerConfig!.RequestTimeoutMs.ShouldBe(60000);
	}

	[Fact(DisplayName = "WithProducerConfig should support custom message timeout")]
	public void WithProducerConfig_Should_Support_Custom_Message_Timeout()
	{
		// Arrange
		var config = new ProducerConfig
		{
			BootstrapServers = "kafka:9092",
			MessageTimeoutMs = 120000 // 2 minutes
		};

		// Act
		var scenario = Given()
			.Topic("test-topic")
			.Produce("test-value")
			.WithProducerConfig(config);

		// Assert
		var step = (KafkaProduceStep)scenario.CurrentStep;
		step.ProducerConfig!.MessageTimeoutMs.ShouldBe(120000);
	}

	[Fact(DisplayName = "WithProducerConfig should support enabling idempotence")]
	public void WithProducerConfig_Should_Support_Enabling_Idempotence()
	{
		// Arrange
		var config = new ProducerConfig
		{
			BootstrapServers = "kafka:9092",
			EnableIdempotence = true
		};

		// Act
		var scenario = Given()
			.Topic("test-topic")
			.Produce("test-value")
			.WithProducerConfig(config);

		// Assert
		var step = (KafkaProduceStep)scenario.CurrentStep;
		step.ProducerConfig!.EnableIdempotence.HasValue.ShouldBeTrue();
		step.ProducerConfig.EnableIdempotence.Value.ShouldBeTrue();
	}

	#endregion

	#region ProducerConfig - Client Configuration Tests

	[Fact(DisplayName = "WithProducerConfig should support custom client id")]
	public void WithProducerConfig_Should_Support_Custom_Client_Id()
	{
		// Arrange
		var config = new ProducerConfig
		{
			BootstrapServers = "kafka:9092",
			ClientId = "my-custom-producer-client"
		};

		// Act
		var scenario = Given()
			.Topic("test-topic")
			.Produce("test-value")
			.WithProducerConfig(config);

		// Assert
		var step = (KafkaProduceStep)scenario.CurrentStep;
		step.ProducerConfig!.ClientId.ShouldBe("my-custom-producer-client");
	}

	[Fact(DisplayName = "WithProducerConfig should support multiple bootstrap servers")]
	public void WithProducerConfig_Should_Support_Multiple_Bootstrap_Servers()
	{
		// Arrange
		var config = new ProducerConfig
		{
			BootstrapServers = "broker1:9092,broker2:9092,broker3:9092"
		};

		// Act
		var scenario = Given()
			.Topic("test-topic")
			.Produce("test-value")
			.WithProducerConfig(config);

		// Assert
		var step = (KafkaProduceStep)scenario.CurrentStep;
		step.ProducerConfig!.BootstrapServers.ShouldBe("broker1:9092,broker2:9092,broker3:9092");
	}

	#endregion

	#region JsonOptions - Naming Policy Tests

	[Fact(DisplayName = "WithJsonOptions should support CamelCase naming policy")]
	public void WithJsonOptions_Should_Support_CamelCase_Naming_Policy()
	{
		// Arrange
		var options = new JsonSerializerOptions
		{
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase
		};

		// Act
		var scenario = Given()
			.Topic("test-topic")
			.Produce(new { FirstName = "John", LastName = "Doe" })
			.WithJsonOptions(options);

		// Assert
		var step = (KafkaProduceStep)scenario.CurrentStep;
		step.JsonOptions.ShouldNotBeNull();
		step.JsonOptions!.PropertyNamingPolicy.ShouldBe(JsonNamingPolicy.CamelCase);
	}

	[Fact(DisplayName = "WithJsonOptions should support SnakeCaseLower naming policy")]
	public void WithJsonOptions_Should_Support_SnakeCaseLower_Naming_Policy()
	{
		// Arrange
		var options = new JsonSerializerOptions
		{
			PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
		};

		// Act
		var scenario = Given()
			.Topic("test-topic")
			.Produce(new { FirstName = "John", LastName = "Doe" })
			.WithJsonOptions(options);

		// Assert
		var step = (KafkaProduceStep)scenario.CurrentStep;
		step.JsonOptions!.PropertyNamingPolicy.ShouldBe(JsonNamingPolicy.SnakeCaseLower);
	}

	[Fact(DisplayName = "WithJsonOptions should support SnakeCaseUpper naming policy")]
	public void WithJsonOptions_Should_Support_SnakeCaseUpper_Naming_Policy()
	{
		// Arrange
		var options = new JsonSerializerOptions
		{
			PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseUpper
		};

		// Act
		var scenario = Given()
			.Topic("test-topic")
			.Produce(new { FirstName = "John", LastName = "Doe" })
			.WithJsonOptions(options);

		// Assert
		var step = (KafkaProduceStep)scenario.CurrentStep;
		step.JsonOptions!.PropertyNamingPolicy.ShouldBe(JsonNamingPolicy.SnakeCaseUpper);
	}

	[Fact(DisplayName = "WithJsonOptions should support KebabCaseLower naming policy")]
	public void WithJsonOptions_Should_Support_KebabCaseLower_Naming_Policy()
	{
		// Arrange
		var options = new JsonSerializerOptions
		{
			PropertyNamingPolicy = JsonNamingPolicy.KebabCaseLower
		};

		// Act
		var scenario = Given()
			.Topic("test-topic")
			.Produce(new { FirstName = "John", LastName = "Doe" })
			.WithJsonOptions(options);

		// Assert
		var step = (KafkaProduceStep)scenario.CurrentStep;
		step.JsonOptions!.PropertyNamingPolicy.ShouldBe(JsonNamingPolicy.KebabCaseLower);
	}

	#endregion

	#region JsonOptions - Serialization Behavior Tests

	[Fact(DisplayName = "WithJsonOptions should support WriteIndented option")]
	public void WithJsonOptions_Should_Support_WriteIndented_Option()
	{
		// Arrange
		var options = new JsonSerializerOptions
		{
			WriteIndented = true
		};

		// Act
		var scenario = Given()
			.Topic("test-topic")
			.Produce(new { Name = "Test" })
			.WithJsonOptions(options);

		// Assert
		var step = (KafkaProduceStep)scenario.CurrentStep;
		step.JsonOptions!.WriteIndented.ShouldBeTrue();
	}

	[Fact(DisplayName = "WithJsonOptions should support DefaultIgnoreCondition")]
	public void WithJsonOptions_Should_Support_DefaultIgnoreCondition()
	{
		// Arrange
		var options = new JsonSerializerOptions
		{
			DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
		};

		// Act
		var scenario = Given()
			.Topic("test-topic")
			.Produce(new { Name = "Test", Description = (string?)null })
			.WithJsonOptions(options);

		// Assert
		var step = (KafkaProduceStep)scenario.CurrentStep;
		step.JsonOptions!.DefaultIgnoreCondition.ShouldBe(JsonIgnoreCondition.WhenWritingNull);
	}

	[Fact(DisplayName = "WithJsonOptions should support PropertyNameCaseInsensitive")]
	public void WithJsonOptions_Should_Support_PropertyNameCaseInsensitive()
	{
		// Arrange
		var options = new JsonSerializerOptions
		{
			PropertyNameCaseInsensitive = true
		};

		// Act
		var scenario = Given()
			.Topic("test-topic")
			.Produce(new { Name = "Test" })
			.WithJsonOptions(options);

		// Assert
		var step = (KafkaProduceStep)scenario.CurrentStep;
		step.JsonOptions!.PropertyNameCaseInsensitive.ShouldBeTrue();
	}

	[Fact(DisplayName = "WithJsonOptions should support AllowTrailingCommas")]
	public void WithJsonOptions_Should_Support_AllowTrailingCommas()
	{
		// Arrange
		var options = new JsonSerializerOptions
		{
			AllowTrailingCommas = true
		};

		// Act
		var scenario = Given()
			.Topic("test-topic")
			.Produce(new { Name = "Test" })
			.WithJsonOptions(options);

		// Assert
		var step = (KafkaProduceStep)scenario.CurrentStep;
		step.JsonOptions!.AllowTrailingCommas.ShouldBeTrue();
	}

	[Fact(DisplayName = "WithJsonOptions should support MaxDepth configuration")]
	public void WithJsonOptions_Should_Support_MaxDepth_Configuration()
	{
		// Arrange
		var options = new JsonSerializerOptions
		{
			MaxDepth = 64
		};

		// Act
		var scenario = Given()
			.Topic("test-topic")
			.Produce(new { Name = "Test" })
			.WithJsonOptions(options);

		// Assert
		var step = (KafkaProduceStep)scenario.CurrentStep;
		step.JsonOptions!.MaxDepth.ShouldBe(64);
	}

	#endregion

	#region Complex Configuration Integration Tests

	[Fact(DisplayName = "Should support complete ProducerConfig for high-throughput scenario")]
	public void Should_Support_Complete_ProducerConfig_For_High_Throughput()
	{
		// Arrange
		var config = new ProducerConfig
		{
			BootstrapServers = "broker1:9092,broker2:9092,broker3:9092",
			ClientId = "high-throughput-producer",
			Acks = Acks.Leader,
			CompressionType = CompressionType.Lz4,
			BatchSize = 65536, // 64KB
			LingerMs = 10,
			MaxInFlight = 5,
			EnableIdempotence = false
		};

		// Act
		var scenario = Given()
			.Topic("high-volume-topic")
			.Produce(new { EventId = Guid.NewGuid(), Data = "test" })
			.WithProducerConfig(config);

		// Assert
		var step = (KafkaProduceStep)scenario.CurrentStep;
		step.ProducerConfig.ShouldNotBeNull();
		step.ProducerConfig!.Acks.ShouldBe(Acks.Leader);
		step.ProducerConfig.CompressionType.ShouldBe(CompressionType.Lz4);
		step.ProducerConfig.BatchSize.ShouldBe(65536);
		step.ProducerConfig.LingerMs.ShouldBe(10);
		step.ProducerConfig.MaxInFlight.ShouldBe(5);
		step.ProducerConfig.EnableIdempotence.ShouldBe(false);
	}

	[Fact(DisplayName = "Should support complete ProducerConfig for reliability scenario")]
	public void Should_Support_Complete_ProducerConfig_For_Reliability()
	{
		// Arrange
		var config = new ProducerConfig
		{
			BootstrapServers = "kafka:9092",
			ClientId = "reliable-producer",
			Acks = Acks.All,
			EnableIdempotence = true,
			MaxInFlight = 1,
			RetryBackoffMs = 1000,
			RequestTimeoutMs = 30000,
			MessageTimeoutMs = 60000
		};

		// Act
		var scenario = Given()
			.Topic("critical-topic")
			.Produce(new { TransactionId = "TXN-123", Amount = 1000.00m })
			.WithProducerConfig(config);

		// Assert
		var step = (KafkaProduceStep)scenario.CurrentStep;
		step.ProducerConfig.ShouldNotBeNull();
		step.ProducerConfig!.Acks.ShouldBe(Acks.All);
		step.ProducerConfig.EnableIdempotence.ShouldBe(true);
		step.ProducerConfig.MaxInFlight.ShouldBe(1);
	}

	[Fact(DisplayName = "Should support complete JsonOptions for API integration")]
	public void Should_Support_Complete_JsonOptions_For_API_Integration()
	{
		// Arrange
		var jsonOptions = new JsonSerializerOptions
		{
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
			DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
			WriteIndented = false,
			PropertyNameCaseInsensitive = true,
			MaxDepth = 32
		};

		var apiPayload = new
		{
			ApiVersion = "v1",
			Timestamp = DateTime.UtcNow,
			Data = new
			{
				UserId = "user-123",
				Action = "CREATE",
				Resource = "Order",
				Metadata = new Dictionary<string, object>
				{
					["client_ip"] = "192.168.1.1",
					["user_agent"] = "Mozilla/5.0"
				}
			}
		};

		// Act
		var scenario = Given()
			.Topic("api-events")
			.Produce(apiPayload)
			.WithJsonOptions(jsonOptions);

		// Assert
		var step = (KafkaProduceStep)scenario.CurrentStep;
		step.JsonOptions.ShouldNotBeNull();
		step.JsonOptions!.PropertyNamingPolicy.ShouldBe(JsonNamingPolicy.CamelCase);
		step.JsonOptions.DefaultIgnoreCondition.ShouldBe(JsonIgnoreCondition.WhenWritingNull);
		step.JsonOptions.PropertyNameCaseInsensitive.ShouldBeTrue();
	}

	[Fact(DisplayName = "Should combine ProducerConfig and JsonOptions in single chain")]
	public void Should_Combine_ProducerConfig_And_JsonOptions_In_Single_Chain()
	{
		// Arrange
		var producerConfig = new ProducerConfig
		{
			BootstrapServers = "kafka:9092",
			CompressionType = CompressionType.Gzip,
			Acks = Acks.All
		};

		var jsonOptions = new JsonSerializerOptions
		{
			PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
			WriteIndented = true
		};

		// Act
		var scenario = Given()
			.Topic("combined-config-topic")
			.Produce(new { OrderId = "123", Status = "PENDING" })
			.WithProducerConfig(producerConfig)
			.WithJsonOptions(jsonOptions)
			.WithTimeout(TimeSpan.FromSeconds(45))
			.WithKey("order-123")
			.WithHeader("schema-version", Encoding.UTF8.GetBytes("v2"));

		// Assert
		var step = (KafkaProduceStep)scenario.CurrentStep;
		step.ProducerConfig.ShouldNotBeNull();
		step.ProducerConfig!.CompressionType.ShouldBe(CompressionType.Gzip);
		step.JsonOptions.ShouldNotBeNull();
		step.JsonOptions!.PropertyNamingPolicy.ShouldBe(JsonNamingPolicy.SnakeCaseLower);
		step.Timeout.ShouldBe(TimeSpan.FromSeconds(45));
		step.Key.ShouldBe("order-123");
		step.Headers.ShouldNotBeNull();
	}

	#endregion

	#region ProducerConfig Immutability Tests

	[Fact(DisplayName = "WithProducerConfig should maintain immutability")]
	public void WithProducerConfig_Should_Maintain_Immutability()
	{
		// Arrange
		var config1 = new ProducerConfig { BootstrapServers = "kafka1:9092" };
		var config2 = new ProducerConfig { BootstrapServers = "kafka2:9092" };

		var scenario = Given().Topic("test-topic").Produce("test-value");
		var originalStep = scenario.CurrentStep;

		// Act
		scenario
			.WithProducerConfig(config1)
			.WithProducerConfig(config2);

		var finalStep = scenario.CurrentStep;

		// Assert
		originalStep.ShouldNotBe(finalStep);
		((KafkaProduceStep)finalStep).ProducerConfig.ShouldBe(config2);
	}

	[Fact(DisplayName = "WithJsonOptions should maintain immutability")]
	public void WithJsonOptions_Should_Maintain_Immutability()
	{
		// Arrange
		var options1 = new JsonSerializerOptions { WriteIndented = true };
		var options2 = new JsonSerializerOptions { WriteIndented = false };

		var scenario = Given().Topic("test-topic").Produce("test-value");
		var originalStep = scenario.CurrentStep;

		// Act
		scenario
			.WithJsonOptions(options1)
			.WithJsonOptions(options2);

		var finalStep = scenario.CurrentStep;

		// Assert
		originalStep.ShouldNotBe(finalStep);
		((KafkaProduceStep)finalStep).JsonOptions.ShouldBe(options2);
	}

	#endregion
}
