namespace XUnitAssured.Http.Configuration;

/// <summary>
/// Interface for providers that supply HTTP authentication configuration.
/// Enables fixtures to expose authentication settings loaded from configuration files
/// (e.g., testsettings.json) to be automatically applied to HTTP requests.
/// </summary>
/// <remarks>
/// Implementers should expose the authentication configuration that should be
/// automatically applied to all HTTP requests when using the fixture with Given(fixture).
/// This allows centralized authentication management without requiring explicit
/// authentication method calls (e.g., WithBasicAuth) on each test.
/// </remarks>
/// <example>
/// <code>
/// // Example implementation in a test fixture
/// public class MyApiFixture : IHttpClientProvider, IHttpClientAuthProvider
/// {
///     public HttpClient CreateClient() => _httpClient;
///     
///     public HttpAuthConfig? GetAuthenticationConfig() 
///     {
///         // Return auth config from testsettings.json
///         return _httpSettings.Authentication;
///     }
/// }
/// 
/// // Usage in tests - authentication applied automatically
/// Given(fixture)
///     .ApiResource("/api/protected")
///     .Get()
/// .When()
///     .Execute()
/// .Then()
///     .AssertStatusCode(200);
/// </code>
/// </example>
public interface IHttpClientAuthProvider
{
	/// <summary>
	/// Gets the authentication configuration to be automatically applied to HTTP requests.
	/// </summary>
	/// <returns>
	/// The authentication configuration, or null if no authentication should be applied.
	/// </returns>
	HttpAuthConfig? GetAuthenticationConfig();
}
