using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

using XUnitAssured.Camunda;
using XUnitAssured.Camunda.Gateway;
using XUnitAssured.Camunda.Results;
using XUnitAssured.Core.Abstractions;
using XUnitAssured.Core.DSL;

namespace XUnitAssured.Tests.CamundaTests;

/// <summary>
/// Um Camunda em memória, do tamanho do que o pacote pergunta.
/// </summary>
/// <remarks>
/// <para>
/// Não simula BPMN. O teste diz o que existe (instâncias, jobs, elementos) e o que acontece
/// quando um job é resolvido (<see cref="AoCompletar"/>, <see cref="AoLancarErro"/>). O padrão
/// é o caso comum: resolvido o último job da instância, ela completa.
/// </para>
/// <para>
/// Responde como o cluster de verdade nos pontos que o pacote trata: job inexistente é
/// <c>NOT_FOUND</c>, job com lease de outro worker é <c>INVALID_STATE</c>.
/// </para>
/// </remarks>
internal sealed class ClusterFalso : ICamundaGateway
{
	private readonly object _trava = new();
	private readonly Dictionary<string, Instancia> _instancias = new();
	private readonly List<Job> _jobs = new();
	private readonly List<DefinicaoVista> _definicoes = new();
	private readonly List<UserTaskFalsa> _userTasks = new();
	private readonly List<AssinaturaFalsa> _assinaturas = new();
	private readonly Dictionary<string, string> _xmls = new();
	private int _sequencia;

	/// <summary>O que acontece quando um job é completado. Padrão: completar a instância se não sobrou job.</summary>
	public Action<ClusterFalso, Job>? AoCompletar { get; set; }

	/// <summary>O que acontece quando um erro BPMN é lançado num job. Padrão: a instância completa.</summary>
	public Action<ClusterFalso, Job, string>? AoLancarErro { get; set; }

	/// <summary>O que acontece quando uma instância é criada (os jobs que ela abre).</summary>
	public Action<ClusterFalso, string>? AoCriarInstancia { get; set; }

	/// <summary>Quantas buscas de job falham com erro de rede antes de voltar a responder.</summary>
	public int BuscasDeJobQueFalham { get; set; }

	public List<string> Comandos { get; } = new();

	public List<string> Canceladas { get; } = new();

	public static CamundaSettings Rapido(int prazoSegundos = 1, int vidaDosMocksSegundos = 30) => new()
	{
		RestAddress = "http://camunda.falso:8080",
		AssertionTimeoutSeconds = prazoSegundos,
		PollIntervalMilliseconds = 10,
		MockLifetimeSeconds = vidaDosMocksSegundos
	};

	/// <summary>Uma cadeia nova, já apontada para este cluster e com prazos curtos.</summary>
	public ITestScenario Cenario(CamundaSettings? settings = null)
	{
		var cenario = ScenarioDsl.Given();
		cenario.Context.SetProperty(Gateways.ChaveNoContexto, (ICamundaGateway)this);
		cenario.Context.SetProperty(CamundaSettings.ChaveNoContexto, settings ?? Rapido());
		return cenario;
	}

	/// <summary>Uma versão já implantada, como a homologação a teria. Devolve a chave da definição.</summary>
	public string Publicar(string processId, int versao, string? rotulo = null)
	{
		lock (_trava)
		{
			var chave = $"def-{processId}-v{versao}";
			_definicoes.Add(new DefinicaoVista(processId, chave, versao, rotulo));
			return chave;
		}
	}

	/// <param name="chamadaNoPai">A call activity do pai que iniciou esta instância, como a busca a mostra.</param>
	public string NovaInstancia(string processId, string? pai = null, string? chamadaNoPai = null, string? definicao = null)
	{
		lock (_trava)
		{
			var chave = $"pi-{++_sequencia}";
			_instancias[chave] = new Instancia(chave, processId, pai) { ChamadaNoPai = chamadaNoPai, Definicao = definicao };
			if (pai is not null && chamadaNoPai is not null)
				_instancias[pai].Elementos[chamadaNoPai] = "ACTIVE";
			return chave;
		}
	}

