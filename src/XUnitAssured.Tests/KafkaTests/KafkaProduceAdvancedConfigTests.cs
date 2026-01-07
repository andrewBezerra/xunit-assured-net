using System.Text;
using System.Text.Json;
using Confluent.Kafka;
using XUnitAssured.Kafka.Steps;

namespace XUnitAssured.Tests.KafkaTests;

[Trait("Category", "Kafka")]
[Trait("Component", "ProduceConfiguration")]
/// <summary>
/// Advanced unit tests for Kafka Produce configuration (Phase 2).
/// Tests for headers, keys, partitioning, timestamps, and JSON serialization.
/// </summary>
public class KafkaProduceAdvancedConfigTests
{
	#region WithKey Advanced Tests

	[Fact(DisplayName = "WithKey should accept string key")]
	public void WithKey_Should_Accept_String_Key()
	{
		// Arrange & Act
		var scenario = Given()
			.Topic("test-topic")
			.Produce("test-value")
			.WithKey("string-key");

		// Assert
		var step = (KafkaProduceStep)scenario.CurrentStep;
		step.Key.ShouldBe("string-key");
		step.Key.ShouldBeOfType<string>();
	}

	[Fact(DisplayName = "WithKey should accept integer key")]
	public void WithKey_Should_Accept_Integer_Key()
	{
		// Arrange & Act
		var scenario = Given()
			.Topic("test-topic")
			.Produce("test-value")
			.WithKey(12345);

		// Assert
		var step = (KafkaProduceStep)scenario.CurrentStep;
		step.Key.ShouldBe(12345);
		step.Key.ShouldBeOfType<int>();
	}

	[Fact(DisplayName = "WithKey should accept GUID key")]
	public void WithKey_Should_Accept_Guid_Key()
	{
		// Arrange
		var guid = Guid.NewGuid();

		// Act
		var scenario = Given()
			.Topic("test-topic")
			.Produce("test-value")
			.WithKey(guid);

		// Assert
		var step = (KafkaProduceStep)scenario.CurrentStep;
		step.Key.ShouldBe(guid);
	}

	[Fact(DisplayName = "WithKey should accept complex object key")]
	public void WithKey_Should_Accept_Complex_Object_Key()
	{
		// Arrange
		var complexKey = new { TenantId = "ABC", OrderId = "123", Region = "US-EAST" };

		// Act
		var scenario = Given()
			.Topic("test-topic")
			.Produce("test-value")
			.WithKey(complexKey);

		// Assert
		var step = (KafkaProduceStep)scenario.CurrentStep;
		step.Key.ShouldBe(complexKey);
	}

	[Fact(DisplayName = "WithKey should replace existing key")]
	public void WithKey_Should_Replace_Existing_Key()
	{
		// Arrange & Act
		var scenario = Given()
			.Topic("test-topic")
			.Produce("key-1", "test-value")
			.WithKey("key-2");

		// Assert
		var step = (KafkaProduceStep)scenario.CurrentStep;
		step.Key.ShouldBe("key-2");
	}

	[Fact(DisplayName = "WithKey should accept null key")]
	public void WithKey_Should_Accept_Null_Key()
	{
		// Arrange & Act
		var scenario = Given()
			.Topic("test-topic")
			.Produce("key-1", "test-value")
			.WithKey(null);

		// Assert
		var step = (KafkaProduceStep)scenario.CurrentStep;
		step.Key.ShouldBeNull();
	}

	#endregion

	#region WithHeaders Advanced Tests

	[Fact(DisplayName = "WithHeaders should replace all existing headers")]
	public void WithHeaders_Should_Replace_All_Existing_Headers()
	{
		// Arrange
		var oldHeaders = new Headers();
		oldHeaders.Add("old-header", Encoding.UTF8.GetBytes("old-value"));

		var newHeaders = new Headers();
		newHeaders.Add("new-header", Encoding.UTF8.GetBytes("new-value"));

		// Act
		var scenario = Given()
			.Topic("test-topic")
			.Produce("test-value")
			.WithHeaders(oldHeaders)
			.WithHeaders(newHeaders);

		// Assert
		var step = (KafkaProduceStep)scenario.CurrentStep;
		step.Headers.ShouldBe(newHeaders);
		step.Headers!.Count.ShouldBe(1);
		step.Headers[0].Key.ShouldBe("new-header");
	}

	[Fact(DisplayName = "WithHeaders should accept empty headers")]
	public void WithHeaders_Should_Accept_Empty_Headers()
	{
		// Arrange
		var emptyHeaders = new Headers();

		// Act
		var scenario = Given()
			.Topic("test-topic")
			.Produce("test-value")
			.WithHeaders(emptyHeaders);

		// Assert
		var step = (KafkaProduceStep)scenario.CurrentStep;
		step.Headers.ShouldNotBeNull();
		step.Headers!.Count.ShouldBe(0);
	}

