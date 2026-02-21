using Shouldly;

using XUnitAssured.Kafka.Extensions;
using XUnitAssured.Kafka.Testing;

namespace XUnitAssured.Kafka.Samples.Local.Test.Authentication;

[Trait("Component", "Kafka")]
[Trait("Category", "Authentication")]
[Trait("Environment", "Local")]

public class AuthenticationScramSha512Tests : KafkaTestBase<KafkaClassFixture>, IClassFixture<KafkaClassFixture>
{
	private const string CertsPath = "docker/config/certs";

	public AuthenticationScramSha512Tests(KafkaClassFixture fixture) : base(fixture)
	{
	}

	[Fact(Skip = "Remote test - requires deployed API environment", DisplayName = "SASL/SCRAM-SHA-512 should produce and consume successfully")]
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
