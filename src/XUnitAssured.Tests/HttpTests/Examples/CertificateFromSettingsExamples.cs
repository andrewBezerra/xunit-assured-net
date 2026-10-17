using System.Collections.Generic;
using XUnitAssured.Http.Configuration;
using XUnitAssured.Http.Extensions;

namespace XUnitAssured.Tests.HttpTests.Examples;

[Trait("Category", "Examples")]
[Trait("Authentication", "Certificate")]
/// <summary>
/// Tests for the .WithCertificate() extension method that loads the certificate configuration
/// from httpsettings.json. The examples that need a real certificate are in
/// <see cref="CertificateFromSettingsExamples"/>.
/// </summary>
public class CertificateFromSettingsTests
{
	/// <summary>
	/// Example 6: Error handling when certificate not configured
	/// </summary>
	[Fact(DisplayName = "Example: Error handling when certificate is not configured in settings")]
	public void Example_Error_When_Certificate_Not_In_Settings()
	{
		// If httpsettings.json doesn't have certificate config,
		// .WithCertificate() will throw helpful error
		var scenario = Given().ApiResource("/endpoint");

		Should.Throw<InvalidOperationException>(() => 
			scenario.WithCertificate())
			.Message.ShouldContain("Certificate authentication is not configured");
	}

	private class Customer
	{
		public int Id { get; set; }
		public string? Name { get; set; }
	}
}

/// <summary>
/// Examples kept here so they compile against the real API. They are not run: each needs
/// infrastructure this suite does not start, as the comment on it says.
/// </summary>
public static class CertificateFromSettingsExamples
{
	/// <summary>
	/// Example 1: Using .WithCertificate() without parameters
	/// Automatically loads certificate from httpsettings.json
	/// 
	/// Required httpsettings.json configuration:
	/// {
	///   "http": {
	///     "authentication": {
	///       "type": "Certificate",
	///       "certificate": {
	///         "certificatePath": "${ENV:CLIENT_CERT_PATH}",
	///         "certificatePassword": "${ENV:CLIENT_CERT_PASSWORD}"
	///       }
	///     }
	///   }
	/// }
	/// 
	/// Environment variables:
	/// CLIENT_CERT_PATH=C:\Certs\client-cert.pfx
	/// CLIENT_CERT_PASSWORD=MySecurePassword123
	/// </summary>
	// Compiled, not run: requires certificate in httpsettings.json.
	public static void Example_WithCertificate_From_Settings()
	{
		Given()
			.ApiResource("/secure/endpoint")
			.WithCertificate()  // ✅ Loads from settings automatically!
			.Get()
			.Validate(response =>
			{
				response.StatusCode.ShouldBe(200);
			});
	}

	/// <summary>
	/// Example 2: Using environment-specific certificate configuration
	/// 
	/// httpsettings.json:
	/// {
	///   "environments": {
	///     "mtls": {
	///       "baseUrl": "https://mtls-api.example.com",
	///       "authentication": {
	///         "type": "Certificate",
	///         "certificate": {
	///           "certificatePath": "${ENV:CLIENT_CERT_PATH}",
	///           "certificatePassword": "${ENV:CLIENT_CERT_PASSWORD}"
	///         }
	///       }
	///     }
	///   }
	/// }
	/// 
	/// Load environment: HttpSettings.Load(environment: "mtls")
	/// </summary>
	// Compiled, not run: requires mtls environment in httpsettings.json.
	public static void Example_WithCertificate_From_Environment()
	{
		// Load mtls environment settings
		var settings = HttpSettings.Load(environment: "mtls");

		Given()
			.ApiResource("/secure/data")
			.WithCertificate()  // Uses mtls environment settings
			.Get()
			.Validate(response =>
			{
				response.IsSuccessStatusCode.ShouldBeTrue();
			});
	}

	/// <summary>
	/// Example 3: Using certificate from Windows Certificate Store
	/// 
	/// httpsettings.json:
	/// {
	///   "authentication": {
	///     "type": "Certificate",
	///     "certificate": {
	///       "thumbprint": "ABC123DEF456789",
	///       "storeLocation": "CurrentUser",
	///       "storeName": "My"
	///     }
	///   }
	/// }
	/// </summary>
	// Compiled, not run: requires certificate in store.
	public static void Example_WithCertificate_From_Store_Via_Settings()
	{
		Given()
			.ApiResource("/api/protected")
			.WithCertificate()  // Loads from certificate store
			.Get();
	}

	/// <summary>
	/// Example 4: Override settings with explicit certificate
	/// Useful when you need different certificate for specific test
	/// </summary>
	// Compiled, not run: requires certificates.
	public static void Example_Override_Settings_Certificate()
	{
		// Most tests use settings certificate
		Given()
			.ApiResource("/endpoint1")
			.WithCertificate()  // From settings
			.Get();

		// But this specific test needs different certificate
		Given()
			.ApiResource("/endpoint2")
			.WithCertificate("path/to/special-cert.pfx", "password")  // Override
			.Get();
	}

	/// <summary>
	/// Example 5: Multiple requests with same certificate (cached)
	/// The certificate and FlurlClient are cached automatically by thumbprint
	/// </summary>
	// Compiled, not run: demonstrates caching.
	public static void Example_Certificate_Caching_Performance()
	{
		// First request: loads certificate and creates FlurlClient (~50ms)
		Given()
			.ApiResource("/endpoint1")
			.WithCertificate()
			.Get();

		// Second request: uses cached FlurlClient (~5ms) ⚡
		Given()
			.ApiResource("/endpoint2")
			.WithCertificate()  // Same certificate = cache hit!
			.Get();

		// Third request: still using cache
		Given()
			.ApiResource("/endpoint3")
			.WithCertificate()
			.Get();
	}

	/// <summary>
	/// Example 7: Complete real-world scenario
	/// Production-ready pattern with error handling
	/// </summary>
	// Compiled, not run: production pattern.
	public static void Example_Production_Pattern()
	{
		try
		{
			// Load certificate from settings (dev/staging/prod environment)
			var result = Given()
				.ApiResource("/api/v1/secure/customers")
				.WithCertificate()  // Automatic from settings
				.WithHeader("X-Request-ID", Guid.NewGuid().ToString())
				.Get()
				.Validate(response =>
				{
					response.StatusCode.ShouldBe(200);
						response.ContentType!.ShouldContain("application/json");
				});

			// Process response (example - actual deserialization depends on your implementation)
			// var customers = DeserializeResponse<List<Customer>>(result);
			// customers.ShouldNotBeEmpty();
		}
		catch (InvalidOperationException ex) when (ex.Message.Contains("Certificate"))
		{
			// Handle certificate configuration issues
			throw new Exception(
				"Certificate not configured. Please set CLIENT_CERT_PATH and CLIENT_CERT_PASSWORD environment variables.", 
				ex);
		}
	}
}
