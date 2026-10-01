using XUnitAssured.Core.Configuration;
using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace XUnitAssured.Playwright.Configuration;

/// <summary>
/// Loads Playwright settings.
///
/// <para>
/// From the <c>playwright</c> section of <c>testsettings.json</c>, which is where every other
/// package reads from. A project that crosses an API, a topic and a screen was configured in
/// three files with three sets of rules — one of them, <c>playwrightsettings.json</c>, had to
/// be copied to the output directory to be found at all, which is a thing to know rather than
/// a thing to guess.
/// </para>
///
/// <para>
/// The old file still works and says so once, out loud. Taking it away in the same release
/// that replaces it would break projects at the moment they upgrade, with settings silently
/// falling back to defaults — a browser suddenly headed, a timeout suddenly 30s.
/// </para>
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

			// The canonical file, same as every other package.
			var doTestSettings = TestSettingsSection.Read<PlaywrightSettings>("playwright", OpcoesDeLeitura);
			if (doTestSettings != null)
			{
				_cachedSettings = doTestSettings;
				return _cachedSettings.Clone();
			}

			// The file this package used to own. Still read, and announced as on its way out.
			foreach (var path in SearchPaths)
			{
				if (File.Exists(path))
				{
					AvisarQueOArquivoVaiSair(path);
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

	/// <summary>
	/// Case-insensitive on purpose: the old file was written in PascalCase and the canonical
	/// one in camelCase, and neither spelling should decide whether a setting is read.
	/// </summary>
	private static JsonSerializerOptions OpcoesDeLeitura => new()
	{
		PropertyNameCaseInsensitive = true,
		ReadCommentHandling = JsonCommentHandling.Skip,
		AllowTrailingCommas = true,
		Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
	};

	private static bool _avisou;

	/// <summary>
	/// Once per run, on standard error so it shows up in test output without failing anything.
	/// </summary>
	private static void AvisarQueOArquivoVaiSair(string caminho)
	{
		if (_avisou) return;
		_avisou = true;

		Console.Error.WriteLine(
			$"[XUnitAssured] '{caminho}' still works, but it is going away. Move its contents " +
			"into a \"playwright\" section of testsettings.json, where the http and kafka " +
			"settings already live.");
	}

	private static PlaywrightSettings LoadFromFile(string path)
	{
		try
		{
			var json = File.ReadAllText(path);

			return JsonSerializer.Deserialize<PlaywrightSettings>(json, OpcoesDeLeitura)
				?? new PlaywrightSettings();
		}
		catch (Exception)
		{
			return new PlaywrightSettings();
		}
	}
}
