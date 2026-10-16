using System;
using System.Linq;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

using XUnitAssured.Camunda;
using XUnitAssured.Camunda.Extensions;
using XUnitAssured.Core.DSL;

namespace XUnitAssured.Tests.CamundaTests;

/// <summary>
/// A ida e volta contra um Camunda 8.10 de verdade.
///
/// <para>
/// O resto da cobertura do pacote roda contra um cluster falso, e isso não prova que o pacote fala
/// com o Camunda: que o motor aceita completar um job que ninguém ativou, que a busca acha a
/// instância filha, que o mapeamento de saída da call activity roda. Isto prova.
/// </para>
///
/// <para>
/// O endereço e as credenciais vêm das variáveis que o SDK oficial lê:
/// <c>CAMUNDA_REST_ADDRESS</c> (padrão <c>http://localhost:8080</c>),
/// <c>CAMUNDA_BASIC_AUTH_USERNAME</c> e <c>CAMUNDA_BASIC_AUTH_PASSWORD</c>. Num Camunda local:
/// </para>
/// <code>
/// podman run -d --name xa-camunda -p 8080:8080 \
///     -e CAMUNDA_DATA_SECONDARYSTORAGE_TYPE=rdbms \
///     -e CAMUNDA_DATA_SECONDARYSTORAGE_RDBMS_URL="jdbc:h2:mem:xa;DB_CLOSE_DELAY=-1;MODE=PostgreSQL" \
///     -e CAMUNDA_DATA_SECONDARYSTORAGE_RDBMS_USERNAME=sa -e CAMUNDA_DATA_SECONDARYSTORAGE_RDBMS_FLUSHINTERVAL=PT0S \
///     -e CAMUNDA_SECURITY_AUTHENTICATION_UNPROTECTEDAPI=false \
///     -e CAMUNDA_SECURITY_INITIALIZATION_USERS_0_USERNAME=xa -e CAMUNDA_SECURITY_INITIALIZATION_USERS_0_PASSWORD=xa-senha \
///     -e CAMUNDA_SECURITY_INITIALIZATION_USERS_0_NAME=xa -e CAMUNDA_SECURITY_INITIALIZATION_USERS_0_EMAIL=xa@localhost \
///     docker.io/camunda/camunda:8.10.4
/// </code>
///
/// <para>
/// Pode rodar num cluster compartilhado: cada teste só enxerga a própria instância, e os job types
/// (<c>xa-cobrar</c>, <c>xa-despachar</c>) não devem ter worker de verdade nele.
/// </para>
/// </summary>
[Trait("Category", "Camunda")]
[Trait("Component", "Integration")]
[Trait("Requires", "Camunda")]
public class IdaEVoltaNoCamundaTests
{
	private const string Pedido = "CamundaTests/bpmn/xa-pedido.bpmn";
	private const string Envio = "CamundaTests/bpmn/xa-envio.bpmn";

	[Fact(DisplayName = "Camunda: an order deployed by process id runs end to end, with the payment and the child process's dispatch mocked")]
	public async Task Order_Runs_End_To_End()
	{
		await ScenarioDsl.Given()
			.Deploy("xa-pedido", "xa-envio")
			.MockJobWorker("xa-cobrar").ThenComplete(new { pago = true })
			.MockJobWorker("xa-despachar").ThenComplete(new { rastreio = "BR-1" })
			.StartInstance("xa-pedido", new { pedidoId = 1 })
			.ExecuteAsync()
			.AssertSuccess()
			.AssertInstanceCompleted()
			.AssertCompletedElements("cobrar", "chamar-envio", "concluido")
			.AssertVariable("codigoRastreio", "BR-1")
			.AssertVariable("pago", true)
			.AssertJobMocked("xa-cobrar")
			.AssertJobMocked("xa-despachar")
			.AssertNoIncidents();
	}

	[Fact(DisplayName = "Camunda: a refused payment takes the error path, and the dispatch is never called")]
	public async Task Refused_Payment_Takes_The_Error_Path()
	{
		await ScenarioDsl.Given()
			.Deploy(Pedido, Envio)
			.MockJobWorker("xa-cobrar").ThenThrowBpmnError("PAGAMENTO_RECUSADO", "sem saldo")
			.MockJobWorker("xa-despachar").ThenComplete(new { rastreio = "nunca" })
			.StartInstance("xa-pedido", new { pedidoId = 2 })
			.ExecuteAsync()
			.AssertInstanceCompleted()
			.AssertCompletedElements("pagamento-recusado", "recusado")
			.AssertJobMocked("xa-cobrar")
			.AssertJobMocked("xa-despachar", 0);
	}

	[Fact(DisplayName = "Camunda: parallel instances mocking the same job types each get their own answer")]
	public async Task Parallel_Instances_Are_Isolated()
	{
		var execucoes = Enumerable.Range(1, 3).Select(async n =>
		{
			var execucao = await ScenarioDsl.Given()
				.Deploy(Pedido, Envio)
				.MockJobWorker("xa-cobrar").ThenComplete(new { pago = true })
				.MockJobWorker("xa-despachar").ThenComplete(new { rastreio = $"BR-P{n}" })
				.StartInstance("xa-pedido", new { pedidoId = 300 + n })
				.ExecuteAsync();

			execucao
				.AssertInstanceCompleted()
				.AssertVariable("codigoRastreio", $"BR-P{n}")
				.AssertJobMocked("xa-despachar", 1);
		});

		await Task.WhenAll(execucoes);
	}

