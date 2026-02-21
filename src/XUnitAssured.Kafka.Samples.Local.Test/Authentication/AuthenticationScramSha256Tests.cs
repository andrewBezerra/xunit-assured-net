using Shouldly;

using XUnitAssured.Kafka.Extensions;
using XUnitAssured.Kafka.Testing;

namespace XUnitAssured.Kafka.Samples.Local.Test.Authentication;

[Trait("Component", "Kafka")]
[Trait("Category", "Authentication")]
[Trait("AuthenticationType", "ScramSha256")]
[Trait("Environment", "Local")]
public class AuthenticationScramSha256Tests : KafkaTestBase<KafkaClassFixture>, IClassFixture<KafkaClassFixture>
{
	private const string CertsPath = "docker/config/certs";

	public AuthenticationScramSha256Tests(KafkaClassFixture fixture) : base(fixture)
	{
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
		.When()
			.Execute()
		.Then()
			.AssertSuccess();

		// Act & Assert - Consume
		Given()
			.Topic(topic)
			.Consume()
			.WithGroupId(groupId)
		.When()
			.Execute()
		.Then()
			.AssertSuccess()
			.AssertMessage<string>(msg => msg.ShouldBe(message));
	}
}
