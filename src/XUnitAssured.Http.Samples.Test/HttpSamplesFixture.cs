using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using XUnitAssured.Core.Abstractions;

namespace XUnitAssured.Http.Samples.Test;

/// <summary>
/// Test fixture for HTTP samples using XUnitAssured.Http.
/// Hosts the SampleWebApi application for integration testing.
/// Does not depend on external configuration files.
/// Implements IHttpClientProvider to work seamlessly with Given(fixture) syntax.
/// </summary>
public class HttpSamplesFixture : IHttpClientProvider, IDisposable
{
	private readonly WebApplicationFactory<Program> _factory;
	private bool _disposed;

	public HttpSamplesFixture()
	{
		// Create the web application factory with proper configuration
		_factory = new WebApplicationFactory<Program>()
			.WithWebHostBuilder(builder =>
			{
				// Set environment to Testing
				builder.UseEnvironment("Testing");

				// Configure app configuration
				builder.ConfigureAppConfiguration((context, config) =>
				{
					// Clear existing sources
					config.Sources.Clear();

					// Add in-memory configuration instead of relying on external files
					config.AddInMemoryCollection(new Dictionary<string, string?>
					{
						["AllowedHosts"] = "*",
						["Logging:LogLevel:Default"] = "Information",
						["Logging:LogLevel:Microsoft.AspNetCore"] = "Warning"
					});
				});

				// Configure test services
				builder.ConfigureServices(services =>
				{
					// Suppress logging in tests to reduce noise
					services.AddLogging(logging =>
					{
						logging.ClearProviders();
						logging.SetMinimumLevel(LogLevel.Warning);
					});
				});

			});

		// Create a client to ensure the server is started
		var client = _factory.CreateClient();

		// Get the base URL from the test server
		BaseUrl = client.BaseAddress?.ToString().TrimEnd('/') ?? "https://localhost";
	}

	/// <summary>
	/// Base URL of the test server.
	/// </summary>
	public string BaseUrl { get; }

	/// <summary>
	/// Creates an HttpClient for direct API calls if needed.
	/// </summary>
	public HttpClient CreateClient() => _factory.CreateClient();

	/// <summary>
	/// Dispose resources.
	/// </summary>
	public void Dispose()
	{
		Dispose(true);
		GC.SuppressFinalize(this);
	}

	protected virtual void Dispose(bool disposing)
	{
		if (_disposed)
			return;

		if (disposing)
		{
			_factory?.Dispose();
		}

		_disposed = true;
	}
}
