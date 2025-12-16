using System;
using System.Collections.Generic;
using System.Linq;

namespace XUnitAssured.Core.Results;

/// <summary>
/// Metadata about the execution of a test step.
/// Contains timing, status, and execution information independent of the technology used.
/// </summary>
public class StepMetadata
{
	/// <summary>
	/// Timestamp when the step execution started (UTC).
	/// </summary>
	public DateTimeOffset StartedAt { get; init; }

	/// <summary>
	/// Timestamp when the step execution completed (UTC).
	/// Null if the step is still running.
	/// </summary>
	public DateTimeOffset? CompletedAt { get; init; }

	/// <summary>
	/// Duration of the step execution.
	/// Returns TimeSpan.Zero if the step hasn't completed yet.
	/// </summary>
	public TimeSpan Duration =>
		CompletedAt.HasValue
			? CompletedAt.Value - StartedAt
			: TimeSpan.Zero;

	/// <summary>
	/// Current status of the step execution.
	/// </summary>
	public StepStatus Status { get; init; }

	/// <summary>
	/// Number of execution attempts (useful for retry scenarios).
	/// Default is 1 (first attempt).
	/// </summary>
	public int AttemptCount { get; init; } = 1;

	/// <summary>
	/// Optional tags for categorizing or filtering steps.
	/// Examples: "critical", "performance", "integration"
	/// </summary>
	public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();

	/// <summary>
	/// Creates a new StepMetadata with default values (NotStarted status).
	/// </summary>
	public StepMetadata()
	{
		StartedAt = DateTimeOffset.UtcNow;
		Status = StepStatus.NotStarted;
	}

	/// <summary>
	/// Creates a copy of this metadata with updated status.
	/// </summary>
	public StepMetadata WithStatus(StepStatus newStatus)
	{
		return new StepMetadata
		{
			StartedAt = StartedAt,
			CompletedAt = newStatus is StepStatus.Succeeded or StepStatus.Failed or StepStatus.Cancelled or StepStatus.Skipped
				? DateTimeOffset.UtcNow
				: CompletedAt,
			Status = newStatus,
			AttemptCount = AttemptCount,
			Tags = Tags
		};
	}

	/// <summary>
	/// Creates a copy of this metadata with incremented attempt count.
	/// </summary>
	public StepMetadata WithIncrementedAttempt()
	{
		return new StepMetadata
		{
			StartedAt = StartedAt,
			CompletedAt = CompletedAt,
			Status = Status,
			AttemptCount = AttemptCount + 1,
			Tags = Tags
		};
	}
}

/// <summary>
/// Represents the execution status of a test step.
/// </summary>
public enum StepStatus
{
	/// <summary>
	/// Step has not started execution yet.
	/// </summary>
	NotStarted = 0,

	/// <summary>
	/// Step is currently executing.
	/// </summary>
	Running = 1,

	/// <summary>
	/// Step completed successfully.
	/// </summary>
	Succeeded = 2,

	/// <summary>
	/// Step failed during execution.
	/// </summary>
	Failed = 3,

	/// <summary>
	/// Step was skipped (conditional execution).
	/// </summary>
	Skipped = 4,

	/// <summary>
	/// Step execution was cancelled.
	/// </summary>
	Cancelled = 5
}
