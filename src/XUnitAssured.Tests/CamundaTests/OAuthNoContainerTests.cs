using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

using XUnitAssured.Camunda.Testcontainers;

using static XUnitAssured.Tests.CamundaTests.WorkersNoContainerTests;

namespace XUnitAssured.Tests.CamundaTests;

/// <summary>
/// O OAuth dos workers no Testcontainers, sem Docker (5e): com o criador falso, o fixture sobe um Keycloak em https antes
/// do Camunda, com o realm importado de um arquivo que ele gera; o Camunda valida o token por esse Keycloak; o worker
/// recebe as variáveis das três famílias e os placeholders; e o cliente do teste pede o token no mesmo Keycloak.
/// </summary>
[Trait("Category", "Camunda")]
[Trait("Component", "Testcontainers")]
public class OAuthNoContainerTests
{
	private const string Realm = "/opt/keycloak/data/import/camunda-platform-realm.json";
	private const string Emissor = "https://keycloak:8443/auth/realms/camunda-platform";
	private const string TokenNaRede = Emissor + "/protocol/openid-connect/token";

	[Fact(DisplayName = "OAuth: a Keycloak starts over https before the Camunda, on the network, with the realm imported from a file")]
	public async Task Keycloak_Starts_First_Over_Https()
	{
		var criador = new CriadorFalso();
		await using var fixture = Fixture(criador);

		await fixture.InitializeAsync();

		criador.Subidos.Select(d => d.Nome).ShouldBe(["keycloak", "camunda", "worker"]);
		var keycloak = criador.Definicao("keycloak");
		keycloak.Original.Imagem.ShouldBe("camunda/keycloak:quay-26.6.4");
		keycloak.Original.Comando.ShouldNotBeNull().ShouldContain("--import-realm");
		keycloak.Variaveis["KC_HOSTNAME"].ShouldBe("https://keycloak:8443/auth");
		keycloak.Variaveis["KC_HTTP_RELATIVE_PATH"].ShouldBe("/auth");
		keycloak.Variaveis["KC_HTTPS_CERTIFICATE_FILE"].ShouldBe("/xunitassured/tls/server.pem");
		keycloak.Variaveis["KC_HTTPS_CERTIFICATE_KEY_FILE"].ShouldBe("/xunitassured/tls/server.key");
		keycloak.Arquivos.Keys.ShouldContain(Realm);
		keycloak.Original.Portas.ShouldContain(8443);
		keycloak.Original.Espera.ShouldNotBeNull().Path.ShouldBe("/auth/realms/camunda-platform/.well-known/openid-configuration");
		keycloak.Autoridade.ShouldNotBeNull();
	}

	[Fact(DisplayName = "OAuth: the server certificate also carries the keycloak name")]
	public async Task Certificate_Names_The_Keycloak()
	{
		var criador = new CriadorFalso();
		await using var fixture = Fixture(criador);

		await fixture.InitializeAsync();

		var pem = Encoding.ASCII.GetString(criador.Definicao("camunda").Arquivos["/xunitassured/tls/server.pem"]);
		using var servidor = X509Certificate2.CreateFromPem(pem);
		servidor.MatchesHostname("keycloak").ShouldBeTrue();
		servidor.MatchesHostname("camunda").ShouldBeTrue();
	}

