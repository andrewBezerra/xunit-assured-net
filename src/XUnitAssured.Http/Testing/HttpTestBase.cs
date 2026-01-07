using System;
using XUnitAssured.Core.Abstractions;
using XUnitAssured.Core.DSL;
using XUnitAssured.Core.Testing;

namespace XUnitAssured.Http.Testing;

/// <summary>
/// Specialized base class for HTTP integration tests using XUnitAssured.Http.
/// Provides a clean Given() method that automatically uses the injected HTTP fixture.
/// This class extends FixtureTestBase and adds HTTP-specific functionality.
/// </summary>
/// <typeparam name="TFixture">The fixture type that implements IHttpClientProvider</typeparam>
/// <example>
/// <code>
/// public class MyApiTests : HttpTestBase&lt;MyTestFixture&gt;, IClassFixture&lt;MyTestFixture&gt;
/// {
///     public MyApiTests(MyTestFixture fixture) : base(fixture) { }
///     
///     [Fact]
///     public void TestEndpoint()
///     {
///         // Given() automatically uses the fixture's HttpClient
///         Given()
///             .ApiResource("/api/endpoint")
///             .Get()
///             .When()
///                 .Execute()
///             .Then()
///                 .AssertStatusCode(200);
///     }
/// }
/// </code>
/// </example>
public abstract class HttpTestBase<TFixture> : FixtureTestBase<TFixture> 
	where TFixture : IHttpClientProvider
{
	/// <summary>
	/// Initializes a new instance of the HttpTestBase class.
	/// </summary>
	/// <param name="fixture">The test fixture that implements IHttpClientProvider</param>
	protected HttpTestBase(TFixture fixture) : base(fixture)
	{
	}

	/// <summary>
	/// Starts a new test scenario with the fixture's HttpClient automatically configured.
	/// This is a convenience method that wraps ScenarioDsl.Given(fixture).
	/// </summary>
	/// <returns>A new test scenario pre-configured with the fixture's HttpClient</returns>
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
	protected new ITestScenario Given()
	{
		return ScenarioDsl.Given(Fixture);
	}

	/// <summary>
	/// Starts a new test scenario with a custom context.
	/// Use this when you need to provide a specific test context.
	/// </summary>
	/// <param name="context">Custom test context</param>
	/// <returns>A new test scenario with the specified context</returns>
	protected new ITestScenario Given(ITestContext context)
	{
		return ScenarioDsl.Given(context);
	}
}

