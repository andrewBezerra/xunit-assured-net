using Confluent.Kafka;
using XUnitAssured.Kafka.Steps;

namespace XUnitAssured.Tests.KafkaTests;

[Trait("Category", "Kafka")]
[Trait("Component", "ProducerConfig")]
/// <summary>
/// Unit tests for Kafka ProducerConfig advanced features.
/// Tests compression, ACKs, batch/buffer, retry policies, and timeout configurations.
/// </summary>
public class ProducerConfigTests
{
	#region Compression Tests

	[Fact(DisplayName = "ProducerConfig with Gzip compression should be applied correctly")]
	public void ProducerConfig_With_Gzip_Compression()
	{
		// Arrange
		var config = new ProducerConfig
		{
			BootstrapServers = "localhost:9092",
			CompressionType = CompressionType.Gzip,
			CompressionLevel = 6
		};

		// Act
		var scenario = Given()
			.Topic("test-topic")
			.Produce("test-value")
			.WithProducerConfig(config);

		// Assert
		var step = (KafkaProduceStep)scenario.CurrentStep;
		step.ProducerConfig.ShouldNotBeNull();
		step.ProducerConfig.CompressionType.ShouldBe(CompressionType.Gzip);
		step.ProducerConfig.CompressionLevel.ShouldBe(6);
	}

	[Fact(DisplayName = "ProducerConfig with Snappy compression should be applied correctly")]
	public void ProducerConfig_With_Snappy_Compression()
	{
		// Arrange
		var config = new ProducerConfig
		{
			BootstrapServers = "localhost:9092",
			CompressionType = CompressionType.Snappy
		};

		// Act
		var scenario = Given()
			.Topic("test-topic")
			.Produce("test-value")
			.WithProducerConfig(config);

		// Assert
		var step = (KafkaProduceStep)scenario.CurrentStep;
		step.ProducerConfig.ShouldNotBeNull();
		step.ProducerConfig.CompressionType.ShouldBe(CompressionType.Snappy);
	}

	[Fact(DisplayName = "ProducerConfig with Lz4 compression should be applied correctly")]
	public void ProducerConfig_With_Lz4_Compression()
	{
		// Arrange
		var config = new ProducerConfig
		{
			BootstrapServers = "localhost:9092",
			CompressionType = CompressionType.Lz4
		};

		// Act
		var scenario = Given()
			.Topic("test-topic")
			.Produce("test-value")
			.WithProducerConfig(config);

		// Assert
		var step = (KafkaProduceStep)scenario.CurrentStep;
		step.ProducerConfig.ShouldNotBeNull();
		step.ProducerConfig.CompressionType.ShouldBe(CompressionType.Lz4);
	}

	[Fact(DisplayName = "ProducerConfig with Zstd compression should be applied correctly")]
	public void ProducerConfig_With_Zstd_Compression()
	{
		// Arrange
		var config = new ProducerConfig
		{
			BootstrapServers = "localhost:9092",
			CompressionType = CompressionType.Zstd
		};

		// Act
		var scenario = Given()
			.Topic("test-topic")
			.Produce("test-value")
			.WithProducerConfig(config);

		// Assert
		var step = (KafkaProduceStep)scenario.CurrentStep;
		step.ProducerConfig.ShouldNotBeNull();
		step.ProducerConfig.CompressionType.ShouldBe(CompressionType.Zstd);
	}

	[Fact(DisplayName = "ProducerConfig with no compression should default to None")]
	public void ProducerConfig_With_No_Compression()
	{
		// Arrange
		var config = new ProducerConfig
		{
			BootstrapServers = "localhost:9092",
			CompressionType = CompressionType.None
		};

		// Act
		var scenario = Given()
			.Topic("test-topic")
			.Produce("test-value")
			.WithProducerConfig(config);

		// Assert
		var step = (KafkaProduceStep)scenario.CurrentStep;
		step.ProducerConfig.ShouldNotBeNull();
		step.ProducerConfig.CompressionType.ShouldBe(CompressionType.None);
	}

	#endregion

	#region ACKs Configuration Tests

