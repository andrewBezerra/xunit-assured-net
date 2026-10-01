using System;
using System.IO;

using Shouldly;
using Xunit;

using XUnitAssured.Core.Configuration;
using XUnitAssured.Playwright.Configuration;

namespace XUnitAssured.Tests.PlaywrightTests;

[Collection("TestSettings")]
[Trait("Category", "Playwright")]
[Trait("Component", "Configuration")]
/// <summary>
/// Where browser settings come from.
///
/// <para>
/// They used to come from <c>playwrightsettings.json</c>, a file only this package knew about,
/// with its own spelling of keys and a requirement to be copied to the output directory to be
/// found at all. A project crossing an API, a topic and a screen was configured in three files
/// under three sets of rules.
/// </para>
///
/// <para>
/// They now come from the <c>playwright</c> section of <c>testsettings.json</c>, where the
/// other packages already read from. The old file still works, because taking it away in the
/// same release that replaces it would leave projects silently running on defaults — a browser
/// suddenly headed, a timeout suddenly back to 30s.
/// </para>
///
/// <para>
/// These tests share global state (an environment variable, the current directory and two
/// caches), hence the sequential collection.
/// </para>
/// </summary>
public sealed class PlaywrightSettingsSourceTests : IDisposable
{
	private readonly string? _caminhoAnterior = Environment.GetEnvironmentVariable("TESTSETTINGS_PATH");
	private readonly string _diretorioAnterior = Directory.GetCurrentDirectory();
	private readonly string _temporario = Path.Combine(Path.GetTempPath(), $"xa-pw-settings-{Guid.NewGuid():N}");

	public PlaywrightSettingsSourceTests()
	{
		Directory.CreateDirectory(_temporario);
		Directory.SetCurrentDirectory(_temporario);
	}

	[Fact(DisplayName = "Settings should come from the playwright section of testsettings.json")]
	public void Settings_Should_Come_From_Canonical_File()
	{
		EscreverTestSettings("""
			{
			  "testMode": "Local",
			  "playwright": {
			    "baseUrl": "https://canonical.test",
			    "headless": false
			  }
			}
			""");

		var settings = PlaywrightSettingsLoader.Load();

		settings.BaseUrl.ShouldBe("https://canonical.test");
		settings.Headless.ShouldBeFalse();
	}

	// Upgrading must not quietly change how a suite runs. A project that still has the old
	// file keeps the settings it had, and is told where they should move to.
	[Fact(DisplayName = "The retired file still configures a project that has not moved yet")]
	public void Retired_File_Still_Configures()
	{
		EscreverArquivoAntigo("""
			{
			  "BaseUrl": "https://legacy.test",
			  "Headless": false
			}
			""");

		var settings = PlaywrightSettingsLoader.Load();

		settings.BaseUrl.ShouldBe("https://legacy.test");
		settings.Headless.ShouldBeFalse();
	}

	// With both present, the one every other package reads from is the one that counts —
	// otherwise moving the settings would appear to do nothing.
	[Fact(DisplayName = "The canonical file wins over the retired one")]
	public void Canonical_Wins_Over_Retired()
	{
		EscreverArquivoAntigo("""
			{
			  "BaseUrl": "https://legacy.test"
			}
			""");
		EscreverTestSettings("""
			{
			  "playwright": {
			    "baseUrl": "https://canonical.test"
			  }
			}
			""");

		PlaywrightSettingsLoader.Load().BaseUrl.ShouldBe("https://canonical.test");
	}

	// A file without a browser section is not a misconfiguration: most projects do not test a
	// browser at all, and the ones that do may rely entirely on defaults.
	[Fact(DisplayName = "A file with no playwright section leaves the defaults in place")]
	public void No_Section_Leaves_Defaults()
	{
		EscreverTestSettings("""
			{
			  "testMode": "Local",
			  "kafka": { "bootstrapServers": "broker.test:9092" }
			}
			""");

		var settings = PlaywrightSettingsLoader.Load();
		var padrao = new PlaywrightSettings();

		settings.BaseUrl.ShouldBe(padrao.BaseUrl);
		settings.Headless.ShouldBe(padrao.Headless);
	}

	private void EscreverTestSettings(string json)
	{
		var caminho = Path.Combine(_temporario, "testsettings.json");
		File.WriteAllText(caminho, json);
		Environment.SetEnvironmentVariable("TESTSETTINGS_PATH", caminho);
		LimparCaches();
	}

	private void EscreverArquivoAntigo(string json)
	{
		File.WriteAllText(Path.Combine(_temporario, "playwrightsettings.json"), json);
		LimparCaches();
	}

	private static void LimparCaches()
	{
		TestSettingsLoader.ClearCache();
		PlaywrightSettingsLoader.ClearCache();
	}

	public void Dispose()
	{
		Environment.SetEnvironmentVariable("TESTSETTINGS_PATH", _caminhoAnterior);
		Directory.SetCurrentDirectory(_diretorioAnterior);
		LimparCaches();

		try
		{
			Directory.Delete(_temporario, recursive: true);
		}
		catch
		{
			// Um diretório temporário que não apaga não pode reprovar um teste que já passou.
		}
	}
}
