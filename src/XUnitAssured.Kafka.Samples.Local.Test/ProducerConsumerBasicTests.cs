using Shouldly;
using XUnitAssured.Kafka.Extensions;
using XUnitAssured.Kafka.Testing;

namespace XUnitAssured.Kafka.Samples.Local.Test;

[Trait("Component", "Kafka")]
[Trait("Category", "ProducerConsumer")]
[Trait("Environment", "Local")]
/// <summary>
/// Local integration tests demonstrating Kafka Producer and Consumer operations.
/// Tests produce messages and immediately consume them to validate end-to-end flow.
/// </summary>
/// <remarks>
/// These tests require a local Kafka instance running via Docker Compose.
/// Ensure Kafka is running before executing tests:
/// <code>
/// docker-compose up -d
/// </code>
/// 
/// Kafka UI available at: http://localhost:8083
/// 
/// Prerequisites:
/// - Docker and Docker Compose installed
/// - Kafka accessible at localhost:29092
/// - testsettings.json configured with local Docker settings
/// </remarks>
public class ProducerConsumerBasicTests : KafkaTestBase<KafkaClassFixture>, IClassFixture<KafkaClassFixture>
{
	public ProducerConsumerBasicTests(KafkaClassFixture fixture) : base(fixture)
	{
	}

	[Fact(DisplayName = "Produce and consume simple string message should succeed")]
	public void Example01_ProduceAndConsumeString_ShouldSucceed()
	{
		// Arrange
		var topic = GenerateUniqueTopic("string-test");
		var uniqueGroupId = $"string-test-{Guid.NewGuid():N}";
		var expectedMessage = $"Test message {GenerateMessageId()}";

		// Act & Assert - Produce message
		Given()
			.Topic(topic)
			.Produce(expectedMessage)
		.When()
			.Execute()
		.Then()
			.AssertSuccess();

		// Act & Assert - Consume message
		Given()
			.Topic(topic)
			.Consume()
			.WithGroupId(uniqueGroupId)
		.When()
			.Execute()
		.Then()
			.AssertSuccess()
			.AssertMessage<string>(msg =>
			{
				msg.ShouldNotBeNullOrEmpty();
				msg.ShouldContain("Test message");
			});
	}

	[Fact(DisplayName = "Produce and consume JSON object should succeed")]
	public void Example02_ProduceAndConsumeJson_ShouldSucceed()
	{
		// Arrange
		var topic = GenerateUniqueTopic("json-test");
		var uniqueGroupId = $"json-test-{Guid.NewGuid():N}";
		var expectedMessage = new TestMessage
		{
			Id = 1,
			Value = "Test",
			Timestamp = DateTime.UtcNow
		};

		// Act & Assert - Produce
		Given()
			.Topic(topic)
			.Produce(expectedMessage)
		.When()
			.Execute()
		.Then()
			.AssertSuccess();

		// Act & Assert - Consume and validate JSON fields
		Given()
			.Topic(topic)
			.Consume()
			.WithSchema(typeof(TestMessage))
			.WithGroupId(uniqueGroupId)
		.When()
			.Execute()
		.Then()
			.AssertSuccess()
			.AssertJsonPath<int>("$.id", id => id.ShouldBe(expectedMessage.Id))
			.AssertJsonPath<string>("$.value", value => value.ShouldBe(expectedMessage.Value))
			.AssertJsonPath<string>("$.timestamp", ts => ts.ShouldNotBeNullOrEmpty());
	}

