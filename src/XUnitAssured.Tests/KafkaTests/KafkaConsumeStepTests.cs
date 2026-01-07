using XUnitAssured.Core.Storage;
using XUnitAssured.Kafka.Steps;

namespace XUnitAssured.Tests.KafkaTests;

[Trait("Category", "Kafka")]
[Trait("Component", "ConsumeStep")]
/// <summary>
/// Unit tests for KafkaConsumeStep and Kafka DSL.
/// Note: These are unit tests for the step structure, not integration tests.
/// Integration tests with real Kafka require a running Kafka instance.
/// </summary>
public class KafkaConsumeStepTests
{
	[Fact(DisplayName = "KafkaConsumeStep should have correct StepType value")]
	public void KafkaConsumeStep_Should_Have_Correct_StepType()
	{
		// Arrange & Act
		var step = new KafkaConsumeStep
		{
			Topic = "test-topic"
		};

		// Assert
		step.StepType.ShouldBe("Kafka");
	}

	[Fact(DisplayName = "KafkaConsumeStep should store topic name correctly")]
	public void KafkaConsumeStep_Should_Store_Topic()
	{
		// Arrange & Act
		var step = new KafkaConsumeStep
		{
			Topic = "my-topic"
		};

		// Assert
		step.Topic.ShouldBe("my-topic");
	}

	[Fact(DisplayName = "KafkaConsumeStep should store schema type correctly")]
	public void KafkaConsumeStep_Should_Store_SchemaType()
	{
		// Arrange & Act
		var step = new KafkaConsumeStep
		{
			Topic = "test-topic",
			SchemaType = typeof(string)
		};

		// Assert
		step.SchemaType.ShouldBe(typeof(string));
	}

	[Fact(DisplayName = "KafkaConsumeStep should have default timeout of 30 seconds")]
	public void KafkaConsumeStep_Should_Have_Default_Timeout()
	{
		// Arrange & Act
		var step = new KafkaConsumeStep
		{
			Topic = "test-topic"
		};

		// Assert
		step.Timeout.ShouldBe(TimeSpan.FromSeconds(30));
	}

	[Fact(DisplayName = "KafkaConsumeStep should allow custom timeout configuration")]
	public void KafkaConsumeStep_Should_Allow_Custom_Timeout()
	{
		// Arrange & Act
		var step = new KafkaConsumeStep
		{
			Topic = "test-topic",
			Timeout = TimeSpan.FromSeconds(60)
		};

		// Assert
		step.Timeout.ShouldBe(TimeSpan.FromSeconds(60));
	}

	[Fact(DisplayName = "KafkaConsumeStep should have default bootstrap servers")]
	public void KafkaConsumeStep_Should_Have_Default_BootstrapServers()
	{
		// Arrange & Act
		var step = new KafkaConsumeStep
		{
			Topic = "test-topic"
		};

		// Assert
		step.BootstrapServers.ShouldBe("localhost:9092");
	}

	[Fact(DisplayName = "KafkaConsumeStep should have default consumer group ID")]
	public void KafkaConsumeStep_Should_Have_Default_GroupId()
	{
		// Arrange & Act
		var step = new KafkaConsumeStep
		{
			Topic = "test-topic"
		};

		// Assert
		step.GroupId.ShouldBe("xunitassured-consumer");
	}

	[Fact(DisplayName = "Topic extension should create KafkaConsumeStep")]
	public void Topic_Extension_Should_Create_KafkaStep()
	{
		// Arrange
		var scenario = Given();

		// Act
		scenario.Topic("my-topic");

		// Assert
		scenario.CurrentStep.ShouldNotBeNull();
		scenario.CurrentStep.ShouldBeOfType<KafkaConsumeStep>();
		((KafkaConsumeStep)scenario.CurrentStep).Topic.ShouldBe("my-topic");
	}

	[Fact(DisplayName = "Topic extension should throw ArgumentException when topic is null")]
	public void Topic_Extension_Should_Throw_When_Topic_Is_Null()
	{
		// Arrange
		var scenario = Given();

		// Act & Assert
		Should.Throw<ArgumentException>(() => scenario.Topic(null!));
	}

	[Fact(DisplayName = "Topic extension should throw ArgumentException when topic is empty")]
	public void Topic_Extension_Should_Throw_When_Topic_Is_Empty()
	{
		// Arrange
		var scenario = Given();

		// Act & Assert
		Should.Throw<ArgumentException>(() => scenario.Topic("   "));
	}

	[Fact(DisplayName = "WithSchema should update schema type in KafkaConsumeStep")]
	public void WithSchema_Should_Update_SchemaType()
	{
		// Arrange
		var scenario = Given().Topic("test-topic");

		// Act
		scenario.WithSchema(typeof(string));

		// Assert
		var kafkaStep = (KafkaConsumeStep)scenario.CurrentStep;
		kafkaStep.SchemaType.ShouldBe(typeof(string));
	}

	[Fact(DisplayName = "WithTimeout should update timeout in KafkaConsumeStep")]
	public void WithTimeout_Should_Update_Timeout()
	{
		// Arrange
		var scenario = Given().Topic("test-topic");

		// Act
		scenario.WithTimeout(TimeSpan.FromSeconds(60));

		// Assert
		var kafkaStep = (KafkaConsumeStep)scenario.CurrentStep;
		kafkaStep.Timeout.ShouldBe(TimeSpan.FromSeconds(60));
	}

	[Fact(DisplayName = "WithBootstrapServers should update bootstrap servers in KafkaConsumeStep")]
	public void WithBootstrapServers_Should_Update_BootstrapServers()
	{
		// Arrange
		var scenario = Given().Topic("test-topic");

		// Act
		scenario.WithBootstrapServers("kafka:9092");

		// Assert
		var kafkaStep = (KafkaConsumeStep)scenario.CurrentStep;
		kafkaStep.BootstrapServers.ShouldBe("kafka:9092");
	}

	[Fact(DisplayName = "WithGroupId should update consumer group ID in KafkaConsumeStep")]
	public void WithGroupId_Should_Update_GroupId()
	{
		// Arrange
		var scenario = Given().Topic("test-topic");

		// Act
		scenario.WithGroupId("my-consumer-group");

		// Assert
		var kafkaStep = (KafkaConsumeStep)scenario.CurrentStep;
		kafkaStep.GroupId.ShouldBe("my-consumer-group");
	}

	[Fact(DisplayName = "Kafka DSL should chain fluently with all configuration methods")]
	public void Kafka_DSL_Should_Chain_Fluently()
	{
		// Act
		var scenario = Given()
			.Topic("test-topic")
			.WithSchema(typeof(string))
			.WithTimeout(TimeSpan.FromSeconds(45))
			.WithBootstrapServers("kafka:9092")
			.WithGroupId("test-group");

		// Assert
		scenario.ShouldNotBeNull();
		var kafkaStep = (KafkaConsumeStep)scenario.CurrentStep;
		kafkaStep.Topic.ShouldBe("test-topic");
		kafkaStep.SchemaType.ShouldBe(typeof(string));
		kafkaStep.Timeout.ShouldBe(TimeSpan.FromSeconds(45));
		kafkaStep.BootstrapServers.ShouldBe("kafka:9092");
		kafkaStep.GroupId.ShouldBe("test-group");
	}
}
