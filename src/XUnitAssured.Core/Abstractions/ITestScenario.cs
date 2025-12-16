using XUnitAssured.Core.Storage;

namespace XUnitAssured.Core.Abstractions;

/// <summary>
/// Represents a test scenario that can chain multiple steps together.
/// This is the main entry point for the fluent DSL.
/// </summary>
public interface ITestScenario
{
	/// <summary>
	/// Gets the test execution context.
	/// </summary>
	ITestContext Context { get; }

	/// <summary>
	/// Gets the current step being configured.
	/// </summary>
	ITestStep? CurrentStep { get; }

	/// <summary>
	/// Chains to the next step using "And".
	/// Executes the current step before moving to the next.
	/// </summary>
	ITestScenario And();

	/// <summary>
	/// Chains to the next step using "On".
	/// Executes the current step before moving to the next.
	/// </summary>
	ITestScenario On();

	/// <summary>
	/// Sets the current step for the scenario.
	/// </summary>
	void SetCurrentStep(ITestStep step);

	/// <summary>
	/// Executes the current step asynchronously.
	/// </summary>
	System.Threading.Tasks.Task ExecuteCurrentStepAsync();
}