	[Fact(DisplayName = "Produce and consume message with headers should succeed")]
	public void Example03_ProduceAndConsumeWithHeaders_ShouldSucceed()
	{
		// Arrange
		var topic = GenerateUniqueTopic("headers-test");
		var uniqueGroupId = $"headers-test-{Guid.NewGuid():N}";
		var message = $"Message with headers {GenerateMessageId()}";
		var correlationId = Guid.NewGuid().ToString();

		// Act & Assert - Produce with headers
		Given()
			.Topic(topic)
			.Produce(message)
			.WithHeader("correlation-id", System.Text.Encoding.UTF8.GetBytes(correlationId))
			.WithHeader("source", System.Text.Encoding.UTF8.GetBytes("xunit-test"))
			.WithHeader("timestamp", System.Text.Encoding.UTF8.GetBytes(DateTime.UtcNow.ToString("O")))
		.When()
			.Execute()
		.Then()
			.AssertSuccess();

		// Act & Assert - Consume and validate headers
		Given()
			.Topic(topic)
			.Consume()
			.WithGroupId(uniqueGroupId)
		.When()
			.Execute()
		.Then()
			.AssertSuccess()
			.AssertHeader("correlation-id", correlationId)
			.AssertHeader("source", "xunit-test")
			.AssertMessage<string>(msg => msg.ShouldBe(message));
	}

	[Fact(DisplayName = "Produce multiple messages and consume all should succeed")]
	public void Example04_ProduceBatchAndConsumeAll_ShouldSucceed()
	{
		// Arrange
		var topic = GenerateUniqueTopic("batch-test");
		var uniqueGroupId = $"batch-test-{Guid.NewGuid():N}";
		var messageCount = 5;
		var messages = Enumerable.Range(1, messageCount)
			.Select(i => (object)new TestMessage
			{
				Id = i,
				Value = $"Message {i}",
				Timestamp = DateTime.UtcNow
			})
			.ToList();

		// Act & Assert - Produce all messages simultaneously using ProduceBatch
		Given()
			.Topic(topic)
			.ProduceBatch(messages)
		.When()
			.Execute()
		.Then()
			.AssertSuccess()
			.AssertBatchCount(messageCount);

		// Act & Assert - Consume all messages using ConsumeBatch
		Given()
			.Topic(topic)
			.ConsumeBatch(messageCount)
			.WithSchema(typeof(TestMessage))
			.WithGroupId(uniqueGroupId)
		.When()
			.Execute()
		.Then()
			.AssertSuccess()
			.AssertBatchCount(messageCount);
	}

	[Fact(DisplayName = "Consume from empty topic should timeout gracefully")]
	public void Example05_ConsumeWithTimeout_ShouldTimeout()
	{
		// Arrange
		var emptyTopic = GenerateUniqueTopic("empty-topic");
		var uniqueGroupId = $"timeout-test-{Guid.NewGuid():N}";

		// Act & Assert - Should timeout gracefully without messages
		Given()
			.Topic(emptyTopic)
			.Consume()
			.WithTimeout(TimeSpan.FromSeconds(2))
			.WithGroupId(uniqueGroupId)
		.When()
			.Execute()
		.Then()
			.AssertFailure();
	}

	[Fact(DisplayName = "Produce message with key should succeed")]
	public void Example06_ProduceWithKey_ShouldSucceed()
	{
		// Arrange
		var topic = GenerateUniqueTopic("key-test");
		var uniqueGroupId = $"key-test-{Guid.NewGuid():N}";
		var key = $"key-{GenerateMessageId()}";
		var value = $"Value for key {key}";

		// Act & Assert - Produce with key
		Given()
			.Topic(topic)
			.Produce(key, value)
		.When()
			.Execute()
		.Then()
			.AssertSuccess();

		// Act & Assert - Consume and validate key + message
		Given()
			.Topic(topic)
			.Consume()
			.WithGroupId(uniqueGroupId)
		.When()
			.Execute()
		.Then()
			.AssertSuccess()
			.AssertKey(k => k.ShouldBe(key))
			.AssertMessage<string>(msg => msg.ShouldBe(value));
	}
}

/// <summary>
/// Helper class for JSON message tests.
/// </summary>
public record TestMessage
{
	public int Id { get; init; }
	public string Value { get; init; } = string.Empty;
	public DateTime Timestamp { get; init; }
}
