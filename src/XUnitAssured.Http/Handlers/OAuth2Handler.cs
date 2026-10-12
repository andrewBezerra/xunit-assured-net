using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
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
	private readonly HttpClient? _tokenClient;

	/// <summary>
	/// Creates a new OAuth2Handler with the specified configuration.
	/// </summary>
	/// <param name="config">OAuth 2.0 configuration</param>
	/// <param name="tokenCache">Token cache (optional)</param>
	public OAuth2Handler(OAuth2Config config, ITokenCache? tokenCache = null)
		: this(config, tokenCache, tokenClient: null)
	{
	}

	/// <summary>
	/// Creates a handler that asks for the token through <paramref name="tokenClient"/>, when
	/// there is one, instead of the network.
	/// </summary>
	/// <remarks>
	/// The client of a <c>WebApplicationFactory</c> reaches a server that lives in memory: a token
	/// URL on it answers only through that client.
	/// </remarks>
	internal OAuth2Handler(OAuth2Config config, ITokenCache? tokenCache, HttpClient? tokenClient)
	{
		_config = config ?? throw new ArgumentNullException(nameof(config));
		_tokenCache = tokenCache ?? new MemoryTokenCache();
		_tokenClient = tokenClient;
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
		ValidateConfig();
		var body = BuildTokenRequestBody();

		// A chave leva tudo o que vai no pedido do token. Com um cache dividido entre pedidos, só
		// cliente e URL deixariam um segredo errado, ou outros escopos, receberem o token já guardado.
		var cacheKey = "oauth2:" + _config.TokenUrl + "?" +
			string.Join("&", body.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => $"{p.Key}={p.Value}"));

		// Check cache first
		var cachedEntry = _tokenCache.Get(cacheKey);
		if (cachedEntry != null && cachedEntry.IsValid())
		{
			return cachedEntry.AccessToken;
		}

		// Request new token
		return RequestNewToken(body, cacheKey);
	}

	/// <summary>
	/// Requests a new access token from OAuth server.
	/// </summary>
	private string RequestNewToken(Dictionary<string, string> body, string cacheKey)
	{
		try
		{
			// Make token request
			var response = _tokenClient != null
				? PedirPeloCliente(_tokenClient, body)
				: _config.TokenUrl
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
			_tokenCache.Set(cacheKey, cacheEntry);

			return tokenResponse.access_token;
		}
		catch (FlurlHttpException ex)
		{
			throw new InvalidOperationException(
				$"OAuth token request failed: {ex.StatusCode} - {ex.GetResponseStringAsync().GetAwaiter().GetResult()}",
				ex);
		}
		catch (TokenRecusado ex)
		{
			throw new InvalidOperationException($"OAuth token request failed: {ex.Status} - {ex.Corpo}", ex);
		}
		catch (Exception ex)
		{
			throw new InvalidOperationException($"Failed to obtain OAuth access token: {ex.Message}", ex);
		}
	}

	/// <summary>
	/// Asks for the token through the client, as a form, the way the Flurl call does.
	/// </summary>
	private string PedirPeloCliente(HttpClient cliente, Dictionary<string, string> body)
	{
		using var pedido = new HttpRequestMessage(HttpMethod.Post, _config.TokenUrl)
		{
			Content = new FormUrlEncodedContent(body)
		};
		using var resposta = cliente.SendAsync(pedido).GetAwaiter().GetResult();
		var corpo = resposta.Content.ReadAsStringAsync().GetAwaiter().GetResult();

		if (!resposta.IsSuccessStatusCode)
			throw new TokenRecusado((int)resposta.StatusCode, corpo);

		return corpo;
	}

	/// <summary>
	/// The token server answered with an error; the message is made where the Flurl refusal's is.
	/// </summary>
	private sealed class TokenRecusado(int status, string corpo) : Exception
	{
		public int Status { get; } = status;
		public string Corpo { get; } = corpo;
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
