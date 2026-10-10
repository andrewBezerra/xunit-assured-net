using System;
using System.Net.Http;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

using XUnitAssured.Core.Abstractions;
using XUnitAssured.Core.Logging;
using XUnitAssured.Http.Configuration;

namespace XUnitAssured.Http.AspNetCore;

/// <summary>
/// Your ASP.NET Core API, hosted in memory with <see cref="WebApplicationFactory{TEntryPoint}"/>,
/// ready for <c>Given(fixture)</c> and <c>HttpTestBase</c>: a shared client, a new client per
/// identity, clients with and without cookies, and the API's services.
/// </summary>
/// <typeparam name="TProgram">The API's entry point, as for <see cref="WebApplicationFactory{TEntryPoint}"/></typeparam>
/// <remarks>
/// <para>
/// <b>Share one instance across the suite with a collection fixture</b>, not an
/// <c>IClassFixture</c> per test class. Each instance starts the API once — and starting it runs
/// whatever the API does on start, database migrations included. In a real consumer suite with an
/// <c>IClassFixture</c> per class, the first test of every class took 2–6 s and the rest
/// 0.05–0.2 s: the startups were about 70 of the suite's 118 s. One shared instance brought it to
/// about a minute, with no test changes.
/// </para>
/// <code>
/// public sealed class MyApi : ApiFixture&lt;Program&gt;
/// {
///     protected override void ConfigureApi(IWebHostBuilder api) =&gt;
///         api.UseEnvironment("Testing");
///
///     // Who the shared client is: here, a test-only header the API reads in Testing.
///     protected override void ConfigureClient(HttpClient client) =&gt;
///         client.DefaultRequestHeaders.Add("X-Test-User", "admin");
/// }
///
/// [CollectionDefinition("API")]
/// public sealed class ApiCollection : ICollectionFixture&lt;MyApi&gt;;
///
/// [Collection("API")]
/// public class OrderTests(MyApi api) : HttpTestBase&lt;MyApi&gt;(api)
/// {
///     [Fact]
///     public async Task Reader_Cannot_Delete() =&gt;
///         (await Given()
///             .WithHttpClient(Fixture.ClientFor(("X-Test-User", "reader")))
///             .ApiResource("/orders/1")
///             .Delete()
///             .ExecuteAsync())
///         .Then().AssertStatusCode(403);
/// }
/// </code>
/// <para>
/// Sharing is safe when each test creates the data it reads and the tests in the collection run one
/// at a time, as xUnit runs the tests of a collection. A test that changes how the API is
/// configured needs its own instance: derive from the fixture and give it its own collection, or
/// create one inside the test with <c>using</c>.
/// </para>
/// </remarks>
public class ApiFixture<TProgram> : IHttpClientProvider, IHttpClientAuthProvider, IDisposable
	where TProgram : class
{
	private readonly FabricaDaApi _api;
	private HttpClient? _cliente;
	private LogCapture? _logs;
	private bool _descartado;

	/// <summary>Prepares the API; it starts with the first client or the first use of its services.</summary>
	public ApiFixture()
	{
		_api = new FabricaDaApi(ConfigurarHost);
	}

	/// <summary>
	/// Configures the API host before it starts — environment, settings, services. Not called
	/// before the first client is created, so a derived fixture's fields are already set.
	/// </summary>
	/// <param name="api">The API's web host builder</param>
	protected virtual void ConfigureApi(IWebHostBuilder api) { }

	/// <summary>
	/// Gives every client this fixture creates its identity — headers, usually. Runs once for the
	/// shared client and once for each new one, before <see cref="ClientFor"/> applies its headers.
	/// </summary>
	/// <param name="client">A client that has not sent anything yet</param>
	protected virtual void ConfigureClient(HttpClient client) { }

	/// <summary>
	/// The authentication applied to every request of a <c>Given(fixture)</c> scenario, from the
	/// configuration types of <c>XUnitAssured.Http</c>. Null (the default) applies none; an API
	/// that tells test users apart by header needs only <see cref="ConfigureClient"/>.
	/// </summary>
	protected virtual HttpAuthConfig? Authentication => null;

	/// <summary>
	/// True records everything the API logs, at every level, in <see cref="Logs"/> — whatever the
	/// API logs with (Microsoft.Extensions.Logging, Serilog), and without changing what it writes
	/// to the console. False (the default) records nothing: a suite-wide host would otherwise keep
	/// every entry of every test.
	/// </summary>
	protected virtual bool CaptureLogs => false;

	/// <summary>
	/// The API's factory, for what this fixture does not cover — <c>Api.Server</c>, or a client with
	/// other <see cref="WebApplicationFactoryClientOptions"/>.
	/// </summary>
	public WebApplicationFactory<TProgram> Api => _api;

	/// <summary>The API's services, for the checks no endpoint shows. Starts the API.</summary>
	public IServiceProvider Services => _api.Services;

	/// <summary>
	/// What the API logged since the last <see cref="LogCapture.Clear"/>. Call it right before the
	/// action under test: a shared host sees every test's entries.
	/// </summary>
	/// <exception cref="InvalidOperationException">When <see cref="CaptureLogs"/> is false</exception>
	public LogCapture Logs =>
		CaptureLogs
			? _logs ??= new LogCapture()
			: throw new InvalidOperationException(
				"This fixture does not capture logs. Override CaptureLogs to return true.");

	/// <summary>
	/// A scope of the API's services, for a test that checks the database directly — the guarantee
	/// under test is the schema's, not an endpoint's. Dispose it with <c>using</c>.
	/// </summary>
	/// <returns>A new service scope</returns>
	public IServiceScope CreateScope() => _api.Services.CreateScope();

	/// <summary>
	/// The shared client, the one <c>Given(fixture)</c> uses: created once, with
	/// <see cref="ConfigureClient"/> applied. It keeps cookies, as <see cref="WebApplicationFactory{TEntryPoint}"/>
	/// clients do by default. Do not change its headers in a test — the next test would inherit
	/// them; ask for another client with <see cref="ClientFor"/>.
	/// </summary>
	/// <returns>The shared client</returns>
	public HttpClient CreateClient() => _cliente ??= NovoCliente(guardaCookies: true);

	/// <summary>
	/// A new client as someone else: <see cref="ConfigureClient"/> first, then these headers, each
	/// replacing a header of the same name. Leaves the shared client as it was.
	/// </summary>
	/// <param name="headers">The headers that make the identity, e.g. <c>("X-Test-User", "reader")</c></param>
	/// <returns>A new client, which keeps cookies</returns>
	public HttpClient ClientFor(params (string Name, string Value)[] headers)
	{
		var cliente = NovoCliente(guardaCookies: true);
		foreach (var (nome, valor) in headers)
		{
			cliente.DefaultRequestHeaders.Remove(nome);
			cliente.DefaultRequestHeaders.TryAddWithoutValidation(nome, valor);
		}
		return cliente;
	}

	/// <summary>
	/// A new client that keeps cookies, as a browser does — for a session that lives in a cookie,
	/// which the shared client would carry from one test into the next.
	/// </summary>
	/// <returns>A new client with its own cookie jar</returns>
	public HttpClient ClientWithCookies() => NovoCliente(guardaCookies: true);

	/// <summary>
	/// A new client that never keeps cookies, to see a session from outside: someone who never
	/// signed in, or who replays a copied cookie in a <c>Cookie</c> header.
	/// </summary>
	/// <returns>A new client without a cookie jar</returns>
	public HttpClient ClientWithoutCookies() => NovoCliente(guardaCookies: false);

	HttpAuthConfig? IHttpClientAuthProvider.GetAuthenticationConfig() => Authentication;

	/// <summary>Stops the API and disposes every client it created.</summary>
	public void Dispose()
	{
		Dispose(true);
		GC.SuppressFinalize(this);
	}

	/// <summary>Stops the API; override to release what a derived fixture holds, then call the base.</summary>
	/// <param name="disposing">True when called from <see cref="Dispose()"/></param>
	protected virtual void Dispose(bool disposing)
	{
		if (_descartado)
			return;

		// O factory descarta os clientes que criou, o compartilhado entre eles.
		if (disposing)
			_api.Dispose();

		_descartado = true;
	}

	private HttpClient NovoCliente(bool guardaCookies)
	{
		var cliente = _api.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = guardaCookies });
		ConfigureClient(cliente);
		return cliente;
	}

	private void ConfigurarHost(IWebHostBuilder host)
	{
		ConfigureApi(host);

		if (CaptureLogs)
		{
			var logs = Logs;
			// Por fora do ILoggerFactory, e não como mais um provedor: o UseSerilog troca a fábrica
			// e ignora os provedores registrados, e os níveis do ambiente cortariam o que o teste
			// quer ver. Depois dos serviços da API, para envolver a fábrica que ela escolheu.
			host.ConfigureTestServices(servicos => FabricaQueCaptura.Envolver(servicos, logs));
		}
	}

	// Uma subclasse, e não WithWebHostBuilder: assim o host é configurado pelos métodos virtuais
	// do fixture, quando ele já está construído, e o Api exposto é o próprio factory que sobe.
	private sealed class FabricaDaApi(Action<IWebHostBuilder> configurar) : WebApplicationFactory<TProgram>
	{
		protected override void ConfigureWebHost(IWebHostBuilder builder) => configurar(builder);
	}
}