	[Fact(DisplayName = "OAuth: the realm has the Camunda's client, the worker's with client credentials and the audience, and the test's")]
	public async Task Realm_Has_The_Three_Clients()
	{
		var criador = new CriadorFalso();
		await using var fixture = Fixture(criador, clientId: "port-worker", clientSecret: "segredo-do-worker");

		await fixture.InitializeAsync();

		var realm = JsonNode.Parse(criador.Definicao("keycloak").Arquivos[Realm]).ShouldNotBeNull();
		realm["realm"]!.GetValue<string>().ShouldBe("camunda-platform");
		realm["enabled"]!.GetValue<bool>().ShouldBeTrue();
		var clientes = realm["clients"]!.AsArray().Select(c => c!).ToDictionary(c => c["clientId"]!.GetValue<string>());
		clientes.Keys.ShouldBe(["camunda", "port-worker", "xunitassured-test"], ignoreOrder: true);

		var worker = clientes["port-worker"];
		worker["secret"]!.GetValue<string>().ShouldBe("segredo-do-worker");
		worker["serviceAccountsEnabled"]!.GetValue<bool>().ShouldBeTrue();
		worker["publicClient"]!.GetValue<bool>().ShouldBeFalse();
		Mapeador(worker, "oidc-audience-mapper")["config"]!["included.custom.audience"]!.GetValue<string>().ShouldBe("orchestration-api");
		Mapeador(worker, "oidc-hardcoded-claim-mapper")["config"]!["claim.value"]!.GetValue<string>().ShouldBe("port-worker");

		clientes["xunitassured-test"]["serviceAccountsEnabled"]!.GetValue<bool>().ShouldBeTrue();
		clientes["camunda"]["secret"]!.GetValue<string>().ShouldNotBeNullOrWhiteSpace();
	}

	[Fact(DisplayName = "OAuth: the Camunda validates the token by the Keycloak on the network, and trusts the test authority to reach it")]
	public async Task Camunda_Points_Its_Oidc_At_The_Keycloak()
	{
		var criador = new CriadorFalso();
		await using var fixture = Fixture(criador);

		await fixture.InitializeAsync();

		var camunda = criador.Definicao("camunda");
		camunda.Variaveis["CAMUNDA_SECURITY_AUTHENTICATION_METHOD"].ShouldBe("oidc");
		camunda.Variaveis["CAMUNDA_SECURITY_AUTHENTICATION_UNPROTECTEDAPI"].ShouldBe("false");
		camunda.Variaveis["CAMUNDA_SECURITY_AUTHENTICATION_OIDC_ISSUERURI"].ShouldBe(Emissor);
		camunda.Variaveis["CAMUNDA_SECURITY_AUTHENTICATION_OIDC_TOKENURI"].ShouldBe(TokenNaRede);
		camunda.Variaveis["CAMUNDA_SECURITY_AUTHENTICATION_OIDC_JWKSETURI"].ShouldBe(Emissor + "/protocol/openid-connect/certs");
		camunda.Variaveis["CAMUNDA_SECURITY_AUTHENTICATION_OIDC_AUTHORIZATIONURI"].ShouldBe(Emissor + "/protocol/openid-connect/auth");
		camunda.Variaveis["CAMUNDA_SECURITY_AUTHENTICATION_OIDC_CLIENTID"].ShouldBe("camunda");
		camunda.Variaveis["CAMUNDA_SECURITY_AUTHENTICATION_OIDC_AUDIENCES_0"].ShouldBe("orchestration-api");
		camunda.Variaveis["CAMUNDA_SECURITY_AUTHENTICATION_OIDC_CLIENTIDCLAIM"].ShouldBe("client_id");
		camunda.Variaveis.Keys.ShouldNotContain("CAMUNDA_SECURITY_INITIALIZATION_USERS_0_USERNAME");
		camunda.Variaveis["JAVA_TOOL_OPTIONS"].ShouldContain("-Djavax.net.ssl.trustStore=/xunitassured/tls/truststore.p12");
		camunda.Basic.ShouldBeNull();
		// Sem credencial na espera, o Camunda pronto responde 401 no /v2/topology: a API de pé e protegida.
		camunda.Original.StatusDaEspera.ShouldBe(HttpStatusCode.Unauthorized);
	}

