using System.Collections.Generic;

namespace XUnitAssured.Core.Abstractions;

/// <summary>
/// Storage for named test steps.
/// Allows accessing previously executed steps by name (e.g., Steps["First"]).
/// </summary>
public interface IStepStorage
{
	/// <summary>
	/// Gets a step by name.
	/// Throws KeyNotFoundException if the step doesn't exist.
	/// </summary>
	/// <param name="stepName">Name of the step to retrieve</param>
	/// <returns>The stored test step</returns>
	ITestStep this[string stepName] { get; }

	/// <summary>
	/// Saves a step with the specified name.
	/// If a step with the same name already exists, it will be replaced.
	/// </summary>
	/// <param name="name">Unique name for the step</param>
	/// <param name="step">Step to store</param>
	void Save(string name, ITestStep step);

	/// <summary>
	/// Tries to get a step by name.
	/// Returns null if the step doesn't exist.
	/// </summary>
	/// <param name="stepName">Name of the step to retrieve</param>
	/// <returns>The step if found, null otherwise</returns>
	ITestStep? TryGet(string stepName);

	/// <summary>
	/// Checks if a step with the specified name exists.
	/// </summary>
	/// <param name="stepName">Name of the step</param>
	/// <returns>True if the step exists, false otherwise</returns>
	bool Contains(string stepName);

	/// <summary>
	/// Gets all stored step names.
	/// </summary>
	IReadOnlyCollection<string> GetStepNames();

	/// <summary>
	/// Clears all stored steps.
	/// </summary>
	void Clear();
}
