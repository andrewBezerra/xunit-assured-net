using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

using XUnitAssured.Camunda;
using XUnitAssured.Camunda.Gateway;

namespace XUnitAssured.Tests.CamundaTests;

/// <summary>
/// O adaptador sobre o SDK oficial, sem cluster: um handler HTTP falso, embaixo do SDK, responde como
/// o Camunda 8.10 e guarda o que o SDK mandou. Trava as rotas e os corpos da API REST v2 que o pacote
/// usa, a leitura das respostas nos tipos do pacote e a tradução das recusas.
/// </summary>
[Trait("Category", "Camunda")]
[Trait("Component", "Gateway")]
public class SdkCamundaGatewayTests
{
	private const string Pagina = "\"page\":{\"totalItems\":1,\"startCursor\":null,\"endCursor\":null,\"hasMoreTotalItems\":false}";

	// ---------- Implantação e definições ----------

	[Fact(DisplayName = "Deploy sends the files as multipart to /v2/deployments and returns the deployed processes")]
	public async Task Deploy_Sends_Multipart_And_Maps_Processes()
	{
		var http = new CamundaHttpFalso();
		http.Responder(HttpMethod.Post, "/v2/deployments", """
			{ "deploymentKey": "2251799813685200", "tenantId": "<default>", "deployments": [
			  { "processDefinition": { "processDefinitionId": "pedido", "processDefinitionVersion": 3, "resourceName": "pedido.bpmn",
			    "tenantId": "<default>", "processDefinitionKey": "2251799813685249" } },
			  { "decisionRequirements": { "decisionRequirementsId": "regras", "decisionRequirementsName": "Regras", "version": 1,
			    "resourceName": "regras.dmn", "tenantId": "<default>", "decisionRequirementsKey": "2251799813685300" } }
			] }
			""");
		using var gateway = Gateway(http);

		var processos = await gateway.DeployAsync(new[]
		{
			new RecursoBpmn("pedido.bpmn", Encoding.UTF8.GetBytes("<definitions/>")),
			new RecursoBpmn("regras.dmn", Encoding.UTF8.GetBytes("<dmn/>"))
		}, default);

		var processo = processos.ShouldHaveSingleItem();
		processo.ProcessId.ShouldBe("pedido");
		processo.DefinitionKey.ShouldBe("2251799813685249");
		processo.Version.ShouldBe(3);
		processo.ResourceName.ShouldBe("pedido.bpmn");

		var pedido = http.Pedidos.ShouldHaveSingleItem();
		pedido.Metodo.ShouldBe("POST");
		pedido.Caminho.ShouldBe("/v2/deployments");
		pedido.TipoDoConteudo.ShouldBe("multipart/form-data");
		var corpo = pedido.Corpo.ShouldNotBeNull();
		corpo.ShouldContain("name=resources");
		corpo.ShouldContain("filename=pedido.bpmn");
		corpo.ShouldContain("filename=regras.dmn");
		corpo.ShouldContain("<definitions/>");
	}

	[Fact(DisplayName = "Searching the latest version filters by process id and isLatestVersion, and maps each definition")]
	public async Task Search_Definitions_Latest()
	{
		var http = new CamundaHttpFalso();
		http.Responder(HttpMethod.Post, "/v2/process-definitions/search", $$"""
			{ "items": [
			  { "name": "Pedido", "resourceName": "pedido.bpmn", "version": 4, "versionTag": "v4", "processDefinitionId": "pedido",
			    "tenantId": "<default>", "processDefinitionKey": "2251799813685251", "hasStartForm": false },
			  { "name": "Pedido", "resourceName": "pedido.bpmn", "version": 2, "versionTag": "  ", "processDefinitionId": "pedido",
			    "tenantId": "<default>", "processDefinitionKey": "2251799813685250", "hasStartForm": false }
			], {{Pagina}} }
			""");
		using var gateway = Gateway(http);

		var definicoes = await gateway.SearchProcessDefinitionsAsync("pedido", null, null, onlyLatest: true, null, default);

		definicoes.ShouldBe(new[]
		{
			new DefinicaoVista("pedido", "2251799813685251", 4, "v4"),
			new DefinicaoVista("pedido", "2251799813685250", 2, null)
		});

		var filtro = http.Corpo(0)["filter"]!;
		filtro["processDefinitionId"]!["$eq"]!.GetValue<string>().ShouldBe("pedido");
		filtro["isLatestVersion"]!.GetValue<bool>().ShouldBeTrue();
		filtro["version"].ShouldBeNull();
		filtro["tenantId"].ShouldBeNull();
	}

	[Fact(DisplayName = "Searching a pinned version sends version, tag and tenant, and does not ask for the latest only")]
	public async Task Search_Definitions_Pinned()
	{
		var http = new CamundaHttpFalso();
		http.Responder(HttpMethod.Post, "/v2/process-definitions/search", $$"""{ "items": [], {{Pagina}} }""");
		using var gateway = Gateway(http);

		var definicoes = await gateway.SearchProcessDefinitionsAsync("pedido", 2, "v2", onlyLatest: true, "loja", default);

		definicoes.ShouldBeEmpty();
		var filtro = http.Corpo(0)["filter"]!;
		filtro["version"]!.GetValue<int>().ShouldBe(2);
		filtro["versionTag"]!.GetValue<string>().ShouldBe("v2");
		filtro["tenantId"]!.GetValue<string>().ShouldBe("loja");
		filtro["isLatestVersion"].ShouldBeNull();
	}

	[Fact(DisplayName = "The process XML comes from GET /v2/process-definitions/{key}/xml, intact and without a byte order mark")]
	public async Task Process_Xml()
	{
		const string xml = "<?xml version=\"1.0\"?><bpmn:definitions xmlns:bpmn=\"x\"><bpmn:process id=\"pedido\"/></bpmn:definitions>";
		var http = new CamundaHttpFalso();
		http.Responder(HttpMethod.Get, "/v2/process-definitions/2251799813685249/xml", "﻿" + xml, tipo: "text/xml");
		using var gateway = Gateway(http);

		var lido = await gateway.GetProcessDefinitionXmlAsync("2251799813685249", default);

		lido.ShouldBe(xml);
		http.Pedidos.ShouldHaveSingleItem().Metodo.ShouldBe("GET");
	}

