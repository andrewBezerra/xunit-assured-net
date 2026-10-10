#if NET10_0_OR_GREATER
using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

using Shouldly;
using Xunit;

using XUnitAssured.Core.DSL;
using XUnitAssured.Playwright.AspNetCore;
using XUnitAssured.Playwright.Extensions;

namespace XUnitAssured.Tests.PlaywrightTests;

/// <summary>
/// O BrowserAppFixture: a API de verdade (a SampleWebApi) numa porta, um front-end compilado
/// noutra, e o navegador -- tudo dentro do dotnet test.
///
/// <para>
/// Num consumidor real, os testes de navegador precisavam de três terminais (API, front-end e o
/// teste) e por isso ficavam fora do CI. Fora do CI envelheceram sem ninguém ver: o front-end mudou
/// o jeito de guardar a sessão e um deles passou a não provocar nada.
/// </para>
/// </summary>
[Trait("Category", "Playwright")]
[Trait("Requires", "Browser")]
public class ApiEmPortaRealTests : IClassFixture<ApiEmPortaRealTests.AppComApi>
{
	private readonly AppComApi _ambiente;

	public ApiEmPortaRealTests(AppComApi ambiente) => _ambiente = ambiente;

	[Fact(DisplayName = "The front-end, served on its own port, talks to the API on another")]
	public async Task Front_End_Talks_To_The_Api()
	{
		await using var pagina = await _ambiente.OpenPageAsync();

		var resultado = await ScenarioDsl.Given(pagina)
			.NavigateTo($"/?api={_ambiente.ApiUrl}")
			.WaitForSelector("#resposta, #erro")
			.ExecuteAsync();

		resultado.Then().AssertText("#resposta", "Public endpoint - no authentication required");
	}

	[Fact(DisplayName = "A client-side route falls back to index.html")]
	public async Task Client_Route_Falls_Back_To_Index()
	{
		await using var pagina = await _ambiente.OpenPageAsync();

		var resultado = await ScenarioDsl.Given(pagina)
			.NavigateTo("/casos/42")
			.ExecuteAsync();

		resultado.Then().AssertTitle("App de teste");
	}

	// Duas origens de verdade: o fetch sai da origem do app, e só passa porque a API libera o
	// AppUrl no CORS com credenciais.
	[Fact(DisplayName = "A request from the page crosses origins under the API's CORS")]
	public async Task Fetch_From_The_Page_Crosses_Origins()
	{
		await using var pagina = await _ambiente.OpenPageAsync();

		var resultado = await ScenarioDsl.Given(pagina)
			.NavigateTo("/")
			.FetchFromPage($"{_ambiente.ApiUrl}/api/auth/public")
			.ExecuteAsync();

		resultado.Then()
			.AssertFetchStatus(200)
			.AssertFetchJsonPath<string>("$.authType", t => t == "None");

		new Uri(_ambiente.AppUrl).Port.ShouldNotBe(new Uri(_ambiente.ApiUrl).Port);
	}

	[Fact(DisplayName = "Data can be arranged through the API's own client")]
	public async Task Api_Client_Arranges_Data()
	{
		using var cliente = _ambiente.Api.CreateClient();

		var resposta = await cliente.GetAsync("/api/auth/public");

		resposta.StatusCode.ShouldBe(HttpStatusCode.OK);
	}

	// ---------- Ambiente ----------

	/// <summary>A SampleWebApi e o front-end de teste, uma vez por classe.</summary>
	public sealed class AppComApi : BrowserAppFixture<Program>
	{
		protected override string? AppDirectory => "PlaywrightTests/AppDeTeste";

		// A origem do app só é conhecida depois que a API sobe, por isso o CORS a lê a cada pedido.
		protected override void ConfigureApi(IWebHostBuilder api)
		{
			api.UseEnvironment("Testing");
			api.UseSetting("Serilog:MinimumLevel:Default", "Warning");
			api.ConfigureServices(servicos => servicos
				.AddCors()
				.AddTransient<IStartupFilter>(_ => new CorsDoApp(() => AppUrl)));
		}
	}

	private sealed class CorsDoApp(Func<string> origem) : IStartupFilter
	{
		public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> proximo) => app =>
		{
			app.UseCors(politica => politica
				.SetIsOriginAllowed(o => o == origem())
				.AllowCredentials()
				.AllowAnyHeader()
				.AllowAnyMethod());
			proximo(app);
		};
	}
}

/// <summary>
/// Sem navegador: um AppDirectory sem index.html falha antes de subir a API, dizendo onde procurou.
/// </summary>
[Trait("Category", "Playwright")]
public class PastaDoAppTests
{
	[Fact(DisplayName = "An AppDirectory without index.html fails before anything starts")]
	public async Task Missing_Index_Fails_First()
	{
		var ambiente = new SemFrontEnd();

		var erro = await Should.ThrowAsync<DirectoryNotFoundException>(ambiente.InitializeAsync());

		erro.Message.ShouldContain("pasta-que-nao-existe");
		ambiente.ApiUrl.ShouldBeEmpty();
	}

	private sealed class SemFrontEnd : BrowserAppFixture<Program>
	{
		protected override string? AppDirectory => "pasta-que-nao-existe";
	}
}
#endif
