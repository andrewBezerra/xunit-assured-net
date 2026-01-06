using System;
using System.Collections.Generic;
using System.Net.Http;

using XUnitAssured.Core.Abstractions;
using XUnitAssured.Http.Steps;

namespace XUnitAssured.Http.Extensions;

/// <summary>
/// Extension methods for HTTP/REST testing in the fluent DSL.
/// </summary>
public static class HttpScenarioExtensions
{
	/// <summary>
	/// Starts an HTTP request step with the specified URL.
	/// Usage: Given().ApiResource("http://api.com/endpoint")
	/// </summary>
	public static ITestScenario ApiResource(this ITestScenario scenario, string url)
	{
		if (scenario == null)
			throw new ArgumentNullException(nameof(scenario));

		// Check if there's already an HttpRequestStep (e.g., from WithHttpClient)
		// and preserve its properties
		HttpClient? existingHttpClient = null;
		if (scenario.CurrentStep is HttpRequestStep existingStep)
		{
			existingHttpClient = existingStep.CustomHttpClient;
		}

		var step = new HttpRequestStep
		{
			Url = url,
			Method = HttpMethod.Get,
			CustomHttpClient = existingHttpClient  // Preserve CustomHttpClient if set
		};

		scenario.SetCurrentStep(step);
		return scenario;
	}

	/// <summary>
	/// Sets the HTTP method to GET.
	/// </summary>
	public static ITestScenario Get(this ITestScenario scenario)
	{
		UpdateHttpMethod(scenario, HttpMethod.Get);
		return scenario;
	}

	/// <summary>
	/// Sets the HTTP method to POST with the specified body.
	/// </summary>
	public static ITestScenario Post(this ITestScenario scenario, object body)
	{
		UpdateHttpMethod(scenario, HttpMethod.Post, body);
		return scenario;
	}

	/// <summary>
	/// Sets the HTTP method to POST without a body (for endpoints that don't require one).
	/// </summary>
	public static ITestScenario Post(this ITestScenario scenario)
	{
		UpdateHttpMethod(scenario, HttpMethod.Post);
		return scenario;
	}

	/// <summary>
	/// Sets the HTTP method to POST with form-urlencoded data.
	/// Automatically sets Content-Type to application/x-www-form-urlencoded.
	/// Usage: .PostFormData(new Dictionary&lt;string, string&gt; { ["key"] = "value" })
	/// </summary>
	/// <param name="scenario">The test scenario</param>
	/// <param name="formData">Dictionary containing form field names and values</param>
	/// <returns>The test scenario for method chaining</returns>
	/// <example>
	/// <code>
	/// Given()
	///     .ApiResource("/api/oauth2/token")
	///     .PostFormData(new Dictionary&lt;string, string&gt;
	///     {
	///         ["grant_type"] = "client_credentials",
	///         ["client_id"] = "my-client-id",
	///         ["client_secret"] = "my-secret"
	///     })
	/// </code>
	/// </example>
	public static ITestScenario PostFormData(this ITestScenario scenario, Dictionary<string, string> formData)
	{
		if (formData == null)
			throw new ArgumentNullException(nameof(formData));

		if (scenario.CurrentStep is not HttpRequestStep currentHttpStep)
			throw new InvalidOperationException("Current step is not an HTTP step. Call ApiResource() first.");

		// Create FormUrlEncodedContent from the dictionary
		var formContent = new FormUrlEncodedContent(formData);

		// Create new step with updated method and body
		var newStep = new HttpRequestStep
		{
			Url = currentHttpStep.Url,
			Method = HttpMethod.Post,
			Body = formContent,  // Store the HttpContent directly
			Headers = currentHttpStep.Headers,
			QueryParams = currentHttpStep.QueryParams,
			TimeoutSeconds = currentHttpStep.TimeoutSeconds,
			CustomHttpClient = currentHttpStep.CustomHttpClient,
			AuthConfig = currentHttpStep.AuthConfig
		};

		scenario.SetCurrentStep(newStep);
		return scenario;
	}

	/// <summary>
	/// Sets the HTTP method to PUT with the specified body.
	/// </summary>
	public static ITestScenario Put(this ITestScenario scenario, object body)
	{
		UpdateHttpMethod(scenario, HttpMethod.Put, body);
		return scenario;
	}

	/// <summary>
	/// Sets the HTTP method to DELETE.
	/// </summary>
	public static ITestScenario Delete(this ITestScenario scenario)
	{
		UpdateHttpMethod(scenario, HttpMethod.Delete);
		return scenario;
	}

