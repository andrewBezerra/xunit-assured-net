using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace SampleWebApi.Authentication;

public class CustomHeaderAuthenticationOptions : AuthenticationSchemeOptions
{
	public const string DefaultScheme = "CustomHeaderAuth";
	public string Scheme => DefaultScheme;
	public string TokenHeaderName { get; set; } = "X-Custom-Auth-Token";
	public string SessionHeaderName { get; set; } = "X-Custom-Session-Id";
	public string ValidToken { get; set; } = "custom-token-12345";
	public string ValidSessionId { get; set; } = "session-abc-xyz";
}

public class CustomHeaderAuthenticationHandler : AuthenticationHandler<CustomHeaderAuthenticationOptions>
{
	public CustomHeaderAuthenticationHandler(
		IOptionsMonitor<CustomHeaderAuthenticationOptions> options,
		ILoggerFactory logger,
		UrlEncoder encoder,
		ISystemClock clock)
		: base(options, logger, encoder, clock)
	{
	}

	protected override Task<AuthenticateResult> HandleAuthenticateAsync()
	{
		var authToken = Request.Headers[Options.TokenHeaderName].ToString();
		var sessionId = Request.Headers[Options.SessionHeaderName].ToString();

		if (string.IsNullOrEmpty(authToken) || string.IsNullOrEmpty(sessionId))
		{
			Logger.LogWarning("Custom Header authentication failed: Missing required headers");
			return Task.FromResult(AuthenticateResult.Fail($"Custom authentication headers required: {Options.TokenHeaderName} and {Options.SessionHeaderName}"));
		}

		// Validate custom headers
		if (authToken == Options.ValidToken && sessionId == Options.ValidSessionId)
		{
			var claims = new[]
			{
				new Claim(ClaimTypes.Name, "CustomHeaderUser"),
				new Claim(ClaimTypes.NameIdentifier, "CustomHeaderUser"),
				new Claim("AuthType", "CustomHeader"),
				new Claim("SessionId", sessionId)
			};

			var identity = new ClaimsIdentity(claims, Scheme.Name);
			var principal = new ClaimsPrincipal(identity);
			var ticket = new AuthenticationTicket(principal, Scheme.Name);

			Logger.LogInformation("Custom Header authentication successful");
			return Task.FromResult(AuthenticateResult.Success(ticket));
		}

		Logger.LogWarning("Custom Header authentication failed: Invalid headers");
		return Task.FromResult(AuthenticateResult.Fail("Invalid custom authentication headers"));
	}
}

public static class CustomHeaderAuthenticationExtensions
{
	public static AuthenticationBuilder AddCustomHeaderAuthentication(
		this AuthenticationBuilder builder,
		Action<CustomHeaderAuthenticationOptions>? configureOptions = null)
	{
		return builder.AddScheme<CustomHeaderAuthenticationOptions, CustomHeaderAuthenticationHandler>(
			CustomHeaderAuthenticationOptions.DefaultScheme,
			configureOptions);
	}
}