	[Fact(DisplayName = "Camunda: an instance waiting on a job nobody mocked fails saying which job")]
	public async Task Unmocked_Job_Is_Named()
	{
		var cenario = ScenarioDsl.Given();
		var settings = CamundaSettings.Load();
		settings.AssertionTimeoutSeconds = 3;
		cenario.Context.SetProperty(CamundaSettings.ChaveNoContexto, settings);

		var execucao = await cenario
			.Deploy(Pedido, Envio)
			.MockJobWorker("xa-cobrar").ThenComplete(new { pago = true })
			.StartInstance("xa-pedido", new { pedidoId = 4 })
			.ExecuteAsync();

		var falha = Should.Throw<ShouldAssertException>(() => execucao.AssertInstanceCompleted());

		falha.Message.ShouldContain("to complete within 3s, but it is ACTIVE");
		falha.Message.ShouldContain("Jobs waiting for a worker (not mocked in this test): 'xa-despachar' at 'despachar'");
	}

	// ---------- A pessoa e o relógio ----------

	[Fact(DisplayName = "Camunda: an intermediate timer, a user task and a timer after an event-based gateway, all answered by the test")]
	public async Task User_Task_And_Timers()
	{
		await ScenarioDsl.Given()
			.Deploy("xa-aprovacao")
			.SkipTimer("esperar-analise")
			.MockUserTask("aprovar").ThenComplete(new { aprovado = true })
			.SkipTimer("sem-confirmacao")
			.StartInstance("xa-aprovacao", new { pedidoId = $"P-{Guid.NewGuid():N}" })
			.ExecuteAsync()
			.AssertInstanceCompleted()
			.AssertCompletedElements("aprovar", "desistiu")
			.AssertVariable("aprovado", true)
			.AssertTimerSkipped("esperar-analise")
			.AssertTimerSkipped("sem-confirmacao")
			.AssertUserTaskMocked("aprovar");
	}

	[Fact(DisplayName = "Camunda: an interrupting boundary timer ends the user task and takes the expiry path")]
	public async Task Boundary_Timer_Expires_The_Task()
	{
		await ScenarioDsl.Given()
			.Deploy("xa-aprovacao")
			.SkipTimer("esperar-analise")
			.SkipTimer("prazo-aprovacao")
			.StartInstance("xa-aprovacao", new { pedidoId = $"P-{Guid.NewGuid():N}" })
			.ExecuteAsync()
			.AssertInstanceCompleted()
			.AssertCompletedElements("expirou")
			.AssertTimerSkipped("prazo-aprovacao");
	}

	// ---------- O processo filho mockado ----------

	[Fact(DisplayName = "Camunda: the child process is mocked, and the call activity's output mapping still runs")]
	public async Task Child_Process_Is_Mocked()
	{
		await ScenarioDsl.Given()
			.Deploy("xa-pedido", "xa-envio")
			.MockJobWorker("xa-cobrar").ThenComplete(new { pago = true })
			.MockChildProcess("chamar-envio").ThenComplete(new { rastreio = "BR-FILHO" })
			.StartInstance("xa-pedido", new { pedidoId = 9 })
			.ExecuteAsync()
			.AssertInstanceCompleted()
			.AssertCompletedElements("chamar-envio", "concluido")
			.AssertVariable("codigoRastreio", "BR-FILHO")
			.AssertChildProcessMocked("chamar-envio")
			.AssertJobMocked("xa-cobrar");
	}

	[Fact(DisplayName = "Camunda: a mocked child throwing a BPMN error takes the call activity's boundary error path")]
	public async Task Child_Process_Throws()
	{
		await ScenarioDsl.Given()
			.Deploy("xa-pedido", "xa-envio")
			.MockJobWorker("xa-cobrar").ThenComplete(new { pago = true })
			.MockChildProcess("chamar-envio").ThenThrowBpmnError("ENVIO_FALHOU", new { motivo = "transportadora fora do ar" })
			.StartInstance("xa-pedido", new { pedidoId = 10 })
			.ExecuteAsync()
			.AssertInstanceCompleted()
			.AssertCompletedElements("nao-enviado")
			.AssertVariable("motivo", "transportadora fora do ar")
			.AssertChildProcessMocked("chamar-envio");
	}

	// ---------- Incidents ----------

	[Fact(DisplayName = "Camunda: a worker that gives up opens an incident, and the test sees it")]
	public async Task Failed_Job_Opens_An_Incident()
	{
		await ScenarioDsl.Given()
			.Deploy("xa-pedido", "xa-envio")
			.MockJobWorker("xa-cobrar").ThenFail("gateway de pagamento fora do ar")
			.StartInstance("xa-pedido", new { pedidoId = 7 })
			.ExecuteAsync()
			.AssertIncident("cobrar", "JOB_NO_RETRIES", "fora do ar")
			.AssertInstanceActive();
	}

