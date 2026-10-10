using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using XUnitAssured.Core.Results;

namespace XUnitAssured.Http.Results;

/// <summary>
/// Specialized result for HTTP/REST API test steps.
/// Extends TestStepResult with HTTP-specific properties and helper methods.
/// </summary>
public class HttpStepResult : TestStepResult
{
	/// <summary>
	/// HTTP status code from the response.
	/// </summary>
	public int StatusCode => GetProperty<int>("StatusCode");

	/// <summary>
	/// HTTP status code as HttpStatusCode enum.
	/// </summary>
	public HttpStatusCode StatusCodeEnum => (HttpStatusCode)StatusCode;

	/// <summary>
	/// The response body/content.
	/// Alias for Data property with more explicit naming for HTTP context.
	/// </summary>
	public object? ResponseBody => Data;

	/// <summary>
	/// HTTP response headers.
	/// </summary>
	public IReadOnlyDictionary<string, IEnumerable<string>>? Headers =>
		GetProperty<IReadOnlyDictionary<string, IEnumerable<string>>>("Headers");

	/// <summary>
	/// Content-Type header value.
	/// </summary>
	public string? ContentType => GetProperty<string>("ContentType");

	/// <summary>
	/// HTTP reason phrase (e.g., "OK", "Not Found").
	/// </summary>
	public string? ReasonPhrase => GetProperty<string>("ReasonPhrase");

	/// <summary>
	/// The request as it went out — method, address, headers, cookies and body — read after the
	/// client sent it, so it includes what the client added, such as the cookies it keeps. Null
	/// when no request left, e.g. the address could not be resolved.
	/// </summary>
	public SentRequest? Request => GetProperty<SentRequest>("Request");

	/// <summary>
	/// The cookie this response set, read from its <c>Set-Cookie</c> headers — to replay a session
	/// from another client, or to read an attribute. When the response sets the same name more
	/// than once, the last one is returned, because that is the one the client keeps.
	/// </summary>
	/// <param name="name">The cookie name, matched exactly</param>
	/// <returns>The cookie, or null when the response did not set it</returns>
	/// <example>
	/// <code>
	/// var signIn = (await Given().ApiResource("/auth/login").Post(credentials).ExecuteAsync()).GetResult();
	/// var session = signIn.SetCookie("sid")!.Value;
	/// </code>
	/// </example>
	public SetCookie? SetCookie(string name) =>
		SetCookies.LastOrDefault(c => string.Equals(c.Name, name, StringComparison.Ordinal));

	/// <summary>
	/// Every cookie this response set, in the order of its <c>Set-Cookie</c> headers.
	/// </summary>
	public IReadOnlyList<SetCookie> SetCookies =>
		(Headers ?? new Dictionary<string, IEnumerable<string>>())
			.Where(h => string.Equals(h.Key, "Set-Cookie", StringComparison.OrdinalIgnoreCase))
			.SelectMany(h => h.Value)
			.Select(Results.SetCookie.Parse)
			.OfType<SetCookie>()
			.ToList();

	/// <summary>
	/// Gets the response body converted to the specified type.
	/// </summary>
	/// <typeparam name="T">Target type for deserialization</typeparam>
	/// <returns>Response body as type T</returns>
	public T? GetResponseBody<T>() => GetData<T>();

	/// <summary>
	/// Checks if the status code is in the success range (200-299).
	/// </summary>
	public bool IsSuccessStatusCode => StatusCode >= 200 && StatusCode <= 299;

	/// <summary>
	/// Checks if the response is a redirect (300-399).
	/// </summary>
	public bool IsRedirect => StatusCode >= 300 && StatusCode <= 399;

	/// <summary>
	/// Checks if the response is a client error (400-499).
	/// </summary>
	public bool IsClientError => StatusCode >= 400 && StatusCode <= 499;

	/// <summary>
	/// Checks if the response is a server error (500-599).
	/// </summary>
	public bool IsServerError => StatusCode >= 500 && StatusCode <= 599;

	/// <summary>
	/// Creates a successful HTTP result.
	/// </summary>
	public static HttpStepResult CreateHttpSuccess(
		int statusCode,
		object? responseBody = null,
		Dictionary<string, IEnumerable<string>>? headers = null,
		string? contentType = null,
		string? reasonPhrase = null)
	{
		var properties = new Dictionary<string, object?>
		{
			["StatusCode"] = statusCode,
			["ContentType"] = contentType,
			["ReasonPhrase"] = reasonPhrase
		};

		if (headers != null)
			properties["Headers"] = headers;

		return new HttpStepResult
		{
			Metadata = new StepMetadata
			{
				StartedAt = DateTimeOffset.UtcNow,
				CompletedAt = DateTimeOffset.UtcNow,
				Status = StepStatus.Succeeded
			},
			Success = statusCode >= 200 && statusCode <= 299,
			Data = responseBody,
			DataType = responseBody?.GetType(),
			Properties = properties
		};
	}

	/// <summary>
	/// How many characters of the body a failure message shows. Enough for a validation error
	/// or a Problem Details, short enough not to bury the line that failed.
	/// </summary>
	internal const int TamanhoDoResumo = 1000;

	/// <summary>
	/// What a failure message needs to say about this response: the body, cut to
	/// <see cref="TamanhoDoResumo"/> characters, or — when no response came — why not.
	/// </summary>
	/// <remarks>
	/// A status alone says that something is wrong; the reason is in the body, or, for a request
	/// that got no answer, in the errors.
	/// </remarks>
	internal string Explicacao()
	{
		if (StatusCode == 0)
			return $"No response: {string.Join("; ", Errors.DefaultIfEmpty("no reason given"))}";

		var corpo = ResponseBody?.ToString();
		if (string.IsNullOrEmpty(corpo))
			return "Body: (empty)";

		return corpo.Length <= TamanhoDoResumo
			? $"Body: {corpo}"
			: $"Body: {corpo.Substring(0, TamanhoDoResumo)}… ({corpo.Length - TamanhoDoResumo} more characters)";
	}

	/// <summary>
	/// The same result, carrying the request that produced it.
	/// </summary>
	internal HttpStepResult ComPedido(SentRequest? pedido)
	{
		if (pedido == null)
			return this;

		var propriedades = new Dictionary<string, object?>(Properties) { ["Request"] = pedido };
		return new HttpStepResult
		{
			Metadata = Metadata,
			Success = Success,
			Data = Data,
			DataType = DataType,
			Errors = Errors,
			Properties = propriedades
		};
	}

	/// <summary>
	/// Creates a failed HTTP result from an exception.
	/// </summary>
	public static new HttpStepResult CreateFailure(Exception exception)
	{
		if (exception == null)
			throw new ArgumentNullException(nameof(exception));

		return new HttpStepResult
		{
			Metadata = new StepMetadata
			{
				StartedAt = DateTimeOffset.UtcNow,
				CompletedAt = DateTimeOffset.UtcNow,
				Status = StepStatus.Failed
			},
			Success = false,
			Errors = new List<string> { exception.Message },
			Properties = new Dictionary<string, object?>
			{
				["StatusCode"] = 0, // Indicates no response received
				// Without these, a transport failure reports only its message and the
				// original call site is lost — the hardest kind of test failure to diagnose.
				["ExceptionType"] = exception.GetType().FullName,
				["ExceptionMessage"] = exception.Message,
				["ExceptionStackTrace"] = exception.StackTrace,
				["ExceptionDetail"] = exception.ToString()
			}
		};
	}
}