	// ---------- Instâncias ----------

	[Fact(DisplayName = "Creating an instance posts the definition key, variables and tenant, and returns the instance key")]
	public async Task Create_Instance()
	{
		var http = new CamundaHttpFalso();
		http.Responder(HttpMethod.Post, "/v2/process-instances", """
			{ "processDefinitionId": "pedido", "processDefinitionVersion": 1, "tenantId": "loja", "variables": {},
			  "processDefinitionKey": "2251799813685249", "processInstanceKey": "2251799813690001", "tags": [] }
			""");
		using var gateway = Gateway(http);

		var chave = await gateway.CreateInstanceAsync("2251799813685249", new { pedidoId = 7 }, "loja", default);

		chave.ShouldBe("2251799813690001");
		var corpo = http.Corpo(0);
		corpo["processDefinitionKey"]!.GetValue<string>().ShouldBe("2251799813685249");
		corpo["variables"]!["pedidoId"]!.GetValue<int>().ShouldBe(7);
		corpo["tenantId"]!.GetValue<string>().ShouldBe("loja");
	}

	[Fact(DisplayName = "Getting an instance uses the search, not the GET, and maps state, parent and version")]
	public async Task Get_Instance_Maps_The_Search()
	{
		var http = new CamundaHttpFalso();
		http.Responder(HttpMethod.Post, "/v2/process-instances/search", $$"""
			{ "items": [ {{Instancia("2251799813690002", "envio", "ACTIVE", true, pai: "2251799813690001", elementoPai: "2251799813690010", tag: "v7")}} ], {{Pagina}} }
			""");
		using var gateway = Gateway(http);

		var instancia = await gateway.GetInstanceAsync("2251799813690002", default);

		instancia.ShouldBe(new InstanciaVista(
			"2251799813690002", "envio", "ACTIVE", true, "2251799813690001", "2251799813685249", "2251799813690010", 7, "v7"));
		http.Pedidos.ShouldHaveSingleItem().Caminho.ShouldBe("/v2/process-instances/search");
		http.Corpo(0)["filter"]!["processInstanceKey"]!.GetValue<string>().ShouldBe("2251799813690002");
	}

	[Fact(DisplayName = "An instance the search does not show yet is null, not an error")]
	public async Task Get_Instance_Not_There_Yet()
	{
		var http = new CamundaHttpFalso();
		http.Responder(HttpMethod.Post, "/v2/process-instances/search", $$"""{ "items": [], {{Pagina}} }""");
		using var gateway = Gateway(http);

		(await gateway.GetInstanceAsync("2251799813690002", default)).ShouldBeNull();
	}

	[Fact(DisplayName = "The children search filters by the parents' keys and maps a root instance with no parent")]
	public async Task Search_Children()
	{
		var http = new CamundaHttpFalso();
		http.Responder(HttpMethod.Post, "/v2/process-instances/search", $$"""
			{ "items": [ {{Instancia("2251799813690003", "etiqueta", "COMPLETED", false)}} ], {{Pagina}} }
			""");
		using var gateway = Gateway(http);

		var filhas = await gateway.SearchChildInstancesAsync(new[] { "2251799813690001", "2251799813690002" }, default);

		var filha = filhas.ShouldHaveSingleItem();
		filha.State.ShouldBe("COMPLETED");
		filha.ParentProcessInstanceKey.ShouldBeNull();
		filha.VersionTag.ShouldBeNull();
		http.Corpo(0)["filter"]!["parentProcessInstanceKey"]!["$in"]!.AsArray().Select(n => n!.GetValue<string>())
			.ShouldBe(new[] { "2251799813690001", "2251799813690002" });
	}

	[Fact(DisplayName = "Finding an instance by variable filters by process and variable, newest first")]
	public async Task Search_By_Variable()
	{
		var http = new CamundaHttpFalso();
		http.Responder(HttpMethod.Post, "/v2/process-instances/search", $$"""
			{ "items": [ {{Instancia("2251799813690005", "pedido", "ACTIVE", false)}}, {{Instancia("2251799813690004", "pedido", "TERMINATED", false)}} ], {{Pagina}} }
			""");
		using var gateway = Gateway(http);

		var achadas = await gateway.SearchInstancesByVariableAsync("pedido", "pedidoId", "\"A-7\"", default);

		achadas.Select(i => i.ProcessInstanceKey).ShouldBe(new[] { "2251799813690005", "2251799813690004" });
		var corpo = http.Corpo(0);
		corpo["filter"]!["processDefinitionId"]!["$eq"]!.GetValue<string>().ShouldBe("pedido");
		var variavel = corpo["filter"]!["variables"]!.AsArray().ShouldHaveSingleItem()!;
		variavel["name"]!.GetValue<string>().ShouldBe("pedidoId");
		variavel["value"]!["$eq"]!.GetValue<string>().ShouldBe("\"A-7\"");
		var ordem = corpo["sort"]!.AsArray().ShouldHaveSingleItem()!;
		ordem["field"]!.GetValue<string>().ShouldBe("startDate");
		ordem["order"]!.GetValue<string>().ShouldBe("desc", StringCompareShould.IgnoreCase);
	}

	[Fact(DisplayName = "Cancelling posts to /v2/process-instances/{key}/cancellation")]
	public async Task Cancel_Instance()
	{
		var http = new CamundaHttpFalso();
		http.Responder(HttpMethod.Post, "/v2/process-instances/2251799813690001/cancellation", null, HttpStatusCode.NoContent);
		using var gateway = Gateway(http);

		await gateway.CancelInstanceAsync("2251799813690001", default);

		http.Pedidos.ShouldHaveSingleItem().Metodo.ShouldBe("POST");
	}