	public string NovoJob(string instancia, string tipo, string elemento, bool comLeaseDeOutroWorker = false)
	{
		lock (_trava)
		{
			var chave = $"job-{++_sequencia}";
			_jobs.Add(new Job(chave, tipo, elemento, instancia) { ComLease = comLeaseDeOutroWorker });
			_instancias[instancia].Elementos[elemento] = "ACTIVE";
			return chave;
		}
	}

	public void Estado(string instancia, string estado, bool comIncident = false)
	{
		lock (_trava)
		{
			_instancias[instancia].Estado = estado;
			_instancias[instancia].ComIncident = comIncident;
		}
	}

	public void Variavel(string instancia, string nome, object? valor)
	{
		lock (_trava) _instancias[instancia].Variaveis[nome] = JsonSerializer.Serialize(valor);
	}

	public void Elemento(string instancia, string elemento, string estado)
	{
		lock (_trava) _instancias[instancia].Elementos[elemento] = estado;
	}

	public string EstadoDe(string instancia)
	{
		lock (_trava) return _instancias[instancia].Estado;
	}

	public string? JobEstado(string chave)
	{
		lock (_trava) return _jobs.FirstOrDefault(j => j.Chave == chave)?.Estado;
	}

	// ----- ICamundaGateway -----

	public Task<IReadOnlyList<DeployedProcess>> DeployAsync(IReadOnlyList<RecursoBpmn> recursos, CancellationToken ct)
	{
		XNamespace bpmn = "http://www.omg.org/spec/BPMN/20100524/MODEL";

		var processos = recursos
			.SelectMany(r => XDocument.Parse(System.Text.Encoding.UTF8.GetString(r.Conteudo))
				.Descendants(bpmn + "process")
				.Select(p => new DeployedProcess((string)p.Attribute("id")!, $"def-{(string)p.Attribute("id")!}", 1, r.Nome)))
			.ToList();

		lock (_trava) Comandos.Add("deploy " + string.Join(",", recursos.Select(r => r.Nome)));
		return Task.FromResult<IReadOnlyList<DeployedProcess>>(processos);
	}

	public Task<IReadOnlyList<DefinicaoVista>> SearchProcessDefinitionsAsync(
		string processId, int? version, string? versionTag, bool onlyLatest, string? tenantId, CancellationToken ct)
	{
		lock (_trava)
		{
			Comandos.Add($"search-definitions {processId}");

			var doProcesso = _definicoes.Where(d => d.ProcessId == processId).ToList();
			IReadOnlyList<DefinicaoVista> achadas = doProcesso
				.Where(d => version is null || d.Version == version)
				.Where(d => versionTag is null || d.VersionTag == versionTag)
				.Where(d => !onlyLatest || version is not null || versionTag is not null || d.Version == doProcesso.Max(x => x.Version))
				.ToList();

			return Task.FromResult(achadas);
		}
	}

	public Task<string> CreateInstanceAsync(string definitionKey, object? variables, string? tenantId, CancellationToken ct)
	{
		string processId;
		lock (_trava)
			processId = _definicoes.FirstOrDefault(d => d.DefinitionKey == definitionKey)?.ProcessId ?? definitionKey.Replace("def-", "");
		var chave = NovaInstancia(processId);
		lock (_trava) _instancias[chave].Definicao = definitionKey;

		if (variables is not null)
		{
			var json = JsonSerializer.SerializeToElement(variables);
			foreach (var propriedade in json.EnumerateObject())
				lock (_trava) _instancias[chave].Variaveis[propriedade.Name] = propriedade.Value.GetRawText();
		}

		lock (_trava) Comandos.Add($"create {definitionKey}");
		AoCriarInstancia?.Invoke(this, chave);
		return Task.FromResult(chave);
	}

	public Task<IReadOnlyList<JobPendente>> SearchCreatedJobsAsync(
		IReadOnlyCollection<string> processInstanceKeys, IReadOnlyCollection<string>? types, CancellationToken ct)
	{
		lock (_trava)
		{
			if (BuscasDeJobQueFalham > 0)
			{
				BuscasDeJobQueFalham--;
				throw new System.Net.Http.HttpRequestException("connection reset (simulated)");
			}

			IReadOnlyList<JobPendente> achados = _jobs
				.Where(j => j.Estado == "CREATED" && processInstanceKeys.Contains(j.Instancia))
				.Where(j => types is null || types.Contains(j.Tipo))
				.Select(j => new JobPendente(j.Chave, j.Tipo, j.Elemento, j.Instancia))
				.ToList();

			return Task.FromResult(achados);
		}
	}

