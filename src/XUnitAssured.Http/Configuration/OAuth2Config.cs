using System.Collections.Generic;

namespace XUnitAssured.Http.Configuration;

/// <summary>
/// Configuration for OAuth 2.0 authentication.
/// </summary>
public class OAuth2Config
{
	/// <summary>
	/// Token endpoint URL.
	/// Example: "https://auth.example.com/oauth/token"
	/// </summary>
	public string TokenUrl { get; set; } = string.Empty;

	/// <summary>
	/// OAuth 2.0 grant type.
	/// Default: ClientCredentials
	/// </summary>
	public OAuth2GrantType GrantType { get; set; } = OAuth2GrantType.ClientCredentials;

	/// <summary>
	/// Client ID.
	/// </summary>
	public string ClientId { get; set; } = string.Empty;

	/// <summary>
	/// Client Secret.
	/// </summary>
	public string ClientSecret { get; set; } = string.Empty;

	/// <summary>
	/// Username (for Password grant type).
	/// </summary>
	public string? Username { get; set; }

	/// <summary>
	/// Password (for Password grant type).
	/// </summary>
	public string? Password { get; set; }

	/// <summary>
	/// Scopes to request.
	/// Example: ["api.read", "api.write"]
	/// </summary>
	public List<string>? Scopes { get; set; }

	/// <summary>
	/// Refresh token (for RefreshToken grant type).
	/// </summary>
	public string? RefreshToken { get; set; }

	/// <summary>
	/// Authorization code (for AuthorizationCode grant type).
	/// </summary>
	public string? Code { get; set; }

	/// <summary>
	/// Redirect URI (for AuthorizationCode grant type).
	/// </summary>
	public string? RedirectUri { get; set; }

	/// <summary>
	/// Additional parameters to send in token request.
	/// </summary>
	public Dictionary<string, string>? AdditionalParameters { get; set; }
}
