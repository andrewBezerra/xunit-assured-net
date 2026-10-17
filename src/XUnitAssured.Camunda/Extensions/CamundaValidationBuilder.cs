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

using XUnitAssured.Camunda.Gateway;
using XUnitAssured.Camunda.Mocks;
using XUnitAssured.Camunda.Results;
using XUnitAssured.Camunda.Steps;
using XUnitAssured.Core.Abstractions;
using XUnitAssured.Core.Extensions;

namespace XUnitAssured.Camunda.Extensions;

/// <summary>
/// As asserções que uma cadeia de Camunda devolve, sobre a instância que ela iniciou.
/// </summary>
/// <remarks>
/// <para>
/// <b>Toda asserção espera.</b> As buscas do Camunda leem o armazenamento secundário, que fica
/// atrás do motor: a instância completa no motor e só aparece completa na busca um pouco depois.
/// Cada asserção pergunta de novo a cada <c>pollIntervalMilliseconds</c> até
/// <c>assertionTimeoutSeconds</c>, como o Camunda Process Test faz.
/// </para>
/// <para>
/// <b>E falha cedo quando esperar não adianta:</b> a instância foi encerrada, abriu um incident,
/// ou um mock encontrou o job com outro worker. A mensagem diz o que a instância está esperando,
/// quais jobs não têm mock, e o endereço dela no Operate.
/// </para>
/// </remarks>
public class CamundaValidationBuilder : ValidationBuilder<CamundaStepResult>
{
	private static readonly JsonSerializerOptions LeituraDeVariavel = new() { PropertyNameCaseInsensitive = true };

	private readonly ITestScenario _cenario;

	/// <summary>Cria o construtor sobre o cenário que já executou.</summary>
	/// <param name="scenario">O cenário com os passos executados.</param>
	public CamundaValidationBuilder(ITestScenario scenario) : base(scenario) => _cenario = scenario;

	/// <inheritdoc cref="ValidationBuilder{TResult}.Then" />
	public new CamundaValidationBuilder Then()
	{
		base.Then();
		return this;
	}

	/// <inheritdoc cref="ValidationBuilder{TResult}.AssertSuccess" />
	public new CamundaValidationBuilder AssertSuccess()
	{
		base.AssertSuccess();
		return this;
	}

	/// <inheritdoc cref="ValidationBuilder{TResult}.AssertFailure" />
	public new CamundaValidationBuilder AssertFailure()
	{
		base.AssertFailure();
		return this;
	}

	/// <summary>Afirma que a instância chega ao fim, por qualquer evento de fim.</summary>
	public CamundaValidationBuilder AssertInstanceCompleted()
	{
		Esperar("to complete", async (a, ct) =>
		{
			var instancia = await a.Gateway.GetInstanceAsync(a.Chave, ct).ConfigureAwait(false);
			if (instancia is null) return Verificacao.Pendente("it has not shown up in the search yet");

			return instancia.State switch
			{
				"COMPLETED" => Verificacao.Ok,
				"TERMINATED" => Verificacao.Impossivel("it was terminated"),
				_ when instancia.HasIncident => Verificacao.Impossivel("it has an open incident"),
				var estado => Verificacao.Pendente($"it is {estado}")
			};
		});

		// A instância completou: a cobertura dela entra já, e não quando o laço perceber, que pode ser
		// depois de o processo de teste terminar.
		var mocks = Alvo().Mocks;
		if (mocks is not null)
			Rodar(ct => mocks.InstanciaTerminouAsync(ct));

		return this;
	}

	/// <summary>Afirma que a instância está ativa: ainda esperando algo.</summary>
	public CamundaValidationBuilder AssertInstanceActive()
	{
		Esperar("to be active", async (a, ct) =>
		{
			var instancia = await a.Gateway.GetInstanceAsync(a.Chave, ct).ConfigureAwait(false);
			if (instancia is null) return Verificacao.Pendente("it has not shown up in the search yet");

			return instancia.State == "ACTIVE"
				? Verificacao.Ok
				: Verificacao.Impossivel($"it is {instancia.State}");
		});

		return this;
	}

