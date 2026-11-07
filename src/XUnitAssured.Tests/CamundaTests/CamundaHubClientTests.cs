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

	// ---------- Apoio ----------

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
