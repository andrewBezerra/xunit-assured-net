using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using XUnitAssured.Core.Abstractions;
using XUnitAssured.Http.Configuration;
using XUnitAssured.Http.Steps;

namespace XUnitAssured.Http.Extensions;

/// <summary>
/// Extension methods for configuring authentication in the fluent DSL.
/// </summary>
public static class AuthenticationExtensions
{
	/// <summary>
	/// Configures Basic authentication for the HTTP request.
	/// Automatically encodes username:password in Base64.
	/// Usage: .WithBasicAuth("username", "password")
	/// </summary>
	/// <param name="scenario">Test scenario</param>
	/// <param name="username">Username</param>
	/// <param name="password">Password</param>
	/// <returns>Test scenario for chaining</returns>
	public static ITestScenario WithBasicAuth(this ITestScenario scenario, string username, string password)
	{
		if (scenario == null)
			throw new ArgumentNullException(nameof(scenario));

		if (scenario.CurrentStep is not HttpRequestStep httpStep)
			throw new InvalidOperationException("Current step is not an HTTP step. Call ApiResource() first.");

		// Create new step with Basic auth configuration
		var authConfig = new HttpAuthConfig();
		authConfig.UseBasicAuth(username, password);

		var newStep = new HttpRequestStep
		{
			Url = httpStep.Url,
			Method = httpStep.Method,
			Body = httpStep.Body,
			Headers = httpStep.Headers,
			QueryParams = httpStep.QueryParams,
			TimeoutSeconds = httpStep.TimeoutSeconds,
			AuthConfig = authConfig
		};

		scenario.SetCurrentStep(newStep);
		return scenario;
	}

	/// <summary>
	/// Configures Bearer token authentication for the HTTP request.
	/// Usage: .WithBearerToken("my-jwt-token")
	/// </summary>
	/// <param name="scenario">Test scenario</param>
	/// <param name="token">Bearer token</param>
	/// <param name="prefix">Token prefix (default: "Bearer")</param>
	/// <returns>Test scenario for chaining</returns>
	public static ITestScenario WithBearerToken(this ITestScenario scenario, string token, string prefix = "Bearer")
	{
		if (scenario == null)
			throw new ArgumentNullException(nameof(scenario));

		if (scenario.CurrentStep is not HttpRequestStep httpStep)
			throw new InvalidOperationException("Current step is not an HTTP step. Call ApiResource() first.");

		// Create new step with Bearer auth configuration
		var authConfig = new HttpAuthConfig();
		authConfig.UseBearerToken(token);
		if (authConfig.Bearer != null)
		{
			authConfig.Bearer.Prefix = prefix;
		}

		var newStep = new HttpRequestStep
		{
			Url = httpStep.Url,
			Method = httpStep.Method,
			Body = httpStep.Body,
			Headers = httpStep.Headers,
			QueryParams = httpStep.QueryParams,
			TimeoutSeconds = httpStep.TimeoutSeconds,
			AuthConfig = authConfig
		};

		scenario.SetCurrentStep(newStep);
		return scenario;
	}

	/// <summary>
	/// Configures custom authentication using a configuration action.
	/// Usage: .WithAuthConfig(config => config.UseBasicAuth("user", "pass"))
	/// </summary>
	/// <param name="scenario">Test scenario</param>
	/// <param name="configure">Configuration action</param>
	/// <returns>Test scenario for chaining</returns>
	public static ITestScenario WithAuthConfig(this ITestScenario scenario, Action<HttpAuthConfig> configure)
	{
		if (scenario == null)
			throw new ArgumentNullException(nameof(scenario));

		if (configure == null)
			throw new ArgumentNullException(nameof(configure));

		if (scenario.CurrentStep is not HttpRequestStep httpStep)
			throw new InvalidOperationException("Current step is not an HTTP step. Call ApiResource() first.");

		// Create auth configuration
		var authConfig = new HttpAuthConfig();
		configure(authConfig);

		// Create new step with auth configuration
		var newStep = new HttpRequestStep
		{
			Url = httpStep.Url,
			Method = httpStep.Method,
			Body = httpStep.Body,
			Headers = httpStep.Headers,
			QueryParams = httpStep.QueryParams,
			TimeoutSeconds = httpStep.TimeoutSeconds,
			AuthConfig = authConfig
		};

		scenario.SetCurrentStep(newStep);
		return scenario;
	}

