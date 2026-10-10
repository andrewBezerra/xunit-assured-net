using XUnitAssured.Http.Abstractions;
using XUnitAssured.Http.DSL;
using System;
using System.Collections.Generic;
using System.Linq;
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
	/// Sets the HTTP method to POST with a body sent exactly as written, without serialization —
	/// for what a serializer would never produce: malformed JSON, a wrong type, XML, plain text.
	/// </summary>
	/// <example>
	/// <code>
	/// // An invalid body, on purpose: the API must refuse it without quoting it back.
	/// Given(api)
	///     .ApiResource("/api/patients")
	///     .PostRaw("{\"city\": [\"Lisbon\"]")
	///     .ExecuteAsync();
	/// </code>
	/// </example>
	/// <param name="scenario">The test scenario</param>
	/// <param name="content">The body, as it must go</param>
	/// <param name="contentType">The Content-Type, parameters included if any, e.g. "text/plain; charset=utf-8"</param>
	/// <returns>The test scenario for method chaining</returns>
	public static IHttpScenario PostRaw(this ITestScenario scenario, string content, string contentType = "application/json")
	{
		UpdateHttpMethod(scenario, HttpMethod.Post, CorpoCru(content, contentType));
		return HttpScenario.De(scenario);
	}

	/// <summary>
	/// Sets the HTTP method to PUT with a body sent exactly as written, without serialization.
	/// </summary>
	/// <inheritdoc cref="PostRaw" path="/param"/>
	/// <returns>The test scenario for method chaining</returns>
	public static IHttpScenario PutRaw(this ITestScenario scenario, string content, string contentType = "application/json")
	{
		UpdateHttpMethod(scenario, HttpMethod.Put, CorpoCru(content, contentType));
		return HttpScenario.De(scenario);
	}

	/// <summary>
	/// Sets the HTTP method to PATCH with a body sent exactly as written, without serialization.
	/// </summary>
	/// <inheritdoc cref="PostRaw" path="/param"/>
	/// <returns>The test scenario for method chaining</returns>
	public static IHttpScenario PatchRaw(this ITestScenario scenario, string content, string contentType = "application/json")
	{
		UpdateHttpMethod(scenario, HttpMethod.Patch, CorpoCru(content, contentType));
		return HttpScenario.De(scenario);
	}

	/// <summary>
	/// Sends the request <paramref name="times"/> times at once, each with the same body — for a
	/// test whose subject is the concurrency itself: a double submit, a race on a counter.
	/// </summary>
	/// <remarks>
	/// All copies are started before any is awaited, through the same client. The result is a
	/// <see cref="Results.ConcurrentHttpStepResult"/>: <c>AssertStatusCode</c> then checks every
	/// response, and <c>AssertEach</c> runs any other assertion on each.
	/// </remarks>
	/// <example>
	/// <code>
	/// (await Given(api)
	///     .ApiResource("/api/orders/7/pay")
	///     .Post(payment)
	///     .Concurrently(2)
	///     .ExecuteAsync())
	/// .Then().AssertEach(r =&gt; r.AssertStatusCode(200));
	/// </code>
	/// </example>
	/// <param name="scenario">The test scenario</param>
	/// <param name="times">How many requests to send at once; at least one</param>
	/// <returns>The test scenario for method chaining</returns>
	public static IHttpScenario Concurrently(this ITestScenario scenario, int times)
	{
		if (times < 1)
			throw new ArgumentOutOfRangeException(nameof(times), times, "Concurrently needs at least one request to send.");

		var passo = PassoHttp(scenario);
		scenario.SetCurrentStep(new HttpRequestStep(passo) { ConcurrentCount = times, ConcurrentBodies = null });
		return HttpScenario.De(scenario);
	}

	/// <summary>
	/// Sends one request per body, all at once, each with the method, address and headers already
	/// described — for simultaneous creates that must each persist.
	/// </summary>
	/// <remarks>
	/// The bodies replace the one given to <c>Post</c>, <c>Put</c> or <c>Patch</c>; the method is
	/// theirs. The responses come in the order of the bodies.
	/// </remarks>
	/// <example>
	/// <code>
	/// var names = Enumerable.Range(1, 6).Select(i =&gt; $"Member {i}").ToList();
	///
	/// (await Given(api)
	///     .ApiResource($"/api/teams/{teamId}/members")
	///     .Post()
	///     .Concurrently(names.Select(n =&gt; new { Name = n }))
	///     .ExecuteAsync())
	/// .Then().AssertStatusCode(201);   // every one of the six
	/// </code>
	/// </example>
	/// <param name="scenario">The test scenario</param>
	/// <param name="bodies">One body per request; at least one</param>
	/// <returns>The test scenario for method chaining</returns>
	public static IHttpScenario Concurrently(this ITestScenario scenario, IEnumerable<object?> bodies)
	{
		if (bodies == null)
			throw new ArgumentNullException(nameof(bodies));

		var lista = bodies.ToList();
		if (lista.Count == 0)
			throw new ArgumentException("Concurrently needs at least one body to send.", nameof(bodies));

		var passo = PassoHttp(scenario);
		scenario.SetCurrentStep(new HttpRequestStep(passo) { ConcurrentCount = null, ConcurrentBodies = lista });
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

	private static CorpoCru CorpoCru(string content, string contentType)
	{
		if (content == null)
			throw new ArgumentNullException(nameof(content));
		if (string.IsNullOrWhiteSpace(contentType))
			throw new ArgumentException("A raw body needs a Content-Type.", nameof(contentType));

		return new CorpoCru(content, contentType);
	}

	private static HttpRequestStep PassoHttp(ITestScenario scenario) =>
		scenario.CurrentStep as HttpRequestStep
			?? throw new InvalidOperationException("Current step is not an HTTP step. Call ApiResource() first.");

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
