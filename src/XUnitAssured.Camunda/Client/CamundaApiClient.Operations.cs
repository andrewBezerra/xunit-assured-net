using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

using Camunda.Orchestration.Sdk;

namespace XUnitAssured.Camunda.Client;

public sealed partial class CamundaApiClient
{
	// O valor da variável no filtro, sem escapar o acento: o Camunda compara com o JSON que guardou, que não o escapa.
	private static readonly JsonSerializerOptions ValorDaVariavel = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

	/// <summary>Reads the topology of the cluster: it also tests the address and the credentials.</summary>
	/// <param name="timeout">The deadline; <see cref="DefaultTimeout"/> when <see langword="null"/>.</param>
	/// <param name="observe">Hears each HTTP request of the operation.</param>
	/// <param name="cancellationToken">The caller's cancellation.</param>
	/// <returns>The topology.</returns>
	public Task<CamundaTopology> GetTopologyAsync(
		TimeSpan? timeout = null, Action<CamundaCall>? observe = null, CancellationToken cancellationToken = default) =>
		Executar(async (sdk, ct) => new CamundaTopology((await sdk.GetTopologyAsync(ct).ConfigureAwait(false)).GatewayVersion),
			timeout ?? DefaultTimeout, observe, cancellationToken);

	/// <summary>
	/// The deployed definitions: the latest version of each process, or, with <paramref name="processId"/>, every version
	/// of that process. At most <see cref="PageSize"/>.
	/// </summary>
	/// <param name="processId">The process id; <see langword="null"/> for the latest version of every process.</param>
	/// <param name="timeout">The deadline; <see cref="DefaultTimeout"/> when <see langword="null"/>.</param>
	/// <param name="observe">Hears each HTTP request of the operation.</param>
	/// <param name="cancellationToken">The caller's cancellation.</param>
	/// <returns>The definitions.</returns>
	public Task<IReadOnlyList<CamundaProcessDefinition>> SearchProcessDefinitionsAsync(
		string? processId, TimeSpan? timeout = null, Action<CamundaCall>? observe = null, CancellationToken cancellationToken = default) =>
		Executar(async (sdk, ct) =>
		{
			var filtro = processId is null
				? new ProcessDefinitionFilter { IsLatestVersion = true }
				: new ProcessDefinitionFilter { ProcessDefinitionId = new StringFilterProperty { Eq = processId } };
			var resposta = await sdk.SearchProcessDefinitionsAsync(
				new ProcessDefinitionSearchQuery { Filter = filtro, Page = new LimitPagination { Limit = PageSize } }, null, ct).ConfigureAwait(false);
			return Lista(resposta.Items.Select(d => new CamundaProcessDefinition(
				d.ProcessDefinitionKey.Value,
				d.ProcessDefinitionId.Value,
				string.IsNullOrEmpty(d.Name) ? d.ProcessDefinitionId.Value : d.Name,
				d.Version,
				string.IsNullOrWhiteSpace(d.VersionTag) ? null : d.VersionTag,
				d.TenantId.Value)));
		}, timeout ?? DefaultTimeout, observe, cancellationToken);

	/// <summary>The XML of a deployed version, as the cluster keeps it.</summary>
	/// <param name="definitionKey">The key of the version.</param>
	/// <param name="timeout">The deadline; <see cref="DefaultTimeout"/> when <see langword="null"/>.</param>
	/// <param name="observe">Hears each HTTP request of the operation.</param>
	/// <param name="cancellationToken">The caller's cancellation.</param>
	/// <returns>The XML.</returns>
	public Task<string> GetProcessDefinitionXmlAsync(
		string definitionKey, TimeSpan? timeout = null, Action<CamundaCall>? observe = null, CancellationToken cancellationToken = default) =>
		Executar(async (sdk, ct) =>
		{
			// O XmlComoJson, embaixo do SDK, entrega o XML como string JSON: ver o porquê nele.
			var xml = await sdk.GetProcessDefinitionXmlAsync(ProcessDefinitionKey.AssumeExists(definitionKey), null, ct).ConfigureAwait(false);
			var texto = xml switch
			{
				string s => s,
				JsonElement { ValueKind: JsonValueKind.String } json => json.GetString(),
				_ => xml?.ToString()
			};
			return (texto ?? string.Empty).TrimStart((char)0xFEFF);
		}, timeout ?? DefaultTimeout, observe, cancellationToken);