	[Fact(DisplayName = "OAuth: the worker gets the token endpoint, client, secret and audience in the SDK, Spring and Zeebe names")]
	public async Task Worker_Gets_The_Three_Families()
	{
		var criador = new CriadorFalso();
		await using var fixture = Fixture(criador, clientId: "port-worker", clientSecret: "segredo-do-worker");

		await fixture.InitializeAsync();

		var worker = criador.Definicao("worker").Variaveis;
		worker["CAMUNDA_AUTH_STRATEGY"].ShouldBe("OAUTH");
		foreach (var (chave, valor) in new Dictionary<string, string>
		{
			["CAMUNDA_OAUTH_URL"] = TokenNaRede,
			["CAMUNDA_CLIENT_ID"] = "port-worker",
			["CAMUNDA_CLIENT_SECRET"] = "segredo-do-worker",
			["CAMUNDA_TOKEN_AUDIENCE"] = "orchestration-api",
			["CAMUNDA_CLIENT_AUTH_METHOD"] = "oidc",
			["CAMUNDA_CLIENT_AUTH_TOKENURL"] = TokenNaRede,
			["CAMUNDA_CLIENT_AUTH_CLIENTID"] = "port-worker",
			["CAMUNDA_CLIENT_AUTH_CLIENTSECRET"] = "segredo-do-worker",
			["CAMUNDA_CLIENT_AUTH_AUDIENCE"] = "orchestration-api",
			["CAMUNDA_CLIENT_RESTADDRESS"] = "https://camunda:8080",
			["CAMUNDA_CLIENT_GRPCADDRESS"] = "https://camunda:26500",
			["ZEEBE_CLIENT_ID"] = "port-worker",
			["ZEEBE_CLIENT_SECRET"] = "segredo-do-worker",
			["ZEEBE_AUTHORIZATION_SERVER_URL"] = TokenNaRede,
			["ZEEBE_TOKEN_AUDIENCE"] = "orchestration-api"
		})
			worker[chave].ShouldBe(valor, chave);
		worker.Keys.ShouldNotContain("CAMUNDA_BASIC_AUTH_PASSWORD");
	}

	[Fact(DisplayName = "OAuth: the placeholders take the token endpoint, the client, the secret and the audience")]
	public async Task Placeholders_Take_The_OAuth_Values()
	{
		var criador = new CriadorFalso();
		var worker = Worker();
		worker.Environment["Camunda__TokenUrl"] = "${camunda.tokenUrl}";
		worker.Environment["Camunda__ClientId"] = "${camunda.clientId}";
		worker.Environment["Camunda__ClientSecret"] = "${camunda.clientSecret}";
		worker.Environment["Camunda__Audience"] = "${camunda.audience}";
		await using var fixture = Fixture(criador, clientId: "port-worker", clientSecret: "s3", servicos: [worker]);

		await fixture.InitializeAsync();

		var variaveis = criador.Definicao("worker").Variaveis;
		variaveis["Camunda__TokenUrl"].ShouldBe(TokenNaRede);
		variaveis["Camunda__ClientId"].ShouldBe("port-worker");
		variaveis["Camunda__ClientSecret"].ShouldBe("s3");
		variaveis["Camunda__Audience"].ShouldBe("orchestration-api");
	}

	[Fact(DisplayName = "OAuth: without a declared secret, the worker's secret is new on every run")]
	public async Task Worker_Secret_Is_Generated_When_Not_Declared()
	{
		var primeiro = new CriadorFalso();
		var segundo = new CriadorFalso();
		await using var um = Fixture(primeiro);
		await using var dois = Fixture(segundo);

		await um.InitializeAsync();
		await dois.InitializeAsync();

		var segredo = primeiro.Definicao("worker").Variaveis["CAMUNDA_CLIENT_SECRET"];
		segredo.ShouldNotBeNullOrWhiteSpace();
		segredo.ShouldNotBe(segundo.Definicao("worker").Variaveis["CAMUNDA_CLIENT_SECRET"]);
	}

