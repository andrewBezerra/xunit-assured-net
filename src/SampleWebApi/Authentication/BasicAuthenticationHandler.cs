using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;

namespace SampleWebApi.Authentication;

public class BasicAuthenticationOptions : AuthenticationSchemeOptions
{
	public const string DefaultScheme = "BasicAuth";
	public string Scheme => DefaultScheme;
	public string Username { get; set; } = "admin";
	public string Password { get; set; } = "secret123";
	public string Realm { get; set; } = "SampleWebApi";
}

public class BasicAuthenticationHandler : AuthenticationHandler<BasicAuthenticationOptions>
{
	public BasicAuthenticationHandler(
		IOptionsMonitor<BasicAuthenticationOptions> options,
		ILoggerFactory logger,
		UrlEncoder encoder,
		ISystemClock clock)
		: base(options, logger, encoder, clock)
	{
	}

	protected override Task<AuthenticateResult> HandleAuthenticateAsync()
	{
		var authHeader = Request.Headers["Authorization"].ToString();

		if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
		{
			return Task.FromResult(AuthenticateResult.Fail("Missing or invalid Authorization header"));
		}

		try
		{
			var encodedCredentials = authHeader.Substring("Basic ".Length).Trim();
			var decodedCredentials = Encoding.UTF8.GetString(Convert.FromBase64String(encodedCredentials));
			var credentials = decodedCredentials.Split(':', 2);

			if (credentials.Length != 2)
			{
				return Task.FromResult(AuthenticateResult.Fail("Invalid credentials format"));
			}

			var username = credentials[0];
			var password = credentials[1];

			// Validate credentials
			if (username == Options.Username && password == Options.Password)
			{
				var claims = new[]
				{
					new Claim(ClaimTypes.Name, username),
					new Claim(ClaimTypes.NameIdentifier, username),
					new Claim("AuthType", "Basic")
				};

				var identity = new ClaimsIdentity(claims, Scheme.Name);
				var principal = new ClaimsPrincipal(identity);
				var ticket = new AuthenticationTicket(principal, Scheme.Name);

				Logger.LogInformation("Basic authentication successful for user: {Username}", username);
				return Task.FromResult(AuthenticateResult.Success(ticket));
			}

			Logger.LogWarning("Basic authentication failed: Invalid credentials for user: {Username}", username);
			return Task.FromResult(AuthenticateResult.Fail("Invalid username or password"));
		}
		catch (Exception ex)
		{
			Logger.LogError(ex, "Basic authentication error");
			return Task.FromResult(AuthenticateResult.Fail("Authentication error"));
		}
	}

	protected override Task HandleChallengeAsync(AuthenticationProperties properties)
	{
		Response.Headers["WWW-Authenticate"] = $"Basic realm=\"{Options.Realm ?? "SampleWebApi"}\"";
		return base.HandleChallengeAsync(properties);
	}
}

public static class BasicAuthenticationExtensions
{
	public static AuthenticationBuilder AddBasicAuthentication(
		this AuthenticationBuilder builder,
		Action<BasicAuthenticationOptions>? configureOptions = null)
	{
		return builder.AddScheme<BasicAuthenticationOptions, BasicAuthenticationHandler>(
			BasicAuthenticationOptions.DefaultScheme,
			configureOptions);
	}
}
