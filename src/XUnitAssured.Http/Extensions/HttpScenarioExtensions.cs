using XUnitAssured.Http.Abstractions;
using XUnitAssured.Http.DSL;
using System;
using System.Collections.Generic;
using System.Net.Http;

using XUnitAssured.Core.Abstractions;
using XUnitAssured.Http.Configuration;
using XUnitAssured.Http.Steps;

namespace XUnitAssured.Http.Extensions;

/// <summary>
/// Extension methods for HTTP/REST testing in the fluent DSL.
/// </summary>
public static class HttpScenarioExtensions
{
	/// <summary>
	/// Starts an HTTP request step with the specified URL.
	/// Automatically detects and applies authentication from IHttpClientAuthProvider if available.
	/// Usage: Given().ApiResource("http://api.com/endpoint")
	/// </summary>
	/// <remarks>
	/// For a URL that depends on a value an earlier step produces, use the overload taking a
	/// <see cref="Func{TResult}"/>: a string is built as the chain is written, and at that point
	/// no step has run yet.
	/// </remarks>
	public static IHttpScenario ApiResource(this ITestScenario scenario, string url)
	{
		if (scenario == null)
			throw new ArgumentNullException(nameof(scenario));

		return StartRequest(scenario, url, urlProvider: null);
	}

	/// <summary>
	/// Starts an HTTP request step whose URL is built when the step runs.
	/// </summary>
	/// <remarks>
	/// For a URL built from something an earlier step of the same chain produced — the usual
	/// shape of a behavior test: create, then read back what was created. Written as a string,
	/// it would be built while the chain is being described, before that step has run.
	/// <code>
	/// string? orderId = null;
	///
	/// await Given(api)
	///     .ApiResource("/api/orders").Post(newOrder)
	///     .Validate((HttpStepResult r) =&gt; orderId = r.JsonPath&lt;string&gt;("$.id"))
	///     .And()
	///     .ApiResource(() =&gt; $"/api/orders/{orderId}").Get()
	///     .Validate((HttpStepResult r) =&gt; r.StatusCode.ShouldBe(200))
	///     .ExecuteAsync();
	/// </code>
	/// </remarks>
	/// <param name="scenario">The test scenario to add the request to</param>
	/// <param name="url">Builds the URL at execution time</param>
	/// <returns>The scenario, typed as an HTTP scenario</returns>
	public static IHttpScenario ApiResource(this ITestScenario scenario, Func<string> url)
	{
		if (scenario == null)
			throw new ArgumentNullException(nameof(scenario));
		if (url == null)
			throw new ArgumentNullException(nameof(url));

		return StartRequest(scenario, string.Empty, url);
	}

	private static IHttpScenario StartRequest(ITestScenario scenario, string url, Func<string>? urlProvider)
	{
		// Check if there's already an HttpRequestStep (e.g., from WithHttpClient)
		// and preserve its properties
		HttpClient? existingHttpClient = null;
		HttpAuthConfig? existingAuthConfig = null;
		if (scenario.CurrentStep is HttpRequestStep existingStep)
		{
			existingHttpClient = existingStep.CustomHttpClient;
			existingAuthConfig = existingStep.AuthConfig;
		}
		
		// Check if HttpClientProvider was set via Given(IHttpClientProvider)
		if (existingHttpClient == null)
		{
			var provider = scenario.Context.GetProperty<Core.Abstractions.IHttpClientProvider>("HttpClientProvider");
			if (provider != null)
			{
				existingHttpClient = provider.CreateClient();
				
				// Check if provider also implements IHttpClientAuthProvider
				if (provider is IHttpClientAuthProvider authProvider && existingAuthConfig == null)
				{
					existingAuthConfig = authProvider.GetAuthenticationConfig();
				}
			}
		}

		var step = new HttpRequestStep
		{
			Url = url,
			UrlProvider = urlProvider,
			Method = HttpMethod.Get,
			CustomHttpClient = existingHttpClient,  // Preserve CustomHttpClient if set
			AuthConfig = existingAuthConfig  // Preserve or set AuthConfig from provider
		};

		scenario.SetCurrentStep(step);
		return HttpScenario.De(scenario);
	}

	/// <summary>
	/// Sets the HTTP method to GET.
	/// </summary>
	public static IHttpScenario Get(this ITestScenario scenario)
	{
		UpdateHttpMethod(scenario, HttpMethod.Get);
		return HttpScenario.De(scenario);
	}

