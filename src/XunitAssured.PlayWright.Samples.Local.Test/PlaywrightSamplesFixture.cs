using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using SampleWebApp.Data;

using XUnitAssured.Playwright.Configuration;
using XUnitAssured.Playwright.Testing;

namespace XunitAssured.PlayWright.Samples.Local.Test;

/// <summary>
/// Custom WebApplicationFactory that uses Kestrel instead of TestServer.
/// This allows Playwright (a real browser) to connect via HTTP to the app.
/// </summary>
internal class KestrelWebApplicationFactory : WebApplicationFactory<Program>
{
	private IHost? _realHost;
	private string? _serverUrl;

	/// <summary>
	/// The actual URL where Kestrel is listening (e.g., http://127.0.0.1:12345).
	/// </summary>
	public string ServerUrl => _serverUrl ?? throw new InvalidOperationException("Server not started");

	protected override IHost CreateHost(IHostBuilder builder)
	{
		// 1. Create a "dummy" host with TestServer to satisfy WebApplicationFactory's
		//    internal cast: _server = (TestServer)host.Services.GetRequiredService<IServer>()
		var dummyBuilder = new HostBuilder();
		dummyBuilder.ConfigureWebHost(wb =>
		{
			wb.UseTestServer();
			wb.Configure(app => { });
		});
		var dummyHost = dummyBuilder.Build();
		dummyHost.Start();

		// 2. Build the REAL host with Kestrel using the fully-configured builder
		//    (which has all of SampleWebApp's services + our test overrides)
		builder.ConfigureWebHost(wb =>
		{
			wb.UseKestrel();
			wb.UseUrls("http://127.0.0.1:0");
		});

		_realHost = builder.Build();
		_realHost.Start();

		// Retrieve the dynamically assigned port
		var server = _realHost.Services.GetRequiredService<IServer>();
		_serverUrl = server.Features.Get<IServerAddressesFeature>()!.Addresses.First();

		// Return dummyHost so WebApplicationFactory's TestServer cast succeeds
		return dummyHost;
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			_realHost?.StopAsync().GetAwaiter().GetResult();
			_realHost?.Dispose();
		}
		base.Dispose(disposing);
	}
}

/// <summary>
/// Test fixture for Playwright E2E tests against SampleWebApp.
/// Hosts the Blazor application on a real Kestrel TCP port so Playwright can connect to it.
/// Replaces SQL Server with in-memory database for test isolation.
/// </summary>
public class PlaywrightSamplesFixture : PlaywrightTestFixture, IDisposable
{
	private readonly KestrelWebApplicationFactory _factory;
	private readonly WebApplicationFactory<Program> _configuredFactory;
	private readonly string _baseUrl;
	private bool _disposed;

	public PlaywrightSamplesFixture()
	{
		_factory = new KestrelWebApplicationFactory();

		// Configure the web host.
		// WithWebHostBuilder returns a NEW factory with the configuration applied.
		// We must use the returned factory to trigger host creation so the overrides take effect.
		// The returned factory delegates CreateHost to the original _factory,
		// which sets up the Kestrel server and stores the server URL.
		_configuredFactory = _factory.WithWebHostBuilder(builder =>
		{
			builder.UseEnvironment("Development");

			// Provide a fake connection string so Program.cs doesn't throw
			builder.ConfigureAppConfiguration((context, config) =>
			{
				config.AddInMemoryCollection(new Dictionary<string, string?>
				{
					["ConnectionStrings:DefaultConnection"] = "DataSource=:memory:"
				});
			});

			builder.ConfigureServices(services =>
			{
				// Remove the real SQL Server DbContext registration
				var descriptor = services.SingleOrDefault(
					d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
				if (descriptor != null)
					services.Remove(descriptor);

				// Add in-memory database for testing
				services.AddDbContext<ApplicationDbContext>(options =>
					options.UseInMemoryDatabase("PlaywrightTestDb"));

				services.AddLogging(logging =>
				{
					logging.ClearProviders();
					logging.SetMinimumLevel(LogLevel.Warning);
				});
			});
		});

		// Trigger host creation through the configured factory (with test overrides applied)
		_ = _configuredFactory.Services;

		_baseUrl = _factory.ServerUrl;
	}

	/// <summary>
	/// The base URL of the hosted SampleWebApp test server.
	/// </summary>
	public string BaseUrl => _baseUrl;

	/// <summary>
	/// Provides PlaywrightSettings pointing to the hosted SampleWebApp on a real port.
	/// Loads trace/video/screenshot settings from playwrightsettings.json,
	/// then overrides BaseUrl with the dynamically assigned test server URL.
	/// </summary>
	protected override PlaywrightSettings CreateSettings()
	{
		PlaywrightSettingsLoader.ClearCache();
		var settings = PlaywrightSettings.Load();
		settings.BaseUrl = _baseUrl;
		settings.IgnoreHttpsErrors = true;
		return settings;
	}

	/// <summary>
	/// Disposes the WebApplicationFactory and browser resources.
	/// </summary>
	public new void Dispose()
	{
		if (!_disposed)
		{
			_configuredFactory.Dispose();
			_factory.Dispose();
			_disposed = true;
		}
		base.Dispose();
		GC.SuppressFinalize(this);
	}
}
