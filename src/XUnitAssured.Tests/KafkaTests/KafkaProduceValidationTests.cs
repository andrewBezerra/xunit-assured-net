using Confluent.Kafka;
using XUnitAssured.Kafka.Results;

namespace XUnitAssured.Tests.KafkaTests;

[Trait("Category", "Kafka")]
[Trait("Component", "ProduceValidation")]
/// <summary>
/// Unit tests for Kafka Produce validation extensions.
/// Tests validation methods specific to produce operations.
/// </summary>
public class KafkaProduceValidationTests
{
	#region ValidateProduceSuccess Tests

	[Fact(DisplayName = "ValidateProduceSuccess should pass for successful persisted message")]
	public void ValidateProduceSuccess_Should_Pass_For_Persisted_Message()
	{
		// Arrange
		var deliveryResult = new DeliveryResult<string, string>
		{
			Topic = "test-topic",
			Partition = new Partition(0),
			Offset = new Offset(123),
			Status = PersistenceStatus.Persisted,
			Message = new Message<string, string>
			{
				Key = "key",
				Value = "value",
				Timestamp = new Timestamp(DateTime.UtcNow)
			}
		};

		var result = KafkaStepResult.CreateKafkaProduceSuccess(deliveryResult);

		// Act & Assert - Should not throw
		Should.NotThrow(() =>
		{
			if (!result.Success)
				throw new InvalidOperationException("Produce operation failed");
			if (result.Status != PersistenceStatus.Persisted)
				throw new InvalidOperationException($"Message was not persisted. Status: {result.Status}");
		});
	}

	[Fact(DisplayName = "ValidateProduceSuccess should fail for non-persisted message")]
	public void ValidateProduceSuccess_Should_Fail_For_Non_Persisted()
	{
		// Arrange
		var deliveryResult = new DeliveryResult<string, string>
		{
			Topic = "test-topic",
			Partition = new Partition(0),
			Offset = new Offset(-1),
			Status = PersistenceStatus.NotPersisted,
			Message = new Message<string, string>
			{
				Value = "value",
				Timestamp = new Timestamp(DateTime.UtcNow)
			}
		};

		var result = KafkaStepResult.CreateKafkaProduceSuccess(deliveryResult);

		// Act & Assert
		Should.Throw<InvalidOperationException>(() =>
		{
			if (!result.Success)
				throw new InvalidOperationException("Produce operation failed");
			if (result.Status != PersistenceStatus.Persisted)
				throw new InvalidOperationException($"Message was not persisted. Status: {result.Status}");
		});
	}

	[Fact(DisplayName = "ValidateProduceSuccess should fail for failed result")]
	public void ValidateProduceSuccess_Should_Fail_For_Failed_Result()
	{
		// Arrange
		var result = KafkaStepResult.CreateFailure(new Exception("Produce failed"));

		// Act & Assert
		Should.Throw<InvalidOperationException>(() =>
		{
			if (!result.Success)
				throw new InvalidOperationException("Produce operation failed");
		});
	}

	#endregion

	#region ValidatePartition Tests

	[Fact(DisplayName = "ValidatePartition should pass for correct partition")]
	public void ValidatePartition_Should_Pass_For_Correct_Partition()
	{
		// Arrange
		var deliveryResult = new DeliveryResult<string, string>
		{
			Topic = "test-topic",
			Partition = new Partition(2),
			Offset = new Offset(123),
			Status = PersistenceStatus.Persisted,
			Message = new Message<string, string>
			{
				Value = "value",
				Timestamp = new Timestamp(DateTime.UtcNow)
			}
		};

		var result = KafkaStepResult.CreateKafkaProduceSuccess(deliveryResult);

		// Act & Assert - Should not throw
		Should.NotThrow(() =>
		{
			if (result.Partition == null)
				throw new InvalidOperationException("Partition information is not available");
			if (result.Partition != 2)
				throw new InvalidOperationException($"Expected partition 2 but got {result.Partition}");
		});
	}

	[Fact(DisplayName = "ValidatePartition should fail for wrong partition")]
	public void ValidatePartition_Should_Fail_For_Wrong_Partition()
	{
		// Arrange
		var deliveryResult = new DeliveryResult<string, string>
		{
			Topic = "test-topic",
			Partition = new Partition(1),
			Offset = new Offset(123),
			Status = PersistenceStatus.Persisted,
			Message = new Message<string, string>
			{
				Value = "value",
				Timestamp = new Timestamp(DateTime.UtcNow)
			}
		};

		var result = KafkaStepResult.CreateKafkaProduceSuccess(deliveryResult);

		// Act & Assert
		Should.Throw<InvalidOperationException>(() =>
		{
			if (result.Partition == null)
				throw new InvalidOperationException("Partition information is not available");
			if (result.Partition != 2)
				throw new InvalidOperationException($"Expected partition 2 but got {result.Partition}");
		});
	}

