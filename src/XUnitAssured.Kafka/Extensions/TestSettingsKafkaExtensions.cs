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
			// The files the section is read from: the base one and the environment's (#154)
			var ambiente = testSettings.AmbienteSelecionado;
			var cacheKey = TestSettingsSection.ChaveDeCache(ambiente);
			
			// Return cached if available
			if (_cache.TryGetValue(cacheKey, out var cached))
				return cached;

			// Load Kafka settings from file
			var kafkaSettings = LoadKafkaSettingsFromFile(ambiente);

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

	private static KafkaSettings? LoadKafkaSettingsFromFile(string? ambiente)
	{
		// Finding the file, honouring TESTSETTINGS_PATH and expanding ${ENV:NAME} is the same
		// work for every package, and lives in Core.
		var settings = TestSettingsSection.Read<KafkaSettings>("kafka", ambiente: ambiente, opcoes: new JsonSerializerOptions
		{
			PropertyNameCaseInsensitive = true,
			ReadCommentHandling = JsonCommentHandling.Skip,
			AllowTrailingCommas = true,
			Converters = { new JsonStringEnumConverter() }
		});

		if (settings != null)
		{
			NormalizeAuthenticationSettings(settings);
			ResolverCaminhos(settings, ambiente);
		}

		return settings;
	}

	/// <summary>
	/// A relative certificate path is read from the folder of the settings file that wrote it,
	/// not from wherever the test runner started (#175). When no file exists there, the path is
	/// kept as written, which librdkafka resolves from the working directory, as before.
	/// </summary>
	/// <remarks>
	/// The environment's file is tried first, then the base one: a merged value does not say
	/// which file wrote it, and the environment's overrides the base.
	/// </remarks>
	private static void ResolverCaminhos(KafkaSettings settings, string? ambiente)
	{
		var pastas = new List<string>();
		ambiente = TestSettingsSection.Ambiente(ambiente);
		if (ambiente != null && TestSettingsSection.LocalizarDoAmbiente(ambiente) is { } doAmbiente)
			pastas.Add(Path.GetDirectoryName(Path.GetFullPath(doAmbiente))!);
		if (TestSettingsSection.Localizar() is { } doBase)
			pastas.Add(Path.GetDirectoryName(Path.GetFullPath(doBase))!);

		string? Resolver(string? caminho)
		{
			if (string.IsNullOrWhiteSpace(caminho) || Path.IsPathRooted(caminho))
				return caminho;

			foreach (var pasta in pastas)
			{
				var candidato = Path.GetFullPath(Path.Combine(pasta, caminho));
				if (File.Exists(candidato))
					return candidato;
			}

			return caminho;
		}

		settings.SslCaLocation = Resolver(settings.SslCaLocation);

		if (settings.SchemaRegistry != null)
			settings.SchemaRegistry.SslCaLocation = Resolver(settings.SchemaRegistry.SslCaLocation);

		if (settings.Authentication?.Ssl is { } ssl)
		{
			ssl.SslCaLocation = Resolver(ssl.SslCaLocation);
			ssl.SslCertificateLocation = Resolver(ssl.SslCertificateLocation);
			ssl.SslKeyLocation = Resolver(ssl.SslKeyLocation);
		}
	}

	/// <summary>
	/// Normalizes authentication settings after deserialization.
	/// Ensures SASL mechanism is correctly set based on authentication type.
	/// </summary>
	private static void NormalizeAuthenticationSettings(KafkaSettings kafkaSettings)
	{
		var authConfig = kafkaSettings.Authentication;
		if (authConfig == null) return;

		// Normalize SASL/SCRAM mechanism based on authentication type
		if (authConfig.SaslScram != null)
		{
			switch (authConfig.Type)
			{
				case Configuration.KafkaAuthenticationType.SaslScram256:
					authConfig.SaslScram.Mechanism = Confluent.Kafka.SaslMechanism.ScramSha256;
					break;
				case Configuration.KafkaAuthenticationType.SaslScram512:
					authConfig.SaslScram.Mechanism = Confluent.Kafka.SaslMechanism.ScramSha512;
					break;
			}
		}
	}
}
