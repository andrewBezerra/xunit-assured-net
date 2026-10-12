using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace XUnitAssured.Core.Configuration;

/// <summary>
/// Loads unified test settings from testsettings.json.
/// This loader is agnostic to HTTP/Kafka - it simply deserializes JSON into TestSettings.
/// Partial class extensions in HTTP/Kafka packages automatically populate their properties.
/// </summary>
public static class TestSettingsLoader
{
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
			var path = ArquivoDeConfiguracao.Localizar(fileName);
			if (path != null)
			{
				var settings = LoadFromFile(path, environment);
				if (environment == null)
					_cachedSettings = settings;
				return settings;
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
			json = ArquivoDeConfiguracao.ExpandirVariaveis(json);

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
}
