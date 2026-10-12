using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

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
	public static T? Read<T>(string nome, JsonSerializerOptions? opcoes = null) where T : class =>
		Read<T>(nome, opcoes, ambiente: null);

	/// <summary>
	/// The section as the environment sees it: <c>testsettings.json</c> with
	/// <c>testsettings.{environment}.json</c> laid over it. With no <paramref name="ambiente"/>,
	/// <c>TEST_ENV</c> decides, as it does for <see cref="TestSettings.Load"/>.
	/// </summary>
	internal static T? Read<T>(string nome, JsonSerializerOptions? opcoes, string? ambiente) where T : class
	{
		try
		{
			var raiz = Mesclar(Ambiente(ambiente));
			if (raiz is null || !raiz.TryGetPropertyValue(nome, out var secao) || secao is null)
				return null;

			return secao.Deserialize<T>(opcoes ?? Padrao);
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

	/// <summary>
	/// The environment in effect: the one asked for, otherwise <c>TEST_ENV</c>; <c>null</c> for none.
	/// </summary>
	internal static string? Ambiente(string? ambiente)
	{
		ambiente ??= Environment.GetEnvironmentVariable("TEST_ENV");
		return string.IsNullOrWhiteSpace(ambiente) ? null : ambiente;
	}

	/// <summary>
	/// The environment's file: next to the one <c>TESTSETTINGS_PATH</c> names, when it names one;
	/// otherwise found by the same search as the base file.
	/// </summary>
	internal static string? LocalizarDoAmbiente(string ambiente)
	{
		var caminhoDoAmbiente = Environment.GetEnvironmentVariable("TESTSETTINGS_PATH");
		if (!string.IsNullOrEmpty(caminhoDoAmbiente) && File.Exists(caminhoDoAmbiente))
		{
			var irmao = Path.Combine(
				Path.GetDirectoryName(Path.GetFullPath(caminhoDoAmbiente))!,
				$"{Path.GetFileNameWithoutExtension(caminhoDoAmbiente)}.{ambiente}{Path.GetExtension(caminhoDoAmbiente)}");
			return File.Exists(irmao) ? irmao : null;
		}

		return ArquivoDeConfiguracao.Localizar($"testsettings.{ambiente}.json");
	}

	/// <summary>
	/// The key for packages that cache a section: the files it is read from. Two settings loaded
	/// from the same files share one entry, which keeps parallel tests from racing to parse them.
	/// </summary>
	internal static string ChaveDeCache(string? ambiente)
	{
		var chave = Localizar() ?? "default";
		ambiente = Ambiente(ambiente);
		return ambiente is null ? chave : $"{chave}|{LocalizarDoAmbiente(ambiente) ?? ambiente}";
	}

	/// <summary>
	/// The base file with the environment's laid over it, or <c>null</c> when neither exists.
	/// </summary>
	/// <remarks>
	/// The environment's file overrides only what it writes, at any depth: a staging file with
	/// <c>{ "http": { "baseUrl": ... } }</c> keeps the base authentication. A file that cannot be
	/// read throws, naming it.
	/// </remarks>
	internal static JsonObject? Mesclar(string? ambiente)
	{
		var doBase = Localizar();
		var doAmbiente = ambiente is null ? null : LocalizarDoAmbiente(ambiente);
		if (doBase is null && doAmbiente is null)
			return null;

		var raiz = doBase is null ? new JsonObject() : Ler(doBase);
		if (doAmbiente is not null)
			Sobrepor(raiz, Ler(doAmbiente));

		return raiz;
	}

	private static JsonObject Ler(string caminho)
	{
		try
		{
			var json = ArquivoDeConfiguracao.ExpandirVariaveis(File.ReadAllText(caminho));
			return JsonNode.Parse(json, documentOptions: new JsonDocumentOptions
			{
				CommentHandling = JsonCommentHandling.Skip,
				AllowTrailingCommas = true
			}) as JsonObject ?? throw new JsonException("The file does not hold a JSON object.");
		}
		catch (Exception ex)
		{
			throw new InvalidOperationException($"Failed to load test settings from '{caminho}': {ex.Message}", ex);
		}
	}

	// Nomes sem diferença de maiúsculas, como a desserialização os lê: "Http" no base e "http"
	// no ambiente são a mesma seção.
	private static void Sobrepor(JsonObject alvo, JsonObject fonte)
	{
		foreach (var (nome, valor) in fonte.ToList())
		{
			var existente = alvo.Select(p => p.Key).FirstOrDefault(k => string.Equals(k, nome, StringComparison.OrdinalIgnoreCase)) ?? nome;

			if (valor is JsonObject objeto && alvo[existente] is JsonObject doAlvo)
				Sobrepor(doAlvo, objeto);
			else
				alvo[existente] = valor?.DeepClone();
		}
	}

	private static JsonSerializerOptions Padrao => new()
	{
		PropertyNameCaseInsensitive = true,
		ReadCommentHandling = JsonCommentHandling.Skip,
		AllowTrailingCommas = true
	};
}
