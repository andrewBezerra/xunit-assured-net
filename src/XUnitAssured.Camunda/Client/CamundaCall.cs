using System;

namespace XUnitAssured.Camunda.Client;

/// <summary>What an HTTP request of the client was for.</summary>
public enum CamundaCallKind
{
	/// <summary>A call to the Camunda REST API.</summary>
	Api,

	/// <summary>The request of an OAuth token to the identity provider.</summary>
	Token
}

/// <summary>
/// One HTTP request the client made for an operation, told to the caller of the operation. It never carries the
/// authorization header, the token or the secret.
/// </summary>
/// <param name="Kind">A call to the API, or the request of a token.</param>
/// <param name="Method">The HTTP method.</param>
/// <param name="Path">
/// For the API, the path from the REST address on, such as <c>/v2/topology</c>; for a token, the path of the token
/// endpoint. Never the query.
/// </param>
/// <param name="Endpoint">The full address, without the query.</param>
/// <param name="Status">The HTTP status; <see langword="null"/> when there was no answer.</param>
/// <param name="Duration">How long it took.</param>
/// <param name="WithRenewedToken">Whether it is the call made again with a token renewed after a 401 or 403.</param>
public sealed record CamundaCall(
	CamundaCallKind Kind, string Method, string Path, string Endpoint, int? Status, TimeSpan Duration, bool WithRenewedToken);
