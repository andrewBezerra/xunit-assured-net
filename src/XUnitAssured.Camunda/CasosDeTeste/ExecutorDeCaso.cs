using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

using Shouldly;

using XUnitAssured.Camunda.DSL;
using XUnitAssured.Camunda.Extensions;
using XUnitAssured.Camunda.Gateway;
using XUnitAssured.Camunda.Mocks;
using XUnitAssured.Core.Abstractions;

namespace XUnitAssured.Camunda.TestCases;

/// <summary>
/// Roda as instruções de um caso de teste JSON, em ordem, sobre o XUnitAssured.Camunda.
/// </summary>
/// <remarks>
/// <para>
/// Os <c>MOCK_*</c> são os mocks reativos do pacote, registrados antes da primeira instância: no CPT
/// também são workers que respondem a qualquer job do tipo, inclusive um que já espera. As outras
/// ações (<c>COMPLETE_JOB</c>, <c>COMPLETE_USER_TASK</c>, mensagens) acontecem uma vez, no ponto do
/// caso em que aparecem, esperando o que precisam aparecer. As asserções esperam, como as do pacote.
/// </para>
/// <para>
/// O caso inteiro é conferido antes de começar: uma instrução que não cabe num cluster compartilhado
/// falha o teste na hora, com o motivo, em vez de rodar pela metade.
/// </para>
/// </remarks>
internal sealed class ExecutorDeCaso
{
	private static readonly JsonSerializerOptions Leitura = new() { PropertyNameCaseInsensitive = true };

	private readonly CamundaTestCase _caso;
	private readonly ITestScenario _cenario;
	private readonly string[] _deploy;
	private readonly List<(string ProcessId, string Chave, MocksDaInstancia? Mocks)> _instancias = new();

	private CamundaSettings _settings = new();
	private ICamundaGateway _gateway = null!;
	private int _passo;
	private string _tipo = string.Empty;

	public ExecutorDeCaso(CamundaTestCase caso, ITestScenario cenario, string[] deploy)
	{
		_caso = caso;
		_cenario = cenario;
		_deploy = deploy;
	}

	public async Task ExecutarAsync()
	{
		if (_caso.Instructions.ValueKind != JsonValueKind.Array)
			throw new ShouldAssertException($"Test case '{_caso.Name}' has no instructions array.");

		var instrucoes = _caso.Instructions.EnumerateArray().ToList();
		var recusas = instrucoes
			.Select((instrucao, i) => (i, Motivo: Recusa(instrucao)))
			.Where(r => r.Motivo is not null)
			.Select(r => $"  #{r.i + 1} {Tipo(instrucoes[r.i])}: {r.Motivo}")
			.ToList();
		if (recusas.Count > 0)
			throw new ShouldAssertException(
				$"Test case '{_caso.Name}' ({_caso.File}) uses instructions XUnitAssured.Camunda does not run:{Environment.NewLine}" +
				string.Join(Environment.NewLine, recusas));

		_settings = CamundaSettings.De(_cenario.Context);
		_gateway = Gateways.De(_cenario.Context, _settings);

		if (_deploy.Length > 0)
			CamundaVerbos.Deploy(_cenario, _deploy);

		// Os mocks antes da primeira instância: o laço de cada uma copia os que existem ao nascer.
		foreach (var instrucao in instrucoes)
			RegistrarMock(instrucao);

		for (_passo = 0; _passo < instrucoes.Count; _passo++)
		{
			var instrucao = instrucoes[_passo];
			_tipo = Tipo(instrucao);
			if (_tipo.StartsWith("MOCK_", StringComparison.Ordinal))
				continue;

			await ExecutarAsync(instrucao).ConfigureAwait(false);
		}
	}

	// ---------- O que não roda num cluster compartilhado ----------

