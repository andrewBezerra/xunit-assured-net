using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

using XUnitAssured.Camunda.Gateway;
using XUnitAssured.Camunda.TestCases;

namespace XUnitAssured.Tests.CamundaTests;

/// <summary>
/// Cada instrução do formato do Camunda Process Test, executada pelo XUnitAssured.Camunda contra o
/// cluster falso: o que ela manda ao cluster, o que ela espera e como falha quando não acontece.
/// </summary>
[Trait("Category", "Camunda")]
[Trait("Component", "TestCases")]
public class CasosJsonInstrucoesTests
{
	// ---------- O caso inteiro ----------

	[Fact(DisplayName = "JSON test cases: a case with no instructions array fails, naming the case")]
	public async Task No_Instructions_Array()
	{
		var caso = Caso("""{ "testCases": [ { "name": "Vazio" } ] }""");

		var falha = await FalhaAsync(() => caso.RunAsync(new ClusterFalso().Cenario()));

		falha.Message.ShouldBe("Test case 'Vazio' has no instructions array.");
	}

	[Fact(DisplayName = "JSON test cases: each refused option is reported with its reason, before anything runs")]
	public async Task Every_Refused_Option_Has_Its_Reason()
	{
		var caso = Caso("""
			{ "testCases": [ { "name": "Opções", "instructions": [
			  { "type": "CREATE_PROCESS_INSTANCE", "processDefinitionSelector": { "processDefinitionId": "pedido" }, "startInstructions": [ { "elementId": "x" } ] },
			  { "type": "COMPLETE_JOB", "jobSelector": { "jobType": "cobrar" }, "useExampleData": true },
			  { "type": "MOCK_CHILD_PROCESS", "processDefinitionId": "envio", "versionTag": "v1" },
			  { "type": "UPDATE_VARIABLES", "elementSelector": { "elementId": "x" }, "variables": { "a": 1 } },
			  { "type": "UPDATE_VARIABLES", "createLocalVariables": true, "variables": { "a": 1 } },
			  { "type": "ASSERT_VARIABLES", "elementSelector": { "elementId": "x" }, "variableNames": [ "a" ] },
			  { "type": "ASSERT_USER_TASK", "userTaskSelector": { "elementId": "aprovar" }, "dueDate": "2026-10-16T10:00:00Z" },
			  { "type": "ASSERT_PROCESS_INSTANCE_MESSAGE_SUBSCRIPTION", "messageSelector": { "messageName": "pago" }, "state": "IS_CORRELATED" },
			  { "type": "MOCK_DMN_DECISION", "decisionDefinitionId": "regras" },
			  { "type": "RESOLVE_INCIDENT" },
			  { "type": "TELEPORT" },
			  { "name": "sem tipo" }
			] } ] }
			""");
		var cluster = new ClusterFalso();

		var falha = await FalhaAsync(() => caso.RunAsync(cluster.Cenario()));

		falha.Message.ShouldContain("#1 CREATE_PROCESS_INSTANCE: startInstructions and runtimeInstructions are not supported");
		falha.Message.ShouldContain("#2 COMPLETE_JOB: useExampleData is not supported");
		falha.Message.ShouldContain("#3 MOCK_CHILD_PROCESS: versionTag is not supported");
		falha.Message.ShouldContain("#4 UPDATE_VARIABLES: only process instance variables are supported");
		falha.Message.ShouldContain("#5 UPDATE_VARIABLES: only process instance variables are supported");
		falha.Message.ShouldContain("#6 ASSERT_VARIABLES: only process instance variables are supported");
		falha.Message.ShouldContain("#7 ASSERT_USER_TASK: dueDate and followUpDate are not supported");
		falha.Message.ShouldContain("#8 ASSERT_PROCESS_INSTANCE_MESSAGE_SUBSCRIPTION: IS_CORRELATED is not supported");
		falha.Message.ShouldContain("#9 MOCK_DMN_DECISION: decisions are not supported yet");
		falha.Message.ShouldContain("#10 RESOLVE_INCIDENT: resolving incidents is not supported yet.");
		falha.Message.ShouldContain("#11 TELEPORT: 'TELEPORT' is not supported.");
		falha.Message.ShouldContain("#12 (no type): '(no type)' is not supported.");
		cluster.Comandos.ShouldBeEmpty();
	}

	[Fact(DisplayName = "JSON test cases: an empty startInstructions array is not a refusal")]
	public void Empty_Start_Instructions_Are_Accepted() =>
		ExecutorDeCaso.Recusa(Instrucao("""
			{ "type": "CREATE_PROCESS_INSTANCE", "startInstructions": [], "runtimeInstructions": null }
			""")).ShouldBeNull();

