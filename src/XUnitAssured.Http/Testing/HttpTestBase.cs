using System;
using System.Net.Http;
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
///     public async Task TestEndpoint()
///     {
///         // Given() automatically uses the fixture's HttpClient
///         (await Given()
///             .ApiResource("/api/endpoint")
///             .Get()
///             .ExecuteAsync())
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
	/// (await Given()
	///     .ApiResource("/api/products")
	///     .Get()
	///     .ExecuteAsync())
	///     .Then()
	///         .AssertStatusCode(200);
	/// </code>
	/// </example>
	protected new ITestScenario Given()
	{
		return ScenarioDsl.Given(Fixture);
	}

	/// <summary>
	/// Starts a new test scenario that sends through the given client instead of the fixture's —
	/// a user with fewer permissions, a client that keeps cookies, a test-only host.
	/// This is a convenience method that wraps ScenarioDsl.Given(httpClient).
	/// </summary>
	/// <param name="httpClient">The client every HTTP step of the scenario sends through.</param>
	/// <returns>A new test scenario that sends through <paramref name="httpClient"/>.</returns>
	/// <remarks>
	/// The fixture's client and authentication are not used: the scenario goes out exactly
	/// as <paramref name="httpClient"/> is configured.
	/// </remarks>
	/// <example>
	/// <code>
	/// (await Given(Fixture.ClientFor(("X-Test-User", "reader")))
	///     .ApiResource("/orders/1")
	///     .Delete()
	///     .ExecuteAsync())
	///     .Then()
	///         .AssertStatusCode(403);
	/// </code>
	/// </example>
	protected ITestScenario Given(HttpClient httpClient)
	{
		return ScenarioDsl.Given(httpClient);
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

