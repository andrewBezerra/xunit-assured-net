using System;

using XUnitAssured.Base.Auth;
using XUnitAssured.Base.Kafka;

namespace XUnitAssured.Base;

/// <summary>
/// Base settings for integration, end-to-end, and regression tests.
/// Uses native .NET types (Uri) and integrates with the Options pattern.
/// Configuration section name: "TestSettings"
/// </summary>
public class BaseSettings
{
	/// <summary>
	/// Configuration section name in appsettings.json
	/// </summary>
	public const string SectionName = "TestSettings";

	/// <summary>
	/// Base URL of the API under test. Must be an absolute HTTP(S) URI.
	/// </summary>
	public Uri BaseUrl { get; set; } = null!;

	/// <summary>
	/// Optional URI to the OpenAPI/Swagger document.
	/// Can be an HTTP(S) URL or a file:// path.
	/// </summary>
	public Uri? OpenApiDocument { get; set; }

	/// <summary>
	/// Optional authentication settings for the API.
	/// </summary>
	public AuthenticationSettings? Authentication { get; set; }

	/// <summary>
	/// Optional Kafka security settings.
	/// [OBSOLETE] Will be moved to XUnitAssured.Kafka package in v3.0.
	/// </summary>
	[Obsolete("Kafka settings will be moved to a separate XUnitAssured.Kafka package in v3.0. Consider using the Confluent.Kafka configuration directly.")]
	public KafkaSecurity? Kafka { get; set; }

	/// <summary>
	/// Default constructor for Options pattern binding.
	/// </summary>
	public BaseSettings()
	{
		// Properties will be set by configuration binding
	}
}
