namespace XUnitAssured.Http.Configuration;

/// <summary>
/// A client provider that says who owns the OAuth 2.0 tokens obtained through it. Providers that
/// name the same owner share a token, as long as the token request is the same.
/// </summary>
/// <remarks>
/// <para>
/// By default a token is kept by the provider given to <c>Given(provider)</c>: the scenarios of one
/// fixture share it, and another fixture asks for its own. A fixture that hands out one provider per
/// API — each with its own base URL — would then ask for a token per API, even when the APIs use the
/// same identity provider and the same client. Naming the fixture as the owner of every provider
/// makes them share it.
/// </para>
/// <para>
/// The token is still looked up by everything that goes in the token request (URL, client,
/// secret, scopes), so APIs with different credentials never get each other's token. It lives as
/// long as the owner: name an owner that lives no longer than the servers its tokens are valid for,
/// such as the fixture that starts an in-memory server.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public sealed class ApisFixture
/// {
///     public Api For(string name) => new(this, settings[name]);
///
///     public sealed class Api(ApisFixture owner, HttpSettings http)
///         : IHttpClientProvider, IHttpClientAuthProvider, IHttpTokenOwnerProvider
///     {
///         public object? TokenOwner => owner;
///         public HttpClient CreateClient() => new() { BaseAddress = new Uri(http.BaseUrl!) };
///         public HttpAuthConfig? GetAuthenticationConfig() => http.Authentication;
///     }
/// }
/// </code>
/// </example>
public interface IHttpTokenOwnerProvider
{
	/// <summary>
	/// The owner of the tokens obtained through this provider, or <c>null</c> to keep them with the
	/// provider itself.
	/// </summary>
	object? TokenOwner { get; }
}
