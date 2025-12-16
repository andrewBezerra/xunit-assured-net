using System;
using System.Security.Cryptography.X509Certificates;
using Flurl.Http;
using XUnitAssured.Http.Configuration;

namespace XUnitAssured.Http.Handlers;

/// <summary>
/// Handler for Certificate-based authentication (mTLS).
/// Attaches client certificate to HTTP requests for mutual TLS authentication.
/// </summary>
public class CertificateAuthHandler : IAuthenticationHandler
{
	private readonly CertificateAuthConfig _config;

	/// <summary>
	/// Creates a new CertificateAuthHandler with the specified configuration.
	/// </summary>
	/// <param name="config">Certificate authentication configuration</param>
	public CertificateAuthHandler(CertificateAuthConfig config)
	{
		_config = config ?? throw new ArgumentNullException(nameof(config));
	}

	/// <inheritdoc />
	public AuthenticationType Type => AuthenticationType.Certificate;

	/// <inheritdoc />
	public void ApplyAuthentication(IFlurlRequest request)
	{
		// Certificate authentication is handled at HttpRequestStep level
		// via FlurlClientFactory to create properly configured FlurlClient
		// This method is intentionally empty as the certificate must be
		// applied when creating the HttpClient, not per-request
	}

	/// <inheritdoc />
	public bool CanHandle(HttpAuthConfig authConfig)
	{
		return authConfig?.Type == AuthenticationType.Certificate && authConfig.Certificate != null;
	}

	/// <summary>
	/// Loads certificate based on configuration.
	/// Public to allow HttpRequestStep to access it.
	/// </summary>
	public X509Certificate2? GetCertificate()
	{
		// Priority 1: Direct certificate instance
		if (_config.Certificate != null)
			return _config.Certificate;

		// Priority 2: Load from file
		if (!string.IsNullOrWhiteSpace(_config.CertificatePath))
			return LoadCertificateFromFile();

		// Priority 3: Load from certificate store
		if (!string.IsNullOrWhiteSpace(_config.Thumbprint))
			return LoadCertificateFromStore();

		throw new InvalidOperationException(
			"Certificate configuration must specify either Certificate instance, CertificatePath, or Thumbprint.");
	}

	/// <summary>
	/// Loads certificate from file.
	/// </summary>
	private X509Certificate2 LoadCertificateFromFile()
	{
		try
		{
			if (string.IsNullOrWhiteSpace(_config.CertificatePath))
				throw new InvalidOperationException("Certificate path is required.");

			if (!System.IO.File.Exists(_config.CertificatePath))
				throw new InvalidOperationException($"Certificate file not found: {_config.CertificatePath}");

			// Load certificate with or without password
			if (!string.IsNullOrWhiteSpace(_config.CertificatePassword))
			{
				return new X509Certificate2(_config.CertificatePath, _config.CertificatePassword);
			}

			return new X509Certificate2(_config.CertificatePath);
		}
		catch (Exception ex)
		{
			throw new InvalidOperationException($"Failed to load certificate from file: {ex.Message}", ex);
		}
	}

	/// <summary>
	/// Loads certificate from Windows certificate store.
	/// </summary>
	private X509Certificate2? LoadCertificateFromStore()
	{
		try
		{
			if (string.IsNullOrWhiteSpace(_config.Thumbprint))
				throw new InvalidOperationException("Certificate thumbprint is required.");

			using var store = new X509Store(_config.StoreName, _config.StoreLocation);
			store.Open(OpenFlags.ReadOnly);

			var certificates = store.Certificates
				.Find(X509FindType.FindByThumbprint, _config.Thumbprint, validOnly: false);

			if (certificates.Count == 0)
				throw new InvalidOperationException(
					$"Certificate with thumbprint '{_config.Thumbprint}' not found in {_config.StoreLocation}/{_config.StoreName}");

			return certificates[0];
		}
		catch (Exception ex)
		{
			throw new InvalidOperationException($"Failed to load certificate from store: {ex.Message}", ex);
		}
	}
}
