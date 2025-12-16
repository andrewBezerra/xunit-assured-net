namespace XUnitAssured.Http.Configuration;

/// <summary>
/// OAuth 2.0 grant types.
/// </summary>
public enum OAuth2GrantType
{
	/// <summary>
	/// Client Credentials grant type.
	/// Used for machine-to-machine authentication.
	/// </summary>
	ClientCredentials = 0,

	/// <summary>
	/// Resource Owner Password Credentials grant type.
	/// Used when user provides username and password.
	/// </summary>
	Password = 1,

	/// <summary>
	/// Authorization Code grant type.
	/// Used for web applications with redirect.
	/// </summary>
	AuthorizationCode = 2,

	/// <summary>
	/// Refresh Token grant type.
	/// Used to refresh an expired access token.
	/// </summary>
	RefreshToken = 3
}
