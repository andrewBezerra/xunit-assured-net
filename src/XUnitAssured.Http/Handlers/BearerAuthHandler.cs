using System;
using Flurl.Http;
using XUnitAssured.Http.Configuration;

namespace XUnitAssured.Http.Handlers;

/// <summary>
/// Handler for Bearer Token Authentication.
/// Adds Authorization header with Bearer token.
/// </summary>
public class BearerAuthHandler : IAuthenticationHandler
{
	private readonly BearerAuthConfig _config;

	/// <summary>
	/// Creates a new BearerAuthHandler with the specified configuration.
	/// </summary>
	/// <param name="config">Bearer authentication configuration</param>
	public BearerAuthHandler(BearerAuthConfig config)
	{
		_config = config ?? throw new ArgumentNullException(nameof(config));
	}

	/// <inheritdoc />
	public AuthenticationType Type => AuthenticationType.Bearer;

	/// <inheritdoc />
	public void ApplyAuthentication(IFlurlRequest request)
	{
		if (request == null)
			throw new ArgumentNullException(nameof(request));

		if (string.IsNullOrWhiteSpace(_config.Token))
			throw new InvalidOperationException("Token is required for Bearer authentication.");

		// Add Authorization header with prefix (default: Bearer)
		var prefix = string.IsNullOrWhiteSpace(_config.Prefix) ? "Bearer" : _config.Prefix;
		request.WithHeader("Authorization", $"{prefix} {_config.Token}");
	}

	/// <inheritdoc />
	public bool CanHandle(HttpAuthConfig authConfig)
	{
		return authConfig?.Type == AuthenticationType.Bearer && authConfig.Bearer != null;
	}
}
