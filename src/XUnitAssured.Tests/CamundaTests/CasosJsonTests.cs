using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

using XUnitAssured.Camunda.TestCases;

namespace XUnitAssured.Tests.CamundaTests;

/// <summary>
/// Os casos de teste JSON no formato do Camunda Process Test, sem cluster: a leitura, a recusa do que
/// não cabe num cluster compartilhado e a execução contra o cluster falso.
/// </summary>
[Trait("Category", "Camunda")]
[Trait("Component", "TestCases")]
public class CasosJsonTests
{
	[Fact(DisplayName = "JSON test cases: a file is read into one case per entry, with its name and description")]
	public void Loads_The_File()
	{
		var casos = CamundaTestCases.Load("CamundaTests/casos/xa-pedido.json");

		casos.Select(c => c.Name).ShouldBe(new[] { "Pedido completo, com o envio mockado", "Pagamento recusado", "Passo a passo, com o filho de verdade" });
		casos[0].Description.ShouldStartWith("Mocks declarados");
		casos[1].Index.ShouldBe(1);
		casos[1].ToString().ShouldBe("Pagamento recusado");
	}

	[Fact(DisplayName = "JSON test cases: the [Theory] attribute gives one row per case")]
	public void Attribute_Gives_One_Row_Per_Case() =>
		new CamundaTestCasesAttribute("CamundaTests/casos/xa-pedido.json").GetData(null!).Count().ShouldBe(3);

	[Fact(DisplayName = "JSON test cases: instructions that do not fit a shared cluster fail the case before it starts, each with its reason")]
	public async Task Unsupported_Instructions_Are_Refused()
	{
		var caso = Caso("""
			{ "testCases": [ { "name": "Relógio e broadcast", "instructions": [
			  { "type": "CREATE_PROCESS_INSTANCE", "processDefinitionSelector": { "processDefinitionId": "pedido" } },
			  { "type": "INCREASE_TIME", "duration": "PT1H" },
			  { "type": "BROADCAST_SIGNAL", "signalName": "liberado" },
			  { "type": "ASSERT_VARIABLE", "variableName": "x", "satisfiesExpression": "x > 1" },
			  { "type": "ASSERT_PROCESS_INSTANCE", "state": "IS_COMPLETED" }
			] } ] }
			""");
		var cluster = new ClusterFalso();

		var falha = await FalhaAsync(() => caso.RunAsync(cluster.Cenario()));

		falha.Message.ShouldContain("#2 INCREASE_TIME: the clock belongs to the whole cluster");
		falha.Message.ShouldContain("#3 BROADCAST_SIGNAL: a broadcast reaches every instance");
		falha.Message.ShouldContain("#4 ASSERT_VARIABLE: FEEL expressions");
		falha.Message.ShouldNotContain("#1");
		cluster.Comandos.ShouldBeEmpty();
	}

	[Fact(DisplayName = "JSON test cases: mocks, creation and assertions run in order against the cluster")]
	public async Task Runs_In_Order()
	{
		var cluster = new ClusterFalso { AoCriarInstancia = (c, chave) => c.NovoJob(chave, "cobrar", "cobrar") };
		cluster.Publicar("pedido", 1);
		var caso = Caso("""
			{ "testCases": [ { "name": "Pedido", "instructions": [
			  { "type": "MOCK_JOB_WORKER_COMPLETE_JOB", "jobType": "cobrar", "variables": { "pago": true } },
			  { "type": "CREATE_PROCESS_INSTANCE", "processDefinitionSelector": { "processDefinitionId": "pedido" }, "variables": { "pedidoId": 7 } },
			  { "type": "ASSERT_PROCESS_INSTANCE", "state": "IS_COMPLETED" },
			  { "type": "ASSERT_ELEMENT_INSTANCE", "elementSelector": { "elementId": "cobrar" }, "state": "IS_COMPLETED" },
			  { "type": "ASSERT_VARIABLES", "variableNames": [ "pedidoId" ], "variables": { "pago": true } }
			] } ] }
			""");

		await caso.RunAsync(cluster.Cenario());

		cluster.Comandos.ShouldContain(c => c.StartsWith("complete "));
	}

