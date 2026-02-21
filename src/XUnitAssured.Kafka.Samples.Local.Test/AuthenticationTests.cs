using Shouldly;

using XUnitAssured.Kafka.Extensions;
using XUnitAssured.Kafka.Testing;

namespace XUnitAssured.Kafka.Samples.Local.Test;

[Trait("Component", "Kafka")]
[Trait("Category", "Authentication")]
[Trait("Environment", "Local")]

public class AuthenticationTests : KafkaTestBase<KafkaClassFixture>, IClassFixture<KafkaClassFixture>
{
	private const string CertsPath = "docker/config/certs";

	public AuthenticationTests(KafkaClassFixture fixture) : base(fixture)
	{
	}


	[Fact(Skip = "Remote test - requires deployed API environment", DisplayName = "SSL (one-way) should produce and consume successfully")]
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

	[Fact(Skip = "Remote test - requires deployed API environment", DisplayName = "Mutual TLS (mTLS) should produce and consume successfully")]
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

	[Fact(Skip = "Remote test - requires deployed API environment", DisplayName = "SASL/PLAIN with invalid credentials should fail")]
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
