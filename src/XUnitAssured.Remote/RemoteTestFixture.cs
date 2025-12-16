using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

using Flurl.Http;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Xunit.Microsoft.DependencyInjection;

using XUnitAssured.Base;

namespace XUnitAssured.Remote;

/// <summary>
/// Base test fixture for remote application testing (Web APIs and Kafka).
/// Provides RemoteSettings configuration and HTTP client setup.
/// </summary>
public abstract class RemoteTestFixture : TestFixture
{
	protected RemoteSettings? RemoteSettings { get; private set; }
	protected FlurlClient? WebClient { get; private set; }

	protected override void AddServices(IServiceCollection services, IConfiguration? configuration)
	{
		if (configuration == null)
		{
			throw new InvalidOperationException("Configuration is required for RemoteTestFixture.");
		}

		// Configure RemoteSettings with validation
		var remoteSection = configuration.GetSection(RemoteSettings.SectionName);
		services.Configure<RemoteSettings>(remoteSection);
		services.AddOptions<RemoteSettings>().ValidateOnStart();

		// Register validator
		services.AddSingleton<IValidateOptions<RemoteSettings>, RemoteSettingsValidator>();

		// Build a temporary provider to get RemoteSettings
		var tempProvider = services.BuildServiceProvider();
		RemoteSettings = tempProvider.GetRequiredService<IOptions<RemoteSettings>>().Value;
		tempProvider.Dispose();

		// Configure HttpClient and FlurlClient
		ConfigureHttpClient(services);

		// Allow derived classes to add more services
		AddRemoteServices(services, configuration);
	}

	/// <summary>
	/// Configures HttpClient and FlurlClient based on RemoteSettings.
	/// </summary>
	private void ConfigureHttpClient(IServiceCollection services)
	{
		if (RemoteSettings == null)
		{
			throw new InvalidOperationException("RemoteSettings not initialized.");
		}

		// Create HttpClient with base configuration
		var httpClientHandler = new HttpClientHandler();

		// Allow invalid SSL certificates if configured (for dev/test environments)
		if (RemoteSettings.AllowInvalidSslCertificates)
		{
			httpClientHandler.ServerCertificateCustomValidationCallback = 
				(message, cert, chain, sslPolicyErrors) => true;
		}

		var httpClient = new HttpClient(httpClientHandler)
		{
			BaseAddress = RemoteSettings.GetBaseUri(),
			Timeout = RemoteSettings.GetTimeout()
		};

		// Add default headers
		if (RemoteSettings.DefaultHeaders != null)
		{
			foreach (var header in RemoteSettings.DefaultHeaders)
			{
				httpClient.DefaultRequestHeaders.Add(header.Key, header.Value);
			}
		}

		// Add authentication header
		if (RemoteSettings.Authentication != null)
		{
			var authHeader = RemoteSettings.Authentication.GetAuthenticationHeader();
			if (authHeader != null)
			{
				httpClient.DefaultRequestHeaders.Authorization = authHeader;
			}

			// Handle API Key authentication (not standard Authorization header)
			if (RemoteSettings.Authentication.Type == AuthenticationType.ApiKey)
			{
				httpClient.DefaultRequestHeaders.Add(
					RemoteSettings.Authentication.ApiKeyHeaderName!,
					RemoteSettings.Authentication.ApiKey!);
			}

			// Handle Custom authentication
			if (RemoteSettings.Authentication.Type == AuthenticationType.Custom)
			{
				httpClient.DefaultRequestHeaders.Add(
					RemoteSettings.Authentication.CustomHeaderName!,
					RemoteSettings.Authentication.CustomHeaderValue!);
			}
		}

		// Create FlurlClient from HttpClient
		WebClient = new FlurlClient(httpClient);

		// Register HttpClient and FlurlClient in DI
		services.AddSingleton(httpClient);
		services.AddSingleton(WebClient);
	}

	/// <summary>
	/// Override this method to add additional services specific to your remote tests.
	/// </summary>
	/// <param name="services">Service collection</param>
	/// <param name="configuration">Configuration</param>
	protected virtual void AddRemoteServices(IServiceCollection services, IConfiguration configuration)
	{
		// Default: no additional services
	}

	/// <summary>
	/// Gets the configured FlurlClient for making HTTP requests.
	/// </summary>
	/// <returns>Configured FlurlClient</returns>
	/// <exception cref="InvalidOperationException">Thrown if WebClient is not initialized</exception>
	public FlurlClient GetWebClient()
	{
		if (WebClient == null)
		{
			throw new InvalidOperationException("WebClient not initialized. Ensure AddServices was called.");
		}

		return WebClient;
	}

	/// <summary>
	/// Gets the configured RemoteSettings.
	/// </summary>
	/// <returns>RemoteSettings instance</returns>
	/// <exception cref="InvalidOperationException">Thrown if RemoteSettings is not initialized</exception>
	public RemoteSettings GetRemoteSettings()
	{
		if (RemoteSettings == null)
		{
			throw new InvalidOperationException("RemoteSettings not initialized. Ensure AddServices was called.");
		}

		return RemoteSettings;
	}

	protected override async ValueTask DisposeAsyncCore()
	{
		// Dispose WebClient
		WebClient?.Dispose();
		
		// Clean up any other resources
		await base.DisposeAsyncCore();
	}

	protected override IEnumerable<TestAppSettings> GetTestAppSettings()
	{
		yield return new() { Filename = "remotesettings.json", IsOptional = false };
	}
}
