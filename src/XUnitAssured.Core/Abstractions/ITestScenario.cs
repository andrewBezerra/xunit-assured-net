using System;
using XUnitAssured.Core.Results;
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
	/// </summary>
	/// <remarks>
	/// It marks a boundary; it does not run anything. Steps run when the chain is executed.
	/// </remarks>
	ITestScenario And();

	/// <summary>
	/// Chains to the next step using "On".
	/// </summary>
	/// <remarks>
	/// It marks a boundary; it does not run anything. Steps run when the chain is executed.
	/// </remarks>
	ITestScenario On();

	/// <summary>
	/// Marks the transition from "Given" (setup) to "When" (action) in BDD-style tests.
	/// This is a pass-through method for readability in the fluent DSL.
	/// </summary>
	ITestScenario When();

	/// <summary>
	/// Marks the transition from "When" (action) to "Then" (assertions) in BDD-style tests.
	/// This is a pass-through method for readability in the fluent DSL.
	/// </summary>
	ITestScenario Then();

	/// <summary>
	/// Sets the current step for the scenario.
	/// </summary>
	void SetCurrentStep(ITestStep step);

	/// <summary>
	/// Registers a check to run once the current step has produced a result.
	/// </summary>
	/// <remarks>
	/// Registered rather than run on the spot, because the step has not run yet when the
	/// chain is being written. The lambda is what makes this work for values that only exist
	/// later: it closes over them and is called after the step produced its result.
	/// </remarks>
	void AddValidation(Action<ITestStepResult> validation);

	/// <summary>
	/// Runs every step that has not run yet, in the order they were written, applying each
	/// step's registered checks before moving to the next.
	/// </summary>
	/// <remarks>
	/// <para>
	/// A chain describes the scenario; this is what carries it out. Running each step as it
	/// was written would mean an <c>await</c> at every <c>And()</c>, which is the shape that
	/// makes a fluent chain impossible to write.
	/// </para>
	/// <para>
	/// Steps run in order and share one context, so a value a step puts in the context is
	/// there for the next one.
	/// </para>
	/// </remarks>
	System.Threading.Tasks.Task ExecutePendingAsync(
		System.Threading.CancellationToken cancellationToken = default);
}
