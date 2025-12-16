namespace XUnitAssured.Kafka.Configuration;

/// <summary>
/// Types of authentication supported for Kafka connections.
/// </summary>
public enum KafkaAuthenticationType
{
	/// <summary>
	/// No authentication (Plaintext protocol).
	/// Default for local development.
	/// </summary>
	None = 0,

	/// <summary>
	/// SASL/PLAIN authentication over plaintext.
	/// Common for Confluent Cloud, AWS MSK.
	/// </summary>
	SaslPlain = 1,

	/// <summary>
	/// SASL/PLAIN authentication over SSL/TLS.
	/// Most secure option for SASL/PLAIN.
	/// </summary>
	SaslSsl = 2,

	/// <summary>
	/// SASL/SCRAM-SHA-256 authentication.
	/// Commonly used in self-hosted Kafka clusters.
	/// </summary>
	SaslScram256 = 3,

	/// <summary>
	/// SASL/SCRAM-SHA-512 authentication.
	/// More secure variant of SCRAM.
	/// </summary>
	SaslScram512 = 4,

	/// <summary>
	/// SSL/TLS authentication only (no SASL).
	/// Uses SSL certificates for authentication.
	/// </summary>
	Ssl = 5,

	/// <summary>
	/// Mutual TLS (mTLS) authentication.
	/// Client and server authenticate each other using certificates.
	/// </summary>
	MutualTls = 6
}
