using Confluent.Kafka;
using Shouldly;
using Xunit;
using XUnitAssured.Core.Results;
using XUnitAssured.Kafka.Results;

namespace XUnitAssured.Tests.KafkaTests;

[Trait("Category", "Kafka")]
[Trait("Component", "Result")]
/// <summary>
/// Unit tests for KafkaStepResult class.
/// Validates Kafka-specific result functionality and helpers.
/// </summary>
public class KafkaStepResultTests
{
	[Fact(DisplayName = "KafkaStepResult should expose message data correctly")]
	public void KafkaStepResult_Should_Expose_Message()
	{
		// Arrange
		var message = new { Id = 1, Name = "Test Message" };

		// Act
		var result = new KafkaStepResult
		{
			Success = true,
			Data = message,
			Properties = new Dictionary<string, object?>
			{
				["Topic"] = "test-topic"
			}
		};

		// Assert
		result.Message.ShouldBe(message);
		result.Message.ShouldNotBeNull();
		
		// Verify GetMessage returns the same object reference
		var messageObject = result.GetMessage<object>();
		messageObject.ShouldBe(message);
	}

	[Fact(DisplayName = "KafkaStepResult should expose Kafka metadata correctly")]
	public void KafkaStepResult_Should_Expose_Kafka_Metadata()
	{
		// Arrange
		var timestamp = DateTime.UtcNow;

		// Act
		var result = new KafkaStepResult
		{
			Success = true,
			Properties = new Dictionary<string, object?>
			{
				["Topic"] = "test-topic",
				["Partition"] = 2,
				["Offset"] = 12345L,
				["Timestamp"] = timestamp,
				["Key"] = "message-key"
			}
		};

		// Assert
		result.Topic.ShouldBe("test-topic");
		result.Partition.ShouldBe(2);
		result.Offset.ShouldBe(12345L);
		result.Timestamp.ShouldBe(timestamp);
		result.Key.ShouldBe("message-key");
	}

	[Fact(DisplayName = "KafkaStepResult should handle null partition and offset gracefully")]
	public void KafkaStepResult_Should_Handle_Null_Partition_And_Offset()
	{
		// Act
		var result = new KafkaStepResult
		{
			Success = true,
			Properties = new Dictionary<string, object?>
			{
				["Topic"] = "test-topic"
			}
		};

		// Assert
		result.Partition.ShouldBeNull();
		result.Offset.ShouldBeNull();
	}

	[Fact(DisplayName = "CreateKafkaConsumeSuccess should create complete result with all properties")]
	public void CreateKafkaConsumeSuccess_Should_Create_Complete_Result()
	{
		// Arrange
		var messageValue = new { Id = 1, Name = "Test" };
		var headers = new Headers();
		headers.Add("correlation-id", System.Text.Encoding.UTF8.GetBytes("123"));

		var consumeResult = new ConsumeResult<string, object>
		{
			Topic = "test-topic",
			Partition = new Partition(2),
			Offset = new Offset(12345),
			Message = new Message<string, object>
			{
				Key = "message-key",
				Value = messageValue,
				Timestamp = new Timestamp(DateTime.UtcNow),
				Headers = headers
			}
		};

		// Act
		var result = KafkaStepResult.CreateKafkaConsumeSuccess(consumeResult);

		// Assert
		result.Success.ShouldBeTrue();
		result.Topic.ShouldBe("test-topic");
		result.Partition.ShouldBe(2);
		result.Offset.ShouldBe(12345L);
		result.Key.ShouldBe("message-key");
		result.Message.ShouldBe(messageValue);
		result.Headers.ShouldNotBeNull();
		result.Metadata.Status.ShouldBe(StepStatus.Succeeded);
	}

	[Fact(DisplayName = "CreateKafkaProduceSuccess should create complete result with all properties")]
	public void CreateKafkaProduceSuccess_Should_Create_Complete_Result()
	{
		// Arrange
		var messageValue = new { Id = 1, Name = "Test" };

		var deliveryResult = new DeliveryResult<string, object>
		{
			Topic = "test-topic",
			Partition = new Partition(2),
			Offset = new Offset(12345),
			Status = PersistenceStatus.Persisted,
			Message = new Message<string, object>
			{
				Key = "message-key",
				Value = messageValue,
				Timestamp = new Timestamp(DateTime.UtcNow)
			}
		};

		// Act
		var result = KafkaStepResult.CreateKafkaProduceSuccess(deliveryResult);

		// Assert
		result.Success.ShouldBeTrue();
		result.Topic.ShouldBe("test-topic");
		result.Partition.ShouldBe(2);
		result.Offset.ShouldBe(12345L);
		result.Key.ShouldBe("message-key");
		result.Message.ShouldBe(messageValue);
		result.Metadata.Status.ShouldBe(StepStatus.Succeeded);
		result.GetProperty<string>("Status").ShouldBe("Persisted");
	}

