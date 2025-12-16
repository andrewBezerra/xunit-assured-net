using System;
using System.Collections.Generic;
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
	/// Creates a failed HTTP result from an exception.
	/// </summary>
	public static new HttpStepResult CreateFailure(Exception exception)
	{
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
				["StatusCode"] = 0 // Indicates no response received
			}
		};
	}
}
