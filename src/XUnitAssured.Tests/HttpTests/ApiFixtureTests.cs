#if NET10_0_OR_GREATER
using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

using XUnitAssured.Http.AspNetCore;
using XUnitAssured.Http.Configuration;
using XUnitAssured.Http.Testing;

namespace XUnitAssured.Tests.HttpTests;

/// <summary>
/// O ApiFixture contra a SampleWebApi: o cliente compartilhado, um cliente por identidade, com e
/// sem cookies, os serviços e os logs da API.
///
/// <para>
/// A SampleWebApi não tem rota que devolva cabeçalhos ou ponha cookie, então o fixture de teste
/// acrescenta duas pelo ConfigureApi -- o que também prova que o ConfigureApi chega ao host.
/// </para>
/// </summary>
[Trait("Category", "Http")]
public class ApiFixtureTests(ApiFixtureTests.Api api) : HttpTestBase<ApiFixtureTests.Api>(api), IClassFixture<ApiFixtureTests.Api>
{
	[Fact(DisplayName = "Given(fixture) goes out through the shared client, with ConfigureClient applied")]
	public async Task Given_Uses_The_Shared_Client()
	{
		var resultado = await Given()
			.ApiResource("/eco/quem")
			.Get()
			.ExecuteAsync();

		resultado.Then()
			.AssertStatusCode(200)
			.AssertJsonPath<string>("$.usuario", u => u == "admin");

		Fixture.CreateClient().ShouldBeSameAs(Fixture.CreateClient());
	}

	[Fact(DisplayName = "ClientFor is someone else, and leaves the shared client as it was")]
	public async Task ClientFor_Replaces_The_Identity()
	{
		using var leitor = Fixture.ClientFor(("X-Usuario", "leitor"), ("X-Extra", "1"));

		(await UsuarioAsync(leitor)).ShouldBe("leitor");
		leitor.DefaultRequestHeaders.GetValues("X-Usuario").ShouldHaveSingleItem();
		(await UsuarioAsync(Fixture.CreateClient())).ShouldBe("admin");
	}

	[Fact(DisplayName = "ClientWithCookies keeps a session; ClientWithoutCookies does not")]
	public async Task Cookies_Are_Kept_Only_When_Asked()
	{
		using var comCookies = Fixture.ClientWithCookies();
		using var semCookies = Fixture.ClientWithoutCookies();

		foreach (var cliente in new[] { comCookies, semCookies })
			(await cliente.PostAsync("/eco/entrar", null)).EnsureSuccessStatusCode();

		(await comCookies.GetStringAsync("/eco/sessao")).ShouldBe("sessao=aberta");
		(await semCookies.GetStringAsync("/eco/sessao")).ShouldBeEmpty();

		// Cada um é novo: a sessão de um não aparece no outro com cookies.
		using var outro = Fixture.ClientWithCookies();
		(await outro.GetStringAsync("/eco/sessao")).ShouldBeEmpty();
	}

	[Fact(DisplayName = "AssertSentCookie sees the cookie the client added on its own")]
	public async Task Sent_Cookie_Comes_From_The_Client()
	{
		using var comCookies = Fixture.ClientWithCookies();
		using var semCookies = Fixture.ClientWithoutCookies();
		foreach (var cliente in new[] { comCookies, semCookies })
			(await cliente.PostAsync("/eco/entrar", null)).EnsureSuccessStatusCode();

		// O teste não escreve cookie nenhum: quem põe a sessão no pedido é o cliente, como um
		// navegador -- e é isso que se confere.
		(await XUnitAssured.Core.DSL.ScenarioDsl.Given()
			.WithHttpClient(comCookies)
			.ApiResource("/eco/sessao")
			.Get()
			.ExecuteAsync())
		.Then().AssertSentCookie("sessao", v => v == "aberta");

		(await XUnitAssured.Core.DSL.ScenarioDsl.Given()
			.WithHttpClient(semCookies)
			.ApiResource("/eco/sessao")
			.Get()
			.ExecuteAsync())
		.Then().AssertNoSentCookie("sessao");
	}

	[Fact(DisplayName = "Services and CreateScope reach the API's own services")]
	public void Services_Reach_The_Api()
	{
		using var escopo = Fixture.CreateScope();

		escopo.ServiceProvider.GetRequiredService<SampleWebApi.Controllers.ProductStore>()
			.ShouldBeSameAs(Fixture.Services.GetRequiredService<SampleWebApi.Controllers.ProductStore>());
	}