	/// <summary>
	/// Afirma que a instância passou por estes elementos, em qualquer ordem.
	/// </summary>
	/// <remarks>Os ids são os do BPMN. Só a instância iniciada conta, não as que ela chamou.</remarks>
	public CamundaValidationBuilder AssertCompletedElements(params string[] elementIds)
	{
		Ids(elementIds);
		Esperar($"to complete {Lista(elementIds)}", (a, ct) => Elementos(a, elementIds, "COMPLETED", ct));
		return this;
	}

	/// <summary>
	/// Afirma que a instância está parada nestes elementos: uma task esperando, um evento aguardando.
	/// </summary>
	public CamundaValidationBuilder AssertActiveElements(params string[] elementIds)
	{
		Ids(elementIds);
		Esperar($"to be waiting at {Lista(elementIds)}", (a, ct) => Elementos(a, elementIds, "ACTIVE", ct));
		return this;
	}

	/// <summary>
	/// Afirma o valor de uma variável da instância, comparado como JSON.
	/// </summary>
	/// <param name="name">O nome da variável.</param>
	/// <param name="expected">O valor: texto, número, booleano, objeto anônimo, lista. Nulo: a variável existe e é null.</param>
	public CamundaValidationBuilder AssertVariable(string name, object? expected)
	{
		if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Variable name is required.", nameof(name));

		var esperado = JsonSerializer.SerializeToNode(expected);
		Esperar($"to have variable '{name}' = {esperado?.ToJsonString() ?? "null"}", async (a, ct) =>
		{
			var valor = await Variavel(a, name, ct).ConfigureAwait(false);
			if (valor is null) return Verificacao.Pendente($"it has no variable '{name}'");

			return JsonNode.DeepEquals(JsonNode.Parse(valor), esperado)
				? Verificacao.Ok
				: Verificacao.Pendente($"'{name}' is {valor}");
		});

		return this;
	}

	/// <summary>
	/// Afirma sobre uma variável da instância, lida no tipo pedido.
	/// </summary>
	/// <remarks>
	/// A verificação roda de novo até passar ou o prazo acabar, porque a variável pode ainda não
	/// ter o valor final. No fim do prazo, a falha é a da última tentativa.
	/// </remarks>
	public CamundaValidationBuilder AssertVariable<T>(string name, Action<T> assertion)
	{
		if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Variable name is required.", nameof(name));
		ArgumentNullException.ThrowIfNull(assertion);

		Esperar($"to have variable '{name}' satisfying the assertion", async (a, ct) =>
		{
			var valor = await Variavel(a, name, ct).ConfigureAwait(false);
			if (valor is null) return Verificacao.Pendente($"it has no variable '{name}'");

			try
			{
				var lido = JsonSerializer.Deserialize<T>(valor, LeituraDeVariavel);
				assertion(lido!);
				return Verificacao.Ok;
			}
			catch (Exception e) when (e is ShouldAssertException or JsonException or InvalidOperationException)
			{
				return Verificacao.Pendente($"'{name}' is {valor}: {e.Message}");
			}
		});

		return this;
	}

	/// <summary>Afirma que a instância não tem incident aberto agora.</summary>
	/// <remarks>Não espera: use depois de uma asserção que espera, como <see cref="AssertInstanceCompleted"/>.</remarks>
	public CamundaValidationBuilder AssertNoIncidents()
	{
		var alvo = Alvo();
		var incidentes = Rodar(ct => alvo.Gateway.SearchActiveIncidentsAsync(alvo.Chave, ct));

		if (incidentes.Count > 0)
			Falhar(alvo, $"Expected process instance {alvo.Chave} to have no open incident, but it has {incidentes.Count}:" +
				string.Concat(incidentes.Select(i => $"{Environment.NewLine}  - at '{i.ElementId}': {i.ErrorType}: {i.ErrorMessage}")));

		return this;
	}

