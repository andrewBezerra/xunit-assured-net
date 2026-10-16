using System.ComponentModel;
using System.Text;
using System.Xml.Linq;

using ModelContextProtocol.Server;

namespace XUnitAssured.Mcp.Tools;

/// <summary>
/// MCP tool for generating XUnitAssured.Camunda tests from a BPMN diagram.
/// Reads the process and writes the test skeleton: a mock for every external dependency
/// the diagram has, and the assertions of the path to its end.
/// </summary>
[McpServerToolType]
public static class CamundaTestGeneratorTools
{
	private static readonly XNamespace Bpmn = "http://www.omg.org/spec/BPMN/20100524/MODEL";
	private static readonly XNamespace Zeebe = "http://camunda.org/schema/zeebe/1.0";

	private static readonly string[] TarefasDeJob = { "serviceTask", "sendTask", "scriptTask", "businessRuleTask" };

	/// <summary>
	/// Writes a ready-to-paste xUnit test method for a BPMN process: Deploy, a mock for every
	/// job type, user task, message, signal and timer in the diagram, StartInstance, and the
	/// assertions of the happy path.
	/// </summary>
	[McpServerTool(Name = "generate_camunda_test"),
	 Description("Generates one XUnitAssured.Camunda test method from the XML of a BPMN diagram. " +
	             "Returns C# source text: an async [Fact] method that deploys the process, mocks every external dependency the diagram has " +
	             "(a MockJobWorker per job type, a MockUserTask per user task, a MockMessage per message, a MockSignal per signal, a SkipTimer per intermediate timer), " +
	             "starts an instance and asserts it completes at its end event. Boundary events and call activities come as commented alternatives, " +
	             "because taking them changes the path. The mocks' answers are placeholders to fill in. " +
	             "It does not write files, compile, run anything or talk to Camunda. Pass the diagram's XML, not a path: read the .bpmn file first.")]
	public static string GenerateCamundaTest(
		[Description("The XML of the .bpmn file.")] string bpmnXml,
		[Description("The id of the bpmn:process to test, when the file has more than one. Default: the first executable process.")] string? processId = null,
		[Description("Test display name. Auto-generated if empty.")] string? displayName = null)
	{
		XDocument documento;
		try
		{
			documento = XDocument.Parse(bpmnXml ?? string.Empty);
		}
		catch (System.Xml.XmlException e)
		{
			return $"// The BPMN could not be read as XML: {C(e.Message)}";
		}

		var processos = documento.Descendants(Bpmn + "process").ToList();
		var processo = processId is null
			? processos.FirstOrDefault(p => (string?)p.Attribute("isExecutable") != "false") ?? processos.FirstOrDefault()
			: processos.FirstOrDefault(p => (string?)p.Attribute("id") == processId);
		if (processo is null)
			return processId is null
				? "// The BPMN has no bpmn:process."
				: $"// The BPMN has no process '{C(processId)}'. Processes in it: {C(string.Join(", ", processos.Select(p => (string?)p.Attribute("id"))))}.";

		var id = (string)processo.Attribute("id")!;
		var mensagens = Nomes(documento, "message");
		var sinais = Nomes(documento, "signal");
		var elementos = processo.Descendants().Where(e => e.Name.Namespace == Bpmn && e.Attribute("id") is not null).ToList();
		var gatewaysDeEvento = elementos.Where(e => e.Name.LocalName == "eventBasedGateway").Select(Id).ToHashSet();
		var saindoDeGatewayDeEvento = processo.Descendants(Bpmn + "sequenceFlow")
			.Where(f => gatewaysDeEvento.Contains((string?)f.Attribute("sourceRef") ?? ""))
			.Select(f => (string?)f.Attribute("targetRef"))
			.ToHashSet();

		// Depois de um gateway de evento, o teste precisa escolher um caminho: o primeiro evento de cada
		// gateway entra no caminho principal, e os outros ficam como alternativa.
		var primeirosDoGateway = processo.Descendants(Bpmn + "sequenceFlow")
			.Where(f => gatewaysDeEvento.Contains((string?)f.Attribute("sourceRef") ?? ""))
			.GroupBy(f => (string?)f.Attribute("sourceRef"))
			.Select(g => (string?)g.First().Attribute("targetRef"))
			.ToHashSet();
		bool Alternativo(XElement evento) =>
			evento.Name.LocalName == "boundaryEvent" ||
			(saindoDeGatewayDeEvento.Contains(Id(evento)) && !primeirosDoGateway.Contains(Id(evento)));

		var mocks = new List<string>();
		var alternativas = new List<string>();
		var avisos = new List<string>();

		// Jobs, um mock por tipo: é pelo tipo que o worker de verdade os pega.
		foreach (var tipo in elementos
			.Where(e => TarefasDeJob.Contains(e.Name.LocalName))
			.Select(e => (string?)e.Descendants(Zeebe + "taskDefinition").FirstOrDefault()?.Attribute("type"))
			.Where(t => !string.IsNullOrWhiteSpace(t))
			.Distinct())
		{
			mocks.Add($".MockJobWorker({L(tipo)}).ThenComplete(new {{ /* what the {CB(tipo)} worker returns */ }})");
		}

		foreach (var tarefa in elementos.Where(e => e.Name.LocalName == "userTask"))
		{
			if (tarefa.Descendants(Zeebe + "userTask").Any())
				mocks.Add($".MockUserTask({L(Id(tarefa))}).ThenComplete(new {{ /* what the form returns */ }}){Rotulo(tarefa)}");
			else
				mocks.Add($".MockJobWorker(\"io.camunda.zeebe:userTask\").ThenComplete() // '{C(Id(tarefa))}' is a job-based user task");
		}


		foreach (var evento in elementos.Where(e => e.Element(Bpmn + "timerEventDefinition") is not null))
		{
			if (evento.Name.LocalName is not ("intermediateCatchEvent" or "boundaryEvent"))
				continue;

			var linha = $".SkipTimer({L(Id(evento))}){Rotulo(evento)}";
			if (Alternativo(evento))
				alternativas.Add($"{linha} // takes the timer's path instead");
			else
				mocks.Add(linha);
		}

		foreach (var evento in elementos.Where(e => e.Element(Bpmn + "messageEventDefinition") is not null || e.Name.LocalName == "receiveTask"))
		{
			var referencia = (string?)(evento.Element(Bpmn + "messageEventDefinition")?.Attribute("messageRef") ?? evento.Attribute("messageRef"));
			if (referencia is null || evento.Name.LocalName is "startEvent" or "endEvent" or "intermediateThrowEvent")
				continue;

			var nome = mensagens.TryGetValue(referencia, out var n) ? n : referencia;
			var linha = $".MockMessage({L(nome)}).ThenCorrelate(new {{ /* the message's variables */ }}){Rotulo(evento)}";
			if (Alternativo(evento))
				alternativas.Add($"{linha} // takes the message's path instead");
			else
				mocks.Add(linha);
		}

		foreach (var evento in elementos.Where(e => e.Element(Bpmn + "signalEventDefinition") is not null))
		{
			if (evento.Name.LocalName is "startEvent" or "endEvent" or "intermediateThrowEvent")
				continue;

			var referencia = (string?)evento.Element(Bpmn + "signalEventDefinition")!.Attribute("signalRef") ?? Id(evento);
			var nome = sinais.TryGetValue(referencia, out var n) ? n : referencia;
			var linha = $".MockSignal({L(nome)}).ThenTrigger(){Rotulo(evento)}";
			if (Alternativo(evento))
				alternativas.Add($"{linha} // takes the signal's path instead");
			else
				mocks.Add(linha);
		}

		foreach (var erro in elementos.Where(e => e.Name.LocalName == "boundaryEvent" && e.Element(Bpmn + "errorEventDefinition") is not null))
		{
			var anexado = (string?)erro.Attribute("attachedToRef");
			var codigo = Codigo(documento, (string?)erro.Element(Bpmn + "errorEventDefinition")!.Attribute("errorRef"));
			var tarefa = elementos.FirstOrDefault(e => Id(e) == anexado);
			var tipo = (string?)tarefa?.Descendants(Zeebe + "taskDefinition").FirstOrDefault()?.Attribute("type");

			if (tarefa?.Name.LocalName == "callActivity")
				alternativas.Add($".MockChildProcess({L(anexado)}).ThenThrowBpmnError({L(codigo)}) // takes '{C(Id(erro))}' instead");
			else if (tipo is not null)
				alternativas.Add($".MockJobWorker({L(tipo)}).ThenThrowBpmnError({L(codigo)}) // takes '{C(Id(erro))}' instead");
		}

		var deploy = new List<string> { id };
		foreach (var chamada in elementos.Where(e => e.Name.LocalName == "callActivity"))
		{
			var elemento = chamada.Descendants(Zeebe + "calledElement").FirstOrDefault();
			var filho = (string?)elemento?.Attribute("processId");
			var vinculo = (string?)elemento?.Attribute("bindingType");
			if (filho is not null && !deploy.Contains(filho))
				deploy.Add(filho);

			alternativas.Add($".MockChildProcess({L(Id(chamada))}).ThenComplete(new {{ /* what '{CB(filho)}' returns */ }}) // instead of running the real child");
			if (vinculo is null or "latest")
				avisos.Add($"Call activity '{Id(chamada)}' calls '{filho}' with bindingType \"{vinculo ?? "latest"}\": on a shared cluster it may start " +
				           "someone else's version. Use bindingType=\"deployment\" and deploy the child with the parent.");
		}

		// O fim esperado é o que se alcança do início pelos fluxos de sequência: uma borda não é alvo de
		// fluxo nenhum, então os fins que só ela alcança ficam como alternativa.
		var fluxos = processo.Elements(Bpmn + "sequenceFlow")
			.Select(f => (De: (string?)f.Attribute("sourceRef") ?? "", Para: (string?)f.Attribute("targetRef") ?? ""))
			.ToList();
		var alcancaveis = new HashSet<string>(processo.Elements(Bpmn + "startEvent").Select(Id));
		var fila = new Queue<string>(alcancaveis);
		while (fila.Count > 0)
		{
			var atual = fila.Dequeue();
			foreach (var proximo in fluxos.Where(f => f.De == atual).Select(f => f.Para))
				if (alcancaveis.Add(proximo))
					fila.Enqueue(proximo);
		}

		var fins = processo.Elements(Bpmn + "endEvent")
			.Where(e => !e.Elements().Any(d => d.Name.LocalName.EndsWith("EventDefinition")))
			.Select(Id)
			.OrderBy(f => alcancaveis.Contains(f) ? 0 : 1)
			.ToList();

		var codigoDoTeste = new StringBuilder();
		codigoDoTeste.AppendLine($"\t[Fact(DisplayName = {L(displayName ?? $"The {id} process runs to its end with its dependencies mocked")})]");
		codigoDoTeste.AppendLine($"\tpublic async Task {Nome(id)}_Runs_To_The_End()");
		codigoDoTeste.AppendLine("\t{");
		foreach (var aviso in avisos)
			codigoDoTeste.AppendLine($"\t\t// Warning: {C(aviso)}");
		codigoDoTeste.AppendLine("\t\tawait Given()");
		codigoDoTeste.AppendLine($"\t\t\t.Deploy({string.Join(", ", deploy.Select(L))})");
		foreach (var mock in mocks)
			codigoDoTeste.AppendLine($"\t\t\t{mock}");
		if (alternativas.Count > 0)
		{
			codigoDoTeste.AppendLine("\t\t\t// Other paths: swap one of these in for a test of its own");
			foreach (var alternativa in alternativas)
				codigoDoTeste.AppendLine($"\t\t\t// {C(alternativa)}");
		}
		codigoDoTeste.AppendLine($"\t\t\t.StartInstance({L(id)}, new {{ /* the start variables; a correlation key unique to the test */ }})");
		codigoDoTeste.AppendLine("\t\t\t.ExecuteAsync()");
		codigoDoTeste.Append("\t\t\t.AssertInstanceCompleted()");
		if (fins.Count > 0)
			codigoDoTeste.Append($"{Environment.NewLine}\t\t\t.AssertCompletedElements({L(fins[0])})");
		codigoDoTeste.Append(';');
		// O comentário vai depois do ponto e vírgula: antes, ele o engoliria.
		if (fins.Count > 1)
			codigoDoTeste.Append($" // or end at: {C(string.Join(", ", fins.Skip(1)))}");
		codigoDoTeste.AppendLine();
		codigoDoTeste.AppendLine("\t}");

		return codigoDoTeste.ToString();
	}