	public Task CompleteJobAsync(string jobKey, object? variables, CancellationToken ct)
	{
		Job job;
		lock (_trava)
		{
			job = Exigir(jobKey);
			job.Estado = "COMPLETED";
			_instancias[job.Instancia].Elementos[job.Elemento] = "COMPLETED";

			if (variables is not null)
			{
				foreach (var propriedade in JsonSerializer.SerializeToElement(variables).EnumerateObject())
					_instancias[job.Instancia].Variaveis[propriedade.Name] = propriedade.Value.GetRawText();
			}

			Comandos.Add($"complete {jobKey}");
		}

		if (AoCompletar is not null)
			AoCompletar(this, job);
		else
			CompletarSeNaoSobrouJob(job.Instancia);

		return Task.CompletedTask;
	}

	public Task ThrowBpmnErrorAsync(string jobKey, string errorCode, string? errorMessage, object? variables, CancellationToken ct)
	{
		Job job;
		lock (_trava)
		{
			job = Exigir(jobKey);
			job.Estado = "ERROR_THROWN";
			_instancias[job.Instancia].Elementos[job.Elemento] = "TERMINATED";
			Comandos.Add($"error {jobKey} {errorCode}");
		}

		if (AoLancarErro is not null)
			AoLancarErro(this, job, errorCode);
		else
			Estado(job.Instancia, "COMPLETED");

		return Task.CompletedTask;
	}

	public Task FailJobAsync(string jobKey, string errorMessage, CancellationToken ct)
	{
		lock (_trava)
		{
			var job = Exigir(jobKey);
			job.Estado = "FAILED";
			var instancia = _instancias[job.Instancia];
			instancia.Incidentes.Add(new IncidenteVisto(job.Elemento, "JOB_NO_RETRIES", errorMessage));
			instancia.ComIncident = true;
			Comandos.Add($"fail {jobKey}");
		}

		return Task.CompletedTask;
	}

	public Task<IReadOnlyList<InstanciaVista>> SearchChildInstancesAsync(IReadOnlyCollection<string> parentKeys, CancellationToken ct)
	{
		lock (_trava)
		{
			IReadOnlyList<InstanciaVista> filhas = _instancias.Values
				.Where(i => i.Pai is not null && parentKeys.Contains(i.Pai))
				.Select(Vista)
				.ToList();
			return Task.FromResult(filhas);
		}
	}

	public Task<InstanciaVista?> GetInstanceAsync(string processInstanceKey, CancellationToken ct)
	{
		lock (_trava)
			return Task.FromResult(_instancias.TryGetValue(processInstanceKey, out var i) ? Vista(i) : null);
	}

	public Task<IReadOnlyList<ElementoVisto>> SearchElementsAsync(string processInstanceKey, CancellationToken ct)
	{
		lock (_trava)
		{
			IReadOnlyList<ElementoVisto> elementos = _instancias[processInstanceKey].Elementos
				.Select(e => new ElementoVisto(e.Key, e.Key, "SERVICE_TASK", e.Value, $"{processInstanceKey}:{e.Key}"))
				.ToList();
			return Task.FromResult(elementos);
		}
	}

	public Task<IReadOnlyList<VariavelVista>> SearchVariablesAsync(string processInstanceKey, string? name, CancellationToken ct)
	{
		lock (_trava)
		{
			IReadOnlyList<VariavelVista> variaveis = _instancias[processInstanceKey].Variaveis
				.Where(v => name is null || v.Key == name)
				.Select(v => new VariavelVista(v.Key, v.Value, processInstanceKey))
				.ToList();
			return Task.FromResult(variaveis);
		}
	}

