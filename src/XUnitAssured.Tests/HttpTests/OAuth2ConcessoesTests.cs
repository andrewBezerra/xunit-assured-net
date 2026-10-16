using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

using Flurl.Http;
using Flurl.Http.Testing;

using Shouldly;
using Xunit;

using XUnitAssured.Http.Configuration;
using XUnitAssured.Http.Handlers;

namespace XUnitAssured.Tests.HttpTests;

[Trait("Category", "Http")]
[Trait("Component", "Authentication")]
/// <summary>
/// O pedido de token de cada concessão OAuth2, e o que o handler diz quando falta o que ela exige.
///
/// <para>
/// O servidor de token é o HttpTest do Flurl: dá para ler o formulário que saiu. Cada concessão tem
/// os seus campos obrigatórios, e o que se verifica é que eles vão no pedido e que, faltando um, o
/// erro nomeia o campo antes de qualquer pedido sair.
/// </para>
/// </summary>
public class OAuth2ConcessoesTests
{
	private const string UrlDoToken = "http://auth.teste/token";

	private static OAuth2Config Configuracao(OAuth2GrantType concessao) => new()
	{
		TokenUrl = UrlDoToken,
		ClientId = "cliente-" + Guid.NewGuid().ToString("N"),
		ClientSecret = "segredo",
		GrantType = concessao
	};

	private static HttpTest ServidorDeToken()
	{
		var http = new HttpTest();
		http.ForCallsTo(UrlDoToken).RespondWithJson(new { access_token = "tk-concessao", expires_in = 3600 });
		return http;
	}

	[Fact(DisplayName = "The OAuth2 handler should say it is for OAuth2, and handle only an OAuth2 config that has one")]
	public void Handler_Should_Handle_Only_OAuth2_Configs()
	{
		var handler = new OAuth2Handler(Configuracao(OAuth2GrantType.ClientCredentials));

		handler.Type.ShouldBe(AuthenticationType.OAuth2);
		handler.CanHandle(new HttpAuthConfig { Type = AuthenticationType.OAuth2, OAuth2 = new OAuth2Config() }).ShouldBeTrue();
		handler.CanHandle(new HttpAuthConfig { Type = AuthenticationType.OAuth2 }).ShouldBeFalse();
		handler.CanHandle(new HttpAuthConfig { Type = AuthenticationType.Bearer, OAuth2 = new OAuth2Config() }).ShouldBeFalse();
		handler.CanHandle(null!).ShouldBeFalse();
		Should.Throw<ArgumentNullException>(() => handler.ApplyAuthentication(null!));
		Should.Throw<ArgumentNullException>(() => new OAuth2Handler(null!));
	}

	[Fact(DisplayName = "The password grant should send the user's credentials and put the token on the request")]
	public void Password_Grant_Should_Send_The_Credentials()
	{
		using var http = ServidorDeToken();
		var configuracao = Configuracao(OAuth2GrantType.Password);
		configuracao.Username = "ana";
		configuracao.Password = "s3nha";
		var pedido = new FlurlRequest("http://api.teste/x");

		new OAuth2Handler(configuracao).ApplyAuthentication(pedido);

		pedido.Headers.FirstOrDefault("Authorization").ShouldBe("Bearer tk-concessao");
		http.ShouldHaveCalled(UrlDoToken)
			.WithVerb(HttpMethod.Post)
			.WithRequestBody("*grant_type=password*")
			.WithRequestBody("*username=ana*")
			.WithRequestBody("*password=s3nha*")
			.WithRequestBody("*client_secret=segredo*")
			.Times(1);
	}

	[Fact(DisplayName = "The refresh token grant should send the refresh token")]
	public void Refresh_Token_Grant_Should_Send_The_Refresh_Token()
	{
		using var http = ServidorDeToken();
		var configuracao = Configuracao(OAuth2GrantType.RefreshToken);
		configuracao.RefreshToken = "rt-1";

		new OAuth2Handler(configuracao).ApplyAuthentication(new FlurlRequest("http://api.teste/x"));

		http.ShouldHaveCalled(UrlDoToken)
			.WithRequestBody("*grant_type=refresh_token*")
			.WithRequestBody("*refresh_token=rt-1*")
			.Times(1);
	}

