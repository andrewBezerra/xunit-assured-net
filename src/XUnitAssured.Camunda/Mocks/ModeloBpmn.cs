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
		string? Escopo,
		string? Sinal = null,
		string? CodigoDeErro = null,
		bool SemDefinicao = false,
		string? ProcessoChamado = null,
		string? Vinculo = null);

	public Elemento? Achar(string id) => _elementos.TryGetValue(id, out var e) ? e : null;

	private static readonly HashSet<string> TiposDeNo = new(StringComparer.Ordinal)
	{
		"startEvent", "endEvent", "intermediateCatchEvent", "intermediateThrowEvent", "boundaryEvent",
		"task", "serviceTask", "userTask", "scriptTask", "sendTask", "receiveTask", "businessRuleTask", "manualTask",
		"callActivity", "subProcess", "adHocSubProcess", "transaction",
		"exclusiveGateway", "parallelGateway", "inclusiveGateway", "eventBasedGateway", "complexGateway"
	};

	/// <summary>
	/// Os nós do fluxo, o que a cobertura conta: eventos, atividades e gateways. Fluxos de sequência,
	/// definições de evento e anotações ficam de fora.
	/// </summary>
	public IReadOnlyList<Elemento> NosDoFluxo() =>
		_elementos.Values.Where(e => TiposDeNo.Contains(e.Tipo)).OrderBy(e => e.Id, StringComparer.Ordinal).ToList();

	/// <summary>O nome de cada elemento que tem um, para o relatório.</summary>
	public IReadOnlyDictionary<string, string> Nomes { get; private set; } = new Dictionary<string, string>();

	/// <summary>O evento de fim simples do processo, fora de subprocessos: por onde um filho mockado termina bem.</summary>
	public string? EventoDeFim() =>
		_elementos.Values.FirstOrDefault(e => e is { Tipo: "endEvent", SemDefinicao: true, Escopo: null })?.Id;

	/// <summary>O evento de fim que lança o erro com este código, fora de subprocessos.</summary>
	public string? EventoDeFimComErro(string codigo) =>
		_elementos.Values.FirstOrDefault(e => e.Tipo == "endEvent" && e.CodigoDeErro == codigo && e.Escopo is null)?.Id;

	/// <summary>
	/// A borda de erro presa à atividade que pega este código (ou qualquer código, sem errorRef).
	/// </summary>
	public Elemento? BordaDeErro(string atividade, string codigo) =>
		_elementos.Values
			.Where(e => e.Tipo == "boundaryEvent" && e.AnexadoA == atividade && e.CodigoDeErro is not null)
			.OrderBy(e => e.CodigoDeErro == codigo ? 0 : 1)
			.FirstOrDefault(e => e.CodigoDeErro == codigo || e.CodigoDeErro!.Length == 0);

	/// <summary>
	/// As call activities sem <c>bindingType="deployment"</c> ou <c>"versionTag"</c>: chamam a última versão do
	/// filho, que num cluster compartilhado pode ser a de outra pessoa.
	/// </summary>
	public IReadOnlyList<Elemento> ChamadasDaUltimaVersao() =>
		_elementos.Values
			.Where(e => e.Tipo == "callActivity" && (e.Vinculo is null || e.Vinculo == "latest"))
			.ToList();

	public static ModeloBpmn Ler(string xml)
	{
		var documento = XDocument.Parse(xml);
		var fluxos = documento.Descendants(Bpmn + "sequenceFlow")
			.Select(f => (De: (string?)f.Attribute("sourceRef") ?? "", Para: (string?)f.Attribute("targetRef") ?? ""))
			.ToList();

		// O sinal se refere pelo id do bpmn:signal; o teste o chama pelo nome.
		// O erro se refere pelo id do bpmn:error; o teste o chama pelo código. Um errorEventDefinition sem
		// errorRef pega qualquer código: fica como "".
		var erros = documento.Descendants(Bpmn + "error")
			.Where(s => s.Attribute("id") is not null)
			.ToDictionary(s => (string)s.Attribute("id")!, s => (string?)s.Attribute("errorCode") ?? string.Empty, StringComparer.Ordinal);
		XNamespace zeebe = "http://camunda.org/schema/zeebe/1.0";

		var sinais = documento.Descendants(Bpmn + "signal")
			.Where(s => s.Attribute("id") is not null)
			.ToDictionary(s => (string)s.Attribute("id")!, s => (string?)s.Attribute("name") ?? (string)s.Attribute("id")!, StringComparer.Ordinal);

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
					escopo,
					(string?)e.Element(Bpmn + "signalEventDefinition")?.Attribute("signalRef") is { } referencia
						? sinais.TryGetValue(referencia, out var nome) ? nome : referencia
						: null,
					e.Element(Bpmn + "errorEventDefinition") is { } erro
						? (string?)erro.Attribute("errorRef") is { } refErro && erros.TryGetValue(refErro, out var codigo) ? codigo : string.Empty
						: null,
					!e.Elements().Any(d => d.Name.Namespace == Bpmn && d.Name.LocalName.EndsWith("EventDefinition", StringComparison.Ordinal)),
					e.Name.LocalName == "callActivity" ? (string?)e.Descendants(zeebe + "calledElement").FirstOrDefault()?.Attribute("processId") : null,
					e.Name.LocalName == "callActivity" ? (string?)e.Descendants(zeebe + "calledElement").FirstOrDefault()?.Attribute("bindingType") : null);
			}
		}

		var nomes = documento.Descendants()
			.Where(e => e.Name.Namespace == Bpmn && e.Attribute("id") is not null && !string.IsNullOrWhiteSpace((string?)e.Attribute("name")))
			.GroupBy(e => (string)e.Attribute("id")!)
			.ToDictionary(g => g.Key, g => (string)g.First().Attribute("name")!, StringComparer.Ordinal);

		return new ModeloBpmn(elementos) { Nomes = nomes };
	}
	/// <summary>
	/// Como pular o timer <paramref name="timerId"/>: qual elemento ativo indica que a instância
	/// chegou nele, se esse elemento é encerrado, e o que ativar depois.
	/// </summary>
	/// <returns>Nulo quando o timer não é deste processo. <paramref name="recusa"/> diz por que não dá para pular.</returns>
	public PuloDeEvento? PlanejarPulo(string timerId, out string? recusa)
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

		return Planejar(timer, $"SkipTimer('{timerId}')", "timer", out recusa);
	}

	/// <summary>
	/// Como disparar o sinal <paramref name="nome"/> só nesta instância: um plano por evento do processo
	/// que espera esse sinal.
	/// </summary>
	/// <remarks>
	/// O nome é o do <c>bpmn:signal</c>, e não o id do evento: o teste responde ao sinal, onde quer que
	/// o processo o espere. Os eventos que não dá para disparar entram em <paramref name="recusas"/>.
	/// </remarks>
	public IReadOnlyList<PuloDeEvento> PlanejarSinal(string nome, ICollection<string> recusas)
	{
		var planos = new List<PuloDeEvento>();
		foreach (var evento in _elementos.Values.Where(e => e.Sinal == nome))
		{
			var plano = Planejar(evento, $"MockSignal('{nome}') at '{evento.Id}'", "signal event", out var recusa);
			if (plano is not null) planos.Add(plano);
			if (recusa is not null) recusas.Add(recusa);
		}

		return planos;
	}

	private PuloDeEvento? Planejar(Elemento evento, string quem, string oQue, out string? recusa)
	{
		recusa = null;

		string esperando;
		bool encerrar;
		switch (evento.Tipo)
		{
			case "boundaryEvent" when evento.AnexadoA is not null:
				// A borda não aparece como espera: a instância está na atividade a que ela se prende.
				esperando = evento.AnexadoA;
				encerrar = evento.Interrompe;
				break;
			case "intermediateCatchEvent" when evento.GatewayDeEvento is not null:
				// Depois de um gateway de evento, quem espera é o gateway; encerrá-lo descarta os outros eventos.
				esperando = evento.GatewayDeEvento;
				encerrar = true;
				break;
			case "intermediateCatchEvent":
				esperando = evento.Id;
				encerrar = true;
				break;
			default:
				recusa = $"{quem}: a {oQue} on a {evento.Tipo} cannot be triggered by the test, only intermediate and boundary events.";
				return null;
		}

		if (evento.Saidas.Count == 0)
		{
			recusa = $"{quem}: the {oQue} has no outgoing sequence flow, so there is nothing to continue to.";
			return null;
		}

		// Ativar um gateway de junção por modificação o faz disparar sem esperar os outros ramos
		// (medido na 8.10.4): o processo seguiria adiante com ramos ainda abertos.
		var juncao = evento.Saidas
			.Select(Achar)
			.FirstOrDefault(s => s is { Tipo: "parallelGateway" or "inclusiveGateway", Entradas: > 1 });
		if (juncao is not null)
		{
			recusa = $"{quem}: the {oQue} flows into the joining gateway '{juncao.Id}'. Activating a joining gateway by " +
				"instance modification fires it without waiting for the other branches, so the test would run a path the " +
				"process never takes." + (evento.EhTimer
					? " Put the timer duration in a variable (timeDuration=\"=prazo\") and set it to PT1S in the test instead."
					: " Put an element (a task, an intermediate event) between the event and the gateway.");
			return null;
		}

		return new PuloDeEvento(evento.Id, esperando, encerrar, evento.Saidas, evento.Escopo);
	}
}

/// <summary>
/// O plano para disparar um evento só na instância: quando <see cref="ElementoEsperando"/> estiver
/// ativo, encerrá-lo (se <see cref="Encerrar"/>) e ativar <see cref="Ativar"/>, no <see cref="Escopo"/> dele.
/// </summary>
internal sealed record PuloDeEvento(
	string EventoId, string ElementoEsperando, bool Encerrar, IReadOnlyList<string> Ativar, string? Escopo);