	[Fact(DisplayName = "Modifying an instance sends the elements to terminate and to activate, with scope and variables")]
	public async Task Modify_Instance()
	{
		var http = new CamundaHttpFalso();
		http.Responder(HttpMethod.Post, "/v2/process-instances/2251799813690001/modification", null, HttpStatusCode.NoContent);
		using var gateway = Gateway(http);

		await gateway.ModifyInstanceAsync(
			"2251799813690001", new[] { "2251799813690020" }, new[] { "fim" }, "2251799813690030", new { aprovado = true }, default);

		var corpo = http.Corpo(0);
		corpo["terminateInstructions"]!.AsArray().ShouldHaveSingleItem()!["elementInstanceKey"]!.GetValue<string>().ShouldBe("2251799813690020");
		var ativar = corpo["activateInstructions"]!.AsArray().ShouldHaveSingleItem()!;
		ativar["elementId"]!.GetValue<string>().ShouldBe("fim");
		ativar["ancestorElementInstanceKey"]!.GetValue<string>().ShouldBe("2251799813690030");
		ativar["variableInstructions"]!.AsArray().ShouldHaveSingleItem()!["variables"]!["aprovado"]!.GetValue<bool>().ShouldBeTrue();
	}

	[Fact(DisplayName = "A modification with no scope and no variables leaves both out of the activation")]
	public async Task Modify_Instance_Without_Scope()
	{
		var http = new CamundaHttpFalso();
		http.Responder(HttpMethod.Post, "/v2/process-instances/2251799813690001/modification", null, HttpStatusCode.NoContent);
		using var gateway = Gateway(http);

		await gateway.ModifyInstanceAsync("2251799813690001", Array.Empty<string>(), new[] { "fim" }, null, null, default);

		var ativar = http.Corpo(0)["activateInstructions"]!.AsArray().ShouldHaveSingleItem()!;
		ativar["ancestorElementInstanceKey"].ShouldBeNull();
		ativar["variableInstructions"].ShouldBeNull();
	}

	[Fact(DisplayName = "Setting variables puts them on the instance's scope, /v2/element-instances/{key}/variables")]
	public async Task Set_Variables()
	{
		var http = new CamundaHttpFalso();
		http.Responder(HttpMethod.Put, "/v2/element-instances/2251799813690001/variables", null, HttpStatusCode.NoContent);
		using var gateway = Gateway(http);

		await gateway.SetVariablesAsync("2251799813690001", new { total = 10.5 }, default);

		http.Pedidos.ShouldHaveSingleItem().Metodo.ShouldBe("PUT");
		http.Corpo(0)["variables"]!["total"]!.GetValue<double>().ShouldBe(10.5);
	}

	// ---------- Elementos, variáveis e incidents ----------

	[Fact(DisplayName = "The element search filters by the instance and maps id, name, state, key and end date")]
	public async Task Search_Elements()
	{
		var http = new CamundaHttpFalso();
		http.Responder(HttpMethod.Post, "/v2/element-instances/search", $$"""
			{ "items": [
			  { "elementInstanceKey": "2251799813690011", "elementId": "cobrar", "elementName": "Cobrar", "type": "SERVICE_TASK",
			    "state": "COMPLETED", "startDate": "2026-10-16T10:00:00.000Z", "endDate": "2026-10-16T10:00:01.000Z", "hasIncident": false,
			    "processInstanceKey": "2251799813690001", "processDefinitionKey": "2251799813685249", "processDefinitionId": "pedido",
			    "tenantId": "<default>", "rootProcessInstanceKey": "2251799813690001" },
			  { "elementInstanceKey": "2251799813690012", "elementId": "chamar_envio", "type": "CALL_ACTIVITY",
			    "state": "ACTIVE", "startDate": "2026-10-16T10:00:01.000Z", "hasIncident": false,
			    "processInstanceKey": "2251799813690001", "processDefinitionKey": "2251799813685249", "processDefinitionId": "pedido",
			    "tenantId": "<default>", "rootProcessInstanceKey": "2251799813690001" }
			], {{Pagina}} }
			""");
		using var gateway = Gateway(http);

		var elementos = await gateway.SearchElementsAsync("2251799813690001", default);

		elementos.Count.ShouldBe(2);
		elementos[0].ElementId.ShouldBe("cobrar");
		elementos[0].ElementName.ShouldBe("Cobrar");
		elementos[0].State.ShouldBe("COMPLETED");
		elementos[0].ElementInstanceKey.ShouldBe("2251799813690011");
		elementos[0].EndDate.ShouldBe(new DateTimeOffset(2026, 10, 16, 10, 0, 1, TimeSpan.Zero));
		elementos[1].ElementName.ShouldBe("");
		elementos[1].State.ShouldBe("ACTIVE");
		elementos[1].EndDate.ShouldBeNull();
		http.Corpo(0)["filter"]!["processInstanceKey"]!.GetValue<string>().ShouldBe("2251799813690001");
	}

