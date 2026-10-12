using System;
using System.IO;
using System.Text.Json;
using System.Linq;

using XUnitAssured.Core.Configuration;

namespace XUnitAssured.Http.Configuration;

/// <summary>
/// Loads HTTP settings from httpsettings.json file.
/// Supports environment variables and environment-specific overrides.
/// </summary>
public static class HttpSettingsLoader
{
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
			var path = ArquivoDeConfiguracao.Localizar("httpsettings.json");
			if (path != null)
			{
				_cachedSettings = LoadFromFile(path, environment);
				return _cachedSettings;
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

	internal static HttpSettings LoadFromFile(string path, string? environment)
	{
		try
		{
			// Load base settings
			var json = File.ReadAllText(path);
			json = ArquivoDeConfiguracao.ExpandirVariaveis(json);

			var options = new JsonSerializerOptions
			{
				PropertyNameCaseInsensitive = true,
				ReadCommentHandling = JsonCommentHandling.Skip,
				AllowTrailingCommas = true,
				Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
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
					envJson = ArquivoDeConfiguracao.ExpandirVariaveis(envJson);
					var envRoot = JsonSerializer.Deserialize<HttpSettingsRoot>(envJson, options);
					var envSettings = envRoot?.Http ?? new HttpSettings();
					settings = settings.Merge(SoOQueFoiEscrito(envSettings, Secao(envJson, "http", nomeDoAmbiente: null)));
				}
				// Try environments section in main file
				else if (root?.Environments != null && root.Environments.TryGetValue(environment, out var envSettings))
				{
					settings = settings.Merge(SoOQueFoiEscrito(envSettings, Secao(json, "environments", environment)));
				}
			}

			return settings;
		}
		catch (Exception ex)
		{
			throw new InvalidOperationException($"Failed to load HTTP settings from '{path}': {ex.Message}", ex);
		}
	}

	/// <summary>
	/// The override with only what its JSON wrote: what it leaves out goes back to "not set", so
	/// <see cref="HttpSettings.Merge"/> keeps the base value.
	/// </summary>
	/// <remarks>
	/// The constructor fills <c>Authentication</c>, <c>DefaultHeaders</c> and <c>Timeout</c>, and
	/// the deserializer calls it: an environment that left out <c>authentication</c> erased the
	/// base one, and one that left out <c>timeout</c> reset it to 30 (#146). The default headers
	/// need nothing here: <c>Merge</c> adds the override's to the base's, and an empty set adds none.
	/// </remarks>
	private static HttpSettings SoOQueFoiEscrito(HttpSettings sobreposicao, JsonElement? escrito)
	{
		bool Escreveu(string nome) =>
			escrito is { ValueKind: JsonValueKind.Object } objeto &&
			objeto.EnumerateObject().Any(p => string.Equals(p.Name, nome, StringComparison.OrdinalIgnoreCase));

		if (!Escreveu("timeout"))
			sobreposicao.Timeout = 0;
		if (!Escreveu("authentication"))
			sobreposicao.Authentication = null;

		return sobreposicao;
	}

	/// <summary>
	/// The section, or one environment inside it: the section name case-insensitive, as the
	/// deserializer reads it; the environment name exact, as the dictionary looks it up.
	/// </summary>
	private static JsonElement? Secao(string json, string secao, string? nomeDoAmbiente)
	{
		using var documento = JsonDocument.Parse(json, new JsonDocumentOptions
		{
			CommentHandling = JsonCommentHandling.Skip,
			AllowTrailingCommas = true
		});

		var achado = Propriedade(documento.RootElement, secao, StringComparison.OrdinalIgnoreCase);
		if (achado is { } encontrado && nomeDoAmbiente != null)
			achado = Propriedade(encontrado, nomeDoAmbiente, StringComparison.Ordinal);

		return achado?.Clone();

		static JsonElement? Propriedade(JsonElement objeto, string nome, StringComparison comparacao)
		{
			if (objeto.ValueKind != JsonValueKind.Object)
				return null;

			foreach (var propriedade in objeto.EnumerateObject())
			{
				if (string.Equals(propriedade.Name, nome, comparacao))
					return propriedade.Value;
			}

			return null;
		}
	}
}