	[Fact(DisplayName = "The authorization code grant should send the code and the redirect URI")]
	public void Authorization_Code_Grant_Should_Send_Code_And_Redirect()
	{
		using var http = ServidorDeToken();
		var configuracao = Configuracao(OAuth2GrantType.AuthorizationCode);
		configuracao.Code = "codigo-1";
		configuracao.RedirectUri = "http://app.teste/volta";

		new OAuth2Handler(configuracao).ApplyAuthentication(new FlurlRequest("http://api.teste/x"));

		http.ShouldHaveCalled(UrlDoToken)
			.WithRequestBody("*grant_type=authorization_code*")
			.WithRequestBody("*code=codigo-1*")
			.WithRequestBody("*redirect_uri=*")
			.Times(1);
	}

	[Fact(DisplayName = "Scopes and additional parameters should travel with the token request")]
	public void Scopes_And_Additional_Parameters_Should_Travel()
	{
		using var http = ServidorDeToken();
		var configuracao = Configuracao(OAuth2GrantType.ClientCredentials);
		configuracao.Scopes = new List<string> { "leitura", "escrita" };
		configuracao.AdditionalParameters = new Dictionary<string, string> { ["audience"] = "api-pedidos" };

		new OAuth2Handler(configuracao).ApplyAuthentication(new FlurlRequest("http://api.teste/x"));

		http.ShouldHaveCalled(UrlDoToken)
			.WithRequestBody("*scope=leitura*escrita*")
			.WithRequestBody("*audience=api-pedidos*")
			.Times(1);
	}

	public static IEnumerable<object[]> CamposQueFaltam() => new[]
	{
		new object[] { OAuth2GrantType.Password, (Action<OAuth2Config>)(c => c.Password = "p"), "Username required" },
		new object[] { OAuth2GrantType.Password, (Action<OAuth2Config>)(c => c.Username = "u"), "Password required" },
		new object[] { OAuth2GrantType.RefreshToken, (Action<OAuth2Config>)(_ => { }), "Refresh token required" },
		new object[] { OAuth2GrantType.AuthorizationCode, (Action<OAuth2Config>)(c => c.RedirectUri = "http://r"), "Authorization code required" },
		new object[] { OAuth2GrantType.AuthorizationCode, (Action<OAuth2Config>)(c => c.Code = "c"), "Redirect URI required" }
	};

	[Theory(DisplayName = "A grant missing a field it needs should name the field and ask for no token")]
	[MemberData(nameof(CamposQueFaltam))]
	public void Missing_Field_Should_Be_Named(OAuth2GrantType concessao, Action<OAuth2Config> preencher, string mensagem)
	{
		using var http = ServidorDeToken();
		var configuracao = Configuracao(concessao);
		preencher(configuracao);

		Should.Throw<InvalidOperationException>(() =>
				new OAuth2Handler(configuracao).ApplyAuthentication(new FlurlRequest("http://api.teste/x")))
			.Message.ShouldContain(mensagem);
		http.ShouldNotHaveMadeACall();
	}

	[Fact(DisplayName = "An OAuth2 step built by WithOAuth2Config should ask for the token with that grant")]
	public async Task WithOAuth2Config_Should_Use_The_Grant()
	{
		using var http = ServidorDeToken();
		http.RespondWith("{}");
		var cliente = "cliente-" + Guid.NewGuid().ToString("N");

		await Given()
			.ApiResource("http://api.teste/recurso")
			.WithOAuth2Config(c =>
			{
				c.TokenUrl = UrlDoToken;
				c.ClientId = cliente;
				c.ClientSecret = "segredo";
				c.GrantType = OAuth2GrantType.Password;
				c.Username = "ana";
				c.Password = "s3nha";
			})
			.Get()
			.ExecuteAsync()
			.AssertStatusCode(200);

		http.ShouldHaveCalled(UrlDoToken).WithRequestBody($"*client_id={cliente}*").WithRequestBody("*grant_type=password*");
		http.ShouldHaveCalled("http://api.teste/recurso").WithHeader("Authorization", "Bearer tk-concessao");
	}

	[Fact(DisplayName = "An OAuth2 step built by WithOAuth2 with scopes should ask for those scopes")]
	public async Task WithOAuth2_Should_Ask_For_The_Scopes()
	{
		using var http = ServidorDeToken();
		http.RespondWith("{}");
		var cliente = "cliente-" + Guid.NewGuid().ToString("N");

		await Given()
			.ApiResource("http://api.teste/recurso")
			.WithOAuth2(UrlDoToken, cliente, "segredo", "pedidos.ler")
			.Get()
			.ExecuteAsync();

		http.ShouldHaveCalled(UrlDoToken).WithRequestBody("*scope=pedidos.ler*").Times(1);
	}
}
