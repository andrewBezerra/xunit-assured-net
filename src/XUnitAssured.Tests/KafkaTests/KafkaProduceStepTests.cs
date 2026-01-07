using System.Text;
using System.Text.Json;
using Confluent.Kafka;
using XUnitAssured.Kafka.Steps;

namespace XUnitAssured.Tests.KafkaTests;

[Trait("Category", "Kafka")]
[Trait("Component", "ProduceStep")]
/// <summary>
/// Unit tests for KafkaProduceStep and Kafka Produce DSL.
/// Note: These are unit tests for the step structure, not integration tests.
/// Integration tests with real Kafka require a running Kafka instance.
/// </summary>
public class KafkaProduceStepTests
{
	#region Basic Properties Tests

	[Fact(DisplayName = "KafkaProduceStep should have correct StepType value")]
	public void KafkaProduceStep_Should_Have_Correct_StepType()
	{
		// Arrange & Act
		var step = new KafkaProduceStep
		{
			Topic = "test-topic",
			Value = "test-value"
		};

		// Assert
		step.StepType.ShouldBe("Kafka");
	}

	[Fact(DisplayName = "KafkaProduceStep should store topic name correctly")]
	public void KafkaProduceStep_Should_Store_Topic()
	{
		// Arrange & Act
		var step = new KafkaProduceStep
		{
			Topic = "my-topic",
			Value = "test"
		};

		// Assert
		step.Topic.ShouldBe("my-topic");
	}

	[Fact(DisplayName = "KafkaProduceStep should store key correctly")]
	public void KafkaProduceStep_Should_Store_Key()
	{
		// Arrange & Act
		var step = new KafkaProduceStep
		{
			Topic = "test-topic",
			Key = "my-key",
			Value = "test-value"
		};

		// Assert
		step.Key.ShouldBe("my-key");
	}

	[Fact(DisplayName = "KafkaProduceStep should store value correctly")]
	public void KafkaProduceStep_Should_Store_Value()
	{
		// Arrange
		var testValue = new { Id = 1, Name = "Test" };

		// Act
		var step = new KafkaProduceStep
		{
			Topic = "test-topic",
			Value = testValue
		};

		// Assert
		step.Value.ShouldBe(testValue);
	}

	[Fact(DisplayName = "KafkaProduceStep should have default timeout of 30 seconds")]
	public void KafkaProduceStep_Should_Have_Default_Timeout()
	{
		// Arrange & Act
		var step = new KafkaProduceStep
		{
			Topic = "test-topic",
			Value = "test"
		};

		// Assert
		step.Timeout.ShouldBe(TimeSpan.FromSeconds(30));
	}

	[Fact(DisplayName = "KafkaProduceStep should allow custom timeout configuration")]
	public void KafkaProduceStep_Should_Allow_Custom_Timeout()
	{
		// Arrange & Act
		var step = new KafkaProduceStep
		{
			Topic = "test-topic",
			Value = "test",
			Timeout = TimeSpan.FromSeconds(60)
		};

		// Assert
		step.Timeout.ShouldBe(TimeSpan.FromSeconds(60));
	}

	[Fact(DisplayName = "KafkaProduceStep should have default bootstrap servers")]
	public void KafkaProduceStep_Should_Have_Default_BootstrapServers()
	{
		// Arrange & Act
		var step = new KafkaProduceStep
		{
			Topic = "test-topic",
			Value = "test"
		};

		// Assert
		step.BootstrapServers.ShouldBe("localhost:9092");
	}

	[Fact(DisplayName = "KafkaProduceStep should store headers correctly")]
	public void KafkaProduceStep_Should_Store_Headers()
	{
		// Arrange
		var headers = new Headers();
		headers.Add("correlation-id", Encoding.UTF8.GetBytes("123"));

		// Act
		var step = new KafkaProduceStep
		{
			Topic = "test-topic",
			Value = "test",
			Headers = headers
		};

		// Assert
		step.Headers.ShouldNotBeNull();
		step.Headers.Count.ShouldBe(1);
	}

