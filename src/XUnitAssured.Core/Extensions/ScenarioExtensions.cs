using System;
using XUnitAssured.Core.Abstractions;

namespace XUnitAssured.Core.Extensions;

/// <summary>
/// Core extension methods for all test scenarios.
/// </summary>
public static class ScenarioExtensions
{
	/// <summary>
	/// Saves the current step with the specified name for later reference.
	/// Usage: .SaveStep("StepName") then access via Steps["StepName"]
	/// Works with any step type (HTTP, Kafka, etc.)
	/// </summary>
	public static ITestScenario SaveStep(this ITestScenario scenario, string name)
	{
		if (scenario == null)
			throw new ArgumentNullException(nameof(scenario));

		if (string.IsNullOrWhiteSpace(name))
			throw new ArgumentException("Step name cannot be null or whitespace.", nameof(name));

		if (scenario.CurrentStep == null)
			throw new InvalidOperationException("No current step to save. Create a step first.");

		// Execute the step
		scenario.ExecuteCurrentStepAsync().GetAwaiter().GetResult();

		// Save to storage
		scenario.Context.Steps.Save(name, scenario.CurrentStep);

		return scenario;
	}
}
