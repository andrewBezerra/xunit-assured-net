using Shouldly;
using Xunit;
using XUnitAssured.Core.Abstractions;
using XUnitAssured.Core.Results;
using XUnitAssured.Core.Storage;

namespace XUnitAssured.Tests.CoreTests;

[Trait("Category", "Core")]
[Trait("Component", "Storage")]
/// <summary>
/// Unit tests for StepStorage class.
/// Validates step storage and retrieval functionality.
/// </summary>
public class StepStorageTests
{
	[Fact(DisplayName = "Step storage should store and retrieve step by name successfully")]
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

	[Fact(DisplayName = "Indexer should throw KeyNotFoundException when step is not found")]
	public void Indexer_Should_Throw_When_Step_Not_Found()
	{
		// Arrange
		var storage = new StepStorage();

		// Act & Assert
		Should.Throw<KeyNotFoundException>(() => storage["NonExistent"]);
	}

	[Fact(DisplayName = "Indexer should throw ArgumentException when step name is null")]
	public void Indexer_Should_Throw_When_Name_Is_Null()
	{
		// Arrange
		var storage = new StepStorage();

		// Act & Assert
		Should.Throw<ArgumentException>(() => storage[null!]);
	}

	[Fact(DisplayName = "Indexer should throw ArgumentException when step name is whitespace")]
	public void Indexer_Should_Throw_When_Name_Is_Whitespace()
	{
		// Arrange
		var storage = new StepStorage();

		// Act & Assert
		Should.Throw<ArgumentException>(() => storage["   "]);
	}

	[Fact(DisplayName = "Save should replace existing step with same name")]
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

	[Fact(DisplayName = "Save should throw ArgumentException when step name is null")]
	public void Save_Should_Throw_When_Name_Is_Null()
	{
		// Arrange
		var storage = new StepStorage();
		var step = CreateMockStep("Test");

		// Act & Assert
		Should.Throw<ArgumentException>(() => storage.Save(null!, step));
	}

	[Fact(DisplayName = "Save should throw ArgumentNullException when step is null")]
	public void Save_Should_Throw_When_Step_Is_Null()
	{
		// Arrange
		var storage = new StepStorage();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => storage.Save("Test", null!));
	}

	[Fact(DisplayName = "TryGet should return step when it exists in storage")]
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

	[Fact(DisplayName = "TryGet should return null when step is not found")]
	public void TryGet_Should_Return_Null_When_Not_Found()
	{
		// Arrange
		var storage = new StepStorage();

		// Act
		var retrieved = storage.TryGet("NonExistent");

		// Assert
		retrieved.ShouldBeNull();
	}

	[Fact(DisplayName = "TryGet should return null when step name is null")]
	public void TryGet_Should_Return_Null_When_Name_Is_Null()
	{
		// Arrange
		var storage = new StepStorage();

		// Act
		var retrieved = storage.TryGet(null!);

		// Assert
		retrieved.ShouldBeNull();
	}

	[Fact(DisplayName = "Contains should return true when step exists in storage")]
	public void Contains_Should_Return_True_When_Step_Exists()
	{
		// Arrange
		var storage = new StepStorage();
		var step = CreateMockStep("Test");
		storage.Save("First", step);

		// Act & Assert
		storage.Contains("First").ShouldBeTrue();
	}

	[Fact(DisplayName = "Contains should return false when step is not found")]
	public void Contains_Should_Return_False_When_Step_Not_Found()
	{
		// Arrange
		var storage = new StepStorage();

		// Act & Assert
		storage.Contains("NonExistent").ShouldBeFalse();
	}

	[Fact(DisplayName = "Contains should be case-insensitive when checking step names")]
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

	[Fact(DisplayName = "GetStepNames should return all stored step names")]
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

	[Fact(DisplayName = "GetStepNames should return empty collection when no steps are stored")]
	public void GetStepNames_Should_Return_Empty_When_No_Steps()
	{
		// Arrange
		var storage = new StepStorage();

		// Act
		var names = storage.GetStepNames();

		// Assert
		names.ShouldBeEmpty();
	}

	[Fact(DisplayName = "Clear should remove all steps from storage")]
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
