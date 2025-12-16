using XUnitAssured.Kafka.Configuration;
using XUnitAssured.Kafka.Extensions;
using Xunit;
using Shouldly;

namespace XUnitAssured.Tests.KafkaTests;

/// <summary>
/// Tests for SASL/PLAIN authentication in Kafka.
/// </summary>
public class SaslPlainAuthTests
{
	[Fact]
	public void Should_Configure_SaslPlain_With_Explicit_Credentials()
	{
		// Arrange & Act
		var scenario = Given()
			.Topic("test-topic")
			.WithSaslPlain("username", "password")
			.Consume();

		// Assert
		scenario.ShouldNotBeNull();
		scenario.CurrentStep.ShouldNotBeNull();
	}

	[Fact]
	public void Should_Configure_SaslPlain_Without_Ssl()
	{
		// Arrange & Act
		var scenario = Given()
			.Topic("test-topic")
			.WithSaslPlain("username", "password", useSsl: false)
			.Consume();

		// Assert
		scenario.ShouldNotBeNull();
	}

	[Fact]
	public void Should_Configure_SaslPlain_Via_Config()
	{
		// Arrange & Act
		var scenario = Given()
			.Topic("test-topic")
			.WithKafkaAuth(config =>
			{
				config.UseSaslPlain("username", "password");
			})
			.Consume();

		// Assert
		scenario.ShouldNotBeNull();
	}

	[Fact]
	public void Should_Disable_Authentication()
	{
		// Arrange & Act
		var scenario = Given()
			.Topic("test-topic")
			.WithNoKafkaAuth()
			.Consume();

		// Assert
		scenario.ShouldNotBeNull();
	}

	[Fact]
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

	[Fact]
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

	[Fact]
	public void Should_Throw_When_Not_Kafka_Step()
	{
		// Arrange
		var scenario = Given();

		// Act & Assert
		Should.Throw<InvalidOperationException>(() =>
			scenario.WithSaslPlain("user", "pass"));
	}

	[Fact(Skip = "Integration test - requires Kafka broker with SASL/PLAIN")]
	public void Integration_Should_Connect_With_SaslPlain()
	{
		var username = Environment.GetEnvironmentVariable("KAFKA_USERNAME");
		var password = Environment.GetEnvironmentVariable("KAFKA_PASSWORD");

		if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
		{
			Given()
				.Topic("test-topic")
				.WithBootstrapServers("pkc-xxxxx.us-east-1.aws.confluent.cloud:9092")
				.WithSaslPlain(username, password)
				.Consume()
				.Validate(result =>
				{
					result.Success.ShouldBeTrue();
				});
		}
	}
}
