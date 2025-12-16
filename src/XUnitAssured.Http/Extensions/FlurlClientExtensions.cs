using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Flurl.Http;

namespace XUnitAssured.Http.Extensions;

/// <summary>
/// Extension methods for FlurlClient providing retry logic and convenience methods.
/// Migrated from XUnitAssured.Remote to provide retry capabilities in HTTP testing.
/// </summary>
public static class FlurlClientExtensions
{
	private static readonly JsonSerializerOptions DefaultJsonOptions = new()
	{
		PropertyNameCaseInsensitive = true,
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase
	};

	/// <summary>
	/// Performs an HTTP GET request with retry logic.
	/// </summary>
	/// <param name="client">FlurlClient instance</param>
	/// <param name="path">Request path (relative to base URL)</param>
	/// <param name="maxRetries">Maximum number of retry attempts (default: 3)</param>
	/// <param name="retryDelay">Delay between retries in milliseconds (default: 1000)</param>
	/// <param name="cancellationToken">Cancellation token</param>
	/// <returns>HTTP response message</returns>
	public static async Task<HttpResponseMessage> GetWithRetryAsync(
		this FlurlClient client,
		string path,
		int maxRetries = 3,
		int retryDelay = 1000,
		CancellationToken cancellationToken = default)
	{
		return await ExecuteWithRetryAsync(
			async () =>
			{
				var flurlResponse = await client.Request(path).GetAsync(cancellationToken: cancellationToken);
				return flurlResponse.ResponseMessage;
			},
			maxRetries,
			retryDelay,
			cancellationToken);
	}

	/// <summary>
	/// Performs an HTTP POST request with retry logic.
	/// </summary>
	/// <typeparam name="T">Type of the request body</typeparam>
	/// <param name="client">FlurlClient instance</param>
	/// <param name="path">Request path (relative to base URL)</param>
	/// <param name="body">Request body object</param>
	/// <param name="maxRetries">Maximum number of retry attempts (default: 3)</param>
	/// <param name="retryDelay">Delay between retries in milliseconds (default: 1000)</param>
	/// <param name="cancellationToken">Cancellation token</param>
	/// <returns>HTTP response message</returns>
	public static async Task<HttpResponseMessage> PostWithRetryAsync<T>(
		this FlurlClient client,
		string path,
		T body,
		int maxRetries = 3,
		int retryDelay = 1000,
		CancellationToken cancellationToken = default)
	{
		return await ExecuteWithRetryAsync(
			async () =>
			{
				var flurlResponse = await client.Request(path).PostJsonAsync(body, cancellationToken: cancellationToken);
				return flurlResponse.ResponseMessage;
			},
			maxRetries,
			retryDelay,
			cancellationToken);
	}

	/// <summary>
	/// Performs an HTTP PUT request with retry logic.
	/// </summary>
	/// <typeparam name="T">Type of the request body</typeparam>
	/// <param name="client">FlurlClient instance</param>
	/// <param name="path">Request path (relative to base URL)</param>
	/// <param name="body">Request body object</param>
	/// <param name="maxRetries">Maximum number of retry attempts (default: 3)</param>
	/// <param name="retryDelay">Delay between retries in milliseconds (default: 1000)</param>
	/// <param name="cancellationToken">Cancellation token</param>
	/// <returns>HTTP response message</returns>
	public static async Task<HttpResponseMessage> PutWithRetryAsync<T>(
		this FlurlClient client,
		string path,
		T body,
		int maxRetries = 3,
		int retryDelay = 1000,
		CancellationToken cancellationToken = default)
	{
		return await ExecuteWithRetryAsync(
			async () =>
			{
				var flurlResponse = await client.Request(path).PutJsonAsync(body, cancellationToken: cancellationToken);
				return flurlResponse.ResponseMessage;
			},
			maxRetries,
			retryDelay,
			cancellationToken);
	}

	/// <summary>
	/// Performs an HTTP DELETE request with retry logic.
	/// </summary>
	/// <param name="client">FlurlClient instance</param>
	/// <param name="path">Request path (relative to base URL)</param>
	/// <param name="maxRetries">Maximum number of retry attempts (default: 3)</param>
	/// <param name="retryDelay">Delay between retries in milliseconds (default: 1000)</param>
	/// <param name="cancellationToken">Cancellation token</param>
	/// <returns>HTTP response message</returns>
	public static async Task<HttpResponseMessage> DeleteWithRetryAsync(
		this FlurlClient client,
		string path,
		int maxRetries = 3,
		int retryDelay = 1000,
		CancellationToken cancellationToken = default)
	{
		return await ExecuteWithRetryAsync(
			async () =>
			{
				var flurlResponse = await client.Request(path).DeleteAsync(cancellationToken: cancellationToken);
				return flurlResponse.ResponseMessage;
			},
			maxRetries,
			retryDelay,
			cancellationToken);
	}

