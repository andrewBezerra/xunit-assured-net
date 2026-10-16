using System;
using System.Linq;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

using XUnitAssured.Camunda.Extensions;
using XUnitAssured.Camunda.Mocks;
using XUnitAssured.Core.DSL;

namespace XUnitAssured.Tests.CamundaTests;

/// <summary>
/// <c>MockUserTask</c> e <c>SkipTimer</c>: a pessoa e o relógio, as duas dependências de um processo
/// que um teste não pode esperar.
///
/// <para>
/// O timer não se pula adiantando o relógio, que é do cluster inteiro: o pacote modifica a
/// instância. Por isso o BPMN importa aqui. Os casos de <see cref="ModeloBpmn"/> são os que a sonda
/// contra a 8.10.4 mostrou: a borda e o timer depois de gateway de evento não aparecem como espera,
/// e ativar um gateway de junção o faz disparar sem os outros ramos.
/// </para>
/// </summary>
[Trait("Category", "Camunda")]
[Trait("Component", "Mocks")]
public class UserTaskETimerTests
{
	// ---------- O plano de cada tipo de timer ----------

	[Fact(DisplayName = "An intermediate timer is skipped by ending it and activating what follows")]
	public void Intermediate_Timer()
	{
		var plano = Modelo().PlanejarPulo("esperar-1h", out var recusa).ShouldNotBeNull();

		recusa.ShouldBeNull();
		plano.ElementoEsperando.ShouldBe("esperar-1h");
		plano.Encerrar.ShouldBeTrue();
		plano.Ativar.ShouldBe(new[] { "revisar" });
	}

	[Fact(DisplayName = "An interrupting boundary timer ends the activity it is attached to and takes its own path")]
	public void Interrupting_Boundary_Timer()
	{
		var plano = Modelo().PlanejarPulo("prazo-revisao", out _).ShouldNotBeNull();

		plano.ElementoEsperando.ShouldBe("revisar");
		plano.Encerrar.ShouldBeTrue();
		plano.Ativar.ShouldBe(new[] { "expirou" });
	}

	[Fact(DisplayName = "A non-interrupting boundary timer leaves the activity running")]
	public void Non_Interrupting_Boundary_Timer()
	{
		var plano = Modelo().PlanejarPulo("lembrete", out _).ShouldNotBeNull();

		plano.ElementoEsperando.ShouldBe("revisar");
		plano.Encerrar.ShouldBeFalse();
		plano.Ativar.ShouldBe(new[] { "lembrar" });
	}

	[Fact(DisplayName = "A timer after an event-based gateway ends the gateway, which drops the other events")]
	public void Timer_After_Event_Gateway()
	{
		var plano = Modelo().PlanejarPulo("sem-resposta", out _).ShouldNotBeNull();

		plano.ElementoEsperando.ShouldBe("aguardar");
		plano.Encerrar.ShouldBeTrue();
		plano.Ativar.ShouldBe(new[] { "desistiu" });
	}

	[Fact(DisplayName = "A timer that flows into a joining gateway is refused, saying why and what to do instead")]
	public void Timer_Into_A_Join_Is_Refused()
	{
		Modelo().PlanejarPulo("antes-da-juncao", out var recusa).ShouldBeNull();

		recusa.ShouldNotBeNull();
		recusa.ShouldContain("joining gateway 'junta'");
		recusa.ShouldContain("PT1S");
	}

	[Fact(DisplayName = "An element that is not a timer is refused, and one from another process is not this model's business")]
	public void Not_A_Timer()
	{
		Modelo().PlanejarPulo("revisar", out var recusa).ShouldBeNull();
		recusa!.ShouldContain("is a userTask, not a timer event");

		Modelo().PlanejarPulo("de-outro-processo", out var outra).ShouldBeNull();
		outra.ShouldBeNull();
	}

	[Fact(DisplayName = "A timer inside an embedded subprocess is activated in that subprocess, not in a new one")]
	public void Timer_In_A_Subprocess()
	{
		Modelo().PlanejarPulo("pausa", out _).ShouldNotBeNull().Escopo.ShouldBe("conferencia");
	}

	// ---------- A cadeia ----------

