using System;
using System.Threading.Tasks;
using XUnitAssured.Core.Results;

namespace XUnitAssured.Core.Abstractions;

/// <summary>
/// Represents a single step in a test scenario.
/// Technology-agnostic interface that can be implemented by HTTP, Kafka, FileSystem, S3, etc.
/// </summary>
public interface ITestStep
{
	/// <summary>
	/// Unique name identifier for this step (used in SaveStep).
	/// Null if the step hasn't been saved.
	/// </summary>
	string? Name { get; }

	/// <summary>
	/// Type of step (e.g., "Http", "Kafka", "FileSystem", "S3").
	/// Used for logging and debugging purposes.
	/// </summary>
	string StepType { get; }

	/// <summary>
	/// Result of the step execution.
	/// Null if the step hasn't been executed yet.
	/// </summary>
	ITestStepResult? Result { get; }

	/// <summary>
	/// Indicates whether the step has been executed.
	/// </summary>
	bool IsExecuted { get; }

	/// <summary>
	/// Indicates whether the step has been validated.
	/// </summary>
	bool IsValid { get; }

	/// <summary>
	/// Executes the step asynchronously.
	/// </summary>
	/// <param name="context">Test execution context containing shared state</param>
	/// <returns>Result of the step execution</returns>
	Task<ITestStepResult> ExecuteAsync(ITestContext context);

	/// <summary>
	/// Validates the step result using a custom validation function.
	/// </summary>
	/// <param name="validation">Validation function that receives the result</param>
	void Validate(Action<ITestStepResult> validation);
}