	[Fact(DisplayName = "JSON test cases: the case deploys what it is given before creating, and starts the deployed version")]
	public async Task Deploys_First()
	{
		var cluster = new ClusterFalso();
		var caso = Caso("""
			{ "testCases": [ { "name": "Com deploy", "instructions": [
			  { "type": "CREATE_PROCESS_INSTANCE", "processDefinitionSelector": { "processDefinitionId": "xa-pedido" } },
			  { "type": "ASSERT_PROCESS_INSTANCE", "state": "IS_ACTIVE" }
			] } ] }
			""");

		await caso.RunAsync(cluster.Cenario(), "xa-pedido");

		cluster.Comandos.ShouldBe(new[] { "deploy xa-pedido.bpmn", "create def-xa-pedido" });
	}

	[Fact(DisplayName = "JSON test cases: an instance that could not be created fails the case, saying why")]
	public async Task Create_Failure()
	{
		var caso = Caso("""
			{ "testCases": [ { "name": "Sem processo", "instructions": [
			  { "type": "CREATE_PROCESS_INSTANCE", "processDefinitionSelector": { "processDefinitionId": "fantasma" } }
			] } ] }
			""");

		var falha = await FalhaAsync(() => caso.RunAsync(new ClusterFalso().Cenario()));

		falha.Message.ShouldContain("instruction #1 CREATE_PROCESS_INSTANCE: the instance of 'fantasma' was not created");
		falha.Message.ShouldContain("Could not start process 'fantasma': it is not deployed");
	}

	[Fact(DisplayName = "JSON test cases: CREATE_PROCESS_INSTANCE without a process id fails the case")]
	public async Task Create_Without_Process_Id()
	{
		var caso = Caso("""
			{ "testCases": [ { "name": "Sem id", "instructions": [ { "type": "CREATE_PROCESS_INSTANCE" } ] } ] }
			""");

		var falha = await FalhaAsync(() => caso.RunAsync(new ClusterFalso().Cenario()));

		falha.Message.ShouldContain("processDefinitionSelector.processDefinitionId is required.");
	}

	[Fact(DisplayName = "JSON test cases: an instruction that needs an instance, before any was created, fails the case")]
	public async Task Needs_An_Instance_First()
	{
		var caso = Caso("""
			{ "testCases": [ { "name": "Fora de ordem", "instructions": [
			  { "type": "ASSERT_PROCESS_INSTANCE", "state": "IS_COMPLETED" }
			] } ] }
			""");

		var falha = await FalhaAsync(() => caso.RunAsync(new ClusterFalso().Cenario()));

		falha.Message.ShouldContain("no instance was created before this instruction. Add a CREATE_PROCESS_INSTANCE first.");
	}

	// ---------- Jobs ----------

	[Fact(DisplayName = "JSON test cases: MOCK_JOB_WORKER_THROW_BPMN_ERROR throws the error with its code on the instance's job")]
	public async Task Mock_Throws_Bpmn_Error()
	{
		var cluster = new ClusterFalso { AoCriarInstancia = (c, chave) => c.NovoJob(chave, "cobrar", "cobrar") };
		cluster.Publicar("pedido", 1);
		var caso = Caso("""
			{ "testCases": [ { "name": "Recusado", "instructions": [
			  { "type": "MOCK_JOB_WORKER_THROW_BPMN_ERROR", "jobType": "cobrar", "errorCode": "PAGAMENTO_RECUSADO", "errorMessage": "sem saldo" },
			  { "type": "CREATE_PROCESS_INSTANCE", "processDefinitionSelector": { "processDefinitionId": "pedido" } },
			  { "type": "ASSERT_PROCESS_INSTANCE", "state": "IS_COMPLETED" },
			  { "type": "ASSERT_ELEMENT_INSTANCE", "elementSelector": { "elementId": "cobrar" }, "state": "IS_TERMINATED" }
			] } ] }
			""");

		await caso.RunAsync(cluster.Cenario());

		cluster.Comandos.ShouldContain("error job-2 PAGAMENTO_RECUSADO");
	}

