using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

using XUnitAssured.Camunda.Extensions;
using XUnitAssured.Camunda.Mocks;

namespace XUnitAssured.Tests.CamundaTests;

/// <summary>
/// A cadeia inteira contra um cluster falso: deploy, mocks, instância e asserções.
///
/// <para>
/// O que se prova aqui é o contrato das asserções: esperam o estado aparecer, desistem cedo
/// quando ele não tem mais como aparecer, e dizem na falha o que a instância está esperando.
/// </para>
/// </summary>
[Trait("Category", "Camunda")]
[Trait("Component", "Assertions")]
public class AssercoesDoCamundaTests
{
	private const string Pedido = "CamundaTests/bpmn/xa-pedido.bpmn";
	private const string Envio = "CamundaTests/bpmn/xa-envio.bpmn";

	[Fact(DisplayName = "The chain deploys, starts by the deployed definition and the mock completes the instance")]
	public async Task Happy_Path()
	{
		var cluster = new ClusterFalso
		{
			AoCriarInstancia = (c, chave) => c.NovoJob(chave, "xa-cobrar", "cobrar")
		};

		await cluster.Cenario()
			.Deploy(Pedido, Envio)
			.MockJobWorker("xa-cobrar").ThenComplete(new { pago = true })
			.StartInstance("xa-pedido", new { pedidoId = 7 })
			.ExecuteAsync()
			.AssertSuccess()
			.AssertInstanceCompleted()
			.AssertCompletedElements("cobrar")
			.AssertVariable("pago", true)
			.AssertVariable("pedidoId", 7)
			.AssertJobMocked("xa-cobrar")
			.AssertNoIncidents();

		cluster.Comandos.ShouldContain("deploy xa-pedido.bpmn,xa-envio.bpmn");
		cluster.Comandos.ShouldContain("create def-xa-pedido");
	}

	[Fact(DisplayName = "AssertVariable compares as JSON, so an object variable is checked by value")]
	public async Task Variable_Compares_As_Json()
	{
		var cluster = new ClusterFalso
		{
			AoCriarInstancia = (c, chave) => c.Variavel(chave, "cliente", new { nome = "Ana", itens = new[] { 1, 2 } })
		};

		var execucao = await cluster.Cenario().Deploy(Pedido, Envio).StartInstance("xa-pedido").ExecuteAsync();

		execucao.AssertVariable("cliente", new { nome = "Ana", itens = new[] { 1, 2 } });
		execucao.AssertVariable<Cliente>("cliente", c => c.Nome.ShouldBe("Ana"));
		Falha(() => execucao.AssertVariable("cliente", new { nome = "Bia" })).Message.ShouldContain("\"Ana\"");
	}

