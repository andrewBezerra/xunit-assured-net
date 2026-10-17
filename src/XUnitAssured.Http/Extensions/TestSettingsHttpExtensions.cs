using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
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
		ArgumentNullException.ThrowIfNull(testSettings);

		lock (_lock)
		{
			// The files the section is read from: the base one and the environment's (#154)
			var ambiente = testSettings.AmbienteSelecionado;
			var cacheKey = TestSettingsSection.ChaveDeCache(ambiente);
			
			// Return cached if available
			if (_cache.TryGetValue(cacheKey, out var cached))
				return cached;

			// Load HTTP settings from file
			var httpSettings = LoadHttpSettingsFromFile(ambiente);

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

	// Finding the file, honouring TESTSETTINGS_PATH and expanding ${ENV:NAME} is Core's,
	// the same for every package; a file that cannot be read gives null, as before.
	private static HttpSettings? LoadHttpSettingsFromFile(string? ambiente) =>
		TestSettingsSection.Read<HttpSettings>("http", ambiente: ambiente, opcoes: new JsonSerializerOptions
		{
			PropertyNameCaseInsensitive = true,
			ReadCommentHandling = JsonCommentHandling.Skip,
			AllowTrailingCommas = true,
			Converters = { new JsonStringEnumConverter() }
		});
}
