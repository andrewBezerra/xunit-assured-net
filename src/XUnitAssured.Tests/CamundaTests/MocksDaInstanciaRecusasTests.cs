using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

using XUnitAssured.Camunda;
using XUnitAssured.Camunda.Cobertura;
using XUnitAssured.Camunda.Gateway;
using XUnitAssured.Camunda.Mocks;

namespace XUnitAssured.Tests.CamundaTests;

/// <summary>
/// O laço dos mocks quando o cluster diz não: cada comando recusado (job, user task, mensagem, sinal,
/// processo filho) vira um problema que a asserção mostra, um <c>NOT_FOUND</c> é só uma corrida que
/// passou, e uma falha de rede é tentada de novo na volta seguinte.
/// </summary>
[Trait("Category", "Camunda")]
[Trait("Component", "Mocks")]
public class MocksDaInstanciaRecusasTests
{
	// ---------- O laço ----------

	[Fact(DisplayName = "A search the cluster refuses stops the loop with a problem that names the code, instead of a silent wait")]
	public async Task Refused_Search_Stops_The_Loop()
	{
		var cluster = new ClusterFalso
		{
			Falha = op => op == nameof(ICamundaGateway.SearchChildInstancesAsync) ? new CamundaRecusouException("UNAUTHORIZED", "bad credentials") : null
		};
		var instancia = cluster.NovaInstancia("pedido");

		var mocks = Iniciar(cluster, instancia, Completar("cobrar"));
		await mocks.Rodando.WaitAsync(TimeSpan.FromSeconds(10));

		mocks.Problemas.ShouldHaveSingleItem().ShouldBe("Camunda rejected the mocks' search: UNAUTHORIZED: bad credentials");
		mocks.Encerrado.ShouldBeTrue();
	}

	[Fact(DisplayName = "A network failure is kept for the diagnosis while the loop keeps trying")]
	public async Task Network_Failure_Is_Kept_For_Diagnosis()
	{
		var cluster = new ClusterFalso
		{
			Falha = op => op == nameof(ICamundaGateway.SearchChildInstancesAsync) ? new HttpRequestException("connection refused") : null
		};
		var instancia = cluster.NovaInstancia("pedido");

		var mocks = Iniciar(cluster, instancia, Completar("cobrar"));
		await Ate(() => mocks.UltimaFalhaDeRede is not null);

		mocks.UltimaFalhaDeRede.ShouldBe("HttpRequestException: connection refused");
		mocks.Encerrado.ShouldBeFalse();
		mocks.Problemas.ShouldBeEmpty();
		await mocks.PararAsync();
	}

	[Fact(DisplayName = "When the lifetime ends and the cancellation fails, the loop still ends quietly")]
	public async Task Failed_Cancellation_At_The_End_Of_Lifetime_Is_Quiet()
	{
		var cluster = new ClusterFalso
		{
			Falha = op => op == nameof(ICamundaGateway.CancelInstanceAsync) ? new HttpRequestException("connection refused") : null
		};
		var instancia = cluster.NovaInstancia("pedido");
		cluster.NovoJob(instancia, "despachar", "despachar");

		var mocks = Iniciar(cluster, instancia, Completar("cobrar"), vidaSegundos: 1);
		await mocks.Rodando.WaitAsync(TimeSpan.FromSeconds(10));

		mocks.Rodando.IsCompletedSuccessfully.ShouldBeTrue();
		cluster.Canceladas.ShouldBeEmpty();
		cluster.EstadoDe(instancia).ShouldBe("ACTIVE");
	}

	[Fact(DisplayName = "At the end of the process, a cancellation the cluster refuses does not throw")]
	public async Task Failed_Cancellation_At_The_End_Of_The_Process_Is_Quiet()
	{
		var cluster = new ClusterFalso
		{
			Falha = op => op == nameof(ICamundaGateway.CancelInstanceAsync) ? new CamundaRecusouException("NOT_FOUND", "gone") : null
		};
		var instancia = cluster.NovaInstancia("pedido");

		var mocks = Iniciar(cluster, instancia, Completar("cobrar"));
		await mocks.EncerrarNaSaidaAsync(default);
		await mocks.Rodando.WaitAsync(TimeSpan.FromSeconds(5));

		cluster.Canceladas.ShouldBeEmpty();
		mocks.Encerrado.ShouldBeTrue();
	}

