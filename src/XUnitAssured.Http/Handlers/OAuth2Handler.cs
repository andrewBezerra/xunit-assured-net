using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Flurl.Http;
using XUnitAssured.Http.Configuration;
using XUnitAssured.Http.TokenManagement;

namespace XUnitAssured.Http.Handlers;

/// <summary>
/// Handler for OAuth 2.0 authentication.
/// Obtains access token from OAuth server and adds to Authorization header.
/// </summary>
public class OAuth2Handler : IAuthenticationHandler
{
	private readonly OAuth2Config _config;
	private readonly ITokenCache _tokenCache;
	private readonly string _cacheKey;

	/// <summary>
	/// Creates a new OAuth2Handler with the specified configuration.
	/// </summary>
	/// <param name="config">OAuth 2.0 configuration</param>
	/// <param name="tokenCache">Token cache (optional)</param>
	public OAuth2Handler(OAuth2Config config, ITokenCache? tokenCache = null)
	{
		_config = config ?? throw new ArgumentNullException(nameof(config));
		_tokenCache = tokenCache ?? new MemoryTokenCache();
		_cacheKey = $"oauth2:{_config.ClientId}:{_config.TokenUrl}";
	}

	/// <inheritdoc />
	public AuthenticationType Type => AuthenticationType.OAuth2;

	/// <inheritdoc />
	public void ApplyAuthentication(IFlurlRequest request)
	{
		if (request == null)
			throw new ArgumentNullException(nameof(request));

		// Get access token (from cache or by requesting new one)
		var accessToken = GetAccessToken();

		// Add Authorization header
		request.WithHeader("Authorization", $"Bearer {accessToken}");
	}

	/// <inheritdoc />
	public bool CanHandle(HttpAuthConfig authConfig)
	{
		return authConfig?.Type == AuthenticationType.OAuth2 && authConfig.OAuth2 != null;
	}

	/// <summary>
	/// Gets access token (from cache or by requesting new one).
	/// </summary>
	private string GetAccessToken()
	{
		// Check cache first
		var cachedEntry = _tokenCache.Get(_cacheKey);
		if (cachedEntry != null && cachedEntry.IsValid())
		{
			return cachedEntry.AccessToken;
		}

		// Request new token
		return RequestNewToken();
	}

	/// <summary>
	/// Requests a new access token from OAuth server.
	/// </summary>
	private string RequestNewToken()
	{
		ValidateConfig();

		try
		{
			// Build token request body
			var body = BuildTokenRequestBody();

			// Make token request
			var response = _config.TokenUrl
				.PostUrlEncodedAsync(body)
				.ReceiveString()
				.GetAwaiter()
				.GetResult();

			// Parse token response
			var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(response);
			if (tokenResponse == null || string.IsNullOrWhiteSpace(tokenResponse.access_token))
				throw new InvalidOperationException("Failed to obtain access token from OAuth server.");

			// Cache token
			var expiresIn = tokenResponse.expires_in > 0 ? tokenResponse.expires_in : 3600;
			var cacheEntry = TokenCacheEntry.Create(tokenResponse.access_token, expiresIn);
			if (!string.IsNullOrWhiteSpace(tokenResponse.refresh_token))
			{
				cacheEntry.RefreshToken = tokenResponse.refresh_token;
			}
			_tokenCache.Set(_cacheKey, cacheEntry);

			return tokenResponse.access_token;
		}
		catch (FlurlHttpException ex)
		{
			throw new InvalidOperationException(
				$"OAuth token request failed: {ex.StatusCode} - {ex.GetResponseStringAsync().GetAwaiter().GetResult()}", 
				ex);
		}
		catch (Exception ex)
		{
			throw new InvalidOperationException($"Failed to obtain OAuth access token: {ex.Message}", ex);
		}
	}

	/// <summary>
	/// Builds the token request body based on grant type.
	/// </summary>
	private Dictionary<string, string> BuildTokenRequestBody()
	{
		var body = new Dictionary<string, string>();

		switch (_config.GrantType)
		{
			case OAuth2GrantType.ClientCredentials:
				body["grant_type"] = "client_credentials";
				body["client_id"] = _config.ClientId;
				body["client_secret"] = _config.ClientSecret;
				break;

			case OAuth2GrantType.Password:
				body["grant_type"] = "password";
				body["client_id"] = _config.ClientId;
				body["client_secret"] = _config.ClientSecret;
				body["username"] = _config.Username ?? throw new InvalidOperationException("Username required for Password grant");
				body["password"] = _config.Password ?? throw new InvalidOperationException("Password required for Password grant");
				break;

			case OAuth2GrantType.RefreshToken:
				body["grant_type"] = "refresh_token";
				body["client_id"] = _config.ClientId;
				body["client_secret"] = _config.ClientSecret;
				body["refresh_token"] = _config.RefreshToken ?? throw new InvalidOperationException("Refresh token required");
				break;

			case OAuth2GrantType.AuthorizationCode:
				body["grant_type"] = "authorization_code";
				body["client_id"] = _config.ClientId;
				body["client_secret"] = _config.ClientSecret;
				body["code"] = _config.Code ?? throw new InvalidOperationException("Authorization code required");
				body["redirect_uri"] = _config.RedirectUri ?? throw new InvalidOperationException("Redirect URI required");
				break;
		}

		// Add scopes
		if (_config.Scopes != null && _config.Scopes.Any())
		{
			body["scope"] = string.Join(" ", _config.Scopes);
		}

		// Add additional parameters
		if (_config.AdditionalParameters != null)
		{
			foreach (var param in _config.AdditionalParameters)
			{
				body[param.Key] = param.Value;
			}
		}

		return body;
	}

	/// <summary>
	/// Validates OAuth configuration.
	/// </summary>
	private void ValidateConfig()
	{
		if (string.IsNullOrWhiteSpace(_config.TokenUrl))
			throw new InvalidOperationException("TokenUrl is required for OAuth2 authentication.");

		if (string.IsNullOrWhiteSpace(_config.ClientId))
			throw new InvalidOperationException("ClientId is required for OAuth2 authentication.");

		if (string.IsNullOrWhiteSpace(_config.ClientSecret))
			throw new InvalidOperationException("ClientSecret is required for OAuth2 authentication.");
	}

	/// <summary>
	/// Token response model.
	/// </summary>
	private class TokenResponse
	{
		public string? access_token { get; set; }
		public string? token_type { get; set; }
		public int expires_in { get; set; }
		public string? refresh_token { get; set; }
		public string? scope { get; set; }
	}
}
