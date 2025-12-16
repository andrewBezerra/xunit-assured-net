namespace XUnitAssured.Http.Configuration;

/// <summary>
/// Supported authentication types for HTTP requests.
/// </summary>
public enum AuthenticationType
{
	/// <summary>
	/// No authentication.
	/// </summary>
	None = 0,

	/// <summary>
	/// Basic authentication (username + password).
	/// </summary>
	Basic = 1,

	/// <summary>
	/// Bearer token authentication (simple token).
	/// </summary>
	Bearer = 2,

	/// <summary>
	/// Bearer token with automatic refresh from token endpoint.
	/// </summary>
	BearerWithAutoRefresh = 3,

	/// <summary>
	/// API Key authentication (header or query parameter).
	/// </summary>
	ApiKey = 4,

	/// <summary>
	/// OAuth 2.0 authentication.
	/// </summary>
	OAuth2 = 5,

	/// <summary>
	/// Digest authentication (RFC 2617).
	/// </summary>
	Digest = 6,

	/// <summary>
	/// Custom header authentication.
	/// </summary>
	CustomHeader = 7,

	/// <summary>
	/// Certificate-based authentication (mTLS).
	/// </summary>
	Certificate = 8
}
