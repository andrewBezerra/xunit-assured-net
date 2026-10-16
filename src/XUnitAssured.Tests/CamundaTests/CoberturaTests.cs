using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

using XUnitAssured.Camunda;
using XUnitAssured.Camunda.Cobertura;
using XUnitAssured.Camunda.Mocks;

namespace XUnitAssured.Tests.CamundaTests;

/// <summary>
/// A cobertura dos diagramas: o que as instâncias dos testes percorreram e o que nunca rodou.
/// </summary>
[Trait("Category", "Camunda")]
[Trait("Component", "Coverage")]
public class CoberturaTests
{
	[Fact(DisplayName = "Coverage counts the flow nodes reached by every instance of a version, and lists the ones never reached")]
	public void Counts_Flow_Nodes()
	{
		var cobertura = new CoberturaDaSuite();
		cobertura.Registrar("def-1", "pedido", 3, Pedido, new[] { "inicio", "cobrar", "concluido" });
		cobertura.Registrar("def-1", "pedido", 3, Pedido, new[] { "inicio", "cobrar" });

		var processo = cobertura.Relatorio().Processes.Single();

		processo.ProcessId.ShouldBe("pedido");
		processo.Version.ShouldBe(3);
		processo.Instances.ShouldBe(2);
		// início, cobrar, a borda, recusado, concluído: os fluxos de sequência e a definição do erro não contam
		processo.Total.ShouldBe(5);
		processo.Reached.ShouldBe(3);
		processo.Coverage.ShouldBe(60.0);
		processo.Missed.Select(m => m.Id).ShouldBe(new[] { "pagamento-recusado", "recusado" });
		processo.Missed.Single(m => m.Id == "recusado").Name.ShouldBe("Pedido recusado");
		processo.Missed.Single(m => m.Id == "recusado").Type.ShouldBe("endEvent");
	}

	[Fact(DisplayName = "Each version of a process is its own entry: a new version starts its coverage over")]
	public void Versions_Are_Separate()
	{
		var cobertura = new CoberturaDaSuite();
		cobertura.Registrar("def-1", "pedido", 1, Pedido, new[] { "inicio" });
		cobertura.Registrar("def-2", "pedido", 2, Pedido, new[] { "inicio", "cobrar" });

		cobertura.Relatorio().Processes.Select(p => (p.Version, p.Reached)).ShouldBe(new (int?, int)[] { (1, 1), (2, 2) });
	}

	[Fact(DisplayName = "The report is a coverage.json and an index.html that draws each diagram with the reached elements")]
	public void Writes_The_Report()
	{
		var pasta = Path.Combine(Path.GetTempPath(), $"xa-cobertura-{Guid.NewGuid():N}");
		try
		{
			var cobertura = new CoberturaDaSuite();
			cobertura.Registrar("def-1", "pedido", 3, Pedido, new[] { "inicio", "cobrar", "concluido" });

			var html = cobertura.Escrever(pasta);

			html.ShouldBe(Path.Combine(pasta, "index.html"));
			var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(pasta, "coverage.json"))).RootElement;
			json.GetProperty("processes")[0].GetProperty("coverage").GetDouble().ShouldBe(60.0);