	[Fact(DisplayName = "MockUserTask completes the task with the form's answer, and AssertUserTaskMocked counts it")]
	public async Task User_Task_Is_Completed()
	{
		var cluster = Publicado(aoCriar: (c, chave) => c.NovaUserTask(chave, "revisar"));

		await cluster.Cenario()
			.MockUserTask("revisar").ThenComplete(new { aprovado = true })
			.StartInstance("pedido")
			.ExecuteAsync()
			.AssertInstanceCompleted()
			.AssertCompletedElements("revisar")
			.AssertVariable("aprovado", true)
			.AssertUserTaskMocked("revisar");
	}

	[Fact(DisplayName = "A user task inside a child process is reached too: the wait states search covers the whole tree")]
	public async Task User_Task_In_A_Child()
	{
		string? raiz = null;
		var cluster = Publicado(aoCriar: (c, chave) =>
		{
			raiz = chave;
			c.NovaUserTask(c.NovaInstancia("envio", chave), "conferir");
		});
		cluster.AoCompletarUserTask = (c, t) => { c.Estado(t.Instancia, "COMPLETED"); c.Estado(raiz!, "COMPLETED"); };

		await cluster.Cenario()
			.MockUserTask("conferir").ThenComplete()
			.StartInstance("pedido")
			.ExecuteAsync()
			.AssertInstanceCompleted()
			.AssertUserTaskMocked("conferir");
	}

	[Fact(DisplayName = "A user task nobody mocked is named in the failure")]
	public async Task Unmocked_User_Task_Is_Named()
	{
		var cluster = Publicado(aoCriar: (c, chave) => c.NovaUserTask(chave, "revisar"));

		var execucao = await cluster.Cenario().StartInstance("pedido").ExecuteAsync();

		Falha(() => execucao.AssertInstanceCompleted())
			.Message.ShouldContain("Waiting for (not answered in this test): user task 'revisar'");
	}

	[Fact(DisplayName = "SkipTimer ends the timer and activates the next element, and AssertTimerSkipped counts it")]
	public async Task Timer_Is_Skipped()
	{
		var cluster = Publicado(aoCriar: (c, chave) => c.Elemento(chave, "esperar-1h", "ACTIVE"));

		await cluster.Cenario()
			.SkipTimer("esperar-1h")
			.StartInstance("pedido")
			.ExecuteAsync()
			.AssertInstanceCompleted()
			.AssertTimerSkipped("esperar-1h");

		cluster.Modificacoes.Single().ShouldEndWith("-[pi-1:esperar-1h] +[revisar]");
	}

	[Fact(DisplayName = "A boundary timer is skipped when the activity it is attached to is active")]
	public async Task Boundary_Timer_Is_Skipped()
	{
		var cluster = Publicado(aoCriar: (c, chave) => c.NovaUserTask(chave, "revisar"));
		cluster.AoModificar = (c, chave) => c.Estado(chave, "COMPLETED");

		await cluster.Cenario()
			.SkipTimer("prazo-revisao")
			.StartInstance("pedido")
			.ExecuteAsync()
			.AssertInstanceCompleted()
			.AssertTimerSkipped("prazo-revisao");

		cluster.Modificacoes.Single().ShouldEndWith("-[pi-1:revisar] +[expirou]");
	}