	[Fact(DisplayName = "ProducerConfig with Acks.All should wait for all replicas")]
	public void ProducerConfig_With_Acks_All()
	{
		// Arrange
		var config = new ProducerConfig
		{
			BootstrapServers = "localhost:9092",
			Acks = Acks.All,
			RequestTimeoutMs = 30000
		};

		// Act
		var scenario = Given()
			.Topic("replicated-topic")
			.Produce("important-data")
			.WithProducerConfig(config);

		// Assert
		var step = (KafkaProduceStep)scenario.CurrentStep;
		step.ProducerConfig.ShouldNotBeNull();
		step.ProducerConfig.Acks.ShouldBe(Acks.All);
		step.ProducerConfig.RequestTimeoutMs.ShouldBe(30000);
	}

	[Fact(DisplayName = "ProducerConfig with Acks.Leader should wait only for leader")]
	public void ProducerConfig_With_Acks_Leader()
	{
		// Arrange
		var config = new ProducerConfig
		{
			BootstrapServers = "localhost:9092",
			Acks = Acks.Leader
		};

		// Act
		var scenario = Given()
			.Topic("test-topic")
			.Produce("test-data")
			.WithProducerConfig(config);

		// Assert
		var step = (KafkaProduceStep)scenario.CurrentStep;
		step.ProducerConfig.ShouldNotBeNull();
		step.ProducerConfig.Acks.ShouldBe(Acks.Leader);
	}

	[Fact(DisplayName = "ProducerConfig with Acks.None should fire and forget")]
	public void ProducerConfig_With_Acks_None()
	{
		// Arrange
		var config = new ProducerConfig
		{
			BootstrapServers = "localhost:9092",
			Acks = Acks.None
		};

		// Act
		var scenario = Given()
			.Topic("fire-forget-topic")
			.Produce("non-critical-data")
			.WithProducerConfig(config);

		// Assert
		var step = (KafkaProduceStep)scenario.CurrentStep;
		step.ProducerConfig.ShouldNotBeNull();
		step.ProducerConfig.Acks.ShouldBe(Acks.None);
	}

	#endregion

	#region Batch and Buffer Configuration Tests

	[Fact(DisplayName = "ProducerConfig with batch size configuration should be applied")]
	public void ProducerConfig_With_Batch_Size()
	{
		// Arrange
		var config = new ProducerConfig
		{
			BootstrapServers = "localhost:9092",
			BatchSize = 16384,  // 16KB
			LingerMs = 10,      // Wait 10ms to batch
			BatchNumMessages = 100
		};

		// Act
		var scenario = Given()
			.Topic("batch-topic")
			.Produce("message-1")
			.WithProducerConfig(config);

		// Assert
		var step = (KafkaProduceStep)scenario.CurrentStep;
		step.ProducerConfig.ShouldNotBeNull();
		step.ProducerConfig.BatchSize.ShouldBe(16384);
		step.ProducerConfig.LingerMs.ShouldBe(10);
		step.ProducerConfig.BatchNumMessages.ShouldBe(100);
	}

	[Fact(DisplayName = "ProducerConfig with larger batch size should allow more batching")]
	public void ProducerConfig_With_Larger_Batch_Size()
	{
		// Arrange
		var config = new ProducerConfig
		{
			BootstrapServers = "localhost:9092",
			BatchSize = 32768,  // 32KB
			LingerMs = 100      // Wait longer for more batching
		};

		// Act
		var scenario = Given()
			.Topic("large-batch-topic")
			.Produce("bulk-message")
			.WithProducerConfig(config);

		// Assert
		var step = (KafkaProduceStep)scenario.CurrentStep;
		step.ProducerConfig.ShouldNotBeNull();
		step.ProducerConfig.BatchSize.ShouldBe(32768);
		step.ProducerConfig.LingerMs.ShouldBe(100);
	}

	[Fact(DisplayName = "ProducerConfig with buffer memory configuration should be applied")]
	public void ProducerConfig_With_Buffer_Memory()
	{
		// Arrange
		var config = new ProducerConfig
		{
			BootstrapServers = "localhost:9092",
			QueueBufferingMaxMessages = 10000,
			QueueBufferingMaxKbytes = 1048576  // 1GB
		};

		// Act
		var scenario = Given()
			.Topic("buffered-topic")
			.Produce("buffered-message")
			.WithProducerConfig(config);

		// Assert
		var step = (KafkaProduceStep)scenario.CurrentStep;
		step.ProducerConfig.ShouldNotBeNull();
		step.ProducerConfig.QueueBufferingMaxMessages.ShouldBe(10000);
		step.ProducerConfig.QueueBufferingMaxKbytes.ShouldBe(1048576);
	}

