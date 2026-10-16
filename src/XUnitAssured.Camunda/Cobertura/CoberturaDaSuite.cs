using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

using XUnitAssured.Camunda.Mocks;

namespace XUnitAssured.Camunda.Cobertura;

/// <summary>
/// O que as instâncias dos testes percorreram em cada diagrama, juntado ao longo do processo de teste.
/// </summary>
/// <remarks>
/// <para>
/// Cada laço de mocks registra aqui os elementos da sua árvore quando a instância termina, ou quando
/// é cancelada no fim da rodada. No fim do processo de teste, <see cref="Escrever"/> grava o relatório:
/// um <c>coverage.json</c> e um <c>index.html</c> que desenha cada diagrama e pinta o que rodou.
/// </para>
/// <para>
/// Conta os nós do fluxo (eventos, atividades, gateways) da versão implantada que as instâncias
/// rodaram. Um elemento conta como percorrido se a instância passou por ele, completado ou encerrado:
/// um timer pulado conta. Os fluxos de sequência não contam: a busca do Camunda mostra os elementos,
/// e não os fluxos.
/// </para>
/// </remarks>
internal sealed class CoberturaDaSuite
{
	/// <summary>A cobertura do processo de teste, que o fim do processo grava.</summary>
	internal static readonly CoberturaDaSuite Global = new();

	private readonly ConcurrentDictionary<string, Processo> _processos = new(StringComparer.Ordinal);

	internal bool Vazia => _processos.IsEmpty;

	/// <summary>Soma os elementos que uma instância percorreu à cobertura da versão dela.</summary>
	internal void Registrar(string definitionKey, string processId, int? version, string xml, IEnumerable<string> percorridos)
	{
		var processo = _processos.GetOrAdd(definitionKey, _ => new Processo(definitionKey, processId, version, xml));
		lock (processo)
		{
			processo.Instancias++;
			foreach (var id in percorridos)
				processo.Percorridos.Add(id);
		}
	}

	/// <summary>Grava <c>coverage.json</c> e <c>index.html</c> na pasta, e devolve o caminho do HTML.</summary>
	internal string? Escrever(string pasta)
	{
		var relatorio = Relatorio();
		if (relatorio.Processes.Count == 0)
			return null;

		Directory.CreateDirectory(pasta);
		File.WriteAllText(Path.Combine(pasta, "coverage.json"), JsonSerializer.Serialize(relatorio, Json));

		var html = Path.Combine(pasta, "index.html");
		File.WriteAllText(html, Html(relatorio));
		return html;
	}

	internal RelatorioDeCobertura Relatorio()
	{
		var processos = new List<CoberturaDoProcesso>();
		foreach (var processo in _processos.Values.OrderBy(p => p.ProcessId, StringComparer.Ordinal).ThenBy(p => p.Version))
		{
			ModeloBpmn modelo;
			try
			{
				modelo = ModeloBpmn.Ler(processo.Xml);
			}
			catch (System.Xml.XmlException)
			{
				continue;
			}

			HashSet<string> percorridos;
			int instancias;
			lock (processo)
			{
				percorridos = new HashSet<string>(processo.Percorridos, StringComparer.Ordinal);
				instancias = processo.Instancias;
			}

			var nos = modelo.NosDoFluxo();
			var feitos = nos.Where(n => percorridos.Contains(n.Id)).ToList();
			var faltam = nos.Where(n => !percorridos.Contains(n.Id))
				.Select(n => new ElementoNaoPercorrido(n.Id, modelo.Nomes.TryGetValue(n.Id, out var nome) ? nome : null, n.Tipo))
				.ToList();

			processos.Add(new CoberturaDoProcesso(
				processo.ProcessId,
				processo.DefinitionKey,
				processo.Version,
				instancias,
				nos.Count,
				feitos.Count,
				nos.Count == 0 ? 0 : Math.Round(100.0 * feitos.Count / nos.Count, 1),
				feitos.Select(n => n.Id).ToList(),
				faltam,
				processo.Xml));
		}

		return new RelatorioDeCobertura(DateTimeOffset.Now, processos);
	}

	// O HTML carrega o bpmn-js de um CDN para desenhar o diagrama; os dados vão dentro da página.
	private static string Html(RelatorioDeCobertura relatorio)
	{
		var dados = JsonSerializer.Serialize(relatorio, Json).Replace("</", "<\\/", StringComparison.Ordinal);
		var html = new StringBuilder(Modelo);
		html.Replace("{{DADOS}}", dados);
		html.Replace("{{GERADO}}", relatorio.GeneratedAt.ToString("yyyy-MM-dd HH:mm:ss zzz"));
		return html.ToString();
	}

	private static readonly JsonSerializerOptions Json = new()
	{
		WriteIndented = true,
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
		Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
	};

	private sealed class Processo
	{
		public Processo(string definitionKey, string processId, int? version, string xml)
		{
			DefinitionKey = definitionKey;
			ProcessId = processId;
			Version = version;
			Xml = xml;
		}

		public string DefinitionKey { get; }
		public string ProcessId { get; }
		public int? Version { get; }
		public string Xml { get; }
		public int Instancias { get; set; }
		public HashSet<string> Percorridos { get; } = new(StringComparer.Ordinal);
	}

