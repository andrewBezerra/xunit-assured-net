using System.Collections.Generic;

namespace XUnitAssured.Http.Configuration;

/// <summary>
/// Configuration for token request to authentication endpoint.
/// </summary>
public class TokenRequestConfig
{
	/// <summary>
	/// HTTP method for token request.
	/// Default: POST
	/// </summary>
	public string Method { get; set; } = "POST";

	/// <summary>
	/// Request body for token request.
	/// Can be Dictionary, anonymous object, or any serializable type.
	/// </summary>
	public object? Body { get; set; }

	/// <summary>
	/// Additional headers for token request.
	/// </summary>
	public Dictionary<string, string>? Headers { get; set; }

	/// <summary>
	/// Content type for request body.
	/// Default: application/x-www-form-urlencoded
	/// Common values: application/json, application/x-www-form-urlencoded
	/// </summary>
	public string ContentType { get; set; } = "application/x-www-form-urlencoded";

	/// <summary>
	/// Query parameters for token request.
	/// </summary>
	public Dictionary<string, string>? QueryParams { get; set; }
}