	/// <summary>
	/// Afirma que um incident abriu no elemento, nesta instância ou numa que ela chamou.
	/// </summary>
	/// <remarks>
	/// Espera o incident aparecer. Desiste quando a instância termina sem ele.
	/// </remarks>
	/// <param name="elementId">O id do elemento no BPMN onde o incident deve abrir.</param>
	/// <param name="errorType">O tipo, como o Operate o mostra: <c>JOB_NO_RETRIES</c>, <c>UNHANDLED_ERROR_EVENT</c>... Nulo: qualquer um.</param>
	/// <param name="messageContains">Um trecho da mensagem do incident. Nulo: qualquer uma.</param>
	public CamundaValidationBuilder AssertIncident(string elementId, string? errorType = null, string? messageContains = null)
	{
		if (string.IsNullOrWhiteSpace(elementId)) throw new ArgumentException("Element id is required.", nameof(elementId));

		var esperado = $"to have an incident at '{elementId}'" +
			(errorType is null ? "" : $" of type {errorType}") +
			(messageContains is null ? "" : $" saying '{messageContains}'");

		Esperar(esperado, async (a, ct) =>
		{
			var arvore = a.Mocks?.Instancias ?? new[] { a.Chave };
			var incidentes = new List<IncidenteVisto>();
			foreach (var instancia in arvore)
				incidentes.AddRange(await a.Gateway.SearchActiveIncidentsAsync(instancia, ct).ConfigureAwait(false));

			if (incidentes.Any(i => i.ElementId == elementId
				&& (errorType is null || string.Equals(i.ErrorType.Replace("_", ""), errorType.Replace("_", ""), StringComparison.OrdinalIgnoreCase))
				&& (messageContains is null || i.ErrorMessage.Contains(messageContains, StringComparison.OrdinalIgnoreCase))))
				return Verificacao.Ok;

			var situacao = incidentes.Count == 0
				? "it has no open incident"
				: "its incidents are " + string.Join("; ", incidentes.Select(i => $"at '{i.ElementId}': {i.ErrorType}: {i.ErrorMessage}"));

			var instanciaVista = await a.Gateway.GetInstanceAsync(a.Chave, ct).ConfigureAwait(false);
			return instanciaVista is { State: "COMPLETED" or "TERMINATED" }
				? Verificacao.Impossivel($"{situacao}, and the instance is already {instanciaVista.State}")
				: Verificacao.Pendente(situacao);
		});

		return this;
	}

	/// <summary>
	/// Afirma quantas vezes o mock deste tipo resolveu um job, nesta instância e nas que ela chamou.
	/// </summary>
	public CamundaValidationBuilder AssertJobMocked(string jobType, int times = 1)
	{
		if (string.IsNullOrWhiteSpace(jobType)) throw new ArgumentException("Job type is required.", nameof(jobType));

		return Contar($"to have its '{jobType}' job mocked {times} time(s)", "the mock ran", times, m => m.Invocacoes(jobType));
	}

	/// <summary>
	/// Afirma quantas vezes o mock desta user task a completou, nesta instância e nas que ela chamou.
	/// </summary>
	public CamundaValidationBuilder AssertUserTaskMocked(string elementId, int times = 1)
	{
		if (string.IsNullOrWhiteSpace(elementId)) throw new ArgumentException("Element id is required.", nameof(elementId));

		return Contar($"to have user task '{elementId}' completed by the mock {times} time(s)", "the mock completed it",
			times, m => m.UserTasksResolvidas(elementId));
	}

	/// <summary>
	/// Afirma quantas vezes o timer foi pulado, nesta instância e nas que ela chamou.
	/// </summary>
	/// <remarks>
	/// Um timer pulado fica <c>TERMINATED</c> na instância, e não <c>COMPLETED</c>: é esta asserção, e não
	/// <c>AssertCompletedElements</c>, que diz que a instância passou por ele.
	/// </remarks>
	public CamundaValidationBuilder AssertTimerSkipped(string elementId, int times = 1)
	{
		if (string.IsNullOrWhiteSpace(elementId)) throw new ArgumentException("Element id is required.", nameof(elementId));

		return Contar($"to have timer '{elementId}' skipped {times} time(s)", "it was skipped",
			times, m => m.TimersPulados(elementId));
	}

