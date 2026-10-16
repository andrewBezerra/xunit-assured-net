using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

using XUnitAssured.Camunda.Extensions;
using XUnitAssured.Camunda.Mocks;
using XUnitAssured.Core.DSL;

namespace XUnitAssured.Tests.CamundaTests;

/// <summary>
/// <c>MockMessage</c> e <c>MockSignal</c>: o que outro sistema mandaria para o processo.
///
/// <para>
/// A mensagem é correlacionada de verdade, mas o Camunda a entrega a quem assinou primeiro, em
/// qualquer instância do cluster: o pacote confere antes que ninguém de fora do teste espera a
/// mesma mensagem com a mesma chave. O sinal não vira broadcast (decisão do usuário): o pacote o
/// dispara só na instância, modificando-a, com as regras do <c>SkipTimer</c>.
/// </para>
/// </summary>
[Trait("Category", "Camunda")]
[Trait("Component", "Mocks")]
public class MensagemESinalTests
{
	// ---------- O plano do sinal ----------

	[Fact(DisplayName = "A signal is answered wherever the process waits for it, by the signal's name")]
	public void Signal_Plans_By_Name()
	{
		var recusas = new List<string>();

		var planos = Modelo().PlanejarSinal("liberado", recusas);

		recusas.ShouldBeEmpty();
		planos.Select(p => p.EventoId).ShouldBe(new[] { "aguardar-liberacao", "pausa" }, ignoreOrder: true);

		var intermediario = planos.Single(p => p.EventoId == "aguardar-liberacao");
		intermediario.ElementoEsperando.ShouldBe("aguardar-liberacao");
		intermediario.Ativar.ShouldBe(new[] { "seguir" });

		var borda = planos.Single(p => p.EventoId == "pausa");
		borda.ElementoEsperando.ShouldBe("seguir");
		borda.Encerrar.ShouldBeFalse();
	}

	[Fact(DisplayName = "A signal event that flows into a joining gateway is refused, with what to change in the diagram")]
	public void Signal_Into_A_Join_Is_Refused()
	{
		var recusas = new List<string>();

		Modelo().PlanejarSinal("fechar", recusas).ShouldBeEmpty();

		recusas.Single().ShouldContain("MockSignal('fechar') at 'fechamento'");
		recusas.Single().ShouldContain("Put an element");
	}

	// ---------- Mensagens ----------

	[Fact(DisplayName = "MockMessage correlates the message with the key the subscription carries, and its variables")]
	public async Task Message_Is_Correlated()
	{
		var cluster = Publicado(aoCriar: (c, chave) => c.NovaAssinatura(chave, "aguardar-pagamento", "pago", "P-1"));

		await cluster.Cenario()
			.MockMessage("pago").ThenCorrelate(new { valor = 10 })
			.StartInstance("pedido")
			.ExecuteAsync()
			.AssertInstanceCompleted()
			.AssertVariable("valor", 10)
			.AssertMessageCorrelated("pago");

		cluster.Comandos.ShouldContain("correlate pago P-1");
	}

	[Fact(DisplayName = "A message another instance also awaits with the same key is not correlated, and the test fails saying why")]
	public async Task Shared_Correlation_Key_Is_Refused()
	{
		var cluster = Publicado(aoCriar: (c, chave) => c.NovaAssinatura(chave, "aguardar-pagamento", "pago", "P-1"));
		var deOutroTeste = cluster.NovaInstancia("pedido");
		cluster.NovaAssinatura(deOutroTeste, "aguardar-pagamento", "pago", "P-1");

		var execucao = await cluster.Cenario(ClusterFalso.Rapido(prazoSegundos: 10))
			.MockMessage("pago").ThenCorrelate()
			.StartInstance("pedido")
			.ExecuteAsync();

		var falha = Falha(() => execucao.AssertInstanceCompleted());
		falha.Message.ShouldContain($"also awaited by instance {deOutroTeste}, outside this test");
		falha.Message.ShouldContain("correlation key unique to the test");
		cluster.Comandos.ShouldNotContain(c => c.StartsWith("correlate"));
	}