	public Task<IReadOnlyList<IncidenteVisto>> SearchActiveIncidentsAsync(string processInstanceKey, CancellationToken ct)
	{
		lock (_trava)
		{
			var instancia = _instancias[processInstanceKey];
			IReadOnlyList<IncidenteVisto> incidentes = instancia.Incidentes.Count > 0
				? instancia.Incidentes.ToList()
				: instancia.ComIncident
					? new[] { new IncidenteVisto("cobrar", "JOB_NO_RETRIES", "simulated incident") }
					: Array.Empty<IncidenteVisto>();
			return Task.FromResult(incidentes);
		}
	}

	public Task CancelInstanceAsync(string processInstanceKey, CancellationToken ct)
	{
		Estado(processInstanceKey, "TERMINATED");
		lock (_trava) Canceladas.Add(processInstanceKey);
		return Task.CompletedTask;
	}

	// ----- user tasks, esperas, modificação, BPMN -----

	/// <summary>O que acontece quando uma user task é completada. Padrão: completar a instância se nada sobrou.</summary>
	public Action<ClusterFalso, UserTaskFalsa>? AoCompletarUserTask { get; set; }

	/// <summary>O que acontece depois de uma modificação. Padrão: completar a instância se nada sobrou ativo.</summary>
	public Action<ClusterFalso, string>? AoModificar { get; set; }

	public List<string> Modificacoes { get; } = new();

	public string NovaUserTask(string instancia, string elemento)
	{
		lock (_trava)
		{
			var chave = $"task-{++_sequencia}";
			_userTasks.Add(new UserTaskFalsa(chave, instancia, elemento));
			_instancias[instancia].Elementos[elemento] = "ACTIVE";
			return chave;
		}
	}

	/// <summary>O BPMN de uma definição, para o pacote ler a estrutura do processo.</summary>
	public void DefinirBpmn(string definitionKey, string xml)
	{
		lock (_trava) _xmls[definitionKey] = xml;
	}

	public Task<IReadOnlyList<EsperaVista>> SearchWaitStatesAsync(string rootProcessInstanceKey, CancellationToken ct)
	{
		lock (_trava)
		{
			var arvore = Arvore(rootProcessInstanceKey);
			IReadOnlyList<EsperaVista> esperas = _userTasks
				.Where(t => t.Aberta && arvore.Contains(t.Instancia))
				.Select(t => new EsperaVista(t.Instancia, $"{t.Instancia}:{t.Elemento}", t.Elemento, "USER_TASK", UserTaskKey: t.Chave))
				.Concat(_assinaturas
					.Where(a => a.Aberta && arvore.Contains(a.Instancia))
					.Select(a => new EsperaVista(a.Instancia, $"{a.Instancia}:{a.Elemento}", a.Elemento, "MESSAGE", MessageName: a.Nome, CorrelationKey: a.Chave)))
				.ToList();
			return Task.FromResult(esperas);
		}
	}

	public Task CompleteUserTaskAsync(string userTaskKey, object? variables, CancellationToken ct)
	{
		UserTaskFalsa task;
		lock (_trava)
		{
			task = _userTasks.FirstOrDefault(t => t.Chave == userTaskKey && t.Aberta)
				?? throw new CamundaRecusouException("NOT_FOUND", $"User task {userTaskKey} not found.");
			task.Aberta = false;
			_instancias[task.Instancia].Elementos[task.Elemento] = "COMPLETED";

			if (variables is not null)
			{
				foreach (var propriedade in JsonSerializer.SerializeToElement(variables).EnumerateObject())
					_instancias[task.Instancia].Variaveis[propriedade.Name] = propriedade.Value.GetRawText();
			}

			Comandos.Add($"complete-user-task {userTaskKey}");
		}

		if (AoCompletarUserTask is not null)
			AoCompletarUserTask(this, task);
		else
			CompletarSeNaoSobrouJob(task.Instancia);

		return Task.CompletedTask;
	}

