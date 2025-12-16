namespace XUnitAssured.Kafka.Configuration;

/// <summary>
/// Configuration for SASL/PLAIN authentication.
/// Used for username/password authentication with Kafka brokers.
/// </summary>
public class SaslPlainConfig
{
	/// <summary>
	/// SASL username.
	/// Required for SASL/PLAIN authentication.
	/// </summary>
	public string Username { get; set; } = string.Empty;

	/// <summary>
	/// SASL password.
	/// Required for SASL/PLAIN authentication.
	/// </summary>
	public string Password { get; set; } = string.Empty;

	/// <summary>
	/// Use SSL/TLS for the connection.
	/// Default: true (recommended for production)
	/// </summary>
	public bool UseSsl { get; set; } = true;
}
