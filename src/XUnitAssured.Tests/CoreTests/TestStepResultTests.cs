using Shouldly;
using Xunit;
using XUnitAssured.Core.Results;

namespace XUnitAssured.Tests.CoreTests;

/// <summary>
/// Unit tests for TestStepResult class.
/// Validates the core result functionality independent of technology.
/// </summary>
public class TestStepResultTests
{
	[Fact]
	public void TestStepResult_Should_Store_Success_And_Data()
	{
		// Arrange
		var testData = new { Name = "Test", Value = 123 };

		// Act
		var result = new TestStepResult
		{
			Success = true,
			Data = testData,
			DataType = testData.GetType()
		};

		// Assert
		result.Success.ShouldBeTrue();
		result.Data.ShouldBe(testData);
		result.DataType.ShouldNotBeNull(); // Anonymous type preserves its actual type
		result.DataType!.Name.ShouldContain("AnonymousType");
	}

	[Fact]
	public void TestStepResult_Should_Store_Properties()
	{
		// Arrange & Act
		var result = new TestStepResult
		{
			Success = true,
			Properties = new Dictionary<string, object?>
			{
				["StringProp"] = "value",
				["IntProp"] = 42,
				["BoolProp"] = true
			}
		};

		// Assert
		result.GetProperty<string>("StringProp").ShouldBe("value");
		result.GetProperty<int>("IntProp").ShouldBe(42);
		result.GetProperty<bool>("BoolProp").ShouldBeTrue();
	}

	[Fact]
	public void GetProperty_Should_Return_Default_For_Missing_Key()
	{
		// Arrange
		var result = new TestStepResult
		{
			Success = true,
			Properties = new Dictionary<string, object?>()
		};

		// Act & Assert
		result.GetProperty<string>("NonExistent").ShouldBeNull();
		result.GetProperty<int>("NonExistent").ShouldBe(0);
		result.GetProperty<bool>("NonExistent").ShouldBe(false);
	}

	[Fact]
	public void GetData_Should_Convert_Compatible_Types()
	{
		// Arrange
		var result = new TestStepResult
		{
			Success = true,
			Data = "test string",
			DataType = typeof(string)
		};

		// Act & Assert
		result.GetData<string>().ShouldBe("test string");
		result.GetData<object>().ShouldBe("test string");
	}

	[Fact]
	public void CreateSuccess_Should_Create_Successful_Result()
	{
		// Arrange
		var testData = new { Name = "Test" };
		var properties = new Dictionary<string, object?>
		{
			["CustomProp"] = "custom value"
		};

		// Act
		var result = TestStepResult.CreateSuccess(testData, properties);

		// Assert
		result.Success.ShouldBeTrue();
		result.Data.ShouldBe(testData);
		result.Metadata.Status.ShouldBe(StepStatus.Succeeded);
		result.GetProperty<string>("CustomProp").ShouldBe("custom value");
		result.Errors.ShouldBeEmpty();
	}

	[Fact]
	public void CreateFailure_Should_Create_Failed_Result_With_Errors()
	{
		// Arrange
		var errorMessages = new[] { "Error 1", "Error 2" };

		// Act
		var result = TestStepResult.CreateFailure(errorMessages);

		// Assert
		result.Success.ShouldBeFalse();
		result.Metadata.Status.ShouldBe(StepStatus.Failed);
		result.Errors.Count.ShouldBe(2);
		result.Errors.ShouldContain("Error 1");
		result.Errors.ShouldContain("Error 2");
	}

	[Fact]
	public void CreateFailure_From_Exception_Should_Capture_Message()
	{
		// Arrange
		var exception = new InvalidOperationException("Something went wrong");

		// Act
		var result = TestStepResult.CreateFailure(exception);

		// Assert
		result.Success.ShouldBeFalse();
		result.Metadata.Status.ShouldBe(StepStatus.Failed);
		result.Errors.Count.ShouldBe(1);
		result.Errors[0].ShouldBe("Something went wrong");
	}

	[Fact]
	public void Metadata_Should_Calculate_Duration()
	{
		// Arrange
		var startTime = DateTimeOffset.UtcNow;
		var endTime = startTime.AddSeconds(2);

		// Act
		var result = new TestStepResult
		{
			Metadata = new StepMetadata
			{
				StartedAt = startTime,
				CompletedAt = endTime,
				Status = StepStatus.Succeeded
			}
		};

		// Assert
		result.Metadata.Duration.TotalSeconds.ShouldBeGreaterThanOrEqualTo(1.9);
		result.Metadata.Duration.TotalSeconds.ShouldBeLessThanOrEqualTo(2.1);
	}

	[Fact]
	public void Metadata_Should_Support_Tags()
	{
		// Arrange & Act
		var result = new TestStepResult
		{
			Metadata = new StepMetadata
			{
				Tags = new[] { "critical", "performance", "http" }
			}
		};

		// Assert
		result.Metadata.Tags.Count.ShouldBe(3);
		result.Metadata.Tags.ShouldContain("critical");
		result.Metadata.Tags.ShouldContain("performance");
		result.Metadata.Tags.ShouldContain("http");
	}

	[Fact]
	public void Metadata_WithStatus_Should_Create_New_Instance()
	{
		// Arrange
		var original = new StepMetadata
		{
			Status = StepStatus.Running,
			AttemptCount = 1
		};

		// Act
		var updated = original.WithStatus(StepStatus.Succeeded);

		// Assert
		updated.Status.ShouldBe(StepStatus.Succeeded);
		updated.CompletedAt.ShouldNotBeNull();
		updated.AttemptCount.ShouldBe(1);
		original.Status.ShouldBe(StepStatus.Running); // Original unchanged
	}

	[Fact]
	public void Metadata_WithIncrementedAttempt_Should_Increment_Count()
	{
		// Arrange
		var original = new StepMetadata
		{
			AttemptCount = 1
		};

		// Act
		var incremented = original.WithIncrementedAttempt();

		// Assert
		incremented.AttemptCount.ShouldBe(2);
		original.AttemptCount.ShouldBe(1); // Original unchanged
	}
}
