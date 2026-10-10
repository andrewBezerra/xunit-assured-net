using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

using XUnitAssured.Core.DSL;
using XUnitAssured.Playwright.Configuration;
using XUnitAssured.Playwright.Extensions;
using XUnitAssured.Playwright.Results;
using XUnitAssured.Playwright.Testing;

namespace XUnitAssured.Tests.PlaywrightTests;

/// <summary>
/// A rede vista pela página: interceptar, recarregar e fazer uma requisição de dentro dela.
///
/// <para>
/// Num consumidor real, os quatro testes de sessão no navegador usavam a API crua do Playwright,
/// porque a DSL não tinha nada disto. E um deles passou a não provocar cenário nenhum quando o
/// app mudou: zero requisições interceptadas, nada para falhar. O AssertIntercepted existe para
/// isso.
/// </para>
///
/// <para>
/// Com navegador de verdade e duas origens de verdade -- o "app" numa porta e a "API" noutra --,
/// porque SameSite, o Path do cookie e o CORS com credenciais só existem dentro de um navegador.
/// O servidor é um HttpListener local: nada de fora.
/// </para>
/// </summary>
[Trait("Category", "Playwright")]
[Trait("Requires", "Browser")]
public class RedeDaPaginaTests : IClassFixture<RedeDaPaginaTests.NavegadorComDuasOrigens>
{
	private readonly NavegadorComDuasOrigens _ambiente;

	public RedeDaPaginaTests(NavegadorComDuasOrigens ambiente) => _ambiente = ambiente;

	private string Api => _ambiente.Origens.Api;

	[Fact(DisplayName = "InterceptRoute answers the first requests and lets the rest reach the server")]
	public async Task Intercept_Answers_Then_Lets_Through()
	{
		await using var pagina = await _ambiente.OpenPageAsync();
		_ambiente.Origens.ZerarContagem();

		// A página pede /api/a e /api/b ao carregar. A rota responde só a primeira.
		var resultado = await ScenarioDsl.Given(pagina)
			.NavigateTo(_ambiente.Origens.App)
			.InterceptRoute($"{Api}/api/**", status: 401, times: 1)
			.Reload()
			.ExecuteAsync();

		resultado.Then().AssertIntercepted("/api/", atLeast: 1);
		resultado.GetResult().InterceptedRequests.Count.ShouldBe(1);

		// A outra seguiu para o servidor: além das duas da primeira carga, uma do reload.
		_ambiente.Origens.ChamadasDaApi.ShouldBe(3);
	}

	[Fact(DisplayName = "The route ends with its step, so the next step reaches the server")]
	public async Task Route_Ends_With_The_Step()
	{
		await using var pagina = await _ambiente.OpenPageAsync();

		await ScenarioDsl.Given(pagina)
			.NavigateTo(_ambiente.Origens.App)
			.InterceptRoute($"{Api}/api/**", status: 503, times: 10)
			.Reload()
			.ExecuteAsync();

		_ambiente.Origens.ZerarContagem();

		var seguinte = await ScenarioDsl.Given(pagina).Reload().ExecuteAsync();

		seguinte.GetResult().InterceptedRequests.ShouldBeEmpty();
		_ambiente.Origens.ChamadasDaApi.ShouldBe(2);
	}

	// O teste que um consumidor real não tinha: a rota não casou com nada, o app se comportou
	// normalmente, e a asserção final passava sem o cenário ter acontecido.
	[Fact(DisplayName = "AssertIntercepted fails when the route matched nothing")]
	public async Task Assert_Intercepted_Fails_When_Nothing_Matched()
	{
		await using var pagina = await _ambiente.OpenPageAsync();

		var resultado = await ScenarioDsl.Given(pagina)
			.NavigateTo(_ambiente.Origens.App)
			.InterceptRoute($"{Api}/rota-que-o-app-nao-chama/**", status: 401)
			.Reload()
			.ExecuteAsync();

		var erro = Should.Throw<ShouldAssertException>(() => resultado.Then().AssertIntercepted());
		erro.Message.ShouldContain("answered 0");
	}

