using XUnitAssured.Kafka.Testing;

namespace XUnitAssured.Kafka.Samples.Local.Test;

/// <summary>
/// Base class for Kafka local sample tests.
/// Extends KafkaTestBase which provides Given() with shared producer and helpers.
/// </summary>
public abstract class KafkaSamplesLocalTestBase : KafkaTestBase<KafkaSamplesLocalFixture>
{
	protected KafkaSamplesLocalTestBase(KafkaSamplesLocalFixture fixture) : base(fixture)
	{
	}
}
