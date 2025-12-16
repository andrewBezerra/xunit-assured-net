using XUnitAssured.Kafka.Configuration;
using XUnitAssured.Kafka.Extensions;
using Xunit;
using Shouldly;

namespace XUnitAssured.Tests.KafkaTests;

/// <summary>
/// Tests for SSL/TLS authentication in Kafka.
/// </summary>
public class SslAuthTests
{
	[Fact]
	public void Should_Configure_Ssl_With_CaLocation()
	{
		// Arrange & Act
		var scenario = Given()
			.Topic("test-topic")
			.WithSsl("/path/to/ca-cert.pem")
			.Consume();

		// Assert
		scenario.ShouldNotBeNull();
		scenario.CurrentStep.ShouldNotBeNull();
	}

	[Fact]
	public void Should_Configure_Ssl_Without_CertificateVerification()
	{
		// Arrange & Act
		var scenario = Given()
			.Topic("test-topic")
			.WithSsl("/path/to/ca-cert.pem", enableCertificateVerification: false)
			.Consume();

		// Assert
		scenario.ShouldNotBeNull();
	}

	[Fact]
	public void Should_Configure_MutualTls()
	{
		// Arrange & Act
		var scenario = Given()
			.Topic("test-topic")
			.WithMutualTls(
				certificateLocation: "/path/to/client-cert.pem",
				keyLocation: "/path/to/client-key.pem"
			)
			.Consume();

		// Assert
		scenario.ShouldNotBeNull();
	}

	[Fact]
	public void Should_Configure_MutualTls_With_CaAndPassword()
	{
		// Arrange & Act
		var scenario = Given()
			.Topic("test-topic")
			.WithMutualTls(
				certificateLocation: "/path/to/client-cert.pem",
				keyLocation: "/path/to/client-key.pem",
				caLocation: "/path/to/ca-cert.pem",
				keyPassword: "password123"
			)
			.Consume();

		// Assert
		scenario.ShouldNotBeNull();
	}

	[Fact]
	public void Should_Configure_Ssl_Via_Config()
	{
		// Arrange & Act
		var scenario = Given()
			.Topic("test-topic")
			.WithKafkaAuth(config =>
			{
				config.UseSsl("/path/to/ca-cert.pem");
			})
			.Consume();

		// Assert
		scenario.ShouldNotBeNull();
	}

	[Fact]
	public void SslConfig_Should_Default_To_EnableVerification()
	{
		// Arrange & Act
		var config = new SslConfig();

		// Assert
		config.EnableSslCertificateVerification.ShouldBeTrue();
	}

	[Fact]
	public void KafkaAuthConfig_UseSsl_Should_Set_Correct_Type()
	{
		// Arrange
		var config = new KafkaAuthConfig();

		// Act
		config.UseSsl("/path/to/ca-cert.pem");

		// Assert
		config.Type.ShouldBe(KafkaAuthenticationType.Ssl);
		config.Ssl.ShouldNotBeNull();
		config.Ssl!.SslCaLocation.ShouldBe("/path/to/ca-cert.pem");
		config.Ssl.EnableSslCertificateVerification.ShouldBeTrue();
	}

	[Fact]
	public void KafkaAuthConfig_UseMutualTls_Should_Set_Correct_Type()
	{
		// Arrange
		var config = new KafkaAuthConfig();

		// Act
		config.UseMutualTls(
			certificateLocation: "/cert.pem",
			keyLocation: "/key.pem"
		);

		// Assert
		config.Type.ShouldBe(KafkaAuthenticationType.MutualTls);
		config.Ssl.ShouldNotBeNull();
		config.Ssl!.SslCertificateLocation.ShouldBe("/cert.pem");
		config.Ssl.SslKeyLocation.ShouldBe("/key.pem");
	}

	[Fact]
	public void Should_Throw_When_Not_Kafka_Step()
	{
		// Arrange
		var scenario = Given();

		// Act & Assert
		Should.Throw<InvalidOperationException>(() =>
			scenario.WithSsl("/path/to/ca-cert.pem"));
	}

	[Fact(Skip = "Integration test - requires Kafka broker with SSL")]
	public void Integration_Should_Connect_With_Ssl()
	{
		var caLocation = Environment.GetEnvironmentVariable("KAFKA_CA_CERT_PATH");
		var bootstrapServers = Environment.GetEnvironmentVariable("KAFKA_SSL_BOOTSTRAP_SERVERS");

		if (!string.IsNullOrEmpty(caLocation) && !string.IsNullOrEmpty(bootstrapServers))
		{
			Given()
				.Topic("test-topic")
				.WithBootstrapServers(bootstrapServers)
				.WithSsl(caLocation)
				.Consume()
				.Validate(result =>
				{
					result.Success.ShouldBeTrue();
				});
		}
	}

	[Fact(Skip = "Integration test - requires Kafka broker with mutual TLS")]
	public void Integration_Should_Connect_With_MutualTls()
	{
		var clientCert = Environment.GetEnvironmentVariable("KAFKA_CLIENT_CERT_PATH");
		var clientKey = Environment.GetEnvironmentVariable("KAFKA_CLIENT_KEY_PATH");
		var caCert = Environment.GetEnvironmentVariable("KAFKA_CA_CERT_PATH");

		if (!string.IsNullOrEmpty(clientCert) && !string.IsNullOrEmpty(clientKey))
		{
			Given()
				.Topic("test-topic")
				.WithMutualTls(clientCert, clientKey, caCert)
				.Consume()
				.Validate(result =>
				{
					result.Success.ShouldBeTrue();
				});
		}
	}
}