	public Task ModifyInstanceAsync(
		string processInstanceKey,
		IReadOnlyList<string> terminateElementInstanceKeys,
		IReadOnlyList<string> activateElementIds,
		string? ancestorElementInstanceKey,
		object? variables,
		CancellationToken ct)
	{
		lock (_trava)
		{
			var instancia = _instancias[processInstanceKey];
			if (variables is not null)
			{
				foreach (var propriedade in JsonSerializer.SerializeToElement(variables).EnumerateObject())
					instancia.Variaveis[propriedade.Name] = propriedade.Value.GetRawText();
			}
			foreach (var chave in terminateElementInstanceKeys)
				instancia.Elementos[chave.Substring(chave.IndexOf(':') + 1)] = "TERMINATED";
			foreach (var id in activateElementIds)
				instancia.Elementos[id] = "COMPLETED";

			Modificacoes.Add(
				$"{processInstanceKey} -[{string.Join(",", terminateElementInstanceKeys)}] +[{string.Join(",", activateElementIds)}]" +
				(ancestorElementInstanceKey is null ? "" : $" in {ancestorElementInstanceKey}"));
		}

		if (AoModificar is not null)
		{
			AoModificar(this, processInstanceKey);
		}
		else
		{
			lock (_trava)
			{
				if (!_instancias[processInstanceKey].Elementos.Values.Contains("ACTIVE"))
					_instancias[processInstanceKey].Estado = "COMPLETED";
			}
		}

		return Task.CompletedTask;
	}

	public Task<string> GetProcessDefinitionXmlAsync(string definitionKey, CancellationToken ct)
	{
		lock (_trava)
			return _xmls.TryGetValue(definitionKey, out var xml)
				? Task.FromResult(xml)
				: throw new CamundaRecusouException("NOT_FOUND", $"Process definition {definitionKey} not found.");
	}

	private HashSet<string> Arvore(string raiz)
	{
		var arvore = new HashSet<string> { raiz };
		bool cresceu;
		do
		{
			cresceu = false;
			foreach (var i in _instancias.Values.Where(i => i.Pai is not null && arvore.Contains(i.Pai) && !arvore.Contains(i.Chave)))
				cresceu |= arvore.Add(i.Chave);
		}
		while (cresceu);

		return arvore;
	}

	// ----- instâncias iniciadas por outro sistema -----

	// A mais nova primeiro, como a busca ordenada pela data de início: aqui, pela sequência das chaves.
	public Task<IReadOnlyList<InstanciaVista>> SearchInstancesByVariableAsync(
		string processId, string variableName, string valueJson, CancellationToken ct)
	{
		lock (_trava)
		{
			IReadOnlyList<InstanciaVista> achadas = _instancias.Values
				.Where(i => i.ProcessId == processId && i.Variaveis.TryGetValue(variableName, out var v) && v == valueJson)
				.OrderByDescending(i => int.Parse(i.Chave.Substring(3)))
				.Select(Vista)
				.ToList();
			return Task.FromResult(achadas);
		}
	}

	// ----- mensagens -----

	/// <summary>O que acontece quando uma mensagem é correlacionada. Padrão: completar a instância se nada sobrou.</summary>
	public Action<ClusterFalso, AssinaturaFalsa>? AoCorrelacionar { get; set; }

	/// <summary>Um elemento esperando a mensagem com esta chave, como uma assinatura do Camunda.</summary>
	public void NovaAssinatura(string instancia, string elemento, string nome, string chave)
	{
		lock (_trava)
		{
			_assinaturas.Add(new AssinaturaFalsa(instancia, elemento, nome, chave));
			_instancias[instancia].Elementos[elemento] = "ACTIVE";
		}
	}

	public Task<IReadOnlyList<AssinaturaVista>> SearchOpenMessageSubscriptionsAsync(
		IReadOnlyCollection<string>? processInstanceKeys,
		IReadOnlyCollection<string> messageNames,
		string? correlationKey,
		CancellationToken ct)
	{
		lock (_trava)
		{
			IReadOnlyList<AssinaturaVista> achadas = _assinaturas
				.Where(a => a.Aberta && messageNames.Contains(a.Nome))
				.Where(a => processInstanceKeys is null || processInstanceKeys.Contains(a.Instancia))
				.Where(a => correlationKey is null || a.Chave == correlationKey)
				.Select(a => new AssinaturaVista(a.Instancia, a.Elemento, $"{a.Instancia}:{a.Elemento}", a.Nome, a.Chave))
				.ToList();
			return Task.FromResult(achadas);
		}
	}

