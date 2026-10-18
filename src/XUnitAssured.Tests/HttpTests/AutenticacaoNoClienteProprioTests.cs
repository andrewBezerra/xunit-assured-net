using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using Flurl.Http.Testing;

using Shouldly;
using Xunit;

using XUnitAssured.Core.DSL;
using XUnitAssured.Core.Storage;
using XUnitAssured.Http.Configuration;
using XUnitAssured.Http.Extensions;
using XUnitAssured.Http.Results;
using XUnitAssured.Http.Steps;

namespace XUnitAssured.Tests.HttpTests;

/// <summary>
/// A autenticação de um pedido com HttpClient próprio — o caminho do WebApplicationFactory — é a
/// mesma do caminho do Flurl (#147).
///
/// <para>
/// O caminho do HttpClient próprio tinha uma cópia da autenticação, e ela divergiu: não aplicava
/// OAuth2, e a ApiKey na query só valia vinda da configuração do passo. Agora os dois usam os
/// mesmos handlers; estes testes comparam o que cada caminho manda.
/// </para>
/// </summary>
[Trait("Category", "Http")]
[Trait("Component", "Authentication")]
public class AutenticacaoNoClienteProprioTests
{
	public static TheoryData<string> Tipos => new() { "basic", "bearer", "apikey-header", "apikey-query", "custom-header", "basic-vazio", "bearer-vazio", "apikey-vazia" };

	[Theory(DisplayName = "The custom HttpClient path sends the same authentication as the Flurl path")]
	[MemberData(nameof(Tipos))]
	public async Task Both_Paths_Send_The_Same(string tipo)
	{
		var config = Configuracao(tipo);

		// Caminho do Flurl, com o envio interceptado pelo HttpTest
		string pelaRedeConsulta;
		Dictionary<string, string> pelaRedeCabecalhos;
		using (var http = new HttpTest())
		{
			http.RespondWith("{}");
			await new HttpRequestStep { Url = "http://servidor.teste/recurso", AuthConfig = config }.ExecuteAsync(Contexto());

			var chamada = http.CallLog.ShouldHaveSingleItem();
			pelaRedeConsulta = chamada.HttpRequestMessage.RequestUri!.Query;
			pelaRedeCabecalhos = chamada.HttpRequestMessage.Headers.ToDictionary(h => h.Key, h => string.Join(",", h.Value));
		}

		// Caminho do HttpClient próprio
		var servidor = ServidorDeMentira.Sempre(Json("{}"));
		await new HttpRequestStep { Url = "/recurso", AuthConfig = config, CustomHttpClient = Cliente(servidor) }.ExecuteAsync(Contexto());
		var recebido = servidor.Recebidos.ShouldHaveSingleItem();

		recebido.Consulta.ShouldBe(pelaRedeConsulta);
		foreach (var nome in new[] { "Authorization", "X-Api-Key", "X-Tenant" })
			recebido.Cabecalhos.GetValueOrDefault(nome).ShouldBe(pelaRedeCabecalhos.GetValueOrDefault(nome), $"Header {nome} ({tipo})");
	}

	// O servidor de um WebApplicationFactory vive em memória: a URL do token só responde pelo cliente dele.
	[Fact(DisplayName = "With a custom HttpClient, OAuth2 asks for the token through it and sends it as Bearer")]
	public async Task OAuth2_Asks_For_The_Token_Through_The_Client()
	{
		string? formulario = null;
		var servidor = new ServidorDeMentira(p =>
		{
			if (p.RequestUri!.AbsolutePath != "/token")
				return Json("{}");
			formulario = p.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
			return Json("""{"access_token":"tk-1","token_type":"Bearer","expires_in":3600}""");
		});

		await ScenarioDsl.Given(new ProvedorDeMentira(servidor))
			.ApiResource("/recurso")
			.WithOAuth2("/token", "cliente", "segredo", "leitura")
			.Get()
			.ExecuteAsync();

		servidor.Recebidos.Select(r => $"{r.Metodo} {r.Caminho}").ShouldBe(["POST /token", "GET /recurso"]);
		servidor.Recebidos[1].Cabecalhos["Authorization"].ShouldBe("Bearer tk-1");
		formulario.ShouldBe("grant_type=client_credentials&client_id=cliente&client_secret=segredo&scope=leitura");
	}