	[Fact(DisplayName = "A message awaited inside a child process is answered too")]
	public async Task Message_In_A_Child()
	{
		string? raiz = null;
		var cluster = Publicado(aoCriar: (c, chave) =>
		{
			raiz = chave;
			c.NovaAssinatura(c.NovaInstancia("envio", chave), "aguardar-coleta", "coletado", "C-9");
		});
		cluster.AoCorrelacionar = (c, a) => { c.Estado(a.Instancia, "COMPLETED"); c.Estado(raiz!, "COMPLETED"); };

		await cluster.Cenario()
			.MockMessage("coletado").ThenCorrelate()
			.StartInstance("pedido")
			.ExecuteAsync()
			.AssertInstanceCompleted()
			.AssertMessageCorrelated("coletado");
	}

	[Fact(DisplayName = "A message nobody mocked is named in the failure, with its correlation key")]
	public async Task Unmocked_Message_Is_Named()
	{
		var cluster = Publicado(aoCriar: (c, chave) => c.NovaAssinatura(chave, "aguardar-pagamento", "pago", "P-7"));

		var execucao = await cluster.Cenario().StartInstance("pedido").ExecuteAsync();

		Falha(() => execucao.AssertInstanceCompleted())
			.Message.ShouldContain("message 'pago' with correlation key 'P-7' at 'aguardar-pagamento'");
	}

	// ---------- Sinais ----------

	[Fact(DisplayName = "MockSignal triggers the signal only in the instance, by modification, with its variables")]
	public async Task Signal_Is_Triggered_In_The_Instance()
	{
		var cluster = Publicado(aoCriar: (c, chave) => c.Elemento(chave, "aguardar-liberacao", "ACTIVE"));

		await cluster.Cenario()
			.MockSignal("liberado").ThenTrigger(new { liberadoPor = "teste" })
			.StartInstance("pedido")
			.ExecuteAsync()
			.AssertInstanceCompleted()
			.AssertVariable("liberadoPor", "teste")
			.AssertSignalTriggered("liberado");

		cluster.Modificacoes.Single().ShouldEndWith("-[pi-1:aguardar-liberacao] +[seguir]");
	}

	[Fact(DisplayName = "A message or signal mocked twice, or with no name, is refused when the chain is written")]
	public void Bad_Declarations_Are_Refused()
	{
		Should.Throw<InvalidOperationException>(() =>
			ScenarioDsl.Given().MockMessage("pago").ThenCorrelate().MockMessage("pago").ThenCorrelate());
		Should.Throw<InvalidOperationException>(() =>
			ScenarioDsl.Given().MockSignal("s").ThenTrigger().MockSignal("s").ThenTrigger());
		Should.Throw<ArgumentException>(() => ScenarioDsl.Given().MockMessage(" "));
		Should.Throw<ArgumentException>(() => ScenarioDsl.Given().MockSignal(""));
	}

	// ---------- Apoio ----------

	private static ModeloBpmn Modelo() => ModeloBpmn.Ler(Pedido);

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
		  <bpmn:signal id="Sinal_Liberado" name="liberado" />
		  <bpmn:signal id="Sinal_Fechar" name="fechar" />
		  <bpmn:process id="pedido" isExecutable="true">
		    <bpmn:intermediateCatchEvent id="aguardar-liberacao"><bpmn:signalEventDefinition signalRef="Sinal_Liberado" /></bpmn:intermediateCatchEvent>
		    <bpmn:serviceTask id="seguir" />
		    <bpmn:boundaryEvent id="pausa" attachedToRef="seguir" cancelActivity="false"><bpmn:signalEventDefinition signalRef="Sinal_Liberado" /></bpmn:boundaryEvent>
		    <bpmn:serviceTask id="anotar" />
		    <bpmn:intermediateCatchEvent id="fechamento"><bpmn:signalEventDefinition signalRef="Sinal_Fechar" /></bpmn:intermediateCatchEvent>
		    <bpmn:serviceTask id="outro" />
		    <bpmn:parallelGateway id="junta" />
		    <bpmn:sequenceFlow id="f1" sourceRef="aguardar-liberacao" targetRef="seguir" />
		    <bpmn:sequenceFlow id="f2" sourceRef="pausa" targetRef="anotar" />
		    <bpmn:sequenceFlow id="f3" sourceRef="fechamento" targetRef="junta" />
		    <bpmn:sequenceFlow id="f4" sourceRef="outro" targetRef="junta" />
		  </bpmn:process>
		</bpmn:definitions>
		""";
}
