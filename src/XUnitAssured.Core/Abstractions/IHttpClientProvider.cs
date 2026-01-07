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
public interface IHttpClientProvider
{
	/// <summary>
	/// Creates an HttpClient instance for testing.
	/// </summary>
	/// <returns>A configured HttpClient instance</returns>
	HttpClient CreateClient();
}