	private const string Modelo = """
		<!doctype html>
		<html lang="en">
		<head>
		<meta charset="utf-8">
		<meta name="viewport" content="width=device-width, initial-scale=1">
		<title>Camunda coverage</title>
		<style>
		  :root { --fundo:#fafafa; --texto:#1b1b1b; --suave:#5f6368; --ok:#2e7d32; --ok-fundo:#c8e6c9; --falta:#c62828; --falta-fundo:#ffcdd2; --borda:#ddd; }
		  @media (prefers-color-scheme: dark) { :root { --fundo:#16181c; --texto:#e8e8e8; --suave:#a0a4a8; --borda:#333; } }
		  body { margin:0; padding:16px; font:14px/1.45 system-ui, sans-serif; background:var(--fundo); color:var(--texto); }
		  h1 { font-size:20px; margin:0 0 4px; } .gerado { color:var(--suave); margin-bottom:16px; }
		  section { border:1px solid var(--borda); border-radius:8px; padding:12px; margin-bottom:20px; }
		  h2 { font-size:16px; margin:0 0 6px; } .pct { font-weight:600; }
		  .barra { height:8px; background:var(--falta-fundo); border-radius:4px; overflow:hidden; margin:6px 0 10px; }
		  .barra > div { height:100%; background:var(--ok); }
		  .diagrama { height:360px; background:#fff; border:1px solid var(--borda); border-radius:6px; }
		  .faltam { margin:10px 0 0; padding-left:18px; } .faltam code { color:var(--falta); }
		  .xa-percorrido:not(.djs-connection) .djs-visual > :nth-child(1) { fill:var(--ok-fundo) !important; stroke:var(--ok) !important; }
		  .xa-faltou:not(.djs-connection) .djs-visual > :nth-child(1) { fill:var(--falta-fundo) !important; stroke:var(--falta) !important; }
		</style>
		</head>
		<body>
		<h1>Camunda coverage</h1>
		<div class="gerado">Generated {{GERADO}} by XUnitAssured.Camunda. Green: reached by a test instance. Red: never reached.</div>
		<div id="processos"></div>
		<script id="dados" type="application/json">{{DADOS}}</script>
		<script src="https://cdn.jsdelivr.net/npm/bpmn-js@18.31.0/dist/bpmn-navigated-viewer.production.min.js"
		  integrity="sha384-gLklyzvVgzCgQ4V6tTL36R8KWYxSlyitIYF4Fi1oDJYK61zQHCb0/N03NfqFLq6X" crossorigin="anonymous"></script>
		<script>
		  const dados = JSON.parse(document.getElementById('dados').textContent);
		  const raiz = document.getElementById('processos');
		  // Os nomes vêm do BPMN: escapados antes de entrar no HTML.
		  const esc = t => String(t).replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[c]);
		  dados.processes.forEach((p, i) => {
		    const s = document.createElement('section');
		    const versao = p.version != null ? ` version ${p.version}` : '';
		    s.innerHTML = `<h2>${esc(p.processId)}${versao} <span class="pct">${p.coverage}%</span></h2>
		      <div>${p.reached} of ${p.total} flow nodes reached, by ${p.instances} instance(s)</div>
		      <div class="barra"><div style="width:${p.coverage}%"></div></div>
		      <div class="diagrama" id="d${i}"></div>` +
		      (p.missed.length ? `<ul class="faltam">${p.missed.map(m => `<li><code>${esc(m.id)}</code> ${m.name ? '(' + esc(m.name) + ') ' : ''}${esc(m.type)}</li>`).join('')}</ul>` : '');
		    raiz.appendChild(s);
		    if (!window.BpmnJS) return;
		    const viewer = new BpmnJS({ container: '#d' + i });
		    viewer.importXML(p.xml).then(() => {
		      const canvas = viewer.get('canvas');
		      canvas.zoom('fit-viewport');
		      p.reachedElements.forEach(id => { try { canvas.addMarker(id, 'xa-percorrido'); } catch {} });
		      p.missed.forEach(m => { try { canvas.addMarker(m.id, 'xa-faltou'); } catch {} });
		    }).catch(() => {});
		  });
		</script>
		</body>
		</html>
		""";
}

/// <summary>O relatório inteiro, como o <c>coverage.json</c> o grava.</summary>
internal sealed record RelatorioDeCobertura(DateTimeOffset GeneratedAt, IReadOnlyList<CoberturaDoProcesso> Processes);

/// <summary>A cobertura de uma versão de um processo.</summary>
internal sealed record CoberturaDoProcesso(
	string ProcessId,
	string DefinitionKey,
	int? Version,
	int Instances,
	int Total,
	int Reached,
	double Coverage,
	IReadOnlyList<string> ReachedElements,
	IReadOnlyList<ElementoNaoPercorrido> Missed,
	string Xml);

/// <summary>Um nó do fluxo que nenhuma instância dos testes percorreu.</summary>
internal sealed record ElementoNaoPercorrido(string Id, string? Name, string Type);