	/// <summary>
	/// Afirma quantas vezes o mock correlacionou esta mensagem, nesta instância e nas que ela chamou.
	/// </summary>
	public CamundaValidationBuilder AssertMessageCorrelated(string messageName, int times = 1)
	{
		if (string.IsNullOrWhiteSpace(messageName)) throw new ArgumentException("Message name is required.", nameof(messageName));

		return Contar($"to have message '{messageName}' correlated by the mock {times} time(s)", "it was correlated",
			times, m => m.MensagensCorrelacionadas(messageName));
	}

	/// <summary>
	/// Afirma quantas vezes o sinal foi disparado, nesta instância e nas que ela chamou.
	/// </summary>
	/// <remarks>
	/// O evento de sinal disparado pelo teste fica <c>TERMINATED</c>, como o timer pulado: é por esta
	/// asserção que se sabe que ele aconteceu.
	/// </remarks>
	public CamundaValidationBuilder AssertSignalTriggered(string signalName, int times = 1)
	{
		if (string.IsNullOrWhiteSpace(signalName)) throw new ArgumentException("Signal name is required.", nameof(signalName));

		return Contar($"to have signal '{signalName}' triggered {times} time(s)", "it was triggered",
			times, m => m.SinaisDisparados(signalName));
	}

	/// <summary>
	/// Afirma quantas vezes o processo filho desta call activity foi mockado.
	/// </summary>
	public CamundaValidationBuilder AssertChildProcessMocked(string callActivityId, int times = 1)
	{
		if (string.IsNullOrWhiteSpace(callActivityId)) throw new ArgumentException("Call activity id is required.", nameof(callActivityId));

		return Contar($"to have the child process of '{callActivityId}' mocked {times} time(s)", "it was mocked",
			times, m => m.FilhosMockados(callActivityId));
	}

	private CamundaValidationBuilder Contar(string oQue, string feito, int times, Func<MocksDaInstancia, int> contagem)
	{
		if (times < 0) throw new ArgumentOutOfRangeException(nameof(times), "Times cannot be negative.");

		Esperar(oQue, (a, _) =>
		{
			var feitas = a.Mocks is null ? 0 : contagem(a.Mocks);
			return Task.FromResult(feitas == times
				? Verificacao.Ok
				: feitas > times
					? Verificacao.Impossivel($"{feito} {feitas} time(s)")
					: Verificacao.Pendente($"{feito} {feitas} time(s)"));
		});

		return this;
	}

	// ---------------------------------------------------------------------------------------

	private sealed record AlvoDaAssercao(
		string Chave, string? ProcessId, ICamundaGateway Gateway, MocksDaInstancia? Mocks, CamundaSettings Settings);

	private sealed record Verificacao(bool Passou, bool Desistir, string Situacao)
	{
		public static readonly Verificacao Ok = new(true, false, string.Empty);

		public static Verificacao Pendente(string situacao) => new(false, false, situacao);

		public static Verificacao Impossivel(string situacao) => new(false, true, situacao);
	}

	private AlvoDaAssercao Alvo()
	{
		if (!Result.Success)
			throw new ShouldAssertException(
				"The chain did not start a process instance, so there is nothing to assert on: " +
				string.Join(" ", Result.Errors));

		var chave = Result.ProcessInstanceKey
			?? throw new InvalidOperationException(
				"The last step of the chain did not start a process instance. End the chain with StartInstance(...) " +
				"before asserting on the instance.");

		var settings = CamundaSettings.De(_cenario.Context);
		var mocks = _cenario.Context.GetProperty<MocksDaInstancia>(MocksDaInstancia.ChaveNoContexto);
		var mocksDestaInstancia = mocks?.ProcessInstanceKey == chave ? mocks : null;

		return new AlvoDaAssercao(chave, Result.ProcessId, Gateways.De(_cenario.Context, settings), mocksDestaInstancia, settings);
	}