	[Fact(DisplayName = "Waits nobody answers lists the user tasks without a mock, and leaves out the mocked ones")]
	public async Task Waits_Without_Answer()
	{
		var cluster = new ClusterFalso();
		var instancia = cluster.NovaInstancia("pedido");
		cluster.NovaUserTask(instancia, "aprovar");
		cluster.NovaUserTask(instancia, "revisar");
		cluster.NovaAssinatura(instancia, "aguardar-pagamento", "pago", "A-7");

		var mocks = new MocksDaInstancia(
			cluster, instancia, new Dictionary<string, JobMock>(), ClusterFalso.Rapido(),
			userTasks: new Dictionary<string, UserTaskMock> { ["aprovar"] = new UserTaskMock("aprovar", null) },
			sinais: new Dictionary<string, EventoMock> { ["liberado"] = new EventoMock("liberado", null) });

		var esperas = await mocks.EsperasSemRespostaAsync(default);

		esperas.Select(e => e.ElementId).ShouldBe(new[] { "revisar", "aguardar-pagamento" }, ignoreOrder: true);
	}

	// ---------- Jobs ----------

	[Fact(DisplayName = "A job that disappeared before the mock resolved it is not a problem, and is not retried")]
	public async Task Job_Not_Found_Is_Not_A_Problem()
	{
		var tentativas = 0;
		var cluster = new ClusterFalso
		{
			Falha = op => op == nameof(ICamundaGateway.CompleteJobAsync) && ++tentativas > 0 ? new CamundaRecusouException("NOT_FOUND", "no job") : null
		};
		var instancia = cluster.NovaInstancia("pedido");
		var job = cluster.NovoJob(instancia, "cobrar", "cobrar");

		var mocks = Iniciar(cluster, instancia, Completar("cobrar"));
		await Ate(() => tentativas > 0);
		await Task.Delay(150);
		await mocks.PararAsync();

		tentativas.ShouldBe(1);
		mocks.Problemas.ShouldBeEmpty();
		mocks.Invocacoes("cobrar").ShouldBe(0);
		cluster.JobEstado(job).ShouldBe("CREATED");
	}

	[Fact(DisplayName = "A job command the cluster refuses becomes a problem naming the action, the job and the code")]
	public async Task Job_Refused_Is_A_Problem()
	{
		var cluster = new ClusterFalso
		{
			Falha = op => op == nameof(ICamundaGateway.ThrowBpmnErrorAsync) ? new CamundaRecusouException("FORBIDDEN", "no permission") : null
		};
		var instancia = cluster.NovaInstancia("pedido");
		var job = cluster.NovoJob(instancia, "cobrar", "cobrar");

		var mocks = Iniciar(cluster, instancia, new Dictionary<string, JobMock>
		{
			["cobrar"] = new JobMock("cobrar", AcaoDoMock.LancarErroBpmn, null, "RECUSADO", null)
		});
		await Ate(() => mocks.Problemas.Count > 0);
		await mocks.PararAsync();

		var problema = mocks.Problemas.ShouldHaveSingleItem();
		problema.ShouldStartWith("Camunda rejected the mock's attempt to throw BPMN error 'RECUSADO' job 'cobrar' at element 'cobrar'");
		problema.ShouldEndWith($"(job {job}): FORBIDDEN: no permission");
	}

