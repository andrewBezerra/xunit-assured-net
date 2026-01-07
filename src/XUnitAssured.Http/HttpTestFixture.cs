using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

using Flurl.Http;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Xunit.Microsoft.DependencyInjection;
using Xunit.Microsoft.DependencyInjection.Abstracts;

using XUnitAssured.Http.Configuration;
using XUnitAssured.Http.Handlers;

namespace XUnitAssured.Http;

/// <summary>
/// Base test fixture for HTTP/REST API integration tests.
/// Provides HttpSettings configuration, FlurlClient setup, and authentication handlers.
/// Does not depend on XUnitAssured.Base.TestFixture, making it independent and reusable.
/// </summary>
public abstract class HttpTestFixture : TestBedFixture
{
	protected HttpSettings? HttpSettings { get; private set; }
	protected IFlurlClient? FlurlClient { get; private set; }

	protected override void AddServices(IServiceCollection services, IConfiguration? configuration)
	{
		if (configuration == null)
		{
			throw new InvalidOperationException("Configuration is required for HttpTestFixture.");
		}

		// Configure HttpSettings
		var httpSection = configuration.GetSection("Http");
		services.AddOptions<HttpSettings>()
			.Bind(httpSection);
			// .ValidateOnStart(); // Commented: Ambiguous with ASP.NET Core dependencies

		// Build a temporary provider to get HttpSettings
		using var tempProvider = services.BuildServiceProvider();
		HttpSettings = tempProvider.GetRequiredService<IOptions<HttpSettings>>().Value;

		// Configure HttpClient and FlurlClient
		ConfigureHttpClient(services);

		// Register authentication handlers
		RegisterAuthenticationHandlers(services);

		// Allow derived classes to add more services
		AddHttpServices(services, configuration);
	}

	/// <summary>
	/// Configures HttpClient and FlurlClient based on HttpSettings.
	/// </summary>
	private void ConfigureHttpClient(IServiceCollection services)
	{
		if (HttpSettings == null)
		{
			throw new InvalidOperationException("HttpSettings not initialized.");
		}

		// Create HttpClientHandler
		var httpClientHandler = new HttpClientHandler();

		// Configure certificate authentication if specified
		if (HttpSettings.Authentication?.Type == AuthenticationType.Certificate)
		{
			var certConfig = HttpSettings.Authentication.Certificate;
			if (certConfig != null)
			{
				X509Certificate2? certificate = null;

				if (!string.IsNullOrWhiteSpace(certConfig.CertificatePath))
				{
					// Load from file
					certificate = string.IsNullOrWhiteSpace(certConfig.CertificatePassword)
						? new X509Certificate2(certConfig.CertificatePath)
						: new X509Certificate2(certConfig.CertificatePath, certConfig.CertificatePassword);
				}
				else if (!string.IsNullOrWhiteSpace(certConfig.Thumbprint))
				{
					// Load from store
					using var store = new X509Store(certConfig.StoreName, certConfig.StoreLocation);
					store.Open(OpenFlags.ReadOnly);
					var certs = store.Certificates.Find(
						X509FindType.FindByThumbprint,
						certConfig.Thumbprint,
						validOnly: false);

					if (certs.Count > 0)
					{
						certificate = certs[0];
					}
					else
					{
						throw new InvalidOperationException(
							$"Certificate with thumbprint '{certConfig.Thumbprint}' not found in store.");
					}
				}

				if (certificate != null)
				{
					httpClientHandler.ClientCertificates.Add(certificate);
				}
			}
		}

		// Create HttpClient
		var httpClient = new HttpClient(httpClientHandler);

		// Set base address if configured
		if (!string.IsNullOrWhiteSpace(HttpSettings.BaseUrl))
		{
			httpClient.BaseAddress = new Uri(HttpSettings.BaseUrl);
		}

		// Set timeout
		httpClient.Timeout = TimeSpan.FromSeconds(HttpSettings.Timeout);

		// Add default headers
		if (HttpSettings.DefaultHeaders != null)
		{
			foreach (var header in HttpSettings.DefaultHeaders)
			{
				httpClient.DefaultRequestHeaders.Add(header.Key, header.Value);
			}
		}

		// Create FlurlClient
		FlurlClient = new FlurlClient(httpClient);

		// Register in DI
		services.AddSingleton(httpClient);
		services.AddSingleton(FlurlClient);
		services.AddSingleton<IFlurlClient>(FlurlClient);
	}

	/// <summary>
	/// Registers authentication handlers in DI container.
	/// </summary>
	private void RegisterAuthenticationHandlers(IServiceCollection services)
	{
		services.AddSingleton<IAuthenticationHandler, BasicAuthHandler>();
		services.AddSingleton<IAuthenticationHandler, BearerAuthHandler>();
		services.AddSingleton<IAuthenticationHandler, ApiKeyAuthHandler>();
		services.AddSingleton<IAuthenticationHandler, OAuth2Handler>();
		services.AddSingleton<IAuthenticationHandler, CustomHeaderAuthHandler>();
		services.AddSingleton<IAuthenticationHandler, CertificateAuthHandler>();
	}

	/// <summary>
	/// Override this method to add additional services specific to your HTTP tests.
	/// </summary>
	/// <param name="services">Service collection</param>
	/// <param name="configuration">Configuration</param>
	protected virtual void AddHttpServices(IServiceCollection services, IConfiguration configuration)
	{
		// Default: no additional services
	}

	/// <summary>
	/// Gets the configured FlurlClient for making HTTP requests.
	/// </summary>
	/// <returns>Configured FlurlClient</returns>
	public IFlurlClient GetFlurlClient()
	{
		if (FlurlClient == null)
		{
			throw new InvalidOperationException("FlurlClient not initialized. Ensure AddServices was called.");
		}

		return FlurlClient;
	}

	/// <summary>
	/// Gets the configured HttpSettings.
	/// </summary>
	/// <returns>HttpSettings instance</returns>
	public HttpSettings GetHttpSettings()
	{
		if (HttpSettings == null)
		{
			throw new InvalidOperationException("HttpSettings not initialized. Ensure AddServices was called.");
		}

		return HttpSettings;
	}

	protected override ValueTask DisposeAsyncCore()
	{
		// Dispose FlurlClient
		FlurlClient?.Dispose();

		// Clean up any other resources
		return ValueTask.CompletedTask;
	}

	protected override IEnumerable<TestAppSettings> GetTestAppSettings()
	{
		yield return new() { Filename = "httpsettings.json", IsOptional = false };
	}
}