			var pagina = File.ReadAllText(html!);
			pagina.ShouldContain("bpmn-navigated-viewer.production.min.js");
			pagina.ShouldContain("integrity=\"sha384-");
			pagina.ShouldContain("\"processId\": \"pedido\"");
			// O XML vai dentro de um <script>: nenhum "</" pode fechá-lo antes da hora.
			pagina.ShouldNotContain("</bpmn:definitions>");
		}
		finally
		{
			if (Directory.Exists(pasta)) Directory.Delete(pasta, true);
		}
	}

	[Fact(DisplayName = "No instance, no report: nothing is written")]
	public void Empty_Writes_Nothing() =>
		new CoberturaDaSuite().Escrever(Path.Combine(Path.GetTempPath(), $"xa-vazia-{Guid.NewGuid():N}")).ShouldBeNull();

	[Fact(DisplayName = "When the instance completes, the mocks' loop records the elements it went through")]
	public async Task Loop_Records_When_The_Instance_Completes()
	{
		var cluster = new ClusterFalso();
		var chave = cluster.Publicar("pedido", 3);
		cluster.DefinirBpmn(chave, Pedido);
		var instancia = cluster.NovaInstancia("pedido", definicao: chave);
		cluster.Elemento(instancia, "inicio", "COMPLETED");
		cluster.NovoJob(instancia, "cobrar", "cobrar");

		var settings = ClusterFalso.Rapido();
		settings.Coverage = new CamundaCoverageSettings { Enabled = true };
		var cobertura = new CoberturaDaSuite();
		var mocks = new MocksDaInstancia(
			cluster, instancia,
			new Dictionary<string, JobMock> { ["cobrar"] = new JobMock("cobrar", AcaoDoMock.Completar, null) },
			settings, definitionKey: chave, cobertura: cobertura);

		mocks.Iniciar();
		await mocks.Rodando.WaitAsync(TimeSpan.FromSeconds(10));

		var processo = cobertura.Relatorio().Processes.Single();
		processo.ReachedElements.ShouldBe(new[] { "cobrar", "inicio" }, ignoreOrder: true);
		processo.Instances.ShouldBe(1);
	}

	[Fact(DisplayName = "A timer the package skipped counts as reached, though Camunda never created an element for it")]
	public async Task Skipped_Timer_Counts()
	{
		const string comTimer = """
			<?xml version="1.0" encoding="UTF-8"?>
			<bpmn:definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL" id="d">
			  <bpmn:process id="espera" isExecutable="true">
			    <bpmn:userTask id="aprovar" />
			    <bpmn:boundaryEvent id="prazo" attachedToRef="aprovar"><bpmn:timerEventDefinition /></bpmn:boundaryEvent>
			    <bpmn:endEvent id="expirou" />
			    <bpmn:sequenceFlow id="f1" sourceRef="prazo" targetRef="expirou" />
			  </bpmn:process>
			</bpmn:definitions>
			""";
		var cluster = new ClusterFalso();
		var chave = cluster.Publicar("espera", 1);
		cluster.DefinirBpmn(chave, comTimer);
		var instancia = cluster.NovaInstancia("espera", definicao: chave);
		cluster.NovaUserTask(instancia, "aprovar");

		var settings = ClusterFalso.Rapido();
		settings.Coverage = new CamundaCoverageSettings { Enabled = true };
		var cobertura = new CoberturaDaSuite();
		var mocks = new MocksDaInstancia(
			cluster, instancia, new Dictionary<string, JobMock>(), settings,
			timers: new[] { "prazo" }, definitionKey: chave, cobertura: cobertura);

		mocks.Iniciar();
		await mocks.Rodando.WaitAsync(TimeSpan.FromSeconds(10));

		// aprovar (encerrada), prazo (pulado pelo pacote) e expirou (ativado): tudo percorrido
		cobertura.Relatorio().Processes.Single().Coverage.ShouldBe(100.0);
	}

	[Fact(DisplayName = "With coverage off, the loop records nothing")]
	public async Task Off_Records_Nothing()
	{
		var cluster = new ClusterFalso();
		var chave = cluster.Publicar("pedido", 3);
		cluster.DefinirBpmn(chave, Pedido);
		var instancia = cluster.NovaInstancia("pedido", definicao: chave);
		cluster.Estado(instancia, "COMPLETED");

		var cobertura = new CoberturaDaSuite();
		var mocks = new MocksDaInstancia(
			cluster, instancia, new Dictionary<string, JobMock>(), ClusterFalso.Rapido(), definitionKey: chave, cobertura: cobertura);

		mocks.Iniciar();
		await mocks.Rodando.WaitAsync(TimeSpan.FromSeconds(10));

		cobertura.Vazia.ShouldBeTrue();
	}

	private const string Pedido = """
		<?xml version="1.0" encoding="UTF-8"?>
		<bpmn:definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL" id="d">
		  <bpmn:error id="Erro" errorCode="RECUSADO" />
		  <bpmn:process id="pedido" isExecutable="true">
		    <bpmn:startEvent id="inicio" />
		    <bpmn:serviceTask id="cobrar" name="Cobrar" />
		    <bpmn:boundaryEvent id="pagamento-recusado" attachedToRef="cobrar"><bpmn:errorEventDefinition id="def-erro" errorRef="Erro" /></bpmn:boundaryEvent>
		    <bpmn:endEvent id="recusado" name="Pedido recusado" />
		    <bpmn:endEvent id="concluido" />
		    <bpmn:sequenceFlow id="f1" sourceRef="inicio" targetRef="cobrar" />
		    <bpmn:sequenceFlow id="f2" sourceRef="cobrar" targetRef="concluido" />
		    <bpmn:sequenceFlow id="f3" sourceRef="pagamento-recusado" targetRef="recusado" />
		  </bpmn:process>
		</bpmn:definitions>
		""";
}
