using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

using Flurl.Http;

using XUnitAssured.Core.Abstractions;
using XUnitAssured.Core.Results;
using XUnitAssured.Http.Client;
using XUnitAssured.Http.Configuration;
using XUnitAssured.Http.Handlers;
using XUnitAssured.Http.Results;

namespace XUnitAssured.Http.Steps;

/// <summary>
/// Represents an HTTP request step in a test scenario.
/// Executes HTTP requests using Flurl and returns HttpStepResult.
/// </summary>
public class HttpRequestStep : ITestStep
{
	/// <inheritdoc />
	public string? Name { get; internal set; }

	/// <inheritdoc />
	public string StepType => "Http";

	/// <inheritdoc />
	public ITestStepResult? Result { get; private set; }

	/// <inheritdoc />
	public bool IsExecuted => Result != null;

	/// <inheritdoc />
	public bool IsValid { get; private set; }

	/// <summary>
	/// The URL to send the request to.
	/// </summary>
	public string Url { get; init; } = string.Empty;

	/// <summary>
	/// HTTP method (GET, POST, PUT, DELETE, etc.).
	/// </summary>
	public HttpMethod Method { get; init; } = HttpMethod.Get;

	/// <summary>
	/// Request body for POST, PUT, PATCH methods.
	/// </summary>
	public object? Body { get; init; }

	/// <summary>
	/// HTTP headers to include in the request.
	/// </summary>
	public Dictionary<string, string> Headers { get; init; } = new();

	/// <summary>
	/// Query string parameters.
	/// </summary>
	public Dictionary<string, object?> QueryParams { get; init; } = new();

	/// <summary>
	/// Request timeout in seconds.
	/// Default is 30 seconds.
	/// </summary>
	public int TimeoutSeconds { get; init; } = 30;

	/// <summary>
	/// Authentication configuration for this request.
	/// If null, will try to load from httpsettings.json.
	/// </summary>
	public HttpAuthConfig? AuthConfig { get; init; }

	/// <summary>
	/// Custom HttpClient to use for this request.
	/// If provided, this will be used instead of creating a new Flurl client.
	/// Useful for integration tests with WebApplicationFactory.
	/// </summary>
	public HttpClient? CustomHttpClient { get; init; }

	/// <inheritdoc />
	public async Task<ITestStepResult> ExecuteAsync(ITestContext context)
	{
		var startTime = DateTimeOffset.UtcNow;

		try
		{
			// Check if certificate authentication is configured
			var certificate = GetCertificateIfConfigured();
			
			IFlurlRequest request;
			