	[Fact(DisplayName = "Camunda: a BPMN error no boundary catches opens an incident of its own type")]
	public async Task Unhandled_Bpmn_Error_Opens_An_Incident()
	{
		await ScenarioDsl.Given()
			.Deploy("xa-pedido", "xa-envio")
			.MockJobWorker("xa-cobrar").ThenThrowBpmnError("CODIGO_QUE_NINGUEM_PEGA")
			.StartInstance("xa-pedido", new { pedidoId = 8 })
			.ExecuteAsync()
			.AssertIncident("cobrar", "UNHANDLED_ERROR_EVENT");
	}

	// ---------- O que outros sistemas mandam ----------

	[Fact(DisplayName = "Camunda: a message correlated with the instance's key and a signal triggered only in the instance")]
	public async Task Message_And_Signal()
	{
		await ScenarioDsl.Given()
			.Deploy("xa-entrega")
			.MockMessage("xa-pagamento-confirmado").ThenCorrelate(new { valor = 99 })
			.MockSignal("xa-estoque-liberado").ThenTrigger(new { lote = "L-1" })
			.MockUserTask("conferir").ThenComplete()
			.StartInstance("xa-entrega", new { pedidoId = $"P-{Guid.NewGuid():N}" })
			.ExecuteAsync()
			.AssertInstanceCompleted()
			.AssertCompletedElements("aguardar-pagamento", "conferir", "entregue")
			.AssertVariable("valor", 99)
			.AssertVariable("lote", "L-1")
			.AssertMessageCorrelated("xa-pagamento-confirmado")
			.AssertSignalTriggered("xa-estoque-liberado");
	}

	[Fact(DisplayName = "Camunda: a boundary message cancels the user task while it waits")]
	public async Task Boundary_Message_Cancels()
	{
		await ScenarioDsl.Given()
			.Deploy("xa-entrega")
			.MockMessage("xa-pagamento-confirmado").ThenCorrelate()
			.MockSignal("xa-estoque-liberado").ThenTrigger()
			.MockMessage("xa-cliente-cancelou").ThenCorrelate(new { motivo = "desistiu" })
			.StartInstance("xa-entrega", new { pedidoId = $"P-{Guid.NewGuid():N}" })
			.ExecuteAsync()
			.AssertInstanceCompleted()
			.AssertCompletedElements("cliente-cancelou", "cancelado")
			.AssertVariable("motivo", "desistiu")
			.AssertMessageCorrelated("xa-cliente-cancelou");
	}

	// ---------- O QA: o que já está publicado no cluster ----------

	[Fact(DisplayName = "Camunda (QA): with no Deploy in the chain, the version already on the cluster runs, by tag or the latest")]
	public async Task Published_Version_Runs_Without_Deploy()
	{
		// A publicação, como o pipeline faria antes dos testes do QA.
		var publicado = (await ScenarioDsl.Given().Deploy("xa-pedido", "xa-envio").ExecuteAsync())
			.GetResult().DeployedProcesses.Single(p => p.ProcessId == "xa-pedido");

		var porRotulo = await AteAparecerNaBusca(() => ScenarioDsl.Given()
			.MockJobWorker("xa-cobrar").ThenComplete(new { pago = true })
			.MockJobWorker("xa-despachar").ThenComplete(new { rastreio = "BR-QA" })
			.StartInstance("xa-pedido", new { pedidoId = 5 }, versionTag: "1.0.0")
			.ExecuteAsync());

		porRotulo.GetResult().VersionTag.ShouldBe("1.0.0");
		porRotulo.GetResult().Version.ShouldBe(publicado.Version);
		porRotulo.GetResult().DefinitionKey.ShouldBe(publicado.DefinitionKey);
		porRotulo
			.AssertInstanceCompleted()
			.AssertVariable("codigoRastreio", "BR-QA");

		var ultima = await AteAparecerNaBusca(() => ScenarioDsl.Given()
			.MockJobWorker("xa-cobrar").ThenThrowBpmnError("PAGAMENTO_RECUSADO")
			.StartInstance("xa-pedido", new { pedidoId = 6 })
			.ExecuteAsync());

		// Num cluster compartilhado, outra pessoa pode ter publicado depois: "a última" é no mínimo esta.
		ultima.GetResult().Version!.Value.ShouldBeGreaterThanOrEqualTo(publicado.Version);
		ultima.AssertInstanceCompleted();
	}

	// A busca de definições lê o armazenamento secundário, que fica um pouco atrás de um deploy
	// recém-feito. O QA não passa por isso: o que ele testa foi publicado bem antes.
	private static async Task<CamundaValidationBuilder> AteAparecerNaBusca(Func<Task<CamundaValidationBuilder>> cadeia)
	{
		var prazo = DateTime.UtcNow.AddSeconds(15);
		while (true)
		{
			var execucao = await cadeia();
			if (execucao.GetResult().Success || DateTime.UtcNow > prazo)
				return execucao;

			await Task.Delay(500);
		}
	}
}
