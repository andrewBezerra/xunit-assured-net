using Shouldly;
using XUnitAssured.Kafka.Extensions;
using XUnitAssured.Kafka.Testing;

namespace XUnitAssured.Kafka.Samples.Local.Test;

[Trait("Component", "Kafka")]
[Trait("Category", "Authentication")]
[Trait("Environment", "Local")]
/// <summary>
/// Integration tests validating all Kafka authentication types.
/// Each test produces and consumes a message using a specific auth mechanism.
/// </summary>
/// <remarks>
/// Prerequisites:
/// 1. Generate certificates: docker/scripts/create-certs.sh
/// 2. Start containers: docker-compose -f docker/docker-compose.yml up -d
/// 3. Wait for kafka-init to create SCRAM users
///
/// Ports:
///   29092 - PLAINTEXT (no auth)
///   29093 - SASL/PLAIN
///   29094 - SASL/SCRAM
///   29095 - SASL/SSL
///   29096 - SSL (one-way)
///   29097 - mTLS
///
/// Credentials:
///   SASL/PLAIN: testuser / testpass
///   SASL/SCRAM: scramuser / scrampass
///   SSL/mTLS:   docker/config/certs/
/// </remarks>
public class AuthenticationTests : KafkaTestBase<KafkaClassFixture>, IClassFixture<KafkaClassFixture>
{
	private const string CertsPath = "docker/config/certs";

	public AuthenticationTests(KafkaClassFixture fixture) : base(fixture)
	{
	}

	[Fact(DisplayName = "Plaintext (no auth) should produce and consume successfully")]
	public void Auth01_Plaintext_ShouldSucceed()
	{
		// Arrange
		var topic = GenerateUniqueTopic("auth-plaintext");
		var groupId = $"auth-plaintext-{Guid.NewGuid():N}";
		var message = $"Plaintext message {GenerateMessageId()}";

		// Act & Assert - Produce
		Given()
			.Topic(topic)
			.Produce(message)
			.WithBootstrapServers("localhost:29092")
		.When()
			.Execute()
		.Then()
			.AssertSuccess();

		// Act & Assert - Consume
		Given()
			.Topic(topic)
			.Consume()
			.WithBootstrapServers("localhost:29092")
			.WithGroupId(groupId)
		.When()
			.Execute()
		.Then()
			.AssertSuccess()
			.AssertMessage<string>(msg => msg.ShouldBe(message));
	}

	[Fact(DisplayName = "SASL/PLAIN should produce and consume successfully")]
	public void Auth02_SaslPlain_ShouldSucceed()
	{
		// Arrange
		var topic = GenerateUniqueTopic("auth-sasl-plain");
		var groupId = $"auth-sasl-plain-{Guid.NewGuid():N}";
		var message = $"SASL/PLAIN message {GenerateMessageId()}";

		// Act & Assert - Produce
		Given()
			.Topic(topic)
			.Produce(message)
			.WithBootstrapServers("localhost:29093")
			.WithAuth(auth => auth.UseSaslPlain("testuser", "testpass", useSsl: false))
		.When()
			.Execute()
		.Then()
			.AssertSuccess();

		// Act & Assert - Consume
		Given()
			.Topic(topic)
			.Consume()
			.WithBootstrapServers("localhost:29093")
			.WithGroupId(groupId)
			.WithAuth(auth => auth.UseSaslPlain("testuser", "testpass", useSsl: false))
		.When()
			.Execute()
		.Then()
			.AssertSuccess()
			.AssertMessage<string>(msg => msg.ShouldBe(message));
	}

	[Fact(DisplayName = "SASL/SCRAM-SHA-256 should produce and consume successfully")]
	public void Auth03_SaslScram256_ShouldSucceed()
	{
		// Arrange
		var topic = GenerateUniqueTopic("auth-scram256");
		var groupId = $"auth-scram256-{Guid.NewGuid():N}";
		var message = $"SCRAM-256 message {GenerateMessageId()}";

		// Act & Assert - Produce
		Given()
			.Topic(topic)
			.Produce(message)
			.WithBootstrapServers("localhost:29094")
			.WithAuth(auth => auth.UseSaslScram256("scramuser", "scrampass", useSsl: false))
		.When()
			.Execute()
		.Then()
			.AssertSuccess();

		// Act & Assert - Consume
		Given()
			.Topic(topic)
			.Consume()
			.WithBootstrapServers("localhost:29094")
			.WithGroupId(groupId)
			.WithAuth(auth => auth.UseSaslScram256("scramuser", "scrampass", useSsl: false))
		.When()
			.Execute()
		.Then()
			.AssertSuccess()
			.AssertMessage<string>(msg => msg.ShouldBe(message));
	}