	/// <summary>Deploys the resources in one deployment, and returns the process definitions it created.</summary>
	/// <param name="resources">The files: the <c>.bpmn</c> and the <c>.form</c> linked to it, for instance.</param>
	/// <param name="timeout">The deadline; <see cref="DeployTimeout"/> when <see langword="null"/>.</param>
	/// <param name="observe">Hears each HTTP request of the operation.</param>
	/// <param name="cancellationToken">The caller's cancellation.</param>
	/// <returns>The process definitions; the forms deployed are left out.</returns>
	public Task<IReadOnlyList<CamundaDeployedProcess>> DeployAsync(
		IReadOnlyList<CamundaDeploymentResource> resources, TimeSpan? timeout = null, Action<CamundaCall>? observe = null,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(resources);
		return Executar(async (sdk, ct) =>
		{
			using var conteudo = new MultipartFormDataContent();
			foreach (var recurso in resources)
			{
				var parte = new ByteArrayContent(recurso.Content);
				parte.Headers.ContentType = MediaTypeHeaderValue.Parse(recurso.MediaType);
				conteudo.Add(parte, "resources", recurso.FileName);
			}

			var resposta = await sdk.CreateDeploymentAsync(conteudo, ct).ConfigureAwait(false);
			return Lista(resposta.Deployments
				.Select(d => d.ProcessDefinition)
				.Where(p => p is not null)
				.Select(p => new CamundaDeployedProcess(p!.ProcessDefinitionId.Value, p.ProcessDefinitionKey.Value, p.ProcessDefinitionVersion)));
		}, timeout ?? DeployTimeout, observe, cancellationToken);
	}

	/// <summary>Starts an instance of the deployed version, with the variables.</summary>
	/// <param name="definitionKey">The key of the version.</param>
	/// <param name="variables">The variables of the instance.</param>
	/// <param name="timeout">The deadline; <see cref="DefaultTimeout"/> when <see langword="null"/>.</param>
	/// <param name="observe">Hears each HTTP request of the operation.</param>
	/// <param name="cancellationToken">The caller's cancellation.</param>
	/// <returns>The key of the instance.</returns>
	public Task<string> CreateProcessInstanceAsync(
		string definitionKey, JsonObject? variables, TimeSpan? timeout = null, Action<CamundaCall>? observe = null,
		CancellationToken cancellationToken = default) =>
		Executar(async (sdk, ct) =>
		{
			var resposta = await sdk.CreateProcessInstanceAsync(new ProcessInstanceCreationInstructionByKey
			{
				ProcessDefinitionKey = ProcessDefinitionKey.AssumeExists(definitionKey),
				Variables = variables?.DeepClone()
			}, ct).ConfigureAwait(false);
			return resposta.ProcessInstanceKey.Value;
		}, timeout ?? DefaultTimeout, observe, cancellationToken);

	/// <summary>
	/// Activates up to <see cref="CamundaJobActivationRequest.MaxJobs"/> jobs of the type, waiting up to
	/// <see cref="CamundaJobActivationRequest.RequestTimeout"/> for one.
	/// </summary>
	/// <param name="request">What to activate.</param>
	/// <param name="timeout">The deadline; the long wait plus <see cref="ActivationMargin"/> when <see langword="null"/>.</param>
	/// <param name="observe">Hears each HTTP request of the operation.</param>
	/// <param name="cancellationToken">The caller's cancellation.</param>
	/// <returns>The jobs; empty when none came within the long wait.</returns>
	public Task<IReadOnlyList<CamundaActivatedJob>> ActivateJobsAsync(
		CamundaJobActivationRequest request, TimeSpan? timeout = null, Action<CamundaCall>? observe = null,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(request);
		return Executar(async (sdk, ct) =>
		{
			var resposta = await sdk.ActivateJobsAsync(new JobActivationRequest
			{
				Type = request.Type,
				Worker = request.Worker,
				Timeout = (long)request.JobTimeout.TotalMilliseconds,
				MaxJobsToActivate = request.MaxJobs,
				RequestTimeout = (long)request.RequestTimeout.TotalMilliseconds
			}, ct).ConfigureAwait(false);
			return Lista(resposta.Jobs.Select(j => new CamundaActivatedJob(
				j.JobKey.Value, j.Type, j.ElementId.Value, j.ProcessInstanceKey.Value, Objeto(j.Variables), j.Retries)));
		}, timeout ?? request.RequestTimeout + ActivationMargin, observe, cancellationToken);
	}

