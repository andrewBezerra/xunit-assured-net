using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace XUnitAssured.Camunda.Client;

/// <summary>
/// Os nomes que mudam de uma versão da API do Hub para a outra: o prefixo dos caminhos, a chave de cada objeto e as que o
/// ligam ao de cima, e o último pedaço do caminho do restore.
/// </summary>
internal sealed record CamposDoHub(
	string Prefixo,
	string ChaveDoWorkspace,
	string ChaveDoProjeto,
	string WorkspaceDoProjeto,
	string ChaveDaPasta,
	string ProjetoDaPasta,
	string PastaMae,
	string ChaveDoArquivo,
	string ProjetoDoArquivo,
	string PastaDoArquivo,
	string ChaveDaVersao,
	string ArquivoDaVersao,
	string Restauracao);

/// <summary>
/// O que as duas versões da API do Hub fazem do mesmo jeito, com os nomes de cada uma (<see cref="CamposDoHub"/>): a
/// leitura das respostas, o conteúdo da pasta, o <c>PATCH</c> do arquivo e as versões. O que muda de forma (a busca
/// paginada, os corpos da criação, o <c>info</c>) fica em cada versão.
/// </summary>
internal abstract class HubApiBase
{
	protected HubApiBase(ChamadasAoHub hub, CamposDoHub campos)
	{
		Chamadas = hub;
		Campos = campos;
	}

	protected ChamadasAoHub Chamadas { get; }

	protected CamposDoHub Campos { get; }

	/// <summary>Todas as páginas da busca, do jeito de cada versão.</summary>
	protected abstract Task<List<JsonNode?>> Buscar(string caminho, JsonObject filtro, CancellationToken ct);

	public async Task<CamundaHubContent> GetFolderContent(string chave, CancellationToken ct) =>
		Conteudo((await Chamadas.Pedir(HttpMethod.Get, Caminho("folders/" + Hub.Chave(chave)), null, ct).ConfigureAwait(false))?["content"]);

	public async Task<CamundaHubFileMetadata> UpdateFile(string chave, string conteudo, int revisao, CancellationToken ct) =>
		Metadados(await Chamadas.Pedir(HttpMethod.Patch, Caminho("files/" + Hub.Chave(chave)), new JsonObject { ["content"] = conteudo, ["revision"] = revisao }, ct)
			.ConfigureAwait(false));

	// A versão é um snapshot do conteúdo atual do arquivo (o Swagger da v2; o pedido da v1 também não leva conteúdo).
	public async Task<CamundaHubVersion> CreateVersion(string arquivo, string nome, string? descricao, CancellationToken ct)
	{
		var corpo = new JsonObject { [Campos.ArquivoDaVersao] = arquivo, ["name"] = nome };
		if (descricao is not null)
			corpo["description"] = descricao;
		return VersaoLida(await Chamadas.Pedir(HttpMethod.Post, Caminho("versions"), corpo, ct).ConfigureAwait(false));
	}

	// A v2 exige o filtro pelo arquivo; a v1 o aceita.
	public async Task<IReadOnlyList<CamundaHubVersion>> SearchVersions(string arquivo, CancellationToken ct) =>
		(await Buscar(Caminho("versions/search"), Hub.Filtro((Campos.ArquivoDaVersao, arquivo)), ct).ConfigureAwait(false)).Select(VersaoLida).ToList();

	// O corpo é {}: o "version" só vale para os templates.
	public async Task<CamundaHubVersion> RestoreVersion(string versao, CancellationToken ct) =>
		VersaoLida(await Chamadas.Pedir(HttpMethod.Post, Caminho("versions/" + Hub.Chave(versao) + "/" + Campos.Restauracao), new JsonObject(), ct)
			.ConfigureAwait(false));

	/// <summary>O caminho da API desta versão: <c>api/v1/…</c> ou <c>api/v2/…</c>.</summary>
	protected string Caminho(string resto) => Campos.Prefixo + "/" + resto;

	protected CamundaHubWorkspace Workspace(JsonNode? json) => new(Hub.Obrigatorio(json, Campos.ChaveDoWorkspace), Hub.Texto(json?["name"]) ?? string.Empty);

	protected CamundaHubProject Projeto(JsonNode? json) =>
		new(Hub.Obrigatorio(json, Campos.ChaveDoProjeto), Hub.Texto(json?["name"]) ?? string.Empty, Hub.Texto(json?[Campos.WorkspaceDoProjeto]));

	protected CamundaHubFolder Pasta(JsonNode? json) => new(
		Hub.Obrigatorio(json, Campos.ChaveDaPasta), Hub.Texto(json?["name"]) ?? string.Empty, Hub.Texto(json?[Campos.ProjetoDaPasta]),
		Hub.Texto(json?[Campos.PastaMae]));

	protected CamundaHubFileMetadata Metadados(JsonNode? json) => new(
		Hub.Obrigatorio(json, Campos.ChaveDoArquivo), Hub.Texto(json?["name"]) ?? string.Empty, Hub.Texto(json?[Campos.ProjetoDoArquivo]),
		Hub.Texto(json?[Campos.PastaDoArquivo]), Hub.Tipo(json?["type"]), Hub.Inteiro(json?["revision"]) ?? 0);

	protected CamundaHubContent Conteudo(JsonNode? conteudo) =>
		new(Hub.Itens(conteudo?["folders"]).Select(Pasta).ToList(), Hub.Itens(conteudo?["files"]).Select(Metadados).ToList());

	// Uma versão do arquivo, como o Hub a devolve.
	private CamundaHubVersion VersaoLida(JsonNode? json) => new(
		Hub.Obrigatorio(json, Campos.ChaveDaVersao), Hub.Texto(json?["name"]) ?? string.Empty, Hub.Texto(json?["description"]),
		Hub.Texto(json?[Campos.ArquivoDaVersao]), Hub.Data(json?["created"]));
}
