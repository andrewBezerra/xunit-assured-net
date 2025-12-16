using System;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using Flurl.Http;

namespace XUnitAssured.Http.Client;

/// <summary>
/// Factory for creating and caching FlurlClient instances with client certificates.
/// Provides thread-safe singleton access to configured HTTP clients for mTLS authentication.
/// </summary>
public static class FlurlClientFactory
{
	private static readonly ConcurrentDictionary<string, IFlurlClient> _clientCache = new();
	private static readonly object _lockObject = new();

	/// <summary>
	/// Gets or creates a FlurlClient configured with the specified certificate.
	/// Clients are cached by certificate thumbprint for reuse and performance.
	/// </summary>
	/// <param name="certificate">X509 certificate for mTLS authentication</param>
	/// <returns>FlurlClient configured with the certificate</returns>
	public static IFlurlClient GetOrCreateClient(X509Certificate2 certificate)
	{
		if (certificate == null)
			throw new ArgumentNullException(nameof(certificate));

		var key = certificate.Thumbprint;

		return _clientCache.GetOrAdd(key, _ => CreateClient(certificate));
	}

	/// <summary>
	/// Creates a new FlurlClient configured with the specified certificate.
	/// </summary>
	/// <param name="certificate">X509 certificate for mTLS authentication</param>
	/// <returns>New FlurlClient instance</returns>
	public static IFlurlClient CreateClient(X509Certificate2 certificate)
	{
		if (certificate == null)
			throw new ArgumentNullException(nameof(certificate));

		// Create HttpClientHandler with certificate
		var handler = new HttpClientHandler();
		handler.ClientCertificates.Add(certificate);

		// Create HttpClient with handler
		var httpClient = new HttpClient(handler);

		// Create and return FlurlClient
		return new FlurlClient(httpClient);
	}

	/// <summary>
	/// Clears the client cache and disposes all cached clients.
	/// Useful for testing or when certificate rotation is needed.
	/// </summary>
	public static void ClearCache()
	{
		lock (_lockObject)
		{
			foreach (var client in _clientCache.Values)
			{
				try
				{
					client.Dispose();
				}
				catch
				{
					// Ignore disposal errors
				}
			}

			_clientCache.Clear();
		}
	}

	/// <summary>
	/// Gets the number of cached clients.
	/// </summary>
	public static int CachedClientCount => _clientCache.Count;
}