	[Fact(DisplayName = "JSON test cases: THROW_BPMN_ERROR_FROM_JOB throws the error once, on the job the selector finds")]
	public async Task Throw_Bpmn_Error_Step()
	{
		var cluster = new ClusterFalso { AoCriarInstancia = (c, chave) => c.NovoJob(chave, "cobrar", "cobrar") };
		cluster.Publicar("pedido", 1);
		var caso = Caso("""
			{ "testCases": [ { "name": "Passo com erro", "instructions": [
			  { "type": "CREATE_PROCESS_INSTANCE", "processDefinitionSelector": { "processDefinitionId": "pedido" } },
			  { "type": "THROW_BPMN_ERROR_FROM_JOB", "jobSelector": { "jobType": "cobrar", "processDefinitionId": "pedido" }, "errorCode": "SEM_ESTOQUE" },
			  { "type": "ASSERT_PROCESS_INSTANCE", "state": "IS_COMPLETED" }
			] } ] }
			""");

		await caso.RunAsync(cluster.Cenario());

		cluster.Comandos.ShouldContain("error job-2 SEM_ESTOQUE");
	}

	[Fact(DisplayName = "JSON test cases: a COMPLETE_JOB whose job never shows up fails, describing the selector")]
	public async Task Complete_Job_Never_Shows_Up()
	{
		var cluster = new ClusterFalso();
		cluster.Publicar("pedido", 1);
		var caso = Caso("""
			{ "testCases": [ { "name": "Sem job", "instructions": [
			  { "type": "CREATE_PROCESS_INSTANCE", "processDefinitionSelector": { "processDefinitionId": "pedido" } },
			  { "type": "COMPLETE_JOB", "jobSelector": { "jobType": "cobrar", "elementId": "cobrar" } }
			] } ] }
			""");

		var falha = await FalhaAsync(() => caso.RunAsync(cluster.Cenario()));

		falha.Message.ShouldContain("expected a job type 'cobrar', element 'cobrar' within 1s, but no such job is waiting.");
	}

	// ---------- User tasks ----------

	[Fact(DisplayName = "JSON test cases: COMPLETE_USER_TASK completes the open task the selector finds, with the variables")]
	public async Task Complete_User_Task_Step()
	{
		var cluster = new ClusterFalso
		{
			AoCriarInstancia = (c, chave) => c.NovaUserTask(chave, "aprovar", nome: "Aprovar pedido", responsavel: "ana", grupos: new[] { "gerentes" }, prioridade: 70)
		};
		cluster.Publicar("pedido", 1);
		var caso = Caso("""
			{ "testCases": [ { "name": "Aprovação", "instructions": [
			  { "type": "CREATE_PROCESS_INSTANCE", "processDefinitionSelector": { "processDefinitionId": "pedido" } },
			  { "type": "ASSERT_USER_TASK", "userTaskSelector": { "elementId": "aprovar" }, "state": "IS_CREATED",
			    "assignee": "ana", "name": "Aprovar pedido", "priority": 70, "candidateGroups": [ "gerentes" ] },
			  { "type": "COMPLETE_USER_TASK", "userTaskSelector": { "taskName": "Aprovar pedido", "processDefinitionId": "pedido" }, "variables": { "aprovado": true } },
			  { "type": "ASSERT_USER_TASK", "userTaskSelector": { "elementId": "aprovar" }, "state": "IS_COMPLETED" },
			  { "type": "ASSERT_VARIABLES", "variables": { "aprovado": true } },
			  { "type": "ASSERT_PROCESS_INSTANCE", "state": "IS_COMPLETED" }
			] } ] }
			""");

		await caso.RunAsync(cluster.Cenario());

		cluster.Comandos.ShouldContain("complete-user-task task-2");
	}

	[Theory(DisplayName = "JSON test cases: ASSERT_USER_TASK fails on the first property that differs, saying what the task has")]
	[InlineData("\"state\": \"IS_CANCELED\"", "'aprovar' is CREATED")]
	[InlineData("\"state\": \"IS_FAILED\"", "'aprovar' is CREATED")]
	[InlineData("\"assignee\": \"bia\"", "'aprovar' is assigned to 'ana'")]
	[InlineData("\"name\": \"Revisar\"", "'aprovar' is named 'Aprovar pedido'")]
	[InlineData("\"priority\": 10", "'aprovar' has priority 70")]
	[InlineData("\"candidateGroups\": [ \"diretoria\" ]", "'aprovar' has candidate groups gerentes")]
	public async Task User_Task_Differences(string propriedade, string esperado)
	{
		var cluster = new ClusterFalso
		{
			AoCriarInstancia = (c, chave) => c.NovaUserTask(chave, "aprovar", nome: "Aprovar pedido", responsavel: "ana", grupos: new[] { "gerentes" }, prioridade: 70)
		};
		cluster.Publicar("pedido", 1);
		var caso = Caso($$"""
			{ "testCases": [ { "name": "Tarefa", "instructions": [
			  { "type": "CREATE_PROCESS_INSTANCE", "processDefinitionSelector": { "processDefinitionId": "pedido" } },
			  { "type": "ASSERT_USER_TASK", "userTaskSelector": { "elementId": "aprovar" }, {{propriedade}} }
			] } ] }
			""");

		var falha = await FalhaAsync(() => caso.RunAsync(cluster.Cenario()));

		falha.Message.ShouldContain($"expected a user task element 'aprovar' to match within 1s, but {esperado}.");
	}