	/// <summary>
	/// Configures API Key authentication in HTTP header.
	/// Usage: .WithApiKey("X-API-Key", "abc123")
	/// </summary>
	/// <param name="scenario">Test scenario</param>
	/// <param name="keyName">Name of the header</param>
	/// <param name="keyValue">API key value</param>
	/// <returns>Test scenario for chaining</returns>
	public static ITestScenario WithApiKey(this ITestScenario scenario, string keyName, string keyValue)
	{
		return WithApiKey(scenario, keyName, keyValue, ApiKeyLocation.Header);
	}

	/// <summary>
	/// Configures API Key authentication.
	/// Usage: .WithApiKey("api_key", "abc123", ApiKeyLocation.Query)
	/// </summary>
	/// <param name="scenario">Test scenario</param>
	/// <param name="keyName">Name of the header or query parameter</param>
	/// <param name="keyValue">API key value</param>
	/// <param name="location">Where to send the key</param>
	/// <returns>Test scenario for chaining</returns>
	public static ITestScenario WithApiKey(this ITestScenario scenario, string keyName, string keyValue, ApiKeyLocation location)
	{
		if (scenario == null)
			throw new ArgumentNullException(nameof(scenario));

		if (scenario.CurrentStep is not HttpRequestStep httpStep)
			throw new InvalidOperationException("Current step is not an HTTP step. Call ApiResource() first.");

		var authConfig = new HttpAuthConfig();
		authConfig.UseApiKey(keyName, keyValue, location);

		var newStep = new HttpRequestStep
		{
			Url = httpStep.Url,
			Method = httpStep.Method,
			Body = httpStep.Body,
			Headers = httpStep.Headers,
			QueryParams = httpStep.QueryParams,
			TimeoutSeconds = httpStep.TimeoutSeconds,
			AuthConfig = authConfig
		};

		scenario.SetCurrentStep(newStep);
		return scenario;
	}

	/// <summary>
	/// Configures OAuth 2.0 Client Credentials authentication.
	/// Usage: .WithOAuth2("https://auth.com/token", "client-id", "secret")
	/// </summary>
	/// <param name="scenario">Test scenario</param>
	/// <param name="tokenUrl">Token endpoint URL</param>
	/// <param name="clientId">Client ID</param>
	/// <param name="clientSecret">Client Secret</param>
	/// <param name="scopes">Optional scopes</param>
	/// <returns>Test scenario for chaining</returns>
	public static ITestScenario WithOAuth2(this ITestScenario scenario, string tokenUrl, string clientId, string clientSecret, params string[] scopes)
	{
		if (scenario == null)
			throw new ArgumentNullException(nameof(scenario));

		if (scenario.CurrentStep is not HttpRequestStep httpStep)
			throw new InvalidOperationException("Current step is not an HTTP step. Call ApiResource() first.");

		var authConfig = new HttpAuthConfig();
		authConfig.UseOAuth2(tokenUrl, clientId, clientSecret);
		if (authConfig.OAuth2 != null && scopes.Length > 0)
		{
			authConfig.OAuth2.Scopes = scopes.ToList();
		}

		var newStep = new HttpRequestStep
		{
			Url = httpStep.Url,
			Method = httpStep.Method,
			Body = httpStep.Body,
			Headers = httpStep.Headers,
			QueryParams = httpStep.QueryParams,
			TimeoutSeconds = httpStep.TimeoutSeconds,
			AuthConfig = authConfig
		};

		scenario.SetCurrentStep(newStep);
		return scenario;
	}

