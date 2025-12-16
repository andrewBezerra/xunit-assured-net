using System.Collections.Generic;
using XUnitAssured.Http.Configuration;
using XUnitAssured.Http.Extensions;

namespace XUnitAssured.Tests.HttpTests.Examples;

/// <summary>
/// Examples demonstrating the .WithCertificate() extension method
/// that automatically loads certificate configuration from httpsettings.json
/// </summary>
public class CertificateFromSettingsExamples
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
	[Fact(Skip = "Example - requires certificate in httpsettings.json")]
	public void Example_WithCertificate_From_Settings()
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
	[Fact(Skip = "Example - requires mtls environment in httpsettings.json")]
	public void Example_WithCertificate_From_Environment()
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
	[Fact(Skip = "Example - requires certificate in store")]
	public void Example_WithCertificate_From_Store_Via_Settings()
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
	[Fact(Skip = "Example - requires certificates")]
	public void Example_Override_Settings_Certificate()
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
	[Fact(Skip = "Example - demonstrates caching")]
	public void Example_Certificate_Caching_Performance()
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
	/// Example 6: Error handling when certificate not configured
	/// </summary>
	[Fact]
	public void Example_Error_When_Certificate_Not_In_Settings()
	{
		// If httpsettings.json doesn't have certificate config,
		// .WithCertificate() will throw helpful error
		var scenario = Given().ApiResource("/endpoint");

		Should.Throw<InvalidOperationException>(() => 
			scenario.WithCertificate())
			.Message.ShouldContain("Certificate authentication is not configured");
	}

	/// <summary>
	/// Example 7: Complete real-world scenario
	/// Production-ready pattern with error handling
	/// </summary>
	[Fact(Skip = "Example - production pattern")]
	public void Example_Production_Pattern()
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
					response.ContentType.ShouldContain("application/json");
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

	private class Customer
	{
		public int Id { get; set; }
		public string? Name { get; set; }
	}
}