	[Fact(DisplayName = "JSON test cases: ASSERT_USER_TASK on a task that never exists fails, saying so")]
	public async Task User_Task_Never_Exists()
	{
		var cluster = new ClusterFalso();
		cluster.Publicar("pedido", 1);
		var caso = Caso("""
			{ "testCases": [ { "name": "Sem tarefa", "instructions": [
			  { "type": "CREATE_PROCESS_INSTANCE", "processDefinitionSelector": { "processDefinitionId": "pedido" } },
			  { "type": "ASSERT_USER_TASK", "userTaskSelector": { "elementId": "aprovar" } }
			] } ] }
			""");

		var falha = await FalhaAsync(() => caso.RunAsync(cluster.Cenario()));

		falha.Message.ShouldContain("but no such user task exists yet.");
	}

	// ---------- Mensagens ----------

	[Fact(DisplayName = "JSON test cases: CORRELATE_MESSAGE waits for the case's subscription and correlates it with the variables")]
	public async Task Correlates_When_The_Subscription_Opens()
	{
		var cluster = new ClusterFalso
		{
			// A assinatura abre um pouco depois da criação, como no motor.
			AoCriarInstancia = (c, chave) => _ = Task.Run(async () =>
			{
				await Task.Delay(100);
				c.NovaAssinatura(chave, "aguardar-pagamento", "pago", "A-7");
			})
		};
		cluster.Publicar("pedido", 1);
		var caso = Caso("""
			{ "testCases": [ { "name": "Pagamento", "instructions": [
			  { "type": "CREATE_PROCESS_INSTANCE", "processDefinitionSelector": { "processDefinitionId": "pedido" } },
			  { "type": "ASSERT_PROCESS_INSTANCE_MESSAGE_SUBSCRIPTION", "messageSelector": { "messageName": "pago", "correlationKey": "A-7" }, "state": "IS_WAITING" },
			  { "type": "CORRELATE_MESSAGE", "name": "pago", "correlationKey": "A-7", "variables": { "valor": 10 } },
			  { "type": "ASSERT_PROCESS_INSTANCE_MESSAGE_SUBSCRIPTION", "messageSelector": { "messageName": "pago" }, "state": "IS_NOT_WAITING" },
			  { "type": "ASSERT_VARIABLES", "variables": { "valor": 10 } },
			  { "type": "ASSERT_PROCESS_INSTANCE", "state": "IS_COMPLETED" }
			] } ] }
			""");

		await caso.RunAsync(cluster.Cenario(ClusterFalso.Rapido(prazoSegundos: 5)));

		cluster.Comandos.ShouldContain("correlate pago A-7");
	}

	[Fact(DisplayName = "JSON test cases: a subscription that closes before the message arrives is retried, not a failure")]
	public async Task Correlation_Not_Found_Is_Retried()
	{
		var recusas = 0;
		var cluster = new ClusterFalso
		{
			AoCriarInstancia = (c, chave) => c.NovaAssinatura(chave, "aguardar-pagamento", "pago", "A-7"),
			Falha = operacao => operacao == nameof(ICamundaGateway.CorrelateMessageAsync) && recusas++ == 0
				? new CamundaRecusouException("NOT_FOUND", "no subscription")
				: null
		};
		cluster.Publicar("pedido", 1);
		var caso = Caso("""
			{ "testCases": [ { "name": "Pagamento", "instructions": [
			  { "type": "CREATE_PROCESS_INSTANCE", "processDefinitionSelector": { "processDefinitionId": "pedido" } },
			  { "type": "PUBLISH_MESSAGE", "name": "pago", "correlationKey": "A-7" },
			  { "type": "ASSERT_PROCESS_INSTANCE", "state": "IS_COMPLETED" }
			] } ] }
			""");

		await caso.RunAsync(cluster.Cenario());

		recusas.ShouldBe(2);
		cluster.Comandos.ShouldContain("correlate pago A-7");
	}