	/// <summary>
	/// Configures OAuth 2.0 with custom configuration.
	/// Usage: .WithOAuth2Config(config => { config.GrantType = OAuth2GrantType.Password; ... })
	/// </summary>
	/// <param name="scenario">Test scenario</param>
	/// <param name="configure">Configuration action</param>
	/// <returns>Test scenario for chaining</returns>
	public static ITestScenario WithOAuth2Config(this ITestScenario scenario, Action<OAuth2Config> configure)
	{
		if (scenario == null)
			throw new ArgumentNullException(nameof(scenario));

		if (configure == null)
			throw new ArgumentNullException(nameof(configure));

		if (scenario.CurrentStep is not HttpRequestStep httpStep)
			throw new InvalidOperationException("Current step is not an HTTP step. Call ApiResource() first.");

		var oauth2Config = new OAuth2Config();
		configure(oauth2Config);

		var authConfig = new HttpAuthConfig
		{
			Type = AuthenticationType.OAuth2,
			OAuth2 = oauth2Config
		};

		var newStep = new HttpRequestStep
		{
			Url = httpStep.Url,
			Method = httpStep.Method,
			Body = httpStep.Body,
			Headers = httpStep.Headers,
			QueryParams = httpStep.QueryParams,
			TimeoutSeconds = httpStep.TimeoutSeconds,
			AuthConfig = authConfig
		};

		scenario.SetCurrentStep(newStep);
		return scenario;
	}

	/// <summary>
	/// Configures custom header authentication.
	/// Usage: .WithCustomHeader("X-Auth-Token", "abc123")
	/// </summary>
	/// <param name="scenario">Test scenario</param>
	/// <param name="headerName">Header name</param>
	/// <param name="headerValue">Header value</param>
	/// <returns>Test scenario for chaining</returns>
	public static ITestScenario WithCustomHeader(this ITestScenario scenario, string headerName, string headerValue)
	{
		if (scenario == null)
			throw new ArgumentNullException(nameof(scenario));

		if (scenario.CurrentStep is not HttpRequestStep httpStep)
			throw new InvalidOperationException("Current step is not an HTTP step. Call ApiResource() first.");

		var authConfig = new HttpAuthConfig();
		authConfig.UseCustomHeader(headerName, headerValue);

		var newStep = new HttpRequestStep
		{
			Url = httpStep.Url,
			Method = httpStep.Method,
			Body = httpStep.Body,
			Headers = httpStep.Headers,
			QueryParams = httpStep.QueryParams,
			TimeoutSeconds = httpStep.TimeoutSeconds,
			AuthConfig = authConfig
		};

		scenario.SetCurrentStep(newStep);
		return scenario;
	}

	/// <summary>
	/// Configures multiple custom headers authentication.
	/// Usage: .WithCustomHeaders(new Dictionary&lt;string, string&gt; { ["X-Token"] = "abc", ["X-Session"] = "xyz" })
	/// </summary>
	/// <param name="scenario">Test scenario</param>
	/// <param name="headers">Dictionary of headers</param>
	/// <returns>Test scenario for chaining</returns>
	public static ITestScenario WithCustomHeaders(this ITestScenario scenario, Dictionary<string, string> headers)
	{
		if (scenario == null)
			throw new ArgumentNullException(nameof(scenario));

		if (headers == null || !headers.Any())
			throw new ArgumentException("At least one header is required.", nameof(headers));

		if (scenario.CurrentStep is not HttpRequestStep httpStep)
			throw new InvalidOperationException("Current step is not an HTTP step. Call ApiResource() first.");

		var authConfig = new HttpAuthConfig
		{
			Type = AuthenticationType.CustomHeader,
			CustomHeader = new CustomHeaderAuthConfig
			{
				Headers = headers
			}
		};

		var newStep = new HttpRequestStep
		{
			Url = httpStep.Url,
			Method = httpStep.Method,
			Body = httpStep.Body,
			Headers = httpStep.Headers,
			QueryParams = httpStep.QueryParams,
			TimeoutSeconds = httpStep.TimeoutSeconds,
			AuthConfig = authConfig
		};

		scenario.SetCurrentStep(newStep);
		return scenario;
	}

