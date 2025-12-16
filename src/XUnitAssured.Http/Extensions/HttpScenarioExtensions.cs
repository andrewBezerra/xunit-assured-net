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

		var step = new HttpRequestStep
		{
			Url = url,
			Method = HttpMethod.Get
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
			TimeoutSeconds = currentHttpStep.TimeoutSeconds
		};

		scenario.SetCurrentStep(newStep);
	}
}