	// O cache é de cada cliente: um servidor de teste nasce e morre com ele, e o token de um não vale no próximo.
	[Fact(DisplayName = "With a custom HttpClient, the token is reused by the requests of that client only, and only for the same credentials")]
	public async Task OAuth2_Token_Is_Reused_Per_Client_And_Credentials()
	{
		var tokensPedidos = 0;
		var servidor = new ServidorDeMentira(p =>
		{
			if (p.RequestUri!.AbsolutePath != "/token")
				return Json("{}");
			tokensPedidos++;
			return Json($$"""{"access_token":"tk-{{tokensPedidos}}","expires_in":3600}""");
		});
		var cliente = Cliente(servidor);

		await Pedido(cliente, "segredo");
		await Pedido(cliente, "segredo");
		tokensPedidos.ShouldBe(1);

		await Pedido(cliente, "outro-segredo");
		tokensPedidos.ShouldBe(2);

		await Pedido(Cliente(servidor), "segredo");
		tokensPedidos.ShouldBe(3);

		servidor.Recebidos.Where(r => r.Caminho == "/recurso").Select(r => r.Cabecalhos["Authorization"])
			.ShouldBe(["Bearer tk-1", "Bearer tk-1", "Bearer tk-2", "Bearer tk-3"]);

		static Task Pedido(HttpClient cliente, string segredo)
		{
			var config = new HttpAuthConfig();
			config.UseOAuth2("/token", "cliente", segredo);
			return new HttpRequestStep { Url = "/recurso", AuthConfig = config, CustomHttpClient = cliente }.ExecuteAsync(Contexto());
		}
	}

	// A fixture dá um cliente novo a cada cenário, e o servidor dela vive mais que todos (#152).
	[Fact(DisplayName = "The scenarios of one fixture share its OAuth2 token, and another fixture asks for its own")]
	public async Task OAuth2_Token_Is_Shared_By_The_Scenarios_Of_A_Fixture()
	{
		var servidor = ServidorDeTokens();
		var fixture = new ProvedorDeMentira(servidor);

		await Cenario(ScenarioDsl.Given(fixture));
		await Cenario(ScenarioDsl.Given(fixture));
		await Cenario(ScenarioDsl.Given(new ProvedorDeMentira(servidor)));

		Autorizacoes(servidor).ShouldBe(["Bearer tk-1", "Bearer tk-1", "Bearer tk-2"]);
	}

	// Uma fixture com várias APIs dá um provedor por API, e as que usam a mesma autenticação dividem o token.
	[Fact(DisplayName = "Providers that name the same token owner share its OAuth2 token, and another owner asks for its own")]
	public async Task OAuth2_Token_Is_Shared_By_Providers_With_The_Same_Owner()
	{
		var servidor = ServidorDeTokens();
		var dono = new object();

		await Cenario(ScenarioDsl.Given(new ProvedorComDono(servidor, dono)));
		await Cenario(ScenarioDsl.Given(new ProvedorComDono(servidor, dono)));
		await Cenario(ScenarioDsl.Given(new ProvedorComDono(servidor, new object())));

		Autorizacoes(servidor).ShouldBe(["Bearer tk-1", "Bearer tk-1", "Bearer tk-2"]);
	}

	[Fact(DisplayName = "A provider that names no token owner keeps its tokens to itself")]
	public async Task OAuth2_Token_Without_Owner_Stays_With_The_Provider()
	{
		var servidor = ServidorDeTokens();
		var provedor = new ProvedorComDono(servidor, null);

		await Cenario(ScenarioDsl.Given(provedor));
		await Cenario(ScenarioDsl.Given(provedor));
		await Cenario(ScenarioDsl.Given(new ProvedorComDono(servidor, null)));

		Autorizacoes(servidor).ShouldBe(["Bearer tk-1", "Bearer tk-1", "Bearer tk-2"]);
	}

