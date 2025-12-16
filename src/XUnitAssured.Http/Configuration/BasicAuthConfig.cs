namespace XUnitAssured.Http.Configuration;

/// <summary>
/// Configuration for Basic authentication.
/// </summary>
public class BasicAuthConfig
{
	/// <summary>
	/// Username for basic authentication.
	/// </summary>
	public string Username { get; set; } = string.Empty;

	/// <summary>
	/// Password for basic authentication.
	/// </summary>
	public string Password { get; set; } = string.Empty;
}