	[Fact(DisplayName = "ProducerConfig with immediate send should have zero linger")]
	public void ProducerConfig_With_Immediate_Send()
	{
		// Arrange
		var config = new ProducerConfig
		{
			BootstrapServers = "localhost:9092",
			LingerMs = 0,  // Send immediately
			BatchSize = 1  // Minimal batching
		};

		// Act
		var scenario = Given()
			.Topic("immediate-topic")
			.Produce("urgent-message")
			.WithProducerConfig(config);

		// Assert
		var step = (KafkaProduceStep)scenario.CurrentStep;
		step.ProducerConfig.ShouldNotBeNull();
		step.ProducerConfig.LingerMs.ShouldBe(0);
		step.ProducerConfig.BatchSize.ShouldBe(1);
	}

	#endregion

	#region Retry and Timeout Configuration Tests

	[Fact(DisplayName = "ProducerConfig with retry configuration should be applied")]
	public void ProducerConfig_With_Retry_Configuration()
	{
		// Arrange
		var config = new ProducerConfig
		{
			BootstrapServers = "localhost:9092",
			MessageSendMaxRetries = 3,
			RetryBackoffMs = 100,
			RequestTimeoutMs = 5000
		};

		// Act
		var scenario = Given()
			.Topic("retry-topic")
			.Produce("retry-test")
			.WithProducerConfig(config);

		// Assert
		var step = (KafkaProduceStep)scenario.CurrentStep;
		step.ProducerConfig.ShouldNotBeNull();
		step.ProducerConfig.MessageSendMaxRetries.ShouldBe(3);
		step.ProducerConfig.RetryBackoffMs.ShouldBe(100);
		step.ProducerConfig.RequestTimeoutMs.ShouldBe(5000);
	}

	[Fact(DisplayName = "ProducerConfig with higher retry count should allow more attempts")]
	public void ProducerConfig_With_Higher_Retry_Count()
	{
		// Arrange
		var config = new ProducerConfig
		{
			BootstrapServers = "localhost:9092",
			MessageSendMaxRetries = 10,
			RetryBackoffMs = 500
		};

		// Act
		var scenario = Given()
			.Topic("resilient-topic")
			.Produce("resilient-message")
			.WithProducerConfig(config);

		// Assert
		var step = (KafkaProduceStep)scenario.CurrentStep;
		step.ProducerConfig.ShouldNotBeNull();
		step.ProducerConfig.MessageSendMaxRetries.ShouldBe(10);
		step.ProducerConfig.RetryBackoffMs.ShouldBe(500);
	}

	[Fact(DisplayName = "ProducerConfig with custom request timeout should be applied")]
	public void ProducerConfig_With_Custom_Request_Timeout()
	{
		// Arrange
		var config = new ProducerConfig
		{
			BootstrapServers = "localhost:9092",
			RequestTimeoutMs = 10000  // 10 seconds
		};

		// Act
		var scenario = Given()
			.Topic("timeout-topic")
			.Produce("timeout-test")
			.WithProducerConfig(config);

		// Assert
		var step = (KafkaProduceStep)scenario.CurrentStep;
		step.ProducerConfig.ShouldNotBeNull();
		step.ProducerConfig.RequestTimeoutMs.ShouldBe(10000);
	}

	[Fact(DisplayName = "ProducerConfig with no retries should fail fast")]
	public void ProducerConfig_With_No_Retries()
	{
		// Arrange
		var config = new ProducerConfig
		{
			BootstrapServers = "localhost:9092",
			MessageSendMaxRetries = 0
		};

		// Act
		var scenario = Given()
			.Topic("fail-fast-topic")
			.Produce("fail-fast-message")
			.WithProducerConfig(config);

		// Assert
		var step = (KafkaProduceStep)scenario.CurrentStep;
		step.ProducerConfig.ShouldNotBeNull();
		step.ProducerConfig.MessageSendMaxRetries.ShouldBe(0);
	}

	#endregion

	#region Idempotence and Transaction Tests