	[Fact(DisplayName = "KafkaProduceStep should store partition correctly")]
	public void KafkaProduceStep_Should_Store_Partition()
	{
		// Arrange & Act
		var step = new KafkaProduceStep
		{
			Topic = "test-topic",
			Value = "test",
			Partition = 2
		};

		// Assert
		step.Partition.ShouldBe(2);
	}

	[Fact(DisplayName = "KafkaProduceStep should store timestamp correctly")]
	public void KafkaProduceStep_Should_Store_Timestamp()
	{
		// Arrange
		var timestamp = DateTime.UtcNow;

		// Act
		var step = new KafkaProduceStep
		{
			Topic = "test-topic",
			Value = "test",
			Timestamp = timestamp
		};

		// Assert
		step.Timestamp.ShouldBe(timestamp);
	}

	[Fact(DisplayName = "KafkaProduceStep should store JsonOptions correctly")]
	public void KafkaProduceStep_Should_Store_JsonOptions()
	{
		// Arrange
		var jsonOptions = new JsonSerializerOptions
		{
			PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
		};

		// Act
		var step = new KafkaProduceStep
		{
			Topic = "test-topic",
			Value = "test",
			JsonOptions = jsonOptions
		};

		// Assert
		step.JsonOptions.ShouldBe(jsonOptions);
	}

	#endregion

	#region DSL Extension Tests

	[Fact(DisplayName = "Produce extension should create KafkaProduceStep with value only")]
	public void Produce_Extension_Should_Create_ProduceStep_With_Value()
	{
		// Arrange
		var scenario = Given().Topic("my-topic");
		var value = new { Id = 1, Name = "Test" };

		// Act
		scenario.Produce(value);

		// Assert
		scenario.CurrentStep.ShouldNotBeNull();
		scenario.CurrentStep.ShouldBeOfType<KafkaProduceStep>();
		var step = (KafkaProduceStep)scenario.CurrentStep;
		step.Topic.ShouldBe("my-topic");
		step.Value.ShouldBe(value);
		step.Key.ShouldBeNull();
	}

	[Fact(DisplayName = "Produce extension should create KafkaProduceStep with key and value")]
	public void Produce_Extension_Should_Create_ProduceStep_With_Key_And_Value()
	{
		// Arrange
		var scenario = Given().Topic("my-topic");
		var key = "order-123";
		var value = new { OrderId = "123", Amount = 99.99 };

		// Act
		scenario.Produce(key, value);

		// Assert
		scenario.CurrentStep.ShouldNotBeNull();
		scenario.CurrentStep.ShouldBeOfType<KafkaProduceStep>();
		var step = (KafkaProduceStep)scenario.CurrentStep;
		step.Topic.ShouldBe("my-topic");
		step.Key.ShouldBe(key);
		step.Value.ShouldBe(value);
	}

	[Fact(DisplayName = "Produce extension should throw InvalidOperationException when no topic specified")]
	public void Produce_Extension_Should_Throw_When_No_Topic()
	{
		// Arrange
		var scenario = Given();

		// Act & Assert
		Should.Throw<InvalidOperationException>(() => scenario.Produce("test"));
	}

	[Fact(DisplayName = "Topic extension should store topic in context for Produce")]
	public void Topic_Extension_Should_Store_Topic_In_Context_For_Produce()
	{
		// Arrange
		var scenario = Given();

		// Act
		scenario.Topic("my-topic").Produce("test-value");

		// Assert
		var step = (KafkaProduceStep)scenario.CurrentStep;
		step.Topic.ShouldBe("my-topic");
	}

	#endregion

	#region Key and Headers Configuration Tests

	[Fact(DisplayName = "WithKey should update key in KafkaProduceStep")]
	public void WithKey_Should_Update_Key()
	{
		// Arrange
		var scenario = Given().Topic("test-topic").Produce("test-value");

		// Act
		scenario.WithKey("my-key");

		// Assert
		var step = (KafkaProduceStep)scenario.CurrentStep;
		step.Key.ShouldBe("my-key");
	}

