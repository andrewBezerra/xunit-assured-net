using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace XUnitAssured.Camunda.Client;

/// <summary>
/// A client of the Camunda Hub (the Web Modeler) REST API: workspaces, projects, folders, files and their versions. One
/// per configuration, with its connections reused, a deadline per call, the OAuth token kept and asked once for
/// concurrent calls, and each HTTP request told to the caller.
/// </summary>
/// <remarks>
/// <para>
/// Get it with <see cref="For"/>, which returns the same client for the same settings for the life of the process. It is
/// safe for concurrent calls, and it is not meant to be disposed.
/// </para>
/// <para>
/// The Hub takes only a JWT: the client asks the token by client credentials, with the audience
/// <see cref="DefaultAudience"/> when none is given, and asks a new one, once, when the Hub answers 401 or 403. It speaks
/// the Hub API v2 (<c>/api/v2</c>) and the Web Modeler API v1 (<c>/api/v1</c>, removed in Camunda 8.12) behind the same
/// methods; with no <see cref="CamundaHubSettings.ApiVersion"/>, the first call chooses: the v2 when <c>/api/v2/info</c>
/// answers, the v1 when only <c>/api/v1/info</c> does.
/// </para>
/// <para>
/// Every operation takes a <c>timeout</c> (<see cref="DefaultTimeout"/> when <see langword="null"/>), an <c>observe</c>
/// callback that hears each HTTP request it makes (<see cref="CamundaCall"/>), and the caller's cancellation token. It
/// throws <see cref="CamundaApiException"/> when the Hub refuses (the 409 of a stale revision included),
/// <see cref="CamundaCredentialException"/> when the credentials are refused, and <see cref="CamundaUnavailableException"/>
/// when there is no answer.
/// </para>
/// </remarks>
public sealed class CamundaHubClient
{
	/// <summary>The audience of the token when none is given: <c>web-modeler-public-api</c>.</summary>
	public const string DefaultAudience = "web-modeler-public-api";

