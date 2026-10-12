using System;
using System.IO;
using System.Text.Json;

namespace XUnitAssured.Core.Configuration;

/// <summary>
/// Reads one section of <c>testsettings.json</c>.
///
/// <para>
/// Each package keeps its own settings type — Core has no business knowing what a broker
/// address or a browser timeout is — but finding the file is the same work every time:
/// honour <c>TESTSETTINGS_PATH</c>, walk up from the working directory and from the output
/// directory, expand <c>${ENV:NAME}</c>. The Kafka package carried its own copy of that
/// search, and the Playwright package was about to carry a third.
/// </para>
///
/// <para>
/// Packages hand in a section name and a type; where the file lives, and how it is found,
/// stays here.
/// </para>
/// </summary>
public static class TestSettingsSection
{
	private const string NomeDoArquivo = "testsettings.json";

	/// <summary>
	/// Deserialises a named section, or returns <c>null</c> when the file or the section is
	/// absent.
	/// </summary>
	/// <remarks>
	/// A malformed file returns <c>null</c> rather than throwing. A test run should fail on
	/// what it is testing, not on the shape of a configuration file it never looked at — and
	/// the package that asked for the section decides what a missing section means.
	/// </remarks>
	public static T? Read<T>(string nome, JsonSerializerOptions? opcoes = null) where T : class
	{
		var caminho = Localizar();
		if (caminho is null) return null;

		try
		{
			var json = ArquivoDeConfiguracao.ExpandirVariaveis(File.ReadAllText(caminho));

			using var documento = JsonDocument.Parse(json, new JsonDocumentOptions
			{
				CommentHandling = JsonCommentHandling.Skip,
				AllowTrailingCommas = true
			});

			if (!documento.RootElement.TryGetProperty(nome, out var secao))
				return null;

			return JsonSerializer.Deserialize<T>(secao.GetRawText(), opcoes ?? Padrao);
		}
		catch
		{
			return null;
		}
	}

	/// <summary>
	/// The path of the file in use, or <c>null</c> when there is none. Packages that cache by
	/// file use this as the key, so that two settings instances loaded from the same file
	/// share one entry.
	/// </summary>
	public static string? Localizar()
	{
		var caminhoDoAmbiente = Environment.GetEnvironmentVariable("TESTSETTINGS_PATH");
		if (!string.IsNullOrEmpty(caminhoDoAmbiente) && File.Exists(caminhoDoAmbiente))
			return caminhoDoAmbiente;

		return ArquivoDeConfiguracao.Localizar(NomeDoArquivo);
	}

	private static JsonSerializerOptions Padrao => new()
	{
		PropertyNameCaseInsensitive = true,
		ReadCommentHandling = JsonCommentHandling.Skip,
		AllowTrailingCommas = true
	};
}