	[Fact(DisplayName = "WithKey should throw InvalidOperationException for non-produce step")]
	public void WithKey_Should_Throw_For_Non_Produce_Step()
	{
		// Arrange
		var scenario = Given().Topic("test-topic").Consume();

		// Act & Assert
		Should.Throw<InvalidOperationException>(() => scenario.WithKey("key"));
	}

	[Fact(DisplayName = "WithHeaders should update headers in KafkaProduceStep")]
	public void WithHeaders_Should_Update_Headers()
	{
		// Arrange
		var scenario = Given().Topic("test-topic").Produce("test-value");
		var headers = new Headers();
		headers.Add("correlation-id", Encoding.UTF8.GetBytes("123"));

		// Act
		scenario.WithHeaders(headers);

		// Assert
		var step = (KafkaProduceStep)scenario.CurrentStep;
		step.Headers.ShouldBe(headers);
	}

	[Fact(DisplayName = "WithHeader should add single header to KafkaProduceStep")]
	public void WithHeader_Should_Add_Single_Header()
	{
		// Arrange
		var scenario = Given().Topic("test-topic").Produce("test-value");
		var headerValue = Encoding.UTF8.GetBytes("correlation-123");

		// Act
		scenario.WithHeader("correlation-id", headerValue);

		// Assert
		var step = (KafkaProduceStep)scenario.CurrentStep;
		step.Headers.ShouldNotBeNull();
		step.Headers.Count.ShouldBe(1);
		var header = step.Headers[0];
		header.Key.ShouldBe("correlation-id");
		Encoding.UTF8.GetString(header.GetValueBytes()).ShouldBe("correlation-123");
	}

	[Fact(DisplayName = "WithHeader should append multiple headers")]
	public void WithHeader_Should_Append_Multiple_Headers()
	{
		// Arrange
		var scenario = Given().Topic("test-topic").Produce("test-value");

		// Act
		scenario
			.WithHeader("correlation-id", Encoding.UTF8.GetBytes("123"))
			.WithHeader("event-type", Encoding.UTF8.GetBytes("OrderCreated"));

		// Assert
		var step = (KafkaProduceStep)scenario.CurrentStep;
		step.Headers.ShouldNotBeNull();
		step.Headers.Count.ShouldBe(2);
	}

	[Fact(DisplayName = "WithHeader should throw InvalidOperationException for non-produce step")]
	public void WithHeader_Should_Throw_For_Non_Produce_Step()
	{
		// Arrange
		var scenario = Given().Topic("test-topic").Consume();

		// Act & Assert
		Should.Throw<InvalidOperationException>(() => 
			scenario.WithHeader("key", Encoding.UTF8.GetBytes("value")));
	}

	#endregion

	#region Partition and Timestamp Configuration Tests

	[Fact(DisplayName = "WithPartition should update partition in KafkaProduceStep")]
	public void WithPartition_Should_Update_Partition()
	{
		// Arrange
		var scenario = Given().Topic("test-topic").Produce("test-value");

		// Act
		scenario.WithPartition(2);

		// Assert
		var step = (KafkaProduceStep)scenario.CurrentStep;
		step.Partition.ShouldBe(2);
	}

	[Fact(DisplayName = "WithPartition should throw InvalidOperationException for non-produce step")]
	public void WithPartition_Should_Throw_For_Non_Produce_Step()
	{
		// Arrange
		var scenario = Given().Topic("test-topic").Consume();

		// Act & Assert
		Should.Throw<InvalidOperationException>(() => scenario.WithPartition(0));
	}

	[Fact(DisplayName = "WithTimestamp should update timestamp in KafkaProduceStep")]
	public void WithTimestamp_Should_Update_Timestamp()
	{
		// Arrange
		var scenario = Given().Topic("test-topic").Produce("test-value");
		var timestamp = DateTime.UtcNow.AddHours(-1);

		// Act
		scenario.WithTimestamp(timestamp);

		// Assert
		var step = (KafkaProduceStep)scenario.CurrentStep;
		step.Timestamp.ShouldBe(timestamp);
	}

