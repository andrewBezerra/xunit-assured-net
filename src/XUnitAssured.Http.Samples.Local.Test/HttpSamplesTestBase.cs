using XUnitAssured.Http.Testing;

namespace XUnitAssured.Http.Samples.Test;

/// <summary>
/// Base class for all sample tests using HttpSamplesFixture.
/// Reduces boilerplate by pre-configuring the fixture type.
/// Derived classes only need to implement IClassFixture marker interface.
/// </summary>
/// <example>
/// <code>
/// public class MyTests : HttpSamplesTestBase
/// {
///     public MyTests(HttpSamplesFixture fixture) : base(fixture) { }
///     
///     [Fact]
///     public void Test() => Given()...
/// }
/// </code>
/// </example>
public abstract class HttpSamplesTestBase : HttpTestBase<HttpSamplesFixture>
{
	/// <summary>
	/// Initializes a new instance of the HttpSamplesTestBase class.
	/// </summary>
	/// <param name="fixture">The HTTP samples fixture</param>
	protected HttpSamplesTestBase(HttpSamplesFixture fixture) : base(fixture)
	{
	}
}