	private static Dictionary<string, string> Nomes(XDocument documento, string elemento) =>
		documento.Descendants(Bpmn + elemento)
			.Where(e => e.Attribute("id") is not null)
			.ToDictionary(e => (string)e.Attribute("id")!, e => (string?)e.Attribute("name") ?? (string)e.Attribute("id")!);

	private static string Codigo(XDocument documento, string? referencia) =>
		documento.Descendants(Bpmn + "error").FirstOrDefault(e => (string?)e.Attribute("id") == referencia)?.Attribute("errorCode")?.Value
		?? "ERROR_CODE";

	private static string Id(XElement elemento) => (string)elemento.Attribute("id")!;

	private static string Rotulo(XElement elemento) =>
		elemento.Attribute("name") is { } nome ? $" // {C(nome.Value)}" : string.Empty;

	// O BPMN vem de fora e o código gerado vai ser colado num projeto: nada do que ele traz pode sair
	// de um literal, de um comentário ou de um identificador.

	private const char Barra = (char)92;
	private const char Aspas = (char)34;
	private const char SeparadorDeLinha = (char)0x2028;
	private const char SeparadorDeParagrafo = (char)0x2029;
	private const char ProximaLinha = (char)0x85;

	private static bool QuebraLinha(char c) =>
		char.IsControl(c) || c == SeparadorDeLinha || c == SeparadorDeParagrafo || c == ProximaLinha;

