namespace XUnitAssured.Kafka.Configuration;

/// <summary>
/// Main authentication configuration for Kafka connections.
/// Supports multiple authentication types with specific configurations.
/// </summary>
public class KafkaAuthConfig
{
	/// <summary>
	/// Type of authentication to use.
	/// Default: None (plaintext, no authentication)
	/// </summary>
	public KafkaAuthenticationType Type { get; set; } = KafkaAuthenticationType.None;

	/// <summary>
	/// SASL/PLAIN configuration.
	/// Used when Type is SaslPlain or SaslSsl.
	/// </summary>
	public SaslPlainConfig? SaslPlain { get; set; }

	/// <summary>
	/// SASL/SCRAM configuration.
	/// Used when Type is SaslScram256 or SaslScram512.
	/// </summary>
	public SaslScramConfig? SaslScram { get; set; }

	/// <summary>
	/// SSL/TLS configuration.
	/// Used when Type is Ssl or MutualTls.
	/// </summary>
	public SslConfig? Ssl { get; set; }

	/// <summary>
	/// Configures SASL/PLAIN authentication.
	/// </summary>
	/// <param name="username">SASL username</param>
	/// <param name="password">SASL password</param>
	/// <param name="useSsl">Use SSL/TLS (default: true)</param>
	public void UseSaslPlain(string username, string password, bool useSsl = true)
	{
		Type = useSsl ? KafkaAuthenticationType.SaslSsl : KafkaAuthenticationType.SaslPlain;
		SaslPlain = new SaslPlainConfig
		{
			Username = username,
			Password = password,
			UseSsl = useSsl
		};
	}

	/// <summary>
	/// Configures SASL/SCRAM-SHA-256 authentication.
	/// </summary>
	/// <param name="username">SASL username</param>
	/// <param name="password">SASL password</param>
	/// <param name="useSsl">Use SSL/TLS (default: true)</param>
	public void UseSaslScram256(string username, string password, bool useSsl = true)
	{
		Type = KafkaAuthenticationType.SaslScram256;
		SaslScram = new SaslScramConfig
		{
			Username = username,
			Password = password,
			Mechanism = Confluent.Kafka.SaslMechanism.ScramSha256,
			UseSsl = useSsl
		};
	}

	/// <summary>
	/// Configures SASL/SCRAM-SHA-512 authentication.
	/// </summary>
	/// <param name="username">SASL username</param>
	/// <param name="password">SASL password</param>
	/// <param name="useSsl">Use SSL/TLS (default: true)</param>
	public void UseSaslScram512(string username, string password, bool useSsl = true)
	{
		Type = KafkaAuthenticationType.SaslScram512;
		SaslScram = new SaslScramConfig
		{
			Username = username,
			Password = password,
			Mechanism = Confluent.Kafka.SaslMechanism.ScramSha512,
			UseSsl = useSsl
		};
	}

	/// <summary>
	/// Configures SSL/TLS authentication.
	/// </summary>
	/// <param name="caLocation">Path to CA certificate file</param>
	/// <param name="enableCertificateVerification">Enable certificate verification (default: true)</param>
	public void UseSsl(string? caLocation = null, bool enableCertificateVerification = true)
	{
		Type = KafkaAuthenticationType.Ssl;
		Ssl = new SslConfig
		{
			SslCaLocation = caLocation,
			EnableSslCertificateVerification = enableCertificateVerification
		};
	}

	/// <summary>
	/// Configures mutual TLS (mTLS) authentication.
	/// </summary>
	/// <param name="certificateLocation">Path to client certificate file</param>
	/// <param name="keyLocation">Path to client private key file</param>
	/// <param name="caLocation">Path to CA certificate file (optional)</param>
	/// <param name="keyPassword">Password for private key (optional)</param>
	public void UseMutualTls(string certificateLocation, string keyLocation, string? caLocation = null, string? keyPassword = null)
	{
		Type = KafkaAuthenticationType.MutualTls;
		Ssl = new SslConfig
		{
			SslCertificateLocation = certificateLocation,
			SslKeyLocation = keyLocation,
			SslCaLocation = caLocation,
			SslKeyPassword = keyPassword,
			EnableSslCertificateVerification = true
		};
	}

	/// <summary>
	/// Disables authentication (plaintext).
	/// </summary>
	public void UseNoAuth()
	{
		Type = KafkaAuthenticationType.None;
		SaslPlain = null;
		SaslScram = null;
		Ssl = null;
	}
}