	/// <summary>
	/// Completes the job with the variables. Tried again, up to 3 times, when the Camunda does not answer; a job that no
	/// longer exists on a new attempt counts as the success of the earlier one.
	/// </summary>
	/// <param name="jobKey">The job.</param>
	/// <param name="variables">The variables.</param>
	/// <param name="timeout">The deadline of each attempt; <see cref="DefaultTimeout"/> when <see langword="null"/>.</param>
	/// <param name="observe">Hears each HTTP request of the operation.</param>
	/// <param name="cancellationToken">The caller's cancellation.</param>
	/// <returns><see langword="false"/> when the job no longer exists (a boundary event interrupted the task).</returns>
	public Task<bool> CompleteJobAsync(
		string jobKey, JsonObject? variables, TimeSpan? timeout = null, Action<CamundaCall>? observe = null, CancellationToken cancellationToken = default) =>
		ComandoDoJob((sdk, ct) => sdk.CompleteJobAsync(JobKey.AssumeExists(jobKey), new JobCompletionRequest { Variables = variables?.DeepClone() }, ct),
			timeout, observe, cancellationToken);

	/// <summary>Throws the BPMN error on the job, tried again as <see cref="CompleteJobAsync"/> is.</summary>
	/// <param name="jobKey">The job.</param>
	/// <param name="errorCode">The error code the error path catches.</param>
	/// <param name="errorMessage">The message.</param>
	/// <param name="timeout">The deadline of each attempt; <see cref="DefaultTimeout"/> when <see langword="null"/>.</param>
	/// <param name="observe">Hears each HTTP request of the operation.</param>
	/// <param name="cancellationToken">The caller's cancellation.</param>
	/// <returns><see langword="false"/> when the job no longer exists.</returns>
	public Task<bool> ThrowJobErrorAsync(
		string jobKey, string errorCode, string? errorMessage, TimeSpan? timeout = null, Action<CamundaCall>? observe = null,
		CancellationToken cancellationToken = default) =>
		ComandoDoJob((sdk, ct) => sdk.ThrowJobErrorAsync(JobKey.AssumeExists(jobKey),
			new JobErrorRequest { ErrorCode = errorCode, ErrorMessage = errorMessage }, ct), timeout, observe, cancellationToken);

	/// <summary>Fails the job with no retries left, so the Camunda opens an incident; tried again as <see cref="CompleteJobAsync"/> is.</summary>
	/// <param name="jobKey">The job.</param>
	/// <param name="errorMessage">The message of the incident.</param>
	/// <param name="timeout">The deadline of each attempt; <see cref="DefaultTimeout"/> when <see langword="null"/>.</param>
	/// <param name="observe">Hears each HTTP request of the operation.</param>
	/// <param name="cancellationToken">The caller's cancellation.</param>
	/// <returns><see langword="false"/> when the job no longer exists.</returns>
	public Task<bool> FailJobAsync(
		string jobKey, string errorMessage, TimeSpan? timeout = null, Action<CamundaCall>? observe = null, CancellationToken cancellationToken = default) =>
		ComandoDoJob((sdk, ct) => sdk.FailJobAsync(JobKey.AssumeExists(jobKey), new JobFailRequest { Retries = 0, ErrorMessage = errorMessage }, ct),
			timeout, observe, cancellationToken);