	[Fact(DisplayName = "A job command lost to the network is retried on the next round, and the job gets resolved")]
	public async Task Job_Network_Failure_Is_Retried()
	{
		var falhas = 1;
		var cluster = new ClusterFalso
		{
			Falha = op => op == nameof(ICamundaGateway.CompleteJobAsync) && falhas-- > 0 ? new HttpRequestException("reset") : null
		};
		var instancia = cluster.NovaInstancia("pedido");
		var job = cluster.NovoJob(instancia, "cobrar", "cobrar");

		var mocks = Iniciar(cluster, instancia, Completar("cobrar"));
		await Ate(() => cluster.JobEstado(job) == "COMPLETED");
		await mocks.PararAsync();

		mocks.Invocacoes("cobrar").ShouldBe(1);
		mocks.Problemas.ShouldBeEmpty();
	}

	// ---------- User tasks ----------

	[Fact(DisplayName = "A user task that disappeared before the mock completed it is not a problem")]
	public async Task User_Task_Not_Found_Is_Not_A_Problem()
	{
		var cluster = new ClusterFalso
		{
			Falha = op => op == nameof(ICamundaGateway.CompleteUserTaskAsync) ? new CamundaRecusouException("NOT_FOUND", "no task") : null
		};
		var instancia = cluster.NovaInstancia("pedido");
		cluster.NovaUserTask(instancia, "aprovar");

		var mocks = IniciarComUserTask(cluster, instancia);
		await Task.Delay(200);
		await mocks.PararAsync();

		mocks.Problemas.ShouldBeEmpty();
		mocks.UserTasksResolvidas("aprovar").ShouldBe(0);
	}

	[Fact(DisplayName = "A user task completion the cluster refuses becomes a problem naming the task and the code")]
	public async Task User_Task_Refused_Is_A_Problem()
	{
		var cluster = new ClusterFalso
		{
			Falha = op => op == nameof(ICamundaGateway.CompleteUserTaskAsync) ? new CamundaRecusouException("INVALID_STATE", "assigned to someone else") : null
		};
		var instancia = cluster.NovaInstancia("pedido");
		var tarefa = cluster.NovaUserTask(instancia, "aprovar");

		var mocks = IniciarComUserTask(cluster, instancia);
		await Ate(() => mocks.Problemas.Count > 0);
		await mocks.PararAsync();

		mocks.Problemas.ShouldHaveSingleItem().ShouldBe(
			$"Camunda rejected completing user task 'aprovar' (task {tarefa}, instance {instancia}): INVALID_STATE: assigned to someone else");
	}

	[Fact(DisplayName = "A user task completion lost to the network is retried, and the task gets completed")]
	public async Task User_Task_Network_Failure_Is_Retried()
	{
		var falhas = 1;
		var cluster = new ClusterFalso
		{
			Falha = op => op == nameof(ICamundaGateway.CompleteUserTaskAsync) && falhas-- > 0 ? new HttpRequestException("reset") : null
		};
		var instancia = cluster.NovaInstancia("pedido");
		cluster.NovaUserTask(instancia, "aprovar");

		var mocks = IniciarComUserTask(cluster, instancia);
		await Ate(() => mocks.UserTasksResolvidas("aprovar") == 1);
		await mocks.PararAsync();

		cluster.EstadoDe(instancia).ShouldBe("COMPLETED");
		mocks.Problemas.ShouldBeEmpty();
	}

	// ---------- Sinais e timers ----------

	[Fact(DisplayName = "A signal whose element left before the modification is not a problem")]
	public async Task Signal_Not_Found_Is_Not_A_Problem()
	{
		var (cluster, instancia) = ComSinal(new CamundaRecusouException("NOT_FOUND", "element gone"));

		var mocks = IniciarComSinal(cluster, instancia);
		await Task.Delay(200);
		await mocks.PararAsync();

		mocks.Problemas.ShouldBeEmpty();
		mocks.SinaisDisparados("liberado").ShouldBe(0);
	}

	[Fact(DisplayName = "A signal modification the cluster refuses becomes a problem naming the signal, the element and the code")]
	public async Task Signal_Refused_Is_A_Problem()
	{
		var (cluster, instancia) = ComSinal(new CamundaRecusouException("INVALID_ARGUMENT", "bad element"));

		var mocks = IniciarComSinal(cluster, instancia);
		await Ate(() => mocks.Problemas.Count > 0);
		await mocks.PararAsync();

		mocks.Problemas.ShouldHaveSingleItem().ShouldBe(
			$"Camunda rejected triggering signal 'liberado' at 'aguardar-liberacao' (waiting at 'aguardar-liberacao', instance {instancia}): INVALID_ARGUMENT: bad element");
	}

