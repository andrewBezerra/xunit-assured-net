using System;
using System.Net.Http;
using XUnitAssured.Core.Abstractions;
using XUnitAssured.Core.Configuration;
using XUnitAssured.Http.Configuration;

namespace XUnitAssured.Http.Samples.Remote.Test;

/// <summary>
/// Test fixture for remote HTTP samples using XUnitAssured.Http.
/// Connects to a remotely published SampleWebApi application for integration testing.
/// Implements IHttpClientProvider to work seamlessly with Given(fixture) syntax.
/// Implements IHttpClientAuthProvider to automatically apply authentication from testsettings.json.
/// </summary>
/// <remarks>
/// This fixture is designed for testing against a remote/deployed API instance.
/// Configuration is loaded from testsettings.json:
/// <code>
/// {
///   "testMode": "Remote",
///   "environment": "staging",
///   "http": {
///     "baseUrl": "https://your-remote-api.com",
///     "timeout": 60,
///     "defaultHeaders": {
///       "X-Test-Source": "XUnitAssured"
///     },
///     "authentication": {
///       "type": "Basic",
///       "basic": {
///         "username": "admin",
///         "password": "secret123"
///       }
///     }
///   }
/// }
/// </code>
/// 
/// Supports environment variables:
/// <code>
/// {
///   "http": {
///     "baseUrl": "${ENV:REMOTE_API_URL}"
///   }
/// }
/// </code>
/// 
/// Set via environment variable:
/// SET REMOTE_API_URL=https://api.staging.com
/// dotnet test
/// 
/// Authentication configured in testsettings.json will be automatically applied
/// to all requests made through this fixture. You can still override it per-test
/// using authentication methods like WithBasicAuth(), WithBearerToken(), etc.
/// </remarks>
public class HttpSamplesRemoteFixture : IHttpClientProvider, IHttpClientAuthProvider, IDisposable
{
	private readonly HttpSettings _httpSettings;
	private readonly HttpClient _httpClient;
	private bool _disposed;

	public HttpSamplesRemoteFixture()
	{
		// Load test settings from testsettings.json
		var settings = TestSettings.Load();

		// Validate test mode
		if (settings.TestMode != TestMode.Remote)
		{
			throw new InvalidOperationException(
				$"Test mode must be 'Remote' for remote tests. Current mode: {settings.TestMode}. " +
				"Update testsettings.json to set 'testMode': 'Remote'");
		}

		// Get HTTP settings
		_httpSettings = settings.GetHttpSettings();
		if (_httpSettings == null)
		{
			throw new InvalidOperationException(
				"HTTP settings not found in testsettings.json. " +
				"Ensure 'http' section exists with 'baseUrl' configured.");
		}

		// Validate base URL
		if (string.IsNullOrWhiteSpace(_httpSettings.BaseUrl))
		{
			throw new InvalidOperationException(
				"HTTP baseUrl is not configured in testsettings.json. " +
				"Set 'http.baseUrl' to your remote API URL (e.g., 'https://api.staging.com')");
		}

		// Validate URL format
		if (!Uri.TryCreate(_httpSettings.BaseUrl, UriKind.Absolute, out var baseUri))
		{
			throw new InvalidOperationException(
				$"Invalid baseUrl format: '{_httpSettings.BaseUrl}'. " +
				"Must be a valid absolute URL (e.g., 'https://api.example.com')");
		}

		BaseUrl = _httpSettings.BaseUrl.TrimEnd('/');

		// Create HTTP client with configured settings
		_httpClient = new HttpClient
		{
			BaseAddress = baseUri,
			Timeout = TimeSpan.FromSeconds(_httpSettings.Timeout)
		};

		// Apply default headers if configured
		if (_httpSettings.DefaultHeaders != null)
		{
			foreach (var header in _httpSettings.DefaultHeaders)
			{
				_httpClient.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, header.Value);
			}
		}

		// Apply authentication if configured
		ApplyAuthentication(_httpClient, _httpSettings.Authentication);
	}

	/// <summary>
	/// Base URL of the remote API server.
	/// </summary>
	public string BaseUrl { get; }

	/// <summary>
	/// HTTP settings loaded from configuration.
	/// </summary>
	public HttpSettings HttpSettings => _httpSettings;

	/// <summary>
	/// Authentication configuration loaded from testsettings.json.
	/// Returns null if no authentication is configured.
	/// </summary>
	public HttpAuthConfig? AuthenticationConfig => _httpSettings.Authentication;

	/// <summary>
	/// Gets the authentication configuration to be automatically applied to HTTP requests.
	/// Implements IHttpClientAuthProvider interface.
	/// </summary>
	/// <returns>The authentication configuration from testsettings.json, or null if none configured.</returns>
	public HttpAuthConfig? GetAuthenticationConfig() => AuthenticationConfig;

	/// <summary>
	/// Creates an HttpClient for direct API calls if needed.
	/// Returns the pre-configured client instance.
	/// </summary>
	public HttpClient CreateClient() => _httpClient;

	/// <summary>
	/// Applies authentication configuration to the HTTP client.
	/// </summary>
	private void ApplyAuthentication(HttpClient client, HttpAuthConfig? authConfig)
	{
		if (authConfig == null || authConfig.Type == AuthenticationType.None)
			return;

		switch (authConfig.Type)
		{
			case AuthenticationType.Basic:
				if (authConfig.Basic != null)
				{
					var credentials = Convert.ToBase64String(
						System.Text.Encoding.UTF8.GetBytes(
							$"{authConfig.Basic.Username}:{authConfig.Basic.Password}"));
					client.DefaultRequestHeaders.Authorization =
						new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);
				}
				break;

			case AuthenticationType.Bearer:
				if (authConfig.Bearer != null)
				{
					client.DefaultRequestHeaders.Authorization =
						new System.Net.Http.Headers.AuthenticationHeaderValue(
							authConfig.Bearer.Prefix ?? "Bearer",
							authConfig.Bearer.Token);
				}
				break;

			case AuthenticationType.ApiKey:
				if (authConfig.ApiKey != null)
				{
					var location = authConfig.ApiKey.Location;
					if (location == ApiKeyLocation.Header)
					{
						client.DefaultRequestHeaders.TryAddWithoutValidation(
							authConfig.ApiKey.KeyName ?? "X-API-Key",
							authConfig.ApiKey.KeyValue);
					}
				}
				break;
		}
	}

	/// <summary>
	/// Dispose resources.
	/// </summary>
	public void Dispose()
	{
		Dispose(true);
		GC.SuppressFinalize(this);
	}

	protected virtual void Dispose(bool disposing)
	{
		if (_disposed)
			return;

		if (disposing)
		{
			_httpClient?.Dispose();
		}

		_disposed = true;
	}
}
