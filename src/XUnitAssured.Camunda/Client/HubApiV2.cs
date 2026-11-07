using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace XUnitAssured.Camunda.Client;

/// <summary>A Camunda Hub API (<c>/api/v2</c>), do Camunda 8.10 em diante: os nomes da tela, e os erros da RFC 9457.</summary>
internal sealed class HubApiV2 : HubApiBase, IHubApi
{
	/// <summary>Quantos itens cada página da busca pede (o padrão da v2; ela aceita até 10000).</summary>
	internal const int Limite = 100;

	public HubApiV2(ChamadasAoHub hub) : base(hub, new CamposDoHub("api/v2", "workspaceKey", "projectKey", "workspaceKey", "folderKey", "projectKey", "parentFolderKey", "fileKey", "projectKey", "folderKey", "versionKey", "fileKey", "restoration"))
	{
	}

	public CamundaHubApiVersion Versao => CamundaHubApiVersion.V2;

	// A v2 não diz as permissões do client.
	public async Task<CamundaHubInfo> Info(CancellationToken ct)
	{
		var info = await Chamadas.Pedir(HttpMethod.Get, "api/v2/info", null, ct).ConfigureAwait(false);
		return new CamundaHubInfo(Versao, Hub.Texto(info?["authorizedOrganization"]), null, null, null, null);
	}

	public async Task<IReadOnlyList<CamundaHubWorkspace>> SearchWorkspaces(string? nome, CancellationToken ct) =>
		(await Buscar("api/v2/workspaces/search", Hub.Filtro(("name", nome)), ct).ConfigureAwait(false)).Select(Workspace).ToList();

	public async Task<CamundaHubWorkspace> CreateWorkspace(string nome, CancellationToken ct) =>
		Workspace(await Chamadas.Pedir(HttpMethod.Post, "api/v2/workspaces", new JsonObject { ["name"] = nome }, ct).ConfigureAwait(false));

	public async Task<CamundaHubWorkspaceDetail> GetWorkspace(string chave, CancellationToken ct)
	{
		var detalhe = await Chamadas.Pedir(HttpMethod.Get, "api/v2/workspaces/" + Hub.Chave(chave), null, ct).ConfigureAwait(false);
		return new CamundaHubWorkspaceDetail(Workspace(detalhe?["workspace"]), Hub.Itens(detalhe?["content"]?["projects"]).Select(Projeto).ToList());
	}

	public async Task<IReadOnlyList<CamundaHubProject>> SearchProjects(string workspace, string? nome, CancellationToken ct) =>
		(await Buscar("api/v2/projects/search", Hub.Filtro(("workspaceKey", workspace), ("name", nome)), ct).ConfigureAwait(false))
			.Select(Projeto).ToList();

	public async Task<CamundaHubProject> CreateProject(string workspace, string nome, CancellationToken ct) =>
		Projeto(await Chamadas.Pedir(HttpMethod.Post, "api/v2/projects", new JsonObject { ["name"] = nome, ["workspaceKey"] = workspace }, ct)
			.ConfigureAwait(false));

	public async Task<CamundaHubContent> GetProjectContent(string chave, CancellationToken ct) =>
		Conteudo((await Chamadas.Pedir(HttpMethod.Get, "api/v2/projects/" + Hub.Chave(chave), null, ct).ConfigureAwait(false))?["content"]);

	public async Task<CamundaHubFolder> CreateFolder(string projeto, string? pastaMae, string nome, CancellationToken ct)
	{
		var corpo = new JsonObject { ["name"] = nome, ["projectKey"] = projeto };
		if (pastaMae is not null)
			corpo["parentFolderKey"] = pastaMae;
		return Pasta(await Chamadas.Pedir(HttpMethod.Post, "api/v2/folders", corpo, ct).ConfigureAwait(false));
	}

	public async Task<IReadOnlyList<CamundaHubFileMetadata>> SearchFiles(string projeto, string? nome, CamundaHubFileType? tipo, CancellationToken ct) =>
		(await Buscar("api/v2/files/search", Hub.Filtro(("projectKey", projeto), ("name", nome), ("type", Hub.Tipo(tipo))), ct).ConfigureAwait(false))
			.Select(Metadados).ToList();

	// O arquivo da v2 traz os metadados e o conteúdo no mesmo objeto.
	public async Task<CamundaHubFile?> GetFile(string chave, CancellationToken ct)
	{
		var arquivo = await Chamadas.Pedir(HttpMethod.Get, "api/v2/files/" + Hub.Chave(chave), null, ct, nuloNo404: true).ConfigureAwait(false);
		return arquivo is null ? null : new CamundaHubFile(Metadados(arquivo), Hub.Texto(arquivo["content"]) ?? string.Empty);
	}

	public async Task<CamundaHubFileMetadata> CreateFile(string projeto, string? pasta, string nome, CamundaHubFileType tipo, string conteudo, CancellationToken ct)
	{
		var corpo = new JsonObject { ["name"] = nome, ["projectKey"] = projeto, ["type"] = Hub.Tipo(tipo), ["content"] = conteudo };
		if (pasta is not null)
			corpo["folderKey"] = pasta;
		return Metadados(await Chamadas.Pedir(HttpMethod.Post, "api/v2/files", corpo, ct).ConfigureAwait(false));
	}

	// Todas as páginas: o from avança pelo que veio, até o totalItems (ou enquanto o Hub diz que há mais).
	protected override async Task<List<JsonNode?>> Buscar(string caminho, JsonObject filtro, CancellationToken ct)
	{
		var todos = new List<JsonNode?>();
		while (true)
		{
			var corpo = new JsonObject { ["filter"] = filtro.DeepClone(), ["page"] = new JsonObject { ["from"] = todos.Count, ["limit"] = Limite } };
			var pagina = await Chamadas.Pedir(HttpMethod.Post, caminho, corpo, ct).ConfigureAwait(false);
			var itens = Hub.Itens(pagina?["items"]);
			todos.AddRange(itens);
			var total = Hub.Longo(pagina?["page"]?["totalItems"]) ?? 0;
			var haMais = Hub.Booleano(pagina?["page"]?["hasMoreTotalItems"]) ?? false;
			if (itens.Count == 0 || (todos.Count >= total && !haMais))
				return todos;
		}
	}
}