	[Fact(DisplayName = "A signal modification lost to the network is retried, and the signal triggered once")]
	public async Task Signal_Network_Failure_Is_Retried()
	{
		var (cluster, instancia) = ComSinal(new HttpRequestException("reset"), vezes: 1);

		var mocks = IniciarComSinal(cluster, instancia);
		await Ate(() => mocks.SinaisDisparados("liberado") == 1);
		await mocks.PararAsync();

		cluster.Modificacoes.ShouldHaveSingleItem().ShouldBe($"{instancia} -[{instancia}:aguardar-liberacao] +[seguir]");
		mocks.Problemas.ShouldBeEmpty();
	}

	[Fact(DisplayName = "Without the definition key, the loop finds the BPMN through the instance, and counts the coverage of what it skipped")]
	public async Task Finds_The_Bpmn_Through_The_Instance()
	{
		var (cluster, instancia) = ComSinal(null);
		var settings = ClusterFalso.Rapido();
		settings.Coverage = new CamundaCoverageSettings { Enabled = true };
		var cobertura = new CoberturaDaSuite();

		var mocks = new MocksDaInstancia(
			cluster, instancia, new Dictionary<string, JobMock>(), settings,
			sinais: new Dictionary<string, EventoMock> { ["liberado"] = new EventoMock("liberado", null) },
			cobertura: cobertura);
		mocks.Iniciar();
		await mocks.Rodando.WaitAsync(TimeSpan.FromSeconds(10));

		mocks.SinaisDisparados("liberado").ShouldBe(1);
		var processo = cobertura.Relatorio().Processes.ShouldHaveSingleItem();
		processo.ReachedElements.ShouldContain("aguardar-liberacao");
		processo.ReachedElements.ShouldContain("seguir");
	}

	[Fact(DisplayName = "Coverage skips an instance whose BPMN cannot be read, and records nothing for it")]
	public async Task Coverage_Skips_Unreadable_Instances()
	{
		var cluster = new ClusterFalso();
		var semDefinicao = cluster.NovaInstancia("pedido");
		cluster.Estado(semDefinicao, "COMPLETED");
		var semXml = cluster.NovaInstancia("pedido", definicao: "def-sem-xml");
		cluster.Estado(semXml, "COMPLETED");
		var settings = ClusterFalso.Rapido();
		settings.Coverage = new CamundaCoverageSettings { Enabled = true };

		foreach (var instancia in new[] { semDefinicao, semXml })
		{
			var cobertura = new CoberturaDaSuite();
			var mocks = new MocksDaInstancia(cluster, instancia, new Dictionary<string, JobMock>(), settings, cobertura: cobertura);
			mocks.Iniciar();
			await mocks.Rodando.WaitAsync(TimeSpan.FromSeconds(10));

			cobertura.Vazia.ShouldBeTrue();
		}
	}

	// ---------- Mensagens ----------

	[Fact(DisplayName = "A message that went to an instance outside the test, despite the check, becomes a problem")]
	public async Task Message_Went_Outside()
	{
		var cluster = new ClusterFalso();
		var instancia = cluster.NovaInstancia("pedido");
		cluster.NovaAssinatura(instancia, "aguardar-pagamento", "pago", "A-7");
		string? deFora = null;
		// Entre a conferência e a correlação, outro teste assina a mesma mensagem e chave, mais cedo.
		cluster.Falha = op =>
		{
			if (op == nameof(ICamundaGateway.CorrelateMessageAsync) && deFora is null)
			{
				deFora = cluster.NovaInstancia("pedido");
				cluster.NovaAssinatura(deFora, "aguardar-pagamento", "pago", "A-7", primeira: true);
			}

			return null;
		};

		var mocks = IniciarComMensagem(cluster, instancia);
		await Ate(() => mocks.Problemas.Count > 0);
		await mocks.PararAsync();

		mocks.Problemas.First().ShouldBe(
			$"Message 'pago' with correlation key 'A-7' went to instance {deFora}, outside this test. Use a correlation key unique to the test.");
		mocks.MensagensCorrelacionadas("pago").ShouldBe(0);
	}

