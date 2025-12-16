using System.Collections.Generic;
using XUnitAssured.Core.Abstractions;

namespace XUnitAssured.Core.DSL;

/// <summary>
/// Implementation of ITestContext that provides execution context for test scenarios.
/// </summary>
public class TestContext : ITestContext
{
	/// <inheritdoc />
	public IStepStorage Steps { get; }

	/// <inheritdoc />
	public IDictionary<string, object?> Properties { get; }

	/// <summary>
	/// Creates a new TestContext with the specified step storage.
	/// </summary>
	public TestContext(IStepStorage stepStorage)
	{
		Steps = stepStorage;
		Properties = new Dictionary<string, object?>();
	}

	/// <inheritdoc />
	public T? GetProperty<T>(string key)
	{
		if (string.IsNullOrWhiteSpace(key))
			return default;

		if (!Properties.TryGetValue(key, out var value))
			return default;

		if (value is T typedValue)
			return typedValue;

		return default;
	}

	/// <inheritdoc />
	public void SetProperty<T>(string key, T? value)
	{
		if (string.IsNullOrWhiteSpace(key))
			return;

		Properties[key] = value;
	}
}
