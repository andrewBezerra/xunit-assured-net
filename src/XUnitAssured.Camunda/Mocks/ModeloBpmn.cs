using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace XUnitAssured.Camunda.Mocks;

/// <summary>
/// O que o pacote precisa saber do BPMN implantado: por onde cada elemento sai, a que cada borda
/// está presa, o que é timer.
/// </summary>
/// <remarks>
/// Lido do XML da versão que a instância roda (<c>/process-definitions/{key}/xml</c>), e não do
/// arquivo do repositório: no teste do QA, quem implantou foi outra pessoa.
/// </remarks>
internal sealed class ModeloBpmn
{
	private static readonly XNamespace Bpmn = "http://www.omg.org/spec/BPMN/20100524/MODEL";

	private readonly Dictionary<string, Elemento> _elementos;

	private ModeloBpmn(Dictionary<string, Elemento> elementos) => _elementos = elementos;

	internal sealed record Elemento(
		string Id,
		string Tipo,
		IReadOnlyList<string> Saidas,
		int Entradas,
		string? AnexadoA,
		bool Interrompe,
		bool EhTimer,
		string? GatewayDeEvento,
		string? Escopo);

	public Elemento? Achar(string id) => _elementos.TryGetValue(id, out var e) ? e : null;

	public static ModeloBpmn Ler(string xml)
	{
		var documento = XDocument.Parse(xml);
		var fluxos = documento.Descendants(Bpmn + "sequenceFlow")
			.Select(f => (De: (string?)f.Attribute("sourceRef") ?? "", Para: (string?)f.Attribute("targetRef") ?? ""))
			.ToList();

		var tipos = new Dictionary<string, string>(StringComparer.Ordinal);
		foreach (var processo in documento.Descendants(Bpmn + "process"))
		{
			foreach (var e in processo.Descendants().Where(e => e.Attribute("id") is not null && e.Name.Namespace == Bpmn))
				tipos[(string)e.Attribute("id")!] = e.Name.LocalName;
		}

		var elementos = new Dictionary<string, Elemento>(StringComparer.Ordinal);
		foreach (var processo in documento.Descendants(Bpmn + "process"))
		{
			foreach (var e in processo.Descendants().Where(e => e.Attribute("id") is not null && e.Name.Namespace == Bpmn))
			{
				var id = (string)e.Attribute("id")!;
				if (e.Name.LocalName == "sequenceFlow") continue;

				var escopo = e.Parent?.Name == Bpmn + "subProcess" ? (string?)e.Parent.Attribute("id") : null;
				var gatewayDeEvento = fluxos
					.Where(f => f.Para == id)
					.Select(f => f.De)
					.FirstOrDefault(origem => tipos.TryGetValue(origem, out var t) && t == "eventBasedGateway");

				elementos[id] = new Elemento(
					id,
					e.Name.LocalName,
					fluxos.Where(f => f.De == id).Select(f => f.Para).ToList(),
					fluxos.Count(f => f.Para == id),
					(string?)e.Attribute("attachedToRef"),
					!string.Equals((string?)e.Attribute("cancelActivity"), "false", StringComparison.OrdinalIgnoreCase),
					e.Element(Bpmn + "timerEventDefinition") is not null,
					gatewayDeEvento,
					escopo);
			}
		}

		return new ModeloBpmn(elementos);
	}

	/// <summary>
	/// Como pular o timer <paramref name="timerId"/>: qual elemento ativo indica que a instância
	/// chegou nele, se esse elemento é encerrado, e o que ativar depois.
	/// </summary>
	/// <returns>Nulo quando o timer não é deste processo. <paramref name="recusa"/> diz por que não dá para pular.</returns>
	public PuloDeTimer? PlanejarPulo(string timerId, out string? recusa)
	{
		recusa = null;
		var timer = Achar(timerId);
		if (timer is null)
			return null;

		if (!timer.EhTimer)
		{
			recusa = $"SkipTimer('{timerId}'): '{timerId}' is a {timer.Tipo}, not a timer event.";
			return null;
		}

		string esperando;
		bool encerrar;
		switch (timer.Tipo)
		{
			case "boundaryEvent" when timer.AnexadoA is not null:
				// A borda não aparece como espera: a instância está na atividade a que ela se prende.
				esperando = timer.AnexadoA;
				encerrar = timer.Interrompe;
				break;
			case "intermediateCatchEvent" when timer.GatewayDeEvento is not null:
				// Depois de um gateway de evento, quem espera é o gateway; encerrá-lo descarta os outros eventos.
				esperando = timer.GatewayDeEvento;
				encerrar = true;
				break;
			case "intermediateCatchEvent":
				esperando = timer.Id;
				encerrar = true;
				break;
			default:
				recusa = $"SkipTimer('{timerId}'): a timer {timer.Tipo} cannot be skipped, only intermediate and boundary timer events.";
				return null;
		}

		if (timer.Saidas.Count == 0)
		{
			recusa = $"SkipTimer('{timerId}'): the timer has no outgoing sequence flow, so there is nothing to continue to.";
			return null;
		}

		// Ativar um gateway de junção por modificação o faz disparar sem esperar os outros ramos
		// (medido na 8.10.4): o processo seguiria adiante com ramos ainda abertos.
		var juncao = timer.Saidas
			.Select(Achar)
			.FirstOrDefault(s => s is { Tipo: "parallelGateway" or "inclusiveGateway", Entradas: > 1 });
		if (juncao is not null)
		{
			recusa = $"SkipTimer('{timerId}'): the timer flows into the joining gateway '{juncao.Id}'. Activating a joining " +
				"gateway by instance modification fires it without waiting for the other branches, so the test would run a " +
				"path the process never takes. Put the timer duration in a variable (timeDuration=\"=prazo\") and set it to " +
				"PT1S in the test instead.";
			return null;
		}

		return new PuloDeTimer(timerId, esperando, encerrar, timer.Saidas, timer.Escopo);
	}
}

/// <summary>
/// O plano para pular um timer: quando <see cref="ElementoEsperando"/> estiver ativo, encerrá-lo
/// (se <see cref="Encerrar"/>) e ativar <see cref="Ativar"/>, no <see cref="Escopo"/> dele.
/// </summary>
internal sealed record PuloDeTimer(
	string TimerId, string ElementoEsperando, bool Encerrar, IReadOnlyList<string> Ativar, string? Escopo);