	[Fact(DisplayName = "A subscription that closed before the correlation is not a problem")]
	public async Task Message_Not_Found_Is_Not_A_Problem()
	{
		var cluster = new ClusterFalso
		{
			Falha = op => op == nameof(ICamundaGateway.CorrelateMessageAsync) ? new CamundaRecusouException("NOT_FOUND", "closed") : null
		};
		var instancia = cluster.NovaInstancia("pedido");
		cluster.NovaAssinatura(instancia, "aguardar-pagamento", "pago", "A-7");

		var mocks = IniciarComMensagem(cluster, instancia);
		await Task.Delay(200);
		await mocks.PararAsync();

		mocks.Problemas.ShouldBeEmpty();
		mocks.MensagensCorrelacionadas("pago").ShouldBe(0);
	}

	[Fact(DisplayName = "A correlation the cluster refuses becomes a problem naming the message, the key and the code")]
	public async Task Message_Refused_Is_A_Problem()
	{
		var cluster = new ClusterFalso
		{
			Falha = op => op == nameof(ICamundaGateway.CorrelateMessageAsync) ? new CamundaRecusouException("FORBIDDEN", "tenant") : null
		};
		var instancia = cluster.NovaInstancia("pedido");
		cluster.NovaAssinatura(instancia, "aguardar-pagamento", "pago", "A-7");

		var mocks = IniciarComMensagem(cluster, instancia);
		await Ate(() => mocks.Problemas.Count > 0);
		await mocks.PararAsync();

		mocks.Problemas.ShouldHaveSingleItem().ShouldBe(
			"Camunda rejected correlating message 'pago' with correlation key 'A-7' (waiting at 'aguardar-pagamento'): FORBIDDEN: tenant");
	}

	[Fact(DisplayName = "A correlation lost to the network is retried, and the message correlated once")]
	public async Task Message_Network_Failure_Is_Retried()
	{
		var falhas = 1;
		var cluster = new ClusterFalso
		{
			Falha = op => op == nameof(ICamundaGateway.CorrelateMessageAsync) && falhas-- > 0 ? new HttpRequestException("reset") : null
		};
		var instancia = cluster.NovaInstancia("pedido");
		cluster.NovaAssinatura(instancia, "aguardar-pagamento", "pago", "A-7");

		var mocks = IniciarComMensagem(cluster, instancia);
		await Ate(() => mocks.MensagensCorrelacionadas("pago") == 1);
		await mocks.PararAsync();

		cluster.EstadoDe(instancia).ShouldBe("COMPLETED");
		mocks.Problemas.ShouldBeEmpty();
	}

	// ---------- Processo filho ----------

	[Fact(DisplayName = "A mocked child whose elements are not in the search yet is ended once they show up")]
	public async Task Child_Waits_For_Its_Elements()
	{
		var cluster = ComFilho(FilhoComFim);
		var filha = cluster.NovaInstancia("envio", "pi-1", "chamar-envio", "def-envio");

		var mocks = IniciarComFilho(cluster, new FilhoMock("chamar-envio", new { rastreio = "BR-9" }));
		await Task.Delay(150);
		cluster.Modificacoes.ShouldBeEmpty();

		cluster.Elemento(filha, "despachar", "ACTIVE");
		await Ate(() => mocks.FilhosMockados("chamar-envio") == 1);
		await mocks.PararAsync();

		cluster.Modificacoes.ShouldHaveSingleItem().ShouldBe($"{filha} -[{filha}:despachar] +[fim]");
	}