	/// <summary>
	/// Configures certificate-based authentication from httpsettings.json.
	/// Loads certificate configuration automatically from settings file.
	/// Usage: .WithCertificate()
	/// </summary>
	/// <param name="scenario">Test scenario</param>
	/// <returns>Test scenario for chaining</returns>
	/// <exception cref="InvalidOperationException">When certificate configuration is not found in settings</exception>
	public static ITestScenario WithCertificate(this ITestScenario scenario)
	{
		if (scenario == null)
			throw new ArgumentNullException(nameof(scenario));

		if (scenario.CurrentStep is not HttpRequestStep httpStep)
			throw new InvalidOperationException("Current step is not an HTTP step. Call ApiResource() first.");

		// Load settings
		var settings = HttpSettings.Load();
		
		// Validate certificate configuration exists
		if (settings.Authentication?.Certificate == null)
			throw new InvalidOperationException(
				"Certificate authentication is not configured in httpsettings.json. " +
				"Please add 'authentication.certificate' section with 'certificatePath' and optional 'certificatePassword'.");

		var certConfig = settings.Authentication.Certificate;

		// Validate required fields
		if (string.IsNullOrWhiteSpace(certConfig.CertificatePath) && 
		    string.IsNullOrWhiteSpace(certConfig.Thumbprint) &&
		    certConfig.Certificate == null)
		{
			throw new InvalidOperationException(
				"Certificate configuration must specify either 'certificatePath', 'thumbprint', or provide a certificate instance.");
		}

		// Create auth config from settings
		var authConfig = new HttpAuthConfig
		{
			Type = AuthenticationType.Certificate,
			Certificate = certConfig
		};

		var newStep = new HttpRequestStep
		{
			Url = httpStep.Url,
			Method = httpStep.Method,
			Body = httpStep.Body,
			Headers = httpStep.Headers,
			QueryParams = httpStep.QueryParams,
			TimeoutSeconds = httpStep.TimeoutSeconds,
			AuthConfig = authConfig
		};

		scenario.SetCurrentStep(newStep);
		return scenario;
	}

	/// <summary>
	/// Configures certificate-based authentication from file.
	/// Usage: .WithCertificate("path/to/cert.pfx", "password")
	/// </summary>
	/// <param name="scenario">Test scenario</param>
	/// <param name="certificatePath">Path to certificate file</param>
	/// <param name="password">Certificate password (optional)</param>
	/// <returns>Test scenario for chaining</returns>
	public static ITestScenario WithCertificate(this ITestScenario scenario, string certificatePath, string? password = null)
	{
		if (scenario == null)
			throw new ArgumentNullException(nameof(scenario));

		if (scenario.CurrentStep is not HttpRequestStep httpStep)
			throw new InvalidOperationException("Current step is not an HTTP step. Call ApiResource() first.");

		var authConfig = new HttpAuthConfig();
		authConfig.UseCertificate(certificatePath, password);

		var newStep = new HttpRequestStep
		{
			Url = httpStep.Url,
			Method = httpStep.Method,
			Body = httpStep.Body,
			Headers = httpStep.Headers,
			QueryParams = httpStep.QueryParams,
			TimeoutSeconds = httpStep.TimeoutSeconds,
			AuthConfig = authConfig
		};

		scenario.SetCurrentStep(newStep);
		return scenario;
	}