	[Fact(DisplayName = "JSON test cases: a step-by-step COMPLETE_JOB completes the job when it shows up")]
	public async Task Complete_Job_Step()
	{
		var cluster = new ClusterFalso { AoCriarInstancia = (c, chave) => c.NovoJob(chave, "cobrar", "cobrar") };
		cluster.Publicar("pedido", 1);
		var caso = Caso("""
			{ "testCases": [ { "name": "Passo a passo", "instructions": [
			  { "type": "CREATE_PROCESS_INSTANCE", "processDefinitionSelector": { "processDefinitionId": "pedido" } },
			  { "type": "ASSERT_ELEMENT_INSTANCE", "elementSelector": { "elementId": "cobrar" }, "state": "IS_ACTIVE" },
			  { "type": "COMPLETE_JOB", "jobSelector": { "elementId": "cobrar" }, "variables": { "pago": true } },
			  { "type": "ASSERT_PROCESS_INSTANCE", "state": "IS_COMPLETED" }
			] } ] }
			""");

		await caso.RunAsync(cluster.Cenario());
	}

	[Fact(DisplayName = "JSON test cases: a failed assertion names the case, the instruction and what it saw")]
	public async Task Failure_Names_The_Instruction()
	{
		var cluster = new ClusterFalso();
		cluster.Publicar("pedido", 1);
		var caso = Caso("""
			{ "testCases": [ { "name": "Nunca termina", "instructions": [
			  { "type": "CREATE_PROCESS_INSTANCE", "processDefinitionSelector": { "processDefinitionId": "pedido" } },
			  { "type": "ASSERT_PROCESS_INSTANCE", "state": "IS_COMPLETED" }
			] } ] }
			""");

		var falha = await FalhaAsync(() => caso.RunAsync(cluster.Cenario()));

		falha.Message.ShouldContain("Test case 'Nunca termina', instruction #2 ASSERT_PROCESS_INSTANCE");
		falha.Message.ShouldContain("but it is ACTIVE");
		falha.Message.ShouldContain("Operate: http://camunda.falso:8080/operate/processes/pi-1");
	}

	[Fact(DisplayName = "JSON test cases: the supported list covers the instructions of the 8.10 schema that fit a shared cluster")]
	public void Supported_Instructions()
	{
		string[] suportadas =
		{
			"CREATE_PROCESS_INSTANCE", "MOCK_JOB_WORKER_COMPLETE_JOB", "MOCK_JOB_WORKER_THROW_BPMN_ERROR", "MOCK_CHILD_PROCESS",
			"COMPLETE_JOB", "THROW_BPMN_ERROR_FROM_JOB", "COMPLETE_USER_TASK", "PUBLISH_MESSAGE", "CORRELATE_MESSAGE",
			"UPDATE_VARIABLES", "ASSERT_PROCESS_INSTANCE", "ASSERT_ELEMENT_INSTANCE", "ASSERT_ELEMENT_INSTANCES",
			"ASSERT_VARIABLES", "ASSERT_USER_TASK", "ASSERT_PROCESS_INSTANCE_MESSAGE_SUBSCRIPTION"
		};

		foreach (var tipo in suportadas)
			ExecutorDeCaso.Recusa(Instrucao(tipo)).ShouldBeNull(tipo);

		ExecutorDeCaso.Recusa(Instrucao("SET_TIME")).ShouldNotBeNull();
		ExecutorDeCaso.Recusa(Instrucao("MOCK_DMN_DECISION")).ShouldNotBeNull();
		ExecutorDeCaso.Recusa(Instrucao("COMPLETE_JOB_AD_HOC_SUB_PROCESS")).ShouldNotBeNull();
	}

	private static System.Text.Json.JsonElement Instrucao(string tipo) =>
		System.Text.Json.JsonDocument.Parse($$"""{ "type": "{{tipo}}" }""").RootElement.Clone();

	// O Should.ThrowAsync do Shouldly deixa passar a própria ShouldAssertException.
	private static async Task<ShouldAssertException> FalhaAsync(Func<Task> acao)
	{
		try
		{
			await acao();
		}
		catch (ShouldAssertException e)
		{
			return e;
		}

		throw new InvalidOperationException("Expected the test case to fail, and it passed.");
	}

	// Um arquivo de um caso só, na pasta temporária.
	private static CamundaTestCase Caso(string json)
	{
		var arquivo = Path.Combine(Path.GetTempPath(), $"xa-caso-{Guid.NewGuid():N}.json");
		File.WriteAllText(arquivo, json);
		return CamundaTestCases.Load(arquivo).Single();
	}
}
