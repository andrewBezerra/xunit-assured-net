using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace XUnitAssured.Camunda.Client;

/// <summary>A Camunda Hub API (<c>/api/v2</c>), do Camunda 8.10 em diante: os nomes da tela, e os erros da RFC 9457.</summary>
internal sealed class HubApiV2 : IHubApi
{
	/// <summary>Quantos itens cada página da busca pede (o padrão da v2; ela aceita até 10000).</summary>
	internal const int Limite = 100;

	private readonly ChamadasAoHub _hub;

	public HubApiV2(ChamadasAoHub hub) => _hub = hub;

	public CamundaHubApiVersion Versao => CamundaHubApiVersion.V2;

	// A v2 não diz as permissões do client.
	public async Task<CamundaHubInfo> Info(CancellationToken ct)
	{
		var info = await _hub.Pedir(HttpMethod.Get, "api/v2/info", null, ct).ConfigureAwait(false);
		return new CamundaHubInfo(Versao, Hub.Texto(info?["authorizedOrganization"]), null, null, null, null);
	}

	public async Task<IReadOnlyList<CamundaHubWorkspace>> SearchWorkspaces(string? nome, CancellationToken ct) =>
		(await Buscar("api/v2/workspaces/search", Hub.Filtro(("name", nome)), ct).ConfigureAwait(false)).Select(Workspace).ToList();

	public async Task<CamundaHubWorkspace> CreateWorkspace(string nome, CancellationToken ct) =>
		Workspace(await _hub.Pedir(HttpMethod.Post, "api/v2/workspaces", new JsonObject { ["name"] = nome }, ct).ConfigureAwait(false));

	public async Task<CamundaHubWorkspaceDetail> GetWorkspace(string chave, CancellationToken ct)
	{
		var detalhe = await _hub.Pedir(HttpMethod.Get, "api/v2/workspaces/" + Hub.Chave(chave), null, ct).ConfigureAwait(false);
		return new CamundaHubWorkspaceDetail(Workspace(detalhe?["workspace"]), Hub.Itens(detalhe?["content"]?["projects"]).Select(Projeto).ToList());
	}

	public async Task<IReadOnlyList<CamundaHubProject>> SearchProjects(string workspace, string? nome, CancellationToken ct) =>
		(await Buscar("api/v2/projects/search", Hub.Filtro(("workspaceKey", workspace), ("name", nome)), ct).ConfigureAwait(false))
			.Select(Projeto).ToList();

	public async Task<CamundaHubProject> CreateProject(string workspace, string nome, CancellationToken ct) =>
		Projeto(await _hub.Pedir(HttpMethod.Post, "api/v2/projects", new JsonObject { ["name"] = nome, ["workspaceKey"] = workspace }, ct)
			.ConfigureAwait(false));

	public async Task<CamundaHubContent> GetProjectContent(string chave, CancellationToken ct) =>
		Conteudo((await _hub.Pedir(HttpMethod.Get, "api/v2/projects/" + Hub.Chave(chave), null, ct).ConfigureAwait(false))?["content"]);

	public async Task<CamundaHubContent> GetFolderContent(string chave, CancellationToken ct) =>
		Conteudo((await _hub.Pedir(HttpMethod.Get, "api/v2/folders/" + Hub.Chave(chave), null, ct).ConfigureAwait(false))?["content"]);

	public async Task<CamundaHubFolder> CreateFolder(string projeto, string? pastaMae, string nome, CancellationToken ct)
	{
		var corpo = new JsonObject { ["name"] = nome, ["projectKey"] = projeto };
		if (pastaMae is not null)
			corpo["parentFolderKey"] = pastaMae;
		return Pasta(await _hub.Pedir(HttpMethod.Post, "api/v2/folders", corpo, ct).ConfigureAwait(false));
	}

	public async Task<IReadOnlyList<CamundaHubFileMetadata>> SearchFiles(string projeto, string? nome, CamundaHubFileType? tipo, CancellationToken ct) =>
		(await Buscar("api/v2/files/search", Hub.Filtro(("projectKey", projeto), ("name", nome), ("type", Hub.Tipo(tipo))), ct).ConfigureAwait(false))
			.Select(Metadados).ToList();

