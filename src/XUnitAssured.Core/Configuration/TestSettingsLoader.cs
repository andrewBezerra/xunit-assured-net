using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace XUnitAssured.Core.Configuration;

/// <summary>
/// Loads unified test settings from testsettings.json.
/// This loader is agnostic to HTTP/Kafka - it simply deserializes JSON into TestSettings.
/// Partial class extensions in HTTP/Kafka packages automatically populate their properties.
/// </summary>
public static class TestSettingsLoader
{
	private static readonly string[] SearchPaths = new[]
	{
		"testsettings.json",
		"./testsettings.json",
		"../testsettings.json",
		"../../testsettings.json",
		"../../../testsettings.json"
	};

	private static TestSettings? _cachedSettings;
	private static readonly object _lockObject = new();

	/// <summary>
	/// Loads test settings from file with caching.
	/// Automatically deserializes HTTP and Kafka settings if those packages are referenced.
	/// </summary>
	/// <param name="environment">
	/// Optional environment name (e.g., "staging", "prod").
	/// If provided, loads testsettings.{environment}.json instead of testsettings.json.
	/// If null, uses TEST_ENV environment variable.
	/// </param>
	/// <returns>Loaded test settings</returns>
	/// <example>
	/// <code>
	/// // Load default settings
	/// var settings = TestSettingsLoader.Load();
	/// 
	/// // Load staging settings
	/// var staging = TestSettingsLoader.Load("staging");
	/// 
	/// // Via environment variable:
	/// // TEST_ENV=prod dotnet test
	/// var prod = TestSettingsLoader.Load();  // Loads testsettings.prod.json
	/// </code>
	/// </example>
	public static TestSettings Load(string? environment = null)
	{
		lock (_lockObject)
		{
			// Return cached if available (only cache default, not environment-specific)
			if (_cachedSettings != null && environment == null)
				return _cachedSettings;

			// Get environment name from parameter or environment variable
			environment ??= System.Environment.GetEnvironmentVariable("TEST_ENV");

			// Try custom path from environment variable
			var customPath = System.Environment.GetEnvironmentVariable("TESTSETTINGS_PATH");
			if (!string.IsNullOrEmpty(customPath) && File.Exists(customPath))
			{
				var settings = LoadFromFile(customPath, environment);
				if (environment == null)
					_cachedSettings = settings;
				return settings;
			}

			// Build file name (with environment suffix if specified)
			var fileName = string.IsNullOrEmpty(environment)
				? "testsettings.json"
				: $"testsettings.{environment}.json";

			// Search in standard paths
			foreach (var basePath in SearchPaths)
			{
				var path = basePath.Replace("testsettings.json", fileName);
				if (File.Exists(path))
				{
					var settings = LoadFromFile(path, environment);
					if (environment == null)
						_cachedSettings = settings;
					return settings;
				}
			}

			// Return default settings if no file found
			var defaultSettings = new TestSettings();
			if (environment == null)
				_cachedSettings = defaultSettings;
			return defaultSettings;
		}
	}

	/// <summary>
	/// Clears cached settings (useful for testing).
	/// </summary>
	public static void ClearCache()
	{
		lock (_lockObject)
		{
			_cachedSettings = null;
		}
	}

	private static TestSettings LoadFromFile(string path, string? environment)
	{
		try
		{
			// Load and process JSON
			var json = File.ReadAllText(path);
			json = ReplaceEnvironmentVariables(json);

			// Configure JSON options
			var options = new JsonSerializerOptions
			{
				PropertyNameCaseInsensitive = true,
				ReadCommentHandling = JsonCommentHandling.Skip,
				AllowTrailingCommas = true,
				Converters = { new JsonStringEnumConverter() }
			};

			// Deserialize (partial classes will automatically populate Http/Kafka properties)
			var settings = JsonSerializer.Deserialize<TestSettings>(json, options)
				?? new TestSettings();

			// Store environment name if specified
			if (!string.IsNullOrEmpty(environment))
			{
				settings.Environment = environment;
			}

			return settings;
		}
		catch (Exception ex)
		{
			throw new InvalidOperationException(
				$"Failed to load test settings from '{path}': {ex.Message}", ex);
		}
	}

	/// <summary>
	/// Replaces ${ENV:VAR_NAME} patterns with actual environment variable values.
	/// </summary>
	/// <param name="json">JSON content with potential variable references</param>
	/// <returns>JSON with variables replaced</returns>
	/// <example>
	/// Input:  "token": "${ENV:API_TOKEN}"
	/// Output: "token": "actual-token-value"
	/// </example>
	private static string ReplaceEnvironmentVariables(string json)
	{
		// Replace ${ENV:VAR_NAME} with actual environment variable value
		var regex = new Regex(@"\$\{ENV:([^}]+)\}", RegexOptions.IgnoreCase);
		return regex.Replace(json, match =>
		{
			var varName = match.Groups[1].Value;
			var value = System.Environment.GetEnvironmentVariable(varName);

			// Keep placeholder if env var not found (for error visibility)
			return value ?? match.Value;
		});
	}
}