	// Duas origens, cookie HttpOnly, CORS com credenciais: o login grava o cookie pela resposta
	// cross-origin, e a chamada seguinte só é aceita se o navegador o mandar de volta.
	[Fact(DisplayName = "FetchFromPage carries the page's cookies across origins")]
	public async Task Fetch_Carries_Cookies_Across_Origins()
	{
		await using var pagina = await _ambiente.OpenPageAsync();

		var resultado = await ScenarioDsl.Given(pagina)
			.NavigateTo(_ambiente.Origens.App)
			.FetchFromPage($"{Api}/login", "POST")
			.FetchFromPage($"{Api}/me")
			.ExecuteAsync();

		resultado.Then()
			.AssertFetchStatus(200)
			.AssertFetchJsonPath<string>("$.user", u => u == "ana", "the API should recognize the session cookie");

		resultado.GetResult().Fetches.First().Status.ShouldBe(204);
	}

	[Fact(DisplayName = "Without the cookie, the same request is refused")]
	public async Task Without_The_Cookie_It_Is_Refused()
	{
		await using var pagina = await _ambiente.OpenPageAsync();

		var resultado = await ScenarioDsl.Given(pagina)
			.NavigateTo(_ambiente.Origens.App)
			.FetchFromPage($"{Api}/me")
			.ExecuteAsync();

		resultado.Then().AssertFetchStatus(401);
	}

	// O formato de uma renovação de sessão: a segunda requisição leva o que a primeira devolveu.
	[Fact(DisplayName = "A deferred body carries a value an earlier step returned")]
	public async Task Deferred_Body_Carries_An_Earlier_Value()
	{
		await using var pagina = await _ambiente.OpenPageAsync();
		string? token = null;

		var resultado = await ScenarioDsl.Given(pagina)
			.NavigateTo(_ambiente.Origens.App)
			.FetchFromPage($"{Api}/echo", "POST", new { AccessToken = "primeiro" })
			.Validate((PlaywrightStepResult r) => token = r.LastFetch!.JsonPath<string>("$.accessToken") + "-renovado")
			.And()
			.FetchFromPage(() => $"{Api}/echo", "POST", () => new { AccessToken = token })
			.ExecuteAsync();

		// camelCase, como uma página mandaria.
		resultado.Then().AssertFetchJsonPath<string>("$.accessToken", t => t == "primeiro-renovado");
	}

	[Fact(DisplayName = "A request CORS refuses comes back with status 0, not as a success")]
	public async Task Cors_Refusal_Is_Status_Zero()
	{
		await using var pagina = await _ambiente.OpenPageAsync();

		var resultado = await ScenarioDsl.Given(pagina)
			.NavigateTo(_ambiente.Origens.App)
			.FetchFromPage($"{Api}/sem-cors")
			.ExecuteAsync();

		resultado.Then().AssertFetchStatus(0);
	}

	// ---------- Ambiente ----------

	/// <summary>Um navegador headless e as duas origens, uma vez por classe.</summary>
	// IAsyncLifetime de novo: o DisposeAsync da base não é virtual, e sem reimplementar a
	// interface o xUnit chamaria o da base -- e os listeners nunca fechariam.
	public sealed class NavegadorComDuasOrigens : PlaywrightTestFixture, IAsyncLifetime
	{
		public DuasOrigens Origens { get; } = new();

		protected override PlaywrightSettings CreateSettings() => new()
		{
			Headless = true,
			BaseUrl = Origens.App
		};

		public new async Task DisposeAsync()
		{
			await base.DisposeAsync();
			Origens.Dispose();
		}
	}

	/// <summary>
	/// O "app" numa porta e a "API" noutra, em localhost: mesmo site, origens diferentes, como
	/// app e API em produção. A API responde CORS com credenciais para a origem do app.
	/// </summary>
	public sealed class DuasOrigens : IDisposable
	{
		private readonly HttpListener _app = new();
		private readonly HttpListener _api = new();
		private int _chamadasDaApi;

		public string App { get; }
		public string Api { get; }
		public int ChamadasDaApi => Volatile.Read(ref _chamadasDaApi);

		public DuasOrigens()
		{
			App = $"http://localhost:{PortaLivre()}";
			Api = $"http://localhost:{PortaLivre()}";

			_app.Prefixes.Add(App + "/");
			_api.Prefixes.Add(Api + "/");
			_app.Start();
			_api.Start();

			_ = Atender(_app, ResponderApp);
			_ = Atender(_api, ResponderApi);
		}

