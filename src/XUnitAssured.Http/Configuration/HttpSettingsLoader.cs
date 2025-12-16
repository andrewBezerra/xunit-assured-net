using System;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace XUnitAssured.Http.Configuration;

/// <summary>
/// Loads HTTP settings from httpsettings.json file.
/// Supports environment variables and environment-specific overrides.
/// </summary>
public static class HttpSettingsLoader
{
	private static readonly string[] SearchPaths = new[]
	{
		"httpsettings.json",
		"./httpsettings.json",
		"../httpsettings.json",
		"../../httpsettings.json",
		"../../../httpsettings.json"
	};

	private static HttpSettings? _cachedSettings;
	private static readonly object _lockObject = new();

	/// <summary>
	/// Loads HTTP settings from file.
	/// Uses caching to avoid repeated file reads.
	/// </summary>
	public static HttpSettings Load(string? customPath = null, string? environment = null)
	{
		lock (_lockObject)
		{
			// Return cached settings if available
			if (_cachedSettings != null)
				return _cachedSettings;

			// Try to load from custom path
			if (!string.IsNullOrEmpty(customPath) && File.Exists(customPath))
			{
				_cachedSettings = LoadFromFile(customPath, environment);
				return _cachedSettings;
			}

			// Try environment variable
			var envPath = Environment.GetEnvironmentVariable("XUNITASSURED_HTTP_SETTINGS_PATH");
			if (!string.IsNullOrEmpty(envPath) && File.Exists(envPath))
			{
				_cachedSettings = LoadFromFile(envPath, environment);
				return _cachedSettings;
			}

			// Try standard search paths
			foreach (var path in SearchPaths)
			{
				if (File.Exists(path))
				{
					_cachedSettings = LoadFromFile(path, environment);
					return _cachedSettings;
				}
			}

			// Return default settings
			_cachedSettings = new HttpSettings();
			return _cachedSettings;
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

	private static HttpSettings LoadFromFile(string path, string? environment)
	{
		try
		{
			// Load base settings
			var json = File.ReadAllText(path);
			json = ReplaceEnvironmentVariables(json);

			var options = new JsonSerializerOptions
			{
				PropertyNameCaseInsensitive = true,
				ReadCommentHandling = JsonCommentHandling.Skip,
				AllowTrailingCommas = true
			};

			var root = JsonSerializer.Deserialize<HttpSettingsRoot>(json, options);
			var settings = root?.Http ?? new HttpSettings();

			// Load environment-specific override
			if (!string.IsNullOrEmpty(environment))
			{
				// Try environment-specific file (e.g., httpsettings.Development.json)
				var envFilePath = path.Replace(".json", $".{environment}.json");
				if (File.Exists(envFilePath))
				{
					var envJson = File.ReadAllText(envFilePath);
					envJson = ReplaceEnvironmentVariables(envJson);
					var envRoot = JsonSerializer.Deserialize<HttpSettingsRoot>(envJson, options);
					var envSettings = envRoot?.Http ?? new HttpSettings();
					settings = settings.Merge(envSettings);
				}
				// Try environments section in main file
				else if (root?.Environments != null && root.Environments.TryGetValue(environment, out var envSettings))
				{
					settings = settings.Merge(envSettings);
				}
			}

			return settings;
		}
		catch (Exception ex)
		{
			throw new InvalidOperationException($"Failed to load HTTP settings from '{path}': {ex.Message}", ex);
		}
	}

	private static string ReplaceEnvironmentVariables(string json)
	{
		// Replace ${ENV:VAR_NAME} with actual environment variable value
		var regex = new Regex(@"\$\{ENV:([^}]+)\}", RegexOptions.IgnoreCase);
		return regex.Replace(json, match =>
		{
			var varName = match.Groups[1].Value;
			var value = Environment.GetEnvironmentVariable(varName);
			
			// Keep placeholder if env var not found (for error visibility)
			return value ?? match.Value;
		});
	}
}