	// O nome do Camunda (SERVICE_TASK, JOB_NO_RETRIES), e não o do membro do enum do SDK. Até a
	// 6.30.6 o pacote mostrava "SERVICETASK" e "JOBNORETRIES" nas mensagens de incidente.
	[Fact(DisplayName = "Element types and incident error types keep Camunda's names, with underscores")]
	public async Task Enum_Names_Are_Camundas()
	{
		var http = new CamundaHttpFalso();
		http.Responder(HttpMethod.Post, "/v2/element-instances/search", $$"""
			{ "items": [
			  { "elementInstanceKey": "2251799813690011", "elementId": "cobrar", "elementName": "Cobrar", "type": "SERVICE_TASK",
			    "state": "ACTIVE", "startDate": "2026-10-16T10:00:00.000Z", "hasIncident": true,
			    "processInstanceKey": "2251799813690001", "processDefinitionKey": "2251799813685249", "processDefinitionId": "pedido",
			    "tenantId": "<default>", "rootProcessInstanceKey": "2251799813690001" }
			], {{Pagina}} }
			""");
		http.Responder(HttpMethod.Post, "/v2/process-instances/2251799813690001/incidents/search", $$"""
			{ "items": [
			  { "incidentKey": "2251799813690050", "processDefinitionId": "pedido", "processDefinitionKey": "2251799813685249",
			    "processInstanceKey": "2251799813690001", "errorType": "JOB_NO_RETRIES", "errorMessage": "sem saldo",
			    "elementId": "cobrar", "elementInstanceKey": "2251799813690011", "creationTime": "2026-10-16T10:00:00.000Z",
			    "state": "ACTIVE", "jobKey": "2251799813690060", "tenantId": "<default>", "rootProcessInstanceKey": "2251799813690001" }
			], {{Pagina}} }
			""");
		using var gateway = Gateway(http);

		(await gateway.SearchElementsAsync("2251799813690001", default)).ShouldHaveSingleItem().Type.ShouldBe("SERVICE_TASK");
		(await gateway.SearchActiveIncidentsAsync("2251799813690001", default)).ShouldHaveSingleItem().ErrorType.ShouldBe("JOB_NO_RETRIES");
	}

	[Fact(DisplayName = "The variable search asks for full values and maps name, JSON value and scope")]
	public async Task Search_Variables()
	{
		var http = new CamundaHttpFalso();
		http.Responder(HttpMethod.Post, "/v2/variables/search", $$"""
			{ "items": [
			  { "name": "pedidoId", "value": "\"A-7\"", "isTruncated": false, "variableKey": "2251799813690040",
			    "scopeKey": "2251799813690001", "processInstanceKey": "2251799813690001", "tenantId": "<default>",
			    "rootProcessInstanceKey": "2251799813690001" }
			], {{Pagina}} }
			""");
		using var gateway = Gateway(http);

		var variaveis = await gateway.SearchVariablesAsync("2251799813690001", "pedidoId", default);

		variaveis.ShouldBe(new[] { new VariavelVista("pedidoId", "\"A-7\"", "2251799813690001") });
		var pedido = http.Pedidos.ShouldHaveSingleItem();
		pedido.Query.ShouldContain("truncateValues=false");
		var filtro = http.Corpo(0)["filter"]!;
		filtro["processInstanceKey"]!.GetValue<string>().ShouldBe("2251799813690001");
		filtro["name"]!.GetValue<string>().ShouldBe("pedidoId");
	}

	[Fact(DisplayName = "The variable search with no name does not filter by name")]
	public async Task Search_All_Variables()
	{
		var http = new CamundaHttpFalso();
		http.Responder(HttpMethod.Post, "/v2/variables/search", $$"""{ "items": [], {{Pagina}} }""");
		using var gateway = Gateway(http);

		(await gateway.SearchVariablesAsync("2251799813690001", null, default)).ShouldBeEmpty();

		http.Corpo(0)["filter"]!["name"].ShouldBeNull();
	}

	[Fact(DisplayName = "Active incidents come from /v2/process-instances/{key}/incidents/search, filtered by the ACTIVE state")]
	public async Task Search_Incidents()
	{
		var http = new CamundaHttpFalso();
		http.Responder(HttpMethod.Post, "/v2/process-instances/2251799813690001/incidents/search", $$"""
			{ "items": [
			  { "incidentKey": "2251799813690050", "processDefinitionId": "pedido", "processDefinitionKey": "2251799813685249",
			    "processInstanceKey": "2251799813690001", "errorType": "JOB_NO_RETRIES", "errorMessage": "sem saldo",
			    "elementId": "cobrar", "elementInstanceKey": "2251799813690011", "creationTime": "2026-10-16T10:00:00.000Z",
			    "state": "ACTIVE", "jobKey": "2251799813690060", "tenantId": "<default>", "rootProcessInstanceKey": "2251799813690001" },
			  { "incidentKey": "2251799813690051", "processDefinitionId": "pedido", "processDefinitionKey": "2251799813685249",
			    "processInstanceKey": "2251799813690001", "errorType": "IO_MAPPING_ERROR",
			    "elementId": "enviar", "elementInstanceKey": "2251799813690012", "creationTime": "2026-10-16T10:00:00.000Z",
			    "state": "ACTIVE", "tenantId": "<default>", "rootProcessInstanceKey": "2251799813690001" }
			], {{Pagina}} }
			""");
		using var gateway = Gateway(http);

		var incidentes = await gateway.SearchActiveIncidentsAsync("2251799813690001", default);

		incidentes.Select(i => (i.ElementId, i.ErrorMessage)).ShouldBe(new[] { ("cobrar", "sem saldo"), ("enviar", "") });
		http.Corpo(0)["filter"]!["state"]!["$eq"]!.GetValue<string>().ShouldBe("ACTIVE");
	}

	// ---------- Jobs ----------

	[Fact(DisplayName = "The job search filters by the instances, the CREATED state and the mocked types")]
	public async Task Search_Jobs()
	{
		var http = new CamundaHttpFalso();
		http.Responder(HttpMethod.Post, "/v2/jobs/search", $$"""
			{ "items": [ {{Job("2251799813690060", "cobrar", "cobrar")}} ], {{Pagina}} }
			""");
		using var gateway = Gateway(http);

		var jobs = await gateway.SearchCreatedJobsAsync(new[] { "2251799813690001" }, new[] { "cobrar", "enviar" }, default);

		jobs.ShouldBe(new[] { new JobPendente("2251799813690060", "cobrar", "cobrar", "2251799813690001") });
		var filtro = http.Corpo(0)["filter"]!;
		filtro["processInstanceKey"]!["$in"]!.AsArray().Select(n => n!.GetValue<string>()).ShouldBe(new[] { "2251799813690001" });
		filtro["state"]!["$eq"]!.GetValue<string>().ShouldBe("CREATED");
		filtro["type"]!["$in"]!.AsArray().Select(n => n!.GetValue<string>()).ShouldBe(new[] { "cobrar", "enviar" });
	}