		public void ZerarContagem() => Interlocked.Exchange(ref _chamadasDaApi, 0);

		private void ResponderApp(HttpListenerContext c)
		{
			switch (c.Request.Url!.AbsolutePath)
			{
				// Um app com service worker, como um PWA: o worker passa a responder pelos pedidos
				// à API -- que deixam de passar pelas rotas da página -- assim que controla a
				// página, a partir do primeiro reload.
				case "/pwa":
					Escrever(c, 200,
						$$"""
						<!doctype html><html><body><script>
						navigator.serviceWorker.register('/sw.js').catch(() => {});
						fetch('{{Api}}/api/a', { credentials: 'include' }).catch(() => {});
						</script></body></html>
						""", "text/html");
					break;
				case "/sw.js":
					Escrever(c, 200,
						"""
						self.addEventListener('install', () => self.skipWaiting());
						self.addEventListener('activate', e => e.waitUntil(self.clients.claim()));
						self.addEventListener('fetch', e => {
							if (e.request.url.includes('/api/')) e.respondWith(fetch(e.request));
						});
						""", "text/javascript");
					break;
				default:
					Escrever(c, 200,
						$$"""
						<!doctype html><html><body><script>
						fetch('{{Api}}/api/a', { credentials: 'include' }).catch(() => {});
						fetch('{{Api}}/api/b', { credentials: 'include' }).catch(() => {});
						</script></body></html>
						""", "text/html");
					break;
			}
		}

		private void ResponderApi(HttpListenerContext c)
		{
			var caminho = c.Request.Url!.AbsolutePath;

			if (caminho != "/sem-cors")
			{
				c.Response.AddHeader("Access-Control-Allow-Origin", App);
				c.Response.AddHeader("Access-Control-Allow-Credentials", "true");
				c.Response.AddHeader("Access-Control-Allow-Headers", "Content-Type");
				c.Response.AddHeader("Access-Control-Allow-Methods", "GET, POST");
			}

			if (c.Request.HttpMethod == "OPTIONS")
			{
				Escrever(c, 204, "");
				return;
			}

			switch (caminho)
			{
				case "/api/a" or "/api/b":
					Interlocked.Increment(ref _chamadasDaApi);
					Escrever(c, 200, """{"ok":true}""");
					break;
				case "/login":
					c.Response.AddHeader("Set-Cookie", "sid=abc; Path=/; HttpOnly; SameSite=Lax");
					Escrever(c, 204, "");
					break;
				case "/me":
					var cookie = c.Request.Headers["Cookie"] ?? "";
					if (cookie.Contains("sid=abc"))
						Escrever(c, 200, """{"user":"ana"}""");
					else
						Escrever(c, 401, """{"error":"no session"}""");
					break;
				case "/echo":
					using (var leitor = new StreamReader(c.Request.InputStream, Encoding.UTF8))
						Escrever(c, 200, leitor.ReadToEnd());
					break;
				default:
					Escrever(c, 200, """{"ok":true}""");
					break;
			}
		}

		private static async Task Atender(HttpListener ouvinte, Action<HttpListenerContext> responder)
		{
			while (ouvinte.IsListening)
			{
				HttpListenerContext contexto;
				try
				{
					contexto = await ouvinte.GetContextAsync();
				}
				catch (Exception) when (!ouvinte.IsListening)
				{
					return;
				}
				catch (HttpListenerException)
				{
					return;
				}

				try
				{
					responder(contexto);
				}
				catch (Exception)
				{
					try { contexto.Response.Abort(); } catch { }
				}
			}
		}

		private static void Escrever(HttpListenerContext c, int status, string corpo, string tipo = "application/json")
		{
			c.Response.StatusCode = status;
			if (corpo.Length > 0)
			{
				var bytes = Encoding.UTF8.GetBytes(corpo);
				c.Response.ContentType = tipo;
				c.Response.ContentLength64 = bytes.Length;
				c.Response.OutputStream.Write(bytes);
			}
			c.Response.Close();
		}

		private static int PortaLivre()
		{
			var escuta = new TcpListener(IPAddress.Loopback, 0);
			escuta.Start();
			var porta = ((IPEndPoint)escuta.LocalEndpoint).Port;
			escuta.Stop();
			return porta;
		}

		public void Dispose()
		{
			_app.Close();
			_api.Close();
		}
	}
}