	// Como o Camunda: vai para a primeira assinatura aberta com o nome e a chave, de qualquer instância.
	public Task<string> CorrelateMessageAsync(
		string messageName, string correlationKey, object? variables, string? tenantId, CancellationToken ct)
	{
		AssinaturaFalsa assinatura;
		lock (_trava)
		{
			assinatura = _assinaturas.FirstOrDefault(a => a.Aberta && a.Nome == messageName && a.Chave == correlationKey)
				?? throw new CamundaRecusouException("NOT_FOUND", $"No subscription for message '{messageName}' with key '{correlationKey}'.");
			assinatura.Aberta = false;
			_instancias[assinatura.Instancia].Elementos[assinatura.Elemento] = "COMPLETED";

			if (variables is not null)
			{
				foreach (var propriedade in JsonSerializer.SerializeToElement(variables).EnumerateObject())
					_instancias[assinatura.Instancia].Variaveis[propriedade.Name] = propriedade.Value.GetRawText();
			}

			Comandos.Add($"correlate {messageName} {correlationKey}");
		}

		if (AoCorrelacionar is not null)
			AoCorrelacionar(this, assinatura);
		else
			CompletarSeNaoSobrouJob(assinatura.Instancia);

		return Task.FromResult(assinatura.Instancia);
	}

	internal sealed class AssinaturaFalsa
	{
		public AssinaturaFalsa(string instancia, string elemento, string nome, string chave)
		{
			Instancia = instancia;
			Elemento = elemento;
			Nome = nome;
			Chave = chave;
		}

		public string Instancia { get; }
		public string Elemento { get; }
		public string Nome { get; }
		public string Chave { get; }
		public bool Aberta { get; set; } = true;
	}

	internal sealed class UserTaskFalsa
	{
		public UserTaskFalsa(string chave, string instancia, string elemento)
		{
			Chave = chave;
			Instancia = instancia;
			Elemento = elemento;
		}

		public string Chave { get; }
		public string Instancia { get; }
		public string Elemento { get; }
		public bool Aberta { get; set; } = true;
	}

	// ----- apoio -----

	public void CompletarSeNaoSobrouJob(string instancia)
	{
		lock (_trava)
		{
			if (!_jobs.Any(j => j.Instancia == instancia && j.Estado == "CREATED") &&
				!_userTasks.Any(t => t.Instancia == instancia && t.Aberta) &&
				!_assinaturas.Any(a => a.Instancia == instancia && a.Aberta))
				_instancias[instancia].Estado = "COMPLETED";
		}
	}

	private Job Exigir(string jobKey)
	{
		var job = _jobs.FirstOrDefault(j => j.Chave == jobKey && j.Estado == "CREATED")
			?? throw new CamundaRecusouException("NOT_FOUND", $"Job {jobKey} not found.");

		if (job.ComLease)
			throw new CamundaRecusouException("INVALID_STATE", $"Expected to complete job {jobKey}, but it is leased by another worker.");

		return job;
	}

	private static InstanciaVista Vista(Instancia i) => new(
		i.Chave, i.ProcessId, i.Estado, i.ComIncident, i.Pai, i.Definicao,
		i.Pai is null || i.ChamadaNoPai is null ? null : $"{i.Pai}:{i.ChamadaNoPai}");

	internal sealed class Instancia
	{
		public Instancia(string chave, string processId, string? pai)
		{
			Chave = chave;
			ProcessId = processId;
			Pai = pai;
		}

		public string Chave { get; }
		public string ProcessId { get; }
		public string? Pai { get; }
		public string? Definicao { get; set; }
		public List<IncidenteVisto> Incidentes { get; } = new();
		public string? ChamadaNoPai { get; init; }
		public string Estado { get; set; } = "ACTIVE";
		public bool ComIncident { get; set; }
		public Dictionary<string, string> Elementos { get; } = new();
		public Dictionary<string, string> Variaveis { get; } = new();
	}

	internal sealed class Job
	{
		public Job(string chave, string tipo, string elemento, string instancia)
		{
			Chave = chave;
			Tipo = tipo;
			Elemento = elemento;
			Instancia = instancia;
		}

		public string Chave { get; }
		public string Tipo { get; }
		public string Elemento { get; }
		public string Instancia { get; }
		public string Estado { get; set; } = "CREATED";
		public bool ComLease { get; set; }
	}
}