	[Fact(DisplayName = "The job search with no types does not filter by type")]
	public async Task Search_Jobs_Of_Any_Type()
	{
		var http = new CamundaHttpFalso();
		http.Responder(HttpMethod.Post, "/v2/jobs/search", $$"""{ "items": [], {{Pagina}} }""");
		using var gateway = Gateway(http);

		(await gateway.SearchCreatedJobsAsync(new[] { "2251799813690001" }, Array.Empty<string>(), default)).ShouldBeEmpty();

		http.Corpo(0)["filter"]!["type"].ShouldBeNull();
	}

	[Fact(DisplayName = "Completing a job posts its variables to /v2/jobs/{key}/completion")]
	public async Task Complete_Job()
	{
		var http = new CamundaHttpFalso();
		http.Responder(HttpMethod.Post, "/v2/jobs/2251799813690060/completion", null, HttpStatusCode.NoContent);
		using var gateway = Gateway(http);

		await gateway.CompleteJobAsync("2251799813690060", new { pago = true }, default);

		http.Corpo(0)["variables"]!["pago"]!.GetValue<bool>().ShouldBeTrue();
	}

	[Fact(DisplayName = "Throwing a BPMN error posts code, message and variables to /v2/jobs/{key}/error")]
	public async Task Throw_Bpmn_Error()
	{
		var http = new CamundaHttpFalso();
		http.Responder(HttpMethod.Post, "/v2/jobs/2251799813690060/error", null, HttpStatusCode.NoContent);
		using var gateway = Gateway(http);

		await gateway.ThrowBpmnErrorAsync("2251799813690060", "PAGAMENTO_RECUSADO", "sem saldo", new { motivo = "saldo" }, default);

		var corpo = http.Corpo(0);
		corpo["errorCode"]!.GetValue<string>().ShouldBe("PAGAMENTO_RECUSADO");
		corpo["errorMessage"]!.GetValue<string>().ShouldBe("sem saldo");
		corpo["variables"]!["motivo"]!.GetValue<string>().ShouldBe("saldo");
	}

	[Fact(DisplayName = "Failing a job posts zero retries and the message to /v2/jobs/{key}/failure, so Camunda opens an incident")]
	public async Task Fail_Job()
	{
		var http = new CamundaHttpFalso();
		http.Responder(HttpMethod.Post, "/v2/jobs/2251799813690060/failure", null, HttpStatusCode.NoContent);
		using var gateway = Gateway(http);

		await gateway.FailJobAsync("2251799813690060", "o banco caiu", default);

		var corpo = http.Corpo(0);
		corpo["retries"]!.GetValue<int>().ShouldBe(0);
		corpo["errorMessage"]!.GetValue<string>().ShouldBe("o banco caiu");
	}

	// ---------- User tasks e esperas ----------

	[Fact(DisplayName = "The user task search filters by the instances and maps state, assignee, groups and priority")]
	public async Task Search_User_Tasks()
	{
		var http = new CamundaHttpFalso();
		http.Responder(HttpMethod.Post, "/v2/user-tasks/search", $$"""
			{ "items": [
			  { "userTaskKey": "2251799813690070", "name": "Aprovar", "state": "CREATED", "assignee": "ana", "elementId": "aprovar",
			    "candidateGroups": [ "gerentes" ], "candidateUsers": [], "processDefinitionId": "pedido",
			    "creationDate": "2026-10-16T10:00:00.000Z", "tenantId": "<default>", "processDefinitionVersion": 1,
			    "customHeaders": {}, "priority": 70, "elementInstanceKey": "2251799813690013", "processName": "Pedido",
			    "processDefinitionKey": "2251799813685249", "processInstanceKey": "2251799813690001",
			    "rootProcessInstanceKey": "2251799813690001", "tags": [] },
			  { "userTaskKey": "2251799813690071", "state": "COMPLETED", "elementId": "revisar",
			    "candidateUsers": [], "processDefinitionId": "pedido",
			    "creationDate": "2026-10-16T10:00:00.000Z", "tenantId": "<default>", "processDefinitionVersion": 1,
			    "customHeaders": {}, "priority": 50, "elementInstanceKey": "2251799813690014",
			    "processDefinitionKey": "2251799813685249", "processInstanceKey": "2251799813690001",
			    "rootProcessInstanceKey": "2251799813690001", "tags": [] }
			], {{Pagina}} }
			""");
		using var gateway = Gateway(http);

		var tarefas = await gateway.SearchUserTasksAsync(new[] { "2251799813690001" }, default);

		tarefas.Count.ShouldBe(2);
		var aprovar = tarefas[0];
		aprovar.UserTaskKey.ShouldBe("2251799813690070");
		aprovar.ProcessInstanceKey.ShouldBe("2251799813690001");
		aprovar.ElementId.ShouldBe("aprovar");
		aprovar.Name.ShouldBe("Aprovar");
		aprovar.State.ShouldBe("CREATED");
		aprovar.Assignee.ShouldBe("ana");
		aprovar.CandidateGroups.ShouldBe(new[] { "gerentes" });
		aprovar.Priority.ShouldBe(70);
		tarefas[1].State.ShouldBe("COMPLETED");
		tarefas[1].CandidateGroups.ShouldBeEmpty();
		http.Corpo(0)["filter"]!["processInstanceKey"]!["$in"]!.AsArray().Select(n => n!.GetValue<string>())
			.ShouldBe(new[] { "2251799813690001" });
	}

