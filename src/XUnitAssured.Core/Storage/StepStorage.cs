using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using XUnitAssured.Core.Abstractions;

namespace XUnitAssured.Core.Storage;

/// <summary>
/// Thread-safe implementation of IStepStorage.
/// Uses ConcurrentDictionary to allow safe access from multiple threads.
/// </summary>
public class StepStorage : IStepStorage
{
	private readonly ConcurrentDictionary<string, ITestStep> _steps;

	/// <summary>
	/// Creates a new instance of StepStorage.
	/// </summary>
	public StepStorage()
	{
		_steps = new ConcurrentDictionary<string, ITestStep>(StringComparer.OrdinalIgnoreCase);
	}

	/// <inheritdoc />
	public ITestStep this[string stepName]
	{
		get
		{
			if (string.IsNullOrWhiteSpace(stepName))
				throw new ArgumentException("Step name cannot be null or whitespace.", nameof(stepName));

			if (!_steps.TryGetValue(stepName, out var step))
				throw new KeyNotFoundException($"Step with name '{stepName}' was not found in storage.");

			return step;
		}
	}

	/// <inheritdoc />
	public void Save(string name, ITestStep step)
	{
		if (string.IsNullOrWhiteSpace(name))
			throw new ArgumentException("Step name cannot be null or whitespace.", nameof(name));

		if (step == null)
			throw new ArgumentNullException(nameof(step));

		_steps[name] = step;
	}

	/// <inheritdoc />
	public ITestStep? TryGet(string stepName)
	{
		if (string.IsNullOrWhiteSpace(stepName))
			return null;

		_steps.TryGetValue(stepName, out var step);
		return step;
	}

	/// <inheritdoc />
	public bool Contains(string stepName)
	{
		if (string.IsNullOrWhiteSpace(stepName))
			return false;

		return _steps.ContainsKey(stepName);
	}

	/// <inheritdoc />
	public IReadOnlyCollection<string> GetStepNames()
	{
		return _steps.Keys.ToList().AsReadOnly();
	}

	/// <inheritdoc />
	public void Clear()
	{
		_steps.Clear();
	}
}