	// O arquivo da v2 traz os metadados e o conteúdo no mesmo objeto.
	public async Task<CamundaHubFile?> GetFile(string chave, CancellationToken ct)
	{
		var arquivo = await _hub.Pedir(HttpMethod.Get, "api/v2/files/" + Hub.Chave(chave), null, ct, nuloNo404: true).ConfigureAwait(false);
		return arquivo is null ? null : new CamundaHubFile(Metadados(arquivo), Hub.Texto(arquivo["content"]) ?? string.Empty);
	}

	public async Task<CamundaHubFileMetadata> CreateFile(string projeto, string? pasta, string nome, CamundaHubFileType tipo, string conteudo, CancellationToken ct)
	{
		var corpo = new JsonObject { ["name"] = nome, ["projectKey"] = projeto, ["type"] = Hub.Tipo(tipo), ["content"] = conteudo };
		if (pasta is not null)
			corpo["folderKey"] = pasta;
		return Metadados(await _hub.Pedir(HttpMethod.Post, "api/v2/files", corpo, ct).ConfigureAwait(false));
	}

	public async Task<CamundaHubFileMetadata> UpdateFile(string chave, string conteudo, int revisao, CancellationToken ct) =>
		Metadados(await _hub.Pedir(HttpMethod.Patch, "api/v2/files/" + Hub.Chave(chave), new JsonObject { ["content"] = conteudo, ["revision"] = revisao }, ct)
			.ConfigureAwait(false));

	// Todas as páginas: o from avança pelo que veio, até o totalItems (ou enquanto o Hub diz que há mais).
	private async Task<List<JsonNode?>> Buscar(string caminho, JsonObject filtro, CancellationToken ct)
	{
		var todos = new List<JsonNode?>();
		while (true)
		{
			var corpo = new JsonObject { ["filter"] = filtro.DeepClone(), ["page"] = new JsonObject { ["from"] = todos.Count, ["limit"] = Limite } };
			var pagina = await _hub.Pedir(HttpMethod.Post, caminho, corpo, ct).ConfigureAwait(false);
			var itens = Hub.Itens(pagina?["items"]);
			todos.AddRange(itens);
			var total = Hub.Longo(pagina?["page"]?["totalItems"]) ?? 0;
			var haMais = Hub.Booleano(pagina?["page"]?["hasMoreTotalItems"]) ?? false;
			if (itens.Count == 0 || (todos.Count >= total && !haMais))
				return todos;
		}
	}

	private static CamundaHubWorkspace Workspace(JsonNode? json) => new(Hub.Obrigatorio(json, "workspaceKey"), Hub.Texto(json?["name"]) ?? string.Empty);

	private static CamundaHubProject Projeto(JsonNode? json) =>
		new(Hub.Obrigatorio(json, "projectKey"), Hub.Texto(json?["name"]) ?? string.Empty, Hub.Texto(json?["workspaceKey"]));

	private static CamundaHubFolder Pasta(JsonNode? json) => new(
		Hub.Obrigatorio(json, "folderKey"), Hub.Texto(json?["name"]) ?? string.Empty, Hub.Texto(json?["projectKey"]), Hub.Texto(json?["parentFolderKey"]));

	private static CamundaHubFileMetadata Metadados(JsonNode? json) => new(
		Hub.Obrigatorio(json, "fileKey"), Hub.Texto(json?["name"]) ?? string.Empty, Hub.Texto(json?["projectKey"]), Hub.Texto(json?["folderKey"]),
		Hub.Tipo(json?["type"]), Hub.Inteiro(json?["revision"]) ?? 0);

	private static CamundaHubContent Conteudo(JsonNode? conteudo) =>
		new(Hub.Itens(conteudo?["folders"]).Select(Pasta).ToList(), Hub.Itens(conteudo?["files"]).Select(Metadados).ToList());
}
