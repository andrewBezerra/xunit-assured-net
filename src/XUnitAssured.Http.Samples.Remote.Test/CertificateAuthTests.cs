using XUnitAssured.Http.Extensions;
using XUnitAssured.Http.Extensions;

namespace XUnitAssured.Http.Samples.Remote.Test;

[Trait("Authentication", "Certificate")]
[Trait("Environment", "Remote")]
/// <summary>
/// Remote tests demonstrating Certificate Authentication (mTLS) against a deployed API.
/// Certificate authentication is used for mutual TLS where both client and server verify each other's identity.
/// 
/// NOTE: These tests demonstrate the API usage but may require actual certificate setup to run successfully.
/// For demonstration purposes, some tests are marked as Skip with instructions.
/// </summary>
/// <remarks>
/// Tests certificate authentication endpoints against a remote server.
/// Most tests are skipped as they require actual certificate infrastructure.
/// </remarks>
public class CertificateAuthTests : HttpSamplesRemoteTestBase, IClassFixture<HttpSamplesRemoteFixture>
{
	public CertificateAuthTests(HttpSamplesRemoteFixture fixture) : base(fixture)
	{
	}

	[Fact(Skip = "Remote test - requires actual certificate setup and deployed API environment", DisplayName = "Certificate Authentication from file should authenticate successfully")]
	public void Example01_Certificate_FromFile_ShouldAuthenticate()
	{
		// Arrange
		var certificatePath = "path/to/client-certificate.pfx";
		var certificatePassword = "certificate-password";

		// Act & Assert
		Given()
			.ApiResource($"{BaseUrl}/api/auth/certificate")
			.WithCertificate(certificatePath, certificatePassword)
			.Get()
		.When()
			.Execute()
		.Then()
			.AssertStatusCode(200)
			.AssertJsonPath<string>("$.authType", value => value == "Certificate", "Auth type should be Certificate")
			.AssertJsonPath<bool>("$.authenticated", value => value, "Should be authenticated")
			.AssertJsonPath<string>("$.customData.Subject", value => !string.IsNullOrEmpty(value), "Should return certificate subject");
	}

	[Fact(Skip = "Remote test - requires actual certificate in Windows Certificate Store and deployed API environment", DisplayName = "Certificate Authentication from Windows Store should authenticate successfully")]
	public void Example02_Certificate_FromStore_ShouldAuthenticate()
	{
		// Arrange
		var thumbprint = "ABC123DEF456..."; // Certificate thumbprint
		var storeLocation = System.Security.Cryptography.X509Certificates.StoreLocation.CurrentUser;
		var storeName = System.Security.Cryptography.X509Certificates.StoreName.My;

		// Act & Assert
		Given()
			.ApiResource($"{BaseUrl}/api/auth/certificate")
			.WithCertificateFromStore(thumbprint, storeLocation, storeName)
			.Get()
		.When()
			.Execute()
		.Then()
			.AssertStatusCode(200)
			.AssertJsonPath<bool>("$.authenticated", value => value, "Should be authenticated with certificate from store");
	}

	[Fact(Skip = "Remote test - requires actual certificate instance and deployed API environment", DisplayName = "Certificate Authentication from instance should authenticate successfully")]
	public void Example03_Certificate_FromInstance_ShouldAuthenticate()
	{
		// Arrange
		// This would typically be loaded from a file or certificate store
		var certificate = new System.Security.Cryptography.X509Certificates.X509Certificate2(
			"path/to/certificate.pfx",
			"password");

		// Act & Assert
		Given()
			.ApiResource($"{BaseUrl}/api/auth/certificate")
			.WithCertificateInstance(certificate)
			.Get()
		.When()
			.Execute()
		.Then()
			.AssertStatusCode(200)
			.AssertJsonPath<bool>("$.authenticated", value => value, "Should be authenticated with certificate instance");
	}

	[Fact(Skip = "Remote test - requires deployed API environment", DisplayName = "Certificate Authentication without certificate should return 401 Unauthorized")]
	public void Example04_Certificate_WithoutCertificate_ShouldReturn401()
	{
		// Act & Assert - No certificate provided
		Given().ApiResource($"/api/auth/certificate")
			.Get()
		.When()
			.Execute()
		.Then()
			.AssertStatusCode(401)
			.AssertJsonPath<string>("$.message", value => value?.Contains("certificate") == true, "Should indicate certificate is required");
	}

	[Fact(Skip = "Remote test - requires actual certificate setup to test validation and deployed API environment", DisplayName = "Certificate Authentication should return complete response structure")]
	public void Example05_Certificate_ValidateResponseStructure()
	{
		// Arrange
		var certificatePath = "path/to/client-certificate.pfx";
		var certificatePassword = "certificate-password";

		// Act & Assert - Validate certificate authentication response structure
		Given()
			.ApiResource($"{BaseUrl}/api/auth/certificate")
			.WithCertificate(certificatePath, certificatePassword)
			.Get()
		.When()
			.Execute()
		.Then()
			.AssertStatusCode(200)
			.AssertJsonPath<string>("$.authType", value => value == "Certificate", "Auth type should be Certificate")
			.AssertJsonPath<bool>("$.authenticated", value => value, "Should be authenticated")
			.AssertJsonPath<object>("$.customData", value => value != null, "customData should exist")
			.AssertJsonPath<object>("$.customData.Subject", value => value != null, "Certificate subject should exist")
			.AssertJsonPath<object>("$.customData.Issuer", value => value != null, "Certificate issuer should exist")
			.AssertJsonPath<object>("$.customData.Thumbprint", value => value != null, "Certificate thumbprint should exist")
			.AssertJsonPath<object>("$.customData.ValidFrom", value => value != null, "Certificate ValidFrom should exist")
			.AssertJsonPath<object>("$.customData.ValidTo", value => value != null, "Certificate ValidTo should exist");
	}