	// O dono dos tokens é da fixture: um cliente dado por WithHttpClient pode ser outro servidor.
	[Fact(DisplayName = "WithHttpClient on a scenario of an owned provider does not reuse the owner's token")]
	public async Task WithHttpClient_Does_Not_Reuse_The_Owner_Token()
	{
		var daFixture = ServidorDeTokens();
		var outro = ServidorDeTokens();
		var dono = new object();

		await Cenario(ScenarioDsl.Given(new ProvedorComDono(daFixture, dono)));
		await ScenarioDsl.Given(new ProvedorComDono(daFixture, dono))
			.ApiResource("/recurso")
			.WithHttpClient(Cliente(outro))
			.WithOAuth2("/token", "cliente", "segredo")
			.Get()
			.ExecuteAsync();

		Autorizacoes(daFixture).ShouldBe(["Bearer tk-1"]);
		Autorizacoes(outro).ShouldBe(["Bearer tk-1"]);
	}

	// Depois de .And(), o passo novo herda o cliente do anterior, e com ele a fixture dos tokens.
	[Fact(DisplayName = "A step after And() keeps the fixture's token, as the next scenario of the fixture does")]
	public async Task Step_After_And_Keeps_The_Fixture_Token()
	{
		var servidor = ServidorDeTokens();
		var fixture = new ProvedorDeMentira(servidor);

		await ScenarioDsl.Given(fixture)
			.ApiResource("/recurso").WithOAuth2("/token", "cliente", "segredo").Get()
			.And()
			.ApiResource("/recurso").Get()
			.ExecuteAsync();
		await Cenario(ScenarioDsl.Given(fixture));

		Autorizacoes(servidor).ShouldBe(["Bearer tk-1", "Bearer tk-1", "Bearer tk-1"]);
	}

	// Given(HttpClient) embrulha o cliente num provedor novo a cada cenário: ali o dono é o cliente.
	[Fact(DisplayName = "Scenarios given the same HttpClient share its token, and another client asks for its own")]
	public async Task OAuth2_Token_Is_Shared_By_The_Scenarios_Of_A_Given_Client()
	{
		var servidor = ServidorDeTokens();
		var cliente = Cliente(servidor);

		await Cenario(ScenarioDsl.Given(cliente));
		await Cenario(ScenarioDsl.Given(cliente));
		await Cenario(ScenarioDsl.Given(Cliente(servidor)));

		Autorizacoes(servidor).ShouldBe(["Bearer tk-1", "Bearer tk-1", "Bearer tk-2"]);
	}

	// Outro cliente pode ser outro servidor: o token da fixture não vai para ele.
	[Fact(DisplayName = "WithHttpClient on a fixture's scenario does not reuse the fixture's token")]
	public async Task WithHttpClient_Does_Not_Reuse_The_Fixture_Token()
	{
		var daFixture = ServidorDeTokens();
		var outro = ServidorDeTokens();
		var fixture = new ProvedorDeMentira(daFixture);

		await Cenario(ScenarioDsl.Given(fixture));
		await ScenarioDsl.Given(fixture)
			.ApiResource("/recurso")
			.WithHttpClient(Cliente(outro))
			.WithOAuth2("/token", "cliente", "segredo")
			.Get()
			.ExecuteAsync();

		Autorizacoes(daFixture).ShouldBe(["Bearer tk-1"]);
		outro.Recebidos.Select(r => r.Caminho).ShouldBe(["/token", "/recurso"]);
	}

	[Fact(DisplayName = "With the token cache turned off, the scenarios of one fixture each ask for a token")]
	public async Task OAuth2_Without_Cache_Ignores_The_Fixture()
	{
		var servidor = ServidorDeTokens();
		var fixture = new ProvedorDeMentira(servidor);

		for (var i = 0; i < 2; i++)
			await ScenarioDsl.Given(fixture)
				.ApiResource("/recurso")
				.WithAuthConfig(c =>
				{
					c.UseOAuth2("/token", "cliente", "segredo");
					c.TokenCache = new TokenCacheConfig { Enabled = false };
				})
				.Get()
				.ExecuteAsync();

		Autorizacoes(servidor).ShouldBe(["Bearer tk-1", "Bearer tk-2"]);
	}