	[Fact(DisplayName = "A mocked child with no plain end event becomes a problem: the mock has nowhere to end it")]
	public async Task Child_Without_End_Event()
	{
		var cluster = ComFilho(FilhoSoComErro);
		var filha = cluster.NovaInstancia("envio", "pi-1", "chamar-envio", "def-envio");
		cluster.Elemento(filha, "despachar", "ACTIVE");

		var mocks = IniciarComFilho(cluster, new FilhoMock("chamar-envio", null));
		await Ate(() => mocks.Problemas.Count > 0);
		await mocks.PararAsync();

		mocks.Problemas.ShouldHaveSingleItem().ShouldBe(
			"MockChildProcess('chamar-envio'): the child process envio has no plain end event outside subprocesses, so the mock has nowhere to end it.");
		cluster.Modificacoes.ShouldBeEmpty();
	}

	[Fact(DisplayName = "A mocked child that ended between the search and the modification is not a problem")]
	public async Task Child_Not_Found_Is_Not_A_Problem()
	{
		var cluster = ComFilho(FilhoComFim);
		cluster.Falha = op => op == nameof(ICamundaGateway.ModifyInstanceAsync) ? new CamundaRecusouException("NOT_FOUND", "gone") : null;
		var filha = cluster.NovaInstancia("envio", "pi-1", "chamar-envio", "def-envio");
		cluster.Elemento(filha, "despachar", "ACTIVE");

		var mocks = IniciarComFilho(cluster, new FilhoMock("chamar-envio", null));
		await Task.Delay(200);
		await mocks.PararAsync();

		mocks.Problemas.ShouldBeEmpty();
		mocks.FilhosMockados("chamar-envio").ShouldBe(0);
	}

	[Fact(DisplayName = "A child modification the cluster refuses becomes a problem naming the call activity and the code")]
	public async Task Child_Refused_Is_A_Problem()
	{
		var cluster = ComFilho(FilhoComFim);
		cluster.Falha = op => op == nameof(ICamundaGateway.ModifyInstanceAsync) ? new CamundaRecusouException("INVALID_STATE", "not allowed") : null;
		var filha = cluster.NovaInstancia("envio", "pi-1", "chamar-envio", "def-envio");
		cluster.Elemento(filha, "despachar", "ACTIVE");

		var mocks = IniciarComFilho(cluster, new FilhoMock("chamar-envio", null));
		await Ate(() => mocks.Problemas.Count > 0);
		await mocks.PararAsync();

		mocks.Problemas.ShouldHaveSingleItem().ShouldBe(
			$"Camunda rejected mocking the child process of 'chamar-envio' (instance {filha}): INVALID_STATE: not allowed");
	}

	// ---------- Apoio ----------

	private static Dictionary<string, JobMock> Completar(string tipo) =>
		new() { [tipo] = new JobMock(tipo, AcaoDoMock.Completar, null) };

	private static MocksDaInstancia Iniciar(
		ClusterFalso cluster, string instancia, Dictionary<string, JobMock> mocks, int vidaSegundos = 30)
	{
		var execucao = new MocksDaInstancia(cluster, instancia, mocks, ClusterFalso.Rapido(vidaDosMocksSegundos: vidaSegundos));
		execucao.Iniciar();
		return execucao;
	}

	private static MocksDaInstancia IniciarComUserTask(ClusterFalso cluster, string instancia)
	{
		var mocks = new MocksDaInstancia(
			cluster, instancia, new Dictionary<string, JobMock>(), ClusterFalso.Rapido(),
			userTasks: new Dictionary<string, UserTaskMock> { ["aprovar"] = new UserTaskMock("aprovar", new { aprovado = true }) });
		mocks.Iniciar();
		return mocks;
	}

	private static MocksDaInstancia IniciarComMensagem(ClusterFalso cluster, string instancia)
	{
		var mocks = new MocksDaInstancia(
			cluster, instancia, new Dictionary<string, JobMock>(), ClusterFalso.Rapido(),
			mensagens: new Dictionary<string, EventoMock> { ["pago"] = new EventoMock("pago", new { valor = 10 }) });
		mocks.Iniciar();
		return mocks;
	}

