using System.Net.Http;

namespace XUnitAssured.Core.Abstractions;

/// <summary>
/// Interface for test fixtures that provide HttpClient instances.
/// Implementing this interface allows fixtures to be used directly with Given() overload.
/// </summary>
/// <example>
/// <code>
/// public class MyTestFixture : IHttpClientProvider
/// {
///     private readonly WebApplicationFactory&lt;Program&gt; _factory;
///     
///     public HttpClient CreateClient() => _factory.CreateClient();
/// }
/// 
/// // In tests:
/// Given(_fixture)
///     .ApiResource("/api/endpoint")
///     .Get()
/// </code>
/// </example>
public interface IHttpClientProvider : ITestContextSeeder
{
	/// <summary>
	/// Creates an HttpClient instance for testing.
	/// </summary>
	/// <returns>A configured HttpClient instance</returns>
	HttpClient CreateClient();

	/// <summary>
	/// Registers this provider as the scenario's HttpClient source. Provided as a
	/// default so existing implementations need no change; a fixture that also
	/// provides other things overrides it and calls the base behaviour itself.
	/// </summary>
	/// <remarks>
	/// Deriving from <see cref="ITestContextSeeder"/> is what lets a single fixture
	/// implementing both be passed to <c>Given(fixture)</c> without an ambiguous
	/// overload: the more specific parameter type wins.
	/// </remarks>
	void ITestContextSeeder.Seed(ITestContext context)
	{
		if (context == null)
			throw new System.ArgumentNullException(nameof(context));

		context.SetProperty("HttpClientProvider", this);
	}
}