	/// <summary>
	/// Gives the job back to the Camunda untouched (the same retries, no back off), for the worker it belongs to.
	/// </summary>
	/// <param name="jobKey">The job.</param>
	/// <param name="retries">The retries it had.</param>
	/// <param name="errorMessage">Why it is given back.</param>
	/// <param name="timeout">The deadline; <see cref="DefaultTimeout"/> when <see langword="null"/>.</param>
	/// <param name="observe">Hears each HTTP request of the operation.</param>
	/// <param name="cancellationToken">The caller's cancellation.</param>
	/// <returns><see langword="false"/> when the job no longer exists.</returns>
	public Task<bool> ReturnJobAsync(
		string jobKey, int retries, string errorMessage, TimeSpan? timeout = null, Action<CamundaCall>? observe = null,
		CancellationToken cancellationToken = default) =>
		Comando((sdk, ct) => sdk.FailJobAsync(JobKey.AssumeExists(jobKey),
			new JobFailRequest { Retries = retries, RetryBackOff = 0, ErrorMessage = errorMessage }, ct), timeout, observe, cancellationToken);

	/// <summary>Renews the deadline of the activated job: the Camunda counts <paramref name="jobTimeout"/> from now.</summary>
	/// <param name="jobKey">The job.</param>
	/// <param name="jobTimeout">The new deadline of the job.</param>
	/// <param name="timeout">The deadline of the call; <see cref="DefaultTimeout"/> when <see langword="null"/>.</param>
	/// <param name="observe">Hears each HTTP request of the operation.</param>
	/// <param name="cancellationToken">The caller's cancellation.</param>
	/// <returns><see langword="false"/> when the job no longer exists.</returns>
	public Task<bool> UpdateJobTimeoutAsync(
		string jobKey, TimeSpan jobTimeout, TimeSpan? timeout = null, Action<CamundaCall>? observe = null, CancellationToken cancellationToken = default) =>
		Comando((sdk, ct) => sdk.UpdateJobAsync(JobKey.AssumeExists(jobKey),
			new JobUpdateRequest { Changeset = new JobChangeset { Timeout = (long)jobTimeout.TotalMilliseconds } }, ct), timeout, observe, cancellationToken);

	/// <summary>Cancels the instance.</summary>
	/// <param name="processInstanceKey">The instance.</param>
	/// <param name="timeout">The deadline; <see cref="DefaultTimeout"/> when <see langword="null"/>.</param>
	/// <param name="observe">Hears each HTTP request of the operation.</param>
	/// <param name="cancellationToken">The caller's cancellation.</param>
	/// <returns><see langword="false"/> when it already ended.</returns>
	public Task<bool> CancelProcessInstanceAsync(
		string processInstanceKey, TimeSpan? timeout = null, Action<CamundaCall>? observe = null, CancellationToken cancellationToken = default) =>
		Comando((sdk, ct) => sdk.CancelProcessInstanceAsync(ProcessInstanceKey.AssumeExists(processInstanceKey), new CancelProcessInstanceRequest(), ct),
			timeout, observe, cancellationToken);

	/// <summary>The user tasks of the instance, at most a thousand.</summary>
	/// <param name="processInstanceKey">The instance.</param>
	/// <param name="elementId">Only those of this element.</param>
	/// <param name="state">Only those in this state, such as <c>CREATED</c>.</param>
	/// <param name="timeout">The deadline; <see cref="DefaultTimeout"/> when <see langword="null"/>.</param>
	/// <param name="observe">Hears each HTTP request of the operation.</param>
	/// <param name="cancellationToken">The caller's cancellation.</param>
	/// <returns>The user tasks.</returns>
	public Task<IReadOnlyList<CamundaUserTaskInfo>> SearchUserTasksAsync(
		string processInstanceKey, string? elementId = null, string? state = null, TimeSpan? timeout = null, Action<CamundaCall>? observe = null,
		CancellationToken cancellationToken = default) =>
		Executar(async (sdk, ct) =>
		{
			var filtro = new UserTaskFilter { ProcessInstanceKey = new ProcessInstanceKeyFilterProperty { Eq = ProcessInstanceKey.AssumeExists(processInstanceKey) } };
			if (elementId is not null)
				filtro.ElementId = ElementId.AssumeExists(elementId);
			if (state is not null)
				filtro.State = new UserTaskStateFilterProperty { Eq = NomesDoSdk.Valor<UserTaskStateEnum>(state) };

			var resposta = await sdk.SearchUserTasksAsync(new UserTaskSearchQuery { Filter = filtro, Page = new LimitPagination { Limit = Limite } }, null, ct)
				.ConfigureAwait(false);
			return Lista(resposta.Items.Select(t => new CamundaUserTaskInfo(
				t.UserTaskKey.Value, t.ElementId.Value, NomesDoSdk.Nome(t.State), t.Assignee, t.Name, t.FormKey?.Value,
				t.CandidateGroups, t.ProcessInstanceKey.Value, t.ProcessDefinitionId.Value)));
		}, timeout ?? DefaultTimeout, observe, cancellationToken);

