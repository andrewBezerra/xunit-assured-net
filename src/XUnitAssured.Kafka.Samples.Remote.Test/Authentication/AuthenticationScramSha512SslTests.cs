using Shouldly;

using XUnitAssured.Kafka.Extensions;
using XUnitAssured.Kafka.Testing;

namespace XUnitAssured.Kafka.Samples.Local.Test.Authentication;

[Trait("Component", "Kafka")]
[Trait("Category", "Authentication")]
[Trait("Environment", "Local")]
public class AuthenticationScramSha512SslTests : KafkaTestBase<KafkaClassFixture>, IClassFixture<KafkaClassFixture>
{
	private const string CertsPath = "docker/config/certs";

	public AuthenticationScramSha512SslTests(KafkaClassFixture fixture) : base(fixture)
	{
	}



	[Fact(Skip = "Remote test - requires deployed API environment", DisplayName = "SASL/SSL should produce and consume successfully")]
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
