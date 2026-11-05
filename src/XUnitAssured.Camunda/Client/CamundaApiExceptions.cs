using System;

namespace XUnitAssured.Camunda.Client;

/// <summary>Which side of a call did not answer.</summary>
public enum CamundaEndpoint
{
	/// <summary>The Camunda REST API.</summary>
	Camunda,

	/// <summary>The identity provider, asked for the OAuth token.</summary>
	IdentityProvider
}

/// <summary>Who refused the credentials.</summary>
public enum CamundaCredentialRefusal
{
	/// <summary>The identity provider refused the client credentials.</summary>
	IdentityProvider,

	/// <summary>The identity provider answered without an access token.</summary>
	MissingAccessToken,

	/// <summary>
	/// The Camunda answered 401 or 403: to the basic credentials, or to an OAuth token already renewed once.
	/// </summary>
	Camunda
}

/// <summary>The Camunda REST API refused a call, with a status outside 2xx.</summary>
public sealed class CamundaApiException : Exception
{
	/// <summary>Creates the refusal.</summary>
	/// <param name="status">The HTTP status.</param>
	/// <param name="title">The <c>title</c> of the problem document, such as <c>INVALID_STATE</c>.</param>
	/// <param name="detail">The <c>detail</c> of the problem document.</param>
	/// <param name="path">The path called, such as <c>/v2/process-instances</c>, without the query.</param>
	/// <param name="inner">The SDK exception.</param>
	public CamundaApiException(int status, string? title, string? detail, string path, Exception? inner = null)
		: base($"The Camunda refused {path} ({status})" + (detail is null ? "." : $": {detail}"), inner)
	{
		Status = status;
		Title = title;
		Detail = detail;
		Path = path;
	}

	/// <summary>The HTTP status.</summary>
	public int Status { get; }

	/// <summary>The <c>title</c> of the problem document; <see langword="null"/> when the answer was not one.</summary>
	public string? Title { get; }

	/// <summary>The <c>detail</c> of the problem document; <see langword="null"/> when the answer was not one.</summary>
	public string? Detail { get; }

	/// <summary>The path called, without the query.</summary>
	public string Path { get; }
}

/// <summary>The credentials were refused, by the identity provider or by the Camunda.</summary>
/// <remarks>Neither the message nor the properties carry the secret or the token.</remarks>
public sealed class CamundaCredentialException : Exception
{
	/// <summary>Creates the refusal.</summary>
	/// <param name="refusal">Who refused.</param>
	/// <param name="status">The HTTP status of the refusal.</param>
	/// <param name="description">What the identity provider said (<c>error_description</c>, or <c>error</c>), without the secret.</param>
	/// <param name="endpoint">The address that refused, without the query.</param>
	public CamundaCredentialException(CamundaCredentialRefusal refusal, int? status, string? description, string endpoint)
		: base(Mensagem(refusal, status, description, endpoint))
	{
		Refusal = refusal;
		Status = status;
		Description = description;
		Endpoint = endpoint;
	}

	/// <summary>Who refused.</summary>
	public CamundaCredentialRefusal Refusal { get; }

	/// <summary>The HTTP status of the refusal.</summary>
	public int? Status { get; }

	/// <summary>What the identity provider said, without the secret; <see langword="null"/> when it said nothing.</summary>
	public string? Description { get; }

	/// <summary>The address that refused, without the query.</summary>
	public string Endpoint { get; }

	private static string Mensagem(CamundaCredentialRefusal recusa, int? status, string? descricao, string endereco) => recusa switch
	{
		CamundaCredentialRefusal.IdentityProvider =>
			$"The identity provider at {endereco} refused the client credentials ({status})" + (descricao is null ? "." : $": {descricao}"),
		CamundaCredentialRefusal.MissingAccessToken => $"The identity provider at {endereco} answered without the access_token.",
		_ => $"The Camunda at {endereco} refused the credentials ({status})."
	};
}

/// <summary>A call got no answer: its deadline passed, or the connection failed.</summary>
public sealed class CamundaUnavailableException : Exception
{
	/// <summary>Creates the failure.</summary>
	/// <param name="target">Who did not answer.</param>
	/// <param name="endpoint">The address called, without the query.</param>
	/// <param name="timeout">The deadline that passed; <see langword="null"/> when the connection failed.</param>
	/// <param name="reason">Why: the deadline, or the message of the connection failure.</param>
	/// <param name="inner">The exception of the connection.</param>
	public CamundaUnavailableException(CamundaEndpoint target, string endpoint, TimeSpan? timeout, string reason, Exception? inner = null)
		: base($"No answer from the {(target == CamundaEndpoint.Camunda ? "Camunda" : "identity provider")} at {endpoint}: {reason}", inner)
	{
		Target = target;
		Endpoint = endpoint;
		Timeout = timeout;
		Reason = reason;
	}

	/// <summary>Who did not answer.</summary>
	public CamundaEndpoint Target { get; }

	/// <summary>The address called, without the query.</summary>
	public string Endpoint { get; }

	/// <summary>The deadline that passed; <see langword="null"/> when the connection failed.</summary>
	public TimeSpan? Timeout { get; }

	/// <summary>Why: the deadline, or the message of the connection failure.</summary>
	public string Reason { get; }
}
