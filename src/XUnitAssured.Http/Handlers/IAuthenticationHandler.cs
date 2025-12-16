using Flurl.Http;
using XUnitAssured.Http.Configuration;

namespace XUnitAssured.Http.Handlers;

/// <summary>
/// Interface for authentication handlers.
/// Handlers are responsible for applying authentication to HTTP requests.
/// </summary>
public interface IAuthenticationHandler
{
	/// <summary>
	/// Gets the authentication type this handler supports.
	/// </summary>
	AuthenticationType Type { get; }

	/// <summary>
	/// Applies authentication to the HTTP request.
	/// </summary>
	/// <param name="request">Flurl request to apply authentication to</param>
	void ApplyAuthentication(IFlurlRequest request);

	/// <summary>
	/// Checks if this handler can handle the given authentication configuration.
	/// </summary>
	/// <param name="authConfig">Authentication configuration</param>
	/// <returns>True if this handler can handle the configuration</returns>
	bool CanHandle(HttpAuthConfig authConfig);
}