	/// <summary>The deadline of a call when none is given: 10 s.</summary>
	public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);

	private static readonly ConcurrentDictionary<string, CamundaHubClient> Compartilhados = new(StringComparer.Ordinal);

	private readonly TimeProvider _tempo;
	private readonly ChamadasAoHub _hub;
	private readonly IHubApi? _dada;
	private readonly object _trava = new();
	private Task<IHubApi>? _escolha;

	/// <summary>
	/// The client for these settings, shared by every caller with the same settings for the life of the process. Nothing
	/// comes from the <c>CAMUNDA_*</c> environment variables: those are the credentials of the orchestration cluster.
	/// </summary>
	/// <param name="settings">The address of the Hub, its OAuth client, and the API version, if fixed.</param>
	/// <returns>The shared client.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="settings"/> is <see langword="null"/>.</exception>
	/// <exception cref="InvalidOperationException">
	/// The address is missing, the authentication type is not OAuth, or the client id, the client secret or the token
	/// address is missing. The message names the field, never the secret.
	/// </exception>
	public static CamundaHubClient For(CamundaHubSettings settings)
	{
		ArgumentNullException.ThrowIfNull(settings);
		Autenticacao(settings);
		return Compartilhados.GetOrAdd(Key(settings), _ => new CamundaHubClient(settings, Transporte(), TimeProvider.System));
	}

	/// <summary>O cliente sobre o handler dado (o transporte de verdade, ou o falso dos testes), com o relógio dos prazos.</summary>
	internal CamundaHubClient(CamundaHubSettings settings, HttpMessageHandler transporte, TimeProvider tempo)
	{
		ArgumentNullException.ThrowIfNull(settings);
		ArgumentNullException.ThrowIfNull(transporte);
		ArgumentNullException.ThrowIfNull(tempo);

		var dados = Autenticacao(settings);
		var endereco = settings.RestAddress!.Trim().TrimEnd('/');
		var prefixo = new Uri(endereco + "/").AbsolutePath.TrimEnd('/');

		_tempo = tempo;
		Transport = transporte;
		Autenticador = new AutenticacaoDoCliente(new ObservadorDasChamadas(transporte, prefixo, tempo), dados, tempo);
		var http = new HttpClient(Autenticador, disposeHandler: false) { BaseAddress = new Uri(endereco + "/"), Timeout = Timeout.InfiniteTimeSpan };
		_hub = new ChamadasAoHub(http, endereco);
		_dada = settings.ApiVersion switch
		{
			CamundaHubApiVersion.V1 => new HubApiV1(_hub),
			CamundaHubApiVersion.V2 => new HubApiV2(_hub),
			_ => null
		};
	}

	/// <summary>O handler embaixo de tudo: o <see cref="SocketsHttpHandler"/> do pool, ou o falso dos testes.</summary>
	internal HttpMessageHandler Transport { get; }

	private AutenticacaoDoCliente Autenticador { get; }

	/// <summary>
	/// A chave do cliente compartilhado: a do <see cref="CamundaApiClient"/> (o endereço e a autenticação, com o hash do
	/// secret, nunca o valor), e a versão da API.
	/// </summary>
	internal static string Key(CamundaHubSettings settings) =>
		JsonSerializer.Serialize(new[]
		{
			"hub",
			CamundaApiClient.Key(new CamundaSettings { RestAddress = settings.RestAddress, Authentication = settings.Authentication }),
			settings.ApiVersion?.ToString()
		});

	// Só o OAuth (a documentação do Camunda 8.10 só descreve o JWT para o Hub), com o que ele pede, e o audience padrão.
	private static DadosDaAutenticacao Autenticacao(CamundaHubSettings settings)
	{
		if (string.IsNullOrWhiteSpace(settings.RestAddress))
			throw new InvalidOperationException("restAddress is required: the address of the Camunda Hub, such as http://localhost:8070.");

		var auth = settings.Authentication ?? new CamundaAuthenticationSettings();
		if (auth.Type is { } tipo && !string.Equals(tipo.Trim(), "OAuth", StringComparison.OrdinalIgnoreCase))
			throw new InvalidOperationException(
				$"The Camunda Hub takes only OAuth: authentication.type '{tipo}' is not supported. Use OAuth, or leave it empty.");

		static string Exigir(string? valor, string campo) => string.IsNullOrWhiteSpace(valor)
			? throw new InvalidOperationException($"authentication.{campo} is required: the Camunda Hub takes an OAuth token by client credentials.")
			: valor.Trim();

		var clientId = Exigir(auth.ClientId, "clientId");
		var secret = Exigir(auth.ClientSecret, "clientSecret");
		var token = Exigir(auth.OAuthUrl, "oauthUrl");
		var audience = string.IsNullOrWhiteSpace(auth.Audience) ? DefaultAudience : auth.Audience.Trim();
		return new DadosDaAutenticacao("OAUTH", null, null, clientId, secret, token, audience, auth.Scope);
	}

	// O pool de conexões, como o do CamundaApiClient: relidas a cada 5 min, fechadas depois de 1 min ociosas.
	private static SocketsHttpHandler Transporte() => new()
	{
		PooledConnectionLifetime = TimeSpan.FromMinutes(5),
		PooledConnectionIdleTimeout = TimeSpan.FromMinutes(1),
	};

	/// <summary>Reads what the Hub says about the API and the credentials: it also tests the address and the client.</summary>
	/// <param name="timeout">The deadline; <see cref="DefaultTimeout"/> when <see langword="null"/>.</param>
	/// <param name="observe">Hears each HTTP request of the operation.</param>
	/// <param name="cancellationToken">The caller's cancellation.</param>
	/// <returns>The API version the client speaks, the organization and, on the v1, the permissions.</returns>
	public Task<CamundaHubInfo> GetInfoAsync(TimeSpan? timeout = null, Action<CamundaCall>? observe = null, CancellationToken cancellationToken = default) =>
		Executar((api, ct) => api.Info(ct), timeout, observe, cancellationToken);

	/// <summary>
	/// The workspaces the client sees, every page of them; with <paramref name="name"/>, only those with exactly that name
	/// (ordinal comparison).
	/// </summary>
	/// <param name="name">The exact name; <see langword="null"/> for every workspace.</param>
	/// <param name="timeout">The deadline of the whole search; <see cref="DefaultTimeout"/> when <see langword="null"/>.</param>
	/// <param name="observe">Hears each HTTP request of the operation.</param>
	/// <param name="cancellationToken">The caller's cancellation.</param>
	/// <returns>The workspaces.</returns>
	public Task<IReadOnlyList<CamundaHubWorkspace>> SearchWorkspacesAsync(
		string? name, TimeSpan? timeout = null, Action<CamundaCall>? observe = null, CancellationToken cancellationToken = default) =>
		Executar(async (api, ct) => Exatos(await api.SearchWorkspaces(name, ct).ConfigureAwait(false), w => w.Name, name),
			timeout, observe, cancellationToken);

	/// <summary>Creates a workspace.</summary>
	/// <param name="name">The name.</param>
	/// <param name="timeout">The deadline; <see cref="DefaultTimeout"/> when <see langword="null"/>.</param>
	/// <param name="observe">Hears each HTTP request of the operation.</param>
	/// <param name="cancellationToken">The caller's cancellation.</param>
	/// <returns>The workspace created.</returns>
	public Task<CamundaHubWorkspace> CreateWorkspaceAsync(
		string name, TimeSpan? timeout = null, Action<CamundaCall>? observe = null, CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(name);
		return Executar((api, ct) => api.CreateWorkspace(name, ct), timeout, observe, cancellationToken);
	}

	/// <summary>A workspace, with its projects.</summary>
	/// <param name="key">The key of the workspace.</param>
	/// <param name="timeout">The deadline; <see cref="DefaultTimeout"/> when <see langword="null"/>.</param>
	/// <param name="observe">Hears each HTTP request of the operation.</param>
	/// <param name="cancellationToken">The caller's cancellation.</param>
	/// <returns>The workspace and its projects.</returns>
	public Task<CamundaHubWorkspaceDetail> GetWorkspaceAsync(
		string key, TimeSpan? timeout = null, Action<CamundaCall>? observe = null, CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(key);
		return Executar((api, ct) => api.GetWorkspace(key, ct), timeout, observe, cancellationToken);
	}

	/// <summary>
	/// The projects of a workspace, every page of them; with <paramref name="name"/>, only those with exactly that name
	/// (ordinal comparison).
	/// </summary>
	/// <param name="workspaceKey">The key of the workspace.</param>
	/// <param name="name">The exact name; <see langword="null"/> for every project of the workspace.</param>
	/// <param name="timeout">The deadline of the whole search; <see cref="DefaultTimeout"/> when <see langword="null"/>.</param>
	/// <param name="observe">Hears each HTTP request of the operation.</param>
	/// <param name="cancellationToken">The caller's cancellation.</param>
	/// <returns>The projects.</returns>
	public Task<IReadOnlyList<CamundaHubProject>> SearchProjectsAsync(
		string workspaceKey, string? name, TimeSpan? timeout = null, Action<CamundaCall>? observe = null, CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(workspaceKey);
		return Executar(async (api, ct) => Exatos(await api.SearchProjects(workspaceKey, name, ct).ConfigureAwait(false), p => p.Name, name),
			timeout, observe, cancellationToken);
	}

	/// <summary>Creates a project in a workspace.</summary>
	/// <param name="workspaceKey">The key of the workspace.</param>
	/// <param name="name">The name.</param>
	/// <param name="timeout">The deadline; <see cref="DefaultTimeout"/> when <see langword="null"/>.</param>
	/// <param name="observe">Hears each HTTP request of the operation.</param>
	/// <param name="cancellationToken">The caller's cancellation.</param>
	/// <returns>The project created.</returns>
	public Task<CamundaHubProject> CreateProjectAsync(
		string workspaceKey, string name, TimeSpan? timeout = null, Action<CamundaCall>? observe = null, CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(workspaceKey);
		ArgumentException.ThrowIfNullOrWhiteSpace(name);
		return Executar((api, ct) => api.CreateProject(workspaceKey, name, ct), timeout, observe, cancellationToken);
	}

	/// <summary>The folders and the files at the root of a project, without the content of the files.</summary>
	/// <param name="key">The key of the project.</param>
	/// <param name="timeout">The deadline; <see cref="DefaultTimeout"/> when <see langword="null"/>.</param>
	/// <param name="observe">Hears each HTTP request of the operation.</param>
	/// <param name="cancellationToken">The caller's cancellation.</param>
	/// <returns>The folders and the files.</returns>
	public Task<CamundaHubContent> GetProjectContentAsync(
		string key, TimeSpan? timeout = null, Action<CamundaCall>? observe = null, CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(key);
		return Executar((api, ct) => api.GetProjectContent(key, ct), timeout, observe, cancellationToken);
	}

	/// <summary>The folders and the files right inside a folder, without the content of the files.</summary>
	/// <param name="key">The key of the folder.</param>
	/// <param name="timeout">The deadline; <see cref="DefaultTimeout"/> when <see langword="null"/>.</param>
	/// <param name="observe">Hears each HTTP request of the operation.</param>
	/// <param name="cancellationToken">The caller's cancellation.</param>
	/// <returns>The folders and the files.</returns>
	public Task<CamundaHubContent> GetFolderContentAsync(
		string key, TimeSpan? timeout = null, Action<CamundaCall>? observe = null, CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(key);
		return Executar((api, ct) => api.GetFolderContent(key, ct), timeout, observe, cancellationToken);
	}

	/// <summary>Creates a folder at the root of a project, or inside another folder of it.</summary>
	/// <param name="projectKey">The key of the project.</param>
	/// <param name="parentFolderKey">The key of the folder to create it in; <see langword="null"/> for the root of the project.</param>
	/// <param name="name">The name.</param>
	/// <param name="timeout">The deadline; <see cref="DefaultTimeout"/> when <see langword="null"/>.</param>
	/// <param name="observe">Hears each HTTP request of the operation.</param>
	/// <param name="cancellationToken">The caller's cancellation.</param>
	/// <returns>The folder created.</returns>
	public Task<CamundaHubFolder> CreateFolderAsync(
		string projectKey, string? parentFolderKey, string name, TimeSpan? timeout = null, Action<CamundaCall>? observe = null,
		CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(projectKey);
		ArgumentException.ThrowIfNullOrWhiteSpace(name);
		return Executar((api, ct) => api.CreateFolder(projectKey, parentFolderKey, name, ct), timeout, observe, cancellationToken);
	}

	/// <summary>
	/// The files of a project, in any of its folders, every page of them, without their content; with
	/// <paramref name="name"/> and <paramref name="type"/>, only those with exactly that name (ordinal comparison) and type.
	/// </summary>
	/// <param name="projectKey">The key of the project.</param>
	/// <param name="name">The exact name, without the extension; <see langword="null"/> for any name.</param>
	/// <param name="type">The type; <see langword="null"/> for any type, those the client does not handle included.</param>
	/// <param name="timeout">The deadline of the whole search; <see cref="DefaultTimeout"/> when <see langword="null"/>.</param>
	/// <param name="observe">Hears each HTTP request of the operation.</param>
	/// <param name="cancellationToken">The caller's cancellation.</param>
	/// <returns>The files.</returns>
	public Task<IReadOnlyList<CamundaHubFileMetadata>> SearchFilesAsync(
		string projectKey, string? name = null, CamundaHubFileType? type = null, TimeSpan? timeout = null, Action<CamundaCall>? observe = null,
		CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(projectKey);
		return Executar(async (api, ct) =>
		{
			var achados = Exatos(await api.SearchFiles(projectKey, name, type, ct).ConfigureAwait(false), f => f.Name, name);
			return type is null ? achados : achados.Where(f => f.Type == type).ToList();
		}, timeout, observe, cancellationToken);
	}

	/// <summary>A file, with its content and its revision.</summary>
	/// <param name="key">The key of the file.</param>
	/// <param name="timeout">The deadline; <see cref="DefaultTimeout"/> when <see langword="null"/>.</param>
	/// <param name="observe">Hears each HTTP request of the operation.</param>
	/// <param name="cancellationToken">The caller's cancellation.</param>
	/// <returns>The file; <see langword="null"/> when the Hub does not have it (404).</returns>
	public Task<CamundaHubFile?> GetFileAsync(
		string key, TimeSpan? timeout = null, Action<CamundaCall>? observe = null, CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(key);
		return Executar((api, ct) => api.GetFile(key, ct), timeout, observe, cancellationToken);
	}

	/// <summary>Creates a file at the root of a project, or in a folder of it.</summary>
	/// <param name="projectKey">The key of the project.</param>
	/// <param name="folderKey">The key of the folder; <see langword="null"/> for the root of the project.</param>
	/// <param name="name">The name, without the extension.</param>
	/// <param name="type">The type.</param>
	/// <param name="content">The content: the XML of a BPMN, the JSON of a form.</param>
	/// <param name="timeout">The deadline; <see cref="DefaultTimeout"/> when <see langword="null"/>.</param>
	/// <param name="observe">Hears each HTTP request of the operation.</param>
	/// <param name="cancellationToken">The caller's cancellation.</param>
	/// <returns>The file created, with its key and its first revision.</returns>
	public Task<CamundaHubFileMetadata> CreateFileAsync(
		string projectKey, string? folderKey, string name, CamundaHubFileType type, string content, TimeSpan? timeout = null,
		Action<CamundaCall>? observe = null, CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(projectKey);
		ArgumentException.ThrowIfNullOrWhiteSpace(name);
		ArgumentException.ThrowIfNullOrEmpty(content);
		return Executar((api, ct) => api.CreateFile(projectKey, folderKey, name, type, content, ct), timeout, observe, cancellationToken);
	}

	/// <summary>
	/// Replaces the content of a file. The revision is the one read: when the file changed in the Hub since then, the Hub
	/// refuses with 409, a <see cref="CamundaApiException"/> whose <see cref="CamundaApiException.Status"/> is 409.
	/// </summary>
	/// <param name="key">The key of the file.</param>
	/// <param name="content">The new content.</param>
	/// <param name="revision">The revision the content was based on.</param>
	/// <param name="timeout">The deadline; <see cref="DefaultTimeout"/> when <see langword="null"/>.</param>
	/// <param name="observe">Hears each HTTP request of the operation.</param>
	/// <param name="cancellationToken">The caller's cancellation.</param>
	/// <returns>The file updated, with its new revision.</returns>
	public Task<CamundaHubFileMetadata> UpdateFileAsync(
		string key, string content, int revision, TimeSpan? timeout = null, Action<CamundaCall>? observe = null,
		CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(key);
		ArgumentException.ThrowIfNullOrEmpty(content);
		return Executar((api, ct) => api.UpdateFile(key, content, revision, ct), timeout, observe, cancellationToken);
	}

	/// <summary>
	/// Creates a version of a file: a snapshot of its current content in the Hub, which the Hub lists in the Versions menu
	/// of the file and can restore.
	/// </summary>
	/// <param name="fileKey">The key of the file.</param>
	/// <param name="name">The name of the version.</param>
	/// <param name="description">The description; <see langword="null"/> for none.</param>
	/// <param name="timeout">The deadline; <see cref="DefaultTimeout"/> when <see langword="null"/>.</param>
	/// <param name="observe">Hears each HTTP request of the operation.</param>
	/// <param name="cancellationToken">The caller's cancellation.</param>
	/// <returns>The version created.</returns>
	public Task<CamundaHubVersion> CreateVersionAsync(
		string fileKey, string name, string? description = null, TimeSpan? timeout = null, Action<CamundaCall>? observe = null,
		CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(fileKey);
		ArgumentException.ThrowIfNullOrWhiteSpace(name);
		return Executar((api, ct) => api.CreateVersion(fileKey, name, description, ct), timeout, observe, cancellationToken);
	}

	/// <summary>The versions of a file, every page of them.</summary>
	/// <param name="fileKey">The key of the file.</param>
	/// <param name="timeout">The deadline of the whole search; <see cref="DefaultTimeout"/> when <see langword="null"/>.</param>
	/// <param name="observe">Hears each HTTP request of the operation.</param>
	/// <param name="cancellationToken">The caller's cancellation.</param>
	/// <returns>The versions.</returns>
	public Task<IReadOnlyList<CamundaHubVersion>> SearchVersionsAsync(
		string fileKey, TimeSpan? timeout = null, Action<CamundaCall>? observe = null, CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(fileKey);
		return Executar((api, ct) => api.SearchVersions(fileKey, ct), timeout, observe, cancellationToken);
	}

	/// <summary>
	/// Restores a version: the file gets the content the version captured. The Hub itself records the restored state as a
	/// new version, unless the file and its latest version already have that content.
	/// </summary>
	/// <param name="versionKey">The key of the version.</param>
	/// <param name="timeout">The deadline; <see cref="DefaultTimeout"/> when <see langword="null"/>.</param>
	/// <param name="observe">Hears each HTTP request of the operation.</param>
	/// <param name="cancellationToken">The caller's cancellation.</param>
	/// <returns>The version the Hub answers with.</returns>
	public Task<CamundaHubVersion> RestoreVersionAsync(
		string versionKey, TimeSpan? timeout = null, Action<CamundaCall>? observe = null, CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(versionKey);
		return Executar((api, ct) => api.RestoreVersion(versionKey, ct), timeout, observe, cancellationToken);
	}

	// A busca do Hub pelo nome não garante a comparação exata (a v1 nem o documenta): o filtro de novo, ordinal.
	private static IReadOnlyList<T> Exatos<T>(IReadOnlyList<T> itens, Func<T, string> nome, string? exato) =>
		exato is null ? itens : itens.Where(i => string.Equals(nome(i), exato, StringComparison.Ordinal)).ToList();

	// A versão da API: a dada, ou a escolhida pela primeira operação, uma vez só para chamadas concorrentes. A escolha que
	// falhou não fica guardada; a que foi cancelada pelo prazo de outra chamada é refeita por esta.
	private async Task<IHubApi> Api(CancellationToken ct)
	{
		if (_dada is not null)
			return _dada;

		while (true)
		{
			Task<IHubApi> escolha;
			lock (_trava)
			{
				if (_escolha is null || _escolha.IsFaulted || _escolha.IsCanceled)
					_escolha = Escolher(ct);
				escolha = _escolha;
			}

			try
			{
				return await escolha.WaitAsync(ct).ConfigureAwait(false);
			}
			catch (OperationCanceledException) when (!ct.IsCancellationRequested && escolha.IsCanceled)
			{
				// A escolha era de outra chamada, que foi cancelada: esta escolhe de novo.
			}
		}
	}

	// O /api/v2/info com 200 é a v2; com 404, o /api/v1/info com 200 é a v1. O 401 e o 403 são a recusa da credencial, e
	// não um motivo para trocar de versão.
	private async Task<IHubApi> Escolher(CancellationToken ct)
	{
		if (await _hub.Pedir(HttpMethod.Get, "api/v2/info", null, ct, nuloNo404: true).ConfigureAwait(false) is not null)
			return new HubApiV2(_hub);
		if (await _hub.Pedir(HttpMethod.Get, "api/v1/info", null, ct, nuloNo404: true).ConfigureAwait(false) is not null)
			return new HubApiV1(_hub);
		throw new CamundaApiException(404, null, $"no Hub API at {_hub.Endereco}", "/api/v1/info");
	}

	/// <summary>
	/// Uma operação: o escopo que leva os avisos a quem chamou, o prazo ligado ao cancelamento de quem chamou, a versão da
	/// API, e a tradução do que o HTTP lança nas exceções do cliente.
	/// </summary>
	private async Task<T> Executar<T>(
		Func<IHubApi, CancellationToken, Task<T>> operacao, TimeSpan? timeout, Action<CamundaCall>? observe, CancellationToken cancellationToken)
	{
		var prazo = timeout ?? DefaultTimeout;
		var anterior = EscopoDaChamada.Atual;
		var escopo = new EscopoDaChamada(observe);
		EscopoDaChamada.Atual = escopo;
		using var limite = new CancellationTokenSource(prazo, _tempo);
		using var ligado = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, limite.Token);
		try
		{
			var api = await Api(ligado.Token).ConfigureAwait(false);
			return await operacao(api, ligado.Token).ConfigureAwait(false);
		}
		catch (Exception ex) when (Traduzir(ex, escopo, prazo, limite, cancellationToken) is { } traduzida)
		{
			throw traduzida;
		}
		finally
		{
			EscopoDaChamada.Atual = anterior;
		}
	}

	// A exceção do cliente para o que veio de baixo; nula para o que sobe como está (o cancelamento de quem chamou, e as
	// exceções do próprio cliente, que o ChamadasAoHub e a autenticação já lançam prontas).
	private Exception? Traduzir(Exception ex, EscopoDaChamada escopo, TimeSpan prazo, CancellationTokenSource limite, CancellationToken deQuemChamou)
	{
		var endereco = escopo.UltimoEndereco ?? _hub.Endereco;
		return ex switch
		{
			OperationCanceledException when deQuemChamou.IsCancellationRequested => null,
			OperationCanceledException when limite.IsCancellationRequested => escopo.PedindoToken
				? new CamundaUnavailableException(CamundaEndpoint.IdentityProvider, Autenticador.EnderecoDoToken, prazo, Prazo(prazo), ex)
				: new CamundaUnavailableException(CamundaEndpoint.Camunda, endereco, prazo, Prazo(prazo), ex),
			HttpRequestException falha => new CamundaUnavailableException(CamundaEndpoint.Camunda, endereco, null, falha.Message, falha),
			_ => null
		};
	}

	private static string Prazo(TimeSpan prazo) =>
		$"no answer in {prazo.TotalSeconds.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)} s";
}
