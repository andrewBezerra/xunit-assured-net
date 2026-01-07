using XUnitAssured.Kafka.Configuration;
using XUnitAssured.Kafka.Extensions;
using XUnitAssured.Kafka.Steps;
using Xunit;
using Shouldly;

namespace XUnitAssured.Tests.KafkaTests;

[Trait("Category", "Kafka")]
[Trait("Authentication", "SASL-PLAIN")]
/// <summary>
/// Tests for SASL/PLAIN authentication in Kafka.
/// </summary>
public class SaslPlainAuthTests
{
	[Fact(DisplayName = "Should configure SASL/PLAIN authentication with explicit credentials")]
	public void Should_Configure_SaslPlain_With_Explicit_Credentials()
	{
		// Arrange & Act
		var scenario = Given()
			.Topic("test-topic")
			.Consume()
			.WithSaslPlain("username", "password");

		// Assert
		scenario.ShouldNotBeNull();
		scenario.CurrentStep.ShouldNotBeNull();
	}

	[Fact(DisplayName = "Should configure SASL/PLAIN authentication without SSL")]
	public void Should_Configure_SaslPlain_Without_Ssl()
	{
		// Arrange & Act
		var scenario = Given()
			.Topic("test-topic")
			.Consume()
			.WithSaslPlain("username", "password", useSsl: false);

		// Assert
		scenario.ShouldNotBeNull();
	}

	[Fact(DisplayName = "Should configure SASL/PLAIN authentication via Kafka auth config")]
	public void Should_Configure_SaslPlain_Via_Config()
	{
		// Arrange & Act
		var scenario = Given()
			.Topic("test-topic")
			.Consume()
			.WithKafkaAuth(config =>
			{
				config.UseSaslPlain("username", "password");
			});

		// Assert
		scenario.ShouldNotBeNull();
	}

	[Fact(DisplayName = "Should disable Kafka authentication when requested")]
	public void Should_Disable_Authentication()
	{
		// Arrange & Act
		var scenario = Given()
			.Topic("test-topic")
			.Consume()
			.WithNoKafkaAuth();

		// Assert
		scenario.ShouldNotBeNull();
	}

	[Fact(DisplayName = "SaslPlainConfig should default to use SSL")]
	public void SaslPlainConfig_Should_Default_To_UseSsl()
	{
		// Arrange & Act
		var config = new SaslPlainConfig
		{
			Username = "user",
			Password = "pass"
		};

		// Assert
		config.UseSsl.ShouldBeTrue();
	}

	[Fact(DisplayName = "KafkaAuthConfig UseSaslPlain should set correct authentication type")]
	public void KafkaAuthConfig_UseSaslPlain_Should_Set_Correct_Type()
	{
		// Arrange
		var config = new KafkaAuthConfig();

		// Act
		config.UseSaslPlain("user", "pass", useSsl: true);

		// Assert
		config.Type.ShouldBe(KafkaAuthenticationType.SaslSsl);
		config.SaslPlain.ShouldNotBeNull();
		config.SaslPlain!.Username.ShouldBe("user");
		config.SaslPlain.Password.ShouldBe("pass");
	}

	[Fact(DisplayName = "Should throw InvalidOperationException when not Kafka step")]
	public void Should_Throw_When_Not_Kafka_Step()
	{
		// Arrange
		var scenario = Given();

		// Act & Assert
		Should.Throw<InvalidOperationException>(() =>
			scenario.WithSaslPlain("user", "pass"));
	}

	[Fact(DisplayName = "Should configure SASL/PLAIN authentication after Topic is set")]
	public void Should_Configure_SaslPlain_After_Topic()
	{
		// Arrange & Act
		var scenario = Given()
			.Topic("test-topic")
			.Consume()
			.WithSaslPlain("username", "password");

		// Assert
		scenario.ShouldNotBeNull();
		scenario.CurrentStep.ShouldNotBeNull();
		scenario.CurrentStep.ShouldBeOfType<KafkaConsumeStep>();
	}

	[Fact(Skip = "Integration test - requires Kafka broker with SASL/PLAIN", DisplayName = "Integration test should connect to Kafka with SASL/PLAIN authentication")]
	public void Integration_Should_Connect_With_SaslPlain()
	{
		var username = Environment.GetEnvironmentVariable("KAFKA_USERNAME");
		var password = Environment.GetEnvironmentVariable("KAFKA_PASSWORD");

		if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
		{
			Given()
				.Topic("test-topic")
				.Consume()
				.WithBootstrapServers("pkc-xxxxx.us-east-1.aws.confluent.cloud:9092")
				.WithSaslPlain(username, password)
				.Validate(result =>
				{
					result.Success.ShouldBeTrue();
				});
		}
	}
}

