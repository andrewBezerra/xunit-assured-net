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
	/// <summary>
	/// The class fixture shared by every test in the class: it holds the broker
	/// address, credentials and the shared producer that <see cref="Given"/> hands
	/// to each scenario.
	/// </summary>
	protected readonly TFixture Fixture;

	/// <summary>
	/// Receives the fixture xUnit injects for the test class.
	/// </summary>
	/// <param name="fixture">The class fixture; xUnit supplies it through <c>IClassFixture&lt;TFixture&gt;</c>.</param>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="fixture"/> is null.</exception>
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
		// The fixture knows what it provides; one place defines the context keys.
		Fixture.Seed(scenario.Context);

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
	/// <summary>
	/// Receives the <see cref="KafkaClassFixture"/> xUnit injects for the test class.
	/// </summary>
	/// <param name="fixture">The class fixture; xUnit supplies it through <c>IClassFixture&lt;KafkaClassFixture&gt;</c>.</param>
	protected KafkaTestBase(KafkaClassFixture fixture) : base(fixture)
	{
	}
}
