using System;
using System.Collections.Generic;
using System.Linq;

namespace XUnitAssured.Core.Results;

/// <summary>
/// Base implementation of ITestStepResult.
/// Provides common functionality for all test step result types.
/// Technology-specific implementations should inherit from this class.
/// </summary>
public class TestStepResult : ITestStepResult
{
	/// <inheritdoc />
	public StepMetadata Metadata { get; init; } = new();

	/// <inheritdoc />
	public bool Success { get; init; }

	/// <inheritdoc />
	public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();

	/// <inheritdoc />
	public object? Data { get; init; }

	/// <inheritdoc />
	public Type? DataType { get; init; }

	/// <inheritdoc />
	public IReadOnlyDictionary<string, object?> Properties { get; init; } = 
		new Dictionary<string, object?>();

	/// <inheritdoc />
	public virtual T? GetProperty<T>(string key)
	{
		if (string.IsNullOrWhiteSpace(key))
			return default;

		if (!Properties.TryGetValue(key, out var value))
			return default;

		if (value == null)
			return default;

		try
		{
			// Try direct cast first
			if (value is T typedValue)
				return typedValue;

			// Try conversion for compatible types
			if (typeof(T).IsAssignableFrom(value.GetType()))
				return (T)value;

			// Try Convert.ChangeType for primitives
			if (typeof(T).IsPrimitive || typeof(T) == typeof(string) || typeof(T) == typeof(decimal))
				return (T)Convert.ChangeType(value, typeof(T));

			return default;
		}
		catch
		{
			return default;
		}
	}

	/// <inheritdoc />
	public virtual T? GetData<T>()
	{
		if (Data == null)
			return default;

		try
		{
			// Try direct cast first
			if (Data is T typedData)
				return typedData;

			// Try conversion for compatible types
			if (typeof(T).IsAssignableFrom(Data.GetType()))
				return (T)Data;

			// Try Convert.ChangeType for primitives
			if (typeof(T).IsPrimitive || typeof(T) == typeof(string) || typeof(T) == typeof(decimal))
				return (T)Convert.ChangeType(Data, typeof(T));

			return default;
		}
		catch
		{
			return default;
		}
	}

	/// <summary>
	/// Creates a successful result with the specified data.
	/// </summary>
	public static TestStepResult CreateSuccess(object? data = null, Dictionary<string, object?>? properties = null)
	{
		return new TestStepResult
		{
			Metadata = new StepMetadata
			{
				StartedAt = DateTimeOffset.UtcNow,
				CompletedAt = DateTimeOffset.UtcNow,
				Status = StepStatus.Succeeded
			},
			Success = true,
			Data = data,
			DataType = data?.GetType(),
			Properties = properties ?? new Dictionary<string, object?>()
		};
	}

	/// <summary>
	/// Creates a failed result with the specified errors.
	/// </summary>
	public static TestStepResult CreateFailure(params string[] errors)
	{
		return new TestStepResult
		{
			Metadata = new StepMetadata
			{
				StartedAt = DateTimeOffset.UtcNow,
				CompletedAt = DateTimeOffset.UtcNow,
				Status = StepStatus.Failed
			},
			Success = false,
			Errors = errors?.ToList() ?? new List<string>()
		};
	}

	/// <summary>
	/// Creates a failed result from an exception.
	/// </summary>
	public static TestStepResult CreateFailure(Exception exception)
	{
		return new TestStepResult
		{
			Metadata = new StepMetadata
			{
				StartedAt = DateTimeOffset.UtcNow,
				CompletedAt = DateTimeOffset.UtcNow,
				Status = StepStatus.Failed
			},
			Success = false,
			Errors = new List<string> { exception.Message }
		};
	}
}
