using System;
using XUnitAssured.Core.Abstractions;
using XUnitAssured.Core.DSL;

namespace XUnitAssured.Kafka.Testing;

/// <summary>
/// Base class for Kafka integration tests using a specific fixture type.
/// Pre-configures the DSL with shared producer and bootstrap servers from the fixture.
/// </summary>
/// <typeparam name="TFixture">The fixture type, must extend KafkaClassFixture.</typeparam>
/// <example>
/// <code>
/// public class MyTests : KafkaTestBase&lt;MyFixture&gt;, IClassFixture&lt;MyFixture&gt;
/// {
///     public MyTests(MyFixture fixture) : base(fixture) { }
///     
///     [Fact]
///     public void Should_Produce()
///     {
///         Given()
///             .Topic("my-topic")
///             .Produce("hello")
///         .When()
///             .Execute()
///         .Then()
///             .AssertSuccess();
///     }
/// }
/// </code>
/// </example>
public abstract class KafkaTestBase<TFixture> where TFixture : KafkaClassFixture
{
	protected readonly TFixture Fixture;

	protected KafkaTestBase(TFixture fixture)
	{
		Fixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
	}

	/// <summary>
	/// Starts a new test scenario pre-configured with the fixture's shared producer
	/// and bootstrap servers. Steps will automatically use the cached producer.
	/// </summary>
	protected ITestScenario Given()
	{
		var scenario = ScenarioDsl.Given();

		// Store shared producer and config in context for steps to use
		scenario.Context.SetProperty("_KafkaSharedProducer", Fixture.SharedProducer);
		scenario.Context.SetProperty("_KafkaSharedProducerErrors", Fixture.SharedProducerErrors);
		scenario.Context.SetProperty("_KafkaBootstrapServers", Fixture.BootstrapServers);
		scenario.Context.SetProperty("_KafkaGroupId", Fixture.DefaultGroupId);
		scenario.Context.SetProperty("_KafkaAuthConfig", Fixture.KafkaSettings.Authentication);

		return scenario;
	}

	/// <summary>
	/// Generates a unique message ID for test isolation.
	/// </summary>
	protected string GenerateMessageId() => Guid.NewGuid().ToString();

	/// <summary>
	/// Generates a unique topic name for test isolation.
	/// </summary>
	protected string GenerateUniqueTopic(string baseName = "test-topic")
	{
		return $"{baseName}-{Guid.NewGuid():N}";
	}
}

/// <summary>
/// Base class for Kafka integration tests using the default KafkaClassFixture.
/// </summary>
/// <example>
/// <code>
/// public class MyTests : KafkaTestBase, IClassFixture&lt;KafkaClassFixture&gt;
/// {
///     public MyTests(KafkaClassFixture fixture) : base(fixture) { }
/// }
/// </code>
/// </example>
public abstract class KafkaTestBase : KafkaTestBase<KafkaClassFixture>
{
	protected KafkaTestBase(KafkaClassFixture fixture) : base(fixture)
	{
	}
}