	[Fact(DisplayName = "Completing a user task posts its variables to /v2/user-tasks/{key}/completion")]
	public async Task Complete_User_Task()
	{
		var http = new CamundaHttpFalso();
		http.Responder(HttpMethod.Post, "/v2/user-tasks/2251799813690070/completion", null, HttpStatusCode.NoContent);
		using var gateway = Gateway(http);

		await gateway.CompleteUserTaskAsync("2251799813690070", new { aprovado = true }, default);

		http.Corpo(0)["variables"]!["aprovado"]!.GetValue<bool>().ShouldBeTrue();
	}

	[Fact(DisplayName = "The wait-state search filters by the root instance and maps each kind of wait with its details")]
	public async Task Search_Wait_States()
	{
		var http = new CamundaHttpFalso();
		http.Responder(HttpMethod.Post, "/v2/element-instances/wait-states/search", $$"""
			{ "items": [
			  {{Espera("2251799813690013", "aprovar", "USER_TASK", "USER_TASK", "\"taskKey\": \"2251799813690070\"")}},
			  {{Espera("2251799813690015", "aguardar_pagamento", "MESSAGE", "RECEIVE_TASK", "\"messageName\": \"pago\", \"correlationKey\": \"A-7\"")}},
			  {{Espera("2251799813690016", "aguardar_liberacao", "SIGNAL", "INTERMEDIATE_CATCH_EVENT", "\"signalName\": \"liberado\"")}},
			  {{Espera("2251799813690017", "esperar", "TIMER", "INTERMEDIATE_CATCH_EVENT", "\"dueDate\": 1792148400000")}},
			  {{Espera("2251799813690018", "cobrar", "JOB", "SERVICE_TASK", "\"jobKey\": \"2251799813690060\", \"jobType\": \"cobrar\", \"jobKind\": \"BPMN_ELEMENT\", \"retries\": 3")}}
			], {{Pagina}} }
			""");
		using var gateway = Gateway(http);

		var esperas = await gateway.SearchWaitStatesAsync("2251799813690001", default);

		esperas.ShouldBe(new[]
		{
			new EsperaVista("2251799813690001", "2251799813690013", "aprovar", "USER_TASK", UserTaskKey: "2251799813690070"),
			new EsperaVista("2251799813690001", "2251799813690015", "aguardar_pagamento", "MESSAGE", MessageName: "pago", CorrelationKey: "A-7"),
			new EsperaVista("2251799813690001", "2251799813690016", "aguardar_liberacao", "SIGNAL", SignalName: "liberado"),
			new EsperaVista("2251799813690001", "2251799813690017", "esperar", "TIMER"),
			new EsperaVista("2251799813690001", "2251799813690018", "cobrar", "JOB")
		});
		http.Corpo(0)["filter"]!["rootProcessInstanceKey"]!["$in"]!.AsArray().Select(n => n!.GetValue<string>())
			.ShouldBe(new[] { "2251799813690001" });
	}

	// ---------- Mensagens ----------

	[Fact(DisplayName = "The subscription search filters by names, open state, instances and key, and skips start-event subscriptions")]
	public async Task Search_Subscriptions()
	{
		var http = new CamundaHttpFalso();
		http.Responder(HttpMethod.Post, "/v2/message-subscriptions/search", $$"""
			{ "items": [
			  { "messageSubscriptionKey": "2251799813690080", "processDefinitionId": "pedido", "processDefinitionKey": "2251799813685249",
			    "processInstanceKey": "2251799813690001", "rootProcessInstanceKey": "2251799813690001", "elementId": "aguardar_pagamento",
			    "elementInstanceKey": "2251799813690015", "messageSubscriptionState": "CREATED", "lastUpdatedDate": "2026-10-16T10:00:00.000Z",
			    "messageName": "pago", "correlationKey": "A-7", "tenantId": "<default>" },
			  { "messageSubscriptionKey": "2251799813690081", "processDefinitionId": "pedido", "processDefinitionKey": "2251799813685249",
			    "elementId": "inicio_por_mensagem", "messageSubscriptionState": "CREATED", "lastUpdatedDate": "2026-10-16T10:00:00.000Z",
			    "messageName": "pago", "tenantId": "<default>" }
			], {{Pagina}} }
			""");
		using var gateway = Gateway(http);

		var assinaturas = await gateway.SearchOpenMessageSubscriptionsAsync(new[] { "2251799813690001" }, new[] { "pago" }, "A-7", default);

		assinaturas.ShouldBe(new[] { new AssinaturaVista("2251799813690001", "aguardar_pagamento", "2251799813690015", "pago", "A-7") });
		var filtro = http.Corpo(0)["filter"]!;
		filtro["messageName"]!["$in"]!.AsArray().Select(n => n!.GetValue<string>()).ShouldBe(new[] { "pago" });
		filtro["messageSubscriptionState"]!["$eq"]!.GetValue<string>().ShouldBe("CREATED");
		filtro["processInstanceKey"]!["$in"]!.AsArray().Select(n => n!.GetValue<string>()).ShouldBe(new[] { "2251799813690001" });
		filtro["correlationKey"]!["$eq"]!.GetValue<string>().ShouldBe("A-7");
	}

	[Fact(DisplayName = "The cluster-wide subscription search leaves instances and key out of the filter")]
	public async Task Search_Subscriptions_Cluster_Wide()
	{
		var http = new CamundaHttpFalso();
		http.Responder(HttpMethod.Post, "/v2/message-subscriptions/search", $$"""{ "items": [], {{Pagina}} }""");
		using var gateway = Gateway(http);

		(await gateway.SearchOpenMessageSubscriptionsAsync(null, new[] { "pago" }, null, default)).ShouldBeEmpty();

		var filtro = http.Corpo(0)["filter"]!;
		filtro["processInstanceKey"].ShouldBeNull();
		filtro["correlationKey"].ShouldBeNull();
	}

