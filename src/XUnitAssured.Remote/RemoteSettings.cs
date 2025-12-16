using System;
using System.Collections.Generic;
using System.Net.Http.Headers;

namespace XUnitAssured.Remote;

/// <summary>
/// Settings for remote application testing (Web APIs and Kafka).
/// Configuration section name: "Remote"
/// </summary>
public class RemoteSettings
{
	/// <summary>
	/// Configuration section name in appsettings.json
	/// </summary>
	public const string SectionName = "Remote";

	/// <summary>
	/// Base URL for the remote application.
	/// Example: "https://api.example.com" or "http://localhost:5000"
	/// </summary>
	public string BaseUrl { get; set; } = string.Empty;

	/// <summary>
	/// Request timeout in seconds.
	/// Default is 30 seconds.
	/// </summary>
	public int TimeoutSeconds { get; set; } = 30;

	/// <summary>
	/// Maximum number of retry attempts for failed requests.
	/// Default is 3.
	/// </summary>
	public int MaxRetryAttempts { get; set; } = 3;

	/// <summary>
	/// Delay between retry attempts in milliseconds.
	/// Default is 1000ms (1 second).
	/// </summary>
	public int RetryDelayMilliseconds { get; set; } = 1000;

	/// <summary>
	/// Authentication settings for remote access.
	/// </summary>
	public AuthenticationSettings? Authentication { get; set; }

	/// <summary>
	/// Kafka settings for remote Kafka testing.
	/// Optional, only needed when testing Kafka integration.
	/// </summary>
	public RemoteKafkaSettings? Kafka { get; set; }

	/// <summary>
	/// Custom HTTP headers to include in all requests.
	/// Example: { "X-Api-Version": "v1", "X-Tenant-Id": "123" }
	/// </summary>
	public Dictionary<string, string>? DefaultHeaders { get; set; }

	/// <summary>
	/// Enable detailed logging for requests and responses.
	/// Default is false.
	/// </summary>
	public bool EnableDetailedLogging { get; set; } = false;

	/// <summary>
	/// Allow invalid SSL certificates (for development/test environments).
	/// Default is false for security.
	/// </summary>
	public bool AllowInvalidSslCertificates { get; set; } = false;

	/// <summary>
	/// Default constructor for Options pattern binding.
	/// </summary>
	public RemoteSettings()
	{
	}

	/// <summary>
	/// Gets the base URL as a Uri object.
	/// </summary>
	/// <returns>Uri representation of BaseUrl</returns>
	/// <exception cref="InvalidOperationException">Thrown when BaseUrl is empty or invalid</exception>
	public Uri GetBaseUri()
	{
		if (string.IsNullOrWhiteSpace(BaseUrl))
		{
			throw new InvalidOperationException("BaseUrl is not configured in RemoteSettings.");
		}

		if (!Uri.TryCreate(BaseUrl, UriKind.Absolute, out var uri))
		{
			throw new InvalidOperationException($"BaseUrl '{BaseUrl}' is not a valid absolute URI.");
		}

		return uri;
	}

	/// <summary>
	/// Gets the configured timeout as a TimeSpan.
	/// </summary>
	/// <returns>TimeSpan representation of TimeoutSeconds</returns>
	public TimeSpan GetTimeout()
	{
		return TimeSpan.FromSeconds(TimeoutSeconds);
	}

	/// <summary>
	/// Gets the retry delay as a TimeSpan.
	/// </summary>
	/// <returns>TimeSpan representation of RetryDelayMilliseconds</returns>
	public TimeSpan GetRetryDelay()
	{
		return TimeSpan.FromMilliseconds(RetryDelayMilliseconds);
	}
}

/// <summary>
/// Authentication settings for remote API access.
/// </summary>
public class AuthenticationSettings
{
	/// <summary>
	/// Authentication type (Bearer, Basic, ApiKey, Custom).
	/// </summary>
	public AuthenticationType Type { get; set; } = AuthenticationType.None;

	/// <summary>
	/// Bearer token for JWT/OAuth authentication.
	/// Used when Type is Bearer.
	/// </summary>
	public string? BearerToken { get; set; }

	/// <summary>
	/// Username for Basic authentication.
	/// Used when Type is Basic.
	/// </summary>
	public string? Username { get; set; }

	/// <summary>
	/// Password for Basic authentication.
	/// Used when Type is Basic.
	/// </summary>
	public string? Password { get; set; }

	/// <summary>
	/// API Key value.
	/// Used when Type is ApiKey.
	/// </summary>
	public string? ApiKey { get; set; }

	/// <summary>
	/// API Key header name (e.g., "X-API-Key").
	/// Used when Type is ApiKey.
	/// </summary>
	public string? ApiKeyHeaderName { get; set; } = "X-API-Key";

	/// <summary>
	/// Custom authentication header name.
	/// Used when Type is Custom.
	/// </summary>
	public string? CustomHeaderName { get; set; }

	/// <summary>
	/// Custom authentication header value.
	/// Used when Type is Custom.
	/// </summary>
	public string? CustomHeaderValue { get; set; }

	/// <summary>
	/// Gets the authentication header based on configured settings.
	/// </summary>
	/// <returns>AuthenticationHeaderValue or null if no authentication</returns>
	public AuthenticationHeaderValue? GetAuthenticationHeader()
	{
		return Type switch
		{
			AuthenticationType.Bearer when !string.IsNullOrWhiteSpace(BearerToken) 
				=> new AuthenticationHeaderValue("Bearer", BearerToken),
			AuthenticationType.Basic when !string.IsNullOrWhiteSpace(Username) && !string.IsNullOrWhiteSpace(Password)
				=> new AuthenticationHeaderValue("Basic", Convert.ToBase64String(
					System.Text.Encoding.UTF8.GetBytes($"{Username}:{Password}"))),
			_ => null
		};
	}
}

/// <summary>
/// Remote Kafka settings for testing Kafka in remote environments.
/// </summary>
public class RemoteKafkaSettings
{
	/// <summary>
	/// Comma-separated list of Kafka broker addresses (host:port).
	/// Example: "kafka.example.com:9092"
	/// </summary>
	public string BootstrapServers { get; set; } = string.Empty;

	/// <summary>
	/// Consumer group ID for Kafka consumers.
	/// </summary>
	public string? GroupId { get; set; }

	/// <summary>
	/// SASL username for Kafka authentication.
	/// </summary>
	public string? SaslUsername { get; set; }

	/// <summary>
	/// SASL password for Kafka authentication.
	/// </summary>
	public string? SaslPassword { get; set; }

	/// <summary>
	/// Security protocol (Plaintext, Ssl, SaslPlaintext, SaslSsl).
	/// </summary>
	public string SecurityProtocol { get; set; } = "Plaintext";

	/// <summary>
	/// SASL mechanism (Plain, ScramSha256, ScramSha512).
	/// </summary>
	public string? SaslMechanism { get; set; }

	/// <summary>
	/// Enable SSL certificate verification.
	/// Default is true.
	/// </summary>
	public bool EnableSslCertificateVerification { get; set; } = true;
}

/// <summary>
/// Authentication type enumeration.
/// </summary>
public enum AuthenticationType
{
	/// <summary>
	/// No authentication.
	/// </summary>
	None = 0,

	/// <summary>
	/// Bearer token authentication (JWT/OAuth).
	/// </summary>
	Bearer = 1,

	/// <summary>
	/// Basic authentication (username/password).
	/// </summary>
	Basic = 2,

	/// <summary>
	/// API Key authentication.
	/// </summary>
	ApiKey = 3,

	/// <summary>
	/// Custom authentication header.
	/// </summary>
	Custom = 4
}
