using System;

namespace XUnitAssured.Base.Kafka;

/// <summary>
/// [OBSOLETE] Kafka settings class.
/// This class will be moved to a separate XUnitAssured.Kafka package in v3.0.
/// Consider using Confluent.Kafka directly for new implementations.
/// </summary>
[Obsolete("Kafka settings will be moved to XUnitAssured.Kafka package in v3.0. Use Confluent.Kafka for new implementations.", error: false)]
public class Kafka
{
	public Kafka()
	{
	}

	public Kafka(string bootstrapServers, SchemaRegistrySettings? schemaRegistry, KafkaSecurity? security)
	{
		BootstrapServers = bootstrapServers;
		SchemaRegistry = schemaRegistry;
		if (security != null)
			SecuritySettings = security;
	}

	public string? BootstrapServers { get; set; }
	public SchemaRegistrySettings? SchemaRegistry { get; set; }
	public string? GroupName { get; set; }
	public KafkaSecurity SecuritySettings { get; set; } = new();
}
