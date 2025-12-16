using System;

namespace XUnitAssured.Base.Kafka;

/// <summary>
/// [OBSOLETE] Kafka security settings.
/// This class will be moved to a separate XUnitAssured.Kafka package in v3.0.
/// Consider using Confluent.Kafka's ClientConfig directly for new implementations.
/// </summary>
[Obsolete("KafkaSecurity will be moved to XUnitAssured.Kafka package in v3.0. Use Confluent.Kafka's ClientConfig for new implementations.", error: false)]
public class KafkaSecurity
{
	/// <summary>
	/// Security protocol for Kafka connection.
	/// Default is "Plaintext".
	/// </summary>
	public string SecurityProtocol { get; set; } = "Plaintext";

	/// <summary>
	/// SASL mechanism for authentication.
	/// Default is "Plain".
	/// </summary>
	public string SaslMechanisms { get; set; } = "Plain";

	/// <summary>
	/// Username for SASL authentication.
	/// </summary>
	public string? Username { get; set; }

	/// <summary>
	/// Password for SASL authentication.
	/// </summary>
	public string? Password { get; set; }

	/// <summary>
	/// Default constructor.
	/// </summary>
	public KafkaSecurity()
	{
	}
}
