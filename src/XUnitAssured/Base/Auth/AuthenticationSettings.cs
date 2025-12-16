using System;

namespace XUnitAssured.Base.Auth;

/// <summary>
/// Authentication settings for API testing.
/// Supports multiple authentication types (Basic, Bearer, NTLM).
/// </summary>
public class AuthenticationSettings
{
	/// <summary>
	/// Base URL of the authentication server. Must be an absolute HTTP(S) URI.
	/// Required when AuthenticationType is not None.
	/// </summary>
	public Uri? BaseUrl { get; set; }

	/// <summary>
	/// Client ID for authentication.
	/// Required when AuthenticationType is not None.
	/// </summary>
	public string? ClientId { get; set; }

	/// <summary>
	/// Client secret for authentication.
	/// Required when AuthenticationType is not None.
	/// </summary>
	public string? ClientSecret { get; set; }

	/// <summary>
	/// Type of authentication to use.
	/// Default is None (no authentication).
	/// </summary>
	public AuthenticationType AuthenticationType { get; set; } = AuthenticationType.None;

	/// <summary>
	/// Default constructor for Options pattern binding.
	/// </summary>
	public AuthenticationSettings()
	{
		// Properties will be set by configuration binding
	}
}
