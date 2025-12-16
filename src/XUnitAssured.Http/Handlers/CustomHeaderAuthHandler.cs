using System;
using System.Linq;
using Flurl.Http;
using XUnitAssured.Http.Configuration;

namespace XUnitAssured.Http.Handlers;

/// <summary>
/// Handler for Custom Header authentication.
/// Adds one or more custom authentication headers to the request.
/// </summary>
public class CustomHeaderAuthHandler : IAuthenticationHandler
{
	private readonly CustomHeaderAuthConfig _config;

	/// <summary>
	/// Creates a new CustomHeaderAuthHandler with the specified configuration.
	/// </summary>
	/// <param name="config">Custom header authentication configuration</param>
	public CustomHeaderAuthHandler(CustomHeaderAuthConfig config)
	{
		_config = config ?? throw new ArgumentNullException(nameof(config));
	}

	/// <inheritdoc />
	public AuthenticationType Type => AuthenticationType.CustomHeader;

	/// <inheritdoc />
	public void ApplyAuthentication(IFlurlRequest request)
	{
		if (request == null)
			throw new ArgumentNullException(nameof(request));

		if (_config.Headers == null || !_config.Headers.Any())
			throw new InvalidOperationException("At least one custom header is required.");

		// Add all custom headers
		foreach (var header in _config.Headers)
		{
			if (!string.IsNullOrWhiteSpace(header.Key) && !string.IsNullOrWhiteSpace(header.Value))
			{
				request.WithHeader(header.Key, header.Value);
			}
		}
	}

	/// <inheritdoc />
	public bool CanHandle(HttpAuthConfig authConfig)
	{
		return authConfig?.Type == AuthenticationType.CustomHeader && authConfig.CustomHeader != null;
	}
}