	internal static string? Recusa(JsonElement instrucao) => Tipo(instrucao) switch
	{
		"CREATE_PROCESS_INSTANCE" when Tem(instrucao, "startInstructions") || Tem(instrucao, "runtimeInstructions") =>
			"startInstructions and runtimeInstructions are not supported; start the instance at its start event.",
		"CREATE_PROCESS_INSTANCE" => null,
		"MOCK_JOB_WORKER_COMPLETE_JOB" or "COMPLETE_JOB" or "COMPLETE_USER_TASK" when Verdadeiro(instrucao, "useExampleData") =>
			"useExampleData is not supported; give the variables.",
		"MOCK_JOB_WORKER_COMPLETE_JOB" or "MOCK_JOB_WORKER_THROW_BPMN_ERROR" => null,
		"MOCK_CHILD_PROCESS" when Tem(instrucao, "versionTag") => "versionTag is not supported; the child is mocked whatever its version.",
		"MOCK_CHILD_PROCESS" => null,
		"COMPLETE_JOB" or "THROW_BPMN_ERROR_FROM_JOB" or "COMPLETE_USER_TASK" => null,
		"PUBLISH_MESSAGE" or "CORRELATE_MESSAGE" => null,
		"UPDATE_VARIABLES" when Tem(instrucao, "elementSelector") || Verdadeiro(instrucao, "createLocalVariables") =>
			"only process instance variables are supported, not local ones.",
		"UPDATE_VARIABLES" => null,
		"ASSERT_PROCESS_INSTANCE" or "ASSERT_ELEMENT_INSTANCE" or "ASSERT_ELEMENT_INSTANCES" => null,
		"ASSERT_VARIABLES" when Tem(instrucao, "elementSelector") => "only process instance variables are supported, not local ones.",
		"ASSERT_VARIABLES" => null,
		"ASSERT_USER_TASK" when Tem(instrucao, "dueDate") || Tem(instrucao, "followUpDate") =>
			"dueDate and followUpDate are not supported; assert the state, assignee, candidate groups, priority or name.",
		"ASSERT_USER_TASK" => null,
		"ASSERT_PROCESS_INSTANCE_MESSAGE_SUBSCRIPTION" when Texto(instrucao, "state") == "IS_CORRELATED" =>
			"IS_CORRELATED is not supported; assert IS_WAITING or IS_NOT_WAITING, or the path the message took.",
		"ASSERT_PROCESS_INSTANCE_MESSAGE_SUBSCRIPTION" => null,
		"INCREASE_TIME" or "SET_TIME" =>
			"the clock belongs to the whole cluster. Mock the timer with SkipTimer in C#, or put its duration in a variable and start with PT1S.",
		"BROADCAST_SIGNAL" =>
			"a broadcast reaches every instance on the cluster that waits for the signal. Use MockSignal in C#, which triggers it only in the test's instance.",
		"MOCK_DMN_DECISION" or "EVALUATE_DECISION" or "ASSERT_DECISION" =>
			"decisions are not supported yet: deploy the real DMN with the process.",
		"ASSERT_VARIABLE" =>
			"FEEL expressions, judges and semantic similarity are not supported; use ASSERT_VARIABLES with the expected values.",
		"RESOLVE_INCIDENT" => "resolving incidents is not supported yet.",
		var outro => $"'{outro}' is not supported."
	};

	// ---------- Mocks ----------

	private void RegistrarMock(JsonElement instrucao)
	{
		switch (Tipo(instrucao))
		{
			case "MOCK_JOB_WORKER_COMPLETE_JOB":
				MocksDaCadeia.Registrar(_cenario.Context,
					new JobMock(Texto(instrucao, "jobType")!, AcaoDoMock.Completar, Variaveis(instrucao)));
				break;
			case "MOCK_JOB_WORKER_THROW_BPMN_ERROR":
				MocksDaCadeia.Registrar(_cenario.Context, new JobMock(
					Texto(instrucao, "jobType")!, AcaoDoMock.LancarErroBpmn, Variaveis(instrucao),
					Texto(instrucao, "errorCode"), Texto(instrucao, "errorMessage")));
				break;
			case "MOCK_CHILD_PROCESS":
				var filho = Texto(instrucao, "processDefinitionId")!;
				MocksDeElemento.FilhosPorProcesso(_cenario.Context)[filho] = new FilhoMock(filho, Variaveis(instrucao));
				break;
		}
	}