	[Fact(DisplayName = "WithTimestamp should throw InvalidOperationException for non-produce step")]
	public void WithTimestamp_Should_Throw_For_Non_Produce_Step()
	{
		// Arrange
		var scenario = Given().Topic("test-topic").Consume();

		// Act & Assert
		Should.Throw<InvalidOperationException>(() => scenario.WithTimestamp(DateTime.UtcNow));
	}

	#endregion

	#region ProducerConfig and JsonOptions Tests

	[Fact(DisplayName = "WithProducerConfig should update producer config in KafkaProduceStep")]
	public void WithProducerConfig_Should_Update_Config()
	{
		// Arrange
		var scenario = Given().Topic("test-topic").Produce("test-value");
		var config = new ProducerConfig
		{
			BootstrapServers = "kafka:9092",
			MessageMaxBytes = 1048576
		};

		// Act
		scenario.WithProducerConfig(config);

		// Assert
		var step = (KafkaProduceStep)scenario.CurrentStep;
		step.ProducerConfig.ShouldBe(config);
	}

	[Fact(DisplayName = "WithProducerConfig should throw InvalidOperationException for non-produce step")]
	public void WithProducerConfig_Should_Throw_For_Non_Produce_Step()
	{
		// Arrange
		var scenario = Given().Topic("test-topic").Consume();
		var config = new ProducerConfig();

		// Act & Assert
		Should.Throw<InvalidOperationException>(() => scenario.WithProducerConfig(config));
	}

	[Fact(DisplayName = "WithJsonOptions should update JSON options in KafkaProduceStep")]
	public void WithJsonOptions_Should_Update_JsonOptions()
	{
		// Arrange
		var scenario = Given().Topic("test-topic").Produce("test-value");
		var jsonOptions = new JsonSerializerOptions
		{
			PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
		};

		// Act
		scenario.WithJsonOptions(jsonOptions);

		// Assert
		var step = (KafkaProduceStep)scenario.CurrentStep;
		step.JsonOptions.ShouldBe(jsonOptions);
	}

	[Fact(DisplayName = "WithJsonOptions should throw InvalidOperationException for non-produce step")]
	public void WithJsonOptions_Should_Throw_For_Non_Produce_Step()
	{
		// Arrange
		var scenario = Given().Topic("test-topic").Consume();
		var jsonOptions = new JsonSerializerOptions();

		// Act & Assert
		Should.Throw<InvalidOperationException>(() => scenario.WithJsonOptions(jsonOptions));
	}

	#endregion

	#region Timeout and BootstrapServers Tests

	[Fact(DisplayName = "WithTimeout should update timeout in KafkaProduceStep")]
	public void WithTimeout_Should_Update_Timeout_For_Produce()
	{
		// Arrange
		var scenario = Given().Topic("test-topic").Produce("test-value");

		// Act
		scenario.WithTimeout(TimeSpan.FromSeconds(60));

		// Assert
		var step = (KafkaProduceStep)scenario.CurrentStep;
		step.Timeout.ShouldBe(TimeSpan.FromSeconds(60));
	}

	[Fact(DisplayName = "WithBootstrapServers should update bootstrap servers in KafkaProduceStep")]
	public void WithBootstrapServers_Should_Update_BootstrapServers_For_Produce()
	{
		// Arrange
		var scenario = Given().Topic("test-topic").Produce("test-value");

		// Act
		scenario.WithBootstrapServers("kafka:9092");

		// Assert
		var step = (KafkaProduceStep)scenario.CurrentStep;
		step.BootstrapServers.ShouldBe("kafka:9092");
	}

	#endregion

	#region Fluent DSL Integration Tests

