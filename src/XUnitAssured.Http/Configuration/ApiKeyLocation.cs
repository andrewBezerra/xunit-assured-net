namespace XUnitAssured.Http.Configuration;

/// <summary>
/// Location where API key can be sent.
/// </summary>
public enum ApiKeyLocation
{
	/// <summary>
	/// API key in HTTP header.
	/// Example: X-API-Key: abc123
	/// </summary>
	Header = 0,

	/// <summary>
	/// API key in query string parameter.
	/// Example: ?api_key=abc123
	/// </summary>
	Query = 1
}