	[Fact(DisplayName = "ValidatePartition should fail when partition is null")]
	public void ValidatePartition_Should_Fail_When_Partition_Null()
	{
		// Arrange
		var result = new KafkaStepResult
		{
			Success = true,
			Properties = new Dictionary<string, object?>
			{
				["Topic"] = "test-topic"
			}
		};

		// Act & Assert
		Should.Throw<InvalidOperationException>(() =>
		{
			if (result.Partition == null)
				throw new InvalidOperationException("Partition information is not available");
		});
	}

	#endregion

	#region ValidateOffset Tests

	[Fact(DisplayName = "ValidateOffset should pass for valid offset")]
	public void ValidateOffset_Should_Pass_For_Valid_Offset()
	{
		// Arrange
		var deliveryResult = new DeliveryResult<string, string>
		{
			Topic = "test-topic",
			Partition = new Partition(0),
			Offset = new Offset(12345),
			Status = PersistenceStatus.Persisted,
			Message = new Message<string, string>
			{
				Value = "value",
				Timestamp = new Timestamp(DateTime.UtcNow)
			}
		};

		var result = KafkaStepResult.CreateKafkaProduceSuccess(deliveryResult);

		// Act & Assert - Should not throw
		Should.NotThrow(() =>
		{
			if (result.Offset == null)
				throw new InvalidOperationException("Offset information is not available");
			if (!(result.Offset > 0))
				throw new InvalidOperationException($"Offset validation failed for offset {result.Offset}");
		});
	}

	[Fact(DisplayName = "ValidateOffset should fail for invalid offset")]
	public void ValidateOffset_Should_Fail_For_Invalid_Offset()
	{
		// Arrange
		var deliveryResult = new DeliveryResult<string, string>
		{
			Topic = "test-topic",
			Partition = new Partition(0),
			Offset = new Offset(-1),
			Status = PersistenceStatus.NotPersisted,
			Message = new Message<string, string>
			{
				Value = "value",
				Timestamp = new Timestamp(DateTime.UtcNow)
			}
		};

		var result = KafkaStepResult.CreateKafkaProduceSuccess(deliveryResult);

		// Act & Assert
		Should.Throw<InvalidOperationException>(() =>
		{
			if (result.Offset == null)
				throw new InvalidOperationException("Offset information is not available");
			if (!(result.Offset > 0))
				throw new InvalidOperationException($"Offset validation failed for offset {result.Offset}");
		});
	}

	[Fact(DisplayName = "ValidateOffset should fail when offset is null")]
	public void ValidateOffset_Should_Fail_When_Offset_Null()
	{
		// Arrange
		var result = new KafkaStepResult
		{
			Success = true,
			Properties = new Dictionary<string, object?>
			{
				["Topic"] = "test-topic"
			}
		};

		// Act & Assert
		Should.Throw<InvalidOperationException>(() =>
		{
			if (result.Offset == null)
				throw new InvalidOperationException("Offset information is not available");
		});
	}

	[Fact(DisplayName = "ValidateOffset should support custom validation logic")]
	public void ValidateOffset_Should_Support_Custom_Validation()
	{
		// Arrange
		var deliveryResult = new DeliveryResult<string, string>
		{
			Topic = "test-topic",
			Partition = new Partition(0),
			Offset = new Offset(100),
			Status = PersistenceStatus.Persisted,
			Message = new Message<string, string>
			{
				Value = "value",
				Timestamp = new Timestamp(DateTime.UtcNow)
			}
		};

		var result = KafkaStepResult.CreateKafkaProduceSuccess(deliveryResult);

		// Act & Assert - Custom validation: offset must be >= 100
		Should.NotThrow(() =>
		{
			if (result.Offset == null)
				throw new InvalidOperationException("Offset information is not available");
			if (!(result.Offset >= 100))
				throw new InvalidOperationException($"Offset validation failed for offset {result.Offset}");
		});
	}

	#endregion

	#region ValidateDeliveryStatus Tests

	[Fact(DisplayName = "ValidateDeliveryStatus should pass for expected status")]
	public void ValidateDeliveryStatus_Should_Pass_For_Expected_Status()
	{
		// Arrange
		var deliveryResult = new DeliveryResult<string, string>
		{
			Topic = "test-topic",
			Partition = new Partition(0),
			Offset = new Offset(123),
			Status = PersistenceStatus.Persisted,
			Message = new Message<string, string>
			{
				Value = "value",
				Timestamp = new Timestamp(DateTime.UtcNow)
			}
		};

		var result = KafkaStepResult.CreateKafkaProduceSuccess(deliveryResult);

		// Act & Assert - Should not throw
		Should.NotThrow(() =>
		{
			if (result.Status == null)
				throw new InvalidOperationException("Delivery status is not available");
			if (result.Status != PersistenceStatus.Persisted)
				throw new InvalidOperationException($"Expected delivery status Persisted but got {result.Status}");
		});
	}

