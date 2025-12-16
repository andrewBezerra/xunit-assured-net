namespace XUnitAssured.Http.Configuration;

/// <summary>
/// Configuration for Bearer token authentication.
/// </summary>
public class BearerAuthConfig
{
	/// <summary>
	/// Bearer token value.
	/// </summary>
	public string Token { get; set; } = string.Empty;

	/// <summary>
	/// Token type prefix (usually "Bearer").
	/// </summary>
	public string Prefix { get; set; } = "Bearer";
}
