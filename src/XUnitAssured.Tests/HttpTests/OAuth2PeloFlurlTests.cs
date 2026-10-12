using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

using Flurl.Http;
using Flurl.Http.Testing;

using Shouldly;
using Xunit;

using XUnitAssured.Core.Storage;
using XUnitAssured.Http.Configuration;
using XUnitAssured.Http.Handlers;
using XUnitAssured.Http.Steps;
using XUnitAssured.Http.TokenManagement;

namespace XUnitAssured.Tests.HttpTests;

/// <summary>
/// OAuth2 sem HttpClient próprio — o caminho do Flurl, pela rede — e as respostas de token fora
/// do comum (#158).
///
/// <para>
/// A rodada de mutação da 6.15.0 mostrou que nenhum teste offline levava um passo OAuth2 por este
/// caminho, e que a resposta sem access_token, sem expires_in ou com refresh_token, e a validação
/// da configuração, podiam quebrar sem que nada notasse. O pedido do token é falso (HttpTest do
/// Flurl): sem servidor, e dá para contar o que foi pedido.
/// </para>
/// </summary>
[Trait("Category", "Http")]
[Trait("Component", "Authentication")]
public class OAuth2PeloFlurlTests
{
	private const string UrlDoToken = "http://auth.teste/token";

	// O cache do caminho da rede é um só para o processo: cada teste usa um cliente seu, para não
	// receber o token de outro.
	private readonly string _cliente = "cliente-" + Guid.NewGuid().ToString("N");

	[Fact(DisplayName = "Without a custom client, an OAuth2 step asks for the token once and the next step reuses it")]
	public async Task Step_On_The_Flurl_Path_Asks_Once()
	{
		using var http = new HttpTest();
		http.ForCallsTo(UrlDoToken).RespondWithJson(new { access_token = "tk-1", expires_in = 3600 });
		http.RespondWith("{}");

		var primeiro = await Passo("http://api.teste/a").ExecuteAsync(Contexto());
		var segundo = await Passo("http://api.teste/b").ExecuteAsync(Contexto());

		primeiro.Success.ShouldBeTrue(string.Join("; ", primeiro.Errors));
		segundo.Success.ShouldBeTrue(string.Join("; ", segundo.Errors));
		http.ShouldHaveCalled(UrlDoToken).WithVerb(HttpMethod.Post).WithRequestBody($"*client_id={_cliente}*").Times(1);
		http.ShouldHaveCalled("http://api.teste/*").WithHeader("Authorization", "Bearer tk-1").Times(2);
	}

	// Sem a checagem, o pedido iria com "Bearer " e nada depois, e a falha seria um 401 da API.
	[Fact(DisplayName = "A token response without access_token fails the step, says so, and sends nothing to the API")]
	public async Task Response_Without_Access_Token_Fails_The_Step()
	{
		using var http = new HttpTest();
		http.ForCallsTo(UrlDoToken).RespondWithJson(new { token_type = "Bearer", expires_in = 3600 });
		http.RespondWith("{}");

		var resultado = await Passo("http://api.teste/a").ExecuteAsync(Contexto());

		resultado.Success.ShouldBeFalse();
		resultado.Errors.ShouldHaveSingleItem().ShouldContain("Failed to obtain access token from OAuth server");
		http.ShouldNotHaveCalled("http://api.teste/*");
	}

	// O cache só devolve um token com mais de 5 minutos pela frente (TokenCacheEntry.IsValid).
	[Theory(DisplayName = "A token without expires_in is kept for an hour; one that expires in under five minutes is asked for again")]
	[InlineData(null, 1)]
	[InlineData(0, 1)]
	[InlineData(200, 2)]
	[InlineData(3600, 1)]
	public void Expires_In_Decides_The_Reuse(int? expiraEm, int pedidosDeToken)
	{
		using var http = new HttpTest();
		http.RespondWithJson(expiraEm is null
			? new Dictionary<string, object> { ["access_token"] = "tk" }
			: new Dictionary<string, object> { ["access_token"] = "tk", ["expires_in"] = expiraEm });

		var handler = new OAuth2Handler(Configuracao());
		handler.ApplyAuthentication(new FlurlRequest("http://api.teste/a"));
		handler.ApplyAuthentication(new FlurlRequest("http://api.teste/b"));

		http.ShouldHaveCalled(UrlDoToken).Times(pedidosDeToken);
	}

	[Theory(DisplayName = "The refresh_token of the response is kept with the token, when there is one")]
	[InlineData("rt-1")]
	[InlineData(null)]
	public void Refresh_Token_Is_Kept(string? refresh)
	{
		using var http = new HttpTest();
		http.RespondWithJson(refresh is null
			? new Dictionary<string, object> { ["access_token"] = "tk", ["expires_in"] = 3600 }
			: new Dictionary<string, object> { ["access_token"] = "tk", ["expires_in"] = 3600, ["refresh_token"] = refresh });
		var cache = new CacheQueGuarda();

		new OAuth2Handler(Configuracao(), cache).ApplyAuthentication(new FlurlRequest("http://api.teste/a"));

		var guardado = cache.Guardados.ShouldHaveSingleItem();
		guardado.AccessToken.ShouldBe("tk");
		guardado.RefreshToken.ShouldBe(refresh);
	}

	[Theory(DisplayName = "A configuration missing TokenUrl, ClientId or ClientSecret fails naming it, before any token request")]
	[InlineData("TokenUrl")]
	[InlineData("ClientId")]
	[InlineData("ClientSecret")]
	public void Incomplete_Configuration_Fails_Naming_What_Is_Missing(string faltando)
	{
		using var http = new HttpTest();
		http.RespondWithJson(new { access_token = "tk", expires_in = 3600 });

		var config = Configuracao();
		switch (faltando)
		{
			case "TokenUrl": config.TokenUrl = " "; break;
			case "ClientId": config.ClientId = ""; break;
			case "ClientSecret": config.ClientSecret = ""; break;
		}

		Should.Throw<InvalidOperationException>(() => new OAuth2Handler(config).ApplyAuthentication(new FlurlRequest("http://api.teste/a")))
			.Message.ShouldBe($"{faltando} is required for OAuth2 authentication.");
		http.CallLog.ShouldBeEmpty();
	}

	// ---------- Apoio ----------

	private OAuth2Config Configuracao() => new() { TokenUrl = UrlDoToken, ClientId = _cliente, ClientSecret = "segredo" };

	private HttpRequestStep Passo(string url) =>
		new() { Url = url, AuthConfig = new HttpAuthConfig { Type = AuthenticationType.OAuth2, OAuth2 = Configuracao() } };

	private static Core.DSL.TestContext Contexto() => new(new StepStorage());

	/// <summary>Um cache que lembra o que recebeu, para ver o que o handler guardou.</summary>
	private sealed class CacheQueGuarda : ITokenCache
	{
		public List<TokenCacheEntry> Guardados { get; } = [];

		public TokenCacheEntry? Get(string key) => null;

		public void Set(string key, TokenCacheEntry entry) => Guardados.Add(entry);

		public void Remove(string key) { }

		public void Clear() { }

		public IEnumerable<string> GetAllKeys() => [];
	}
}