	[Fact(DisplayName = "ProducerConfig with idempotence enabled should be applied")]
	public void ProducerConfig_With_Idempotence_Enabled()
	{
		// Arrange
		var config = new ProducerConfig
		{
			BootstrapServers = "localhost:9092",
			EnableIdempotence = true,
			Acks = Acks.All,  // Required for idempotence
			MessageSendMaxRetries = 5
		};

		// Act
		var scenario = Given()
			.Topic("idempotent-topic")
			.Produce("exactly-once-message")
			.WithProducerConfig(config);

		// Assert
		var step = (KafkaProduceStep)scenario.CurrentStep;
		step.ProducerConfig.ShouldNotBeNull();
		step.ProducerConfig.EnableIdempotence.ShouldBe(true);
		step.ProducerConfig.Acks.ShouldBe(Acks.All);
	}

	[Fact(DisplayName = "ProducerConfig with transactional ID should be applied")]
	public void ProducerConfig_With_Transactional_Id()
	{
		// Arrange
		var config = new ProducerConfig
		{
			BootstrapServers = "localhost:9092",
			TransactionalId = "test-transaction-id",
			EnableIdempotence = true
		};

		// Act
		var scenario = Given()
			.Topic("transactional-topic")
			.Produce("transactional-message")
			.WithProducerConfig(config);

		// Assert
		var step = (KafkaProduceStep)scenario.CurrentStep;
		step.ProducerConfig.ShouldNotBeNull();
		step.ProducerConfig.TransactionalId.ShouldBe("test-transaction-id");
		step.ProducerConfig.EnableIdempotence.ShouldBe(true);
	}

	#endregion

	#region Message Size Configuration Tests

	[Fact(DisplayName = "ProducerConfig with max message size should be applied")]
	public void ProducerConfig_With_Max_Message_Size()
	{
		// Arrange
		var config = new ProducerConfig
		{
			BootstrapServers = "localhost:9092",
			MessageMaxBytes = 2097152  // 2MB
		};

		// Act
		var scenario = Given()
			.Topic("large-message-topic")
			.Produce("large-payload")
			.WithProducerConfig(config);

		// Assert
		var step = (KafkaProduceStep)scenario.CurrentStep;
		step.ProducerConfig.ShouldNotBeNull();
		step.ProducerConfig.MessageMaxBytes.ShouldBe(2097152);
	}

	[Fact(DisplayName = "ProducerConfig with smaller max message size should restrict payload")]
	public void ProducerConfig_With_Smaller_Max_Message_Size()
	{
		// Arrange
		var config = new ProducerConfig
		{
			BootstrapServers = "localhost:9092",
			MessageMaxBytes = 1024  // 1KB
		};

		// Act
		var scenario = Given()
			.Topic("small-message-topic")
			.Produce("small-payload")
			.WithProducerConfig(config);

		// Assert
		var step = (KafkaProduceStep)scenario.CurrentStep;
		step.ProducerConfig.ShouldNotBeNull();
		step.ProducerConfig.MessageMaxBytes.ShouldBe(1024);
	}

	#endregion

	#region Complex ProducerConfig Scenarios

	[Fact(DisplayName = "ProducerConfig with multiple settings should all be applied")]
	public void ProducerConfig_With_Multiple_Settings()
	{
		// Arrange
		var config = new ProducerConfig
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
			MessageMaxBytes = 5242880  // 5MB
		};

		// Act
		var scenario = Given()
			.Topic("complex-topic")
			.Produce("complex-message")
			.WithProducerConfig(config);

		// Assert
		var step = (KafkaProduceStep)scenario.CurrentStep;
		step.ProducerConfig.ShouldNotBeNull();
		step.ProducerConfig.BootstrapServers.ShouldBe("kafka-cluster:9092");
		step.ProducerConfig.CompressionType.ShouldBe(CompressionType.Gzip);
		step.ProducerConfig.CompressionLevel.ShouldBe(9);
		step.ProducerConfig.Acks.ShouldBe(Acks.All);
		step.ProducerConfig.BatchSize.ShouldBe(32768);
		step.ProducerConfig.LingerMs.ShouldBe(50);
		step.ProducerConfig.MessageSendMaxRetries.ShouldBe(5);
		step.ProducerConfig.RetryBackoffMs.ShouldBe(200);
		step.ProducerConfig.RequestTimeoutMs.ShouldBe(15000);
		step.ProducerConfig.EnableIdempotence.ShouldBe(true);
		step.ProducerConfig.MessageMaxBytes.ShouldBe(5242880);
	}

	#endregion
}
