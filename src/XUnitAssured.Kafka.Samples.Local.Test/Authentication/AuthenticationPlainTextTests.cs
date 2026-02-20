using Shouldly;

using XUnitAssured.Kafka.Extensions;
using XUnitAssured.Kafka.Testing;

namespace XUnitAssured.Kafka.Samples.Local.Test.Authentication;

[Trait("Component", "Kafka")]
[Trait("Category", "Authentication")]
[Trait("AuthenticationType", "PlainText")]
[Trait("Environment", "Local")]
public class AuthenticationPlainTextTests : KafkaTestBase<KafkaClassFixture>, IClassFixture<KafkaClassFixture>
{

	public AuthenticationPlainTextTests(KafkaClassFixture fixture) : base(fixture)
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