	[Fact(DisplayName = "With the token cache turned off, every request asks for its own token")]
	public async Task OAuth2_Without_Cache_Asks_Every_Time()
	{
		var tokensPedidos = 0;
		var servidor = new ServidorDeMentira(p =>
		{
			if (p.RequestUri!.AbsolutePath == "/token")
				tokensPedidos++;
			return Json("""{"access_token":"tk","expires_in":3600}""");
		});
		var cliente = Cliente(servidor);
		var config = new HttpAuthConfig { TokenCache = new TokenCacheConfig { Enabled = false } };
		config.UseOAuth2("/token", "cliente", "segredo");

		await new HttpRequestStep { Url = "/recurso", AuthConfig = config, CustomHttpClient = cliente }.ExecuteAsync(Contexto());
		await new HttpRequestStep { Url = "/recurso", AuthConfig = config, CustomHttpClient = cliente }.ExecuteAsync(Contexto());

		tokensPedidos.ShouldBe(2);
	}

	[Fact(DisplayName = "With a custom HttpClient, a refused token request fails the step and says why")]
	public async Task OAuth2_Refused_Through_The_Client_Says_Why()
	{
		var servidor = ServidorDeMentira.Sempre(ServidorDeMentira.Responder(HttpStatusCode.Unauthorized, """{"error":"invalid_client"}"""));
		var config = new HttpAuthConfig();
		config.UseOAuth2("/token", "cliente", "errado");

		var resultado = await new HttpRequestStep { Url = "/recurso", AuthConfig = config, CustomHttpClient = Cliente(servidor) }
			.ExecuteAsync(Contexto());

		resultado.Success.ShouldBeFalse();
		resultado.Errors.ShouldHaveSingleItem().ShouldContain("OAuth token request failed: 401 - {\"error\":\"invalid_client\"}");
		servidor.Recebidos.ShouldHaveSingleItem().Caminho.ShouldBe("/token");
	}

	// ---------- Apoio ----------

	private static HttpAuthConfig Configuracao(string tipo)
	{
		var config = new HttpAuthConfig();
		switch (tipo)
		{
			case "basic": config.UseBasicAuth("ana", "senha"); break;
			case "bearer": config.UseBearerToken("tk"); break;
			case "apikey-header": config.UseApiKey("X-Api-Key", "chave", ApiKeyLocation.Header); break;
			case "apikey-query": config.UseApiKey("api_key", "chave", ApiKeyLocation.Query); break;
			// Credencial vazia vai como está, para testar que a API a recusa
			case "basic-vazio": config.UseBasicAuth("", ""); break;
			case "bearer-vazio": config.UseBearerToken(""); break;
			case "apikey-vazia": config.UseApiKey("api_key", "", ApiKeyLocation.Query); break;
			case "custom-header":
				config.Type = AuthenticationType.CustomHeader;
				config.CustomHeader = new CustomHeaderAuthConfig { Headers = new() { ["X-Tenant"] = "colih" } };
				break;
		}
		return config;
	}

	private static HttpClient Cliente(ServidorDeMentira servidor) =>
		new(servidor, disposeHandler: false) { BaseAddress = new Uri("http://servidor.teste") };

	private static TestContext Contexto() => new(new StepStorage());

	/// <summary>Responde /token com um token novo a cada pedido, e o resto com {}.</summary>
	private static ServidorDeMentira ServidorDeTokens()
	{
		var tokensPedidos = 0;
		return new ServidorDeMentira(p => p.RequestUri!.AbsolutePath == "/token"
			? Json($$"""{"access_token":"tk-{{Interlocked.Increment(ref tokensPedidos)}}","expires_in":3600}""")
			: Json("{}"));
	}

	private static async Task Cenario(Core.Abstractions.ITestScenario cenario) =>
		await cenario.ApiResource("/recurso").WithOAuth2("/token", "cliente", "segredo").Get().ExecuteAsync();

	private static string[] Autorizacoes(ServidorDeMentira servidor) =>
		servidor.Recebidos.Where(r => r.Caminho == "/recurso").Select(r => r.Cabecalhos["Authorization"]).ToArray();

	private static HttpResponseMessage Json(string corpo) => ServidorDeMentira.Responder(HttpStatusCode.OK, corpo);
}