	[Fact(DisplayName = "JSON test cases: a message another instance also awaits with the same key is not correlated, and the case fails saying why")]
	public async Task Message_Awaited_Outside_The_Case()
	{
		var cluster = new ClusterFalso { AoCriarInstancia = (c, chave) => c.NovaAssinatura(chave, "aguardar-pagamento", "pago", "A-7") };
		cluster.Publicar("pedido", 1);
		var deOutroTeste = cluster.NovaInstancia("pedido");
		cluster.NovaAssinatura(deOutroTeste, "aguardar-pagamento", "pago", "A-7");
		var caso = Caso("""
			{ "testCases": [ { "name": "Chave repetida", "instructions": [
			  { "type": "CREATE_PROCESS_INSTANCE", "processDefinitionSelector": { "processDefinitionId": "pedido" } },
			  { "type": "CORRELATE_MESSAGE", "name": "pago", "correlationKey": "A-7" }
			] } ] }
			""");

		var falha = await FalhaAsync(() => caso.RunAsync(cluster.Cenario()));

		falha.Message.ShouldContain($"instance {deOutroTeste}, outside this test case, also awaits message 'pago' with key 'A-7'");
		cluster.Comandos.ShouldNotContain("correlate pago A-7");
	}

	[Fact(DisplayName = "JSON test cases: a message nobody in the case awaits fails when the deadline passes")]
	public async Task Message_Nobody_Awaits()
	{
		var cluster = new ClusterFalso();
		cluster.Publicar("pedido", 1);
		var caso = Caso("""
			{ "testCases": [ { "name": "Ninguém espera", "instructions": [
			  { "type": "CREATE_PROCESS_INSTANCE", "processDefinitionSelector": { "processDefinitionId": "pedido" } },
			  { "type": "PUBLISH_MESSAGE", "name": "pago" }
			] } ] }
			""");

		var falha = await FalhaAsync(() => caso.RunAsync(cluster.Cenario()));

		falha.Message.ShouldContain("expected message 'pago' with correlation key '' to be correlated within 1s, but no instance of this test case awaits it yet.");
	}

	[Fact(DisplayName = "JSON test cases: a message subscription assertion fails when the instance does not wait as stated")]
	public async Task Subscription_Assertion_Fails()
	{
		var cluster = new ClusterFalso();
		cluster.Publicar("pedido", 1);
		var caso = Caso("""
			{ "testCases": [ { "name": "Não espera", "instructions": [
			  { "type": "CREATE_PROCESS_INSTANCE", "processDefinitionSelector": { "processDefinitionId": "pedido" } },
			  { "type": "ASSERT_PROCESS_INSTANCE_MESSAGE_SUBSCRIPTION", "messageSelector": { "messageName": "pago" }, "state": "IS_WAITING" }
			] } ] }
			""");

		var falha = await FalhaAsync(() => caso.RunAsync(cluster.Cenario()));

		falha.Message.ShouldContain("expected the instance to wait for message 'pago' within 1s, but it does not wait for it.");
	}

	// ---------- Variáveis ----------

	[Fact(DisplayName = "JSON test cases: UPDATE_VARIABLES writes on the instance, and ASSERT_VARIABLES sees the new values")]
	public async Task Update_Variables()
	{
		var cluster = new ClusterFalso();
		cluster.Publicar("pedido", 1);
		var caso = Caso("""
			{ "testCases": [ { "name": "Variáveis", "instructions": [
			  { "type": "CREATE_PROCESS_INSTANCE", "processDefinitionSelector": { "processDefinitionId": "pedido" }, "variables": { "total": 1 } },
			  { "type": "UPDATE_VARIABLES", "processInstanceSelector": { "processDefinitionId": "pedido" }, "variables": { "total": 2, "cliente": { "id": 9 } } },
			  { "type": "ASSERT_VARIABLES", "processInstanceSelector": { "processDefinitionId": "pedido" }, "variables": { "total": 2, "cliente": { "id": 9 } } }
			] } ] }
			""");

		await caso.RunAsync(cluster.Cenario());

		cluster.Comandos.ShouldContain("set-variables pi-1");
	}