	[Fact(DisplayName = "SASL/SCRAM-SHA-512 should produce and consume successfully")]
	public void Auth04_SaslScram512_ShouldSucceed()
	{
		// Arrange
		var topic = GenerateUniqueTopic("auth-scram512");
		var groupId = $"auth-scram512-{Guid.NewGuid():N}";
		var message = $"SCRAM-512 message {GenerateMessageId()}";

		// Act & Assert - Produce
		Given()
			.Topic(topic)
			.Produce(message)
			.WithBootstrapServers("localhost:29094")
			.WithAuth(auth => auth.UseSaslScram512("scramuser", "scrampass", useSsl: false))
		.When()
			.Execute()
		.Then()
			.AssertSuccess();

		// Act & Assert - Consume
		Given()
			.Topic(topic)
			.Consume()
			.WithBootstrapServers("localhost:29094")
			.WithGroupId(groupId)
			.WithAuth(auth => auth.UseSaslScram512("scramuser", "scrampass", useSsl: false))
		.When()
			.Execute()
		.Then()
			.AssertSuccess()
			.AssertMessage<string>(msg => msg.ShouldBe(message));
	}

	[Fact(DisplayName = "SASL/SSL should produce and consume successfully")]
	public void Auth05_SaslSsl_ShouldSucceed()
	{
		// Arrange
		var topic = GenerateUniqueTopic("auth-sasl-ssl");
		var groupId = $"auth-sasl-ssl-{Guid.NewGuid():N}";
		var message = $"SASL/SSL message {GenerateMessageId()}";

		// Act & Assert - Produce
		Given()
			.Topic(topic)
			.Produce(message)
			.WithBootstrapServers("localhost:29095")
			.WithAuth(auth => auth.UseSaslPlain("testuser", "testpass", useSsl: true))
		.When()
			.Execute()
		.Then()
			.AssertSuccess();

		// Act & Assert - Consume
		Given()
			.Topic(topic)
			.Consume()
			.WithBootstrapServers("localhost:29095")
			.WithGroupId(groupId)
			.WithAuth(auth => auth.UseSaslPlain("testuser", "testpass", useSsl: true))
		.When()
			.Execute()
		.Then()
			.AssertSuccess()
			.AssertMessage<string>(msg => msg.ShouldBe(message));
	}

	[Fact(DisplayName = "SSL (one-way) should produce and consume successfully")]
	public void Auth06_Ssl_ShouldSucceed()
	{
		// Arrange
		var topic = GenerateUniqueTopic("auth-ssl");
		var groupId = $"auth-ssl-{Guid.NewGuid():N}";
		var message = $"SSL message {GenerateMessageId()}";
		var caLocation = $"{CertsPath}/ca-cert.pem";

		// Act & Assert - Produce
		Given()
			.Topic(topic)
			.Produce(message)
			.WithBootstrapServers("localhost:29096")
			.WithAuth(auth => auth.UseSsl(caLocation))
		.When()
			.Execute()
		.Then()
			.AssertSuccess();

		// Act & Assert - Consume
		Given()
			.Topic(topic)
			.Consume()
			.WithBootstrapServers("localhost:29096")
			.WithGroupId(groupId)
			.WithAuth(auth => auth.UseSsl(caLocation))
		.When()
			.Execute()
		.Then()
			.AssertSuccess()
			.AssertMessage<string>(msg => msg.ShouldBe(message));
	}

	[Fact(DisplayName = "Mutual TLS (mTLS) should produce and consume successfully")]
	public void Auth07_MutualTls_ShouldSucceed()
	{
		// Arrange
		var topic = GenerateUniqueTopic("auth-mtls");
		var groupId = $"auth-mtls-{Guid.NewGuid():N}";
		var message = $"mTLS message {GenerateMessageId()}";
		var caLocation = $"{CertsPath}/ca-cert.pem";
		var certLocation = $"{CertsPath}/client-cert.pem";
		var keyLocation = $"{CertsPath}/client-key.pem";

		// Act & Assert - Produce
		Given()
			.Topic(topic)
			.Produce(message)
			.WithBootstrapServers("localhost:29097")
			.WithAuth(auth => auth.UseMutualTls(certLocation, keyLocation, caLocation))
		.When()
			.Execute()
		.Then()
			.AssertSuccess();

		// Act & Assert - Consume
		Given()
			.Topic(topic)
			.Consume()
			.WithBootstrapServers("localhost:29097")
			.WithGroupId(groupId)
			.WithAuth(auth => auth.UseMutualTls(certLocation, keyLocation, caLocation))
		.When()
			.Execute()
		.Then()
			.AssertSuccess()
			.AssertMessage<string>(msg => msg.ShouldBe(message));
	}

	[Fact(DisplayName = "SASL/PLAIN with invalid credentials should fail")]
	public void Auth08_InvalidCredentials_ShouldFail()
	{
		// Arrange
		var topic = GenerateUniqueTopic("auth-invalid");

		// Act & Assert - Produce with wrong password should fail
		Given()
			.Topic(topic)
			.Produce("should fail")
			.WithBootstrapServers("localhost:29093")
			.WithAuth(auth => auth.UseSaslPlain("testuser", "wrong-password", useSsl: false))
			.WithTimeout(TimeSpan.FromSeconds(5))
		.When()
			.Execute()
		.Then()
			.AssertFailure();
	}
}
