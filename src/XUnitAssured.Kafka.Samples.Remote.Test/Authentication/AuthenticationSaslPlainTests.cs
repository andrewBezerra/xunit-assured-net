using Shouldly;

using XUnitAssured.Kafka.Extensions;
using XUnitAssured.Kafka.Testing;

namespace XUnitAssured.Kafka.Samples.Local.Test.Authentication;

[Trait("Component", "Kafka")]
[Trait("Category", "Authentication")]
[Trait("AuthenticationType", "SaslPlain")]
[Trait("Environment", "Local")]

public class AuthenticationSaslPlainTests : KafkaTestBase<KafkaClassFixture>, IClassFixture<KafkaClassFixture>
{

	public AuthenticationSaslPlainTests(KafkaClassFixture fixture) : base(fixture)
	{
	}

	[Fact(Skip = "Remote test - requires deployed API environment", DisplayName = "SASL/PLAIN should produce and consume successfully")]
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