	// ---------- As instruções ----------

	private Task ExecutarAsync(JsonElement i) => _tipo switch
	{
		"CREATE_PROCESS_INSTANCE" => CriarAsync(i),
		"COMPLETE_JOB" => ResolverJobAsync(i, erro: false),
		"THROW_BPMN_ERROR_FROM_JOB" => ResolverJobAsync(i, erro: true),
		"COMPLETE_USER_TASK" => CompletarUserTaskAsync(i),
		"PUBLISH_MESSAGE" or "CORRELATE_MESSAGE" => CorrelacionarAsync(i),
		"UPDATE_VARIABLES" => AtualizarVariaveisAsync(i),
		"ASSERT_PROCESS_INSTANCE" => AfirmarInstanciaAsync(i),
		"ASSERT_ELEMENT_INSTANCE" => AfirmarElementosAsync(i, new[] { Objeto(i, "elementSelector") }, Texto(i, "state")!, Inteiro(i, "amount")),
		"ASSERT_ELEMENT_INSTANCES" => AfirmarElementosAsync(i, Lista(i, "elementSelectors"), Texto(i, "state")!, null),
		"ASSERT_VARIABLES" => AfirmarVariaveisAsync(i),
		"ASSERT_USER_TASK" => AfirmarUserTaskAsync(i),
		"ASSERT_PROCESS_INSTANCE_MESSAGE_SUBSCRIPTION" => AfirmarAssinaturaAsync(i),
		_ => Task.CompletedTask
	};

	private async Task CriarAsync(JsonElement i)
	{
		var processo = Texto(Objeto(i, "processDefinitionSelector"), "processDefinitionId")
			?? throw Falha("processDefinitionSelector.processDefinitionId is required.");

		CamundaVerbos.StartInstance(_cenario, processo, Variaveis(i), null, null);
		var execucao = await CamundaScenario.De(_cenario).ExecuteAsync().ConfigureAwait(false);
		var resultado = execucao.GetResult();
		if (!resultado.Success)
			throw Falha($"the instance of '{processo}' was not created: {string.Join(" ", resultado.Errors)}");

		_instancias.Add((processo, resultado.ProcessInstanceKey!,
			_cenario.Context.GetProperty<MocksDaInstancia>(MocksDaInstancia.ChaveNoContexto)));
	}

	private async Task ResolverJobAsync(JsonElement i, bool erro)
	{
		var seletor = Objeto(i, "jobSelector");
		var tipo = Texto(seletor, "jobType");
		var elemento = Texto(seletor, "elementId");
		var processo = Texto(seletor, "processDefinitionId");

		var job = await EsperarAsync($"a job {Descrever(("type", tipo), ("element", elemento), ("process", processo))}", async ct =>
		{
			var instancias = await InstanciasAsync(processo, ct).ConfigureAwait(false);
			var jobs = await _gateway.SearchCreatedJobsAsync(instancias, tipo is null ? null : new[] { tipo }, ct).ConfigureAwait(false);
			var achado = jobs.FirstOrDefault(j => elemento is null || j.ElementId == elemento);
			return (achado, achado is null ? "no such job is waiting" : null);
		}).ConfigureAwait(false);

		if (erro)
			await _gateway.ThrowBpmnErrorAsync(job.JobKey, Texto(i, "errorCode")!, Texto(i, "errorMessage"), Variaveis(i), CancellationToken.None)
				.ConfigureAwait(false);
		else
			await _gateway.CompleteJobAsync(job.JobKey, Variaveis(i), CancellationToken.None).ConfigureAwait(false);
	}

