using XUnitAssured.Extensions.Http;
using XUnitAssured.Http.Extensions;
using XUnitAssured.Http.Testing;

namespace XUnitAssured.Http.Samples.Test;

[Trait("Authentication", "Certificate")]
[Trait("Environment", "Local")]

/// <summary>
/// Sample tests demonstrating Certificate Authentication (mTLS) using XUnitAssured.Http.
/// Certificate authentication is used for mutual TLS where both client and server verify each other's identity.
/// 
/// NOTE: These tests demonstrate the API usage but may require actual certificate setup to run successfully.
/// For demonstration purposes, some tests are marked as Skip with instructions.
/// </summary>
public class CertificateAuthTests : HttpTestBase<HttpSamplesFixture>, IClassFixture<HttpSamplesFixture>
{
	public CertificateAuthTests(HttpSamplesFixture fixture) : base(fixture)
	{
	}

	[Fact(Skip = "Requires actual certificate setup. This test demonstrates API usage.", DisplayName = "Certificate Authentication from file should authenticate successfully")]
	public void Example01_Certificate_FromFile_ShouldAuthenticate()
	{
		// Arrange
		var certificatePath = "path/to/client-certificate.pfx";
		var certificatePassword = "certificate-password";

		// Act & Assert
		Given()
			.ApiResource($"{Fixture.BaseUrl}/api/auth/certificate")
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

	[Fact(Skip = "Requires actual certificate in Windows Certificate Store.", DisplayName = "Certificate Authentication from Windows Store should authenticate successfully")]
	public void Example02_Certificate_FromStore_ShouldAuthenticate()
	{
		// Arrange
		var thumbprint = "ABC123DEF456..."; // Certificate thumbprint
		var storeLocation = System.Security.Cryptography.X509Certificates.StoreLocation.CurrentUser;
		var storeName = System.Security.Cryptography.X509Certificates.StoreName.My;

		// Act & Assert
		Given()
			.ApiResource($"{Fixture.BaseUrl}/api/auth/certificate")
			.WithCertificateFromStore(thumbprint, storeLocation, storeName)
			.Get()
		.When()
			.Execute()
		.Then()
			.AssertStatusCode(200)
			.AssertJsonPath<bool>("$.authenticated", value => value, "Should be authenticated with certificate from store");
	}

	[Fact(Skip = "Requires actual certificate instance.", DisplayName = "Certificate Authentication from instance should authenticate successfully")]
	public void Example03_Certificate_FromInstance_ShouldAuthenticate()
	{
		// Arrange
		// This would typically be loaded from a file or certificate store
		var certificate = new System.Security.Cryptography.X509Certificates.X509Certificate2(
			"path/to/certificate.pfx",
			"password");

		// Act & Assert
		Given()
			.ApiResource($"{Fixture.BaseUrl}/api/auth/certificate")
			.WithCertificateInstance(certificate)
			.Get()
		.When()
			.Execute()
		.Then()
			.AssertStatusCode(200)
			.AssertJsonPath<bool>("$.authenticated", value => value, "Should be authenticated with certificate instance");
	}

	[Fact(DisplayName = "Certificate Authentication without certificate should return 401 Unauthorized")]
	public void Example04_Certificate_WithoutCertificate_ShouldReturn401()
	{
		// Act & Assert - No certificate provided (using fixture's client to ensure proper test server communication)
		Given().ApiResource($"/api/auth/certificate")
			.Get()
		.When()
			.Execute()
		.Then()
			.AssertStatusCode(401)
			.AssertJsonPath<string>("$.message", value => value?.Contains("certificate") == true, "Should indicate certificate is required");
	}

	[Fact(Skip = "Requires actual certificate setup to test validation.", DisplayName = "Certificate Authentication should return complete response structure")]
	public void Example05_Certificate_ValidateResponseStructure()
	{
		// Arrange
		var certificatePath = "path/to/client-certificate.pfx";
		var certificatePassword = "certificate-password";

		// Act & Assert - Validate certificate authentication response structure
		Given()
			.ApiResource($"{Fixture.BaseUrl}/api/auth/certificate")
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

	[Fact(Skip = "Requires httpsettings.json configuration and actual certificate.", DisplayName = "Certificate Authentication from configuration should authenticate successfully")]
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
			.ApiResource($"{Fixture.BaseUrl}/api/auth/certificate")
			.WithCertificate() // Loads from httpsettings.json
			.Get()
		.When()
			.Execute()
		.Then()
			.AssertStatusCode(200)
			.AssertJsonPath<bool>("$.authenticated", value => value, "Should be authenticated using certificate from configuration");
	}

	/// <summary>
	/// Documentation test - demonstrates how to set up certificate authentication.
	/// This is not a runnable test but serves as inline documentation.
	/// </summary>
	[Fact(Skip = "Documentation example - not a runnable test.", DisplayName = "Certificate Authentication setup guide and documentation")]
	public void Example07_Certificate_Documentation_SetupGuide()
	{
		/*
		 * CERTIFICATE AUTHENTICATION SETUP GUIDE
		 * ======================================
		 * 
		 * Certificate authentication (mTLS) requires both client and server certificates.
		 * 
		 * 1. GENERATE CERTIFICATES (for testing):
		 *    - Create a self-signed CA certificate
		 *    - Generate server certificate signed by CA
		 *    - Generate client certificate signed by CA
		 *    
		 * 2. SERVER CONFIGURATION (SampleWebApi):
		 *    - Install server certificate
		 *    - Configure Kestrel to require client certificates
		 *    - Already configured in Program.cs:
		 *      serverOptions.ConfigureHttpsDefaults(httpsOptions =>
		 *      {
		 *          httpsOptions.ClientCertificateMode = ClientCertificateMode.AllowCertificate;
		 *      });
		 * 
		 * 3. CLIENT CONFIGURATION (Test Project):
		 *    Option A - From File:
		 *      .WithCertificate("path/to/client.pfx", "password")
		 *    
		 *    Option B - From Windows Certificate Store:
		 *      .WithCertificateFromStore("thumbprint", StoreLocation.CurrentUser, StoreName.My)
		 *    
		 *    Option C - From Certificate Instance:
		 *      var cert = new X509Certificate2("path/to/cert.pfx", "password");
		 *      .WithCertificateInstance(cert)
		 *    
		 *    Option D - From httpsettings.json:
		 *      {
		 *        "authentication": {
		 *          "certificate": {
		 *            "certificatePath": "path/to/cert.pfx",
		 *            "certificatePassword": "password"
		 *          }
		 *        }
		 *      }
		 *      Then use: .WithCertificate()
		 * 
		 * 4. POWERSHELL COMMANDS TO GENERATE TEST CERTIFICATES:
		 * 
		 *    # Create CA certificate
		 *    $ca = New-SelfSignedCertificate -Subject "CN=Test CA" -CertStoreLocation "Cert:\CurrentUser\My" `
		 *          -KeyExportPolicy Exportable -KeySpec Signature -KeyLength 2048 -KeyAlgorithm RSA `
		 *          -HashAlgorithm SHA256 -Provider "Microsoft Enhanced RSA and AES Cryptographic Provider"
		 *    
		 *    # Create server certificate
		 *    $server = New-SelfSignedCertificate -Subject "CN=localhost" -DnsName "localhost" `
		 *          -CertStoreLocation "Cert:\CurrentUser\My" -Signer $ca -KeyExportPolicy Exportable `
		 *          -KeySpec KeyExchange -KeyLength 2048 -KeyAlgorithm RSA -HashAlgorithm SHA256
		 *    
		 *    # Create client certificate
		 *    $client = New-SelfSignedCertificate -Subject "CN=Test Client" -CertStoreLocation "Cert:\CurrentUser\My" `
		 *          -Signer $ca -KeyExportPolicy Exportable -KeySpec KeyExchange -KeyLength 2048 `
		 *          -KeyAlgorithm RSA -HashAlgorithm SHA256
		 *    
		 *    # Export client certificate
		 *    $pwd = ConvertTo-SecureString -String "password123" -Force -AsPlainText
		 *    Export-PfxCertificate -Cert $client -FilePath "client-cert.pfx" -Password $pwd
		 * 
		 * 5. TROUBLESHOOTING:
		 *    - Ensure certificates are not expired
		 *    - Verify certificate chain is valid
		 *    - Check that client certificate is trusted by server
		 *    - Ensure proper permissions on certificate files
		 */

		Assert.True(true, "This is a documentation test");
	}

	[Fact(Skip = "Requires expired certificate for testing.", DisplayName = "Certificate Authentication with expired certificate should handle gracefully")]
	public void Example08_Certificate_WithExpiredCertificate_ShouldHandleGracefully()
	{
		// Arrange
		var expiredCertPath = "path/to/expired-certificate.pfx";
		var password = "password";

		// Act & Assert - Depending on configuration, this might return 401 or specific error
		Given()
			.ApiResource($"{Fixture.BaseUrl}/api/auth/certificate")
			.WithCertificate(expiredCertPath, password)
			.Get()
		.When()
			.Execute()
		.Then()
			.AssertStatusCode(401); // Or specific error code for expired certificate
	}

	[Fact(Skip = "Requires untrusted certificate for testing.", DisplayName = "Certificate Authentication with untrusted certificate should return 401 Unauthorized")]
	public void Example09_Certificate_WithUntrustedCertificate_ShouldReturn401()
	{
		// Arrange
		var untrustedCertPath = "path/to/untrusted-certificate.pfx";
		var password = "password";

		// Act & Assert
		Given()
			.ApiResource($"{Fixture.BaseUrl}/api/auth/certificate")
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
