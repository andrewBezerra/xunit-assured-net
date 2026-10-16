using System;
using System.Diagnostics;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

using XUnitAssured.Camunda.Extensions;
using XUnitAssured.Core.DSL;

namespace XUnitAssured.Tests.CamundaTests;

/// <summary>
/// O caminho técnico: o worker que desiste (<c>ThenFail</c>) e o incident que ele abre
/// (<c>AssertIncident</c>). O de negócio, que uma borda pega, é o <c>ThenThrowBpmnError</c>.
/// </summary>
[Trait("Category", "Camunda")]
[Trait("Component", "Assertions")]
public class IncidentTests
{
	private const string Pedido = "CamundaTests/bpmn/xa-pedido.bpmn";
	private const string Envio = "CamundaTests/bpmn/xa-envio.bpmn";

	[Fact(DisplayName = "ThenFail fails the job with no retries left, and AssertIncident sees the incident it opens")]
	public async Task Fail_Opens_An_Incident()
	{
		var cluster = new ClusterFalso { AoCriarInstancia = (c, chave) => c.NovoJob(chave, "xa-cobrar", "cobrar") };

		await cluster.Cenario()
			.Deploy(Pedido, Envio)
			.MockJobWorker("xa-cobrar").ThenFail("gateway de pagamento fora do ar")
			.StartInstance("xa-pedido")
			.ExecuteAsync()
			.AssertIncident("cobrar")
			.AssertIncident("cobrar", "JOB_NO_RETRIES", "fora do ar")
			.AssertInstanceActive();

		cluster.Comandos.ShouldContain(c => c.StartsWith("fail "));
	}

	[Fact(DisplayName = "An incident at another element, or of another type, does not pass, and the failure lists the incidents")]
	public async Task Wrong_Incident_Is_Listed()
	{
		var cluster = new ClusterFalso { AoCriarInstancia = (c, chave) => c.NovoJob(chave, "xa-cobrar", "cobrar") };

		var execucao = await cluster.Cenario()
			.Deploy(Pedido, Envio)
			.MockJobWorker("xa-cobrar").ThenFail("sem saldo")
			.StartInstance("xa-pedido")
			.ExecuteAsync();

		var falha = Should.Throw<ShouldAssertException>(() => execucao.AssertIncident("despachar"));
		falha.Message.ShouldContain("to have an incident at 'despachar'");
		falha.Message.ShouldContain("its incidents are at 'cobrar': JOB_NO_RETRIES: sem saldo");

		Should.Throw<ShouldAssertException>(() => execucao.AssertIncident("cobrar", "UNHANDLED_ERROR_EVENT"));
	}

	[Fact(DisplayName = "AssertIncident gives up at once when the instance finished without the incident")]
	public async Task Gives_Up_When_The_Instance_Finished()
	{
		var cluster = new ClusterFalso { AoCriarInstancia = (c, chave) => c.NovoJob(chave, "xa-cobrar", "cobrar") };
		var execucao = await cluster.Cenario(ClusterFalso.Rapido(prazoSegundos: 10))
			.Deploy(Pedido, Envio)
			.MockJobWorker("xa-cobrar").ThenComplete()
			.StartInstance("xa-pedido")
			.ExecuteAsync()
			.AssertInstanceCompleted();
		var cronometro = Stopwatch.StartNew();

		Should.Throw<ShouldAssertException>(() => execucao.AssertIncident("cobrar"))
			.Message.ShouldContain("and the instance is already COMPLETED");
		cronometro.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(5));
	}

	[Fact(DisplayName = "ThenFail needs the message the incident will show")]
	public void Fail_Needs_A_Message() =>
		Should.Throw<ArgumentException>(() => ScenarioDsl.Given().MockJobWorker("xa-cobrar").ThenFail(" "));
}
