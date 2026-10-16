using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using XUnitAssured.Camunda.Results;

using Camunda.Orchestration.Sdk;

namespace XUnitAssured.Camunda.Gateway;

/// <summary>
/// O <see cref="ICamundaGateway"/> sobre o <c>Camunda.Orchestration.Sdk</c>.
/// </summary>
/// <remarks>
/// É o único lugar do pacote que conhece os tipos do SDK. Toda recusa do cluster sai daqui como
/// <see cref="CamundaRecusouException"/>, com o código que o cluster respondeu, porque é pelo
/// código que o pacote decide o que uma recusa significa.
/// </remarks>
internal sealed class SdkCamundaGateway : ICamundaGateway, IDisposable
{
	private readonly CamundaClient _client;

	public SdkCamundaGateway(CamundaSettings settings)
	{
		if (settings == null) throw new ArgumentNullException(nameof(settings));

		_client = CamundaClient.Create(new CamundaOptions { Config = Configuracao(settings) });
	}

	/// <summary>
	/// Traduz a seção <c>camunda</c> para as chaves do SDK. Só entra o que foi preenchido: o que
	/// faltar, o SDK busca nas variáveis <c>CAMUNDA_*</c> e depois nos padrões dele.
	/// </summary>
	internal static Dictionary<string, string> Configuracao(CamundaSettings settings)
	{
		var config = new Dictionary<string, string>();
		var auth = settings.Authentication ?? new CamundaAuthenticationSettings();

		void Por(string chave, string? valor)
		{
			if (!string.IsNullOrWhiteSpace(valor)) config[chave] = valor!;
		}

		Por("CAMUNDA_REST_ADDRESS", settings.RestAddress);
		Por("CAMUNDA_DEFAULT_TENANT_ID", settings.TenantId);

		// O SDK não deduz a estratégia Basic do usuário e da senha: sem CAMUNDA_AUTH_STRATEGY ele
		// não manda credencial nenhuma, e o cluster responde 401 (medido contra a 8.10.4). Então,
		// sem tipo no arquivo nem na variável, o tipo sai do que foi preenchido.
		var tipo = auth.Type;
		if (string.IsNullOrWhiteSpace(tipo) && string.IsNullOrWhiteSpace(Ambiente("CAMUNDA_AUTH_STRATEGY")))
		{
			if (!string.IsNullOrWhiteSpace(auth.Username ?? Ambiente("CAMUNDA_BASIC_AUTH_USERNAME")))
				tipo = "BASIC";
			else if (!string.IsNullOrWhiteSpace(auth.ClientId ?? Ambiente("CAMUNDA_CLIENT_ID")))
				tipo = "OAUTH";
		}

		if (!string.IsNullOrWhiteSpace(tipo))
		{
			config["CAMUNDA_AUTH_STRATEGY"] = tipo!.Trim().ToUpperInvariant() switch
			{
				"NONE" => "NONE",
				"BASIC" => "BASIC",
				"OAUTH" => "OAUTH",
				var outro => throw new InvalidOperationException(
					$"camunda.authentication.type '{auth.Type}' is not supported. Use None, Basic or OAuth.")
			};
		}

		Por("CAMUNDA_BASIC_AUTH_USERNAME", auth.Username);
		Por("CAMUNDA_BASIC_AUTH_PASSWORD", auth.Password);
		Por("CAMUNDA_CLIENT_ID", auth.ClientId);
		Por("CAMUNDA_CLIENT_SECRET", auth.ClientSecret);
		Por("CAMUNDA_OAUTH_URL", auth.OAuthUrl);
		Por("CAMUNDA_TOKEN_AUDIENCE", auth.Audience);

		return config;
	}

	private static string? Ambiente(string nome) => Environment.GetEnvironmentVariable(nome);