	/// <summary>
	/// Sets the HTTP method to PATCH with the specified body.
	/// </summary>
	public static ITestScenario Patch(this ITestScenario scenario, object body)
	{
		UpdateHttpMethod(scenario, HttpMethod.Patch, body);
		return scenario;
	}

	/// <summary>
	/// Adds a custom header to the HTTP request.
	/// </summary>
	public static ITestScenario WithHeader(this ITestScenario scenario, string name, string value)
	{
		if (scenario.CurrentStep is HttpRequestStep httpStep)
		{
			httpStep.Headers[name] = value;
		}

		return scenario;
	}

	/// <summary>
	/// Adds a query parameter to the HTTP request.
	/// </summary>
	public static ITestScenario WithQueryParam(this ITestScenario scenario, string name, object? value)
	{
		if (scenario.CurrentStep is HttpRequestStep httpStep)
		{
			httpStep.QueryParams[name] = value;
		}

		return scenario;
	}

	/// <summary>
	/// Sets the timeout for the HTTP request.
	/// </summary>
	public static ITestScenario WithTimeout(this ITestScenario scenario, int seconds)
	{
		if (scenario.CurrentStep is HttpRequestStep httpStep)
		{
			// Create new step with updated timeout (immutable pattern)
			var newStep = new HttpRequestStep
			{
				Url = httpStep.Url,
				Method = httpStep.Method,
				Body = httpStep.Body,
				Headers = httpStep.Headers,
				QueryParams = httpStep.QueryParams,
				TimeoutSeconds = seconds
			};

			scenario.SetCurrentStep(newStep);
		}

		return scenario;
	}

	/// <summary>
	/// Sets a custom HttpClient to use for the HTTP request.
	/// This is particularly useful for integration tests with WebApplicationFactory,
	/// where you want to use the test server's HttpClient.
	/// </summary>
	/// <param name="scenario">The test scenario</param>
	/// <param name="httpClient">The custom HttpClient to use (e.g., from WebApplicationFactory.CreateClient())</param>
	/// <returns>The test scenario for method chaining</returns>
	/// <example>
	/// <code>
	/// // Option 1: Set HttpClient before ApiResource (recommended for integration tests)
	/// Given()
	///     .WithHttpClient(_fixture.CreateClient())
	///     .ApiResource("/api/products/1")
	///     .Get()
	///     
	/// // Option 2: Set HttpClient after ApiResource
	/// Given()
	///     .ApiResource("/api/products/1")
	///     .WithHttpClient(_fixture.CreateClient())
	///     .Get()
	/// </code>
	/// </example>
	public static ITestScenario WithHttpClient(this ITestScenario scenario, HttpClient httpClient)
	{
		if (httpClient == null)
			throw new ArgumentNullException(nameof(httpClient));

		if (scenario.CurrentStep is HttpRequestStep httpStep)
		{
			// There's already an HTTP step, update it with CustomHttpClient
			var newStep = new HttpRequestStep
			{
				Url = httpStep.Url,
				Method = httpStep.Method,
				Body = httpStep.Body,
				Headers = httpStep.Headers,
				QueryParams = httpStep.QueryParams,
				TimeoutSeconds = httpStep.TimeoutSeconds,
				CustomHttpClient = httpClient
			};

			scenario.SetCurrentStep(newStep);
		}
		else
		{
			// No step yet, create a placeholder step with just the HttpClient
			// ApiResource will be called next and will preserve this
			var step = new HttpRequestStep
			{
				Url = string.Empty,  // Will be set by ApiResource
				Method = HttpMethod.Get,
				CustomHttpClient = httpClient
			};

			scenario.SetCurrentStep(step);
		}

		return scenario;
	}

	// Helper method to update HTTP method
	private static void UpdateHttpMethod(ITestScenario scenario, HttpMethod method, object? body = null)
	{
		if (scenario.CurrentStep is not HttpRequestStep currentHttpStep)
			throw new InvalidOperationException("Current step is not an HTTP step.");

		// Create new step with updated method/body (immutable pattern)
		var newStep = new HttpRequestStep
		{
			Url = currentHttpStep.Url,
			Method = method,
			Body = body,
			Headers = currentHttpStep.Headers,
			QueryParams = currentHttpStep.QueryParams,
			TimeoutSeconds = currentHttpStep.TimeoutSeconds,
			CustomHttpClient = currentHttpStep.CustomHttpClient,  // Preserve custom HttpClient
			AuthConfig = currentHttpStep.AuthConfig  // Preserve authentication configuration
		};

		scenario.SetCurrentStep(newStep);
	}
}
