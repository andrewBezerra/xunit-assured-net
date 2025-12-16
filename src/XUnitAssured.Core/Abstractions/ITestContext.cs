using System.Collections.Generic;

namespace XUnitAssured.Core.Abstractions;

/// <summary>
/// Represents the execution context for a test scenario.
/// Contains shared state and services that steps can access during execution.
/// </summary>
public interface ITestContext
{
	/// <summary>
	/// Storage for named test steps.
	/// Allows steps to reference results from previous steps (e.g., Steps["First"]).
	/// </summary>
	IStepStorage Steps { get; }

	/// <summary>
	/// Shared properties bag for passing data between steps.
	/// Use this for temporary data that doesn't need to be in a named step.
	/// </summary>
	IDictionary<string, object?> Properties { get; }

	/// <summary>
	/// Gets a property value by key.
	/// Returns default(T) if the key doesn't exist.
	/// </summary>
	T? GetProperty<T>(string key);

	/// <summary>
	/// Sets a property value.
	/// </summary>
	void SetProperty<T>(string key, T? value);
}