	/// <summary>Assigns the user task, overriding who had it.</summary>
	/// <param name="userTaskKey">The user task.</param>
	/// <param name="assignee">Who takes it.</param>
	/// <param name="timeout">The deadline; <see cref="DefaultTimeout"/> when <see langword="null"/>.</param>
	/// <param name="observe">Hears each HTTP request of the operation.</param>
	/// <param name="cancellationToken">The caller's cancellation.</param>
	/// <returns><see langword="false"/> when the task no longer exists.</returns>
	public Task<bool> AssignUserTaskAsync(
		string userTaskKey, string assignee, TimeSpan? timeout = null, Action<CamundaCall>? observe = null, CancellationToken cancellationToken = default) =>
		Comando((sdk, ct) => sdk.AssignUserTaskAsync(UserTaskKey.AssumeExists(userTaskKey),
			new UserTaskAssignmentRequest { Assignee = assignee, AllowOverride = true }, ct), timeout, observe, cancellationToken);

	/// <summary>Completes the user task with the variables, as the Tasklist does.</summary>
	/// <param name="userTaskKey">The user task.</param>
	/// <param name="variables">The variables of the answer.</param>
	/// <param name="timeout">The deadline; <see cref="DefaultTimeout"/> when <see langword="null"/>.</param>
	/// <param name="observe">Hears each HTTP request of the operation.</param>
	/// <param name="cancellationToken">The caller's cancellation.</param>
	/// <returns><see langword="false"/> when the task no longer exists.</returns>
	public Task<bool> CompleteUserTaskAsync(
		string userTaskKey, JsonObject? variables, TimeSpan? timeout = null, Action<CamundaCall>? observe = null, CancellationToken cancellationToken = default) =>
		Comando((sdk, ct) => sdk.CompleteUserTaskAsync(UserTaskKey.AssumeExists(userTaskKey),
			new UserTaskCompletionRequest { Variables = variables?.DeepClone() }, ct), timeout, observe, cancellationToken);

	/// <summary>The process instances that match the filter, at most a thousand.</summary>
	/// <param name="filter">The filter.</param>
	/// <param name="timeout">The deadline; <see cref="DefaultTimeout"/> when <see langword="null"/>.</param>
	/// <param name="observe">Hears each HTTP request of the operation.</param>
	/// <param name="cancellationToken">The caller's cancellation.</param>
	/// <returns>The instances.</returns>
	public Task<IReadOnlyList<CamundaProcessInstance>> SearchProcessInstancesAsync(
		CamundaProcessInstanceFilter filter, TimeSpan? timeout = null, Action<CamundaCall>? observe = null, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(filter);
		return Executar(async (sdk, ct) =>
		{
			var filtro = new ProcessInstanceFilter();
			if (filter.ParentProcessInstanceKey is { } mae)
				filtro.ParentProcessInstanceKey = new ProcessInstanceKeyFilterProperty { Eq = ProcessInstanceKey.AssumeExists(mae) };
			if (filter.ProcessId is { } processo)
				filtro.ProcessDefinitionId = new StringFilterProperty { Eq = processo };
			if (filter.State is { } estado)
				filtro.State = new ProcessInstanceStateFilterProperty { Eq = NomesDoSdk.Valor<ProcessInstanceStateEnum>(estado) };
			if (filter.VariableName is { } variavel)
				filtro.Variables = new List<VariableValueFilterProperty>
				{
					new() { Name = variavel, Value = new StringFilterProperty { Eq = (filter.VariableValue ?? JsonValue.Create((string?)null))?.ToJsonString(ValorDaVariavel) ?? "null" } }
				};

			var resposta = await sdk.SearchProcessInstancesAsync(
				new ProcessInstanceSearchQuery { Filter = filtro, Page = new LimitPagination { Limit = Limite } }, null, ct).ConfigureAwait(false);
			return Lista(resposta.Items.Select(Instancia));
		}, timeout ?? DefaultTimeout, observe, cancellationToken);
	}

