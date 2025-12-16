using System;
using System.Collections.Generic;

using Confluent.SchemaRegistry;

namespace XUnitAssured.Kafka;

/// <summary>
/// Schema Registry settings for Avro/Protobuf serialization.
/// Uses Confluent.SchemaRegistry native types.
/// </summary>
public class SchemaRegistrySettings
{
	/// <summary>
	/// Schema Registry URL.
	/// Example: "http://localhost:8081" or "https://schema-registry.example.com"
	/// </summary>
	public string Url { get; set; } = "http://localhost:8081";

	/// <summary>
	/// Maximum number of schemas to cache locally.
	/// Default is 1000.
	/// </summary>
	public int MaxCachedSchemas { get; set; } = 1000;

	/// <summary>
	/// Request timeout in milliseconds.
	/// Default is 30000 (30 seconds).
	/// </summary>
	public int RequestTimeoutMs { get; set; } = 30000;

	/// <summary>
	/// Authentication credentials source.
	/// Use 0 for None, 1 for UserInfo, 2 for SaslInherit.
	/// Default is 0 (None - no authentication).
	/// </summary>
	public AuthCredentialsSource BasicAuthCredentialsSource { get; set; } = (AuthCredentialsSource)0;

	/// <summary>
	/// Basic authentication credentials in format "username:password".
	/// Required when BasicAuthCredentialsSource is UserInfo.
	/// </summary>
	public string? BasicAuthUserInfo { get; set; }

	/// <summary>
	/// Enable SSL certificate verification.
	/// Default is true for security.
	/// </summary>
	public bool EnableSslCertificateVerification { get; set; } = true;

	/// <summary>
	/// Path to SSL CA certificate file.
	/// Optional, used for custom certificate chains.
	/// </summary>
	public string? SslCaLocation { get; set; }

	/// <summary>
	/// Additional Schema Registry configuration properties.
	/// Use this for any configuration not directly mapped as properties.
	/// </summary>
	public Dictionary<string, string>? AdditionalProperties { get; set; }

	/// <summary>
	/// Default constructor for Options pattern binding.
	/// </summary>
	public SchemaRegistrySettings()
	{
	}

	/// <summary>
	/// Converts SchemaRegistrySettings to Confluent.SchemaRegistry SchemaRegistryConfig.
	/// </summary>
	/// <returns>SchemaRegistryConfig ready to use with Schema Registry client</returns>
	public SchemaRegistryConfig ToSchemaRegistryConfig()
	{
		var config = new SchemaRegistryConfig
		{
			Url = Url,
			MaxCachedSchemas = MaxCachedSchemas,
			RequestTimeoutMs = RequestTimeoutMs,
			BasicAuthCredentialsSource = BasicAuthCredentialsSource,
			BasicAuthUserInfo = BasicAuthUserInfo,
			EnableSslCertificateVerification = EnableSslCertificateVerification,
			SslCaLocation = SslCaLocation
		};

		// Apply additional properties if any
		if (AdditionalProperties != null)
		{
			foreach (var kvp in AdditionalProperties)
			{
				config.Set(kvp.Key, kvp.Value);
			}
		}

		return config;
	}
}