	private void Esperar(string oQue, Func<AlvoDaAssercao, CancellationToken, Task<Verificacao>> verificar)
	{
		var alvo = Alvo();
		var prazo = TimeSpan.FromSeconds(Math.Max(1, alvo.Settings.AssertionTimeoutSeconds));
		var intervalo = TimeSpan.FromMilliseconds(Math.Max(10, alvo.Settings.PollIntervalMilliseconds));

		var (passou, situacao, desistiu) = Rodar(async ct =>
		{
			var cronometro = Stopwatch.StartNew();
			var ultima = "it was never checked";

			while (true)
			{
				if (alvo.Mocks is { Problemas.Count: > 0 })
					return (false, "the mocks hit a problem", true);

				var verificacao = await verificar(alvo, ct).ConfigureAwait(false);
				if (verificacao.Passou) return (true, string.Empty, false);
				if (verificacao.Desistir) return (false, verificacao.Situacao, true);

				ultima = verificacao.Situacao;
				if (cronometro.Elapsed >= prazo) return (false, ultima, false);

				await Task.Delay(intervalo, ct).ConfigureAwait(false);
			}
		});

		if (passou) return;

		var quando = desistiu ? string.Empty : $" within {prazo.TotalSeconds:0.#}s";
		Falhar(alvo, $"Expected process instance {alvo.Chave}{Processo()} {oQue}{quando}, but {situacao}.");
	}

	// O processo e a versão que a instância roda: sem Deploy na cadeia, "a última" muda quando
	// alguém publica, e a mensagem precisa dizer qual diagrama falhou.
	private string Processo()
	{
		if (Result.ProcessId is null) return string.Empty;

		var versao = Result.Version is { } v ? $" version {v}" : string.Empty;
		var rotulo = Result.VersionTag is { } t ? $", tag '{t}'" : string.Empty;
		return $" ({Result.ProcessId}{versao}{rotulo})";
	}

	private void Falhar(AlvoDaAssercao alvo, string mensagem)
	{
		var texto = new StringBuilder(mensagem);

		try
		{
			Rodar(async ct =>
			{
				if (alvo.Mocks is { Problemas.Count: > 0 } comProblema)
				{
					texto.AppendLine().Append("  Mock problems:");
					foreach (var problema in comProblema.Problemas)
						texto.AppendLine().Append("  - ").Append(problema);
				}

				var ativos = (await alvo.Gateway.SearchElementsAsync(alvo.Chave, ct).ConfigureAwait(false))
					.Where(e => e.State == "ACTIVE" && e.Type != "PROCESS")
					.Select(e => $"'{e.ElementId}' ({e.Type})")
					.ToList();
				if (ativos.Count > 0)
					texto.AppendLine().Append("  Waiting at: ").Append(string.Join(", ", ativos));

				if (alvo.Mocks is not null)
				{
					var esperas = await alvo.Mocks.EsperasSemRespostaAsync(ct).ConfigureAwait(false);
					if (esperas.Count > 0)
						texto.AppendLine().Append("  Waiting for (not answered in this test): ")
							.Append(string.Join(", ", esperas.Select(DescreverEspera)));

					var nuncaUsados = alvo.Mocks.NuncaUsados();
					if (nuncaUsados.Count > 0)
						texto.AppendLine().Append("  Declared in this test but never used: ").Append(string.Join(", ", nuncaUsados));

					var semMock = await alvo.Mocks.PendentesSemMockAsync(ct).ConfigureAwait(false);
					if (semMock.Count > 0)
						texto.AppendLine().Append("  Jobs waiting for a worker (not mocked in this test): ")
							.Append(string.Join(", ", semMock.Select(j => $"'{j.Type}' at '{j.ElementId}' (instance {j.ProcessInstanceKey})")));

					if (alvo.Mocks.UltimaFalhaDeRede is { } rede)
						texto.AppendLine().Append("  Last network failure of the mocks: ").Append(rede);
				}

				var incidentes = await alvo.Gateway.SearchActiveIncidentsAsync(alvo.Chave, ct).ConfigureAwait(false);
				foreach (var incidente in incidentes)
					texto.AppendLine().Append("  Incident at '").Append(incidente.ElementId).Append("': ")
						.Append(incidente.ErrorType).Append(": ").Append(incidente.ErrorMessage);

				return true;
			});
		}
		catch (Exception e)
		{
			texto.AppendLine().Append("  (Could not gather diagnostics: ").Append(e.Message).Append(')');
		}

		foreach (var aviso in Result.Warnings)
			texto.AppendLine().Append("  Warning: ").Append(aviso);

		var endereco = alvo.Settings.RestAddress ?? Environment.GetEnvironmentVariable("CAMUNDA_REST_ADDRESS");
		if (!string.IsNullOrWhiteSpace(endereco))
			texto.AppendLine().Append("  Operate: ").Append(endereco!.TrimEnd('/')).Append("/operate/processes/").Append(alvo.Chave);

		throw new ShouldAssertException(texto.ToString());
	}