	[Fact(DisplayName = "JSON test cases: ASSERT_VARIABLES fails with the value the instance has, or the name it lacks")]
	public async Task Variable_Assertion_Fails()
	{
		var cluster = new ClusterFalso();
		cluster.Publicar("pedido", 1);
		var diferente = Caso("""
			{ "testCases": [ { "name": "Valor", "instructions": [
			  { "type": "CREATE_PROCESS_INSTANCE", "processDefinitionSelector": { "processDefinitionId": "pedido" }, "variables": { "pago": false } },
			  { "type": "ASSERT_VARIABLES", "variables": { "pago": true } }
			] } ] }
			""");
		var faltando = Caso("""
			{ "testCases": [ { "name": "Nome", "instructions": [
			  { "type": "CREATE_PROCESS_INSTANCE", "processDefinitionSelector": { "processDefinitionId": "pedido" } },
			  { "type": "ASSERT_VARIABLES", "variableNames": [ "rastreio" ] }
			] } ] }
			""");

		(await FalhaAsync(() => diferente.RunAsync(cluster.Cenario()))).Message.ShouldContain("but 'pago' is false, not true.");
		(await FalhaAsync(() => faltando.RunAsync(cluster.Cenario()))).Message.ShouldContain("but 'rastreio' does not exist.");
	}

	// ---------- A instância ----------

	[Fact(DisplayName = "JSON test cases: ASSERT_PROCESS_INSTANCE checks open incidents as well as the state")]
	public async Task Instance_With_Incident()
	{
		var cluster = new ClusterFalso { AoCriarInstancia = (c, chave) => c.Estado(chave, "ACTIVE", comIncident: true) };
		cluster.Publicar("pedido", 1);
		var caso = Caso("""
			{ "testCases": [ { "name": "Incident", "instructions": [
			  { "type": "CREATE_PROCESS_INSTANCE", "processDefinitionSelector": { "processDefinitionId": "pedido" } },
			  { "type": "ASSERT_PROCESS_INSTANCE", "state": "IS_ACTIVE", "hasActiveIncidents": true },
			  { "type": "ASSERT_PROCESS_INSTANCE", "hasActiveIncidents": true },
			  { "type": "ASSERT_PROCESS_INSTANCE", "hasActiveIncidents": false }
			] } ] }
			""");

		var falha = await FalhaAsync(() => caso.RunAsync(cluster.Cenario()));

		falha.Message.ShouldContain("instruction #4 ASSERT_PROCESS_INSTANCE: expected the instance of 'the test case' to have no open incident within 1s, but it is ACTIVE with an open incident.");
	}

