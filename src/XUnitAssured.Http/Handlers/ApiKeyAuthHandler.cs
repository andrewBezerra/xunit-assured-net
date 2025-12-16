using System;
using Flurl.Http;
using XUnitAssured.Http.Configuration;

namespace XUnitAssured.Http.Handlers;

/// <summary>
/// Handler for API Key authentication.
/// Adds API key to header or query string.
/// </summary>
public class ApiKeyAuthHandler : IAuthenticationHandler
{
	private readonly ApiKeyAuthConfig _config;

	/// <summary>
	/// Creates a new ApiKeyAuthHandler with the specified configuration.
	/// </summary>
	/// <param name="config">API Key authentication configuration</param>
	public ApiKeyAuthHandler(ApiKeyAuthConfig config)
	{
		_config = config ?? throw new ArgumentNullException(nameof(config));
	}

	/// <inheritdoc />
	public AuthenticationType Type => AuthenticationType.ApiKey;

	/// <inheritdoc />
	public void ApplyAuthentication(IFlurlRequest request)
	{
		if (request == null)
			throw new ArgumentNullException(nameof(request));

		if (string.IsNullOrWhiteSpace(_config.KeyName))
			throw new InvalidOperationException("API Key name is required.");

		if (string.IsNullOrWhiteSpace(_config.KeyValue))
			throw new InvalidOperationException("API Key value is required.");

		// Add API key based on location
		if (_config.Location == ApiKeyLocation.Header)
		{
			request.WithHeader(_config.KeyName, _config.KeyValue);
		}
		else // Query
		{
			request.SetQueryParam(_config.KeyName, _config.KeyValue);
		}
	}

	/// <inheritdoc />
	public bool CanHandle(HttpAuthConfig authConfig)
	{
		return authConfig?.Type == AuthenticationType.ApiKey && authConfig.ApiKey != null;
	}
}
