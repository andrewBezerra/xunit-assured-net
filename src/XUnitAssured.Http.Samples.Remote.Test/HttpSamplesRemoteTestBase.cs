using XUnitAssured.Http.Testing;

namespace XUnitAssured.Http.Samples.Remote.Test;

/// <summary>
/// Base class for all remote sample tests using HttpSamplesRemoteFixture.
/// Reduces boilerplate by pre-configuring the fixture type for remote testing.
/// Derived classes only need to implement IClassFixture marker interface.
/// </summary>
/// <remarks>
/// This base class is specifically designed for testing remote/deployed APIs.
/// It uses HttpSamplesRemoteFixture which loads configuration from testsettings.json.
/// 
/// Usage:
/// <code>
/// public class MyRemoteTests : HttpSamplesRemoteTestBase, IClassFixture&lt;HttpSamplesRemoteFixture&gt;
/// {
///     public MyRemoteTests(HttpSamplesRemoteFixture fixture) : base(fixture) { }
///     
///     [Fact]
///     public void TestRemoteApi() 
///     {
///         Given()
///             .ApiResource("/api/products")
///             .Get()
///         .When()
///             .Execute()
///         .Then()
///             .AssertStatusCode(200);
///     }
/// }
/// </code>
/// 
/// Configuration (testsettings.json):
/// <code>
/// {
///   "testMode": "Remote",
///   "http": {
///     "baseUrl": "https://api.staging.com",
///     "timeout": 60
///   }
/// }
/// </code>
/// </remarks>
/// <example>
/// <code>
/// // Simple remote test
/// public class ProductTests : HttpSamplesRemoteTestBase, IClassFixture&lt;HttpSamplesRemoteFixture&gt;
/// {
///     public ProductTests(HttpSamplesRemoteFixture fixture) : base(fixture) { }
///     
///     [Fact]
///     public void GetProduct_ShouldReturnProduct()
///     {
///         Given()
///             .ApiResource("/api/products/1")
///             .Get()
///         .When()
///             .Execute()
///         .Then()
///             .AssertStatusCode(200)
///             .AssertJsonPath&lt;string&gt;("$.name", name => !string.IsNullOrEmpty(name));
///     }
/// }
/// </code>
/// </example>
public abstract class HttpSamplesRemoteTestBase : HttpTestBase<HttpSamplesRemoteFixture>
{
	/// <summary>
	/// Initializes a new instance of the HttpSamplesRemoteTestBase class.
	/// </summary>
	/// <param name="fixture">The HTTP remote samples fixture configured from testsettings.json</param>
	protected HttpSamplesRemoteTestBase(HttpSamplesRemoteFixture fixture) : base(fixture)
	{
	}

	/// <summary>
	/// Gets the base URL of the remote API being tested.
	/// </summary>
	protected string BaseUrl => Fixture.BaseUrl;
}
