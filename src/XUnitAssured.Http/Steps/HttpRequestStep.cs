using System.Threading;
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

	private HttpAuthConfig? _effectiveAuthConfig;
	private bool _authConfigResolved;

	/// <summary>
	/// The authentication that applies to this request: <see cref="AuthConfig"/> when
	/// set, otherwise whatever the settings file provides. Resolved once per step —
	/// the step is immutable and runs once — rather than on each of the several
	/// places that need it during execution, each of which used to take the
	/// settings loader's lock.
	/// </summary>
	private HttpAuthConfig? EffectiveAuthConfig
	{
		get
		{
			if (!_authConfigResolved)
			{
				_effectiveAuthConfig = AuthConfig ?? HttpSettings.Load().Authentication;
				_authConfigResolved = true;
			}

			return _effectiveAuthConfig;
		}
	}

	/// <summary>
	/// Creates a new, empty HTTP request step.
	/// </summary>
	public HttpRequestStep()
	{
	}

	/// <summary>
	/// Creates a copy of an existing step, carrying over every configured value.
	/// Use an object initializer to override only the values that change:
	/// <c>new HttpRequestStep(previous) { TimeoutSeconds = 60 }</c>.
	/// </summary>
	/// <remarks>
	/// Rebuilding a step field-by-field is how configuration such as
	/// <see cref="AuthConfig"/> or <see cref="CustomHttpClient"/> silently gets dropped.
	/// Always copy through this constructor so new properties are carried over by default.
	/// </remarks>
	/// <param name="source">The step to copy configuration from.</param>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> is null.</exception>
	public HttpRequestStep(HttpRequestStep source)
	{
		if (source == null)
			throw new ArgumentNullException(nameof(source));

		Name = source.Name;
		Url = source.Url;
		Method = source.Method;
		Body = source.Body;
		Headers = source.Headers;
		QueryParams = source.QueryParams;
		TimeoutSeconds = source.TimeoutSeconds;
		AuthConfig = source.AuthConfig;
		CustomHttpClient = source.CustomHttpClient;
	}

	/// <inheritdoc />
	public async Task<ITestStepResult> ExecuteAsync(ITestContext context, CancellationToken cancellationToken = default)
	{
		try
		{
			// If custom HttpClient is provided, use it directly (bypass Flurl for better compatibility)
			if (CustomHttpClient != null)
			{
				return await ExecuteWithCustomHttpClient();
			}

			IFlurlRequest request;

			// Check if certificate authentication is configured
			var certificate = GetCertificateIfConfigured();

			if (certificate != null)
			{
				// Certificate authentication: create request with custom FlurlClient.
				// The certificate itself is attached to the client's handler; the
				// CertificateAuthHandler has no per-request work to do.
				var flurlClient = FlurlClientFactory.GetOrCreateClient(certificate);
				request = flurlClient
					.Request(Url)
					.WithTimeout(TimeSpan.FromSeconds(TimeoutSeconds));
			}
			else
			{
				// Normal request without certificate
				request = Url
					.WithTimeout(TimeSpan.FromSeconds(TimeoutSeconds));

				// Apply other authentication types (Basic, Bearer, ApiKey, etc.)
				ApplyAuthentication(request);
			}

			// Add headers
			foreach (var header in Headers)
			{
				request = request.WithHeader(header.Key, header.Value);
			}

			// Add query parameters
			foreach (var param in QueryParams)
			{
				request = request.SetQueryParam(param.Key, param.Value);
			}

			// Execute request based on method
			IFlurlResponse response;

			if (Method == HttpMethod.Get)
			{
				response = await request.GetAsync();
			}
			else if (Method == HttpMethod.Post)
			{
				// Check if Body is HttpContent (e.g., FormUrlEncodedContent)
				if (Body is HttpContent httpContent)
				{
					response = await request.SendAsync(HttpMethod.Post, httpContent);
				}
				else
				{
					response = await request.PostJsonAsync(Body);
				}
			}
			else if (Method == HttpMethod.Put)
			{
				// Check if Body is HttpContent
				if (Body is HttpContent httpContentPut)
				{
					response = await request.SendAsync(HttpMethod.Put, httpContentPut);
				}
				else
				{
					response = await request.PutJsonAsync(Body);
				}
			}
			else if (Method == HttpMethod.Delete)
			{
				response = await request.DeleteAsync();
			}
			else if (Method == HttpMethod.Patch)
			{
				// Check if Body is HttpContent
				if (Body is HttpContent httpContentPatch)
				{
					response = await request.SendAsync(HttpMethod.Patch, httpContentPatch);
				}
				else
				{
					response = await request.PatchJsonAsync(Body);
				}
			}
			else if (Method == HttpMethod.Head)
			{
				response = await request.HeadAsync();
			}
			else if (Method == HttpMethod.Options)
			{
				response = await request.OptionsAsync();
			}
			else
			{
				throw new NotSupportedException($"HTTP method '{Method}' is not supported.");
			}

			// Parse response
			var statusCode = response.StatusCode;
			var responseBody = await response.GetStringAsync();

			// Group headers by name to handle duplicates
			var headers = response.Headers
				.GroupBy(h => h.Name)
				.ToDictionary(
					g => g.Key,
					g => g.Select(h => h.Value).AsEnumerable()
				);

			var contentType = response.ResponseMessage.Content?.Headers?.ContentType?.ToString();
			var reasonPhrase = response.ResponseMessage.ReasonPhrase;

			// Create result
			Result = HttpStepResult.CreateHttpSuccess(
				statusCode: statusCode,
				responseBody: responseBody,
				headers: headers,
				contentType: contentType,
				reasonPhrase: reasonPhrase
			);

			return Result;
		}
		catch (FlurlHttpException ex)
		{
			// HTTP error (4xx, 5xx)
			var statusCode = ex.StatusCode ?? 0;
			var responseBody = await ex.GetResponseStringAsync();

			// Group headers by name to handle duplicates
			var headers = ex.Call?.Response?.Headers?
				.GroupBy(h => h.Name)
				.ToDictionary(
					g => g.Key,
					g => g.Select(h => h.Value).AsEnumerable()
				);

			// HTTP errors (4xx, 5xx) should still create an HttpStepResult with the status code
			// but Success should be false since these are error responses
			Result = HttpStepResult.CreateHttpSuccess(
				statusCode: statusCode,
				responseBody: responseBody,
				headers: headers,
				contentType: ex.Call?.Response?.ResponseMessage?.Content?.Headers?.ContentType?.ToString(),
				reasonPhrase: ex.Call?.Response?.ResponseMessage?.ReasonPhrase
			);

			return Result;
		}
		catch (Exception ex)
		{
			// Network error, timeout, etc.
			Result = HttpStepResult.CreateFailure(ex);
			return Result;
		}
	}

	/// <inheritdoc />
	public void Validate(Action<ITestStepResult> validation)
	{
		if (Result == null)
			throw new InvalidOperationException("Step must be executed before validation. Call ExecuteAsync first.");

		try
		{
			validation(Result);
			IsValid = true;
		}
		catch
		{
			IsValid = false;
			throw;
		}
	}

	/// <summary>
	/// Gets certificate if certificate authentication is configured.
	/// Returns null if not using certificate authentication.
	/// </summary>
	private X509Certificate2? GetCertificateIfConfigured()
	{
		var authConfig = EffectiveAuthConfig;

		// Check if certificate authentication is configured
		if (authConfig?.Type == AuthenticationType.Certificate && authConfig.Certificate != null)
		{
			var handler = new CertificateAuthHandler(authConfig.Certificate);
			return handler.GetCertificate();
		}

		return null;
	}

	/// <summary>
	/// Executes HTTP request using CustomHttpClient directly (bypasses Flurl).
	/// This ensures proper header propagation for integration tests with WebApplicationFactory.
	/// </summary>
	private async Task<ITestStepResult> ExecuteWithCustomHttpClient()
	{
		if (CustomHttpClient == null)
			throw new InvalidOperationException("CustomHttpClient is null");

		// Combine BaseAddress with relative URL if needed
		var requestUrl = Url;
		if (CustomHttpClient.BaseAddress != null && !Uri.IsWellFormedUriString(Url, UriKind.Absolute))
		{
			requestUrl = new Uri(CustomHttpClient.BaseAddress, Url).ToString();
		}

		// Build query string
		var allQueryParams = new Dictionary<string, object?>(QueryParams);
		
		// Add authentication query parameters if needed
		var authConfig = AuthConfig;
		if (authConfig?.Type == AuthenticationType.ApiKey && 
		    authConfig.ApiKey?.Location == ApiKeyLocation.Query)
		{
			allQueryParams[authConfig.ApiKey.KeyName] = authConfig.ApiKey.KeyValue;
		}
		
		if (allQueryParams.Any())
		{
			var queryString = string.Join("&", allQueryParams.Select(p => $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value?.ToString() ?? "")}"));
			requestUrl = requestUrl.Contains("?") ? $"{requestUrl}&{queryString}" : $"{requestUrl}?{queryString}";
		}

		// Create HttpRequestMessage
		var httpRequest = new HttpRequestMessage(Method, requestUrl);

		// Add authentication headers
		var authHeaders = GetAuthenticationHeaders();
		foreach (var header in authHeaders)
		{
			httpRequest.Headers.TryAddWithoutValidation(header.Key, header.Value);
		}

		// Add regular headers
		foreach (var header in Headers)
		{
			httpRequest.Headers.TryAddWithoutValidation(header.Key, header.Value);
		}

		// Add body if present
		if (Body != null && (Method == HttpMethod.Post || Method == HttpMethod.Put || Method == HttpMethod.Patch))
		{
			// Check if Body is already HttpContent (e.g., FormUrlEncodedContent)
			if (Body is HttpContent httpContent)
			{
				httpRequest.Content = httpContent;
			}
			else
			{
				// Serialize as JSON
				var json = System.Text.Json.JsonSerializer.Serialize(Body);
				httpRequest.Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
			}
		}

		try
		{
			// Execute request
			var httpResponse = await CustomHttpClient.SendAsync(httpRequest);
			var responseBody = await httpResponse.Content.ReadAsStringAsync();

			// Parse headers
			var headers = httpResponse.Headers
				.Concat(httpResponse.Content.Headers)
				.GroupBy(h => h.Key)
				.ToDictionary(
					g => g.Key,
					g => g.SelectMany(h => h.Value).AsEnumerable()
				);

			Result = HttpStepResult.CreateHttpSuccess(
				statusCode: (int)httpResponse.StatusCode,
				responseBody: responseBody,
				headers: headers,
				contentType: httpResponse.Content.Headers.ContentType?.ToString(),
				reasonPhrase: httpResponse.ReasonPhrase
			);

			return Result;
		}
		catch (HttpRequestException ex)
		{
			Result = HttpStepResult.CreateFailure(ex);
			return Result;
		}
	}

	/// <summary>
	/// Gets authentication headers as a dictionary.
	/// Used when CustomHttpClient is present to collect headers before creating FlurlRequest.
	/// </summary>
	private Dictionary<string, string> GetAuthenticationHeaders()
	{
		var headers = new Dictionary<string, string>();
		
		// Get authentication config
		var authConfig = EffectiveAuthConfig;

		// Skip if no authentication configured
		if (authConfig == null || authConfig.Type == AuthenticationType.None)
			return headers;

		// Generate authentication headers based on type
		switch (authConfig.Type)
		{
			case AuthenticationType.Basic when authConfig.Basic != null:
			{
				var credentials = $"{authConfig.Basic.Username}:{authConfig.Basic.Password}";
				var base64Credentials = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(credentials));
				headers["Authorization"] = $"Basic {base64Credentials}";
				break;
			}
			
			case AuthenticationType.Bearer when authConfig.Bearer != null:
			{
				var prefix = authConfig.Bearer.Prefix ?? "Bearer";
				headers["Authorization"] = $"{prefix} {authConfig.Bearer.Token}";
				break;
			}
			
			case AuthenticationType.ApiKey when authConfig.ApiKey != null:
			{
				if (authConfig.ApiKey.Location == ApiKeyLocation.Header)
				{
					headers[authConfig.ApiKey.KeyName] = authConfig.ApiKey.KeyValue;
				}
				break;
			}
			
			case AuthenticationType.CustomHeader when authConfig.CustomHeader != null:
			{
				if (authConfig.CustomHeader.Headers != null)
				{
					foreach (var header in authConfig.CustomHeader.Headers)
					{
						headers[header.Key] = header.Value;
					}
				}
				break;
			}
		}

		return headers;
	}

	/// <summary>
	/// Applies authentication to the request.
	/// Uses AuthConfig if provided, otherwise loads from httpsettings.json.
	/// Note: Certificate authentication is handled separately via FlurlClientFactory.
	/// </summary>
	private void ApplyAuthentication(IFlurlRequest request)
	{
		// Get authentication config
		var authConfig = EffectiveAuthConfig;

		// Skip if no authentication configured
		if (authConfig == null || authConfig.Type == AuthenticationType.None)
			return;

		// Create appropriate handler and apply authentication
		IAuthenticationHandler? handler = authConfig.Type switch
		{
			AuthenticationType.Basic when authConfig.Basic != null => new BasicAuthHandler(authConfig.Basic),
			AuthenticationType.Bearer when authConfig.Bearer != null => new BearerAuthHandler(authConfig.Bearer),
			AuthenticationType.ApiKey when authConfig.ApiKey != null => new ApiKeyAuthHandler(authConfig.ApiKey),
			AuthenticationType.OAuth2 when authConfig.OAuth2 != null => new OAuth2Handler(authConfig.OAuth2),
			AuthenticationType.CustomHeader when authConfig.CustomHeader != null => new CustomHeaderAuthHandler(authConfig.CustomHeader),
			AuthenticationType.Certificate when authConfig.Certificate != null => new CertificateAuthHandler(authConfig.Certificate),
			_ => null
		};

		handler?.ApplyAuthentication(request);
	}
}
