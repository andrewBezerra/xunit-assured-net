using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace XUnitAssured.Playwright.Configuration;

/// <summary>
/// Loads Playwright settings from playwrightsettings.json file.
/// Supports environment variables and caching.
/// </summary>
public static class PlaywrightSettingsLoader
{
	private static readonly string[] SearchPaths = new[]
	{
		"playwrightsettings.json",
		"./playwrightsettings.json",
		"../playwrightsettings.json",
		"../../playwrightsettings.json",
		"../../../playwrightsettings.json"
	};

	private static PlaywrightSettings? _cachedSettings;
	private static readonly object _lockObject = new();

	/// <summary>
	/// Loads Playwright settings from file.
	/// Uses caching to avoid repeated file reads.
	/// </summary>
	public static PlaywrightSettings Load(string? customPath = null)
	{
		lock (_lockObject)
		{
			if (_cachedSettings != null)
				return _cachedSettings.Clone();

			// Try custom path
			if (!string.IsNullOrEmpty(customPath) && File.Exists(customPath))
			{
				_cachedSettings = LoadFromFile(customPath);
				return _cachedSettings.Clone();
			}

			// Try environment variable
			var envPath = Environment.GetEnvironmentVariable("XUNITASSURED_PLAYWRIGHT_SETTINGS_PATH");
			if (!string.IsNullOrEmpty(envPath) && File.Exists(envPath))
			{
				_cachedSettings = LoadFromFile(envPath);
				return _cachedSettings.Clone();
			}

			// Try standard search paths
			foreach (var path in SearchPaths)
			{
				if (File.Exists(path))
				{
					_cachedSettings = LoadFromFile(path);
					return _cachedSettings.Clone();
				}
			}

			// Return default settings
			_cachedSettings = new PlaywrightSettings();
			return _cachedSettings.Clone();
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

	private static PlaywrightSettings LoadFromFile(string path)
	{
		try
		{
			var json = File.ReadAllText(path);
			var options = new JsonSerializerOptions
				{
					PropertyNameCaseInsensitive = true,
					ReadCommentHandling = JsonCommentHandling.Skip,
					AllowTrailingCommas = true,
					Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
				};

			return JsonSerializer.Deserialize<PlaywrightSettings>(json, options)
				?? new PlaywrightSettings();
		}
		catch (Exception)
		{
			return new PlaywrightSettings();
		}
	}
}
