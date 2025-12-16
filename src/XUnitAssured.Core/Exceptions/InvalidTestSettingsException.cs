using System;

namespace XUnitAssured.Core.Exceptions;

/// <summary>
/// Exception thrown when test settings configuration is invalid.
/// Supports inner exceptions to preserve the original error context.
/// This is a general-purpose exception that can be used across all XUnitAssured packages.
/// </summary>
public class InvalidTestSettingsException : Exception
{
	/// <summary>
	/// Creates a new instance of InvalidTestSettingsException.
	/// </summary>
	/// <param name="message">The error message</param>
	public InvalidTestSettingsException(string? message) : base(message)
	{
	}

	/// <summary>
	/// Creates a new instance of InvalidTestSettingsException with an inner exception.
	/// </summary>
	/// <param name="message">The error message</param>
	/// <param name="innerException">The exception that caused this exception</param>
	public InvalidTestSettingsException(string? message, Exception? innerException) 
		: base(message, innerException)
	{
	}
}
