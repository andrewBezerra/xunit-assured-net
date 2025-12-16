using Confluent.Kafka;

namespace XUnitAssured.Kafka.Configuration;

/// <summary>
/// Configuration for SASL/SCRAM authentication.
/// Used for SCRAM-SHA-256 and SCRAM-SHA-512 authentication with Kafka brokers.
/// Common in AWS MSK and self-hosted Kafka clusters.
/// </summary>
public class SaslScramConfig
{
	/// <summary>
	/// SASL username.
	/// Required for SASL/SCRAM authentication.
	/// </summary>
	public string Username { get; set; } = string.Empty;

	/// <summary>
	/// SASL password.
	/// Required for SASL/SCRAM authentication.
	/// </summary>
	public string Password { get; set; } = string.Empty;

	/// <summary>
	/// SCRAM mechanism to use.
	/// Default: ScramSha256
	/// </summary>
	public SaslMechanism Mechanism { get; set; } = SaslMechanism.ScramSha256;

	/// <summary>
	/// Use SSL/TLS for the connection.
	/// Default: true (recommended for production)
	/// </summary>
	public bool UseSsl { get; set; } = true;
}
