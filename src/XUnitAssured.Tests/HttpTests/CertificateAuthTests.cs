using System.Security.Cryptography.X509Certificates;
using XUnitAssured.Http.Client;
using XUnitAssured.Http.Configuration;
using XUnitAssured.Http.Extensions;

namespace XUnitAssured.Tests.HttpTests;

[Trait("Category", "Http")]
[Trait("Authentication", "Certificate")]
/// <summary>
/// Tests for Certificate (mTLS) authentication.
/// Note: Most tests are skipped as they require valid X509 certificates.
/// The framework functionality is validated through integration tests with real certificates.
/// </summary>
public class CertificateAuthTests
{
	[Fact(DisplayName = "Should configure certificate authentication from file")]
	public void Should_Configure_Certificate_From_File()
	{
		// Arrange & Act
		var scenario = Given()
			.ApiResource("https://api.example.com/resource")
			.WithCertificate("path/to/cert.pfx", "password")
			.Get();

		// Assert
		scenario.ShouldNotBeNull();
		scenario.CurrentStep.ShouldNotBeNull();
	}

	[Fact(DisplayName = "Should configure certificate authentication from Windows certificate store")]
	public void Should_Configure_Certificate_From_Store()
	{
		// Arrange & Act
		var scenario = Given()
			.ApiResource("https://api.example.com/resource")
			.WithCertificateFromStore("ABC123DEF456")
			.Get();

		// Assert
		scenario.ShouldNotBeNull();
	}

	[Fact(DisplayName = "Should support certificate authentication via auth config")]
	public void Should_Support_Certificate_Via_Config()
	{
		// Arrange & Act
		var scenario = Given()
			.ApiResource("https://api.example.com/resource")
			.WithAuthConfig(config =>
			{
				config.UseCertificate("path/to/cert.pfx");
			})
			.Get();

		// Assert
		scenario.ShouldNotBeNull();
	}

	[Fact(DisplayName = "CertificateAuthConfig should have default values")]
	public void CertificateAuthConfig_Should_Have_Defaults()
	{
		// Arrange & Act
		var config = new CertificateAuthConfig();

		// Assert
		config.StoreLocation.ShouldBe(StoreLocation.CurrentUser);
		config.StoreName.ShouldBe(StoreName.My);
	}

	[Fact(DisplayName = "Should throw ArgumentNullException when certificate instance is null")]
	public void Should_Throw_When_Certificate_Is_Null()
	{
		// Arrange
		var scenario = Given().ApiResource("https://api.example.com/resource");

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			scenario.WithCertificateInstance(null!));
	}

	[Fact(DisplayName = "Should throw InvalidOperationException when not HTTP step")]
	public void Should_Throw_When_Not_Http_Step()
	{
		// Arrange
		var scenario = Given();

		// Act & Assert
		Should.Throw<InvalidOperationException>(() =>
			scenario.WithCertificate("path/to/cert.pfx"));
	}

	[Fact(DisplayName = "FlurlClientFactory should clear cache successfully")]
	public void FlurlClientFactory_Should_Clear_Cache_Successfully()
	{
		// Act
		FlurlClientFactory.ClearCache();

		// Assert - No exception thrown
		FlurlClientFactory.CachedClientCount.ShouldBe(0);
	}

	[Fact(Skip = "Integration test - requires real certificate and mTLS server", DisplayName = "Integration test should authenticate with client certificate successfully")]
	public void Integration_Should_Authenticate_With_Certificate()
	{
		var certPath = Environment.GetEnvironmentVariable("TEST_CLIENT_CERT_PATH");
		var certPassword = Environment.GetEnvironmentVariable("TEST_CLIENT_CERT_PASSWORD");

		if (!string.IsNullOrEmpty(certPath))
		{
			Given()
				.ApiResource("https://mtls-api.example.com/protected")
				.WithCertificate(certPath, certPassword)
				.Get()
				.Validate(response =>
				{
					response.StatusCode.ShouldBe(200);
				});
		}
	}
}
