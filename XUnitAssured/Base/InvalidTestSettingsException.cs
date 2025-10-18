using System;

namespace XUnitAssured.Base;

public class InvalidTestSettingsException : Exception
{
	public InvalidTestSettingsException(string? message) : base(message)
	{
	}
}