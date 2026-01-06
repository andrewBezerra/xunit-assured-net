using Microsoft.AspNetCore.Mvc;

namespace SampleWebApi.Models;

/// <summary>
/// Authentication response model.
/// </summary>
public class AuthResponse
{
	public string AuthType { get; set; } = string.Empty;
	public bool Authenticated { get; set; }
	public string? Username { get; set; }
	public string Message { get; set; } = string.Empty;
	public Dictionary<string, string>? CustomData { get; set; }
}

/// <summary>
/// OAuth2 token request model.
/// </summary>
public class OAuth2TokenRequest
{
	public string GrantType { get; set; } = string.Empty;
	public string ClientId { get; set; } = string.Empty;
	public string ClientSecret { get; set; } = string.Empty;
	public string? Scope { get; set; }
}

/// <summary>
/// OAuth2 token response model.
/// </summary>
public class OAuth2TokenResponse
{
	[System.Text.Json.Serialization.JsonPropertyName("access_token")]
	public string AccessToken { get; set; } = string.Empty;

	[System.Text.Json.Serialization.JsonPropertyName("token_type")]
	public string TokenType { get; set; } = string.Empty;

	[System.Text.Json.Serialization.JsonPropertyName("expires_in")]
	public int ExpiresIn { get; set; }

	[System.Text.Json.Serialization.JsonPropertyName("scope")]
	public string Scope { get; set; } = string.Empty;
}