	[Fact(DisplayName = "WithHeader should append multiple headers sequentially")]
	public void WithHeader_Should_Append_Multiple_Headers_Sequentially()
	{
		// Arrange & Act
		var scenario = Given()
			.Topic("test-topic")
			.Produce("test-value")
			.WithHeader("correlation-id", Encoding.UTF8.GetBytes("abc-123"))
			.WithHeader("event-type", Encoding.UTF8.GetBytes("OrderCreated"))
			.WithHeader("tenant-id", Encoding.UTF8.GetBytes("tenant-001"))
			.WithHeader("version", Encoding.UTF8.GetBytes("v1"));

		// Assert
		var step = (KafkaProduceStep)scenario.CurrentStep;
		step.Headers.ShouldNotBeNull();
		step.Headers!.Count.ShouldBe(4);
		step.Headers[0].Key.ShouldBe("correlation-id");
		step.Headers[1].Key.ShouldBe("event-type");
		step.Headers[2].Key.ShouldBe("tenant-id");
		step.Headers[3].Key.ShouldBe("version");
	}

	[Fact(DisplayName = "WithHeader should handle binary header values")]
	public void WithHeader_Should_Handle_Binary_Header_Values()
	{
		// Arrange
		var binaryData = new byte[] { 0x01, 0x02, 0x03, 0xFF, 0xFE };

		// Act
		var scenario = Given()
			.Topic("test-topic")
			.Produce("test-value")
			.WithHeader("binary-data", binaryData);

		// Assert
		var step = (KafkaProduceStep)scenario.CurrentStep;
		step.Headers.ShouldNotBeNull();
		var header = step.Headers![0];
		header.GetValueBytes().ShouldBe(binaryData);
	}

	[Fact(DisplayName = "WithHeader should handle empty header value")]
	public void WithHeader_Should_Handle_Empty_Header_Value()
	{
		// Arrange & Act
		var scenario = Given()
			.Topic("test-topic")
			.Produce("test-value")
			.WithHeader("empty-header", Array.Empty<byte>());

		// Assert
		var step = (KafkaProduceStep)scenario.CurrentStep;
		step.Headers.ShouldNotBeNull();
		step.Headers!.Count.ShouldBe(1);
		step.Headers[0].GetValueBytes().Length.ShouldBe(0);
	}

	[Fact(DisplayName = "WithHeader should handle very long header names")]
	public void WithHeader_Should_Handle_Long_Header_Names()
	{
		// Arrange
		var longHeaderName = new string('x', 1000);

		// Act
		var scenario = Given()
			.Topic("test-topic")
			.Produce("test-value")
			.WithHeader(longHeaderName, Encoding.UTF8.GetBytes("value"));

		// Assert
		var step = (KafkaProduceStep)scenario.CurrentStep;
		step.Headers.ShouldNotBeNull();
		step.Headers![0].Key.ShouldBe(longHeaderName);
		step.Headers[0].Key.Length.ShouldBe(1000);
	}

	[Fact(DisplayName = "WithHeader should allow duplicate header keys")]
	public void WithHeader_Should_Allow_Duplicate_Header_Keys()
	{
		// Arrange & Act
		var scenario = Given()
			.Topic("test-topic")
			.Produce("test-value")
			.WithHeader("duplicate", Encoding.UTF8.GetBytes("value1"))
			.WithHeader("duplicate", Encoding.UTF8.GetBytes("value2"));

		// Assert
		var step = (KafkaProduceStep)scenario.CurrentStep;
		step.Headers.ShouldNotBeNull();
		step.Headers!.Count.ShouldBe(2);
		step.Headers[0].Key.ShouldBe("duplicate");
		step.Headers[1].Key.ShouldBe("duplicate");
		Encoding.UTF8.GetString(step.Headers[0].GetValueBytes()).ShouldBe("value1");
		Encoding.UTF8.GetString(step.Headers[1].GetValueBytes()).ShouldBe("value2");
	}

	#endregion

	#region WithPartition Advanced Tests

	[Fact(DisplayName = "WithPartition should accept partition 0")]
	public void WithPartition_Should_Accept_Partition_Zero()
	{
		// Arrange & Act
		var scenario = Given()
			.Topic("test-topic")
			.Produce("test-value")
			.WithPartition(0);

		// Assert
		var step = (KafkaProduceStep)scenario.CurrentStep;
		step.Partition.ShouldBe(0);
	}

	[Fact(DisplayName = "WithPartition should accept high partition numbers")]
	public void WithPartition_Should_Accept_High_Partition_Numbers()
	{
		// Arrange & Act
		var scenario = Given()
			.Topic("test-topic")
			.Produce("test-value")
			.WithPartition(999);

		// Assert
		var step = (KafkaProduceStep)scenario.CurrentStep;
		step.Partition.ShouldBe(999);
	}

