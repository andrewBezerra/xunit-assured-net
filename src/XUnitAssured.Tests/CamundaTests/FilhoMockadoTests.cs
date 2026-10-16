using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

using XUnitAssured.Camunda.Extensions;
using XUnitAssured.Camunda.Gateway;
using XUnitAssured.Camunda.Steps;
using XUnitAssured.Core.DSL;

namespace XUnitAssured.Tests.CamundaTests;

/// <summary>
/// <c>MockChildProcess</c>: o processo filho de outra equipe, que o teste não quer rodar.
///
/// <para>
/// O filho roda de verdade até ser interceptado; o pacote então o termina pelo evento de fim (forma
/// A do plano), e o mapeamento de saída da call activity roda como rodaria. Para um erro, o evento de
/// fim de erro do filho, ou a borda de erro da call activity no pai (forma B). E o aviso de
/// <c>bindingType="latest"</c>, que num cluster compartilhado chama o filho de outra pessoa.
/// </para>
/// </summary>
[Trait("Category", "Camunda")]
[Trait("Component", "Mocks")]
public class FilhoMockadoTests
{
	[Fact(DisplayName = "MockChildProcess ends the child at its end event with the test's variables")]
	public async Task Child_Ends_At_Its_End_Event()
	{
		var cluster = Cluster();

		await cluster.Cenario()
			.MockChildProcess("chamar-envio").ThenComplete(new { rastreio = "BR-9" })
			.StartInstance("pedido")
			.ExecuteAsync()
			.AssertInstanceCompleted()
			.AssertChildProcessMocked("chamar-envio");

		cluster.Modificacoes.Single().ShouldBe("pi-2 -[pi-2:despachar] +[fim]");
	}

	[Fact(DisplayName = "ThenThrowBpmnError ends the child at its error end event with that code")]
	public async Task Error_At_The_Child_Error_End_Event()
	{
		var cluster = Cluster();

		await cluster.Cenario()
			.MockChildProcess("chamar-envio").ThenThrowBpmnError("TRANSPORTADORA_FORA")
			.StartInstance("pedido")
			.ExecuteAsync()
			.AssertChildProcessMocked("chamar-envio");

		cluster.Modificacoes.Single().ShouldBe("pi-2 -[pi-2:despachar] +[fim-erro]");
	}

	[Fact(DisplayName = "Without an error end event for the code, the call activity ends and takes its boundary error path")]
	public async Task Error_By_The_Boundary_In_The_Parent()
	{
		var cluster = Cluster();

		await cluster.Cenario()
			.MockChildProcess("chamar-envio").ThenThrowBpmnError("ENVIO_FALHOU")
			.StartInstance("pedido")
			.ExecuteAsync()
			.AssertInstanceCompleted()
			.AssertChildProcessMocked("chamar-envio");

		cluster.Modificacoes.Single().ShouldBe("pi-1 -[pi-1:chamar-envio] +[reembolsar]");
	}

	[Fact(DisplayName = "An error code with no path in the child or the parent fails the test, saying why")]
	public async Task Error_With_No_Path_Fails()
	{
		var cluster = Cluster();

		var execucao = await cluster.Cenario(ClusterFalso.Rapido(prazoSegundos: 10))
			.MockChildProcess("chamar-envio").ThenThrowBpmnError("NINGUEM_PEGA")
			.StartInstance("pedido")
			.ExecuteAsync();

		Should.Throw<ShouldAssertException>(() => execucao.AssertInstanceCompleted())
			.Message.ShouldContain("has no error end event with that code, and the call activity has no boundary error event");
		cluster.Modificacoes.ShouldBeEmpty();
	}

	[Fact(DisplayName = "A child that finished before the mock found it fails the test: it ran for real")]
	public async Task Child_Too_Fast_Fails()
	{
		var cluster = Cluster(filhaJaTerminada: true);

		var execucao = await cluster.Cenario(ClusterFalso.Rapido(prazoSegundos: 10))
			.MockChildProcess("chamar-envio").ThenComplete()
			.StartInstance("pedido")
			.ExecuteAsync();

		Should.Throw<ShouldAssertException>(() => execucao.AssertChildProcessMocked("chamar-envio"))
			.Message.ShouldContain("was already COMPLETED when the mock found it, so it ran for real");
	}

	[Fact(DisplayName = "A child of a call activity nobody mocked runs untouched")]
	public async Task Unmocked_Call_Activity_Is_Left_Alone()
	{
		var cluster = Cluster();

		var execucao = await cluster.Cenario()
			.MockChildProcess("outra-chamada").ThenComplete()
			.StartInstance("pedido")
			.ExecuteAsync();

		Should.Throw<ShouldAssertException>(() => execucao.AssertInstanceCompleted())
			.Message.ShouldContain("Declared in this test but never used: child process of 'outra-chamada'");
		cluster.Modificacoes.ShouldBeEmpty();
	}