	[Fact(DisplayName = "Correlating posts name, key, variables and tenant to /v2/messages/correlation and returns the instance that got it")]
	public async Task Correlate_Message()
	{
		var http = new CamundaHttpFalso();
		http.Responder(HttpMethod.Post, "/v2/messages/correlation", """
			{ "tenantId": "loja", "messageKey": "2251799813690090", "processInstanceKey": "2251799813690001" }
			""");
		using var gateway = Gateway(http);

		var instancia = await gateway.CorrelateMessageAsync("pago", "A-7", new { valor = 10 }, "loja", default);

		instancia.ShouldBe("2251799813690001");
		var corpo = http.Corpo(0);
		corpo["name"]!.GetValue<string>().ShouldBe("pago");
		corpo["correlationKey"]!.GetValue<string>().ShouldBe("A-7");
		corpo["variables"]!["valor"]!.GetValue<int>().ShouldBe(10);
		corpo["tenantId"]!.GetValue<string>().ShouldBe("loja");
	}

	// ---------- Recusas ----------

	[Fact(DisplayName = "A NOT_FOUND from the cluster becomes CamundaRecusouException with that code and Camunda's detail")]
	public async Task Not_Found_Is_Translated()
	{
		var http = new CamundaHttpFalso();
		http.Responder(HttpMethod.Post, "/v2/jobs/2251799813690060/completion", """
			{ "type": "about:blank", "title": "NOT_FOUND", "status": 404,
			  "detail": "Command 'COMPLETE' rejected with code 'NOT_FOUND': Expected to complete job with key '2251799813690060', but no such job was found",
			  "instance": "/v2/jobs/2251799813690060/completion" }
			""", HttpStatusCode.NotFound, "application/problem+json");
		using var gateway = Gateway(http);

		var recusa = await Should.ThrowAsync<CamundaRecusouException>(() => gateway.CompleteJobAsync("2251799813690060", null, default));

		recusa.Codigo.ShouldBe("NOT_FOUND");
		recusa.Message.ShouldContain("no such job was found");
		recusa.InnerException.ShouldNotBeNull();
	}

	[Fact(DisplayName = "An INVALID_STATE on a search with a result also becomes CamundaRecusouException with the code")]
	public async Task Invalid_State_On_A_Search_Is_Translated()
	{
		var http = new CamundaHttpFalso();
		http.Responder(HttpMethod.Post, "/v2/messages/correlation", """
			{ "type": "about:blank", "title": "INVALID_STATE", "status": 409, "detail": "already correlated",
			  "instance": "/v2/messages/correlation" }
			""", HttpStatusCode.Conflict, "application/problem+json");
		using var gateway = Gateway(http);

		var recusa = await Should.ThrowAsync<CamundaRecusouException>(() => gateway.CorrelateMessageAsync("pago", "A-7", null, null, default));

		recusa.Codigo.ShouldBe("INVALID_STATE");
		recusa.Message.ShouldBe("already correlated");
	}

	[Fact(DisplayName = "A refusal with no problem body still becomes CamundaRecusouException, coded from the SDK's message")]
	public async Task Refusal_Without_Problem_Body()
	{
		var http = new CamundaHttpFalso();
		http.Responder(HttpMethod.Post, "/v2/process-instances/2251799813690001/cancellation", "", HttpStatusCode.BadRequest, "text/plain");
		using var gateway = Gateway(http);

		var recusa = await Should.ThrowAsync<CamundaRecusouException>(() => gateway.CancelInstanceAsync("2251799813690001", default));

		recusa.Codigo.ShouldNotBeNullOrWhiteSpace();
		recusa.InnerException.ShouldNotBeNull();
	}

	// ---------- Configuração ----------

	[Fact(DisplayName = "The settings become the SDK's CAMUNDA_* keys, with only what was filled in")]
	public void Settings_Become_Sdk_Keys()
	{
		var config = SdkCamundaGateway.Configuracao(new CamundaSettings
		{
			RestAddress = "http://camunda:8080",
			TenantId = "loja",
			Authentication = new CamundaAuthenticationSettings { Type = "basic", Username = "demo", Password = "segredo" }
		});

		config["CAMUNDA_REST_ADDRESS"].ShouldBe("http://camunda:8080");
		config["CAMUNDA_DEFAULT_TENANT_ID"].ShouldBe("loja");
		config["CAMUNDA_AUTH_STRATEGY"].ShouldBe("BASIC");
		config["CAMUNDA_BASIC_AUTH_USERNAME"].ShouldBe("demo");
		config["CAMUNDA_BASIC_AUTH_PASSWORD"].ShouldBe("segredo");
		config.ContainsKey("CAMUNDA_CLIENT_ID").ShouldBeFalse();
	}

	[Fact(DisplayName = "An authentication type the SDK does not know is refused, naming the ones it does")]
	public void Unknown_Authentication_Type_Is_Refused()
	{
		var erro = Should.Throw<InvalidOperationException>(() => SdkCamundaGateway.Configuracao(new CamundaSettings
		{
			Authentication = new CamundaAuthenticationSettings { Type = "kerberos" }
		}));

		erro.Message.ShouldContain("'kerberos' is not supported");
	}

	[Fact(DisplayName = "The gateway needs settings and an HTTP handler")]
	public void Requires_Settings_And_Handler()
	{
		Should.Throw<ArgumentNullException>(() => new SdkCamundaGateway(null!, new CamundaHttpFalso()));
		Should.Throw<ArgumentNullException>(() => new SdkCamundaGateway(new CamundaSettings(), null!));
		Should.Throw<ArgumentNullException>(() => new SdkCamundaGateway(null!));
	}

	[Fact(DisplayName = "The gateway over the real network is built without contacting the cluster")]
	public void Builds_Without_Contacting_The_Cluster()
	{
		using var gateway = new SdkCamundaGateway(new CamundaSettings
		{
			RestAddress = "http://camunda.inexistente.invalid:8080",
			Authentication = new CamundaAuthenticationSettings { Type = "None" }
		});

		gateway.ShouldNotBeNull();
	}

