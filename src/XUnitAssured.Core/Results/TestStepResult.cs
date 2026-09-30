using System;
using System.Collections.Generic;
using System.Globalization;
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

		return Coerce<T>(value);
	}

	/// <inheritdoc />
	public virtual T? GetData<T>()
	{
		return Coerce<T>(Data);
	}

	/// <summary>
	/// Converts a loosely-typed value to <typeparamref name="T"/>, returning
	/// <c>default</c> when the value is absent or genuinely not convertible.
	/// </summary>
	/// <remarks>
	/// Only conversion failures are absorbed. Any other exception — for example one
	/// thrown by a custom <see cref="IConvertible"/> implementation — propagates, so a
	/// real defect is not disguised as a missing value.
	/// </remarks>
	private static T? Coerce<T>(object? value)
	{
		if (value == null)
			return default;

		// Direct cast covers the common case, including T being the exact type.
		if (value is T typedValue)
			return typedValue;

		// Unwrap Nullable<T> so "int?" behaves the same as "int".
		var targetType = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);

		try
		{
			if (targetType.IsEnum)
			{
				return value is string enumText
					? (T)Enum.Parse(targetType, enumText, ignoreCase: true)
					: (T)Enum.ToObject(targetType, value);
			}

			// Guid, DateTime, DateTimeOffset and TimeSpan are common in step results
			// but are not IConvertible-compatible, so they need explicit parsing.
			if (value is string text)
			{
				if (targetType == typeof(Guid))
					return (T)(object)Guid.Parse(text);
				if (targetType == typeof(DateTimeOffset))
					return (T)(object)DateTimeOffset.Parse(text, CultureInfo.InvariantCulture);
				if (targetType == typeof(DateTime))
					return (T)(object)DateTime.Parse(text, CultureInfo.InvariantCulture);
				if (targetType == typeof(TimeSpan))
					return (T)(object)TimeSpan.Parse(text, CultureInfo.InvariantCulture);
			}

			if (value is IConvertible && typeof(IConvertible).IsAssignableFrom(targetType))
				return (T)Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);

			return default;
		}
		catch (Exception ex) when (
			ex is InvalidCastException
			or FormatException
			or OverflowException
			or ArgumentException)
		{
			// The value exists but does not represent a T.
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
			Errors = new List<string> { exception.ToString() },
			Properties = new Dictionary<string, object?>
			{
				["ExceptionType"] = exception.GetType().FullName,
				["ExceptionMessage"] = exception.Message,
				["ExceptionStackTrace"] = exception.StackTrace
			}
		};
	}
}
