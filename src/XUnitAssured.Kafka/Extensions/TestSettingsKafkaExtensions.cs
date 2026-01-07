using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using XUnitAssured.Core.Configuration;

namespace XUnitAssured.Kafka.Configuration;

/// <summary>
/// Extension methods to access Kafka settings from TestSettings.
/// Provides zero-coupling access to KafkaSettings without modifying core TestSettings class.
/// </summary>
/// <remarks>
/// This approach uses extension methods instead of partial classes to avoid cross-assembly conflicts.
/// The Kafka settings are loaded dynamically from testsettings.json when first accessed.
/// 
/// Cache Strategy:
/// - Cache is keyed by file path (TESTSETTINGS_PATH or default path), not by TestSettings instance
/// - This ensures that multiple TestSettings instances loaded from the same file share the same cached KafkaSettings
/// - Eliminates race conditions when tests run in parallel
/// </remarks>
/// <example>
/// <code>
/// // Load settings
/// var settings = TestSettings.Load();
/// 
/// // Access Kafka settings via extension method
/// var kafkaSettings = settings.GetKafkaSettings();
/// if (kafkaSettings != null)
/// {
///     Console.WriteLine($"Bootstrap Servers: {kafkaSettings.BootstrapServers}");
/// }
/// 
/// // Or use TryGetKafkaSettings for safe access
/// if (settings.TryGetKafkaSettings(out var kafka))
/// {
///     var config = kafka.ToProducerConfig();
/// }
/// </code>
/// </example>
public static class TestSettingsKafkaExtensions
{
	private static readonly Dictionary<string, KafkaSettings?> _cache = new();
	private static readonly object _lock = new();

	/// <summary>
	/// Gets KafkaSettings from TestSettings.
	/// Settings are loaded from testsettings.json "kafka" section and cached by file path.
	/// </summary>
	/// <param name="testSettings">The TestSettings instance</param>
	/// <returns>KafkaSettings if available, null otherwise</returns>
	/// <example>
	/// <code>
	/// var settings = TestSettings.Load();
	/// var kafka = settings.GetKafkaSettings();
	/// 
	/// if (kafka != null)
	/// {
	///     var producerConfig = kafka.ToProducerConfig();
	///     var producer = new ProducerBuilder&lt;string, string&gt;(producerConfig).Build();
	/// }
	/// </code>
	/// </example>
	public static KafkaSettings? GetKafkaSettings(this TestSettings testSettings)
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

			// Load Kafka settings from file
			var kafkaSettings = LoadKafkaSettingsFromFile();

			// Cache and return
			_cache[cacheKey] = kafkaSettings;
			return kafkaSettings;
		}
	}

	/// <summary>
	/// Tries to get KafkaSettings from TestSettings.
	/// Returns true if Kafka settings are available.
	/// </summary>
	/// <param name="testSettings">The TestSettings instance</param>
	/// <param name="kafkaSettings">Output parameter for KafkaSettings</param>
	/// <returns>True if Kafka settings found, false otherwise</returns>
	/// <example>
	/// <code>
	/// var settings = TestSettings.Load();
	/// 
	/// if (settings.TryGetKafkaSettings(out var kafka))
	/// {
	///     Console.WriteLine($"Kafka configured: {kafka.BootstrapServers}");
	/// }
	/// else
	/// {
	///     Console.WriteLine("Kafka not configured");
	/// }
	/// </code>
	/// </example>
	public static bool TryGetKafkaSettings(this TestSettings testSettings, out KafkaSettings? kafkaSettings)
	{
		kafkaSettings = testSettings.GetKafkaSettings();
		return kafkaSettings != null;
	}

	/// <summary>
	/// Clears the Kafka settings cache.
	/// Useful for testing or when settings need to be reloaded.
	/// </summary>
	public static void ClearKafkaSettingsCache()
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

	private static KafkaSettings? LoadKafkaSettingsFromFile()
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
			return ParseKafkaSettings(customPath);
		}

		// Try standard paths
		foreach (var path in searchPaths)
		{
			if (File.Exists(path))
			{
				return ParseKafkaSettings(path);
			}
		}

		// No settings found
		return null;
	}

	private static KafkaSettings? ParseKafkaSettings(string filePath)
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

			// Look for "kafka" section
			if (document.RootElement.TryGetProperty("kafka", out var kafkaElement))
			{
				// Deserialize KafkaSettings
				var kafkaSettings = JsonSerializer.Deserialize<KafkaSettings>(
					kafkaElement.GetRawText(),
					new JsonSerializerOptions
					{
						PropertyNameCaseInsensitive = true,
						ReadCommentHandling = JsonCommentHandling.Skip,
						AllowTrailingCommas = true,
						Converters = { new JsonStringEnumConverter() }
					});

				return kafkaSettings;
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