	/// <summary>
	/// Sets the HTTP method to POST with the specified body.
	/// </summary>
	public static IHttpScenario Post(this ITestScenario scenario, object body)
	{
		UpdateHttpMethod(scenario, HttpMethod.Post, body);
		return HttpScenario.De(scenario);
	}

	/// <summary>
	/// Sets the HTTP method to POST without a body (for endpoints that don't require one).
	/// </summary>
	public static IHttpScenario Post(this ITestScenario scenario)
	{
		UpdateHttpMethod(scenario, HttpMethod.Post);
		return HttpScenario.De(scenario);
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
	public static IHttpScenario PostFormData(this ITestScenario scenario, Dictionary<string, string> formData)
	{
		if (formData == null)
			throw new ArgumentNullException(nameof(formData));

		if (scenario.CurrentStep is not HttpRequestStep currentHttpStep)
			throw new InvalidOperationException("Current step is not an HTTP step. Call ApiResource() first.");

		// Create FormUrlEncodedContent from the dictionary
		var formContent = new FormUrlEncodedContent(formData);

		// Copy the step, overriding only method and body
		var newStep = new HttpRequestStep(currentHttpStep)
		{
			Method = HttpMethod.Post,
			Body = formContent  // Store the HttpContent directly
		};

		scenario.SetCurrentStep(newStep);
		return HttpScenario.De(scenario);
	}

	/// <summary>
	/// Sets the HTTP method to PUT with the specified body.
	/// </summary>
	public static IHttpScenario Put(this ITestScenario scenario, object body)
	{
		UpdateHttpMethod(scenario, HttpMethod.Put, body);
		return HttpScenario.De(scenario);
	}

	/// <summary>
	/// Sets the HTTP method to DELETE.
	/// </summary>
	public static IHttpScenario Delete(this ITestScenario scenario)
	{
		UpdateHttpMethod(scenario, HttpMethod.Delete);
		return HttpScenario.De(scenario);
	}

	/// <summary>
	/// Sets the HTTP method to PATCH with the specified body.
	/// </summary>
	public static IHttpScenario Patch(this ITestScenario scenario, object body)
	{
		UpdateHttpMethod(scenario, HttpMethod.Patch, body);
		return HttpScenario.De(scenario);
	}

	/// <summary>
	/// Adds a custom header to the HTTP request.
	/// </summary>
	public static IHttpScenario WithHeader(this ITestScenario scenario, string name, string value)
	{
		if (scenario.CurrentStep is HttpRequestStep httpStep)
		{
			httpStep.Headers[name] = value;
		}

		return HttpScenario.De(scenario);
	}

	/// <summary>
	/// Adds a query parameter to the HTTP request.
	/// </summary>
	public static IHttpScenario WithQueryParam(this ITestScenario scenario, string name, object? value)
	{
		if (scenario.CurrentStep is HttpRequestStep httpStep)
		{
			httpStep.QueryParams[name] = value;
		}

		return HttpScenario.De(scenario);
	}

	/// <summary>
	/// Sets the timeout for the HTTP request.
	/// </summary>
	public static IHttpScenario WithTimeout(this ITestScenario scenario, int seconds)
	{
		if (scenario.CurrentStep is HttpRequestStep httpStep)
		{
			// Copy the step, overriding only the timeout (immutable pattern)
			var newStep = new HttpRequestStep(httpStep)
			{
				TimeoutSeconds = seconds
			};

			scenario.SetCurrentStep(newStep);
		}

		return HttpScenario.De(scenario);
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
	public static IHttpScenario WithHttpClient(this ITestScenario scenario, HttpClient httpClient)
	{
		if (httpClient == null)
			throw new ArgumentNullException(nameof(httpClient));

		if (scenario.CurrentStep is HttpRequestStep httpStep)
		{
			// There's already an HTTP step, update it with CustomHttpClient
			var newStep = new HttpRequestStep(httpStep)
			{
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

		return HttpScenario.De(scenario);
	}

	// Helper method to update HTTP method
	private static void UpdateHttpMethod(ITestScenario scenario, HttpMethod method, object? body = null)
	{
		if (scenario.CurrentStep is not HttpRequestStep currentHttpStep)
			throw new InvalidOperationException("Current step is not an HTTP step.");

		// Copy the step, overriding only method and body (immutable pattern)
		var newStep = new HttpRequestStep(currentHttpStep)
		{
			Method = method,
			Body = body
		};

		scenario.SetCurrentStep(newStep);
	}
}