	[Fact(DisplayName = "JSON test cases: an instance that already ended in another state fails at once, without waiting the deadline")]
	public async Task Ended_Instance_Fails_At_Once()
	{
		var cluster = new ClusterFalso { AoCriarInstancia = (c, chave) => c.Estado(chave, "TERMINATED") };
		cluster.Publicar("pedido", 1);
		var caso = Caso("""
			{ "testCases": [ { "name": "Cancelada", "instructions": [
			  { "type": "CREATE_PROCESS_INSTANCE", "processDefinitionSelector": { "processDefinitionId": "pedido" } },
			  { "type": "ASSERT_PROCESS_INSTANCE", "processInstanceSelector": { "processDefinitionId": "pedido" }, "state": "IS_TERMINATED" },
			  { "type": "ASSERT_PROCESS_INSTANCE", "processInstanceSelector": { "processDefinitionId": "pedido" }, "state": "IS_COMPLETED" }
			] } ] }
			""");
		var cronometro = System.Diagnostics.Stopwatch.StartNew();

		var falha = await FalhaAsync(() => caso.RunAsync(cluster.Cenario(ClusterFalso.Rapido(prazoSegundos: 30))));

		falha.Message.ShouldContain("expected the instance of 'pedido' to be completed, but it is TERMINATED.");
		cronometro.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(20));
	}

	[Fact(DisplayName = "JSON test cases: a selector for a child process finds the instance the case's instance called")]
	public async Task Child_Instance_Selector()
	{
		var cluster = FilhoMockado();
		var caso = Caso("""
			{ "testCases": [ { "name": "Filho mockado", "instructions": [
			  { "type": "MOCK_CHILD_PROCESS", "processDefinitionId": "envio", "variables": { "rastreio": "BR-9" } },
			  { "type": "CREATE_PROCESS_INSTANCE", "processDefinitionSelector": { "processDefinitionId": "pedido" } },
			  { "type": "ASSERT_PROCESS_INSTANCE", "processInstanceSelector": { "processDefinitionId": "envio" }, "state": "IS_COMPLETED" },
			  { "type": "ASSERT_VARIABLES", "processInstanceSelector": { "processDefinitionId": "envio" }, "variables": { "rastreio": "BR-9" } },
			  { "type": "ASSERT_PROCESS_INSTANCE", "processInstanceSelector": { "processDefinitionId": "pedido" }, "state": "IS_COMPLETED" }
			] } ] }
			""");

		await caso.RunAsync(cluster.Cenario());

		cluster.Modificacoes.ShouldHaveSingleItem().ShouldBe("pi-2 -[pi-2:despachar] +[fim]");
	}

	[Fact(DisplayName = "JSON test cases: a selector for a process the case never called fails when the deadline passes")]
	public async Task Child_Instance_Never_Shows_Up()
	{
		var cluster = new ClusterFalso();
		cluster.Publicar("pedido", 1);
		var caso = Caso("""
			{ "testCases": [ { "name": "Sem filho", "instructions": [
			  { "type": "CREATE_PROCESS_INSTANCE", "processDefinitionSelector": { "processDefinitionId": "pedido" } },
			  { "type": "UPDATE_VARIABLES", "processInstanceSelector": { "processDefinitionId": "envio" }, "variables": { "a": 1 } }
			] } ] }
			""");

		var falha = await FalhaAsync(() => caso.RunAsync(cluster.Cenario()));

		falha.Message.ShouldContain("expected an instance of 'envio' called by this test case within 1s, but none showed up.");
	}

	// ---------- Elementos ----------

	[Fact(DisplayName = "JSON test cases: element states, counts, exact sets and order, by id or by the name in the BPMN")]
	public async Task Element_Assertions_Pass()
	{
		var cluster = Elementos();
		var caso = Caso("""
			{ "testCases": [ { "name": "Elementos", "instructions": [
			  { "type": "CREATE_PROCESS_INSTANCE", "processDefinitionSelector": { "processDefinitionId": "pedido" } },
			  { "type": "ASSERT_ELEMENT_INSTANCE", "elementSelector": { "elementName": "Cobrar" }, "state": "IS_COMPLETED", "amount": 1 },
			  { "type": "ASSERT_ELEMENT_INSTANCES", "elementSelectors": [ { "elementId": "inicio" }, { "elementName": "Cobrar" } ], "state": "IS_COMPLETED_IN_ORDER" },
			  { "type": "ASSERT_ELEMENT_INSTANCES", "elementSelectors": [ { "elementId": "enviar" } ], "state": "IS_ACTIVE_EXACTLY" },
			  { "type": "ASSERT_ELEMENT_INSTANCES", "elementSelectors": [ { "elementId": "cobrar" }, { "elementId": "fim" } ], "state": "IS_NOT_ACTIVE" },
			  { "type": "ASSERT_ELEMENT_INSTANCES", "elementSelectors": [ { "elementId": "fim" } ], "state": "IS_NOT_ACTIVATED" }
			] } ] }
			""");

		await caso.RunAsync(cluster.Cenario());
	}

	[Theory(DisplayName = "JSON test cases: an element assertion fails naming the element and what it is")]
	[InlineData("[ { \"elementId\": \"enviar\" } ], \"state\": \"IS_COMPLETED\"", "'enviar' is active")]
	[InlineData("[ { \"elementId\": \"fim\" } ], \"state\": \"IS_ACTIVE\"", "'fim' is not activated")]
	[InlineData("[ { \"elementId\": \"enviar\" } ], \"state\": \"IS_NOT_ACTIVE\"", "'enviar' is active")]
	[InlineData("[ { \"elementId\": \"cobrar\" } ], \"state\": \"IS_NOT_ACTIVATED\"", "'cobrar' was activated")]
	[InlineData("[ { \"elementId\": \"fim\" } ], \"state\": \"IS_ACTIVE_EXACTLY\"", "'fim' is not active")]
	[InlineData("[ ], \"state\": \"IS_ACTIVE_EXACTLY\"", "'enviar' is active too")]
	[InlineData("[ { \"elementId\": \"cobrar\" }, { \"elementId\": \"inicio\" } ], \"state\": \"IS_COMPLETED_IN_ORDER\"", "'inicio' did not complete after the elements before it")]
	[InlineData("[ { \"elementId\": \"inicio\" }, { \"elementId\": \"fim\" } ], \"state\": \"IS_COMPLETED_IN_ORDER\"", "'fim' is not completed")]
	[InlineData("[ { \"elementId\": \"inicio\" } ], \"state\": \"IS_SLEEPING\"", "state IS_SLEEPING is not supported")]
	public async Task Element_Assertion_Fails(string seletoresEEstado, string esperado)
	{
		var cluster = Elementos();
		var caso = Caso($$"""
			{ "testCases": [ { "name": "Elemento", "instructions": [
			  { "type": "CREATE_PROCESS_INSTANCE", "processDefinitionSelector": { "processDefinitionId": "pedido" } },
			  { "type": "ASSERT_ELEMENT_INSTANCES", "elementSelectors": {{seletoresEEstado}} }
			] } ] }
			""");

		var falha = await FalhaAsync(() => caso.RunAsync(cluster.Cenario()));

		falha.Message.ShouldContain($"within 1s, but {esperado}.");
	}

	[Fact(DisplayName = "JSON test cases: an element amount that differs fails with every state the element went through")]
	public async Task Element_Amount_Fails()
	{
		var cluster = Elementos();
		var caso = Caso("""
			{ "testCases": [ { "name": "Quantidade", "instructions": [
			  { "type": "CREATE_PROCESS_INSTANCE", "processDefinitionSelector": { "processDefinitionId": "pedido" } },
			  { "type": "ASSERT_ELEMENT_INSTANCE", "elementSelector": { "elementId": "cobrar" }, "state": "IS_COMPLETED", "amount": 2 }
			] } ] }
			""");

		var falha = await FalhaAsync(() => caso.RunAsync(cluster.Cenario()));

		falha.Message.ShouldContain("expected 'cobrar' is completed 2 time(s) within 1s, but 'cobrar' is completed.");
	}

	[Fact(DisplayName = "JSON test cases: an element name the BPMN does not have fails the case at once")]
	public async Task Unknown_Element_Name()
	{
		var cluster = Elementos();
		var caso = Caso("""
			{ "testCases": [ { "name": "Nome", "instructions": [
			  { "type": "CREATE_PROCESS_INSTANCE", "processDefinitionSelector": { "processDefinitionId": "pedido" } },
			  { "type": "ASSERT_ELEMENT_INSTANCE", "elementSelector": { "elementName": "Teletransportar" }, "state": "IS_ACTIVE" }
			] } ] }
			""");

		var falha = await FalhaAsync(() => caso.RunAsync(cluster.Cenario()));

		falha.Message.ShouldContain("no element of the process is named 'Teletransportar'.");
	}

	// ---------- Apoio ----------

	// inicio e cobrar completados, nessa ordem; enviar ativo; fim nunca ativado.
	private static ClusterFalso Elementos()
	{
		var cluster = new ClusterFalso
		{
			AoCriarInstancia = (c, chave) =>
			{
				c.Elemento(chave, "inicio", "COMPLETED");
				c.Elemento(chave, "cobrar", "COMPLETED");
				c.Elemento(chave, "enviar", "ACTIVE");
			}
		};
		cluster.DefinirBpmn(cluster.Publicar("pedido", 1), """
			<?xml version="1.0" encoding="UTF-8"?>
			<bpmn:definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL" id="d">
			  <bpmn:process id="pedido" isExecutable="true">
			    <bpmn:startEvent id="inicio" />
			    <bpmn:serviceTask id="cobrar" name="Cobrar" />
			    <bpmn:serviceTask id="enviar" name="Enviar" />
			    <bpmn:endEvent id="fim" />
			    <bpmn:sequenceFlow id="f1" sourceRef="inicio" targetRef="cobrar" />
			    <bpmn:sequenceFlow id="f2" sourceRef="cobrar" targetRef="enviar" />
			    <bpmn:sequenceFlow id="f3" sourceRef="enviar" targetRef="fim" />
			  </bpmn:process>
			</bpmn:definitions>
			""");
		return cluster;
	}

	// O pedido chama o envio em chamar-envio; o envio espera em despachar até o mock o terminar.
	private static ClusterFalso FilhoMockado()
	{
		var cluster = new ClusterFalso
		{
			AoCriarInstancia = (c, chave) =>
			{
				var filha = c.NovaInstancia("envio", chave, "chamar-envio", "def-envio");
				c.Elemento(filha, "despachar", "ACTIVE");
			},
			AoModificar = (c, chave) =>
			{
				c.Estado(chave, "COMPLETED");
				c.Estado("pi-1", "COMPLETED");
			}
		};
		cluster.Publicar("pedido", 1);
		cluster.DefinirBpmn("def-envio", """
			<?xml version="1.0" encoding="UTF-8"?>
			<bpmn:definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL" id="d">
			  <bpmn:process id="envio" isExecutable="true">
			    <bpmn:startEvent id="inicio" />
			    <bpmn:serviceTask id="despachar" />
			    <bpmn:endEvent id="fim" />
			  </bpmn:process>
			</bpmn:definitions>
			""");
		return cluster;
	}

	private static System.Text.Json.JsonElement Instrucao(string json) =>
		System.Text.Json.JsonDocument.Parse(json).RootElement.Clone();

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
