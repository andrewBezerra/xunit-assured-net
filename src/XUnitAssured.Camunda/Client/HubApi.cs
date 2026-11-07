using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace XUnitAssured.Camunda.Client;

/// <summary>
/// Uma versão da API do Hub, atrás da interface do <see cref="CamundaHubClient"/> (spec do salvar-no-hub, E1): os
/// caminhos, os corpos e as páginas de cada uma, com os nomes da v2 (workspace, projeto, pasta, arquivo, versão).
/// </summary>
internal interface IHubApi
{
	CamundaHubApiVersion Versao { get; }

	Task<CamundaHubInfo> Info(CancellationToken ct);

	Task<IReadOnlyList<CamundaHubWorkspace>> SearchWorkspaces(string? nome, CancellationToken ct);

	Task<CamundaHubWorkspace> CreateWorkspace(string nome, CancellationToken ct);

	Task<CamundaHubWorkspaceDetail> GetWorkspace(string chave, CancellationToken ct);

	Task<IReadOnlyList<CamundaHubProject>> SearchProjects(string workspace, string? nome, CancellationToken ct);

	Task<CamundaHubProject> CreateProject(string workspace, string nome, CancellationToken ct);

	Task<CamundaHubContent> GetProjectContent(string chave, CancellationToken ct);

	Task<CamundaHubContent> GetFolderContent(string chave, CancellationToken ct);

	Task<CamundaHubFolder> CreateFolder(string projeto, string? pastaMae, string nome, CancellationToken ct);

	Task<IReadOnlyList<CamundaHubFileMetadata>> SearchFiles(string projeto, string? nome, CamundaHubFileType? tipo, CancellationToken ct);

	Task<CamundaHubFile?> GetFile(string chave, CancellationToken ct);

	Task<CamundaHubFileMetadata> CreateFile(string projeto, string? pasta, string nome, CamundaHubFileType tipo, string conteudo, CancellationToken ct);

	Task<CamundaHubFileMetadata> UpdateFile(string chave, string conteudo, int revisao, CancellationToken ct);
}

/// <summary>
/// O HTTP do Hub, comum às duas versões: o pedido com o corpo em JSON, e a resposta lida, ou traduzida nas exceções do
/// cliente. O token, a troca dele no 401/403 e o aviso de cada chamada ficam nos handlers embaixo do HttpClient.
/// </summary>
internal sealed class ChamadasAoHub
{
	// O corpo sem escapar o acento nem o XML: o Hub lê o JSON do mesmo jeito, e o log de quem olha o tráfego fica legível.
	private static readonly JsonSerializerOptions Corpo = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

	private readonly HttpClient _http;

	public ChamadasAoHub(HttpClient http, string endereco)
	{
		_http = http;
		Endereco = endereco;
	}

	/// <summary>O endereço do Hub, como cadastrado, sem a barra do fim.</summary>
	public string Endereco { get; }

	/// <summary>
	/// O pedido, e o JSON da resposta 2xx (um objeto vazio quando ela não tem corpo). Com <paramref name="nuloNo404"/>, o
	/// 404 é nulo; senão, é a recusa, como qualquer status fora de 2xx. O caminho é a partir do endereço do Hub, sem a
	/// barra do começo (<c>api/v2/info</c>): um endereço com caminho (<c>http://host/modeler</c>) fica na frente.
	/// </summary>
	public async Task<JsonNode?> Pedir(HttpMethod metodo, string caminho, JsonNode? corpo, CancellationToken ct, bool nuloNo404 = false)
	{
		using var pedido = new HttpRequestMessage(metodo, caminho);
		pedido.Headers.Accept.ParseAdd("application/json");
		if (corpo is not null)
			pedido.Content = new StringContent(corpo.ToJsonString(Corpo), Encoding.UTF8, "application/json");

		using var resposta = await _http.SendAsync(pedido, ct).ConfigureAwait(false);
		var texto = await resposta.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
		var status = (int)resposta.StatusCode;
		if (status is >= 200 and <= 299)
			return string.IsNullOrWhiteSpace(texto) ? new JsonObject() : JsonNode.Parse(texto);
		if (resposta.StatusCode == HttpStatusCode.NotFound && nuloNo404)
			return null;

		var endereco = (resposta.RequestMessage?.RequestUri ?? pedido.RequestUri!).GetLeftPart(UriPartial.Path);
		if (resposta.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
			throw new CamundaCredentialException(CamundaCredentialRefusal.Camunda, status, null, endereco);
		var problema = Ler(texto);
		throw new CamundaApiException(status, Hub.Texto(problema?["title"]), Hub.Texto(problema?["detail"]), "/" + caminho.Split('?')[0]);
	}

	// O documento do problema (a RFC 9457 da v2, o JSON do Hub ou do Spring da v1); nulo quando a resposta não é JSON.
	private static JsonObject? Ler(string texto)
	{
		try
		{
			return JsonNode.Parse(texto) as JsonObject;
		}
		catch (JsonException)
		{
			return null;
		}
	}
}

/// <summary>A leitura do JSON do Hub, comum às duas versões.</summary>
internal static class Hub
{
	public static string? Texto(JsonNode? valor) =>
		valor is JsonValue v && v.TryGetValue<string>(out var texto) && !string.IsNullOrWhiteSpace(texto) ? texto : null;

	public static bool? Booleano(JsonNode? valor) => valor is JsonValue v && v.TryGetValue<bool>(out var b) ? b : null;

	public static int? Inteiro(JsonNode? valor) => valor is JsonValue v && v.TryGetValue<int>(out var i) ? i : null;

	public static long? Longo(JsonNode? valor) => valor is JsonValue v && v.TryGetValue<long>(out var l) ? l : null;

	public static DateTimeOffset? Data(JsonNode? valor) =>
		Texto(valor) is { } texto && DateTimeOffset.TryParse(texto, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var data)
			? data
			: null;

	/// <summary>O campo que a resposta tem de trazer; sem ele, a resposta não é a do Hub.</summary>
	public static string Obrigatorio(JsonNode? json, string campo) =>
		Texto(json?[campo]) ?? throw new InvalidOperationException($"The Hub answered without the {campo}.");

	/// <summary>Os itens de uma lista do JSON; vazia quando ela não veio.</summary>
	public static List<JsonNode?> Itens(JsonNode? lista) => lista is JsonArray itens ? itens.ToList() : new List<JsonNode?>();

	/// <summary>O filtro de uma busca, só com os campos dados.</summary>
	public static JsonObject Filtro(params (string Campo, string? Valor)[] campos)
	{
		var filtro = new JsonObject();
		foreach (var (campo, valor) in campos)
			if (valor is not null)
				filtro[campo] = valor;
		return filtro;
	}

	/// <summary>O tipo do arquivo, sem diferenciar maiúsculas (a v1 o diz em maiúsculas); nulo para um que o cliente não trata.</summary>
	public static CamundaHubFileType? Tipo(JsonNode? valor) => Texto(valor)?.ToUpperInvariant() switch
	{
		"BPMN" => CamundaHubFileType.Bpmn,
		"FORM" => CamundaHubFileType.Form,
		_ => null
	};

	/// <summary>O tipo como as duas versões o pedem: em minúsculas.</summary>
	public static string? Tipo(CamundaHubFileType? tipo) => tipo switch
	{
		CamundaHubFileType.Bpmn => "bpmn",
		CamundaHubFileType.Form => "form",
		_ => null
	};

	/// <summary>A chave no caminho, escapada.</summary>
	public static string Chave(string chave)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(chave);
		return Uri.EscapeDataString(chave);
	}
}