	/// <summary>The instance, as the search of the Camunda sees it.</summary>
	/// <param name="processInstanceKey">The instance.</param>
	/// <param name="timeout">The deadline; <see cref="DefaultTimeout"/> when <see langword="null"/>.</param>
	/// <param name="observe">Hears each HTTP request of the operation.</param>
	/// <param name="cancellationToken">The caller's cancellation.</param>
	/// <returns>The instance; <see langword="null"/> while it has not reached the search yet.</returns>
	public async Task<CamundaProcessInstance?> GetProcessInstanceAsync(
		string processInstanceKey, TimeSpan? timeout = null, Action<CamundaCall>? observe = null, CancellationToken cancellationToken = default)
	{
		try
		{
			return await Executar(async (sdk, ct) => Instancia(
				await sdk.GetProcessInstanceAsync(ProcessInstanceKey.AssumeExists(processInstanceKey), null, ct).ConfigureAwait(false)),
				timeout ?? DefaultTimeout, observe, cancellationToken).ConfigureAwait(false);
		}
		catch (CamundaApiException recusa) when (recusa.Status == 404)
		{
			return null;
		}
	}

	/// <summary>The open message subscriptions of the instance, at most a thousand.</summary>
	/// <param name="processInstanceKey">The instance.</param>
	/// <param name="timeout">The deadline; <see cref="DefaultTimeout"/> when <see langword="null"/>.</param>
	/// <param name="observe">Hears each HTTP request of the operation.</param>
	/// <param name="cancellationToken">The caller's cancellation.</param>
	/// <returns>The subscriptions.</returns>
	public Task<IReadOnlyList<CamundaMessageSubscriptionInfo>> SearchMessageSubscriptionsAsync(
		string processInstanceKey, TimeSpan? timeout = null, Action<CamundaCall>? observe = null, CancellationToken cancellationToken = default) =>
		Executar(async (sdk, ct) =>
		{
			var resposta = await sdk.SearchMessageSubscriptionsAsync(new MessageSubscriptionSearchQuery
			{
				Filter = new MessageSubscriptionFilter
				{
					ProcessInstanceKey = new ProcessInstanceKeyFilterProperty { Eq = ProcessInstanceKey.AssumeExists(processInstanceKey) },
					MessageSubscriptionState = new MessageSubscriptionStateFilterProperty { Eq = MessageSubscriptionStateEnum.CREATED }
				},
				Page = new LimitPagination { Limit = Limite }
			}, null, ct).ConfigureAwait(false);
			return Lista(resposta.Items.Select(a => new CamundaMessageSubscriptionInfo(
				a.MessageSubscriptionKey.Value, a.ElementId.Value, a.MessageName ?? string.Empty, a.CorrelationKey ?? string.Empty)));
		}, timeout ?? DefaultTimeout, observe, cancellationToken);

	/// <summary>Correlates the message with the subscription that waits for it.</summary>
	/// <param name="name">The message name.</param>
	/// <param name="correlationKey">The correlation key.</param>
	/// <param name="variables">The variables of the message.</param>
	/// <param name="timeout">The deadline; <see cref="DefaultTimeout"/> when <see langword="null"/>.</param>
	/// <param name="observe">Hears each HTTP request of the operation.</param>
	/// <param name="cancellationToken">The caller's cancellation.</param>
	/// <returns><see langword="false"/> when no subscription waits for it any more.</returns>
	public Task<bool> CorrelateMessageAsync(
		string name, string correlationKey, JsonObject? variables, TimeSpan? timeout = null, Action<CamundaCall>? observe = null,
		CancellationToken cancellationToken = default) =>
		Comando((sdk, ct) => sdk.CorrelateMessageAsync(new MessageCorrelationRequest
		{
			Name = name, CorrelationKey = correlationKey, Variables = variables?.DeepClone()
		}, ct), timeout, observe, cancellationToken);