	[Fact(Skip = "Remote test - requires httpsettings.json configuration, actual certificate and deployed API environment", DisplayName = "Certificate Authentication from configuration should authenticate successfully")]
	public void Example06_Certificate_FromConfiguration_ShouldAuthenticate()
	{
		// This example demonstrates using certificate configuration from httpsettings.json
		// The configuration would look like:
		// {
		//   "authentication": {
		//     "certificate": {
		//       "certificatePath": "path/to/cert.pfx",
		//       "certificatePassword": "password"
		//     }
		//   }
		// }

		// Act & Assert
		Given()
			.ApiResource($"{BaseUrl}/api/auth/certificate")
			.WithCertificate() // Loads from httpsettings.json
			.Get()
		.When()
			.Execute()
		.Then()
			.AssertStatusCode(200)
			.AssertJsonPath<bool>("$.authenticated", value => value, "Should be authenticated using certificate from configuration");
	}

	[Fact(Skip = "Documentation example - not a runnable remote test", DisplayName = "Certificate Authentication setup guide and documentation")]
	public void Example07_Certificate_Documentation_SetupGuide()
	{
		/*
		 * CERTIFICATE AUTHENTICATION SETUP GUIDE FOR REMOTE TESTING
		 * ========================================================
		 * 
		 * Certificate authentication (mTLS) requires both client and server certificates.
		 * 
		 * 1. REMOTE API REQUIREMENTS:
		 *    - Server must be configured to accept client certificates
		 *    - Server certificate must be valid and trusted
		 *    - Client certificates must be signed by trusted CA
		 *    
		 * 2. CLIENT CONFIGURATION (testsettings.json):
		 *    {
		 *      "testMode": "Remote",
		 *      "http": {
		 *        "baseUrl": "https://your-mtls-api.com",
		 *        "authentication": {
		 *          "type": "Certificate",
		 *          "certificate": {
		 *            "certificatePath": "path/to/client.pfx",
		 *            "certificatePassword": "${ENV:CERT_PASSWORD}"
		 *          }
		 *        }
		 *      }
		 *    }
		 * 
		 * 3. TEST USAGE:
		 *    Option A - From File:
		 *      .WithCertificate("path/to/client.pfx", "password")
		 *    
		 *    Option B - From Windows Certificate Store:
		 *      .WithCertificateFromStore("thumbprint", StoreLocation.CurrentUser, StoreName.My)
		 *    
		 *    Option C - From Configuration:
		 *      .WithCertificate() // Uses testsettings.json config
		 * 
		 * 4. ENVIRONMENT VARIABLES:
		 *    $env:CERT_PASSWORD="your-cert-password"
		 *    $env:CERT_PATH="C:\certs\client.pfx"
		 * 
		 * 5. TROUBLESHOOTING REMOTE mTLS:
		 *    - Verify server accepts client certificates (check server logs)
		 *    - Ensure certificate is not expired
		 *    - Check certificate chain is valid and trusted
		 *    - Verify client certificate permissions
		 *    - Test with curl first: curl --cert client.pfx --cert-type PFX https://api.com
		 */

		Assert.True(true, "This is a documentation test");
	}

	[Fact(Skip = "Remote test - requires expired certificate for testing and deployed API environment", DisplayName = "Certificate Authentication with expired certificate should handle gracefully")]
	public void Example08_Certificate_WithExpiredCertificate_ShouldHandleGracefully()
	{
		// Arrange
		var expiredCertPath = "path/to/expired-certificate.pfx";
		var password = "password";

		// Act & Assert - Depending on configuration, this might return 401 or specific error
		Given()
			.ApiResource($"{BaseUrl}/api/auth/certificate")
			.WithCertificate(expiredCertPath, password)
			.Get()
		.When()
			.Execute()
		.Then()
			.AssertStatusCode(401); // Or specific error code for expired certificate
	}

	[Fact(Skip = "Remote test - requires untrusted certificate for testing and deployed API environment", DisplayName = "Certificate Authentication with untrusted certificate should return 401 Unauthorized")]
	public void Example09_Certificate_WithUntrustedCertificate_ShouldReturn401()
	{
		// Arrange
		var untrustedCertPath = "path/to/untrusted-certificate.pfx";
		var password = "password";

		// Act & Assert
		Given()
			.ApiResource($"{BaseUrl}/api/auth/certificate")
			.WithCertificate(untrustedCertPath, password)
			.Get()
		.When()
			.Execute()
		.Then()
			.AssertStatusCode(401)
			.AssertJsonPath<string>("$.message", value => value?.Contains("untrusted") == true ||
												   value?.Contains("invalid") == true,
							"Should indicate certificate is not trusted");
	}
}
