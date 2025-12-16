using System;
using System.Collections.Generic;

namespace XUnitAssured.Core.Results;

/// <summary>
/// Represents the result of a test step execution.
/// This is a technology-agnostic interface that can be extended by specific implementations
/// (HTTP, Kafka, FileSystem, S3, etc.) to provide additional context-specific data.
/// </summary>
public interface ITestStepResult
{
	/// <summary>
	/// Metadata about the step execution (timing, status, attempts, etc.)
	/// </summary>
	StepMetadata Metadata { get; }

	/// <summary>
	/// Indicates whether the step execution was successful.
	/// </summary>
	bool Success { get; }

	/// <summary>
	/// Collection of error messages if the step failed.
	/// Empty if the step was successful.
	/// </summary>
	IReadOnlyList<string> Errors { get; }

	/// <summary>
	/// The primary data/payload returned by the step.
	/// For HTTP: response body
	/// For Kafka: consumed message
	/// For FileSystem: file info
	/// For S3: object metadata
	/// </summary>
	object? Data { get; }

	/// <summary>
	/// The type of the data returned (helps with safe casting).
	/// </summary>
	Type? DataType { get; }

	/// <summary>
	/// Technology-specific properties stored as key-value pairs.
	/// Examples:
	/// - HTTP: ["StatusCode"] = 200, ["Headers"] = {...}
	/// - Kafka: ["Partition"] = 2, ["Offset"] = 12345
	/// - S3: ["ETag"] = "abc123", ["VersionId"] = "v1"
	/// </summary>
	IReadOnlyDictionary<string, object?> Properties { get; }

	/// <summary>
	/// Retrieves a specific property value by key.
	/// Returns default(T) if the key doesn't exist.
	/// </summary>
	/// <typeparam name="T">Expected type of the property</typeparam>
	/// <param name="key">Property key</param>
	/// <returns>Property value or default(T)</returns>
	T? GetProperty<T>(string key);

	/// <summary>
	/// Retrieves the main data converted to the specified type.
	/// </summary>
	/// <typeparam name="T">Expected type of the data</typeparam>
	/// <returns>Data converted to T or default(T)</returns>
	T? GetData<T>();
}
