namespace XUnitAssured.Http.Configuration;

/// <summary>
/// Configuration for Bearer token with automatic refresh.
/// </summary>
public class BearerWithAutoRefreshConfig
{
	/// <summary>
	/// Token endpoint URL.
	/// </summary>
	public string TokenEndpoint { get; set; } = string.Empty;
}