	private async Task CompletarUserTaskAsync(JsonElement i)
	{
		var seletor = Objeto(i, "userTaskSelector");
		var processo = Texto(seletor, "processDefinitionId");

		var tarefa = await EsperarAsync($"a user task {DescreverTarefa(seletor)}", async ct =>
		{
			var tarefas = await _gateway.SearchUserTasksAsync(await InstanciasAsync(processo, ct).ConfigureAwait(false), ct).ConfigureAwait(false);
			var achada = tarefas.FirstOrDefault(t => t.State == "CREATED" && Casa(t, seletor));
			return (achada, achada is null ? "no such user task is waiting" : null);
		}).ConfigureAwait(false);

		await _gateway.CompleteUserTaskAsync(tarefa.UserTaskKey, Variaveis(i), CancellationToken.None).ConfigureAwait(false);
	}

	// A chave vem do caso, e não da assinatura: confere antes que só as instâncias do caso esperam a
	// mensagem com essa chave, porque o Camunda a entrega a quem assinou primeiro no cluster inteiro.
	private async Task CorrelacionarAsync(JsonElement i)
	{
		var nome = Texto(i, "name")!;
		var chave = Texto(i, "correlationKey") ?? string.Empty;

		await EsperarAsync($"message '{nome}' with correlation key '{chave}' to be correlated", async ct =>
		{
			var nossas = await InstanciasAsync(null, ct).ConfigureAwait(false);
			var assinaturas = await _gateway.SearchOpenMessageSubscriptionsAsync(null, new[] { nome }, chave, ct).ConfigureAwait(false);
			var deFora = assinaturas.Where(a => !nossas.Contains(a.ProcessInstanceKey)).Select(a => a.ProcessInstanceKey).Distinct().ToList();
			if (deFora.Count > 0)
				throw Falha($"instance {string.Join(", ", deFora)}, outside this test case, also awaits message '{nome}' with key " +
					$"'{chave}', and Camunda delivers it to whoever subscribed first. Use a correlation key unique to the test case.");
			if (assinaturas.Count == 0)
				return (false, "no instance of this test case awaits it yet");

			try
			{
				await _gateway.CorrelateMessageAsync(nome, chave, Variaveis(i), _settings.TenantId, ct).ConfigureAwait(false);
				return (true, null);
			}
			catch (CamundaRecusouException e) when (e.Codigo == "NOT_FOUND")
			{
				return (false, "the subscription closed before the message arrived");
			}
		}).ConfigureAwait(false);
	}

	private async Task AtualizarVariaveisAsync(JsonElement i)
	{
		var chave = await InstanciaAsync(Texto(Objeto(i, "processInstanceSelector"), "processDefinitionId")).ConfigureAwait(false);
		await _gateway.SetVariablesAsync(chave, Variaveis(i) ?? new Dictionary<string, object?>(), CancellationToken.None).ConfigureAwait(false);
	}

	// ---------- As asserções ----------

	private async Task AfirmarInstanciaAsync(JsonElement i)
	{
		var processo = Texto(Objeto(i, "processInstanceSelector"), "processDefinitionId");
		var estado = Texto(i, "state");
		var incidentes = i.TryGetProperty("hasActiveIncidents", out var h) && h.ValueKind is JsonValueKind.True or JsonValueKind.False
			? h.GetBoolean()
			: (bool?)null;

		await EsperarAsync($"the instance of '{processo ?? "the test case"}' {Esperado(estado, incidentes)}", async ct =>
		{
			var chave = await InstanciaAsync(processo, ct).ConfigureAwait(false);
			var instancia = await _gateway.GetInstanceAsync(chave, ct).ConfigureAwait(false);
			if (instancia is null) return (false, "it has not shown up in the search yet");

			var esperado = estado switch
			{
				"IS_ACTIVE" => "ACTIVE",
				"IS_COMPLETED" => "COMPLETED",
				"IS_TERMINATED" => "TERMINATED",
				_ => null
			};
			var estadoOk = esperado is null || instancia.State == esperado;
			var incidentesOk = incidentes is null || instancia.HasIncident == incidentes;
			if (estadoOk && incidentesOk) return (true, null);

			var situacao = $"it is {instancia.State}{(instancia.HasIncident ? " with an open incident" : "")}";
			if (!estadoOk && instancia.State is "COMPLETED" or "TERMINATED")
				throw Falha($"expected the instance of '{processo}' {Esperado(estado, incidentes)}, but {situacao}.");
			return (false, situacao);
		}).ConfigureAwait(false);
	}

