using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

using Microsoft.Extensions.Time.Testing;

using Shouldly;
using Xunit;

using XUnitAssured.Camunda;
using XUnitAssured.Camunda.Client;

namespace XUnitAssured.Tests.CamundaTests;

/// <summary>
/// O cliente do Hub do Camunda (o Web Modeler), sem Hub: a <see cref="ApiFalsa"/> responde como o Hub 8.10, nas duas
/// versões da API, e como o provedor de identidade; o <see cref="FakeTimeProvider"/> conta os prazos, sem esperar de
/// verdade.
/// </summary>
[Trait("Category", "Camunda")]
[Trait("Component", "HubClient")]
public class CamundaHubClientTests
{
	internal const string Token = "/realms/camunda/protocol/openid-connect/token";
	private const string Hub = "http://hub.falso:8070";
	private const string InfoV1 = """
		{ "version": "v1", "authorizedOrganization": "org-1", "createPermission": true, "readPermission": true,
		  "updatePermission": false, "deletePermission": false }
		""";
	private const string InfoV2 = """{ "version": "v2", "authorizedOrganization": "org-2" }""";

	// ---------- 1. Só OAuth ----------

	[Fact(DisplayName = "The token is asked with the client credentials and the default audience, and sent as Bearer")]
	public async Task Token_Default_Audience()
	{
		var api = Api().Responder(HttpMethod.Get, "/api/v2/info", InfoV2);

		await Cliente(api, CamundaHubApiVersion.V2).GetInfoAsync();

		var pedido = api.A(Token).ShouldHaveSingleItem();
		pedido.Host.ShouldBe("idp.falso");
		pedido.Corpo!.ShouldContain("grant_type=client_credentials");
		pedido.Corpo!.ShouldContain("client_id=qa-hub");
		pedido.Corpo!.ShouldContain("audience=" + CamundaHubClient.DefaultAudience);
		pedido.Corpo!.ShouldNotContain("scope=");
		api.A("/api/v2/info").ShouldHaveSingleItem().Autorizacao.ShouldBe("Bearer t1");
	}

	[Fact(DisplayName = "The audience and the scope given go to the token request")]
	public async Task Token_Given_Audience()
	{
		var api = Api().Responder(HttpMethod.Get, "/api/v2/info", InfoV2);
		var auth = OAuth();
		auth.Audience = "outra-api";
		auth.Scope = "openid";

		await Cliente(api, CamundaHubApiVersion.V2, auth: auth).GetInfoAsync();

		var corpo = api.A(Token).ShouldHaveSingleItem().Corpo!;
		corpo.ShouldContain("audience=outra-api");
		corpo.ShouldContain("scope=openid");
	}

	[Theory(DisplayName = "A null type, or OAuth in any case, is OAuth")]
	[InlineData(null)]
	[InlineData("OAuth")]
	[InlineData("oauth")]
	public async Task Null_Type_Is_OAuth(string? tipo)
	{
		var api = Api().Responder(HttpMethod.Get, "/api/v2/info", InfoV2);
		var auth = OAuth();
		auth.Type = tipo;

		await Cliente(api, CamundaHubApiVersion.V2, auth: auth).GetInfoAsync();

		api.A("/api/v2/info").ShouldHaveSingleItem().Autorizacao.ShouldBe("Bearer t1");
	}

	[Theory(DisplayName = "For refuses a type that is not OAuth, naming it")]
	[InlineData("None")]
	[InlineData("Basic")]
	[InlineData("Kerberos")]
	public void For_Refuses_Other_Types(string tipo)
	{
		var auth = OAuth();
		auth.Type = tipo;

		var recusa = Should.Throw<InvalidOperationException>(() => CamundaHubClient.For(Config(auth)));

		recusa.Message.ShouldContain(tipo);
		recusa.Message.ShouldContain("OAuth");
	}

	[Theory(DisplayName = "For refuses a missing client id, client secret or token address, naming the field and not the secret")]
	[InlineData("clientId")]
	[InlineData("clientSecret")]
	[InlineData("oauthUrl")]
	public void For_Refuses_Missing_Field(string campo)
	{
		var auth = OAuth();
		switch (campo)
		{
			case "clientId": auth.ClientId = " "; break;
			case "clientSecret": auth.ClientSecret = null; break;
			default: auth.OAuthUrl = ""; break;
		}

		var recusa = Should.Throw<InvalidOperationException>(() => CamundaHubClient.For(Config(auth)));

		recusa.Message.ShouldContain("authentication." + campo);
		recusa.Message.ShouldNotContain("s3cr3t-hub");
	}

	[Fact(DisplayName = "For returns the same client for the same settings, and another for another secret or API version")]
	public void For_Shares_By_Settings()
	{
		static CamundaHubSettings Com(string secret, CamundaHubApiVersion? versao)
		{
			var auth = OAuth();
			auth.ClientSecret = secret;
			return new CamundaHubSettings { RestAddress = "http://hub.compartilhado.invalid:8070", Authentication = auth, ApiVersion = versao };
		}

		var um = CamundaHubClient.For(Com("s1", null));

		CamundaHubClient.For(Com("s1", null)).ShouldBeSameAs(um);
		CamundaHubClient.For(Com("s2", null)).ShouldNotBeSameAs(um);
		CamundaHubClient.For(Com("s1", CamundaHubApiVersion.V1)).ShouldNotBeSameAs(um);
	}

	[Fact(DisplayName = "The settings key carries a hash of the secret, never the secret")]
	public void Key_Has_No_Secret()
	{
		var chave = CamundaHubClient.Key(Config(OAuth()));

		chave.ShouldNotContain("s3cr3t-hub");
		chave.ShouldContain("qa-hub");
	}

