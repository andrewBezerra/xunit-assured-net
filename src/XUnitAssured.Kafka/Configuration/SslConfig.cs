namespace XUnitAssured.Kafka.Configuration;

/// <summary>
/// Configuration for SSL/TLS authentication.
/// Used for encrypted connections to Kafka brokers without SASL.
/// Common in enterprise on-premise Kafka clusters.
/// </summary>
public class SslConfig
{
	/// <summary>
	/// Path to CA certificate file for SSL/TLS.
	/// Required for SSL connections.
	/// Typically a .pem or .crt file.
	/// </summary>
	public string? SslCaLocation { get; set; }

	/// <summary>
	/// Path to client certificate file (for mutual TLS).
	/// Optional - only needed if server requires client authentication.
	/// </summary>
	public string? SslCertificateLocation { get; set; }

	/// <summary>
	/// Path to client private key file (for mutual TLS).
	/// Optional - only needed if server requires client authentication.
	/// </summary>
	public string? SslKeyLocation { get; set; }

	/// <summary>
	/// Password for the private key file (if encrypted).
	/// Optional.
	/// </summary>
	public string? SslKeyPassword { get; set; }

	/// <summary>
	/// Enable SSL certificate verification.
	/// Default: true (recommended for production)
	/// Set to false only for development/testing with self-signed certificates.
	/// </summary>
	public bool EnableSslCertificateVerification { get; set; } = true;
}