	private async Task AfirmarElementosAsync(JsonElement i, IReadOnlyList<JsonElement> seletores, string estado, int? quantidade)
	{
		var processo = Texto(Objeto(i, "processInstanceSelector"), "processDefinitionId");
		var nomes = seletores.Select(DescreverElemento).ToList();

		await EsperarAsync($"{string.Join(", ", nomes)} {estado.ToLowerInvariant().Replace('_', ' ')}" +
			(quantidade is null ? "" : $" {quantidade} time(s)"), async ct =>
		{
			var chave = await InstanciaAsync(processo, ct).ConfigureAwait(false);
			var elementos = await _gateway.SearchElementsAsync(chave, ct).ConfigureAwait(false);
			var ids = await IdsAsync(chave, seletores, ct).ConfigureAwait(false);

			int Contar(string id, string? noEstado) => elementos.Count(e => e.ElementId == id && (noEstado is null || e.State == noEstado));

			string? falta = estado switch
			{
				"IS_ACTIVE" or "IS_COMPLETED" or "IS_TERMINATED" => ids
					.Where(id => quantidade is int n ? Contar(id, Estado(estado)) != n : Contar(id, Estado(estado)) == 0)
					.Select(id => $"'{id}' is {Situacao(elementos, id)}")
					.FirstOrDefault(),
				"IS_NOT_ACTIVE" => ids.Where(id => Contar(id, "ACTIVE") > 0).Select(id => $"'{id}' is active").FirstOrDefault(),
				"IS_NOT_ACTIVATED" => ids.Where(id => Contar(id, null) > 0).Select(id => $"'{id}' was activated").FirstOrDefault(),
				"IS_ACTIVE_EXACTLY" => AtivosExatamente(elementos, ids),
				"IS_COMPLETED_IN_ORDER" => EmOrdem(elementos, ids),
				_ => $"state {estado} is not supported"
			};

			return (falta is null, falta);
		}).ConfigureAwait(false);
	}

	private async Task AfirmarVariaveisAsync(JsonElement i)
	{
		var processo = Texto(Objeto(i, "processInstanceSelector"), "processDefinitionId");
		var nomes = Lista(i, "variableNames").Select(n => n.GetString()!).ToList();
		var valores = i.TryGetProperty("variables", out var v) && v.ValueKind == JsonValueKind.Object
			? v.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.Clone())
			: new Dictionary<string, JsonElement>();

