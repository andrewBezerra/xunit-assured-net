using Shouldly;
using Xunit;
using XUnitAssured.Core.Abstractions;
using XUnitAssured.Core.Results;
using XUnitAssured.Core.Storage;

namespace XUnitAssured.Tests.CoreTests;

/// <summary>
/// Unit tests for StepStorage class.
/// Validates step storage and retrieval functionality.
/// </summary>
public class StepStorageTests
{
	[Fact]
	public void StepStorage_Should_Store_And_Retrieve_Step()
	{
		// Arrange
		var storage = new StepStorage();
		var step = CreateMockStep("TestStep");

		// Act
		storage.Save("First", step);
		var retrieved = storage["First"];

		// Assert
		retrieved.ShouldBe(step);
		retrieved.Name.ShouldBe("TestStep");
	}

	[Fact]
	public void Indexer_Should_Throw_When_Step_Not_Found()
	{
		// Arrange
		var storage = new StepStorage();

		// Act & Assert
		Should.Throw<KeyNotFoundException>(() => storage["NonExistent"]);
	}

	[Fact]
	public void Indexer_Should_Throw_When_Name_Is_Null()
	{
		// Arrange
		var storage = new StepStorage();

		// Act & Assert
		Should.Throw<ArgumentException>(() => storage[null!]);
	}

	[Fact]
	public void Indexer_Should_Throw_When_Name_Is_Whitespace()
	{
		// Arrange
		var storage = new StepStorage();

		// Act & Assert
		Should.Throw<ArgumentException>(() => storage["   "]);
	}

	[Fact]
	public void Save_Should_Replace_Existing_Step()
	{
		// Arrange
		var storage = new StepStorage();
		var step1 = CreateMockStep("Step1");
		var step2 = CreateMockStep("Step2");

		// Act
		storage.Save("Test", step1);
		storage.Save("Test", step2); // Replace

		// Assert
		var retrieved = storage["Test"];
		retrieved.ShouldBe(step2);
		retrieved.Name.ShouldBe("Step2");
	}

	[Fact]
	public void Save_Should_Throw_When_Name_Is_Null()
	{
		// Arrange
		var storage = new StepStorage();
		var step = CreateMockStep("Test");

		// Act & Assert
		Should.Throw<ArgumentException>(() => storage.Save(null!, step));
	}

	[Fact]
	public void Save_Should_Throw_When_Step_Is_Null()
	{
		// Arrange
		var storage = new StepStorage();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => storage.Save("Test", null!));
	}

	[Fact]
	public void TryGet_Should_Return_Step_When_Exists()
	{
		// Arrange
		var storage = new StepStorage();
		var step = CreateMockStep("TestStep");
		storage.Save("First", step);

		// Act
		var retrieved = storage.TryGet("First");

		// Assert
		retrieved.ShouldNotBeNull();
		retrieved.ShouldBe(step);
	}

	[Fact]
	public void TryGet_Should_Return_Null_When_Not_Found()
	{
		// Arrange
		var storage = new StepStorage();

		// Act
		var retrieved = storage.TryGet("NonExistent");

		// Assert
		retrieved.ShouldBeNull();
	}

	[Fact]
	public void TryGet_Should_Return_Null_When_Name_Is_Null()
	{
		// Arrange
		var storage = new StepStorage();

		// Act
		var retrieved = storage.TryGet(null!);

		// Assert
		retrieved.ShouldBeNull();
	}

	[Fact]
	public void Contains_Should_Return_True_When_Step_Exists()
	{
		// Arrange
		var storage = new StepStorage();
		var step = CreateMockStep("Test");
		storage.Save("First", step);

		// Act & Assert
		storage.Contains("First").ShouldBeTrue();
	}

	[Fact]
	public void Contains_Should_Return_False_When_Step_Not_Found()
	{
		// Arrange
		var storage = new StepStorage();

		// Act & Assert
		storage.Contains("NonExistent").ShouldBeFalse();
	}

	[Fact]
	public void Contains_Should_Be_Case_Insensitive()
	{
		// Arrange
		var storage = new StepStorage();
		var step = CreateMockStep("Test");
		storage.Save("First", step);

		// Act & Assert
		storage.Contains("first").ShouldBeTrue();
		storage.Contains("FIRST").ShouldBeTrue();
		storage.Contains("FiRsT").ShouldBeTrue();
	}

	[Fact]
	public void GetStepNames_Should_Return_All_Stored_Names()
	{
		// Arrange
		var storage = new StepStorage();
		storage.Save("First", CreateMockStep("Step1"));
		storage.Save("Second", CreateMockStep("Step2"));
		storage.Save("Third", CreateMockStep("Step3"));

		// Act
		var names = storage.GetStepNames();

		// Assert
		names.Count.ShouldBe(3);
		names.ShouldContain("First");
		names.ShouldContain("Second");
		names.ShouldContain("Third");
	}

	[Fact]
	public void GetStepNames_Should_Return_Empty_When_No_Steps()
	{
		// Arrange
		var storage = new StepStorage();

		// Act
		var names = storage.GetStepNames();

		// Assert
		names.ShouldBeEmpty();
	}

	[Fact]
	public void Clear_Should_Remove_All_Steps()
	{
		// Arrange
		var storage = new StepStorage();
		storage.Save("First", CreateMockStep("Step1"));
		storage.Save("Second", CreateMockStep("Step2"));

		// Act
		storage.Clear();

		// Assert
		storage.GetStepNames().ShouldBeEmpty();
		storage.Contains("First").ShouldBeFalse();
		storage.Contains("Second").ShouldBeFalse();
	}

	// Helper method to create a mock step
	private ITestStep CreateMockStep(string name)
	{
		return new MockTestStep { Name = name };
	}

	// Mock implementation for testing
	private class MockTestStep : ITestStep
	{
		public string? Name { get; init; }
		public string StepType => "Mock";
		public ITestStepResult? Result { get; private set; }
		public bool IsExecuted => Result != null;
		public bool IsValid { get; private set; }

		public Task<ITestStepResult> ExecuteAsync(ITestContext context)
		{
			Result = TestStepResult.CreateSuccess();
			return Task.FromResult(Result);
		}

		public void Validate(Action<ITestStepResult> validation)
		{
			if (Result == null)
				throw new InvalidOperationException("Step must be executed before validation.");

			validation(Result);
			IsValid = true;
		}
	}
}
