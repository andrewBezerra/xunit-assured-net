using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;

namespace XUnitAssured.Camunda.Steps;

/// <summary>
/// Acha o arquivo de um processo pelo id dele, para o <c>Deploy("pedido")</c>.
/// </summary>
/// <remarks>
/// <para>
/// O id vem de dentro do XML (<c>&lt;bpmn:process id="pedido"&gt;</c>), e não do nome do arquivo:
/// os dois não precisam coincidir, e um arquivo pode declarar mais de um processo.
/// </para>
/// <para>
/// O índice de uma pasta é lido uma vez por execução: os arquivos da pasta de saída não mudam
/// enquanto os testes rodam.
/// </para>
/// </remarks>
internal static class ArquivosBpmn
{
	private const string NamespaceBpmn = "http://www.omg.org/spec/BPMN/20100524/MODEL";

	private static readonly string[] Extensoes = { ".bpmn", ".dmn", ".form" };

	private static readonly ConcurrentDictionary<string, Lazy<Indice>> Indices = new(StringComparer.OrdinalIgnoreCase);

	/// <summary>
	/// Se o argumento do <c>Deploy</c> é um caminho, e não um id: tem separador de pasta ou termina
	/// numa extensão que o Camunda implanta. Um id de processo pode ter ponto (<c>pedido.v2</c>), então
	/// só essas extensões contam.
	/// </summary>
	internal static bool EhCaminho(string argumento) =>
		argumento.IndexOfAny(new[] { '/', '\\' }) >= 0 ||
		Extensoes.Any(e => argumento.EndsWith(e, StringComparison.OrdinalIgnoreCase));

	/// <summary>O arquivo que declara o processo, na pasta dada e nas de dentro dela.</summary>
	internal static string Localizar(string processId, string pasta)
	{
		var indice = Indices.GetOrAdd(Path.GetFullPath(pasta), p => new Lazy<Indice>(() => Ler(p))).Value;

		if (indice.Arquivos.TryGetValue(processId, out var arquivos))
		{
			if (arquivos.Count == 1)
				return arquivos[0];

			throw new InvalidOperationException(
				$"Process '{processId}' is declared in more than one BPMN file of {pasta}: " +
				$"{string.Join(", ", arquivos.Select(a => Relativo(pasta, a)))}. Pass the path of the one to deploy instead of the id.");
		}

		var conhecidos = indice.Arquivos.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList();
		var mensagem =
			$"No BPMN file in the test output folder ({pasta}) declares process '{processId}'. " +
			(conhecidos.Count > 0
				? $"Processes found there: {string.Join(", ", conhecidos.Select(k => $"'{k}'"))}. "
				: "No BPMN file was found there. ") +
			"Copy the files to the output with <None Include=\"bpmn\\*.bpmn\" CopyToOutputDirectory=\"PreserveNewest\" />, " +
			"or pass the path instead of the id.";

		if (indice.Ilegiveis.Count > 0)
			mensagem += $" These files could not be read as XML: {string.Join(", ", indice.Ilegiveis.Select(a => Relativo(pasta, a)))}.";

		throw new FileNotFoundException(mensagem, processId);
	}

	private static Indice Ler(string pasta)
	{
		var arquivos = new Dictionary<string, List<string>>(StringComparer.Ordinal);
		var ilegiveis = new List<string>();

		if (!Directory.Exists(pasta))
			return new Indice(arquivos, ilegiveis);

		foreach (var arquivo in Directory.EnumerateFiles(pasta, "*.bpmn", SearchOption.AllDirectories))
		{
			try
			{
				foreach (var id in Processos(arquivo))
				{
					if (!arquivos.TryGetValue(id, out var lista))
						arquivos[id] = lista = new List<string>();
					lista.Add(arquivo);
				}
			}
			catch (Exception e) when (e is XmlException or IOException or UnauthorizedAccessException)
			{
				ilegiveis.Add(arquivo);
			}
		}

		return new Indice(arquivos, ilegiveis);
	}

	private static IEnumerable<string> Processos(string arquivo)
	{
		var ids = new List<string>();
		using var leitor = XmlReader.Create(arquivo, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit });

		while (leitor.Read())
		{
			if (leitor.NodeType == XmlNodeType.Element && leitor.LocalName == "process" && leitor.NamespaceURI == NamespaceBpmn)
			{
				var id = leitor.GetAttribute("id");
				if (!string.IsNullOrWhiteSpace(id))
					ids.Add(id!);
			}
		}

		return ids;
	}

	private static string Relativo(string pasta, string arquivo)
	{
		var raiz = Path.GetFullPath(pasta).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
		return arquivo.StartsWith(raiz, StringComparison.OrdinalIgnoreCase) ? arquivo.Substring(raiz.Length) : arquivo;
	}

	private sealed record Indice(Dictionary<string, List<string>> Arquivos, List<string> Ilegiveis);
}