	/// <summary>
	/// Performs an HTTP PATCH request with retry logic.
	/// </summary>
	/// <typeparam name="T">Type of the request body</typeparam>
	/// <param name="client">FlurlClient instance</param>
	/// <param name="path">Request path (relative to base URL)</param>
	/// <param name="body">Request body object</param>
	/// <param name="maxRetries">Maximum number of retry attempts (default: 3)</param>
	/// <param name="retryDelay">Delay between retries in milliseconds (default: 1000)</param>
	/// <param name="cancellationToken">Cancellation token</param>
	/// <returns>HTTP response message</returns>
	public static async Task<HttpResponseMessage> PatchWithRetryAsync<T>(
		this FlurlClient client,
		string path,
		T body,
		int maxRetries = 3,
		int retryDelay = 1000,
		CancellationToken cancellationToken = default)
	{
		return await ExecuteWithRetryAsync(
			async () =>
			{
				var flurlResponse = await client.Request(path).PatchJsonAsync(body, cancellationToken: cancellationToken);
				return flurlResponse.ResponseMessage;
			},
			maxRetries,
			retryDelay,
			cancellationToken);
	}

	/// <summary>
	/// Deserializes response content to specified type.
	/// </summary>
	/// <typeparam name="T">Target type</typeparam>
	/// <param name="response">HTTP response message</param>
	/// <param name="options">JSON serializer options (optional)</param>
	/// <param name="cancellationToken">Cancellation token</param>
	/// <returns>Deserialized object</returns>
	public static async Task<T?> ReadAsJsonAsync<T>(
		this HttpResponseMessage response,
		JsonSerializerOptions? options = null,
		CancellationToken cancellationToken = default)
	{
		var content = await response.Content.ReadAsStringAsync(cancellationToken);
		return JsonSerializer.Deserialize<T>(content, options ?? DefaultJsonOptions);
	}

	/// <summary>
	/// Adds Bearer token authentication to the request.
	/// </summary>
	/// <param name="request">Flurl request</param>
	/// <param name="token">Bearer token</param>
	/// <returns>Request with authentication header</returns>
	public static IFlurlRequest WithBearerToken(this IFlurlRequest request, string token)
	{
		return request.WithHeader("Authorization", $"Bearer {token}");
	}

	/// <summary>
	/// Adds Basic authentication to the request.
	/// </summary>
	/// <param name="request">Flurl request</param>
	/// <param name="username">Username</param>
	/// <param name="password">Password</param>
	/// <returns>Request with authentication header</returns>
	public static IFlurlRequest WithBasicAuth(this IFlurlRequest request, string username, string password)
	{
		var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
		return request.WithHeader("Authorization", $"Basic {credentials}");
	}

	/// <summary>
	/// Adds API Key authentication to the request.
	/// </summary>
	/// <param name="request">Flurl request</param>
	/// <param name="apiKey">API key value</param>
	/// <param name="headerName">API key header name (default: X-API-Key)</param>
	/// <returns>Request with API key header</returns>
	public static IFlurlRequest WithApiKey(this IFlurlRequest request, string apiKey, string headerName = "X-API-Key")
	{
		return request.WithHeader(headerName, apiKey);
	}

	/// <summary>
	/// Executes an HTTP request with retry logic.
	/// </summary>
	private static async Task<HttpResponseMessage> ExecuteWithRetryAsync(
		Func<Task<HttpResponseMessage>> action,
		int maxRetries,
		int retryDelay,
		CancellationToken cancellationToken)
	{
		int attempts = 0;
		Exception? lastException = null;

		while (attempts < maxRetries)
		{
			try
			{
				cancellationToken.ThrowIfCancellationRequested();
				var response = await action();
				
				// If request was successful, return immediately
				if (response.IsSuccessStatusCode)
				{
					return response;
				}

				// If status code is 4xx (client error), don't retry
				if ((int)response.StatusCode >= 400 && (int)response.StatusCode < 500)
				{
					return response;
				}

				// For 5xx errors, retry
				lastException = new HttpRequestException($"Request failed with status code {response.StatusCode}");
			}
			catch (FlurlHttpException ex)
			{
				lastException = ex;
				
				// Don't retry on client errors (4xx)
				if (ex.StatusCode.HasValue && (int)ex.StatusCode.Value >= 400 && (int)ex.StatusCode.Value < 500)
				{
					throw;
				}
			}
			catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
			{
				lastException = ex;
			}

			attempts++;

			if (attempts < maxRetries)
			{
				await Task.Delay(retryDelay, cancellationToken);
			}
		}

		throw new HttpRequestException(
			$"Request failed after {maxRetries} attempts. Last error: {lastException?.Message}",
			lastException);
	}
}