	[Fact(DisplayName = "Kafka Produce DSL should chain fluently with all configuration methods")]
	public void Kafka_Produce_DSL_Should_Chain_Fluently()
	{
		// Arrange
		var timestamp = DateTime.UtcNow;
		var jsonOptions = new JsonSerializerOptions
		{
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase
		};

		// Act
		var scenario = Given()
			.Topic("test-topic")
			.Produce("order-123", new { OrderId = "123", Amount = 99.99 })
			.WithHeader("correlation-id", Encoding.UTF8.GetBytes("abc-123"))
			.WithPartition(2)
			.WithTimestamp(timestamp)
			.WithTimeout(TimeSpan.FromSeconds(45))
			.WithBootstrapServers("kafka:9092")
			.WithJsonOptions(jsonOptions);

		// Assert
		scenario.ShouldNotBeNull();
		var step = (KafkaProduceStep)scenario.CurrentStep;
		step.Topic.ShouldBe("test-topic");
		step.Key.ShouldBe("order-123");
		step.Value.ShouldNotBeNull();
		step.Headers.ShouldNotBeNull();
		step.Headers.Count.ShouldBe(1);
		step.Partition.ShouldBe(2);
		step.Timestamp.ShouldBe(timestamp);
		step.Timeout.ShouldBe(TimeSpan.FromSeconds(45));
		step.BootstrapServers.ShouldBe("kafka:9092");
		step.JsonOptions.ShouldBe(jsonOptions);
	}

	[Fact(DisplayName = "Kafka DSL should support produce and consume in same scenario")]
	public void Kafka_DSL_Should_Support_Produce_And_Consume()
	{
		// Act
		var scenario = Given()
			.Topic("orders")
			.Produce(new { OrderId = "123" })
		.When()
			.And()
		.Then()
			.Topic("order-confirmations")
			.Consume();

		// Assert
		scenario.ShouldNotBeNull();
		// Last step should be consume
		scenario.CurrentStep.ShouldBeOfType<KafkaConsumeStep>();
	}

	[Fact(DisplayName = "Kafka DSL should maintain immutability pattern for all With methods")]
	public void Kafka_DSL_Should_Maintain_Immutability()
	{
		// Arrange
		var scenario = Given().Topic("test-topic").Produce("test");
		var originalStep = scenario.CurrentStep;

		// Act
		scenario.WithKey("new-key");
		var newStep = scenario.CurrentStep;

		// Assert
		originalStep.ShouldNotBe(newStep);
		((KafkaProduceStep)originalStep).Key.ShouldBeNull();
		((KafkaProduceStep)newStep).Key.ShouldBe("new-key");
	}

	[Fact(DisplayName = "Produce with string value should store value correctly")]
	public void Produce_With_String_Should_Store_Correctly()
	{
		// Arrange & Act
		var scenario = Given()
			.Topic("test-topic")
			.Produce("simple string message");

		// Assert
		var step = (KafkaProduceStep)scenario.CurrentStep;
		step.Value.ShouldBe("simple string message");
	}

	[Fact(DisplayName = "Produce with object value should store value correctly")]
	public void Produce_With_Object_Should_Store_Correctly()
	{
		// Arrange
		var order = new { OrderId = "123", Amount = 99.99m, Status = "Pending" };

		// Act
		var scenario = Given()
			.Topic("orders")
			.Produce(order);

		// Assert
		var step = (KafkaProduceStep)scenario.CurrentStep;
		step.Value.ShouldBe(order);
	}

	[Fact(DisplayName = "Produce with complex key should store key correctly")]
	public void Produce_With_Complex_Key_Should_Store_Correctly()
	{
		// Arrange
		var key = new { TenantId = "ABC", OrderId = "123" };
		var value = new { Amount = 99.99 };

		// Act
		var scenario = Given()
			.Topic("orders")
			.Produce(key, value);

		// Assert
		var step = (KafkaProduceStep)scenario.CurrentStep;
		step.Key.ShouldBe(key);
		step.Value.ShouldBe(value);
	}

	#endregion
}
