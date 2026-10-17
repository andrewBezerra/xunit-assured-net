using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace XUnitAssured.Core.Configuration;

/// <summary>
/// Where a settings file is looked for, and the <c>${ENV:NAME}</c> expansion every package
/// applies to it — one rule for <c>testsettings.json</c>, <c>httpsettings.json</c> and the old
/// <c>playwrightsettings.json</c> (#146).
/// </summary>
/// <remarks>
/// Each package had its own copy of the search, and they had drifted: Kafka, RabbitMQ and
/// Playwright also walked up from the output directory, while <c>TestSettings.Load</c>, the
/// HTTP settings and <c>httpsettings.json</c> did not. The same file next to the project was
/// found by one package and not by the next.
/// </remarks>
internal static partial class ArquivoDeConfiguracao
{
	// Gerada em tempo de compilação e com tempo limite: o texto vem de um arquivo, e uma regex
	// sem limite é a porta de um ReDoS (Sonar S6444).
	[GeneratedRegex(@"\$\{ENV:([^}]+)\}", RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 1000)]
	private static partial Regex VariavelDeAmbiente();

	/// <summary>
	/// The full path of the first <paramref name="nome"/> found, or <c>null</c>.
	/// </summary>
	internal static string? Localizar(string nome) =>
		Localizar(nome, Directory.GetCurrentDirectory(), AppContext.BaseDirectory);

	internal static string? Localizar(string nome, string diretorioAtual, string diretorioDeSaida)
	{
		foreach (var caminho in Candidatos(nome, diretorioAtual, diretorioDeSaida))
		{
			if (File.Exists(caminho))
				return caminho;
		}

		return null;
	}

	/// <summary>
	/// The working directory and up to three parents, then up from the output directory.
	/// </summary>
	/// <remarks>
	/// Three parents reach the project folder from <c>bin/&lt;Configuration&gt;/&lt;tfm&gt;</c>,
	/// where a test run usually starts. The walk from the output directory finds the file when
	/// the run starts somewhere else — a runner or a tool that keeps its own working directory.
	/// </remarks>
	internal static IEnumerable<string> Candidatos(string nome, string diretorioAtual, string diretorioDeSaida)
	{
		var diretorio = diretorioAtual;
		for (var nivel = 0; nivel <= 3 && !string.IsNullOrEmpty(diretorio); nivel++)
		{
			yield return Path.GetFullPath(Path.Combine(diretorio, nome));
			diretorio = Directory.GetParent(diretorio)?.FullName;
		}

		diretorio = diretorioDeSaida;
		while (!string.IsNullOrEmpty(diretorio))
		{
			yield return Path.GetFullPath(Path.Combine(diretorio, nome));
			diretorio = Directory.GetParent(diretorio)?.FullName;
		}
	}

	[GeneratedRegex(@"\$\{SECRET:([^}]+)\}", RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 1000)]
	private static partial Regex Segredo();

	/// <summary>
	/// Replaces <c>${ENV:NAME}</c> with the variable's value, and <c>${SECRET:Key}</c> with the
	/// test project's user secret (<see cref="SegredosDoUsuario"/>). One that is not found stays
	/// as written, so the setting that needed it shows what is missing.
	/// </summary>
	internal static string ExpandirVariaveis(string json) =>
		ExpandirVariaveis(json, Segredo().IsMatch(json) ? SegredosDoUsuario.Para(json) : _ => null);

	/// <param name="json">The settings file's text.</param>
	/// <param name="segredo">The user secret of a key, or <c>null</c>.</param>
	internal static string ExpandirVariaveis(string json, Func<string, string?> segredo)
	{
		var comVariaveis = VariavelDeAmbiente().Replace(json, m => Environment.GetEnvironmentVariable(m.Groups[1].Value) ?? m.Value);

		// Without a user secret, the environment variable with the same key, ':' written as '__'
		// as .NET configuration does, so one placeholder works on a developer machine and in CI.
		// The value is escaped for a JSON string: a password with a quote or a backslash would
		// otherwise break the file. ${ENV:...} keeps inserting the value as it is.
		return Segredo().Replace(comVariaveis, m =>
		{
			var chave = m.Groups[1].Value;
			var valor = segredo(chave) ?? Environment.GetEnvironmentVariable(chave.Replace(":", "__"));
			return valor is null ? m.Value : JsonEncodedText.Encode(valor).ToString();
		});
	}
}