	[Fact(DisplayName = "OAuth: the test's own client asks the same Keycloak for its token, on the Keycloak's mapped port")]
	public async Task Test_Client_Uses_The_Same_Keycloak()
	{
		var criador = new CriadorFalso();
		await using var fixture = Fixture(criador, audience: "minha-api");

		await fixture.InitializeAsync();

		var auth = fixture.Settings.Authentication;
		auth.Type.ShouldBe("OAuth");
		auth.ClientId.ShouldBe("xunitassured-test");
		auth.ClientSecret.ShouldNotBeNullOrWhiteSpace();
		auth.Audience.ShouldBe("minha-api");
		auth.OAuthUrl.ShouldBe($"https://{CriadorFalso.Host}:{CriadorFalso.PortaNoHost(8443)}/auth/realms/camunda-platform/protocol/openid-connect/token");
		fixture.Settings.CaCertificatePath.ShouldNotBeNull();
		fixture.Settings.RestAddress.ShouldBe($"https://{CriadorFalso.Host}:{CriadorFalso.PortaNoHost(8080)}");
		criador.Definicao("camunda").Variaveis["CAMUNDA_SECURITY_AUTHENTICATION_OIDC_AUDIENCES_0"].ShouldBe("minha-api");
	}

	[Fact(DisplayName = "OAuth without the worker's client id is a configuration error that says what to add")]
	public void OAuth_Needs_The_Client_Id()
	{
		var erro = Should.Throw<InvalidOperationException>(() => Fixture(new CriadorFalso(), clientId: null));

		erro.Message.ShouldContain("camunda.containers.oauth.clientId");
	}

	[Fact(DisplayName = "OAuth: the worker's client id cannot take the name of the Camunda's or the test's client")]
	public void OAuth_Client_Id_Cannot_Be_Reserved()
	{
		var erro = Should.Throw<InvalidOperationException>(() => Fixture(new CriadorFalso(), clientId: "xunitassured-test"));

		erro.Message.ShouldContain("xunitassured-test");
	}

	[Fact(DisplayName = "OAuth: the keycloak name on the network is reserved, like the camunda one")]
	public void Keycloak_Name_Is_Reserved()
	{
		var erro = Should.Throw<InvalidOperationException>(() => Fixture(new CriadorFalso(), servicos: [Worker("keycloak")]));

		erro.Message.ShouldContain("keycloak");
	}

	[Fact(DisplayName = "OAuth: the camunda.containers.oauth section is read from testsettings.json")]
	public void OAuth_Section_Is_Read()
	{
		var secao = System.Text.Json.JsonSerializer.Deserialize<CamundaSectionWithContainers>(
			"""{ "containers": { "authentication": "oauth", "oauth": { "clientId": "w", "clientSecret": "s", "audience": "a" }, "services": [] } }""",
			new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }).ShouldNotBeNull();

		var oauth = secao.Containers.ShouldNotBeNull().OAuth.ShouldNotBeNull();
		oauth.ClientId.ShouldBe("w");
		oauth.ClientSecret.ShouldBe("s");
		oauth.Audience.ShouldBe("a");
	}

	[Fact(DisplayName = "In Docker, a wait for a status other than success asks the HTTP request for that status")]
	public void Docker_Wait_Can_Expect_A_Status()
	{
		var definicao = new DefinicaoDeContainer(
			"camunda", "camunda/camunda:x", new Dictionary<string, string>(), [8080], new CamundaContainerWait { Port = 8080, Path = "/v2/topology" },
			null, new Dictionary<string, byte[]>(), null, StatusDaEspera: HttpStatusCode.Unauthorized);

		CriadorNoDocker.Espera(definicao).ShouldNotBeNull();
	}

	// ---------- Apoio ----------

	private static JsonNode Mapeador(JsonNode cliente, string tipo) =>
		cliente["protocolMappers"]!.AsArray().Single(m => m!["protocolMapper"]!.GetValue<string>() == tipo)!;

	private static CamundaServiceContainer Worker(string nome = "worker") => new() { Name = nome, Image = $"exemplo/{nome}:1" };

	private static CamundaFixture Fixture(
		CriadorFalso criador, string? clientId = "worker-do-cliente", string? clientSecret = null, string? audience = null,
		CamundaServiceContainer[]? servicos = null) =>
		new(new CamundaSectionWithContainers
		{
			Containers = new CamundaContainersSettings
			{
				Authentication = "oauth",
				OAuth = new CamundaContainersOAuth { ClientId = clientId, ClientSecret = clientSecret, Audience = audience },
				Services = [.. servicos ?? [Worker()]]
			}
		}, _ => null, criador);
}