	// ---------- A escolha da versão ----------

	[Fact(DisplayName = "A Hub that answers the v2 info speaks the v2")]
	public async Task Choice_V2()
	{
		var api = Api().Responder(HttpMethod.Get, "/api/v2/info", InfoV2);

		var info = await Cliente(api).GetInfoAsync();

		info.ShouldBe(new CamundaHubInfo(CamundaHubApiVersion.V2, "org-2", null, null, null, null));
		api.A("/api/v1/info").ShouldBeEmpty();
	}

	[Fact(DisplayName = "A Hub without the v2 (404) and with the v1 speaks the v1, with the permissions")]
	public async Task Choice_V1_After_404()
	{
		var api = Api().Responder(HttpMethod.Get, "/api/v1/info", InfoV1);

		var info = await Cliente(api).GetInfoAsync();

		info.ShouldBe(new CamundaHubInfo(CamundaHubApiVersion.V1, "org-1", true, true, false, false));
		api.A("/api/v2/info").Count.ShouldBe(1);
	}

	[Fact(DisplayName = "A Hub without either API (404 on both) is a refusal 404")]
	public async Task Choice_Neither()
	{
		var api = Api();

		var recusa = await Should.ThrowAsync<CamundaApiException>(() => Cliente(api).GetInfoAsync());

		recusa.Status.ShouldBe(404);
		recusa.Message.ShouldContain("no Hub API at " + Hub);
	}

	[Fact(DisplayName = "A 401 on the v2 info is the refusal of the credentials, not a reason to try the v1")]
	public async Task Choice_401_Does_Not_Switch()
	{
		var api = Api().Responder(HttpMethod.Get, "/api/v2/info", "{}", HttpStatusCode.Unauthorized).Responder(HttpMethod.Get, "/api/v1/info", InfoV1);

		var recusa = await Should.ThrowAsync<CamundaCredentialException>(() => Cliente(api).GetInfoAsync());

		(recusa.Refusal, recusa.Status).ShouldBe((CamundaCredentialRefusal.Camunda, (int?)401));
		api.A("/api/v1/info").ShouldBeEmpty();
	}