	[Fact(DisplayName = "An instance that was terminated fails AssertInstanceCompleted at once, without waiting the timeout")]
	public async Task Terminated_Fails_Fast()
	{
		var cluster = new ClusterFalso { AoCriarInstancia = (c, chave) => c.Estado(chave, "TERMINATED") };
		var execucao = await cluster.Cenario(ClusterFalso.Rapido(prazoSegundos: 30))
			.Deploy(Pedido, Envio).StartInstance("xa-pedido").ExecuteAsync();

		var cronometro = Stopwatch.StartNew();
		var falha = Falha(() => execucao.AssertInstanceCompleted());

		cronometro.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(5));
		falha.Message.ShouldContain("it was terminated");
		falha.Message.ShouldNotContain("within");
	}

	[Fact(DisplayName = "An instance with an incident fails at once and the message shows the incident")]
	public async Task Incident_Fails_Fast()
	{
		var cluster = new ClusterFalso { AoCriarInstancia = (c, chave) => c.Estado(chave, "ACTIVE", comIncident: true) };
		var execucao = await cluster.Cenario(ClusterFalso.Rapido(prazoSegundos: 30))
			.Deploy(Pedido, Envio).StartInstance("xa-pedido").ExecuteAsync();

		var falha = Falha(() => execucao.AssertInstanceCompleted());

		falha.Message.ShouldContain("open incident");
		falha.Message.ShouldContain("JOB_NO_RETRIES");
	}

	[Fact(DisplayName = "When the instance does not move, the failure says which job has no mock and where to see it in Operate")]
	public async Task Stuck_Instance_Explains_Why()
	{
		var cluster = new ClusterFalso { AoCriarInstancia = (c, chave) => c.NovoJob(chave, "xa-despachar", "despachar") };
		var execucao = await cluster.Cenario()
			.Deploy(Pedido, Envio)
			.MockJobWorker("xa-cobrar").ThenComplete()
			.StartInstance("xa-pedido")
			.ExecuteAsync();

		var falha = Falha(() => execucao.AssertInstanceCompleted());
		var chave = execucao.GetResult().ProcessInstanceKey;

		falha.Message.ShouldContain($"Expected process instance {chave} (xa-pedido version 1) to complete within 1s, but it is ACTIVE");
		falha.Message.ShouldContain("Waiting at: 'despachar'");
		falha.Message.ShouldContain("Jobs waiting for a worker (not mocked in this test): 'xa-despachar' at 'despachar'");
		falha.Message.ShouldContain($"Operate: http://camunda.falso:8080/operate/processes/{chave}");
	}

	[Fact(DisplayName = "A job leased by another worker fails the assertion at once, naming the cause")]
	public async Task Leased_Job_Fails_Fast()
	{
		var cluster = new ClusterFalso
		{
			AoCriarInstancia = (c, chave) => c.NovoJob(chave, "xa-cobrar", "cobrar", comLeaseDeOutroWorker: true)
		};
		var execucao = await cluster.Cenario(ClusterFalso.Rapido(prazoSegundos: 30))
			.Deploy(Pedido, Envio)
			.MockJobWorker("xa-cobrar").ThenComplete()
			.StartInstance("xa-pedido")
			.ExecuteAsync();

		var falha = Falha(() => execucao.AssertInstanceCompleted());

		falha.Message.ShouldContain("the mocks hit a problem");
		falha.Message.ShouldContain("held by another worker with a lease");
	}

	[Fact(DisplayName = "AssertActiveElements passes while the instance waits there")]
	public async Task Active_Elements()
	{
		var cluster = new ClusterFalso { AoCriarInstancia = (c, chave) => c.NovoJob(chave, "xa-despachar", "despachar") };

		await cluster.Cenario()
			.Deploy(Pedido, Envio)
			.StartInstance("xa-pedido")
			.ExecuteAsync()
			.AssertInstanceActive()
			.AssertActiveElements("despachar");
	}

	[Fact(DisplayName = "AssertCompletedElements gives up at once when the instance finished without the element")]
	public async Task Missing_Element_On_Finished_Instance_Fails_Fast()
	{
		var cluster = new ClusterFalso { AoCriarInstancia = (c, chave) => c.Estado(chave, "COMPLETED") };
		var execucao = await cluster.Cenario(ClusterFalso.Rapido(prazoSegundos: 30))
			.Deploy(Pedido, Envio).StartInstance("xa-pedido").ExecuteAsync();

		Falha(() => execucao.AssertCompletedElements("cobrar"))
			.Message.ShouldContain("'cobrar' is not completed, and the instance is already COMPLETED");
	}

	[Fact(DisplayName = "AssertJobMocked counts what the mock resolved")]
	public async Task Job_Mocked_Counts()
	{
		var cluster = new ClusterFalso { AoCriarInstancia = (c, chave) => c.NovoJob(chave, "xa-cobrar", "cobrar") };
		var execucao = await cluster.Cenario()
			.Deploy(Pedido, Envio)
			.MockJobWorker("xa-cobrar").ThenComplete()
			.StartInstance("xa-pedido")
			.ExecuteAsync();

		execucao.AssertJobMocked("xa-cobrar", 1);
		Falha(() => execucao.AssertJobMocked("xa-cobrar", 2)).Message.ShouldContain("the mock ran 1 time(s)");
	}

	[Fact(DisplayName = "A chain that deploys refuses to start a process it did not deploy, instead of the cluster's version")]
	public async Task Start_Of_A_Process_Not_Deployed_Fails()
	{
		var cluster = new ClusterFalso();
		cluster.Publicar("xa-cobranca", 4);

		var execucao = await cluster.Cenario().Deploy(Envio).StartInstance("xa-cobranca").ExecuteAsync();

		execucao.AssertFailure();
		var erro = execucao.GetResult().Errors.Single();
		erro.ShouldContain("was not deployed in this chain (it deployed 'xa-envio')");
		erro.ShouldContain("use a chain without Deploy");
		Falha(() => execucao.AssertInstanceCompleted()).Message.ShouldContain("did not start a process instance");
		cluster.Comandos.ShouldNotContain(c => c.StartsWith("create"));
	}

	[Fact(DisplayName = "A missing BPMN file fails the deploy, and the start says the deploy is why")]
	public async Task Missing_File_Fails_The_Deploy()
	{
		var cluster = new ClusterFalso();

		var execucao = await cluster.Cenario().Deploy("nao-existe.bpmn").StartInstance("xa-pedido").ExecuteAsync();

		var erro = execucao.GetResult().Errors.Single();
		erro.ShouldContain("because the deployment failed");
		erro.ShouldContain("nao-existe.bpmn");
	}

	[Fact(DisplayName = "Asserting on the instance after a chain that only deployed is a clear mistake, not a null")]
	public async Task Assert_Without_Start_Is_Clear()
	{
		var cluster = new ClusterFalso();
		var execucao = await cluster.Cenario().Deploy(Pedido, Envio).ExecuteAsync();

		execucao.AssertSuccess();
		execucao.GetResult().DeployedProcesses.Select(p => p.ProcessId).ShouldBe(new[] { "xa-pedido", "xa-envio" });
		Should.Throw<InvalidOperationException>(() => execucao.AssertInstanceCompleted())
			.Message.ShouldContain("End the chain with StartInstance");
	}

	[Fact(DisplayName = "The started instance's mocks are kept in the context for the assertions")]
	public async Task Mocks_Are_In_The_Context()
	{
		var cluster = new ClusterFalso();
		var cenario = cluster.Cenario();

		var execucao = await cenario.Deploy(Pedido, Envio).StartInstance("xa-pedido").ExecuteAsync();

		cenario.Context.GetProperty<MocksDaInstancia>(MocksDaInstancia.ChaveNoContexto)!
			.ProcessInstanceKey.ShouldBe(execucao.GetResult().ProcessInstanceKey);
	}

	private sealed class Cliente
	{
		public string Nome { get; set; } = string.Empty;
	}

	private static ShouldAssertException Falha(Action assercao)
	{
		try
		{
			assercao();
		}
		catch (ShouldAssertException e)
		{
			return e;
		}

		throw new ShouldAssertException("The assertion was expected to fail, and it passed.");
	}
}
