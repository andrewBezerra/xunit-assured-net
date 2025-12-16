using System.Collections.Generic;

namespace XUnitAssured.Http.Configuration;

/// <summary>
/// HTTP configuration settings that can be loaded from httpsettings.json.
/// </summary>
public class HttpSettings
{
	/// <summary>
	/// Base URL for HTTP requests.
	/// Example: "https://api.example.com"
	/// </summary>
	public string? BaseUrl { get; set; }

	/// <summary>
	/// Default timeout in seconds for HTTP requests.
	/// Default: 30 seconds
	/// </summary>
	public int Timeout { get; set; } = 30;

	/// <summary>
	/// Default headers to include in all HTTP requests.
	/// </summary>
	public Dictionary<string, string>? DefaultHeaders { get; set; }

	/// <summary>
	/// Authentication configuration.
	/// </summary>
	public HttpAuthConfig? Authentication { get; set; }

	/// <summary>
	/// Creates a new HttpSettings instance with default values.
	/// </summary>
	public HttpSettings()
	{
		Authentication = new HttpAuthConfig();
		DefaultHeaders = new Dictionary<string, string>();
	}

	/// <summary>
	/// Loads settings from httpsettings.json file.
	/// </summary>
	/// <param name="customPath">Custom path to settings file (optional)</param>
	/// <param name="environment">Environment name for environment-specific settings (optional)</param>
	/// <returns>Loaded HTTP settings</returns>
	public static HttpSettings Load(string? customPath = null, string? environment = null)
	{
		return HttpSettingsLoader.Load(customPath, environment);
	}

	/// <summary>
	/// Merges this settings with another settings instance.
	/// Values from 'other' take precedence.
	/// </summary>
	public HttpSettings Merge(HttpSettings other)
	{
		if (other == null)
			return this;

		return new HttpSettings
		{
			BaseUrl = other.BaseUrl ?? BaseUrl,
			Timeout = other.Timeout != 0 ? other.Timeout : Timeout,
			DefaultHeaders = MergeHeaders(DefaultHeaders, other.DefaultHeaders),
			Authentication = other.Authentication ?? Authentication
		};
	}

	private Dictionary<string, string>? MergeHeaders(Dictionary<string, string>? baseHeaders, Dictionary<string, string>? overrideHeaders)
	{
		if (baseHeaders == null && overrideHeaders == null)
			return null;

		var result = new Dictionary<string, string>();

		if (baseHeaders != null)
		{
			foreach (var header in baseHeaders)
			{
				result[header.Key] = header.Value;
			}
		}

		if (overrideHeaders != null)
		{
			foreach (var header in overrideHeaders)
			{
				result[header.Key] = header.Value;
			}
		}

		return result;
	}
}

/// <summary>
/// Root object for httpsettings.json deserialization.
/// </summary>
public class HttpSettingsRoot
{
	/// <summary>
	/// HTTP configuration section.
	/// </summary>
	public HttpSettings? Http { get; set; }

	/// <summary>
	/// Environment-specific configurations.
	/// </summary>
	public Dictionary<string, HttpSettings>? Environments { get; set; }
}
