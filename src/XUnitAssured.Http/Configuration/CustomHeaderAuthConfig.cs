using System.Collections.Generic;

namespace XUnitAssured.Http.Configuration;

/// <summary>
/// Configuration for Custom Header authentication.
/// Allows sending one or more custom authentication headers.
/// </summary>
public class CustomHeaderAuthConfig
{
	/// <summary>
	/// Custom authentication headers to send.
	/// Key: Header name, Value: Header value
	/// Examples: 
	/// - "X-Auth-Token": "abc123"
	/// - "X-Session-ID": "session-xyz"
	/// - "X-Request-ID": "req-123"
	/// </summary>
	public Dictionary<string, string> Headers { get; set; } = new();

	/// <summary>
	/// Adds a custom header.
	/// </summary>
	public void AddHeader(string name, string value)
	{
		if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(value))
		{
			Headers[name] = value;
		}
	}
}