	[Fact(DisplayName = "Twenty calls at once at the start choose the version once")]
	public async Task Choice_Once_For_Concurrent_Calls()
	{
		var api = Api().Responder(HttpMethod.Get, "/api/v1/info", InfoV1);
		var cliente = Cliente(api);

		var infos = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => cliente.GetInfoAsync()));

		infos.ShouldAllBe(i => i.ApiVersion == CamundaHubApiVersion.V1);
		api.A("/api/v2/info").Count.ShouldBe(1);
		api.A("/api/v1/info").Count.ShouldBe(21);
	}

	[Theory(DisplayName = "An API version given skips the choice")]
	[InlineData(CamundaHubApiVersion.V1, "/api/v1/info", "/api/v2/info")]
	[InlineData(CamundaHubApiVersion.V2, "/api/v2/info", "/api/v1/info")]
	public async Task Version_Given_Skips_Choice(CamundaHubApiVersion versao, string usado, string outro)
	{
		var api = Api().Responder(HttpMethod.Get, "/api/v1/info", InfoV1).Responder(HttpMethod.Get, "/api/v2/info", InfoV2);

		(await Cliente(api, versao).GetInfoAsync()).ApiVersion.ShouldBe(versao);

		api.A(usado).Count.ShouldBe(1);
		api.A(outro).ShouldBeEmpty();
	}

	[Fact(DisplayName = "A choice that failed is not kept: the next call chooses again")]
	public async Task Choice_Failure_Not_Kept()
	{
		var api = Api().SemResposta(HttpMethod.Get, "/api/v2/info").Responder(HttpMethod.Get, "/api/v2/info", InfoV2);
		var cliente = Cliente(api);

		await Should.ThrowAsync<CamundaUnavailableException>(() => cliente.GetInfoAsync());
		(await cliente.GetInfoAsync()).ApiVersion.ShouldBe(CamundaHubApiVersion.V2);
	}

	// ---------- 2. A navegação ----------

	[Theory(DisplayName = "The workspace search follows three pages and keeps only the exact name")]
	[InlineData(CamundaHubApiVersion.V1)]
	[InlineData(CamundaHubApiVersion.V2)]
	public async Task Search_Workspaces_Pages(CamundaHubApiVersion versao)
	{
		var caminho = versao == CamundaHubApiVersion.V1 ? "/api/v1/projects/search" : "/api/v2/workspaces/search";
		var api = Paginas(Api(), versao, caminho, (k, nome) => Workspace(versao, k, nome), ("minusc", "alvo"), ("certo", "Alvo"));

		var achados = await Cliente(api, versao).SearchWorkspacesAsync("Alvo");

		achados.ShouldHaveSingleItem().ShouldBe(new CamundaHubWorkspace("certo", "Alvo"));
		ConferirPaginas(api.A(caminho), versao);
		api.A(caminho).ShouldAllBe(p => p.Json["filter"]!["name"]!.GetValue<string>() == "Alvo");
	}

	[Theory(DisplayName = "The workspace search without a name returns every workspace of every page")]
	[InlineData(CamundaHubApiVersion.V1)]
	[InlineData(CamundaHubApiVersion.V2)]
	public async Task Search_Workspaces_All(CamundaHubApiVersion versao)
	{
		var caminho = versao == CamundaHubApiVersion.V1 ? "/api/v1/projects/search" : "/api/v2/workspaces/search";
		var api = Paginas(Api(), versao, caminho, (k, nome) => Workspace(versao, k, nome), ("minusc", "alvo"), ("certo", "Alvo"));

		var todos = await Cliente(api, versao).SearchWorkspacesAsync(null);

		todos.Count.ShouldBe(2 * Tamanho(versao) + 20);
		api.A(caminho)[0].Json["filter"]?["name"].ShouldBeNull();
	}

	[Theory(DisplayName = "Creating a workspace posts its name")]
	[InlineData(CamundaHubApiVersion.V1, "/api/v1/projects")]
	[InlineData(CamundaHubApiVersion.V2, "/api/v2/workspaces")]
	public async Task Create_Workspace(CamundaHubApiVersion versao, string caminho)
	{
		var api = Api().Responder(HttpMethod.Post, caminho, Workspace(versao, "w1", "Solução"));

		var criado = await Cliente(api, versao).CreateWorkspaceAsync("Solução");

		criado.ShouldBe(new CamundaHubWorkspace("w1", "Solução"));
		api.A(caminho).ShouldHaveSingleItem().Json["name"]!.GetValue<string>().ShouldBe("Solução");
	}

	[Theory(DisplayName = "A workspace comes with its projects")]
	[InlineData(CamundaHubApiVersion.V1)]
	[InlineData(CamundaHubApiVersion.V2)]
	public async Task Get_Workspace(CamundaHubApiVersion versao)
	{
		var api = Api();
		if (versao == CamundaHubApiVersion.V1)
			api.Responder(HttpMethod.Get, "/api/v1/projects/w1", $$"""
				{ "metadata": {{Workspace(versao, "w1", "Solução")}},
				  "content": { "folders": [], "files": [], "processApplications": [ {{Projeto(versao, "p1", "Pedidos", "w1")}}, {{Projeto(versao, "p2", "Outro", "w1")}} ] } }
				""");
		else
			api.Responder(HttpMethod.Get, "/api/v2/workspaces/w1", $$"""
				{ "workspace": {{Workspace(versao, "w1", "Solução")}},
				  "content": { "projects": [ {{Projeto(versao, "p1", "Pedidos", "w1")}}, {{Projeto(versao, "p2", "Outro", "w1")}} ] } }
				""");

		var detalhe = await Cliente(api, versao).GetWorkspaceAsync("w1");

		detalhe.Workspace.ShouldBe(new CamundaHubWorkspace("w1", "Solução"));
		detalhe.Projects.ShouldBe(new[] { new CamundaHubProject("p1", "Pedidos", "w1"), new CamundaHubProject("p2", "Outro", "w1") });
	}

	[Theory(DisplayName = "The project search is by workspace, and keeps only the exact name")]
	[InlineData(CamundaHubApiVersion.V1)]
	[InlineData(CamundaHubApiVersion.V2)]
	public async Task Search_Projects(CamundaHubApiVersion versao)
	{
		var api = Api();
		var itens = $"{Projeto(versao, "p1", "pedidos", "w1")}, {Projeto(versao, "p2", "Pedidos", "w1")}";
		if (versao == CamundaHubApiVersion.V1)
			api.Responder(HttpMethod.Get, "/api/v1/projects/w1", $$"""
				{ "metadata": {{Workspace(versao, "w1", "S")}}, "content": { "folders": [], "files": [], "processApplications": [ {{itens}} ] } }
				""");
		else
			api.Responder(HttpMethod.Post, "/api/v2/projects/search", Pagina(versao, 2, itens));
		var cliente = Cliente(api, versao);

		var achados = await cliente.SearchProjectsAsync("w1", "Pedidos");
		var todos = await cliente.SearchProjectsAsync("w1", null);

		achados.ShouldHaveSingleItem().ShouldBe(new CamundaHubProject("p2", "Pedidos", "w1"));
		todos.Count.ShouldBe(2);
		if (versao == CamundaHubApiVersion.V2)
		{
			var filtro = api.A("/api/v2/projects/search")[0].Json["filter"]!;
			(filtro["workspaceKey"]!.GetValue<string>(), filtro["name"]!.GetValue<string>()).ShouldBe(("w1", "Pedidos"));
		}
	}

	[Fact(DisplayName = "The project search of the v2 follows three pages")]
	public async Task Search_Projects_Pages_V2()
	{
		const CamundaHubApiVersion versao = CamundaHubApiVersion.V2;
		var api = Paginas(Api(), versao, "/api/v2/projects/search", (k, nome) => Projeto(versao, k, nome, "w1"), ("minusc", "alvo"), ("certo", "Alvo"));

		var achados = await Cliente(api, versao).SearchProjectsAsync("w1", "Alvo");

		achados.ShouldHaveSingleItem().Key.ShouldBe("certo");
		ConferirPaginas(api.A("/api/v2/projects/search"), versao);
	}

	[Theory(DisplayName = "Creating a project posts its name and its workspace")]
	[InlineData(CamundaHubApiVersion.V1, "/api/v1/process-applications", "projectId")]
	[InlineData(CamundaHubApiVersion.V2, "/api/v2/projects", "workspaceKey")]
	public async Task Create_Project(CamundaHubApiVersion versao, string caminho, string campoDoWorkspace)
	{
		var api = Api().Responder(HttpMethod.Post, caminho, Projeto(versao, "p1", "Pedidos", "w1"));

		var criado = await Cliente(api, versao).CreateProjectAsync("w1", "Pedidos");

		criado.ShouldBe(new CamundaHubProject("p1", "Pedidos", "w1"));
		var corpo = api.A(caminho).ShouldHaveSingleItem().Json;
		(corpo["name"]!.GetValue<string>(), corpo[campoDoWorkspace]!.GetValue<string>()).ShouldBe(("Pedidos", "w1"));
	}

	[Theory(DisplayName = "The content of a project is its folders and files, with the type read in any case and an unknown type null")]
	[InlineData(CamundaHubApiVersion.V1, "/api/v1/process-applications/p1", "metadata")]
	[InlineData(CamundaHubApiVersion.V2, "/api/v2/projects/p1", "project")]
	public async Task Get_Project_Content(CamundaHubApiVersion versao, string caminho, string campo)
	{
		var api = Api().Responder(HttpMethod.Get, caminho, $$"""
			{ "{{campo}}": {{Projeto(versao, "p1", "Pedidos", "w1")}}, "content": {
			  "folders": [ {{Pasta(versao, "f1", "Sub", "p1", null)}} ],
			  "files": [ {{Arquivo(versao, "a1", "pedido", "p1", null, "bpmn", 4)}}, {{Arquivo(versao, "a2", "aprovar", "p1", null, "form", 1)}},
			             {{Arquivo(versao, "a3", "tabela", "p1", null, "dmn", 2)}} ] } }
			""");

		var conteudo = await Cliente(api, versao).GetProjectContentAsync("p1");

		conteudo.Folders.ShouldBe(new[] { new CamundaHubFolder("f1", "Sub", "p1", null) });
		conteudo.Files.ShouldBe(new[]
		{
			new CamundaHubFileMetadata("a1", "pedido", "p1", null, CamundaHubFileType.Bpmn, 4),
			new CamundaHubFileMetadata("a2", "aprovar", "p1", null, CamundaHubFileType.Form, 1),
			new CamundaHubFileMetadata("a3", "tabela", "p1", null, null, 2)
		});
	}

	[Theory(DisplayName = "The content of a folder is its folders and files")]
	[InlineData(CamundaHubApiVersion.V1, "/api/v1/folders/f1", "metadata")]
	[InlineData(CamundaHubApiVersion.V2, "/api/v2/folders/f1", "folder")]
	public async Task Get_Folder_Content(CamundaHubApiVersion versao, string caminho, string campo)
	{
		var api = Api().Responder(HttpMethod.Get, caminho, $$"""
			{ "{{campo}}": {{Pasta(versao, "f1", "Sub", "p1", null)}}, "content": {
			  "folders": [ {{Pasta(versao, "f2", "Neta", "p1", "f1")}} ], "files": [ {{Arquivo(versao, "a4", "x", "p1", "f1", "bpmn", 1)}} ] } }
			""");

		var conteudo = await Cliente(api, versao).GetFolderContentAsync("f1");

		conteudo.Folders.ShouldBe(new[] { new CamundaHubFolder("f2", "Neta", "p1", "f1") });
		conteudo.Files.ShouldBe(new[] { new CamundaHubFileMetadata("a4", "x", "p1", "f1", CamundaHubFileType.Bpmn, 1) });
	}

	[Theory(DisplayName = "A folder is created at the root of the project, or inside another folder")]
	[InlineData(CamundaHubApiVersion.V1, "/api/v1/folders")]
	[InlineData(CamundaHubApiVersion.V2, "/api/v2/folders")]
	public async Task Create_Folder(CamundaHubApiVersion versao, string caminho)
	{
		var api = Api().Responder(HttpMethod.Post, caminho, Pasta(versao, "f1", "Sub", "p1", null))
			.Responder(HttpMethod.Post, caminho, Pasta(versao, "f2", "Neta", "p1", "f1"));
		var cliente = Cliente(api, versao);

		(await cliente.CreateFolderAsync("p1", null, "Sub")).ShouldBe(new CamundaHubFolder("f1", "Sub", "p1", null));
		(await cliente.CreateFolderAsync("p1", "f1", "Neta")).ShouldBe(new CamundaHubFolder("f2", "Neta", "p1", "f1"));

		var (naRaiz, naPasta) = (api.A(caminho)[0].Json, api.A(caminho)[1].Json);
		naRaiz["name"]!.GetValue<string>().ShouldBe("Sub");
		naPasta["name"]!.GetValue<string>().ShouldBe("Neta");
		if (versao == CamundaHubApiVersion.V1)
		{
			// Na v1, a process application e a pasta mãe são exclusivas (o Swagger do Hub 8.10).
			(naRaiz["processApplicationId"]!.GetValue<string>(), naRaiz["parentId"]).ShouldBe(("p1", (System.Text.Json.Nodes.JsonNode?)null));
			(naPasta["parentId"]!.GetValue<string>(), naPasta["processApplicationId"]).ShouldBe(("f1", (System.Text.Json.Nodes.JsonNode?)null));
		}
		else
		{
			(naRaiz["projectKey"]!.GetValue<string>(), naRaiz["parentFolderKey"]).ShouldBe(("p1", (System.Text.Json.Nodes.JsonNode?)null));
			(naPasta["projectKey"]!.GetValue<string>(), naPasta["parentFolderKey"]!.GetValue<string>()).ShouldBe(("p1", "f1"));
		}
	}

	[Theory(DisplayName = "The paths go after the address of the Hub, with a final slash or a path of its own, and the call is told without it")]
	[InlineData(CamundaHubApiVersion.V1, "http://hub.falso:8070/", "/api/v1/projects/w1", "")]
	[InlineData(CamundaHubApiVersion.V2, "http://hub.falso:8070/", "/api/v2/workspaces/w1", "")]
	[InlineData(CamundaHubApiVersion.V1, "http://hub.falso/modeler", "/api/v1/projects/w1", "/modeler")]
	[InlineData(CamundaHubApiVersion.V2, "http://hub.falso/modeler/", "/api/v2/workspaces/w1", "/modeler")]
	public async Task Paths_After_Address(CamundaHubApiVersion versao, string endereco, string caminho, string prefixo)
	{
		var campo = versao == CamundaHubApiVersion.V1 ? "metadata" : "workspace";
		var conteudo = versao == CamundaHubApiVersion.V1 ? "\"processApplications\": []" : "\"projects\": []";
		var api = Api().Responder(HttpMethod.Get, prefixo + caminho, $$"""{ "{{campo}}": {{Workspace(versao, "w1", "S")}}, "content": { {{conteudo}} } }""");
		var chamadas = new List<CamundaCall>();

		await Cliente(api, versao, endereco: endereco).GetWorkspaceAsync("w1", observe: chamadas.Add);

		api.A(prefixo + caminho).ShouldHaveSingleItem().Host.ShouldBe("hub.falso");
		chamadas.Where(c => c.Kind == CamundaCallKind.Api).ShouldHaveSingleItem().Path.ShouldBe(caminho);
	}

	// ---------- 3. Os arquivos ----------

	[Theory(DisplayName = "The file search is by project, follows three pages, and keeps only the exact name and type")]
	[InlineData(CamundaHubApiVersion.V1)]
	[InlineData(CamundaHubApiVersion.V2)]
	public async Task Search_Files_Pages(CamundaHubApiVersion versao)
	{
		var caminho = versao == CamundaHubApiVersion.V1 ? "/api/v1/files/search" : "/api/v2/files/search";
		var tipos = new Dictionary<string, string> { ["form-alvo"] = "form", ["certo"] = "bpmn" };
		var api = Paginas(Api(), versao, caminho,
			(k, nome) => Arquivo(versao, k, nome == "alvo" ? "Alvo" : nome, "p1", null, tipos.GetValueOrDefault(k, "bpmn"), 1),
			("form-alvo", "alvo"), ("certo", "Alvo"));

		var achados = await Cliente(api, versao).SearchFilesAsync("p1", "Alvo", CamundaHubFileType.Bpmn);

		achados.ShouldHaveSingleItem().ShouldBe(new CamundaHubFileMetadata("certo", "Alvo", "p1", null, CamundaHubFileType.Bpmn, 1));
		ConferirPaginas(api.A(caminho), versao);
		var filtro = api.A(caminho)[0].Json["filter"]!;
		filtro["name"]!.GetValue<string>().ShouldBe("Alvo");
		if (versao == CamundaHubApiVersion.V1)
		{
			filtro["processApplicationId"]!.GetValue<string>().ShouldBe("p1");
			// A v1 diz o tipo em maiúsculas e não documenta o filtro por ele: o cliente filtra o tipo do lado de cá.
			filtro["type"].ShouldBeNull();
		}
		else
		{
			filtro["projectKey"]!.GetValue<string>().ShouldBe("p1");
			filtro["type"]!.GetValue<string>().ShouldBe("bpmn");
		}
	}

	[Theory(DisplayName = "A file comes with its metadata and its content, and is null when the Hub does not have it")]
	[InlineData(CamundaHubApiVersion.V1)]
	[InlineData(CamundaHubApiVersion.V2)]
	public async Task Get_File(CamundaHubApiVersion versao)
	{
		const string xml = "<bpmn:definitions id=\"d\">ç</bpmn:definitions>";
		var metadados = Arquivo(versao, "a1", "pedido", "p1", "f1", "bpmn", 7);
		var api = Api().Responder(HttpMethod.Get, Caminho(versao, "files/a1"), versao == CamundaHubApiVersion.V1
			? $$"""{ "metadata": {{metadados}}, "content": {{System.Text.Json.JsonSerializer.Serialize(xml)}} }"""
			: metadados.Replace("\"content\": null", "\"content\": " + System.Text.Json.JsonSerializer.Serialize(xml)));
		var cliente = Cliente(api, versao);

		var arquivo = await cliente.GetFileAsync("a1");
		var semArquivo = await cliente.GetFileAsync("nao-existe");

		arquivo.ShouldBe(new CamundaHubFile(new CamundaHubFileMetadata("a1", "pedido", "p1", "f1", CamundaHubFileType.Bpmn, 7), xml));
		semArquivo.ShouldBeNull();
	}

	[Theory(DisplayName = "Creating a file posts the name, the project or the folder, the type and the content")]
	[InlineData(CamundaHubApiVersion.V1)]
	[InlineData(CamundaHubApiVersion.V2)]
	public async Task Create_File(CamundaHubApiVersion versao)
	{
		var caminho = Caminho(versao, "files");
		var api = Api().Responder(HttpMethod.Post, caminho, Arquivo(versao, "a1", "pedido", "p1", null, "bpmn", 1))
			.Responder(HttpMethod.Post, caminho, Arquivo(versao, "a2", "aprovar", "p1", "f1", "form", 1));
		var cliente = Cliente(api, versao);

		(await cliente.CreateFileAsync("p1", null, "pedido", CamundaHubFileType.Bpmn, "<definitions/>"))
			.ShouldBe(new CamundaHubFileMetadata("a1", "pedido", "p1", null, CamundaHubFileType.Bpmn, 1));
		(await cliente.CreateFileAsync("p1", "f1", "aprovar", CamundaHubFileType.Form, "{ \"id\": \"aprovar\" }"))
			.ShouldBe(new CamundaHubFileMetadata("a2", "aprovar", "p1", "f1", CamundaHubFileType.Form, 1));

		var (bpmn, form) = (api.A(caminho)[0].Json, api.A(caminho)[1].Json);
		(bpmn["name"]!.GetValue<string>(), bpmn["content"]!.GetValue<string>()).ShouldBe(("pedido", "<definitions/>"));
		(form["name"]!.GetValue<string>(), form["content"]!.GetValue<string>()).ShouldBe(("aprovar", "{ \"id\": \"aprovar\" }"));
		if (versao == CamundaHubApiVersion.V1)
		{
			// Na v1, a process application e a pasta são exclusivas (o Swagger do Hub 8.10).
			(bpmn["fileType"]!.GetValue<string>(), bpmn["processApplicationId"]!.GetValue<string>(), bpmn["folderId"]).ShouldBe(("bpmn", "p1", (System.Text.Json.Nodes.JsonNode?)null));
			(form["fileType"]!.GetValue<string>(), form["folderId"]!.GetValue<string>(), form["processApplicationId"]).ShouldBe(("form", "f1", (System.Text.Json.Nodes.JsonNode?)null));
		}
		else
		{
			(bpmn["type"]!.GetValue<string>(), bpmn["projectKey"]!.GetValue<string>(), bpmn["folderKey"]).ShouldBe(("bpmn", "p1", (System.Text.Json.Nodes.JsonNode?)null));
			(form["type"]!.GetValue<string>(), form["projectKey"]!.GetValue<string>(), form["folderKey"]!.GetValue<string>()).ShouldBe(("form", "p1", "f1"));
		}
	}

	[Theory(DisplayName = "Updating a file patches the content with the revision, and reads the new revision")]
	[InlineData(CamundaHubApiVersion.V1)]
	[InlineData(CamundaHubApiVersion.V2)]
	public async Task Update_File(CamundaHubApiVersion versao)
	{
		var caminho = Caminho(versao, "files/a1");
		var api = Api().Responder(HttpMethod.Patch, caminho, Arquivo(versao, "a1", "pedido", "p1", null, "bpmn", 5));

		var atualizado = await Cliente(api, versao).UpdateFileAsync("a1", "<definitions id=\"novo\"/>", 4);

		atualizado.Revision.ShouldBe(5);
		var corpo = api.A(caminho).ShouldHaveSingleItem().Json;
		(corpo["content"]!.GetValue<string>(), corpo["revision"]!.GetValue<int>()).ShouldBe(("<definitions id=\"novo\"/>", 4));
	}

	[Theory(DisplayName = "A stale revision is a refusal 409 with the detail of the Hub")]
	[InlineData(CamundaHubApiVersion.V1)]
	[InlineData(CamundaHubApiVersion.V2)]
	public async Task Update_File_Conflict(CamundaHubApiVersion versao)
	{
		var caminho = Caminho(versao, "files/a1");
		var api = Api().Recusar(HttpMethod.Patch, caminho, HttpStatusCode.Conflict, "Conflict", "The file was changed: revision 4 is not the latest (5)");

		var recusa = await Should.ThrowAsync<CamundaApiException>(() => Cliente(api, versao).UpdateFileAsync("a1", "<x/>", 4));

		(recusa.Status, recusa.Title, recusa.Path).ShouldBe((409, "Conflict", caminho));
		recusa.Detail.ShouldBe("The file was changed: revision 4 is not the latest (5)");
	}

	[Theory(DisplayName = "A 403 that stays after the new token is a credential refusal by the Hub")]
	[InlineData(CamundaHubApiVersion.V1)]
	[InlineData(CamundaHubApiVersion.V2)]
	public async Task Forbidden(CamundaHubApiVersion versao)
	{
		var caminho = Caminho(versao, "files");
		var api = Api().Responder(HttpMethod.Post, caminho, "{}", HttpStatusCode.Forbidden);

		var recusa = await Should.ThrowAsync<CamundaCredentialException>(
			() => Cliente(api, versao).CreateFileAsync("p1", null, "pedido", CamundaHubFileType.Bpmn, "<x/>"));

		(recusa.Refusal, recusa.Status, recusa.Endpoint).ShouldBe((CamundaCredentialRefusal.Camunda, (int?)403, Hub + caminho));
		api.A(caminho).Count.ShouldBe(2);
	}

	[Theory(DisplayName = "A token the Hub refuses (401) is renewed once, and the call goes again with the same body")]
	[InlineData(CamundaHubApiVersion.V1)]
	[InlineData(CamundaHubApiVersion.V2)]
	public async Task Unauthorized_Renews_Token_Once(CamundaHubApiVersion versao)
	{
		var caminho = Caminho(versao, "files/a1");
		var api = Api().Responder(HttpMethod.Patch, caminho, "{}", HttpStatusCode.Unauthorized)
			.Responder(HttpMethod.Patch, caminho, Arquivo(versao, "a1", "pedido", "p1", null, "bpmn", 5));
		var chamadas = new List<CamundaCall>();

		(await Cliente(api, versao).UpdateFileAsync("a1", "<x/>", 4, observe: chamadas.Add)).Revision.ShouldBe(5);

		var pedidos = api.A(caminho);
		pedidos.Select(p => p.Autorizacao).ShouldBe(new[] { "Bearer t1", "Bearer t2" });
		pedidos[1].Corpo.ShouldBe(pedidos[0].Corpo);
		chamadas.Where(c => c.Kind == CamundaCallKind.Api).Select(c => (c.Status, c.WithRenewedToken)).ShouldBe(new[] { ((int?)401, false), (200, true) });
	}

	[Theory(DisplayName = "A call past its deadline is unavailable, saying the deadline, with no real wait")]
	[InlineData(CamundaHubApiVersion.V1)]
	[InlineData(CamundaHubApiVersion.V2)]
	public async Task Deadline(CamundaHubApiVersion versao)
	{
		var tempo = new FakeTimeProvider();
		var caminho = Caminho(versao, "files/a1");
		var api = Api().Pendurar(HttpMethod.Get, caminho);

		var chamada = Cliente(api, versao, tempo).GetFileAsync("a1");
		await api.EsperarPedidos(2);
		tempo.Advance(CamundaHubClient.DefaultTimeout);

		var fora = await Should.ThrowAsync<CamundaUnavailableException>(() => chamada);
		(fora.Target, fora.Endpoint, fora.Timeout).ShouldBe((CamundaEndpoint.Camunda, Hub + caminho, (TimeSpan?)CamundaHubClient.DefaultTimeout));
		fora.Reason.ShouldContain("10 s");
	}

	[Theory(DisplayName = "A connection that fails is unavailable, with the reason")]
	[InlineData(CamundaHubApiVersion.V1)]
	[InlineData(CamundaHubApiVersion.V2)]
	public async Task Connection_Fails(CamundaHubApiVersion versao)
	{
		var api = Api().SemResposta(HttpMethod.Get, Caminho(versao, "files/a1"));

		var fora = await Should.ThrowAsync<CamundaUnavailableException>(() => Cliente(api, versao).GetFileAsync("a1"));

		(fora.Target, fora.Timeout).ShouldBe((CamundaEndpoint.Camunda, (TimeSpan?)null));
		fora.Reason.ShouldContain("Nenhuma conexão");
	}

	[Theory(DisplayName = "No failure message carries the secret, not even when the provider echoes it")]
	[InlineData(CamundaHubApiVersion.V1)]
	[InlineData(CamundaHubApiVersion.V2)]
	public async Task No_Secret_In_Messages(CamundaHubApiVersion versao)
	{
		var arquivo = Caminho(versao, "files/a1");
		var falhas = new List<Exception>
		{
			await Should.ThrowAsync<CamundaApiException>(() => Cliente(
				Api().Recusar(HttpMethod.Patch, arquivo, HttpStatusCode.Conflict, "Conflict", "stale"), versao).UpdateFileAsync("a1", "<x/>", 1)),
			await Should.ThrowAsync<CamundaCredentialException>(() => Cliente(
				Api().Responder(HttpMethod.Get, arquivo, "{}", HttpStatusCode.Forbidden), versao).GetFileAsync("a1")),
			await Should.ThrowAsync<CamundaUnavailableException>(() => Cliente(
				Api().SemResposta(HttpMethod.Get, arquivo), versao).GetFileAsync("a1")),
			await Should.ThrowAsync<CamundaCredentialException>(() => Cliente(
				new ApiFalsa().Responder(HttpMethod.Post, Token, """{ "error": "unauthorized_client", "error_description": "Invalid secret s3cr3t-hub" }""",
					HttpStatusCode.Unauthorized), versao).GetFileAsync("a1"))
		};

		falhas.ShouldAllBe(f => !f.Message.Contains("s3cr3t-hub") && !f.ToString().Contains("s3cr3t-hub"));
	}

	// ---------- Apoio ----------

	private static string Caminho(CamundaHubApiVersion v, string resto) => (v == CamundaHubApiVersion.V1 ? "/api/v1/" : "/api/v2/") + resto;

	internal static ApiFalsa Api() => new ApiFalsa()
		.Responder(HttpMethod.Post, Token, TokenDe("t1", 300)).Responder(HttpMethod.Post, Token, TokenDe("t2", 300));

	internal static CamundaHubClient Cliente(
		ApiFalsa api, CamundaHubApiVersion? versao = null, TimeProvider? tempo = null, CamundaAuthenticationSettings? auth = null, string endereco = Hub) =>
		new(Config(auth ?? OAuth(), versao, endereco), api, tempo ?? TimeProvider.System);

	internal static CamundaHubSettings Config(CamundaAuthenticationSettings auth, CamundaHubApiVersion? versao = null, string endereco = Hub) =>
		new() { RestAddress = endereco, Authentication = auth, ApiVersion = versao };

	internal static CamundaAuthenticationSettings OAuth() => new()
	{
		Type = "OAuth", ClientId = "qa-hub", ClientSecret = "s3cr3t-hub", OAuthUrl = "http://idp.falso" + Token
	};

	// O JSON de cada coisa do Hub, como a versão o devolve (a v1 com os nomes de antes do 8.10, e o tipo em maiúsculas).
	internal static string Workspace(CamundaHubApiVersion v, string chave, string nome) => v == CamundaHubApiVersion.V1
		? $$"""{ "id": "{{chave}}", "name": "{{nome}}", "created": "2026-11-07T10:00:00Z" }"""
		: $$"""{ "workspaceKey": "{{chave}}", "name": "{{nome}}", "description": null, "created": "2026-11-07T10:00:00Z" }""";

	internal static string Projeto(CamundaHubApiVersion v, string chave, string nome, string workspace) => v == CamundaHubApiVersion.V1
		? $$"""{ "id": "{{chave}}", "name": "{{nome}}", "projectId": "{{workspace}}" }"""
		: $$"""{ "projectKey": "{{chave}}", "name": "{{nome}}", "workspaceKey": "{{workspace}}" }""";

	internal static string Pasta(CamundaHubApiVersion v, string chave, string nome, string projeto, string? mae) => v == CamundaHubApiVersion.V1
		? $$"""{ "id": "{{chave}}", "name": "{{nome}}", "projectId": "w1", "processApplicationId": "{{projeto}}", "parentId": {{Json(mae)}} }"""
		: $$"""{ "folderKey": "{{chave}}", "name": "{{nome}}", "projectKey": "{{projeto}}", "parentFolderKey": {{Json(mae)}} }""";

	internal static string Arquivo(CamundaHubApiVersion v, string chave, string nome, string projeto, string? pasta, string tipo, int revisao) =>
		v == CamundaHubApiVersion.V1
			? $$"""{ "id": "{{chave}}", "name": "{{nome}}", "projectId": "w1", "processApplicationId": "{{projeto}}", "folderId": {{Json(pasta)}}, "revision": {{revisao}}, "type": "{{tipo.ToUpperInvariant()}}", "simplePath": "x" }"""
			: $$"""{ "fileKey": "{{chave}}", "name": "{{nome}}", "projectKey": "{{projeto}}", "folderKey": {{Json(pasta)}}, "revision": {{revisao}}, "type": "{{tipo}}", "simplePath": "x", "content": null }""";

	private static string Json(string? texto) => texto is null ? "null" : $"\"{texto}\"";

	internal static int Tamanho(CamundaHubApiVersion v) => v == CamundaHubApiVersion.V1 ? 50 : 100;

	internal static string Pagina(CamundaHubApiVersion v, int total, string itens) => v == CamundaHubApiVersion.V1
		? $$"""{ "items": [ {{itens}} ], "total": {{total}} }"""
		: $$"""{ "items": [ {{itens}} ], "page": { "totalItems": {{total}}, "hasMoreTotalItems": false } }""";

	// Três páginas cheias até a última: a primeira com o item "primeiro", a última com o "ultimo" (chave e nome).
	internal static ApiFalsa Paginas(ApiFalsa api, CamundaHubApiVersion v, string caminho, Func<string, string, string> item,
		(string Chave, string Nome) primeiro, (string Chave, string Nome) ultimo)
	{
		var tamanho = Tamanho(v);
		var total = 2 * tamanho + 20;
		string Enchimento(int pagina, int quantos) => string.Join(", ", Enumerable.Range(0, quantos).Select(i => item($"k{pagina}-{i}", $"Outro {pagina}-{i}")));
		return api
			.Responder(HttpMethod.Post, caminho, Pagina(v, total, Enchimento(1, tamanho - 1) + ", " + item(primeiro.Chave, primeiro.Nome)))
			.Responder(HttpMethod.Post, caminho, Pagina(v, total, Enchimento(2, tamanho)))
			.Responder(HttpMethod.Post, caminho, Pagina(v, total, Enchimento(3, 19) + ", " + item(ultimo.Chave, ultimo.Nome)));
	}

	// As três páginas pedidas: a v1 pelo número (desde 0) e o tamanho 50; a v2 pelo from e o limit 100.
	internal static void ConferirPaginas(IReadOnlyList<PedidoVisto> pedidos, CamundaHubApiVersion v)
	{
		pedidos.Count.ShouldBe(3);
		if (v == CamundaHubApiVersion.V1)
		{
			pedidos.Select(p => p.Json["page"]!.GetValue<int>()).ShouldBe(new[] { 0, 1, 2 });
			pedidos.ShouldAllBe(p => p.Json["size"]!.GetValue<int>() == 50);
		}
		else
		{
			pedidos.Select(p => p.Json["page"]!["from"]!.GetValue<int>()).ShouldBe(new[] { 0, 100, 200 });
			pedidos.ShouldAllBe(p => p.Json["page"]!["limit"]!.GetValue<int>() == 100);
		}
	}

	private static string TokenDe(string token, int segundos) =>
		$$"""{ "access_token": "{{token}}", "token_type": "Bearer", "expires_in": {{segundos}} }""";
}