	[Fact(DisplayName = "WithPartition should replace existing partition")]
	public void WithPartition_Should_Replace_Existing_Partition()
	{
		// Arrange & Act
		var scenario = Given()
			.Topic("test-topic")
			.Produce("test-value")
			.WithPartition(1)
			.WithPartition(5);

		// Assert
		var step = (KafkaProduceStep)scenario.CurrentStep;
		step.Partition.ShouldBe(5);
	}

	#endregion

	#region WithTimestamp Advanced Tests

	[Fact(DisplayName = "WithTimestamp should accept past timestamp")]
	public void WithTimestamp_Should_Accept_Past_Timestamp()
	{
		// Arrange
		var pastTimestamp = DateTime.UtcNow.AddHours(-24);

		// Act
		var scenario = Given()
			.Topic("test-topic")
			.Produce("test-value")
			.WithTimestamp(pastTimestamp);

		// Assert
		var step = (KafkaProduceStep)scenario.CurrentStep;
		step.Timestamp.ShouldBe(pastTimestamp);
	}

	[Fact(DisplayName = "WithTimestamp should accept future timestamp")]
	public void WithTimestamp_Should_Accept_Future_Timestamp()
	{
		// Arrange
		var futureTimestamp = DateTime.UtcNow.AddHours(24);

		// Act
		var scenario = Given()
			.Topic("test-topic")
			.Produce("test-value")
			.WithTimestamp(futureTimestamp);

		// Assert
		var step = (KafkaProduceStep)scenario.CurrentStep;
		step.Timestamp.ShouldBe(futureTimestamp);
	}

	[Fact(DisplayName = "WithTimestamp should accept epoch timestamp")]
	public void WithTimestamp_Should_Accept_Epoch_Timestamp()
	{
		// Arrange
		var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

		// Act
		var scenario = Given()
			.Topic("test-topic")
			.Produce("test-value")
			.WithTimestamp(epoch);

		// Assert
		var step = (KafkaProduceStep)scenario.CurrentStep;
		step.Timestamp.ShouldBe(epoch);
	}

	[Fact(DisplayName = "WithTimestamp should replace existing timestamp")]
	public void WithTimestamp_Should_Replace_Existing_Timestamp()
	{
		// Arrange
		var timestamp1 = DateTime.UtcNow.AddHours(-1);
		var timestamp2 = DateTime.UtcNow;

		// Act
		var scenario = Given()
			.Topic("test-topic")
			.Produce("test-value")
			.WithTimestamp(timestamp1)
			.WithTimestamp(timestamp2);

		// Assert
		var step = (KafkaProduceStep)scenario.CurrentStep;
		step.Timestamp.ShouldBe(timestamp2);
	}

	#endregion

	#region JSON Serialization Tests

	[Fact(DisplayName = "Produce should handle nested object serialization")]
	public void Produce_Should_Handle_Nested_Object_Serialization()
	{
		// Arrange
		var nestedObject = new
		{
			OrderId = "123",
			Customer = new
			{
				Id = "C001",
				Name = "John Doe",
				Address = new
				{
					Street = "123 Main St",
					City = "New York",
					ZipCode = "10001"
				}
			},
			Items = new[]
			{
				new { ProductId = "P1", Quantity = 2 },
				new { ProductId = "P2", Quantity = 1 }
			}
		};

		// Act
		var scenario = Given()
			.Topic("test-topic")
			.Produce(nestedObject);

		// Assert
		var step = (KafkaProduceStep)scenario.CurrentStep;
		step.Value.ShouldBe(nestedObject);
	}

	[Fact(DisplayName = "Produce should handle array values")]
	public void Produce_Should_Handle_Array_Values()
	{
		// Arrange
		var arrayValue = new[] { 1, 2, 3, 4, 5 };

		// Act
		var scenario = Given()
			.Topic("test-topic")
			.Produce(arrayValue);

		// Assert
		var step = (KafkaProduceStep)scenario.CurrentStep;
		step.Value.ShouldBe(arrayValue);
	}

	[Fact(DisplayName = "Produce should handle null value")]
	public void Produce_Should_Handle_Null_Value()
	{
		// Arrange & Act
		var scenario = Given()
			.Topic("test-topic")
			.Produce(null!);

		// Assert
		var step = (KafkaProduceStep)scenario.CurrentStep;
		step.Value.ShouldBeNull();
	}

