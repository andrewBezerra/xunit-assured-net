using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Time.Testing;

using Shouldly;
using Xunit;

using XUnitAssured.Camunda;
using XUnitAssured.Camunda.Client;

namespace XUnitAssured.Tests.CamundaTests;

/// <summary>
/// O cliente público do Camunda, sem cluster: a <see cref="ApiFalsa"/> responde embaixo do SDK como o
/// Camunda 8.10 e o provedor de identidade, e o <see cref="FakeTimeProvider"/> conta os prazos, sem
/// esperar de verdade.
/// </summary>
[Trait("Category", "Camunda")]
[Trait("Component", "Client")]
public class CamundaApiClientTests
{
	private const string Topologia = """{ "brokers": [], "clusterSize": 1, "partitionsCount": 1, "replicationFactor": 1, "gatewayVersion": "8.10.7" }""";
	private const string Pagina = "\"page\":{\"totalItems\":1,\"hasMoreTotalItems\":false}";
	private const string Token = "/realms/camunda/protocol/openid-connect/token";

	// ---------- 1. Uma conexão para muitas chamadas ----------

	[Fact(DisplayName = "Fifty calls in a row and twenty at once go through the one handler of the client")]
	public async Task Many_Calls_One_Handler()
	{
		var api = new ApiFalsa().Responder(HttpMethod.Get, "/v2/topology", Topologia);
		var cliente = Cliente(api);

		for (var i = 0; i < 50; i++)
			(await cliente.GetTopologyAsync()).GatewayVersion.ShouldBe("8.10.7");
		await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => cliente.GetTopologyAsync()));

		api.A("/v2/topology").Count.ShouldBe(70);
		cliente.HttpClientsCreated.ShouldBe(1);
	}

	[Fact(DisplayName = "For returns the same client for the same settings, and another for another secret")]
	public void For_Shares_By_Settings()
	{
		static CamundaSettings Config(string secret) => new()
		{
			RestAddress = "http://camunda.compartilhado.invalid:8080",
			Authentication = new CamundaAuthenticationSettings
			{
				Type = "OAuth", ClientId = "qa", ClientSecret = secret, OAuthUrl = "http://idp.invalid/token", Audience = "orchestration-api"
			}
		};

		var um = CamundaApiClient.For(Config("s1"));

		CamundaApiClient.For(Config("s1")).ShouldBeSameAs(um);
		CamundaApiClient.For(Config("s2")).ShouldNotBeSameAs(um);
	}

	[Fact(DisplayName = "The shared client pools its connections, renewing them every 5 minutes and closing the idle ones after 1")]
	public void For_Pools_Connections()
	{
		var cliente = CamundaApiClient.For(new CamundaSettings
		{
			RestAddress = "http://camunda.pool.invalid:8080",
			Authentication = new CamundaAuthenticationSettings { Type = "None" }
		});

		var transporte = cliente.Transport.ShouldBeOfType<SocketsHttpHandler>();
		transporte.PooledConnectionLifetime.ShouldBe(TimeSpan.FromMinutes(5));
		transporte.PooledConnectionIdleTimeout.ShouldBe(TimeSpan.FromMinutes(1));
	}

	[Fact(DisplayName = "The settings key carries a hash of the secret, never the secret")]
	public void Key_Has_No_Secret()
	{
		var chave = CamundaApiClient.Key(new CamundaSettings
		{
			RestAddress = "http://camunda:8080",
			Authentication = new CamundaAuthenticationSettings { Type = "Basic", Username = "demo", Password = "senha-que-nao-aparece" }
		});

		chave.ShouldNotContain("senha-que-nao-aparece");
		chave.ShouldContain("demo");
	}

	[Fact(DisplayName = "The scope of the token becomes CAMUNDA_OAUTH_SCOPE")]
	public void Scope_Becomes_Sdk_Key()
	{
		var config = XUnitAssured.Camunda.Gateway.SdkCamundaGateway.Configuracao(new CamundaSettings
		{
			Authentication = new CamundaAuthenticationSettings { Type = "OAuth", ClientId = "qa", Scope = "openid" }
		}, _ => null);

		config["CAMUNDA_OAUTH_SCOPE"].ShouldBe("openid");
	}

	// ---------- 2. As operações ----------

	[Fact(DisplayName = "The topology reads the gateway version")]
	public async Task Topology()
	{
		var api = new ApiFalsa().Responder(HttpMethod.Get, "/v2/topology", Topologia);

		(await Cliente(api).GetTopologyAsync()).GatewayVersion.ShouldBe("8.10.7");
	}

	[Fact(DisplayName = "The definitions are the latest of each process, or every version of one process")]
	public async Task Process_Definitions()
	{
		var api = new ApiFalsa().Responder(HttpMethod.Post, "/v2/process-definitions/search", $$"""
			{ "items": [ { "name": "Pedido", "resourceName": "pedido.bpmn", "version": 3, "versionTag": "v3", "processDefinitionId": "pedido",
			  "tenantId": "<default>", "processDefinitionKey": "2251799813685249", "hasStartForm": false } ], {{Pagina}} }
			""");
		var cliente = Cliente(api);

		var ultimas = await cliente.SearchProcessDefinitionsAsync(null);
		await cliente.SearchProcessDefinitionsAsync("pedido");

		var definicao = ultimas.ShouldHaveSingleItem();
		definicao.ShouldBe(new CamundaProcessDefinition("2251799813685249", "pedido", "Pedido", 3, "v3", "<default>"));
		var pedidos = api.A("/v2/process-definitions/search");
		pedidos[0].Json["filter"]!["isLatestVersion"]!.GetValue<bool>().ShouldBeTrue();
		pedidos[0].Json["page"]!["limit"]!.GetValue<int>().ShouldBe(CamundaApiClient.PageSize);
		pedidos[1].Json["filter"]!["processDefinitionId"]!.ToJsonString().ShouldContain("pedido");
		pedidos[1].Json["filter"]!["isLatestVersion"].ShouldBeNull();
	}

	[Fact(DisplayName = "The XML of a definition comes back as the cluster keeps it")]
	public async Task Definition_Xml()
	{
		var api = new ApiFalsa().Responder(HttpMethod.Get, "/v2/process-definitions/2251799813685249/xml", "<definitions id=\"x\"/>", tipo: "text/xml");

		(await Cliente(api).GetProcessDefinitionXmlAsync("2251799813685249")).ShouldBe("<definitions id=\"x\"/>");
	}

	[Fact(DisplayName = "The deployment sends every resource as multipart and returns the process definitions")]
	public async Task Deploy()
	{
		var api = new ApiFalsa().Responder(HttpMethod.Post, "/v2/deployments", """
			{ "deploymentKey": "2251799813685200", "tenantId": "<default>", "deployments": [
			  { "processDefinition": { "processDefinitionId": "pedido", "processDefinitionVersion": 3, "resourceName": "pedido.bpmn",
			    "tenantId": "<default>", "processDefinitionKey": "2251799813685249" } },
			  { "form": { "formId": "aprovar", "version": 1, "resourceName": "aprovar.form", "tenantId": "<default>", "formKey": "2251799813685300" } }
			] }
			""");

		var implantados = await Cliente(api).DeployAsync(new[]
		{
			new CamundaDeploymentResource("pedido.bpmn", Encoding.UTF8.GetBytes("<definitions/>"), "application/xml"),
			new CamundaDeploymentResource("aprovar.form", Encoding.UTF8.GetBytes("{}"), "application/json")
		});

		implantados.ShouldHaveSingleItem().ShouldBe(new CamundaDeployedProcess("pedido", "2251799813685249", 3));
		var pedido = api.A("/v2/deployments").ShouldHaveSingleItem();
		pedido.TipoDoConteudo.ShouldBe("multipart/form-data");
		pedido.Corpo!.ShouldContain("filename=pedido.bpmn");
		pedido.Corpo!.ShouldContain("filename=aprovar.form");
	}

	[Fact(DisplayName = "Starting an instance sends the definition key and the variables, and returns the instance key")]
	public async Task Create_Instance()
	{
		var api = new ApiFalsa().Responder(HttpMethod.Post, "/v2/process-instances", """
			{ "processDefinitionId": "pedido", "processDefinitionVersion": 3, "tenantId": "<default>", "variables": {},
			  "processDefinitionKey": "2251799813685249", "processInstanceKey": "2251799813690001", "tags": [] }
			""");

		var chave = await Cliente(api).CreateProcessInstanceAsync("2251799813685249", new JsonObject { ["valor"] = 42 });

		chave.ShouldBe("2251799813690001");
		var corpo = api.A("/v2/process-instances").ShouldHaveSingleItem().Json;
		corpo["processDefinitionKey"]!.GetValue<string>().ShouldBe("2251799813685249");
		corpo["variables"]!["valor"]!.GetValue<int>().ShouldBe(42);
	}

	[Fact(DisplayName = "Activating jobs sends the type, the worker, the job timeout and the long wait, and reads the variables")]
	public async Task Activate_Jobs()
	{
		var api = new ApiFalsa().Responder(HttpMethod.Post, "/v2/jobs/activation", """
			{ "jobs": [ { "type": "qa-x", "processDefinitionId": "pedido", "processDefinitionVersion": 3, "elementId": "Cobrar",
			  "customHeaders": {}, "worker": "qualityassured", "retries": 2, "deadline": 1, "variables": { "valor": 42 }, "tenantId": "<default>",
			  "jobKey": "2251799813690010", "processInstanceKey": "2251799813690001", "processDefinitionKey": "2251799813685249",
			  "elementInstanceKey": "2251799813690009", "kind": "BPMN_ELEMENT", "listenerEventType": "UNSPECIFIED", "tags": [], "priority": 0 } ] }
			""");

		var jobs = await Cliente(api).ActivateJobsAsync(
			new CamundaJobActivationRequest("qa-x", 32, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(1), "qualityassured"));

		var job = jobs.ShouldHaveSingleItem();
		(job.JobKey, job.Type, job.ElementId, job.ProcessInstanceKey, job.Retries).ShouldBe(("2251799813690010", "qa-x", "Cobrar", "2251799813690001", 2));
		job.Variables["valor"]!.GetValue<int>().ShouldBe(42);
		var corpo = api.A("/v2/jobs/activation").ShouldHaveSingleItem().Json;
		corpo["type"]!.GetValue<string>().ShouldBe("qa-x");
		corpo["worker"]!.GetValue<string>().ShouldBe("qualityassured");
		corpo["timeout"]!.GetValue<long>().ShouldBe(30_000);
		corpo["maxJobsToActivate"]!.GetValue<int>().ShouldBe(32);
		corpo["requestTimeout"]!.GetValue<long>().ShouldBe(1_000);
	}

	[Fact(DisplayName = "The job commands post to their routes, and are false when the job no longer exists")]
	public async Task Job_Commands()
	{
		var api = new ApiFalsa()
			.Responder(HttpMethod.Post, "/v2/jobs/1/completion", null, HttpStatusCode.NoContent)
			.Responder(HttpMethod.Post, "/v2/jobs/2/error", null, HttpStatusCode.NoContent)
			.Responder(HttpMethod.Post, "/v2/jobs/3/failure", null, HttpStatusCode.NoContent)
			.Responder(HttpMethod.Post, "/v2/jobs/4/failure", null, HttpStatusCode.NoContent)
			.Responder(HttpMethod.Patch, "/v2/jobs/5", null, HttpStatusCode.NoContent);
		var cliente = Cliente(api);

		(await cliente.CompleteJobAsync("1", new JsonObject { ["ok"] = true })).ShouldBeTrue();
		(await cliente.ThrowJobErrorAsync("2", "SEM_SALDO", "Sem saldo")).ShouldBeTrue();
		(await cliente.FailJobAsync("3", "Quebrou")).ShouldBeTrue();
		(await cliente.ReturnJobAsync("4", 2, "De outra instância")).ShouldBeTrue();
		(await cliente.UpdateJobTimeoutAsync("5", TimeSpan.FromMinutes(5))).ShouldBeTrue();
		(await cliente.CompleteJobAsync("99", new JsonObject())).ShouldBeFalse();

		api.A("/v2/jobs/1/completion")[0].Json["variables"]!["ok"]!.GetValue<bool>().ShouldBeTrue();
		var erro = api.A("/v2/jobs/2/error")[0].Json;
		(erro["errorCode"]!.GetValue<string>(), erro["errorMessage"]!.GetValue<string>()).ShouldBe(("SEM_SALDO", "Sem saldo"));
		var falha = api.A("/v2/jobs/3/failure")[0].Json;
		(falha["retries"]!.GetValue<int>(), falha["errorMessage"]!.GetValue<string>()).ShouldBe((0, "Quebrou"));
		var devolvido = api.A("/v2/jobs/4/failure")[0].Json;
		(devolvido["retries"]!.GetValue<int>(), devolvido["retryBackOff"]!.GetValue<long>()).ShouldBe((2, 0L));
		api.A("/v2/jobs/5")[0].Json["changeset"]!["timeout"]!.GetValue<long>().ShouldBe(300_000);
	}

	[Fact(DisplayName = "Cancelling an instance is false when it already ended")]
	public async Task Cancel_Instance()
	{
		var api = new ApiFalsa().Responder(HttpMethod.Post, "/v2/process-instances/7/cancellation", null, HttpStatusCode.NoContent);
		var cliente = Cliente(api);

		(await cliente.CancelProcessInstanceAsync("7")).ShouldBeTrue();
		(await cliente.CancelProcessInstanceAsync("8")).ShouldBeFalse();
	}

	[Fact(DisplayName = "The user tasks are searched by instance, element and state, and read with the form and the groups")]
	public async Task User_Tasks()
	{
		var api = new ApiFalsa().Responder(HttpMethod.Post, "/v2/user-tasks/search", $$"""
			{ "items": [ { "name": "Aprovar", "state": "CREATED", "assignee": null, "elementId": "Aprovar", "candidateGroups": ["gerentes"],
			  "candidateUsers": [], "processDefinitionId": "pedido", "creationDate": "2026-11-05T10:00:00.000Z", "tenantId": "<default>",
			  "processDefinitionVersion": 3, "customHeaders": {}, "priority": 50, "userTaskKey": "2251799813690020",
			  "elementInstanceKey": "2251799813690019", "processDefinitionKey": "2251799813685249", "processInstanceKey": "2251799813690001",
			  "formKey": "2251799813685300", "tags": [] } ], {{Pagina}} }
			""");
		var cliente = Cliente(api);

		var tarefas = await cliente.SearchUserTasksAsync("2251799813690001", elementId: "Aprovar");
		await cliente.SearchUserTasksAsync("2251799813690001", state: "CREATED");

		tarefas.ShouldHaveSingleItem().ShouldBe(new CamundaUserTaskInfo("2251799813690020", "Aprovar", "CREATED", null, "Aprovar",
			"2251799813685300", tarefas[0].CandidateGroups, "2251799813690001", "pedido"));
		tarefas[0].CandidateGroups.ShouldBe(new[] { "gerentes" });
		var pedidos = api.A("/v2/user-tasks/search");
		pedidos[0].Json["filter"]!["elementId"]!.GetValue<string>().ShouldBe("Aprovar");
		pedidos[0].Json["filter"]!["state"].ShouldBeNull();
		pedidos[1].Json["filter"]!["state"]!.ToJsonString().ShouldContain("CREATED");
		pedidos[1].Json["page"]!["limit"]!.GetValue<int>().ShouldBe(1000);
	}

	[Fact(DisplayName = "Assigning and completing a user task are false when the task no longer exists")]
	public async Task User_Task_Commands()
	{
		var api = new ApiFalsa()
			.Responder(HttpMethod.Post, "/v2/user-tasks/20/assignment", null, HttpStatusCode.NoContent)
			.Responder(HttpMethod.Post, "/v2/user-tasks/20/completion", null, HttpStatusCode.NoContent);
		var cliente = Cliente(api);

		(await cliente.AssignUserTaskAsync("20", "qualityassured")).ShouldBeTrue();
		(await cliente.CompleteUserTaskAsync("20", new JsonObject { ["aprovado"] = true })).ShouldBeTrue();
		(await cliente.CompleteUserTaskAsync("21", new JsonObject())).ShouldBeFalse();

		var atribuicao = api.A("/v2/user-tasks/20/assignment")[0].Json;
		(atribuicao["assignee"]!.GetValue<string>(), atribuicao["allowOverride"]!.GetValue<bool>()).ShouldBe(("qualityassured", true));
		api.A("/v2/user-tasks/20/completion")[0].Json["variables"]!["aprovado"]!.GetValue<bool>().ShouldBeTrue();
	}

	[Fact(DisplayName = "The instances are searched by parent, process, state and a variable in JSON, without escaping accents")]
	public async Task Process_Instances()
	{
		var api = new ApiFalsa().Responder(HttpMethod.Post, "/v2/process-instances/search", $$"""
			{ "items": [ { "processInstanceKey": "2251799813690002", "processDefinitionId": "retencao", "processDefinitionName": "Retenção",
			  "processDefinitionVersion": 1, "startDate": "2026-11-05T10:00:00.000Z", "state": "ACTIVE", "hasIncident": false, "tenantId": "<default>",
			  "processDefinitionKey": "2251799813685250", "parentProcessInstanceKey": "2251799813690001", "parentElementInstanceKey": "2251799813690005",
			  "tags": [] } ], {{Pagina}} }
			""");
		var cliente = Cliente(api);

		var filhas = await cliente.SearchProcessInstancesAsync(new CamundaProcessInstanceFilter { ParentProcessInstanceKey = "2251799813690001" });
		await cliente.SearchProcessInstancesAsync(new CamundaProcessInstanceFilter
		{
			ProcessId = "retencao", State = "ACTIVE", VariableName = "nu", VariableValue = JsonValue.Create("ção")
		});

		filhas.ShouldHaveSingleItem().ShouldBe(new CamundaProcessInstance("2251799813690002", "ACTIVE", false, "retencao", "2251799813690001", "2251799813690005"));
		var pedidos = api.A("/v2/process-instances/search");
		pedidos[0].Json["filter"]!["parentProcessInstanceKey"]!.ToJsonString().ShouldContain("2251799813690001");
		var variavel = pedidos[1].Json["filter"]!["variables"]![0]!;
		variavel["name"]!.GetValue<string>().ShouldBe("nu");
		// O valor é o JSON da variável, que o Camunda compara com o que guardou: o texto entre aspas.
		variavel["value"]!["$eq"]!.GetValue<string>().ShouldBe("\"ção\"");
	}

	[Fact(DisplayName = "The open message subscriptions of the instance")]
	public async Task Message_Subscriptions()
	{
		var api = new ApiFalsa().Responder(HttpMethod.Post, "/v2/message-subscriptions/search", $$"""
			{ "items": [ { "messageSubscriptionKey": "2251799813690030", "processDefinitionId": "pedido", "processInstanceKey": "2251799813690001",
			  "elementId": "Pago", "elementInstanceKey": "2251799813690029", "messageSubscriptionState": "CREATED",
			  "lastUpdatedDate": "2026-11-05T10:00:00.000Z", "messageName": "pagamento", "correlationKey": "42", "tenantId": "<default>" } ], {{Pagina}} }
			""");

		var assinaturas = await Cliente(api).SearchMessageSubscriptionsAsync("2251799813690001");

		assinaturas.ShouldHaveSingleItem().ShouldBe(new CamundaMessageSubscriptionInfo("2251799813690030", "Pago", "pagamento", "42"));
		api.A("/v2/message-subscriptions/search")[0].Json["filter"]!["messageSubscriptionState"]!.ToJsonString().ShouldContain("CREATED");
	}

	[Fact(DisplayName = "Correlating a message is false when no subscription waits for it")]
	public async Task Correlate_Message()
	{
		var api = new ApiFalsa().Responder(HttpMethod.Post, "/v2/messages/correlation",
			"""{ "tenantId": "<default>", "messageKey": "2251799813690040", "processInstanceKey": "2251799813690001" }""");
		var cliente = Cliente(api);

		(await cliente.CorrelateMessageAsync("pagamento", "42", new JsonObject { ["pago"] = true })).ShouldBeTrue();

		var corpo = api.A("/v2/messages/correlation")[0].Json;
		(corpo["name"]!.GetValue<string>(), corpo["correlationKey"]!.GetValue<string>()).ShouldBe(("pagamento", "42"));
		corpo["variables"]!["pago"]!.GetValue<bool>().ShouldBeTrue();

		var semAssinatura = new ApiFalsa().Recusar(HttpMethod.Post, "/v2/messages/correlation", HttpStatusCode.NotFound, "NOT_FOUND", "Ninguém espera");
		(await Cliente(semAssinatura).CorrelateMessageAsync("pagamento", "42", new JsonObject())).ShouldBeFalse();
	}

	[Fact(DisplayName = "Reading an instance is null while the search does not have it yet")]
	public async Task Get_Instance()
	{
		var api = new ApiFalsa().Responder(HttpMethod.Get, "/v2/process-instances/2251799813690001", """
			{ "processInstanceKey": "2251799813690001", "processDefinitionId": "pedido", "processDefinitionName": "Pedido", "processDefinitionVersion": 3,
			  "startDate": "2026-11-05T10:00:00.000Z", "state": "COMPLETED", "hasIncident": true, "tenantId": "<default>",
			  "processDefinitionKey": "2251799813685249", "tags": [] }
			""");
		var cliente = Cliente(api);

		var instancia = await cliente.GetProcessInstanceAsync("2251799813690001");
		var nenhuma = await cliente.GetProcessInstanceAsync("2251799813690099");

		instancia.ShouldNotBeNull();
		(instancia.Key, instancia.State, instancia.HasIncident, instancia.ProcessId).ShouldBe(("2251799813690001", "COMPLETED", true, "pedido"));
		nenhuma.ShouldBeNull();
	}

	[Fact(DisplayName = "The elements come in key order, with their dates and the total of the instance")]
	public async Task Element_Instances()
	{
		var api = new ApiFalsa().Responder(HttpMethod.Post, "/v2/element-instances/search", """
			{ "items": [ { "startDate": "2026-11-05T10:00:00.000Z", "endDate": "2026-11-05T10:00:01.000Z", "elementId": "Cobrar", "elementName": "Cobrar",
			  "type": "SERVICE_TASK", "state": "COMPLETED", "hasIncident": false, "tenantId": "<default>", "elementInstanceKey": "2251799813690009",
			  "processInstanceKey": "2251799813690001", "processDefinitionKey": "2251799813685249" } ],
			  "page": { "totalItems": 1500, "hasMoreTotalItems": false } }
			""");

		var pagina = await Cliente(api).SearchElementInstancesAsync("2251799813690001");

		pagina.TotalItems.ShouldBe(1500);
		var elemento = pagina.Items.ShouldHaveSingleItem();
		(elemento.ElementId, elemento.Type, elemento.State, elemento.Key).ShouldBe(("Cobrar", "SERVICE_TASK", "COMPLETED", 2251799813690009L));
		elemento.EndDate.ShouldBe(DateTimeOffset.Parse("2026-11-05T10:00:01Z", System.Globalization.CultureInfo.InvariantCulture));
		var corpo = api.A("/v2/element-instances/search")[0].Json;
		corpo["sort"]![0]!["field"]!.GetValue<string>().ShouldBe("elementInstanceKey");
		corpo["sort"]![0]!["order"]!.GetValue<string>().ShouldBe("ASC");
		corpo["page"]!["limit"]!.GetValue<int>().ShouldBe(1000);
	}

	[Fact(DisplayName = "The incidents of the instance, with the Camunda name of the error type")]
	public async Task Incidents()
	{
		var api = new ApiFalsa().Responder(HttpMethod.Post, "/v2/incidents/search", $$"""
			{ "items": [ { "processDefinitionId": "pedido", "errorType": "JOB_NO_RETRIES", "errorMessage": "Quebrou", "elementId": "Cobrar",
			  "creationTime": "2026-11-05T10:00:00.000Z", "state": "ACTIVE", "tenantId": "<default>", "incidentKey": "2251799813690050",
			  "processDefinitionKey": "2251799813685249", "processInstanceKey": "2251799813690001", "elementInstanceKey": "2251799813690009" } ], {{Pagina}} }
			""");

		var incidentes = await Cliente(api).SearchIncidentsAsync("2251799813690001");

		incidentes.ShouldHaveSingleItem().ShouldBe(new CamundaIncidentInfo("Cobrar", "JOB_NO_RETRIES", "Quebrou"));
		api.A("/v2/incidents/search")[0].Json["filter"]!["processInstanceKey"]!.ToJsonString().ShouldContain("2251799813690001");
	}

	[Fact(DisplayName = "A refusal carries the status, the title, the detail and the path called")]
	public async Task Refusal()
	{
		var api = new ApiFalsa().Recusar(HttpMethod.Post, "/v2/process-instances", HttpStatusCode.BadRequest, "INVALID_ARGUMENT", "Sem a definição");

		var recusa = await Should.ThrowAsync<CamundaApiException>(() => Cliente(api).CreateProcessInstanceAsync("1", new JsonObject()));

		(recusa.Status, recusa.Title, recusa.Detail, recusa.Path).ShouldBe((400, "INVALID_ARGUMENT", "Sem a definição", "/v2/process-instances"));
	}

	[Fact(DisplayName = "A refusal that is not a problem document keeps only the status")]
	public async Task Refusal_Without_Problem()
	{
		var api = new ApiFalsa().Responder(HttpMethod.Get, "/v2/topology", "<html>bad gateway</html>", HttpStatusCode.BadGateway, "text/html");

		var recusa = await Should.ThrowAsync<CamundaApiException>(() => Cliente(api).GetTopologyAsync());

		(recusa.Status, recusa.Detail, recusa.Path).ShouldBe((502, null, "/v2/topology"));
	}

	[Fact(DisplayName = "A 404 that is not a command is a refusal, with the path")]
	public async Task Not_Found_Is_Refusal()
	{
		var recusa = await Should.ThrowAsync<CamundaApiException>(() => Cliente(new ApiFalsa()).GetProcessDefinitionXmlAsync("1"));

		(recusa.Status, recusa.Path).ShouldBe((404, "/v2/process-definitions/1/xml"));
	}

	// ---------- 3. O prazo ----------

	[Fact(DisplayName = "A call past its deadline is unavailable, saying the deadline, with no real wait")]
	public async Task Deadline()
	{
		var tempo = new FakeTimeProvider();
		var api = new ApiFalsa().Pendurar(HttpMethod.Get, "/v2/topology");
		var chamada = Cliente(api, tempo).GetTopologyAsync();
		await api.EsperarPedidos(1);

		tempo.Advance(TimeSpan.FromSeconds(9));
		chamada.IsCompleted.ShouldBeFalse();
		tempo.Advance(TimeSpan.FromSeconds(1));

		var fora = await Should.ThrowAsync<CamundaUnavailableException>(() => chamada);
		(fora.Target, fora.Timeout).ShouldBe((CamundaEndpoint.Camunda, TimeSpan.FromSeconds(10)));
	}

	[Fact(DisplayName = "The deployment waits 30 s, and a deadline given wins")]
	public async Task Deploy_And_Given_Deadline()
	{
		var tempo = new FakeTimeProvider();
		var api = new ApiFalsa().Pendurar(HttpMethod.Post, "/v2/deployments").Pendurar(HttpMethod.Get, "/v2/topology");
		var cliente = Cliente(api, tempo);
		var implantar = cliente.DeployAsync(new[] { new CamundaDeploymentResource("a.bpmn", new byte[] { 1 }, "application/xml") });
		var topologia = cliente.GetTopologyAsync(timeout: TimeSpan.FromSeconds(2));
		await api.EsperarPedidos(2);

		tempo.Advance(TimeSpan.FromSeconds(2));
		(await Should.ThrowAsync<CamundaUnavailableException>(() => topologia)).Timeout.ShouldBe(TimeSpan.FromSeconds(2));
		tempo.Advance(TimeSpan.FromSeconds(27));
		implantar.IsCompleted.ShouldBeFalse();
		tempo.Advance(TimeSpan.FromSeconds(1));
		(await Should.ThrowAsync<CamundaUnavailableException>(() => implantar)).Timeout.ShouldBe(TimeSpan.FromSeconds(30));
	}

	[Fact(DisplayName = "The activation waits the long wait plus 10 s")]
	public async Task Activation_Deadline()
	{
		var tempo = new FakeTimeProvider();
		var api = new ApiFalsa().Pendurar(HttpMethod.Post, "/v2/jobs/activation");
		var ativar = Cliente(api, tempo).ActivateJobsAsync(
			new CamundaJobActivationRequest("qa-x", 32, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(1), "qualityassured"));
		await api.EsperarPedidos(1);

		tempo.Advance(TimeSpan.FromMilliseconds(10_999));
		ativar.IsCompleted.ShouldBeFalse();
		tempo.Advance(TimeSpan.FromMilliseconds(1));

		(await Should.ThrowAsync<CamundaUnavailableException>(() => ativar)).Timeout.ShouldBe(TimeSpan.FromSeconds(11));
	}

	[Fact(DisplayName = "A call cancelled by the caller is an OperationCanceledException, not a deadline")]
	public async Task Caller_Cancels()
	{
		var api = new ApiFalsa().Pendurar(HttpMethod.Get, "/v2/topology");
		using var cancelar = new CancellationTokenSource();
		var chamada = Cliente(api, new FakeTimeProvider()).GetTopologyAsync(cancellationToken: cancelar.Token);
		await api.EsperarPedidos(1);

		cancelar.Cancel();

		await Should.ThrowAsync<OperationCanceledException>(() => chamada);
	}

	[Fact(DisplayName = "A connection that fails is unavailable, with the reason")]
	public async Task Connection_Fails()
	{
		var api = new ApiFalsa().SemResposta(HttpMethod.Get, "/v2/topology");

		var fora = await Should.ThrowAsync<CamundaUnavailableException>(() => Cliente(api).GetTopologyAsync());

		(fora.Target, fora.Timeout).ShouldBe((CamundaEndpoint.Camunda, (TimeSpan?)null));
		fora.Reason.ShouldContain("Nenhuma conexão");
	}

	// ---------- 3a. A espera de consistência ----------

	[Fact(DisplayName = "The settled elements are searched again while one is active, and come back once none is")]
	public async Task Settled_Elements()
	{
		var tempo = new FakeTimeProvider();
		var api = new ApiFalsa()
			.Responder(HttpMethod.Post, "/v2/element-instances/search", Elementos("ACTIVE"))
			.Responder(HttpMethod.Post, "/v2/element-instances/search", Elementos("ACTIVE"))
			.Responder(HttpMethod.Post, "/v2/element-instances/search", Elementos("COMPLETED"));

		var elementos = await AteTerminar(Cliente(api, tempo).SearchSettledElementsAsync("2251799813690001", "COMPLETED"), tempo);

		elementos.ShouldHaveSingleItem().State.ShouldBe("COMPLETED");
		api.A("/v2/element-instances/search").Count.ShouldBe(3);
	}

	[Fact(DisplayName = "After the attempts, the settled elements are the last ones seen, without an error")]
	public async Task Settled_Elements_Give_Up()
	{
		var tempo = new FakeTimeProvider();
		var api = new ApiFalsa().Responder(HttpMethod.Post, "/v2/element-instances/search", Elementos("ACTIVE"));

		var elementos = await AteTerminar(
			Cliente(api, tempo).SearchSettledElementsAsync("2251799813690001", "TERMINATED", attempts: 3, interval: TimeSpan.FromMilliseconds(200)), tempo);

		elementos.ShouldHaveSingleItem().State.ShouldBe("ACTIVE");
		api.A("/v2/element-instances/search").Count.ShouldBe(3);
	}

	[Fact(DisplayName = "An instance left active is searched once")]
	public async Task Settled_Elements_Active_Instance()
	{
		var api = new ApiFalsa().Responder(HttpMethod.Post, "/v2/element-instances/search", Elementos("ACTIVE"));

		var elementos = await Cliente(api, new FakeTimeProvider()).SearchSettledElementsAsync("2251799813690001", "ACTIVE");

		elementos.ShouldHaveSingleItem();
		api.A("/v2/element-instances/search").Count.ShouldBe(1);
	}

	[Fact(DisplayName = "The settled elements wait 500 ms between searches by default")]
	public async Task Settled_Elements_Interval()
	{
		var tempo = new FakeTimeProvider();
		var api = new ApiFalsa()
			.Responder(HttpMethod.Post, "/v2/element-instances/search", Elementos("ACTIVE"))
			.Responder(HttpMethod.Post, "/v2/element-instances/search", Elementos("COMPLETED"));
		var busca = Cliente(api, tempo).SearchSettledElementsAsync("2251799813690001", "COMPLETED");
		await api.EsperarPedidos(1);

		tempo.Advance(TimeSpan.FromMilliseconds(499));
		await Task.Delay(50);
		api.Pedidos.Count.ShouldBe(1);
		tempo.Advance(TimeSpan.FromMilliseconds(1));

		(await busca).ShouldHaveSingleItem().State.ShouldBe("COMPLETED");
	}

	// ---------- 4. O token ----------

	[Fact(DisplayName = "The token is asked once, with the client credentials, the audience and the scope, and kept between calls")]
	public async Task Token_Kept()
	{
		var api = new ApiFalsa().Responder(HttpMethod.Get, "/v2/topology", Topologia).Responder(HttpMethod.Post, Token, TokenDe("t1", 300));
		var cliente = Cliente(api, configuracao: OAuth());

		await cliente.GetTopologyAsync();
		await cliente.GetTopologyAsync();

		var pedido = api.A(Token).ShouldHaveSingleItem();
		pedido.Host.ShouldBe("idp.falso");
		pedido.Corpo!.ShouldContain("grant_type=client_credentials");
		pedido.Corpo!.ShouldContain("client_id=qa");
		pedido.Corpo!.ShouldContain("audience=orchestration-api");
		pedido.Corpo!.ShouldContain("scope=openid");
		api.A("/v2/topology").Select(p => p.Autorizacao).ShouldBe(new[] { "Bearer t1", "Bearer t1" });
	}

	[Fact(DisplayName = "The token is renewed 30 s before it expires")]
	public async Task Token_Renewed_Before_Expiry()
	{
		var tempo = new FakeTimeProvider();
		var api = new ApiFalsa().Responder(HttpMethod.Get, "/v2/topology", Topologia)
			.Responder(HttpMethod.Post, Token, TokenDe("t1", 60)).Responder(HttpMethod.Post, Token, TokenDe("t2", 60));
		var cliente = Cliente(api, tempo, OAuth());

		await cliente.GetTopologyAsync();
		tempo.Advance(TimeSpan.FromSeconds(29));
		await cliente.GetTopologyAsync();
		tempo.Advance(TimeSpan.FromSeconds(2));
		await cliente.GetTopologyAsync();

		api.A("/v2/topology").Select(p => p.Autorizacao).ShouldBe(new[] { "Bearer t1", "Bearer t1", "Bearer t2" });
	}

	[Fact(DisplayName = "A token the Camunda refuses is renewed once, and the call says so")]
	public async Task Token_Refused_Renewed_Once()
	{
		var api = new ApiFalsa()
			.Responder(HttpMethod.Get, "/v2/topology", "{}", HttpStatusCode.Unauthorized).Responder(HttpMethod.Get, "/v2/topology", Topologia)
			.Responder(HttpMethod.Post, Token, TokenDe("t1", 300)).Responder(HttpMethod.Post, Token, TokenDe("t2", 300));
		var chamadas = new List<CamundaCall>();

		await Cliente(api, configuracao: OAuth()).GetTopologyAsync(observe: chamadas.Add);

		api.A("/v2/topology").Select(p => p.Autorizacao).ShouldBe(new[] { "Bearer t1", "Bearer t2" });
		chamadas.Where(c => c.Kind == CamundaCallKind.Api).Select(c => (c.Status, c.WithRenewedToken)).ShouldBe(new[] { ((int?)401, false), (200, true) });
	}

	[Fact(DisplayName = "The Camunda refusing the renewed token too is a credential refusal by the Camunda")]
	public async Task Token_Refused_Twice()
	{
		var api = new ApiFalsa().Responder(HttpMethod.Get, "/v2/topology", "{}", HttpStatusCode.Forbidden).Responder(HttpMethod.Post, Token, TokenDe("t", 300));

		var recusa = await Should.ThrowAsync<CamundaCredentialException>(() => Cliente(api, configuracao: OAuth()).GetTopologyAsync());

		(recusa.Refusal, recusa.Status).ShouldBe((CamundaCredentialRefusal.Camunda, (int?)403));
		api.A("/v2/topology").Count.ShouldBe(2);
	}

	[Fact(DisplayName = "The identity provider refusing says its status and description, without the secret")]
	public async Task Provider_Refuses()
	{
		var api = new ApiFalsa().Responder(HttpMethod.Post, Token,
			"""{ "error": "unauthorized_client", "error_description": "Invalid client secret s3cr3t" }""", HttpStatusCode.Unauthorized);

		var recusa = await Should.ThrowAsync<CamundaCredentialException>(() => Cliente(api, configuracao: OAuth()).GetTopologyAsync());

		(recusa.Refusal, recusa.Status, recusa.Description).ShouldBe((CamundaCredentialRefusal.IdentityProvider, (int?)401, "Invalid client secret ***"));
		recusa.Endpoint.ShouldBe("http://idp.falso" + Token);
		recusa.Message.ShouldNotContain("s3cr3t");
		api.A("/v2/topology").ShouldBeEmpty();
	}

	[Fact(DisplayName = "A provider answer without the access token is a credential refusal of its own")]
	public async Task Provider_Without_Token()
	{
		var api = new ApiFalsa().Responder(HttpMethod.Post, Token, """{ "token_type": "Bearer" }""");

		var recusa = await Should.ThrowAsync<CamundaCredentialException>(() => Cliente(api, configuracao: OAuth()).GetTopologyAsync());

		recusa.Refusal.ShouldBe(CamundaCredentialRefusal.MissingAccessToken);
	}

	[Fact(DisplayName = "A provider that does not answer is unavailable, naming the identity provider")]
	public async Task Provider_Unavailable()
	{
		var api = new ApiFalsa().SemResposta(HttpMethod.Post, Token);

		var fora = await Should.ThrowAsync<CamundaUnavailableException>(() => Cliente(api, configuracao: OAuth()).GetTopologyAsync());

		(fora.Target, fora.Endpoint).ShouldBe((CamundaEndpoint.IdentityProvider, "http://idp.falso" + Token));
	}

	[Fact(DisplayName = "Twenty calls at once with no token yet ask the provider once")]
	public async Task Token_Single_Flight()
	{
		var portao = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
		var api = new ApiFalsa().Responder(HttpMethod.Get, "/v2/topology", Topologia)
			.Com(HttpMethod.Post, Token, async (pedido, _) =>
			{
				await portao.Task.ConfigureAwait(false);
				return new HttpResponseMessage(HttpStatusCode.OK) { RequestMessage = pedido, Content = new StringContent(TokenDe("t", 300), Encoding.UTF8, "application/json") };
			});
		var cliente = Cliente(api, configuracao: OAuth());

		var chamadas = Enumerable.Range(0, 20).Select(_ => cliente.GetTopologyAsync()).ToArray();
		await api.EsperarPedidos(1);
		await Task.Delay(100);
		portao.SetResult(true);
		await Task.WhenAll(chamadas);

		api.A(Token).Count.ShouldBe(1);
		api.A("/v2/topology").Count.ShouldBe(20);
	}

	[Fact(DisplayName = "Basic sends the user and the password, and a refusal is a credential refusal by the Camunda")]
	public async Task Basic()
	{
		var api = new ApiFalsa().Responder(HttpMethod.Get, "/v2/topology", Topologia).Responder(HttpMethod.Get, "/v2/topology", "{}", HttpStatusCode.Unauthorized);
		var cliente = Cliente(api, configuracao: new CamundaAuthenticationSettings { Type = "Basic", Username = "demo", Password = "demo" });

		await cliente.GetTopologyAsync();
		var recusa = await Should.ThrowAsync<CamundaCredentialException>(() => cliente.GetTopologyAsync());

		api.A("/v2/topology")[0].Autorizacao.ShouldBe("Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("demo:demo")));
		(recusa.Refusal, recusa.Status).ShouldBe((CamundaCredentialRefusal.Camunda, (int?)401));
	}

	// ---------- 5. A repetição dos comandos do job ----------

	[Fact(DisplayName = "A job command the Camunda did not answer is tried again, up to 3 times, 1 s apart")]
	public async Task Job_Command_Retried()
	{
		var tempo = new FakeTimeProvider();
		var api = new ApiFalsa().SemResposta(HttpMethod.Post, "/v2/jobs/1/completion").SemResposta(HttpMethod.Post, "/v2/jobs/1/completion")
			.Responder(HttpMethod.Post, "/v2/jobs/1/completion", null, HttpStatusCode.NoContent);

		(await AteTerminar(Cliente(api, tempo).CompleteJobAsync("1", new JsonObject()), tempo, TimeSpan.FromSeconds(1))).ShouldBeTrue();

		api.A("/v2/jobs/1/completion").Count.ShouldBe(3);
	}

	[Fact(DisplayName = "A job that no longer exists on a new attempt counts as the success of the earlier one")]
	public async Task Job_Command_Not_Found_On_Retry()
	{
		var tempo = new FakeTimeProvider();
		var api = new ApiFalsa().SemResposta(HttpMethod.Post, "/v2/jobs/2/error")
			.Recusar(HttpMethod.Post, "/v2/jobs/2/error", HttpStatusCode.NotFound, "NOT_FOUND", "Sem o job");

		(await AteTerminar(Cliente(api, tempo).ThrowJobErrorAsync("2", "X", "x"), tempo, TimeSpan.FromSeconds(1))).ShouldBeTrue();
	}

	[Fact(DisplayName = "Three attempts without an answer are unavailable, and a first 404 is false at once")]
	public async Task Job_Command_Gives_Up()
	{
		var tempo = new FakeTimeProvider();
		var api = new ApiFalsa().SemResposta(HttpMethod.Post, "/v2/jobs/3/failure");
		var cliente = Cliente(api, tempo);

		await Should.ThrowAsync<CamundaUnavailableException>(() => AteTerminar(cliente.FailJobAsync("3", "x"), tempo, TimeSpan.FromSeconds(1)));
		(await cliente.FailJobAsync("4", "x")).ShouldBeFalse();

		api.A("/v2/jobs/3/failure").Count.ShouldBe(3);
		api.A("/v2/jobs/4/failure").Count.ShouldBe(1);
	}

	[Fact(DisplayName = "A refused job command is not tried again")]
	public async Task Job_Command_Refused_Not_Retried()
	{
		var api = new ApiFalsa().Recusar(HttpMethod.Post, "/v2/jobs/5/completion", HttpStatusCode.Conflict, "INVALID_STATE", "Já completado");

		await Should.ThrowAsync<CamundaApiException>(() => Cliente(api).CompleteJobAsync("5", new JsonObject()));

		api.A("/v2/jobs/5/completion").Count.ShouldBe(1);
	}

	// ---------- 6. O registro de cada chamada ----------

	[Fact(DisplayName = "Each call is told to its caller: the method, the path, the status and the duration")]
	public async Task Calls_Observed()
	{
		var tempo = new FakeTimeProvider();
		var api = new ApiFalsa().Responder(HttpMethod.Get, "/v2/topology", Topologia).Responder(HttpMethod.Post, Token, TokenDe("t", 300));
		var chamadas = new List<CamundaCall>();

		await Cliente(api, tempo, OAuth()).GetTopologyAsync(observe: chamadas.Add);

		chamadas.Count.ShouldBe(2);
		(chamadas[0].Kind, chamadas[0].Method, chamadas[0].Path, chamadas[0].Status).ShouldBe((CamundaCallKind.Token, "POST", Token, (int?)200));
		chamadas[0].Endpoint.ShouldBe("http://idp.falso" + Token);
		(chamadas[1].Kind, chamadas[1].Method, chamadas[1].Path, chamadas[1].Status).ShouldBe((CamundaCallKind.Api, "GET", "/v2/topology", (int?)200));
		chamadas.ShouldAllBe(c => c.Duration >= TimeSpan.Zero);
	}

	[Fact(DisplayName = "A call without an answer is told with no status")]
	public async Task Call_Without_Answer_Observed()
	{
		var api = new ApiFalsa().SemResposta(HttpMethod.Get, "/v2/topology");
		var chamadas = new List<CamundaCall>();

		await Should.ThrowAsync<CamundaUnavailableException>(() => Cliente(api).GetTopologyAsync(observe: chamadas.Add));

		chamadas.ShouldHaveSingleItem().Status.ShouldBeNull();
	}

	[Fact(DisplayName = "Concurrent callers each hear only their own calls, and no call carries the token or the secret")]
	public async Task Calls_Observed_Per_Caller()
	{
		var api = new ApiFalsa().Responder(HttpMethod.Get, "/v2/topology", Topologia)
			.Responder(HttpMethod.Get, "/v2/process-instances/1", "{}", HttpStatusCode.NotFound)
			.Responder(HttpMethod.Post, Token, TokenDe("token-secreto", 300));
		var cliente = Cliente(api, configuracao: OAuth());
		var deUm = new ConcurrentQueue<CamundaCall>();
		var deOutro = new ConcurrentQueue<CamundaCall>();

		await Task.WhenAll(Enumerable.Range(0, 10).Select(i => i % 2 == 0
			? (Task)cliente.GetTopologyAsync(observe: deUm.Enqueue)
			: cliente.GetProcessInstanceAsync("1", observe: deOutro.Enqueue)));

		deUm.Where(c => c.Kind == CamundaCallKind.Api).ShouldAllBe(c => c.Path == "/v2/topology");
		deUm.Count(c => c.Kind == CamundaCallKind.Api).ShouldBe(5);
		deOutro.Where(c => c.Kind == CamundaCallKind.Api).ShouldAllBe(c => c.Path == "/v2/process-instances/1");
		deOutro.Count(c => c.Kind == CamundaCallKind.Api).ShouldBe(5);
		deUm.Concat(deOutro).ShouldAllBe(c => !c.ToString().Contains("token-secreto") && !c.ToString().Contains("s3cr3t"));
	}

	// ---------- Apoio ----------

	private static CamundaApiClient Cliente(ApiFalsa api, TimeProvider? tempo = null, CamundaAuthenticationSettings? configuracao = null) =>
		new(new CamundaSettings
		{
			RestAddress = "http://camunda.falso:8080",
			Authentication = configuracao ?? new CamundaAuthenticationSettings { Type = "None" }
		}, api, tempo ?? TimeProvider.System, _ => null);

	private static CamundaAuthenticationSettings OAuth() => new()
	{
		Type = "OAuth", ClientId = "qa", ClientSecret = "s3cr3t", OAuthUrl = "http://idp.falso" + Token + "?ignorado=1",
		Audience = "orchestration-api", Scope = "openid"
	};

	private static string TokenDe(string token, int segundos) =>
		$$"""{ "access_token": "{{token}}", "token_type": "Bearer", "expires_in": {{segundos}} }""";

	private static string Elementos(string estado) => $$"""
		{ "items": [ { "startDate": "2026-11-05T10:00:00.000Z", "elementId": "Fim", "elementName": "Fim", "type": "END_EVENT", "state": "{{estado}}",
		  "hasIncident": false, "tenantId": "<default>", "elementInstanceKey": "2251799813690009", "processInstanceKey": "2251799813690001",
		  "processDefinitionKey": "2251799813685249" } ], "page": { "totalItems": 1, "hasMoreTotalItems": false } }
		""";

	// Avança o relógio falso, em passos, até a tarefa terminar (com limite de 10 s de verdade).
	private static async Task<T> AteTerminar<T>(Task<T> tarefa, FakeTimeProvider tempo, TimeSpan? passo = null)
	{
		var limite = DateTime.UtcNow.AddSeconds(10);
		while (!tarefa.IsCompleted && DateTime.UtcNow < limite)
		{
			await Task.Delay(20);
			tempo.Advance(passo ?? TimeSpan.FromMilliseconds(100));
		}

		return await tarefa;
	}
}
