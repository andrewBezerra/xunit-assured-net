namespace XUnitAssured.Http.Configuration;

/// <summary>
/// Configuration for API Key authentication.
/// </summary>
public class ApiKeyAuthConfig
{
	/// <summary>
	/// Name of the header or query parameter.
	/// Examples: "X-API-Key", "api_key", "apiKey"
	/// </summary>
	public string KeyName { get; set; } = "X-API-Key";

	/// <summary>
	/// API key value.
	/// </summary>
	public string KeyValue { get; set; } = string.Empty;

	/// <summary>
	/// Location where to send the API key.
	/// Default: Header
	/// </summary>
	public ApiKeyLocation Location { get; set; } = ApiKeyLocation.Header;
}