	[Fact(DisplayName = "Skipping a timer into a joining gateway fails the assertion at once with the reason")]
	public async Task Join_Fails_Fast()
	{
		var cluster = Publicado(aoCriar: (c, chave) => c.Elemento(chave, "antes-da-juncao", "ACTIVE"));
		var cronometro = System.Diagnostics.Stopwatch.StartNew();

		var execucao = await cluster.Cenario(ClusterFalso.Rapido(prazoSegundos: 10))
			.SkipTimer("antes-da-juncao")
			.StartInstance("pedido")
			.ExecuteAsync();

		Falha(() => execucao.AssertInstanceCompleted()).Message.ShouldContain("joining gateway 'junta'");
		cronometro.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(8));
		cluster.Modificacoes.ShouldBeEmpty();
	}

	[Fact(DisplayName = "A timer the instance never reached is named in the failure as declared but never used")]
	public async Task Unused_Timer_Is_Named()
	{
		var cluster = Publicado(aoCriar: (c, chave) => c.NovaUserTask(chave, "revisar"));

		var execucao = await cluster.Cenario().SkipTimer("esperar-1h").StartInstance("pedido").ExecuteAsync();

		Falha(() => execucao.AssertInstanceCompleted())
			.Message.ShouldContain("Declared in this test but never used: timer 'esperar-1h'");
	}

	[Fact(DisplayName = "A user task mocked twice, a timer skipped twice, or an empty id is refused when the chain is written")]
	public void Bad_Declarations_Are_Refused()
	{
		Should.Throw<InvalidOperationException>(() =>
			ScenarioDsl.Given().MockUserTask("revisar").ThenComplete().MockUserTask("revisar").ThenComplete());
		Should.Throw<InvalidOperationException>(() => ScenarioDsl.Given().SkipTimer("t").SkipTimer("t"));
		Should.Throw<ArgumentException>(() => ScenarioDsl.Given().MockUserTask(" "));
		Should.Throw<ArgumentException>(() => ScenarioDsl.Given().SkipTimer(""));
	}

	// ---------- Apoio ----------

	private static ModeloBpmn Modelo() => ModeloBpmn.Ler(Pedido);

	// Um pedido publicado, sem Deploy na cadeia, com o BPMN que o pacote vai ler.
	private static ClusterFalso Publicado(Action<ClusterFalso, string> aoCriar)
	{
		var cluster = new ClusterFalso { AoCriarInstancia = aoCriar };
		var chave = cluster.Publicar("pedido", 1);
		cluster.DefinirBpmn(chave, Pedido);
		return cluster;
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

	private const string Pedido = """
		<?xml version="1.0" encoding="UTF-8"?>
		<bpmn:definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL" id="d">
		  <bpmn:process id="pedido" isExecutable="true">
		    <bpmn:startEvent id="inicio" />
		    <bpmn:intermediateCatchEvent id="esperar-1h"><bpmn:timerEventDefinition /></bpmn:intermediateCatchEvent>
		    <bpmn:userTask id="revisar" />
		    <bpmn:boundaryEvent id="prazo-revisao" attachedToRef="revisar"><bpmn:timerEventDefinition /></bpmn:boundaryEvent>
		    <bpmn:boundaryEvent id="lembrete" attachedToRef="revisar" cancelActivity="false"><bpmn:timerEventDefinition /></bpmn:boundaryEvent>
		    <bpmn:serviceTask id="lembrar" />
		    <bpmn:endEvent id="expirou" />
		    <bpmn:eventBasedGateway id="aguardar" />
		    <bpmn:intermediateCatchEvent id="resposta"><bpmn:messageEventDefinition /></bpmn:intermediateCatchEvent>
		    <bpmn:intermediateCatchEvent id="sem-resposta"><bpmn:timerEventDefinition /></bpmn:intermediateCatchEvent>
		    <bpmn:endEvent id="desistiu" />
		    <bpmn:intermediateCatchEvent id="antes-da-juncao"><bpmn:timerEventDefinition /></bpmn:intermediateCatchEvent>
		    <bpmn:serviceTask id="outro-ramo" />
		    <bpmn:parallelGateway id="junta" />
		    <bpmn:subProcess id="conferencia">
		      <bpmn:intermediateCatchEvent id="pausa"><bpmn:timerEventDefinition /></bpmn:intermediateCatchEvent>
		      <bpmn:serviceTask id="conferir" />
		      <bpmn:sequenceFlow id="s1" sourceRef="pausa" targetRef="conferir" />
		    </bpmn:subProcess>
		    <bpmn:endEvent id="fim" />
		    <bpmn:sequenceFlow id="f1" sourceRef="inicio" targetRef="esperar-1h" />
		    <bpmn:sequenceFlow id="f2" sourceRef="esperar-1h" targetRef="revisar" />
		    <bpmn:sequenceFlow id="f3" sourceRef="revisar" targetRef="fim" />
		    <bpmn:sequenceFlow id="f4" sourceRef="prazo-revisao" targetRef="expirou" />
		    <bpmn:sequenceFlow id="f5" sourceRef="lembrete" targetRef="lembrar" />
		    <bpmn:sequenceFlow id="f6" sourceRef="aguardar" targetRef="resposta" />
		    <bpmn:sequenceFlow id="f7" sourceRef="aguardar" targetRef="sem-resposta" />
		    <bpmn:sequenceFlow id="f8" sourceRef="sem-resposta" targetRef="desistiu" />
		    <bpmn:sequenceFlow id="f9" sourceRef="antes-da-juncao" targetRef="junta" />
		    <bpmn:sequenceFlow id="f10" sourceRef="outro-ramo" targetRef="junta" />
		  </bpmn:process>
		</bpmn:definitions>
		""";
}
