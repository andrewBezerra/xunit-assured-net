using Confluent.Kafka;
using XUnitAssured.Kafka.Configuration;
using XUnitAssured.Kafka.Extensions;
using Xunit;
using Shouldly;

namespace XUnitAssured.Tests.KafkaTests;

[Trait("Category", "Kafka")]
[Trait("Authentication", "SASL-SCRAM")]
/// <summary>
/// Tests for SASL/SCRAM authentication in Kafka.
/// </summary>
public class SaslScramAuthTests
{
	[Fact(DisplayName = "Should configure SASL/SCRAM-SHA-256 authentication with explicit credentials")]
	public void Should_Configure_SaslScram256_With_Explicit_Credentials()
	{
		// Arrange & Act
		var scenario = Given()
			.Topic("test-topic")
			.WithSaslScram256("username", "password")
			.Consume();

		// Assert
		scenario.ShouldNotBeNull();
		scenario.CurrentStep.ShouldNotBeNull();
	}

	[Fact(DisplayName = "Should configure SASL/SCRAM-SHA-512 authentication with explicit credentials")]
	public void Should_Configure_SaslScram512_With_Explicit_Credentials()
	{
		// Arrange & Act
		var scenario = Given()
			.Topic("test-topic")
			.WithSaslScram512("username", "password")
			.Consume();

		// Assert
		scenario.ShouldNotBeNull();
	}

	[Fact(DisplayName = "Should configure SASL/SCRAM authentication without SSL")]
	public void Should_Configure_SaslScram_Without_Ssl()
	{
		// Arrange & Act
		var scenario = Given()
			.Topic("test-topic")
			.WithSaslScram256("username", "password", useSsl: false)
			.Consume();

		// Assert
		scenario.ShouldNotBeNull();
	}

	[Fact(DisplayName = "Should configure SASL/SCRAM authentication via Kafka auth config")]
	public void Should_Configure_SaslScram_Via_Config()
	{
		// Arrange & Act
		var scenario = Given()
			.Topic("test-topic")
			.WithKafkaAuth(config =>
			{
				config.UseSaslScram256("username", "password");
			})
			.Consume();

		// Assert
		scenario.ShouldNotBeNull();
	}

	[Fact(DisplayName = "SaslScramConfig should default to SCRAM-SHA-256 mechanism")]
	public void SaslScramConfig_Should_Default_To_ScramSha256()
	{
		// Arrange & Act
		var config = new SaslScramConfig
		{
			Username = "user",
			Password = "pass"
		};

		// Assert
		config.Mechanism.ShouldBe(SaslMechanism.ScramSha256);
		config.UseSsl.ShouldBeTrue();
	}

	[Fact(DisplayName = "KafkaAuthConfig UseSaslScram256 should set correct authentication type")]
	public void KafkaAuthConfig_UseSaslScram256_Should_Set_Correct_Type()
	{
		// Arrange
		var config = new KafkaAuthConfig();

		// Act
		config.UseSaslScram256("user", "pass", useSsl: true);

		// Assert
		config.Type.ShouldBe(KafkaAuthenticationType.SaslScram256);
		config.SaslScram.ShouldNotBeNull();
		config.SaslScram!.Username.ShouldBe("user");
		config.SaslScram.Password.ShouldBe("pass");
		config.SaslScram.Mechanism.ShouldBe(SaslMechanism.ScramSha256);
	}

	[Fact(DisplayName = "KafkaAuthConfig UseSaslScram512 should set correct authentication type")]
	public void KafkaAuthConfig_UseSaslScram512_Should_Set_Correct_Type()
	{
		// Arrange
		var config = new KafkaAuthConfig();

		// Act
		config.UseSaslScram512("user", "pass", useSsl: true);

		// Assert
		config.Type.ShouldBe(KafkaAuthenticationType.SaslScram512);
		config.SaslScram.ShouldNotBeNull();
		config.SaslScram!.Mechanism.ShouldBe(SaslMechanism.ScramSha512);
	}

	[Fact(DisplayName = "Should throw InvalidOperationException when not Kafka step")]
	public void Should_Throw_When_Not_Kafka_Step()
	{
		// Arrange
		var scenario = Given();

		// Act & Assert
		Should.Throw<InvalidOperationException>(() =>
			scenario.WithSaslScram256("user", "pass"));
	}

	[Fact(Skip = "Integration test - requires Kafka broker with SASL/SCRAM-SHA-256", DisplayName = "Integration test should connect to Kafka with SASL/SCRAM-SHA-256 authentication")]
	public void Integration_Should_Connect_With_SaslScram256()
	{
		var username = Environment.GetEnvironmentVariable("MSK_USERNAME");
		var password = Environment.GetEnvironmentVariable("MSK_PASSWORD");
		var bootstrapServers = Environment.GetEnvironmentVariable("MSK_BOOTSTRAP_SERVERS");

		if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password) && !string.IsNullOrEmpty(bootstrapServers))
		{
			Given()
				.Topic("test-topic")
				.WithBootstrapServers(bootstrapServers)
				.WithSaslScram256(username, password)
				.Consume()
				.Validate(result =>
				{
					result.Success.ShouldBeTrue();
				});
		}
	}

	[Fact(Skip = "Integration test - requires Kafka broker with SASL/SCRAM-SHA-512", DisplayName = "Integration test should connect to Kafka with SASL/SCRAM-SHA-512 authentication")]
	public void Integration_Should_Connect_With_SaslScram512()
	{
		var username = Environment.GetEnvironmentVariable("KAFKA_SCRAM512_USERNAME");
		var password = Environment.GetEnvironmentVariable("KAFKA_SCRAM512_PASSWORD");

		if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
		{
			Given()
				.Topic("test-topic")
				.WithSaslScram512(username, password)
				.Consume()
				.Validate(result =>
				{
					result.Success.ShouldBeTrue();
				});
		}
	}
}
