using System.Threading.Tasks;

using Shouldly;
using Xunit;

using XUnitAssured.Core.DSL;
using XUnitAssured.Playwright.Configuration;
using XUnitAssured.Playwright.Extensions;
using XUnitAssured.Playwright.Testing;

namespace XUnitAssured.Tests.PlaywrightTests;

/// <summary>
/// Um service worker tira os pedidos do alcance do InterceptRoute; o BlockServiceWorkers devolve.
///
/// <para>
/// Achado migrando um consumidor real para a 6.4.0: o app é um PWA, o worker atende os pedidos
/// de dado (rede primeiro, cache de reserva), e o InterceptRoute respondia zero depois do
/// reload -- os pedidos passavam pelo worker, que a rota da página não enxerga. Os dois testes
/// fazem o mesmo; só o ajuste muda.
/// </para>
/// </summary>
[Trait("Category", "Playwright")]
[Trait("Requires", "Browser")]
public class ServiceWorkerTests :
	IClassFixture<ServiceWorkerTests.ComWorker>,
	IClassFixture<ServiceWorkerTests.SemWorker>
{
	private readonly ComWorker _comWorker;
	private readonly SemWorker _semWorker;

	public ServiceWorkerTests(ComWorker comWorker, SemWorker semWorker)
	{
		_comWorker = comWorker;
		_semWorker = semWorker;
	}

	[Fact(DisplayName = "With a service worker in control, the route answers nothing")]
	public async Task Worker_Takes_Requests_Out_Of_The_Route()
	{
		await using var pagina = await _comWorker.OpenPageAsync();
		await PrepararAsync(pagina, _comWorker);

		// Prova de que o worker está no caminho: sem isto o teste passaria também se ele nunca
		// tivesse se registrado.
		(await pagina.Page.EvaluateAsync<bool>("!!navigator.serviceWorker.controller")).ShouldBeTrue();

		var resultado = await InterceptarNoReloadAsync(pagina, _comWorker);

		var erro = Should.Throw<ShouldAssertException>(() => resultado.Then().AssertIntercepted());
		erro.Message.ShouldContain("BlockServiceWorkers");
	}

	[Fact(DisplayName = "BlockServiceWorkers puts the requests back in the route's reach")]
	public async Task Blocking_Brings_The_Requests_Back()
	{
		await using var pagina = await _semWorker.OpenPageAsync();
		await PrepararAsync(pagina, _semWorker);

		(await pagina.Page.EvaluateAsync<bool>("!!navigator.serviceWorker.controller")).ShouldBeFalse();

		var resultado = await InterceptarNoReloadAsync(pagina, _semWorker);

		resultado.Then().AssertIntercepted("/api/a");
	}

	// A primeira carga registra o worker; ele só controla a página depois de pronto. Esperar o
	// "ready" e recarregar uma vez deixa a página controlada antes do cenário.
	private static async Task PrepararAsync(PlaywrightPageSession pagina, Ambiente ambiente)
	{
		await pagina.Page.GotoAsync(ambiente.Origens.App + "/pwa");
		await pagina.Page.EvaluateAsync(
			"() => Promise.race([navigator.serviceWorker.ready, new Promise(r => setTimeout(r, 2000))])");
		await pagina.Page.ReloadAsync();
	}

	private static Task<Playwright.Extensions.PlaywrightValidationBuilder> InterceptarNoReloadAsync(
		PlaywrightPageSession pagina, Ambiente ambiente) =>
		ScenarioDsl.Given(pagina)
			.InterceptRoute($"{ambiente.Origens.Api}/api/**", status: 401, times: 5)
			.Reload()
			.ExecuteAsync();

	// ---------- Ambiente ----------

	public abstract class Ambiente : PlaywrightTestFixture, IAsyncLifetime
	{
		public RedeDaPaginaTests.DuasOrigens Origens { get; } = new();

		protected abstract bool Bloquear { get; }

		protected override PlaywrightSettings CreateSettings() => new()
		{
			Headless = true,
			BaseUrl = Origens.App,
			BlockServiceWorkers = Bloquear
		};

		public new async Task DisposeAsync()
		{
			await base.DisposeAsync();
			Origens.Dispose();
		}
	}

	public sealed class ComWorker : Ambiente
	{
		protected override bool Bloquear => false;
	}

	public sealed class SemWorker : Ambiente
	{
		protected override bool Bloquear => true;
	}
}