	/// <summary>
	/// Configures certificate-based authentication from Windows certificate store.
	/// Usage: .WithCertificateFromStore("thumbprint")
	/// </summary>
	/// <param name="scenario">Test scenario</param>
	/// <param name="thumbprint">Certificate thumbprint</param>
	/// <param name="storeLocation">Store location (default: CurrentUser)</param>
	/// <param name="storeName">Store name (default: My)</param>
	/// <returns>Test scenario for chaining</returns>
	public static ITestScenario WithCertificateFromStore(this ITestScenario scenario, string thumbprint, StoreLocation storeLocation = StoreLocation.CurrentUser, StoreName storeName = StoreName.My)
	{
		if (scenario == null)
			throw new ArgumentNullException(nameof(scenario));

		if (scenario.CurrentStep is not HttpRequestStep httpStep)
			throw new InvalidOperationException("Current step is not an HTTP step. Call ApiResource() first.");

		var authConfig = new HttpAuthConfig();
		authConfig.UseCertificateFromStore(thumbprint, storeLocation, storeName);

		var newStep = new HttpRequestStep
		{
			Url = httpStep.Url,
			Method = httpStep.Method,
			Body = httpStep.Body,
			Headers = httpStep.Headers,
			QueryParams = httpStep.QueryParams,
			TimeoutSeconds = httpStep.TimeoutSeconds,
			AuthConfig = authConfig
		};

		scenario.SetCurrentStep(newStep);
		return scenario;
	}

	/// <summary>
	/// Configures certificate-based authentication with a certificate instance.
	/// Usage: .WithCertificateInstance(myCertificate)
	/// </summary>
	/// <param name="scenario">Test scenario</param>
	/// <param name="certificate">Certificate instance</param>
	/// <returns>Test scenario for chaining</returns>
	public static ITestScenario WithCertificateInstance(this ITestScenario scenario, X509Certificate2 certificate)
	{
		if (scenario == null)
			throw new ArgumentNullException(nameof(scenario));

		if (certificate == null)
			throw new ArgumentNullException(nameof(certificate));

		if (scenario.CurrentStep is not HttpRequestStep httpStep)
			throw new InvalidOperationException("Current step is not an HTTP step. Call ApiResource() first.");

		var authConfig = new HttpAuthConfig
		{
			Type = AuthenticationType.Certificate,
			Certificate = new CertificateAuthConfig
			{
				Certificate = certificate
			}
		};

		var newStep = new HttpRequestStep
		{
			Url = httpStep.Url,
			Method = httpStep.Method,
			Body = httpStep.Body,
			Headers = httpStep.Headers,
			QueryParams = httpStep.QueryParams,
			TimeoutSeconds = httpStep.TimeoutSeconds,
			AuthConfig = authConfig
		};

		scenario.SetCurrentStep(newStep);
		return scenario;
	}

	/// <summary>
	/// Clears authentication configuration for this request.
	/// Useful to override global authentication from httpsettings.json.
	/// Usage: .WithNoAuth()
	/// </summary>
	/// <param name="scenario">Test scenario</param>
	/// <returns>Test scenario for chaining</returns>
	public static ITestScenario WithNoAuth(this ITestScenario scenario)
	{
		if (scenario == null)
			throw new ArgumentNullException(nameof(scenario));

		if (scenario.CurrentStep is not HttpRequestStep httpStep)
			throw new InvalidOperationException("Current step is not an HTTP step. Call ApiResource() first.");

		// Create new step with no authentication
		var authConfig = new HttpAuthConfig
		{
			Type = AuthenticationType.None
		};

		var newStep = new HttpRequestStep
		{
			Url = httpStep.Url,
			Method = httpStep.Method,
			Body = httpStep.Body,
			Headers = httpStep.Headers,
			QueryParams = httpStep.QueryParams,
			TimeoutSeconds = httpStep.TimeoutSeconds,
			AuthConfig = authConfig
		};

		scenario.SetCurrentStep(newStep);
		return scenario;
	}
}
