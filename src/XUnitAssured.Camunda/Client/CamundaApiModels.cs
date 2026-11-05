using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace XUnitAssured.Camunda.Client;

/// <summary>The cluster topology, as far as a client needs it.</summary>
/// <param name="GatewayVersion">The version of the gateway; <see langword="null"/> when the cluster does not say.</param>
public sealed record CamundaTopology(string? GatewayVersion);

/// <summary>A version of a deployed process.</summary>
/// <param name="DefinitionKey">The key of this version.</param>
/// <param name="ProcessId">The process id (<c>processDefinitionId</c>).</param>
/// <param name="Name">The process name, or the process id when it has none.</param>
/// <param name="Version">The version number.</param>
/// <param name="VersionTag">The version tag, when there is one.</param>
/// <param name="TenantId">The tenant.</param>
public sealed record CamundaProcessDefinition(
	string DefinitionKey, string ProcessId, string Name, int Version, string? VersionTag, string? TenantId);

/// <summary>A file to deploy. The extension of <paramref name="FileName"/> tells the cluster what it is.</summary>
/// <param name="FileName">The name the cluster shows, such as <c>order.bpmn</c> or <c>approve.form</c>.</param>
/// <param name="Content">The content.</param>
/// <param name="MediaType">The media type of the part.</param>
public sealed record CamundaDeploymentResource(string FileName, byte[] Content, string MediaType = "application/octet-stream");

/// <summary>A process definition a deployment created.</summary>
/// <param name="ProcessId">The process id.</param>
/// <param name="DefinitionKey">The key of the deployed version.</param>
/// <param name="Version">The version number.</param>
public sealed record CamundaDeployedProcess(string ProcessId, string DefinitionKey, int Version);

/// <summary>What a job activation asks for.</summary>
/// <param name="Type">The job type.</param>
/// <param name="MaxJobs">At most this many jobs.</param>
/// <param name="JobTimeout">How long each activated job stays with the worker.</param>
/// <param name="RequestTimeout">How long the request waits for a job (the long wait).</param>
/// <param name="Worker">The worker name the cluster records.</param>
public sealed record CamundaJobActivationRequest(string Type, int MaxJobs, TimeSpan JobTimeout, TimeSpan RequestTimeout, string Worker);

/// <summary>An activated job.</summary>
/// <param name="JobKey">The job key, for the commands.</param>
/// <param name="Type">The job type.</param>
/// <param name="ElementId">The element of the diagram the job belongs to.</param>
/// <param name="ProcessInstanceKey">The instance.</param>
/// <param name="Variables">The variables the job sees.</param>
/// <param name="Retries">The retries left.</param>
public sealed record CamundaActivatedJob(
	string JobKey, string Type, string ElementId, string ProcessInstanceKey, JsonObject Variables, int Retries);

/// <summary>A process instance.</summary>
/// <param name="Key">The instance key.</param>
/// <param name="State"><c>ACTIVE</c>, <c>COMPLETED</c>, <c>TERMINATED</c> or <c>SUSPENDED</c>.</param>
/// <param name="HasIncident">Whether the instance has an incident.</param>
/// <param name="ProcessId">The process id.</param>
/// <param name="ParentProcessInstanceKey">The instance that called it by a call activity; <see langword="null"/> for a root instance.</param>
/// <param name="ParentElementInstanceKey">The call activity of the parent that started it.</param>
public sealed record CamundaProcessInstance(
	string Key, string State, bool HasIncident, string? ProcessId = null, string? ParentProcessInstanceKey = null, string? ParentElementInstanceKey = null);

/// <summary>
/// The filter of an instance search. Unset fields are left out of the request.
/// </summary>
public sealed record CamundaProcessInstanceFilter
{
	/// <summary>The parent instance: the instances it called by a call activity.</summary>
	public string? ParentProcessInstanceKey { get; init; }

	/// <summary>The process id, of any version.</summary>
	public string? ProcessId { get; init; }

	/// <summary>The state: <c>ACTIVE</c>, <c>COMPLETED</c>, <c>SUSPENDED</c> or <c>TERMINATED</c>.</summary>
	public string? State { get; init; }

	/// <summary>A variable the instance must have, with <see cref="VariableValue"/>.</summary>
	public string? VariableName { get; init; }

	/// <summary>
	/// The value of <see cref="VariableName"/>. It is compared as JSON, the way the cluster keeps it: the text
	/// <c>abc</c> matches <c>"abc"</c>.
	/// </summary>
	public JsonNode? VariableValue { get; init; }
}

/// <summary>A user task.</summary>
/// <param name="Key">The user task key, to assign and complete it.</param>
/// <param name="ElementId">The element of the diagram.</param>
/// <param name="State"><c>CREATED</c>, <c>COMPLETED</c>, <c>CANCELED</c>...</param>
/// <param name="Assignee">Who it is assigned to.</param>
/// <param name="Name">The name the Tasklist shows.</param>
/// <param name="FormKey">The key of the deployed form linked to it.</param>
/// <param name="CandidateGroups">The candidate groups; <see langword="null"/> when the cluster does not send them.</param>
/// <param name="ProcessInstanceKey">The instance.</param>
/// <param name="ProcessId">The process id.</param>
public sealed record CamundaUserTaskInfo(
	string Key, string ElementId, string State, string? Assignee, string? Name, string? FormKey, IReadOnlyList<string>? CandidateGroups,
	string? ProcessInstanceKey, string? ProcessId);

/// <summary>An element an instance went through.</summary>
/// <param name="ElementId">The element of the diagram.</param>
/// <param name="Type">The Camunda type, such as <c>SERVICE_TASK</c>.</param>
/// <param name="State"><c>ACTIVE</c>, <c>COMPLETED</c> or <c>TERMINATED</c>.</param>
/// <param name="Key">The element instance key, whose order is the order the cluster created them in.</param>
/// <param name="StartDate">When it started.</param>
/// <param name="EndDate">When it ended; <see langword="null"/> while it runs.</param>
public sealed record CamundaElementInstance(
	string ElementId, string Type, string State, long? Key, DateTimeOffset? StartDate, DateTimeOffset? EndDate);

/// <summary>A page of the elements of an instance.</summary>
/// <param name="Items">The elements that came, in key order.</param>
/// <param name="TotalItems">How many the instance has, which is more than came when it has over a thousand.</param>
public sealed record CamundaElementInstancePage(IReadOnlyList<CamundaElementInstance> Items, long TotalItems);

/// <summary>An incident of an instance.</summary>
/// <param name="ElementId">The element.</param>
/// <param name="ErrorType">The Camunda name of the error type, such as <c>JOB_NO_RETRIES</c>.</param>
/// <param name="Message">The error message.</param>
public sealed record CamundaIncidentInfo(string ElementId, string ErrorType, string Message);

/// <summary>An open message subscription.</summary>
/// <param name="Key">The subscription key.</param>
/// <param name="ElementId">The element that waits for the message.</param>
/// <param name="MessageName">The message name.</param>
/// <param name="CorrelationKey">The correlation key, already evaluated.</param>
public sealed record CamundaMessageSubscriptionInfo(string Key, string ElementId, string MessageName, string CorrelationKey);