		await EsperarAsync($"variables {string.Join(", ", nomes.Concat(valores.Keys).Distinct())}", async ct =>
		{
			var chave = await InstanciaAsync(processo, ct).ConfigureAwait(false);
			var todas = (await _gateway.SearchVariablesAsync(chave, null, ct).ConfigureAwait(false))
				.Where(x => x.ScopeKey == chave)
				.ToDictionary(x => x.Name, x => x.ValueJson);

			var faltando = nomes.Concat(valores.Keys).FirstOrDefault(n => !todas.ContainsKey(n));
			if (faltando is not null) return (false, $"'{faltando}' does not exist");

			foreach (var (nome, esperado) in valores)
			{
				if (!JsonNode.DeepEquals(JsonNode.Parse(todas[nome]), JsonNode.Parse(esperado.GetRawText())))
					return (false, $"'{nome}' is {todas[nome]}, not {esperado.GetRawText()}");
			}

			return (true, null);
		}).ConfigureAwait(false);
	}

	private async Task AfirmarUserTaskAsync(JsonElement i)
	{
		var seletor = Objeto(i, "userTaskSelector");
		var processo = Texto(seletor, "processDefinitionId");
		var estado = Texto(i, "state");

		await EsperarAsync($"a user task {DescreverTarefa(seletor)} to match", async ct =>
		{
			var tarefas = (await _gateway.SearchUserTasksAsync(await InstanciasAsync(processo, ct).ConfigureAwait(false), ct).ConfigureAwait(false))
				.Where(t => Casa(t, seletor))
				.ToList();
			if (tarefas.Count == 0) return (false, "no such user task exists yet");

			var diferencas = tarefas.Select(t => Diferencas(t, i, estado)).ToList();
			return diferencas.Any(d => d is null) ? (true, null) : (false, diferencas.First());
		}).ConfigureAwait(false);
	}

	private async Task AfirmarAssinaturaAsync(JsonElement i)
	{
		var processo = Texto(Objeto(i, "processInstanceSelector"), "processDefinitionId");
		var mensagem = Objeto(i, "messageSelector");
		var nome = Texto(mensagem, "messageName")!;
		var chaveDeCorrelacao = Texto(mensagem, "correlationKey");
		var esperando = Texto(i, "state") == "IS_WAITING";

		await EsperarAsync($"the instance {(esperando ? "to wait" : "not to wait")} for message '{nome}'", async ct =>
		{
			var chave = await InstanciaAsync(processo, ct).ConfigureAwait(false);
			var abertas = await _gateway.SearchOpenMessageSubscriptionsAsync(new[] { chave }, new[] { nome }, chaveDeCorrelacao, ct)
				.ConfigureAwait(false);
			return abertas.Count > 0 == esperando ? (true, null) : (false, esperando ? "it does not wait for it" : "it waits for it");
		}).ConfigureAwait(false);
	}

	// ---------- Apoio ----------

	// Pergunta até o prazo das asserções; uma falha dentro de verificar (ShouldAssertException) para na hora.
	private async Task<T> EsperarAsync<T>(string oQue, Func<CancellationToken, Task<(T? Valor, string? Situacao)>> verificar)
	{
		var prazo = TimeSpan.FromSeconds(Math.Max(1, _settings.AssertionTimeoutSeconds));
		var intervalo = TimeSpan.FromMilliseconds(Math.Max(10, _settings.PollIntervalMilliseconds));
		var cronometro = Stopwatch.StartNew();
		var ultima = "it was never checked";

		while (true)
		{
			var (valor, situacao) = await verificar(CancellationToken.None).ConfigureAwait(false);
			if (situacao is null && valor is not null) return valor;

			ultima = situacao ?? ultima;
			if (cronometro.Elapsed >= prazo)
				throw Falha($"expected {oQue} within {prazo.TotalSeconds:0.#}s, but {ultima}.");

			await Task.Delay(intervalo).ConfigureAwait(false);
		}
	}

	private Task EsperarAsync(string oQue, Func<CancellationToken, Task<(bool Ok, string? Situacao)>> verificar) =>
		EsperarAsync<object>(oQue, async ct =>
		{
			var (ok, situacao) = await verificar(ct).ConfigureAwait(false);
			return ok ? (new object(), null) : (null, situacao);
		});

	// As instâncias do caso: as que ele criou e as filhas delas, do processo pedido ou de todos.
	private Task<IReadOnlyCollection<string>> InstanciasAsync(string? processo, CancellationToken ct)
	{
		var todas = new List<string>();
		foreach (var (raiz, chave, mocks) in _instancias)
		{
			if (processo is null || raiz == processo) todas.Add(chave);
			if (mocks is null) continue;
			todas.AddRange(mocks.FilhasVistas.Where(f => processo is null || f.ProcessId == processo).Select(f => f.ProcessInstanceKey));
			if (processo is null) todas.AddRange(mocks.Instancias);
		}

		return Task.FromResult<IReadOnlyCollection<string>>(todas.Distinct().ToList());
	}

	private async Task<string> InstanciaAsync(string? processo, CancellationToken ct = default)
	{
		if (_instancias.Count == 0)
			throw Falha("no instance was created before this instruction. Add a CREATE_PROCESS_INSTANCE first.");
		if (processo is null)
			return _instancias[^1].Chave;

		var raiz = _instancias.LastOrDefault(x => x.ProcessId == processo);
		if (raiz.Chave is not null)
			return raiz.Chave;

		// O processo de uma filha: espera o laço a descobrir.
		var filha = await EsperarAsync($"an instance of '{processo}' called by this test case", async _ =>
		{
			var chaves = await InstanciasAsync(processo, ct).ConfigureAwait(false);
			var chave = chaves.FirstOrDefault();
			return (chave, chave is null ? "none showed up" : null);
		}).ConfigureAwait(false);
		return filha;
	}

	private async Task<IReadOnlyList<string>> IdsAsync(string instancia, IReadOnlyList<JsonElement> seletores, CancellationToken ct)
	{
		var ids = new List<string>();
		ModeloBpmn? modelo = null;
		foreach (var seletor in seletores)
		{
			if (Texto(seletor, "elementId") is { } id)
			{
				ids.Add(id);
				continue;
			}

			var nome = Texto(seletor, "elementName");
			if (modelo is null)
			{
				var vista = await _gateway.GetInstanceAsync(instancia, ct).ConfigureAwait(false);
				modelo = vista?.ProcessDefinitionKey is null
					? null
					: ModeloBpmn.Ler(await _gateway.GetProcessDefinitionXmlAsync(vista.ProcessDefinitionKey, ct).ConfigureAwait(false));
			}

			ids.Add(modelo?.Nomes.FirstOrDefault(n => n.Value == nome).Key
				?? throw Falha($"no element of the process is named '{nome}'."));
		}

		return ids;
	}

	private static string? AtivosExatamente(IReadOnlyList<ElementoVisto> elementos, IReadOnlyList<string> ids)
	{
		var ativos = elementos.Where(e => e.State == "ACTIVE" && e.Type != "PROCESS").Select(e => e.ElementId).ToHashSet();
		var falta = ids.FirstOrDefault(id => !ativos.Contains(id));
		if (falta is not null) return $"'{falta}' is not active";
		var sobra = ativos.FirstOrDefault(id => !ids.Contains(id));
		return sobra is null ? null : $"'{sobra}' is active too";
	}

	private static string? EmOrdem(IReadOnlyList<ElementoVisto> elementos, IReadOnlyList<string> ids)
	{
		var completados = elementos.Where(e => e.State == "COMPLETED").OrderBy(e => e.EndDate).Select(e => e.ElementId).ToList();
		var posicao = -1;
		foreach (var id in ids)
		{
			var proxima = completados.IndexOf(id, posicao + 1);
			if (proxima < 0)
				return completados.Contains(id) ? $"'{id}' did not complete after the elements before it" : $"'{id}' is not completed";
			posicao = proxima;
		}

		return null;
	}

	private static string? Diferencas(UserTaskVista t, JsonElement i, string? estado)
	{
		var esperado = estado switch
		{
			"IS_CREATED" => "CREATED",
			"IS_COMPLETED" => "COMPLETED",
			"IS_CANCELED" => "CANCELED",
			"IS_FAILED" => "FAILED",
			_ => null
		};
		if (esperado is not null && t.State != esperado) return $"'{t.ElementId}' is {t.State}";
		if (Texto(i, "assignee") is { } assignee && t.Assignee != assignee) return $"'{t.ElementId}' is assigned to '{t.Assignee}'";
		if (Texto(i, "name") is { } nome && t.Name != nome) return $"'{t.ElementId}' is named '{t.Name}'";
		if (Inteiro(i, "priority") is int prioridade && t.Priority != prioridade) return $"'{t.ElementId}' has priority {t.Priority}";
		var grupos = Lista(i, "candidateGroups").Select(g => g.GetString()).ToList();
		if (grupos.Count > 0 && !grupos.All(g => t.CandidateGroups.Contains(g!)))
			return $"'{t.ElementId}' has candidate groups {string.Join(", ", t.CandidateGroups)}";
		return null;
	}

	private static bool Casa(UserTaskVista t, JsonElement seletor) =>
		(Texto(seletor, "elementId") is not { } id || t.ElementId == id) &&
		(Texto(seletor, "taskName") is not { } nome || t.Name == nome);

	private static string Situacao(IReadOnlyList<ElementoVisto> elementos, string id)
	{
		var estados = elementos.Where(e => e.ElementId == id).Select(e => e.State.ToLowerInvariant()).ToList();
		return estados.Count == 0 ? "not activated" : string.Join(", ", estados);
	}

	private static string? Estado(string estado) => estado switch
	{
		"IS_ACTIVE" => "ACTIVE",
		"IS_COMPLETED" => "COMPLETED",
		"IS_TERMINATED" => "TERMINATED",
		_ => null
	};

	private static string Esperado(string? estado, bool? incidentes) =>
		string.Join(" and ", new[]
		{
			estado is null ? null : "to be " + estado.Replace("IS_", "").ToLowerInvariant(),
			incidentes is null ? null : incidentes.Value ? "to have an open incident" : "to have no open incident"
		}.Where(x => x is not null));

	private ShouldAssertException Falha(string mensagem)
	{
		var texto = new StringBuilder($"Test case '{_caso.Name}', instruction #{_passo + 1} {_tipo}: {mensagem}");
		var endereco = _settings.RestAddress ?? Environment.GetEnvironmentVariable("CAMUNDA_REST_ADDRESS");
		foreach (var (_, chave, _) in _instancias)
			if (!string.IsNullOrWhiteSpace(endereco))
				texto.AppendLine().Append("  Operate: ").Append(endereco!.TrimEnd('/')).Append("/operate/processes/").Append(chave);
		return new ShouldAssertException(texto.ToString());
	}

	private static string Tipo(JsonElement instrucao) => Texto(instrucao, "type") ?? "(no type)";

	private static string? Texto(JsonElement elemento, string nome) =>
		elemento.ValueKind == JsonValueKind.Object && elemento.TryGetProperty(nome, out var valor) && valor.ValueKind == JsonValueKind.String
			? valor.GetString()
			: null;

	private static int? Inteiro(JsonElement elemento, string nome) =>
		elemento.ValueKind == JsonValueKind.Object && elemento.TryGetProperty(nome, out var valor) && valor.ValueKind == JsonValueKind.Number
			? valor.GetInt32()
			: null;

	private static bool Tem(JsonElement elemento, string nome) =>
		elemento.TryGetProperty(nome, out var valor) && valor.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined) &&
		!(valor.ValueKind == JsonValueKind.Array && valor.GetArrayLength() == 0);

	private static bool Verdadeiro(JsonElement elemento, string nome) =>
		elemento.TryGetProperty(nome, out var valor) && valor.ValueKind == JsonValueKind.True;

	private static JsonElement Objeto(JsonElement elemento, string nome) =>
		elemento.ValueKind == JsonValueKind.Object && elemento.TryGetProperty(nome, out var valor) ? valor : default;

	private static IReadOnlyList<JsonElement> Lista(JsonElement elemento, string nome) =>
		elemento.ValueKind == JsonValueKind.Object && elemento.TryGetProperty(nome, out var valor) && valor.ValueKind == JsonValueKind.Array
			? valor.EnumerateArray().ToList()
			: Array.Empty<JsonElement>();

	// As variáveis do JSON viram um dicionário de JsonElement: o SDK as serializa como vieram.
	private static Dictionary<string, object?>? Variaveis(JsonElement instrucao) =>
		instrucao.TryGetProperty("variables", out var v) && v.ValueKind == JsonValueKind.Object
			? v.EnumerateObject().ToDictionary(p => p.Name, p => (object?)p.Value.Clone())
			: null;

	private static string Descrever(params (string Nome, string? Valor)[] partes) =>
		string.Join(", ", partes.Where(p => p.Valor is not null).Select(p => $"{p.Nome} '{p.Valor}'"));

	private static string DescreverTarefa(JsonElement seletor) =>
		Descrever(("element", Texto(seletor, "elementId")), ("name", Texto(seletor, "taskName")), ("process", Texto(seletor, "processDefinitionId")));

	private static string DescreverElemento(JsonElement seletor) =>
		Texto(seletor, "elementId") is { } id ? $"'{id}'" : $"the element named '{Texto(seletor, "elementName")}'";
}
