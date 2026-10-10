using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

using Xunit;

using XUnitAssured.Playwright.Configuration;
using XUnitAssured.Playwright.Testing;

namespace XUnitAssured.Playwright.AspNetCore;

/// <summary>
/// A browser, your ASP.NET Core API on a real port, and your built front-end on another — so
/// browser tests run with <c>dotnet test</c> alone, in CI, with real cross-origin rules.
/// </summary>
/// <typeparam name="TProgram">The API's entry point, as for <see cref="WebApplicationFactory{TEntryPoint}"/></typeparam>
/// <remarks>
/// <para>
/// The usual <see cref="WebApplicationFactory{TEntryPoint}"/> hosts the API in memory, where no
/// browser can reach it, so browser tests needed the API and the front-end started by hand —
/// three terminals — and stayed out of CI. Out of CI they go stale without anyone noticing: in a
/// real consumer, the front-end changed how it stores the session and a browser test kept passing
/// while provoking nothing.
/// </para>
/// <para>
/// The API and the front-end run on different ports on purpose: two origins, like an app and its
/// API in production, so SameSite, the cookie <c>Path</c> and CORS with credentials apply. The
/// API must allow the front-end's origin (<see cref="AppUrl"/>) — configure it in
/// <see cref="ConfigureApi"/> if the API does not already.
/// </para>
/// <code>
/// public sealed class MyApp : BrowserAppFixture&lt;Program&gt;
/// {
///     // The front-end was built with the API address baked in, so the API port is fixed.
///     protected override int ApiPort =&gt; 5199;
///     protected override int AppPort =&gt; 4173;
///     protected override string? AppDirectory =&gt; "../../../../my-app/dist";
///
///     protected override void ConfigureApi(IWebHostBuilder api) =&gt;
///         api.UseEnvironment("E2ETesting");
/// }
/// </code>
/// </remarks>
public class BrowserAppFixture<TProgram> : PlaywrightTestFixture, IAsyncLifetime
	where TProgram : class
{
	private WebApplicationFactory<TProgram>? _api;
	private WebApplication? _app;

	/// <summary>
	/// The API's port. 0 (the default) picks a free one; fix it when the front-end was built with
	/// the API address baked in.
	/// </summary>
	protected virtual int ApiPort => 0;

	/// <summary>
	/// The front-end's port. 0 (the default) picks a free one; fix it when the API only allows
	/// known origins.
	/// </summary>
	protected virtual int AppPort => 0;

	/// <summary>
	/// The built front-end (the folder with <c>index.html</c>), served with a fallback to
	/// <c>index.html</c> for client-side routes. Relative paths are resolved from the test's output
	/// directory. Null (the default) serves no front-end: the browser starts on the API.
	/// </summary>
	protected virtual string? AppDirectory => null;

	/// <summary>
	/// Configures the API host before it starts — environment, settings, services, CORS for
	/// <see cref="AppUrl"/>.
	/// </summary>
	/// <param name="api">The API's web host builder</param>
	protected virtual void ConfigureApi(IWebHostBuilder api) { }

	/// <summary>The API's root address, e.g. <c>http://localhost:5199</c>.</summary>
	public string ApiUrl { get; private set; } = string.Empty;

	/// <summary>
	/// The front-end's root address, e.g. <c>http://localhost:4173</c>; the API's when there is no
	/// front-end. It is the browser's base URL.
	/// </summary>
	public string AppUrl { get; private set; } = string.Empty;

	/// <summary>
	/// The API's factory, for what a browser cannot do: its services (<c>Api.Services</c>) and an
	/// <see cref="System.Net.Http.HttpClient"/> for arranging data (<c>Api.CreateClient()</c>).
	/// </summary>
	public WebApplicationFactory<TProgram> Api =>
		_api ?? throw new InvalidOperationException("The API starts in InitializeAsync; it is not running yet.");

	/// <summary>Starts the API, the front-end, and then the browser.</summary>
	public new async Task InitializeAsync()
	{
		// Antes de subir qualquer coisa: um AppDirectory errado não deve custar a partida da API.
		var pastaDoApp = AppDirectory == null ? null : PastaDoApp(AppDirectory);

		var portaDaApi = ApiPort == 0 ? PortaLivre() : ApiPort;
		_api = new FabricaDaApi(ConfigureApi);
		_api.UseKestrel(portaDaApi);
		_api.StartServer();
		ApiUrl = $"http://localhost:{portaDaApi}";

		AppUrl = ApiUrl;
		if (pastaDoApp != null)
		{
			var portaDoApp = AppPort == 0 ? PortaLivre() : AppPort;
			_app = ServirApp(pastaDoApp, portaDoApp);
			await _app.StartAsync();
			AppUrl = $"http://localhost:{portaDoApp}";
		}

		await base.InitializeAsync();
	}

	/// <summary>Closes the browser, the front-end and the API.</summary>
	public new async Task DisposeAsync()
	{
		await base.DisposeAsync();

		if (_app != null)
		{
			await _app.StopAsync();
			await _app.DisposeAsync();
		}

		_api?.Dispose();
	}

	/// <summary>
	/// The browser settings, with the base URL pointing at the front-end. Override to change other
	/// settings, starting from <c>base.CreateSettings()</c>.
	/// </summary>
	/// <returns>The browser settings</returns>
	protected override PlaywrightSettings CreateSettings()
	{
		var configuracao = base.CreateSettings();
		configuracao.BaseUrl = AppUrl;
		return configuracao;
	}

	// O app é só arquivo estático. Uma rota do cliente (/casos/42) não existe em disco e cai no
	// index.html, como num servidor de SPA de verdade.
	private static WebApplication ServirApp(string pasta, int porta)
	{
		var construtor = WebApplication.CreateSlimBuilder(new WebApplicationOptions { WebRootPath = pasta });
		construtor.WebHost.UseUrls($"http://localhost:{porta}");

		var app = construtor.Build();
		var arquivos = new PhysicalFileProvider(pasta);
		app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = arquivos });
		app.UseStaticFiles(new StaticFileOptions { FileProvider = arquivos });
		app.MapFallbackToFile("index.html", new StaticFileOptions { FileProvider = arquivos });
		return app;
	}

	// Uma subclasse, e não WithWebHostBuilder: na fábrica derivada que ele devolve o UseKestrel(porta)
	// não pega, e a API sobe na porta padrão (5000) em vez da escolhida.
	private sealed class FabricaDaApi(Action<IWebHostBuilder> configurar) : WebApplicationFactory<TProgram>
	{
		protected override void ConfigureWebHost(IWebHostBuilder builder) => configurar(builder);
	}

	private static string PastaDoApp(string caminho)
	{
		var pasta = Path.GetFullPath(Path.IsPathRooted(caminho) ? caminho : Path.Combine(AppContext.BaseDirectory, caminho));
		if (!File.Exists(Path.Combine(pasta, "index.html")))
			throw new DirectoryNotFoundException(
				$"No index.html in '{pasta}'. AppDirectory must point at the built front-end (e.g. dist).");
		return pasta;
	}

	// O Kestrel não aceita porta dinâmica em "localhost", e as origens precisam ser localhost --
	// é o que a API costuma liberar no CORS e o que o cookie enxerga. Então a porta livre é
	// escolhida aqui, antes de subir.
	private static int PortaLivre()
	{
		var escuta = new TcpListener(IPAddress.Loopback, 0);
		escuta.Start();
		var porta = ((IPEndPoint)escuta.LocalEndpoint).Port;
		escuta.Stop();
		return porta;
	}
}