	public Task<IReadOnlyList<DeployedProcess>> DeployAsync(
		IReadOnlyList<RecursoBpmn> recursos, CancellationToken ct) =>
		Recusa(async () =>
		{
			using var conteudo = new MultipartFormDataContent();
			foreach (var recurso in recursos)
				conteudo.Add(new ByteArrayContent(recurso.Conteudo), "resources", recurso.Nome);

			var resposta = await _client.CreateDeploymentAsync(conteudo, ct).ConfigureAwait(false);

			return (IReadOnlyList<DeployedProcess>)resposta.Deployments
				.Select(d => d.ProcessDefinition)
				.Where(p => p is not null)
				.Select(p => new DeployedProcess(
					p!.ProcessDefinitionId.Value,
					p.ProcessDefinitionKey.Value,
					p.ProcessDefinitionVersion,
					p.ResourceName))
				.ToList();
		});

	public Task<string> CreateInstanceAsync(
		string definitionKey, object? variables, string? tenantId, CancellationToken ct) =>
		Recusa(async () =>
		{
			var instrucao = new ProcessInstanceCreationInstructionByKey
			{
				ProcessDefinitionKey = ProcessDefinitionKey.AssumeExists(definitionKey),
				Variables = variables,
				TenantId = tenantId is null ? null : TenantId.AssumeExists(tenantId)
			};

			var resposta = await _client.CreateProcessInstanceAsync(instrucao, ct).ConfigureAwait(false);
			return resposta.ProcessInstanceKey.Value;
		});

	public Task<IReadOnlyList<JobPendente>> SearchCreatedJobsAsync(
		IReadOnlyCollection<string> processInstanceKeys, IReadOnlyCollection<string>? types, CancellationToken ct) =>
		Recusa(async () =>
		{
			var filtro = new JobFilter
			{
				ProcessInstanceKey = new ProcessInstanceKeyFilterProperty
				{
					In = processInstanceKeys.Select(ProcessInstanceKey.AssumeExists).ToList()
				},
				State = new JobStateFilterProperty { Eq = JobStateEnum.CREATED }
			};

			if (types is { Count: > 0 })
				filtro.Type = new StringFilterProperty { In = types.ToList() };

			var resposta = await _client.SearchJobsAsync(new JobSearchQuery { Filter = filtro }, null, ct)
				.ConfigureAwait(false);

			return (IReadOnlyList<JobPendente>)resposta.Items
				.Select(j => new JobPendente(
					j.JobKey.Value, j.Type, j.ElementId?.Value ?? string.Empty, j.ProcessInstanceKey.Value))
				.ToList();
		});

	public Task CompleteJobAsync(string jobKey, object? variables, CancellationToken ct) =>
		Recusa(() => _client.CompleteJobAsync(
			JobKey.AssumeExists(jobKey), new JobCompletionRequest { Variables = variables }, ct));

	public Task ThrowBpmnErrorAsync(
		string jobKey, string errorCode, string? errorMessage, object? variables, CancellationToken ct) =>
		Recusa(() => _client.ThrowJobErrorAsync(
			JobKey.AssumeExists(jobKey),
			new JobErrorRequest { ErrorCode = errorCode, ErrorMessage = errorMessage, Variables = variables },
			ct));

	public Task<IReadOnlyList<InstanciaVista>> SearchChildInstancesAsync(
		IReadOnlyCollection<string> parentKeys, CancellationToken ct) =>
		Recusa(async () =>
		{
			var resposta = await _client.SearchProcessInstancesAsync(new ProcessInstanceSearchQuery
			{
				Filter = new ProcessInstanceFilter
				{
					ParentProcessInstanceKey = new ProcessInstanceKeyFilterProperty
					{
						In = parentKeys.Select(ProcessInstanceKey.AssumeExists).ToList()
					}
				}
			}, null, ct).ConfigureAwait(false);

			return (IReadOnlyList<InstanciaVista>)resposta.Items.Select(Vista).ToList();
		});