	[Fact(DisplayName = "Logs asks for CaptureLogs instead of answering empty")]
	public void Logs_Without_Capture_Says_So()
	{
		Should.Throw<InvalidOperationException>(() => Fixture.Logs)
			.Message.ShouldContain("CaptureLogs");
	}

	private static async Task<string> UsuarioAsync(HttpClient cliente)
	{
		var resposta = await XUnitAssured.Core.DSL.ScenarioDsl.Given()
			.WithHttpClient(cliente)
			.ApiResource("/eco/quem")
			.Get()
			.ExecuteAsync();
		return resposta.JsonPath<string>("$.usuario")!;
	}

	// ---------- Ambiente ----------

	/// <summary>A SampleWebApi com as rotas de eco, e o admin como identidade de todo cliente.</summary>
	public sealed class Api : ApiFixture<Program>
	{
		protected override void ConfigureApi(IWebHostBuilder api)
		{
			api.UseEnvironment("Testing");
			api.UseSetting("Serilog:MinimumLevel:Default", "Warning");
			api.ConfigureServices(servicos => servicos.AddTransient<IStartupFilter, RotasDeEco>());
		}

		protected override void ConfigureClient(HttpClient client) =>
			client.DefaultRequestHeaders.Add("X-Usuario", "admin");
	}

	private sealed class RotasDeEco : IStartupFilter
	{
		public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> proximo) => app =>
		{
			app.Map("/eco/quem", a => a.Run(c =>
				c.Response.WriteAsJsonAsync(new { usuario = c.Request.Headers["X-Usuario"].ToString() })));
			app.Map("/eco/entrar", a => a.Run(c =>
			{
				c.Response.Cookies.Append("sessao", "aberta");
				return Task.CompletedTask;
			}));
			app.Map("/eco/sessao", a => a.Run(c =>
				c.Response.WriteAsync(string.Join("; ", c.Request.Cookies.Select(k => $"{k.Key}={k.Value}")))));
			proximo(app);
		};
	}
}

/// <summary>
/// O que muda o host: autenticação aplicada pelo fixture e logs capturados. Uma instância própria,
/// como o README pede para quem muda a configuração.
/// </summary>
[Trait("Category", "Http")]
public class ApiFixtureComAutenticacaoTests(ApiFixtureComAutenticacaoTests.ApiComBasic api)
	: HttpTestBase<ApiFixtureComAutenticacaoTests.ApiComBasic>(api), IClassFixture<ApiFixtureComAutenticacaoTests.ApiComBasic>
{
	[Fact(DisplayName = "Authentication is applied to every Given(fixture) request, and Logs sees what the API logged")]
	public async Task Authentication_And_Logs()
	{
		Fixture.Logs.Clear();

		var resultado = await Given()
			.ApiResource("/api/auth/basic")
			.Get()
			.ExecuteAsync();

		resultado.Then().AssertStatusCode(200);
		Fixture.Logs.AssertLoggedOnce(predicate: l => l.Message.Contains("Basic authentication successful for user: admin"));
	}

	[Fact(DisplayName = "A fixture created inside a test starts its own API and stops it")]
	public async Task Fixture_Inside_A_Test()
	{
		using var propria = new ApiComBasic();

		var resultado = await XUnitAssured.Core.DSL.ScenarioDsl.Given(propria)
			.ApiResource("/api/auth/basic")
			.Get()
			.ExecuteAsync();

		resultado.Then().AssertStatusCode(200);
		propria.Logs.Entries.ShouldNotBeEmpty();
		Fixture.Services.ShouldNotBeSameAs(propria.Services);
	}

	public sealed class ApiComBasic : ApiFixture<Program>
	{
		protected override bool CaptureLogs => true;

		protected override HttpAuthConfig? Authentication
		{
			get
			{
				var autenticacao = new HttpAuthConfig();
				autenticacao.UseBasicAuth("admin", "secret123");
				return autenticacao;
			}
		}

		// O nível do Serilog continua Warning: a captura recebe tudo mesmo assim.
		protected override void ConfigureApi(IWebHostBuilder api)
		{
			api.UseEnvironment("Testing");
			api.UseSetting("Serilog:MinimumLevel:Default", "Warning");
		}
	}
}
#endif
