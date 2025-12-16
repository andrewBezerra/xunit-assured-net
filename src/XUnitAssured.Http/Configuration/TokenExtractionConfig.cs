namespace XUnitAssured.Http.Configuration;

/// <summary>
/// Configuration for extracting token information from authentication response.
/// Uses JSONPath-like syntax to extract values from JSON response.
/// </summary>
public class TokenExtractionConfig
{
	/// <summary>
	/// JSONPath to extract access token from response.
	/// Examples: "$.access_token", "$.data.token", "$.token"
	/// Default: "$.access_token"
	/// </summary>
	public string JsonPath { get; set; } = "$.access_token";

	/// <summary>
	/// JSONPath to extract expiration time (in seconds) from response.
	/// Examples: "$.expires_in", "$.exp", "$.expiration"
	/// Default: "$.expires_in"
	/// </summary>
	public string? ExpiresInPath { get; set; } = "$.expires_in";

	/// <summary>
	/// JSONPath to extract refresh token from response.
	/// Examples: "$.refresh_token", "$.data.refresh_token"
	/// Default: "$.refresh_token"
	/// </summary>
	public string? RefreshTokenPath { get; set; } = "$.refresh_token";

	/// <summary>
	/// Token type prefix to use in Authorization header.
	/// Examples: "Bearer", "JWT", "Token"
	/// Default: "Bearer"
	/// </summary>
	public string TokenTypePrefix { get; set; } = "Bearer";
}
