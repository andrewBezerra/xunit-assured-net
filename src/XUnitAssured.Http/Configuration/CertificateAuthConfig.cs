using System.Security.Cryptography.X509Certificates;

namespace XUnitAssured.Http.Configuration;

/// <summary>
/// Configuration for Certificate-based authentication (mTLS).
/// </summary>
public class CertificateAuthConfig
{
	/// <summary>
	/// Path to the certificate file (.pfx, .p12, .pem, .crt).
	/// </summary>
	public string? CertificatePath { get; set; }

	/// <summary>
	/// Password for the certificate file (if encrypted).
	/// </summary>
	public string? CertificatePassword { get; set; }

	/// <summary>
	/// Certificate thumbprint (SHA-1 hash) to load from certificate store.
	/// Alternative to using certificate file path.
	/// </summary>
	public string? Thumbprint { get; set; }

	/// <summary>
	/// Certificate store location.
	/// Default: CurrentUser
	/// </summary>
	public StoreLocation StoreLocation { get; set; } = StoreLocation.CurrentUser;

	/// <summary>
	/// Certificate store name.
	/// Default: My (Personal)
	/// </summary>
	public StoreName StoreName { get; set; } = StoreName.My;

	/// <summary>
	/// Direct certificate instance (for advanced scenarios).
	/// Takes precedence over file path and thumbprint.
	/// </summary>
	public X509Certificate2? Certificate { get; set; }
}
