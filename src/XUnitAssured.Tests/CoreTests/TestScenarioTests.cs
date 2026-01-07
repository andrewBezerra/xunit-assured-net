using Shouldly;
using Xunit;
using XUnitAssured.Core.Abstractions;
using XUnitAssured.Core.DSL;
using XUnitAssured.Core.Results;
using XUnitAssured.Core.Storage;
using static XUnitAssured.Core.DSL.ScenarioDsl;

namespace XUnitAssured.Tests.CoreTests;

[Trait("Category", "Core")]
[Trait("Component", "Scenario")]
/// <summary>
/// Unit tests for TestScenario and DSL infrastructure.
/// </summary>
public class TestScenarioTests
{
	[Fact(DisplayName = "Given should create a new test scenario with context")]
	public void Given_Should_Create_New_Scenario()
	{
		// Act
		var scenario = Given();

		// Assert
		scenario.ShouldNotBeNull();
		scenario.Context.ShouldNotBeNull();
		scenario.Context.Steps.ShouldNotBeNull();
	}

	[Fact(DisplayName = "Given should create scenario with empty steps collection")]
	public void Given_Should_Create_Scenario_With_Empty_Steps()
	{
		// Act
		var scenario = Given();

		// Assert
		scenario.Context.Steps.GetStepNames().ShouldBeEmpty();
	}

	[Fact(DisplayName = "SetCurrentStep should set the current step in scenario")]
	public void SetCurrentStep_Should_Set_Step()
	{
		// Arrange
		var scenario = Given();
		var mockStep = new MockTestStep();

		// Act
		scenario.SetCurrentStep(mockStep);

		// Assert
		scenario.CurrentStep.ShouldBe(mockStep);
	}

	[Fact(DisplayName = "And should return the same scenario instance for chaining")]
	public void And_Should_Return_Same_Scenario()
	{
		// Arrange
		var scenario = Given();
		scenario.SetCurrentStep(new MockTestStep());

		// Act
		var result = scenario.And();

		// Assert
		result.ShouldBe(scenario);
	}

	[Fact(DisplayName = "On should return the same scenario instance for chaining")]
	public void On_Should_Return_Same_Scenario()
	{
		// Arrange
		var scenario = Given();
		scenario.SetCurrentStep(new MockTestStep());

		// Act
		var result = scenario.On();

		// Assert
		result.ShouldBe(scenario);
	}

	[Fact(DisplayName = "ExecuteCurrentStepAsync should execute the current step")]
	public async Task ExecuteCurrentStepAsync_Should_Execute_Step()
	{
		// Arrange
		var scenario = Given();
		var mockStep = new MockTestStep();
		scenario.SetCurrentStep(mockStep);

		// Act
		await scenario.ExecuteCurrentStepAsync();

		// Assert
		mockStep.IsExecuted.ShouldBeTrue();
	}

	[Fact(DisplayName = "ExecuteCurrentStepAsync should not execute the same step twice")]
	public async Task ExecuteCurrentStepAsync_Should_Not_Execute_Twice()
	{
		// Arrange
		var scenario = Given();
		var mockStep = new MockTestStep();
		scenario.SetCurrentStep(mockStep);

		// Act
		await scenario.ExecuteCurrentStepAsync();
		mockStep.ExecutionCount.ShouldBe(1);

		await scenario.ExecuteCurrentStepAsync();

		// Assert
		mockStep.ExecutionCount.ShouldBe(1); // Still 1, not 2
	}

	[Fact(DisplayName = "TestContext should store and retrieve properties correctly")]
	public void TestContext_Should_Store_And_Retrieve_Properties()
	{
		// Arrange
		var stepStorage = new StepStorage();
		var context = new TestContext(stepStorage);

		// Act
		context.SetProperty("key1", "value1");
		context.SetProperty("key2", 123);

		// Assert
		context.GetProperty<string>("key1").ShouldBe("value1");
		context.GetProperty<int>("key2").ShouldBe(123);
	}

	[Fact(DisplayName = "TestContext should return default value for missing property")]
	public void TestContext_Should_Return_Default_For_Missing_Property()
	{
		// Arrange
		var stepStorage = new StepStorage();
		var context = new TestContext(stepStorage);

		// Act & Assert
		context.GetProperty<string>("nonexistent").ShouldBeNull();
		context.GetProperty<int>("nonexistent").ShouldBe(0);
	}

	// Mock test step for testing
	private class MockTestStep : ITestStep
	{
		public string? Name { get; set; }
		public string StepType => "Mock";
		public ITestStepResult? Result { get; private set; }
		public bool IsExecuted => Result != null;
		public bool IsValid { get; private set; }
		public int ExecutionCount { get; private set; }

		public async System.Threading.Tasks.Task<ITestStepResult> ExecuteAsync(ITestContext context)
		{
			ExecutionCount++;
			Result = TestStepResult.CreateSuccess();
			return await System.Threading.Tasks.Task.FromResult(Result);
		}

		public void Validate(System.Action<ITestStepResult> validation)
		{
			if (Result == null)
				throw new System.InvalidOperationException("Step must be executed before validation.");

			validation(Result);
			IsValid = true;
		}
	}
}