	[Fact(DisplayName = "A wait with no details is UNKNOWN, and a kind the package does not name keeps the SDK's name")]
	public async Task Wait_States_Without_Known_Details()
	{
		var http = new CamundaHttpFalso();
		http.Responder(HttpMethod.Post, "/v2/element-instances/wait-states/search", $$"""
			{ "items": [
			  { "processInstanceKey": "2251799813690001", "rootProcessInstanceKey": "2251799813690001", "elementInstanceKey": "2251799813690019",
			    "elementId": "misterio", "elementType": "INTERMEDIATE_CATCH_EVENT", "bpmnProcessId": "pedido", "tenantId": "<default>" },
			  {{Espera("2251799813690020", "condicao", "CONDITION", "INTERMEDIATE_CATCH_EVENT", "\"expression\": \"=total > 10\"")}}
			], {{Pagina}} }
			""");
		using var gateway = Gateway(http);

		var esperas = await gateway.SearchWaitStatesAsync("2251799813690001", default);

		esperas.Select(e => (e.ElementId, e.Tipo)).ShouldBe(new[] { ("misterio", "UNKNOWN"), ("condicao", "CONDITION") });
	}

	// ---------- Apoio ----------

	private static SdkCamundaGateway Gateway(CamundaHttpFalso http) => new(new CamundaSettings
	{
		RestAddress = "http://camunda.falso:8080",
		Authentication = new CamundaAuthenticationSettings { Type = "None" }
	}, http);

	private static string Instancia(
		string chave, string processo, string estado, bool incidente, string? pai = null, string? elementoPai = null, string? tag = null) => $$"""
		{ "processInstanceKey": "{{chave}}", "processDefinitionId": "{{processo}}", "processDefinitionName": "{{processo}}",
		  "processDefinitionVersion": 7, {{(tag is null ? "" : $"\"processDefinitionVersionTag\": \"{tag}\",")}}
		  "startDate": "2026-10-16T10:00:00.000Z", "state": "{{estado}}", "hasIncident": {{(incidente ? "true" : "false")}},
		  "tenantId": "<default>", "processDefinitionKey": "2251799813685249", "rootProcessInstanceKey": "{{pai ?? chave}}",
		  {{(pai is null ? "" : $"\"parentProcessInstanceKey\": \"{pai}\", \"parentElementInstanceKey\": \"{elementoPai}\",")}}
		  "tags": [] }
		""";

	private static string Job(string chave, string tipo, string elemento) => $$"""
		{ "jobKey": "{{chave}}", "type": "{{tipo}}", "worker": "", "state": "CREATED", "kind": "BPMN_ELEMENT",
		  "listenerEventType": "UNSPECIFIED", "retries": 3, "isDenied": false, "hasFailedWithRetriesLeft": false,
		  "customHeaders": {}, "processDefinitionId": "pedido", "processDefinitionKey": "2251799813685249",
		  "processInstanceKey": "2251799813690001", "rootProcessInstanceKey": "2251799813690001", "elementId": "{{elemento}}",
		  "elementInstanceKey": "2251799813690011", "tenantId": "<default>", "creationTime": "2026-10-16T10:00:00.000Z",
		  "lastUpdateTime": "2026-10-16T10:00:00.000Z" }
		""";

	private static string Espera(string elementoInstancia, string elemento, string tipo, string tipoDoElemento, string detalhes) => $$"""
		{ "processInstanceKey": "2251799813690001", "rootProcessInstanceKey": "2251799813690001", "elementInstanceKey": "{{elementoInstancia}}",
		  "elementId": "{{elemento}}", "elementType": "{{tipoDoElemento}}", "bpmnProcessId": "pedido", "tenantId": "<default>",
		  "waitStateType": "{{tipo}}", "details": { "waitStateType": "{{tipo}}", {{detalhes}} } }
		""";

	/// <summary>Um pedido HTTP como o SDK o mandou.</summary>
	internal sealed record Pedido(string Metodo, string Caminho, string Query, string? TipoDoConteudo, string? Corpo);

	/// <summary>
	/// O Camunda do outro lado do SDK: responde pelo método e pela rota, e guarda cada pedido. Rota
	/// sem resposta configurada é 404, como uma rota que o cluster não tem.
	/// </summary>
	internal sealed class CamundaHttpFalso : HttpMessageHandler
	{
		private readonly Dictionary<string, (HttpStatusCode Status, string? Corpo, string Tipo)> _respostas = new();
		private readonly List<Pedido> _pedidos = new();

		public IReadOnlyList<Pedido> Pedidos
		{
			get { lock (_pedidos) return _pedidos.ToList(); }
		}

		public void Responder(HttpMethod metodo, string caminho, string? corpo, HttpStatusCode status = HttpStatusCode.OK, string tipo = "application/json") =>
			_respostas[$"{metodo.Method} {caminho}"] = (status, corpo, tipo);

		/// <summary>O corpo JSON do pedido de número <paramref name="i"/>.</summary>
		public JsonNode Corpo(int i) =>
			JsonNode.Parse(Pedidos[i].Corpo ?? throw new InvalidOperationException($"Request #{i} has no body."))!;

		protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			var corpo = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
			var caminho = request.RequestUri!.AbsolutePath;
			lock (_pedidos)
				_pedidos.Add(new Pedido(request.Method.Method, caminho, request.RequestUri.Query,
					request.Content?.Headers.ContentType?.MediaType, corpo));

			if (!_respostas.TryGetValue($"{request.Method.Method} {caminho}", out var resposta))
				resposta = (HttpStatusCode.NotFound,
					$$"""{ "type": "about:blank", "title": "NOT_FOUND", "status": 404, "detail": "No route {{caminho}} in the fake", "instance": "{{caminho}}" }""",
					"application/problem+json");

			var mensagem = new HttpResponseMessage(resposta.Status) { RequestMessage = request };
			if (resposta.Corpo is not null)
				mensagem.Content = new StringContent(resposta.Corpo, Encoding.UTF8, resposta.Tipo);
			return mensagem;
		}
	}
}