	[Fact(DisplayName = "CreateKafkaProduceSuccess should set Success to false for NotPersisted status")]
	public void CreateKafkaProduceSuccess_Should_Set_Success_False_For_NotPersisted()
	{
		// Arrange
		var deliveryResult = new DeliveryResult<string, object>
		{
			Topic = "test-topic",
			Partition = new Partition(2),
			Offset = new Offset(-1), // Not persisted
			Status = PersistenceStatus.NotPersisted,
			Message = new Message<string, object>
			{
				Value = new { Id = 1 },
				Timestamp = new Timestamp(DateTime.UtcNow)
			}
		};

		// Act
		var result = KafkaStepResult.CreateKafkaProduceSuccess(deliveryResult);

		// Assert
		result.Success.ShouldBeFalse();
	}

	[Fact(DisplayName = "CreateFailure should create failed result with exception message")]
	public void CreateFailure_Should_Create_Failed_Result()
	{
		// Arrange
		var exception = new ConsumeException(
			new ConsumeResult<byte[], byte[]>(), 
			new Error(ErrorCode.Local_TimedOut, "Timed out"));

		// Act
		var result = KafkaStepResult.CreateFailure(exception);

		// Assert
		result.Success.ShouldBeFalse();
		result.Metadata.Status.ShouldBe(StepStatus.Failed);
		result.Errors.Count.ShouldBe(1);
		result.Errors[0].ShouldContain("Timed out");
	}

	[Fact(DisplayName = "CreateTimeout should create timeout result with error message")]
	public void CreateTimeout_Should_Create_Timeout_Result()
	{
		// Act
		var result = KafkaStepResult.CreateTimeout("test-topic", TimeSpan.FromSeconds(30));

		// Assert
		result.Success.ShouldBeFalse();
		result.Topic.ShouldBe("test-topic");
		result.Metadata.Status.ShouldBe(StepStatus.Failed);
		result.Errors.Count.ShouldBe(1);
		result.Errors[0].ShouldContain("test-topic");
		result.Errors[0].ShouldContain("30");
	}

	[Fact(DisplayName = "GetHeaderValue should return header value as bytes")]
	public void GetHeaderValue_Should_Return_Header_Bytes()
	{
		// Arrange
		var headers = new Headers();
		var headerValue = System.Text.Encoding.UTF8.GetBytes("correlation-123");
		headers.Add("correlation-id", headerValue);

		var result = new KafkaStepResult
		{
			Success = true,
			Properties = new Dictionary<string, object?>
			{
				["Headers"] = headers
			}
		};

		// Act
		var retrievedValue = result.GetHeaderValue("correlation-id");

		// Assert
		retrievedValue.ShouldNotBeNull();
		System.Text.Encoding.UTF8.GetString(retrievedValue!).ShouldBe("correlation-123");
	}

	[Fact(DisplayName = "GetHeaderValue should return null for missing header")]
	public void GetHeaderValue_Should_Return_Null_For_Missing_Header()
	{
		// Arrange
		var headers = new Headers();
		var result = new KafkaStepResult
		{
			Success = true,
			Properties = new Dictionary<string, object?>
			{
				["Headers"] = headers
			}
		};

		// Act
		var retrievedValue = result.GetHeaderValue("non-existent");

		// Assert
		retrievedValue.ShouldBeNull();
	}

	[Fact(DisplayName = "GetHeaderValue should return null when headers collection is missing")]
	public void GetHeaderValue_Should_Return_Null_When_Headers_Missing()
	{
		// Arrange
		var result = new KafkaStepResult
		{
			Success = true,
			Properties = new Dictionary<string, object?>()
		};

		// Act
		var retrievedValue = result.GetHeaderValue("any-key");

		// Assert
		retrievedValue.ShouldBeNull();
	}
}