	[Fact(DisplayName = "WithJsonOptions should customize serialization")]
	public void WithJsonOptions_Should_Customize_Serialization()
	{
		// Arrange
		var customOptions = new JsonSerializerOptions
		{
			PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
			WriteIndented = true
		};

		// Act
		var scenario = Given()
			.Topic("test-topic")
			.Produce(new { FirstName = "John", LastName = "Doe" })
			.WithJsonOptions(customOptions);

		// Assert
		var step = (KafkaProduceStep)scenario.CurrentStep;
		step.JsonOptions.ShouldBe(customOptions);
		step.JsonOptions!.PropertyNamingPolicy.ShouldBe(JsonNamingPolicy.SnakeCaseLower);
		step.JsonOptions.WriteIndented.ShouldBeTrue();
	}

	#endregion

	#region Immutability Tests

	[Fact(DisplayName = "All With methods should maintain immutability pattern")]
	public void All_With_Methods_Should_Maintain_Immutability()
	{
		// Arrange
		var scenario = Given().Topic("test-topic").Produce("test-value");
		var originalStep = scenario.CurrentStep;

		// Act - Call all With methods
		scenario
			.WithKey("new-key")
			.WithHeader("header1", Encoding.UTF8.GetBytes("value1"))
			.WithPartition(3)
			.WithTimestamp(DateTime.UtcNow)
			.WithTimeout(TimeSpan.FromSeconds(60))
			.WithBootstrapServers("kafka:9092")
			.WithProducerConfig(new ProducerConfig())
			.WithJsonOptions(new JsonSerializerOptions());

		var finalStep = scenario.CurrentStep;

		// Assert
		originalStep.ShouldNotBe(finalStep);
		((KafkaProduceStep)originalStep).Key.ShouldBeNull();
		((KafkaProduceStep)finalStep).Key.ShouldBe("new-key");
	}

	[Fact(DisplayName = "WithKey should preserve all other properties")]
	public void WithKey_Should_Preserve_All_Other_Properties()
	{
		// Arrange
		var headers = new Headers();
		headers.Add("test", Encoding.UTF8.GetBytes("value"));

		var scenario = Given()
			.Topic("test-topic")
			.Produce("test-value")
			.WithHeaders(headers)
			.WithPartition(2)
			.WithTimestamp(DateTime.UtcNow)
			.WithTimeout(TimeSpan.FromSeconds(45))
			.WithBootstrapServers("kafka:9092");

		var stepBeforeKey = (KafkaProduceStep)scenario.CurrentStep;

		// Act
		scenario.WithKey("new-key");
		var stepAfterKey = (KafkaProduceStep)scenario.CurrentStep;

		// Assert
		stepAfterKey.Topic.ShouldBe(stepBeforeKey.Topic);
		stepAfterKey.Value.ShouldBe(stepBeforeKey.Value);
		stepAfterKey.Headers.ShouldBe(stepBeforeKey.Headers);
		stepAfterKey.Partition.ShouldBe(stepBeforeKey.Partition);
		stepAfterKey.Timestamp.ShouldBe(stepBeforeKey.Timestamp);
		stepAfterKey.Timeout.ShouldBe(stepBeforeKey.Timeout);
		stepAfterKey.BootstrapServers.ShouldBe(stepBeforeKey.BootstrapServers);
	}

	#endregion

	#region Complex Integration Tests

	[Fact(DisplayName = "Should support full message configuration chain")]
	public void Should_Support_Full_Message_Configuration_Chain()
	{
		// Arrange
		var timestamp = DateTime.UtcNow;
		var complexValue = new
		{
			EventId = Guid.NewGuid(),
			Type = "OrderCreated",
			Payload = new { OrderId = "123", Amount = 99.99m }
		};

		// Act
		var scenario = Given()
			.Topic("events")
			.Produce(complexValue)
			.WithKey("order-123")
			.WithHeader("correlation-id", Encoding.UTF8.GetBytes("abc-123"))
			.WithHeader("event-type", Encoding.UTF8.GetBytes("OrderCreated"))
			.WithHeader("schema-version", Encoding.UTF8.GetBytes("v1"))
			.WithPartition(2)
			.WithTimestamp(timestamp)
			.WithTimeout(TimeSpan.FromSeconds(30))
			.WithBootstrapServers("kafka:9092")
			.WithJsonOptions(new JsonSerializerOptions
			{
				PropertyNamingPolicy = JsonNamingPolicy.CamelCase
			});

		// Assert
		var step = (KafkaProduceStep)scenario.CurrentStep;
		step.Topic.ShouldBe("events");
		step.Key.ShouldBe("order-123");
		step.Value.ShouldBe(complexValue);
		step.Headers.ShouldNotBeNull();
		step.Headers!.Count.ShouldBe(3);
		step.Partition.ShouldBe(2);
		step.Timestamp.ShouldBe(timestamp);
		step.Timeout.ShouldBe(TimeSpan.FromSeconds(30));
		step.BootstrapServers.ShouldBe("kafka:9092");
		step.JsonOptions.ShouldNotBeNull();
	}

	#endregion
}
