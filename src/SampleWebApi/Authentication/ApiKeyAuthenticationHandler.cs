using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace SampleWebApi.Authentication;

public class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
	public const string DefaultScheme = "ApiKeyAuth";
	public string Scheme => DefaultScheme;
	public string HeaderName { get; set; } = "X-API-Key";
	public string QueryParameterName { get; set; } = "api_key";
	public string ValidApiKeyHeader { get; set; } = "api-key-header-abc123xyz";
	public string ValidApiKeyQuery { get; set; } = "api-key-query-xyz789abc";
}

public class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions>
{
	public ApiKeyAuthenticationHandler(
		IOptionsMonitor<ApiKeyAuthenticationOptions> options,
		ILoggerFactory logger,
		UrlEncoder encoder,
		ISystemClock clock)
		: base(options, logger, encoder, clock)
	{
	}

	protected override Task<AuthenticateResult> HandleAuthenticateAsync()
	{
		string? apiKey = null;
		string source = string.Empty;

		// Check header first
		if (Request.Headers.ContainsKey(Options.HeaderName))
		{
			apiKey = Request.Headers[Options.HeaderName].ToString();
			source = "Header";
		}
		// Then check query parameter
		else if (Request.Query.ContainsKey(Options.QueryParameterName))
		{
			apiKey = Request.Query[Options.QueryParameterName].ToString();
			source = "Query";
		}

		if (string.IsNullOrEmpty(apiKey))
		{
			Logger.LogWarning("API Key authentication failed: No API key provided");
			return Task.FromResult(AuthenticateResult.Fail("API Key required"));
		}

		// Validate API key based on source
		var isValid = source switch
		{
			"Header" => apiKey == Options.ValidApiKeyHeader,
			"Query" => apiKey == Options.ValidApiKeyQuery,
			_ => false
		};

		if (isValid)
		{
			var claims = new[]
			{
				new Claim(ClaimTypes.Name, "ApiKeyUser"),
				new Claim(ClaimTypes.NameIdentifier, "ApiKeyUser"),
				new Claim("AuthType", $"ApiKey-{source}"),
				new Claim("ApiKeySource", source)
			};

			var identity = new ClaimsIdentity(claims, Scheme.Name);
			var principal = new ClaimsPrincipal(identity);
			var ticket = new AuthenticationTicket(principal, Scheme.Name);

			Logger.LogInformation("API Key ({Source}) authentication successful", source);
			return Task.FromResult(AuthenticateResult.Success(ticket));
		}

		Logger.LogWarning("API Key authentication failed: Invalid key");
		return Task.FromResult(AuthenticateResult.Fail("Invalid API Key"));
	}
}

public static class ApiKeyAuthenticationExtensions
{
	public static AuthenticationBuilder AddApiKeyAuthentication(
		this AuthenticationBuilder builder,
		Action<ApiKeyAuthenticationOptions>? configureOptions = null)
	{
		return builder.AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(
			ApiKeyAuthenticationOptions.DefaultScheme,
			configureOptions);
	}
}