	private static async Task<Verificacao> Elementos(
		AlvoDaAssercao alvo, string[] ids, string estado, CancellationToken ct)
	{
		var elementos = await alvo.Gateway.SearchElementsAsync(alvo.Chave, ct).ConfigureAwait(false);
		var nesseEstado = elementos.Where(e => e.State == estado).Select(e => e.ElementId).ToHashSet(StringComparer.Ordinal);
		var faltam = ids.Where(id => !nesseEstado.Contains(id)).ToList();

		if (faltam.Count == 0) return Verificacao.Ok;

		var situacao = $"{Lista(faltam)} {(faltam.Count == 1 ? "is" : "are")} not {estado.ToLowerInvariant()}";

		var instancia = await alvo.Gateway.GetInstanceAsync(alvo.Chave, ct).ConfigureAwait(false);
		if (instancia is { State: "COMPLETED" or "TERMINATED" })
			return Verificacao.Impossivel($"{situacao}, and the instance is already {instancia.State}");

		return Verificacao.Pendente(situacao);
	}

	private static async Task<string?> Variavel(AlvoDaAssercao alvo, string nome, CancellationToken ct)
	{
		var variaveis = await alvo.Gateway.SearchVariablesAsync(alvo.Chave, nome, ct).ConfigureAwait(false);

		// A do escopo da instância ganha de uma homônima local de um subprocesso.
		var daInstancia = variaveis.FirstOrDefault(v => v.ScopeKey == alvo.Chave) ?? variaveis.FirstOrDefault();
		return daInstancia?.ValueJson;
	}

	private static string DescreverEspera(EsperaVista e) => e.Tipo switch
	{
		"USER_TASK" => $"user task '{e.ElementId}'",
		"MESSAGE" => $"message '{e.MessageName}' with correlation key '{e.CorrelationKey}' at '{e.ElementId}'",
		"SIGNAL" => $"signal '{e.SignalName}' at '{e.ElementId}'",
		"TIMER" => $"timer '{e.ElementId}'",
		var outro => $"{outro.ToLowerInvariant().Replace('_', ' ')} at '{e.ElementId}'"
	} + (e.ProcessInstanceKey == string.Empty ? "" : $" (instance {e.ProcessInstanceKey})");

	private static void Ids(string[] elementIds)
	{
		if (elementIds == null || elementIds.Length == 0)
			throw new ArgumentException("Name at least one element id from the BPMN.", nameof(elementIds));
	}

	private static string Lista(IEnumerable<string> ids) => string.Join(", ", ids.Select(id => $"'{id}'"));

	// As asserções são síncronas para encadear como as dos outros pacotes; o trabalho de rede roda
	// fora do contexto de sincronização do chamador, para não haver deadlock num runner que tenha um.
	private static T Rodar<T>(Func<CancellationToken, Task<T>> trabalho) =>
		Task.Run(() => trabalho(CancellationToken.None)).GetAwaiter().GetResult();
}