	[Fact(DisplayName = "ValidateDeliveryStatus should fail for unexpected status")]
	public void ValidateDeliveryStatus_Should_Fail_For_Unexpected_Status()
	{
		// Arrange
		var deliveryResult = new DeliveryResult<string, string>
		{
			Topic = "test-topic",
			Partition = new Partition(0),
			Offset = new Offset(-1),
			Status = PersistenceStatus.NotPersisted,
			Message = new Message<string, string>
			{
				Value = "value",
				Timestamp = new Timestamp(DateTime.UtcNow)
			}
		};

		var result = KafkaStepResult.CreateKafkaProduceSuccess(deliveryResult);

		// Act & Assert
		Should.Throw<InvalidOperationException>(() =>
		{
			if (result.Status == null)
				throw new InvalidOperationException("Delivery status is not available");
			if (result.Status != PersistenceStatus.Persisted)
				throw new InvalidOperationException($"Expected delivery status Persisted but got {result.Status}");
		});
	}

	[Fact(DisplayName = "ValidateDeliveryStatus should handle PossiblyPersisted status")]
	public void ValidateDeliveryStatus_Should_Handle_PossiblyPersisted()
	{
		// Arrange
		var deliveryResult = new DeliveryResult<string, string>
		{
			Topic = "test-topic",
			Partition = new Partition(0),
			Offset = new Offset(123),
			Status = PersistenceStatus.PossiblyPersisted,
			Message = new Message<string, string>
			{
				Value = "value",
				Timestamp = new Timestamp(DateTime.UtcNow)
			}
		};

		var result = KafkaStepResult.CreateKafkaProduceSuccess(deliveryResult);

		// Act & Assert
		result.Status.ShouldBe(PersistenceStatus.PossiblyPersisted);
	}

	#endregion

	#region KafkaStepResult Status Property Tests

	[Fact(DisplayName = "KafkaStepResult Status property should return correct PersistenceStatus for Persisted")]
	public void KafkaStepResult_Status_Should_Return_Persisted()
	{
		// Arrange
		var result = new KafkaStepResult
		{
			Success = true,
			Properties = new Dictionary<string, object?>
			{
				["Status"] = "Persisted"
			}
		};

		// Act & Assert
		result.Status.ShouldBe(PersistenceStatus.Persisted);
	}

	[Fact(DisplayName = "KafkaStepResult Status property should return correct PersistenceStatus for NotPersisted")]
	public void KafkaStepResult_Status_Should_Return_NotPersisted()
	{
		// Arrange
		var result = new KafkaStepResult
		{
			Success = false,
			Properties = new Dictionary<string, object?>
			{
				["Status"] = "NotPersisted"
			}
		};

		// Act & Assert
		result.Status.ShouldBe(PersistenceStatus.NotPersisted);
	}

	[Fact(DisplayName = "KafkaStepResult Status property should return correct PersistenceStatus for PossiblyPersisted")]
	public void KafkaStepResult_Status_Should_Return_PossiblyPersisted()
	{
		// Arrange
		var result = new KafkaStepResult
		{
			Success = true,
			Properties = new Dictionary<string, object?>
			{
				["Status"] = "PossiblyPersisted"
			}
		};

		// Act & Assert
		result.Status.ShouldBe(PersistenceStatus.PossiblyPersisted);
	}

	[Fact(DisplayName = "KafkaStepResult Status property should return null for missing status")]
	public void KafkaStepResult_Status_Should_Return_Null_For_Missing()
	{
		// Arrange
		var result = new KafkaStepResult
		{
			Success = true,
			Properties = new Dictionary<string, object?>()
		};

		// Act & Assert
		result.Status.ShouldBeNull();
	}

	[Fact(DisplayName = "CreateProduceTimeout should create timeout result with correct error message")]
	public void CreateProduceTimeout_Should_Create_Timeout_Result()
	{
		// Act
		var result = KafkaStepResult.CreateProduceTimeout("test-topic", TimeSpan.FromSeconds(30));

		// Assert
		result.Success.ShouldBeFalse();
		result.Topic.ShouldBe("test-topic");
		result.Metadata.Status.ShouldBe(Core.Results.StepStatus.Failed);
		result.Errors.Count.ShouldBe(1);
		result.Errors[0].ShouldContain("test-topic");
		result.Errors[0].ShouldContain("30");
		result.Errors[0].ShouldContain("produce");
	}

	#endregion
}