	/// <summary>The elements the instance went through, at most a thousand, in key order, and how many it has.</summary>
	/// <param name="processInstanceKey">The instance.</param>
	/// <param name="timeout">The deadline; <see cref="DefaultTimeout"/> when <see langword="null"/>.</param>
	/// <param name="observe">Hears each HTTP request of the operation.</param>
	/// <param name="cancellationToken">The caller's cancellation.</param>
	/// <returns>The page of elements.</returns>
	public Task<CamundaElementInstancePage> SearchElementInstancesAsync(
		string processInstanceKey, TimeSpan? timeout = null, Action<CamundaCall>? observe = null, CancellationToken cancellationToken = default) =>
		Executar(async (sdk, ct) =>
		{
			var resposta = await sdk.SearchElementInstancesAsync(new ElementInstanceSearchQuery
			{
				Filter = new ElementInstanceFilter { ProcessInstanceKey = ProcessInstanceKey.AssumeExists(processInstanceKey) },
				Sort = new List<ElementInstanceSearchQuerySortRequest>
				{
					new() { Field = ElementInstanceSearchQuerySortRequestField.ElementInstanceKey, Order = SortOrderEnum.ASC }
				},
				Page = new LimitPagination { Limit = Limite }
			}, null, ct).ConfigureAwait(false);
			var itens = Lista(resposta.Items.Select(e => new CamundaElementInstance(
				e.ElementId.Value, NomesDoSdk.Nome(e.Type), NomesDoSdk.Nome(e.State),
				long.TryParse(e.ElementInstanceKey.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var chave) ? chave : null,
				e.StartDate == default ? null : e.StartDate, e.EndDate)));
			return new CamundaElementInstancePage(itens, resposta.Page?.TotalItems ?? itens.Count);
		}, timeout ?? DefaultTimeout, observe, cancellationToken);

	/// <summary>
	/// The elements of the instance once the search of the Camunda, which lags behind the engine, shows the instance in the
	/// state it was left in: searched again while an element is still <c>ACTIVE</c>, unless the instance was left
	/// <c>ACTIVE</c>. After <paramref name="attempts"/> searches, the last one seen, without an error.
	/// </summary>
	/// <param name="processInstanceKey">The instance.</param>
	/// <param name="expectedState">The state the instance was left in.</param>
	/// <param name="attempts">At most this many searches.</param>
	/// <param name="interval">The wait between searches; 500 ms when <see langword="null"/>.</param>
	/// <param name="timeout">The deadline of each search; <see cref="DefaultTimeout"/> when <see langword="null"/>.</param>
	/// <param name="observe">Hears each HTTP request of the operation.</param>
	/// <param name="cancellationToken">The caller's cancellation.</param>
	/// <returns>The elements, in key order.</returns>
	public async Task<IReadOnlyList<CamundaElementInstance>> SearchSettledElementsAsync(
		string processInstanceKey, string expectedState, int attempts = 20, TimeSpan? interval = null, TimeSpan? timeout = null,
		Action<CamundaCall>? observe = null, CancellationToken cancellationToken = default)
	{
		for (var tentativa = 1; ; tentativa++)
		{
			var pagina = await SearchElementInstancesAsync(processInstanceKey, timeout, observe, cancellationToken).ConfigureAwait(false);
			if (string.Equals(expectedState, "ACTIVE", StringComparison.OrdinalIgnoreCase)
				|| pagina.Items.All(e => e.State != "ACTIVE")
				|| tentativa >= attempts)
				return pagina.Items;
			await Task.Delay(interval ?? TimeSpan.FromMilliseconds(500), _tempo, cancellationToken).ConfigureAwait(false);
		}
	}