	/// <summary>Um literal de string C#, com aspas, barras e caracteres de controle escapados.</summary>
	internal static string L(string? valor)
	{
		var texto = new StringBuilder().Append(Aspas);
		foreach (var c in valor ?? string.Empty)
		{
			if (c == Aspas || c == Barra)
				texto.Append(Barra).Append(c);
			else if (QuebraLinha(c))
				texto.Append(Barra).Append('u').Append(((int)c).ToString("X4"));
			else
				texto.Append(c);
		}

		return texto.Append(Aspas).ToString();
	}

	/// <summary>Texto para um comentário de linha: sem quebra de linha, que o encerraria.</summary>
	internal static string C(string? valor) =>
		new string((valor ?? string.Empty).Select(c => QuebraLinha(c) ? ' ' : c).ToArray());

	/// <summary>Texto para um comentário de bloco: também sem o fim de comentário.</summary>
	internal static string CB(string? valor) => C(valor).Replace("*/", "* /", StringComparison.Ordinal);

	/// <summary>O nome do método: só letras e dígitos ASCII, começando por letra.</summary>
	internal static string Nome(string id)
	{
		var nome = string.Concat(id.Split('-', '_', '.', ' ')
			.Select(p => new string(p.Where(char.IsAsciiLetterOrDigit).ToArray()))
			.Where(p => p.Length > 0)
			.Select(p => char.ToUpperInvariant(p[0]) + p[1..]));

		return nome.Length == 0 || char.IsAsciiDigit(nome[0]) ? "Process" + nome : nome;
	}
}