	// Uma instância esperando o sinal liberado em aguardar-liberacao. Com uma falha, a modificação a
	// recebe (todas as vezes, ou só as primeiras).
	private static (ClusterFalso Cluster, string Instancia) ComSinal(Exception? falha, int vezes = int.MaxValue)
	{
		var restantes = vezes;
		var cluster = new ClusterFalso
		{
			Falha = op => falha is not null && op == nameof(ICamundaGateway.ModifyInstanceAsync) && restantes-- > 0 ? falha : null
		};
		var chave = cluster.Publicar("pedido", 1);
		cluster.DefinirBpmn(chave, ComSinalBpmn);
		var instancia = cluster.NovaInstancia("pedido", definicao: chave);
		cluster.Elemento(instancia, "aguardar-liberacao", "ACTIVE");
		return (cluster, instancia);
	}

	private static MocksDaInstancia IniciarComSinal(ClusterFalso cluster, string instancia)
	{
		var mocks = new MocksDaInstancia(
			cluster, instancia, new Dictionary<string, JobMock>(), ClusterFalso.Rapido(),
			definitionKey: "def-pedido-v1",
			sinais: new Dictionary<string, EventoMock> { ["liberado"] = new EventoMock("liberado", null) });
		mocks.Iniciar();
		return mocks;
	}

	// O pai (pi-1) com a call activity chamar-envio; o BPMN do filho, como o teste o quer.
	private static ClusterFalso ComFilho(string bpmnDoFilho)
	{
		var cluster = new ClusterFalso();
		cluster.NovaInstancia("pedido");
		cluster.DefinirBpmn("def-envio", bpmnDoFilho);
		return cluster;
	}

	private static MocksDaInstancia IniciarComFilho(ClusterFalso cluster, FilhoMock filho)
	{
		var mocks = new MocksDaInstancia(
			cluster, "pi-1", new Dictionary<string, JobMock>(), ClusterFalso.Rapido(),
			filhos: new Dictionary<string, FilhoMock> { [filho.CallActivityId] = filho });
		mocks.Iniciar();
		return mocks;
	}

	private static async Task Ate(Func<bool> condicao)
	{
		var cronometro = Stopwatch.StartNew();
		while (!condicao())
		{
			if (cronometro.Elapsed > TimeSpan.FromSeconds(10))
				throw new TimeoutException("The condition was not met within 10 s.");

			await Task.Delay(10);
		}
	}

	private const string ComSinalBpmn = """
		<?xml version="1.0" encoding="UTF-8"?>
		<bpmn:definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL" id="d">
		  <bpmn:signal id="Sinal_Liberado" name="liberado" />
		  <bpmn:process id="pedido" isExecutable="true">
		    <bpmn:intermediateCatchEvent id="aguardar-liberacao"><bpmn:signalEventDefinition signalRef="Sinal_Liberado" /></bpmn:intermediateCatchEvent>
		    <bpmn:serviceTask id="seguir" />
		    <bpmn:sequenceFlow id="f1" sourceRef="aguardar-liberacao" targetRef="seguir" />
		  </bpmn:process>
		</bpmn:definitions>
		""";

	private const string FilhoComFim = """
		<?xml version="1.0" encoding="UTF-8"?>
		<bpmn:definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL" id="d">
		  <bpmn:process id="envio" isExecutable="true">
		    <bpmn:startEvent id="inicio" />
		    <bpmn:serviceTask id="despachar" />
		    <bpmn:endEvent id="fim" />
		  </bpmn:process>
		</bpmn:definitions>
		""";

	private const string FilhoSoComErro = """
		<?xml version="1.0" encoding="UTF-8"?>
		<bpmn:definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL" id="d">
		  <bpmn:error id="Erro" errorCode="FALHOU" />
		  <bpmn:process id="envio" isExecutable="true">
		    <bpmn:startEvent id="inicio" />
		    <bpmn:serviceTask id="despachar" />
		    <bpmn:endEvent id="fim-erro"><bpmn:errorEventDefinition errorRef="Erro" /></bpmn:endEvent>
		  </bpmn:process>
		</bpmn:definitions>
		""";
}