	[Fact(DisplayName = "A call activity with bindingType latest, or none, becomes a warning on the deploy and in the failure")]
	public async Task Latest_Binding_Is_A_Warning()
	{
		var arquivo = Path.Combine(Path.GetTempPath(), $"xa-latest-{Guid.NewGuid():N}.bpmn");
		File.WriteAllText(arquivo, Pai.Replace("bindingType=\"deployment\"", ""));
		try
		{
			var cluster = new ClusterFalso();
			var execucao = await cluster.Cenario().Deploy(arquivo).StartInstance("pedido").ExecuteAsync();

			var aviso = execucao.GetResult().Warnings.Single();
			aviso.ShouldContain("Call activity 'chamar-envio'");
			aviso.ShouldContain("calls 'envio' with bindingType \"latest\" (the default)");
			aviso.ShouldContain("Use bindingType=\"deployment\"");

			Should.Throw<ShouldAssertException>(() => execucao.AssertInstanceCompleted())
				.Message.ShouldContain("Warning: Call activity 'chamar-envio'");
		}
		finally
		{
			File.Delete(arquivo);
		}
	}

	[Fact(DisplayName = "bindingType deployment, versionTag, or a file that is not BPMN, gives no warning")]
	public void No_Warning_When_Bound()
	{
		CamundaDeployStep.AvisosDe(new RecursoBpmn("pedido.bpmn", Encoding.UTF8.GetBytes(Pai))).ShouldBeEmpty();
		CamundaDeployStep.AvisosDe(new RecursoBpmn("pedido.bpmn",
			Encoding.UTF8.GetBytes(Pai.Replace("bindingType=\"deployment\"", "bindingType=\"versionTag\"")))).ShouldBeEmpty();
		CamundaDeployStep.AvisosDe(new RecursoBpmn("regras.dmn", Encoding.UTF8.GetBytes("<nada/>"))).ShouldBeEmpty();
	}

	[Fact(DisplayName = "A call activity mocked twice, or with no id or error code, is refused when the chain is written")]
	public void Bad_Declarations_Are_Refused()
	{
		Should.Throw<InvalidOperationException>(() => ScenarioDsl.Given()
			.MockChildProcess("chamar-envio").ThenComplete().MockChildProcess("chamar-envio").ThenComplete());
		Should.Throw<ArgumentException>(() => ScenarioDsl.Given().MockChildProcess(" "));
		Should.Throw<ArgumentException>(() => ScenarioDsl.Given().MockChildProcess("chamar-envio").ThenThrowBpmnError(""));
	}

	// ---------- Apoio ----------

	// O pedido chama o envio em chamar-envio; o envio espera em despachar (um job ainda aberto).
	private static ClusterFalso Cluster(bool filhaJaTerminada = false)
	{
		var cluster = new ClusterFalso
		{
			AoCriarInstancia = (c, chave) =>
			{
				var filha = c.NovaInstancia("envio", chave, "chamar-envio", "def-envio");
				if (filhaJaTerminada)
					c.Estado(filha, "COMPLETED");
				else
					c.Elemento(filha, "despachar", "ACTIVE");
			},
			// O motor: a filha que termina devolve o controle ao pai, que segue até o fim.
			AoModificar = (c, chave) =>
			{
				c.Estado(chave, "COMPLETED");
				c.Estado("pi-1", "COMPLETED");
			}
		};

		cluster.DefinirBpmn(cluster.Publicar("pedido", 1), Pai);
		cluster.DefinirBpmn("def-envio", Filho);
		return cluster;
	}

	private const string Pai = """
		<?xml version="1.0" encoding="UTF-8"?>
		<bpmn:definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL" xmlns:zeebe="http://camunda.org/schema/zeebe/1.0" id="d">
		  <bpmn:error id="Erro_EnvioFalhou" errorCode="ENVIO_FALHOU" />
		  <bpmn:process id="pedido" isExecutable="true">
		    <bpmn:callActivity id="chamar-envio">
		      <bpmn:extensionElements><zeebe:calledElement processId="envio" bindingType="deployment" /></bpmn:extensionElements>
		    </bpmn:callActivity>
		    <bpmn:boundaryEvent id="envio-falhou" attachedToRef="chamar-envio"><bpmn:errorEventDefinition errorRef="Erro_EnvioFalhou" /></bpmn:boundaryEvent>
		    <bpmn:serviceTask id="reembolsar" />
		    <bpmn:endEvent id="concluido" />
		    <bpmn:sequenceFlow id="f1" sourceRef="chamar-envio" targetRef="concluido" />
		    <bpmn:sequenceFlow id="f2" sourceRef="envio-falhou" targetRef="reembolsar" />
		  </bpmn:process>
		</bpmn:definitions>
		""";

	private const string Filho = """
		<?xml version="1.0" encoding="UTF-8"?>
		<bpmn:definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL" id="d">
		  <bpmn:error id="Erro_Transportadora" errorCode="TRANSPORTADORA_FORA" />
		  <bpmn:process id="envio" isExecutable="true">
		    <bpmn:startEvent id="inicio" />
		    <bpmn:serviceTask id="despachar" />
		    <bpmn:endEvent id="fim-erro"><bpmn:errorEventDefinition errorRef="Erro_Transportadora" /></bpmn:endEvent>
		    <bpmn:endEvent id="fim" />
		  </bpmn:process>
		</bpmn:definitions>
		""";
}
