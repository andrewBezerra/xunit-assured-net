using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace XUnitAssured.Camunda.Client;

/// <summary>
/// A API do Web Modeler (<c>/api/v1</c>), obsoleta no Camunda 8.10 e removida no 8.12, com os nomes de antes do 8.10: o
/// "project" dela é o workspace, e a "process application", o projeto. As chaves são as mesmas da v2.
/// </summary>
internal sealed class HubApiV1 : HubApiBase, IHubApi
{
	/// <summary>Quantos itens cada página da busca pede: o máximo da v1.</summary>
	internal const int Tamanho = 50;

	public HubApiV1(ChamadasAoHub hub) : base(hub, new CamposDoHub("api/v1", "id", "id", "projectId", "id", "processApplicationId", "parentId", "id", "processApplicationId", "folderId", "id", "fileId", "restore"))
	{
	}

	public CamundaHubApiVersion Versao => CamundaHubApiVersion.V1;

	public async Task<CamundaHubInfo> Info(CancellationToken ct)
	{
		var info = await Chamadas.Pedir(HttpMethod.Get, "api/v1/info", null, ct).ConfigureAwait(false);
		return new CamundaHubInfo(Versao, Hub.Texto(info?["authorizedOrganization"]),
			Hub.Booleano(info?["createPermission"]), Hub.Booleano(info?["readPermission"]),
			Hub.Booleano(info?["updatePermission"]), Hub.Booleano(info?["deletePermission"]));
	}

	// O workspace é o "project" da v1.
	public async Task<IReadOnlyList<CamundaHubWorkspace>> SearchWorkspaces(string? nome, CancellationToken ct) =>
		(await Buscar("api/v1/projects/search", Hub.Filtro(("name", nome)), ct).ConfigureAwait(false)).Select(Workspace).ToList();

	public async Task<CamundaHubWorkspace> CreateWorkspace(string nome, CancellationToken ct) =>
		Workspace(await Chamadas.Pedir(HttpMethod.Post, "api/v1/projects", new JsonObject { ["name"] = nome }, ct).ConfigureAwait(false));

	public async Task<CamundaHubWorkspaceDetail> GetWorkspace(string chave, CancellationToken ct)
	{
		var detalhe = await Chamadas.Pedir(HttpMethod.Get, "api/v1/projects/" + Hub.Chave(chave), null, ct).ConfigureAwait(false);
		return new CamundaHubWorkspaceDetail(Workspace(detalhe?["metadata"]),
			Hub.Itens(detalhe?["content"]?["processApplications"]).Select(Projeto).ToList());
	}

	// A v1 não busca process applications: elas vêm no "project" (o workspace), todas de uma vez.
	public async Task<IReadOnlyList<CamundaHubProject>> SearchProjects(string workspace, string? nome, CancellationToken ct) =>
		(await GetWorkspace(workspace, ct).ConfigureAwait(false)).Projects;

	public async Task<CamundaHubProject> CreateProject(string workspace, string nome, CancellationToken ct) =>
		Projeto(await Chamadas.Pedir(HttpMethod.Post, "api/v1/process-applications", new JsonObject { ["name"] = nome, ["projectId"] = workspace }, ct)
			.ConfigureAwait(false));

	public async Task<CamundaHubContent> GetProjectContent(string chave, CancellationToken ct) =>
		Conteudo((await Chamadas.Pedir(HttpMethod.Get, "api/v1/process-applications/" + Hub.Chave(chave), null, ct).ConfigureAwait(false))?["content"]);

	// Na raiz, a process application; numa pasta, só a pasta mãe: as duas são exclusivas na v1.
	public async Task<CamundaHubFolder> CreateFolder(string projeto, string? pastaMae, string nome, CancellationToken ct)
	{
		var corpo = new JsonObject { ["name"] = nome };
		if (pastaMae is null)
			corpo["processApplicationId"] = projeto;
		else
			corpo["parentId"] = pastaMae;
		return Pasta(await Chamadas.Pedir(HttpMethod.Post, "api/v1/folders", corpo, ct).ConfigureAwait(false));
	}

	// O tipo não vai no filtro: a v1 o devolve em maiúsculas e não documenta como o compara; o cliente filtra do lado de cá.
	public async Task<IReadOnlyList<CamundaHubFileMetadata>> SearchFiles(string projeto, string? nome, CamundaHubFileType? tipo, CancellationToken ct) =>
		(await Buscar("api/v1/files/search", Hub.Filtro(("processApplicationId", projeto), ("name", nome)), ct).ConfigureAwait(false))
			.Select(Metadados).ToList();

	public async Task<CamundaHubFile?> GetFile(string chave, CancellationToken ct)
	{
		var arquivo = await Chamadas.Pedir(HttpMethod.Get, "api/v1/files/" + Hub.Chave(chave), null, ct, nuloNo404: true).ConfigureAwait(false);
		return arquivo is null ? null : new CamundaHubFile(Metadados(arquivo["metadata"]), Hub.Texto(arquivo["content"]) ?? string.Empty);
	}

	// Na raiz, a process application; numa pasta, só a pasta: as duas são exclusivas na v1. Só com o projectId (o
	// workspace), o Hub criaria o arquivo numa process application "… - General" que ele mesmo cria.
	public async Task<CamundaHubFileMetadata> CreateFile(string projeto, string? pasta, string nome, CamundaHubFileType tipo, string conteudo, CancellationToken ct)
	{
		var corpo = new JsonObject { ["name"] = nome, ["content"] = conteudo, ["fileType"] = Hub.Tipo(tipo) };
		if (pasta is null)
			corpo["processApplicationId"] = projeto;
		else
			corpo["folderId"] = pasta;
		return Metadados(await Chamadas.Pedir(HttpMethod.Post, "api/v1/files", corpo, ct).ConfigureAwait(false));
	}

	// Todas as páginas: o número da página, desde 0, até o total.
	protected override async Task<List<JsonNode?>> Buscar(string caminho, JsonObject filtro, CancellationToken ct)
	{
		var todos = new List<JsonNode?>();
		for (var numero = 0; ; numero++)
		{
			var corpo = new JsonObject { ["filter"] = filtro.DeepClone(), ["page"] = numero, ["size"] = Tamanho };
			var pagina = await Chamadas.Pedir(HttpMethod.Post, caminho, corpo, ct).ConfigureAwait(false);
			var itens = Hub.Itens(pagina?["items"]);
			todos.AddRange(itens);
			if (itens.Count == 0 || todos.Count >= (Hub.Longo(pagina?["total"]) ?? 0))
				return todos;
		}
	}
}