/// <summary>
/// As variáveis <c>CAMUNDA_*</c> não entram no cliente do Hub. Muda o ambiente do processo: roda fora do paralelo, na
/// coleção do <c>TestSettings</c>.
/// </summary>
[Collection("TestSettings")]
[Trait("Category", "Camunda")]
[Trait("Component", "HubClient")]
public class CamundaHubClientAmbienteTests
{
	[Fact(DisplayName = "The CAMUNDA_* environment variables do not fill the Hub settings")]
	public async Task Environment_Not_Used()
	{
		var antes = new Dictionary<string, string?>();
		foreach (var nome in new[] { "CAMUNDA_CLIENT_ID", "CAMUNDA_CLIENT_SECRET", "CAMUNDA_OAUTH_URL", "CAMUNDA_TOKEN_AUDIENCE" })
			antes[nome] = Environment.GetEnvironmentVariable(nome);
		try
		{
			Environment.SetEnvironmentVariable("CAMUNDA_CLIENT_ID", "do-ambiente");
			Environment.SetEnvironmentVariable("CAMUNDA_CLIENT_SECRET", "secret-do-ambiente");
			Environment.SetEnvironmentVariable("CAMUNDA_OAUTH_URL", "http://idp.ambiente/token");
			Environment.SetEnvironmentVariable("CAMUNDA_TOKEN_AUDIENCE", "orchestration-api");
			var semClient = CamundaHubClientTests.OAuth();
			semClient.ClientId = null;
			var api = CamundaHubClientTests.Api().Responder(HttpMethod.Get, "/api/v2/info", """{ "version": "v2", "authorizedOrganization": "o" }""");

			Should.Throw<InvalidOperationException>(() => CamundaHubClient.For(CamundaHubClientTests.Config(semClient))).Message
				.ShouldContain("authentication.clientId");
			await CamundaHubClientTests.Cliente(api, CamundaHubApiVersion.V2).GetInfoAsync();

			var corpo = api.A(CamundaHubClientTests.Token).ShouldHaveSingleItem().Corpo!;
			corpo.ShouldContain("client_id=qa-hub");
			corpo.ShouldContain("audience=" + CamundaHubClient.DefaultAudience);
		}
		finally
		{
			foreach (var (nome, valor) in antes)
				Environment.SetEnvironmentVariable(nome, valor);
		}
	}
}