	/// <summary>
	/// The variables of the instance, at its own scope (not the local ones of an element), by name, each value read as the
	/// JSON the Camunda keeps. The search is asked without truncating the values, and follows the pages until the last one;
	/// a value that still comes truncated is read whole by its key.
	/// </summary>
	/// <param name="processInstanceKey">The instance.</param>
	/// <param name="timeout">The deadline of the whole search; <see cref="DefaultTimeout"/> when <see langword="null"/>.</param>
	/// <param name="observe">Hears each HTTP request of the operation.</param>
	/// <param name="cancellationToken">The caller's cancellation.</param>
	/// <returns>The variables by name; empty when the instance has none, or while it has not reached the search yet.</returns>
	public Task<IReadOnlyDictionary<string, JsonNode?>> SearchInstanceVariablesAsync(
		string processInstanceKey, TimeSpan? timeout = null, Action<CamundaCall>? observe = null, CancellationToken cancellationToken = default) =>
		Executar<IReadOnlyDictionary<string, JsonNode?>>(async (sdk, ct) =>
		{
			var instancia = ProcessInstanceKey.AssumeExists(processInstanceKey);
			var variaveis = new Dictionary<string, JsonNode?>(StringComparer.Ordinal);
			EndCursor? depois = null;
			while (true)
			{
				var resposta = await sdk.SearchVariablesAsync(new VariableSearchQuery
				{
					Filter = new VariableFilter
					{
						ProcessInstanceKey = new ProcessInstanceKeyFilterProperty { Eq = instancia },
						ScopeKey = new ScopeKeyFilterProperty { Eq = ScopeKey.AssumeExists(processInstanceKey) }
					},
					Page = new CursorForwardPagination { Limit = Limite, After = depois }
				}, false, null, ct).ConfigureAwait(false);

				foreach (var variavel in resposta.Items)
				{
					var valor = variavel.IsTruncated == true
						? (await sdk.GetVariableAsync(variavel.VariableKey, null, ct).ConfigureAwait(false)).Value
						: variavel.Value;
					variaveis[variavel.Name] = valor is null ? null : JsonNode.Parse(valor);
				}

				// A última página: veio vazia, ou já se leu o total, ou não há de onde seguir.
				depois = resposta.Page?.EndCursor;
				if (resposta.Items.Count == 0 || variaveis.Count >= (resposta.Page?.TotalItems ?? 0) || depois is null)
					return variaveis;
			}
		}, timeout ?? DefaultTimeout, observe, cancellationToken);

	/// <summary>The incidents of the instance, at most a thousand.</summary>
	/// <param name="processInstanceKey">The instance.</param>
	/// <param name="timeout">The deadline; <see cref="DefaultTimeout"/> when <see langword="null"/>.</param>
	/// <param name="observe">Hears each HTTP request of the operation.</param>
	/// <param name="cancellationToken">The caller's cancellation.</param>
	/// <returns>The incidents.</returns>
	public Task<IReadOnlyList<CamundaIncidentInfo>> SearchIncidentsAsync(
		string processInstanceKey, TimeSpan? timeout = null, Action<CamundaCall>? observe = null, CancellationToken cancellationToken = default) =>
		Executar(async (sdk, ct) =>
		{
			var resposta = await sdk.SearchIncidentsAsync(new IncidentSearchQuery
			{
				Filter = new IncidentFilter { ProcessInstanceKey = new ProcessInstanceKeyFilterProperty { Eq = ProcessInstanceKey.AssumeExists(processInstanceKey) } },
				Page = new LimitPagination { Limit = Limite }
			}, null, ct).ConfigureAwait(false);
			return Lista(resposta.Items.Select(i => new CamundaIncidentInfo(i.ElementId.Value, NomesDoSdk.Nome(i.ErrorType), i.ErrorMessage ?? string.Empty)));
		}, timeout ?? DefaultTimeout, observe, cancellationToken);

	private static CamundaProcessInstance Instancia(ProcessInstanceResult p) => new(
		p.ProcessInstanceKey.Value, NomesDoSdk.Nome(p.State), p.HasIncident, p.ProcessDefinitionId.Value,
		p.ParentProcessInstanceKey?.Value, p.ParentElementInstanceKey?.Value);

	// As variáveis de um job, que o SDK lê como objeto sem tipo (um JsonElement).
	private static JsonObject Objeto(object? variaveis) => variaveis switch
	{
		null => new JsonObject(),
		JsonObject objeto => objeto,
		_ => JsonSerializer.SerializeToNode(variaveis) as JsonObject ?? new JsonObject()
	};
}
