using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
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
	/// If provided, testsettings.{environment}.json is laid over testsettings.json: it overrides
	/// only what it writes, in every section, so it needs only what differs.
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
			environment = TestSettingsSection.Ambiente(environment);

			// testsettings.json with testsettings.{environment}.json laid over it — the same
			// files every package section is read from (#154)
			var raiz = TestSettingsSection.Mesclar(environment);
			var settings = raiz is null ? new TestSettings() : Deserialize(raiz, environment);
			settings.AmbienteSelecionado = environment;

			if (environment == null)
				_cachedSettings = settings;
			return settings;
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

	private static TestSettings Deserialize(JsonObject raiz, string? environment)
	{
		// A file that cannot be read has already said so, naming it; what fails here is a value
		// that does not fit its setting.
		try
		{
			var options = new JsonSerializerOptions
			{
				PropertyNameCaseInsensitive = true,
				ReadCommentHandling = JsonCommentHandling.Skip,
				AllowTrailingCommas = true,
				Converters = { new JsonStringEnumConverter() }
			};

			var settings = raiz.Deserialize<TestSettings>(options) ?? new TestSettings();

			// Store environment name if specified
			if (!string.IsNullOrEmpty(environment))
			{
				settings.Environment = environment;
			}

			return settings;
		}
		catch (Exception ex)
		{
			throw new InvalidOperationException($"Failed to load test settings: {ex.Message}", ex);
		}
	}
}
