using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using XUnitAssured.Core.Configuration;

namespace XUnitAssured.Http.Configuration;

/// <summary>
/// Extension methods to access HTTP settings from TestSettings.
/// Provides zero-coupling access to HttpSettings without modifying core TestSettings class.
/// </summary>
/// <remarks>
/// This approach uses extension methods instead of partial classes to avoid cross-assembly conflicts.
/// The HTTP settings are loaded dynamically from testsettings.json when first accessed.
/// 
/// Cache Strategy:
/// - Cache is keyed by file path (TESTSETTINGS_PATH or default path), not by TestSettings instance
/// - This ensures that multiple TestSettings instances loaded from the same file share the same cached HttpSettings
/// - Eliminates race conditions when tests run in parallel
/// </remarks>
/// <example>
/// <code>
/// // Load settings
/// var settings = TestSettings.Load();
/// 
/// // Access HTTP settings via extension method
/// var httpSettings = settings.GetHttpSettings();
/// if (httpSettings != null)
/// {
///     Console.WriteLine($"Base URL: {httpSettings.BaseUrl}");
/// }
/// 
/// // Or use TryGetHttpSettings for safe access
/// if (settings.TryGetHttpSettings(out var http))
/// {
///     Console.WriteLine($"Timeout: {http.Timeout}");
/// }
/// </code>
/// </example>
public static class TestSettingsHttpExtensions
{
	private static readonly Dictionary<string, HttpSettings?> _cache = new();
	private static readonly object _lock = new();

	/// <summary>
	/// Gets HttpSettings from TestSettings.
	/// Settings are loaded from testsettings.json "http" section and cached by file path.
	/// </summary>
	/// <param name="testSettings">The TestSettings instance</param>
	/// <returns>HttpSettings if available, null otherwise</returns>
	/// <example>
	/// <code>
	/// var settings = TestSettings.Load();
	/// var http = settings.GetHttpSettings();
	/// 
	/// if (http != null)
	/// {
	///     var client = new HttpClient { BaseAddress = new Uri(http.BaseUrl) };
	/// }
	/// </code>
	/// </example>
	public static HttpSettings? GetHttpSettings(this TestSettings testSettings)
	{
		if (testSettings == null)
			throw new ArgumentNullException(nameof(testSettings));

		lock (_lock)
		{
			// Get cache key (file path)
			var cacheKey = GetCacheKey();
			
			// Return cached if available
			if (_cache.TryGetValue(cacheKey, out var cached))
				return cached;

			// Load HTTP settings from file
			var httpSettings = LoadHttpSettingsFromFile();

			// Cache and return
			_cache[cacheKey] = httpSettings;
			return httpSettings;
		}
	}

	/// <summary>
	/// Tries to get HttpSettings from TestSettings.
	/// Returns true if HTTP settings are available.
	/// </summary>
	/// <param name="testSettings">The TestSettings instance</param>
	/// <param name="httpSettings">Output parameter for HttpSettings</param>
	/// <returns>True if HTTP settings found, false otherwise</returns>
	/// <example>
	/// <code>
	/// var settings = TestSettings.Load();
	/// 
	/// if (settings.TryGetHttpSettings(out var http))
	/// {
	///     Console.WriteLine($"HTTP configured: {http.BaseUrl}");
	/// }
	/// else
	/// {
	///     Console.WriteLine("HTTP not configured");
	/// }
	/// </code>
	/// </example>
	public static bool TryGetHttpSettings(this TestSettings testSettings, out HttpSettings? httpSettings)
	{
		httpSettings = testSettings.GetHttpSettings();
		return httpSettings != null;
	}

	/// <summary>
	/// Clears the HTTP settings cache.
	/// Useful for testing or when settings need to be reloaded.
	/// </summary>
	public static void ClearHttpSettingsCache()
	{
		lock (_lock)
		{
			_cache.Clear();
		}
	}

	/// <summary>
	/// Gets the cache key for the current test settings file path.
	/// </summary>
	/// <returns>Cache key (file path or "default")</returns>
	private static string GetCacheKey()
	{
		// Try custom path from environment variable
		var customPath = System.Environment.GetEnvironmentVariable("TESTSETTINGS_PATH");
		if (!string.IsNullOrEmpty(customPath))
			return customPath;

		// Try to find default testsettings.json
		var searchPaths = new[]
		{
			"testsettings.json",
			"./testsettings.json",
			"../testsettings.json",
			"../../testsettings.json",
			"../../../testsettings.json"
		};

		foreach (var path in searchPaths)
		{
			if (File.Exists(path))
				return Path.GetFullPath(path);
		}

		// No file found, use "default" as cache key
		return "default";
	}

	private static HttpSettings? LoadHttpSettingsFromFile()
	{
		// Try to find testsettings.json
		var searchPaths = new[]
		{
			"testsettings.json",
			"./testsettings.json",
			"../testsettings.json",
			"../../testsettings.json",
			"../../../testsettings.json"
		};

		// Try custom path from environment variable
		var customPath = System.Environment.GetEnvironmentVariable("TESTSETTINGS_PATH");
		if (!string.IsNullOrEmpty(customPath) && File.Exists(customPath))
		{
			return ParseHttpSettings(customPath);
		}

		// Try standard paths
		foreach (var path in searchPaths)
		{
			if (File.Exists(path))
			{
				return ParseHttpSettings(path);
			}
		}

		// No settings found
		return null;
	}

	private static HttpSettings? ParseHttpSettings(string filePath)
	{
		try
		{
			// Read and process JSON
			var json = File.ReadAllText(filePath);
			json = ReplaceEnvironmentVariables(json);

			// Parse JSON
			using var document = JsonDocument.Parse(json, new JsonDocumentOptions
			{
				CommentHandling = JsonCommentHandling.Skip,
				AllowTrailingCommas = true
			});

			// Look for "http" section
			if (document.RootElement.TryGetProperty("http", out var httpElement))
			{
				// Deserialize HttpSettings
				var httpSettings = JsonSerializer.Deserialize<HttpSettings>(
					httpElement.GetRawText(),
					new JsonSerializerOptions
					{
						PropertyNameCaseInsensitive = true,
						ReadCommentHandling = JsonCommentHandling.Skip,
						AllowTrailingCommas = true,
						Converters = { new JsonStringEnumConverter() }
					});

				return httpSettings;
			}

			return null;
		}
		catch
		{
			// If parsing fails, return null (settings not available)
			return null;
		}
	}

	private static string ReplaceEnvironmentVariables(string json)
	{
		// Replace ${ENV:VAR_NAME} with actual environment variable value
		var regex = new Regex(@"\$\{ENV:([^}]+)\}", RegexOptions.IgnoreCase);
		return regex.Replace(json, match =>
		{
			var varName = match.Groups[1].Value;
			var value = System.Environment.GetEnvironmentVariable(varName);
			return value ?? match.Value;
		});
	}
}
