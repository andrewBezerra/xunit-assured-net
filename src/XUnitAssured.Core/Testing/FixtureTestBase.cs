using System;
using XUnitAssured.Core.Abstractions;
using XUnitAssured.Core.DSL;

namespace XUnitAssured.Core.Testing;

/// <summary>
/// Generic abstract base class for integration tests using XUnitAssured with any fixture type.
/// Provides a clean Given() method that automatically uses the injected fixture.
/// This class is technology-agnostic and can be used with HTTP, Kafka, FileSystem, S3, or any other fixture type.
/// </summary>
/// <typeparam name="TFixture">The fixture type to be injected via xUnit's IClassFixture</typeparam>
/// <example>
/// <code>
/// // Example with HTTP fixture
/// public class MyApiTests : FixtureTestBase&lt;HttpTestFixture&gt;, IClassFixture&lt;HttpTestFixture&gt;
/// {
///     public MyApiTests(HttpTestFixture fixture) : base(fixture) { }
/// }
/// 
/// // Example with Kafka fixture
/// public class MyKafkaTests : FixtureTestBase&lt;KafkaTestFixture&gt;, IClassFixture&lt;KafkaTestFixture&gt;
/// {
///     public MyKafkaTests(KafkaTestFixture fixture) : base(fixture) { }
/// }
/// </code>
/// </example>
public abstract class FixtureTestBase<TFixture>
{
	/// <summary>
	/// The test fixture instance injected via xUnit's IClassFixture.
	/// </summary>
	protected TFixture Fixture { get; }

	/// <summary>
	/// Initializes a new instance of the FixtureTestBase class.
	/// </summary>
	/// <param name="fixture">The test fixture</param>
	protected FixtureTestBase(TFixture fixture)
	{
		Fixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
	}

	/// <summary>
	/// Starts a new test scenario.
	/// This is the standard entry point for the fluent DSL without fixture integration.
	/// </summary>
	/// <returns>A new test scenario</returns>
	/// <example>
	/// <code>
	/// Given()
	///     .ApiResource("/api/products")
	///     .Get()
	///     .When()
	///         .Execute()
	///     .Then()
	///         .AssertStatusCode(200);
	/// </code>
	/// </example>
	protected ITestScenario Given()
	{
		return ScenarioDsl.Given();
	}

	/// <summary>
	/// Starts a new test scenario with a custom context.
	/// Use this when you need to provide a specific test context.
	/// </summary>
	/// <param name="context">Custom test context</param>
	/// <returns>A new test scenario with the specified context</returns>
	protected ITestScenario Given(ITestContext context)
	{
		return ScenarioDsl.Given(context);
	}
}