	public Task<InstanciaVista?> GetInstanceAsync(string processInstanceKey, CancellationToken ct) =>
		Recusa(async () =>
		{
			// A busca, e não o GET: o GET responde 404 enquanto a instância não chegou ao
			// armazenamento secundário, e "ainda não apareceu" é resposta normal aqui.
			var resposta = await _client.SearchProcessInstancesAsync(new ProcessInstanceSearchQuery
			{
				Filter = new ProcessInstanceFilter { ProcessInstanceKey = ProcessInstanceKey.AssumeExists(processInstanceKey) }
			}, null, ct).ConfigureAwait(false);

			var primeira = resposta.Items.FirstOrDefault();
			return primeira is null ? null : Vista(primeira);
		});

	public Task<IReadOnlyList<ElementoVisto>> SearchElementsAsync(string processInstanceKey, CancellationToken ct) =>
		Recusa(async () =>
		{
			var resposta = await _client.SearchElementInstancesAsync(new ElementInstanceSearchQuery
			{
				Filter = new ElementInstanceFilter { ProcessInstanceKey = ProcessInstanceKey.AssumeExists(processInstanceKey) }
			}, null, ct).ConfigureAwait(false);

			return (IReadOnlyList<ElementoVisto>)resposta.Items
				.Select(e => new ElementoVisto(e.ElementId.Value, e.ElementName ?? string.Empty, e.Type.ToString(), e.State.ToString()))
				.ToList();
		});

	public Task<IReadOnlyList<VariavelVista>> SearchVariablesAsync(
		string processInstanceKey, string? name, CancellationToken ct) =>
		Recusa(async () =>
		{
			var filtro = new VariableFilter { ProcessInstanceKey = ProcessInstanceKey.AssumeExists(processInstanceKey) };
			if (name is not null)
				filtro.Name = name;

			var resposta = await _client.SearchVariablesAsync(new VariableSearchQuery { Filter = filtro }, false, null, ct)
				.ConfigureAwait(false);

			return (IReadOnlyList<VariavelVista>)resposta.Items
				.Select(v => new VariavelVista(v.Name, v.Value, v.ScopeKey.Value))
				.ToList();
		});

	public Task<IReadOnlyList<IncidenteVisto>> SearchActiveIncidentsAsync(string processInstanceKey, CancellationToken ct) =>
		Recusa(async () =>
		{
			var resposta = await _client.SearchProcessInstanceIncidentsAsync(
				ProcessInstanceKey.AssumeExists(processInstanceKey),
				new IncidentSearchQuery
				{
					Filter = new IncidentFilter { State = new IncidentStateFilterProperty { Eq = IncidentStateEnum.ACTIVE } }
				}, null, ct).ConfigureAwait(false);

			return (IReadOnlyList<IncidenteVisto>)resposta.Items
				.Select(i => new IncidenteVisto(i.ElementId.Value, i.ErrorType.ToString(), i.ErrorMessage ?? string.Empty))
				.ToList();
		});

	public Task CancelInstanceAsync(string processInstanceKey, CancellationToken ct) =>
		Recusa(() => _client.CancelProcessInstanceAsync(
			ProcessInstanceKey.AssumeExists(processInstanceKey), new CancelProcessInstanceRequest(), ct));

	public void Dispose() => _client.Dispose();

	private static InstanciaVista Vista(ProcessInstanceResult p) => new(
		p.ProcessInstanceKey.Value,
		p.ProcessDefinitionId.Value,
		p.State.ToString(),
		p.HasIncident,
		p.ParentProcessInstanceKey?.Value);

	private static async Task Recusa(Func<Task> acao)
	{
		try
		{
			await acao().ConfigureAwait(false);
		}
		catch (HttpSdkException e)
		{
			throw Traduzir(e);
		}
	}

	private static async Task<T> Recusa<T>(Func<Task<T>> acao)
	{
		try
		{
			return await acao().ConfigureAwait(false);
		}
		catch (HttpSdkException e)
		{
			throw Traduzir(e);
		}
	}

	private static CamundaRecusouException Traduzir(HttpSdkException e)
	{
		var codigo = !string.IsNullOrWhiteSpace(e.Title)
			? e.Title
			: e.Message.Split('\n')[0].Trim();

		return new CamundaRecusouException(codigo, e.Detail ?? e.Message, e);
	}
}
