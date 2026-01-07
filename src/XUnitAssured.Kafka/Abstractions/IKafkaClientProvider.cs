using Confluent.Kafka;

namespace XUnitAssured.Kafka.Abstractions;

/// <summary>
/// Interface for test fixtures that provide Kafka producer and consumer instances.
/// Implementing this interface allows fixtures to be used with Kafka testing scenarios.
/// </summary>
/// <remarks>
/// This interface is defined in XUnitAssured.Kafka package (not Core) because it depends on
/// Confluent.Kafka types. This maintains zero-coupling between Core and Kafka packages.
/// </remarks>
/// <example>
/// <code>
/// public class MyKafkaFixture : IKafkaClientProvider
/// {
///     private readonly KafkaSettings _settings;
///     
///     public MyKafkaFixture()
///     {
///         _settings = TestSettings.Load().Kafka ?? new KafkaSettings();
///     }
///     
///     public IProducer&lt;TKey, TValue&gt; CreateProducer&lt;TKey, TValue&gt;()
///     {
///         var config = _settings.ToProducerConfig();
///         return new ProducerBuilder&lt;TKey, TValue&gt;(config)
///             .SetKeySerializer(/* custom serializer */)
///             .SetValueSerializer(/* custom serializer */)
///             .Build();
///     }
///     
///     public IConsumer&lt;TKey, TValue&gt; CreateConsumer&lt;TKey, TValue&gt;()
///     {
///         var config = _settings.ToConsumerConfig();
///         return new ConsumerBuilder&lt;TKey, TValue&gt;(config)
///             .SetKeyDeserializer(/* custom deserializer */)
///             .SetValueDeserializer(/* custom deserializer */)
///             .Build();
///     }
/// }
/// 
/// // Use in tests with Given(fixture):
/// Given(myKafkaFixture)
///     .Topic("my-topic")
///     .Produce("my-key", myValue);
/// </code>
/// </example>
public interface IKafkaClientProvider
{
	/// <summary>
	/// Creates a Kafka producer instance with the specified key and value types.
	/// </summary>
	/// <typeparam name="TKey">The type of the message key</typeparam>
	/// <typeparam name="TValue">The type of the message value</typeparam>
	/// <returns>A configured Kafka producer instance</returns>
	IProducer<TKey, TValue> CreateProducer<TKey, TValue>();

	/// <summary>
	/// Creates a Kafka consumer instance with the specified key and value types.
	/// </summary>
	/// <typeparam name="TKey">The type of the message key</typeparam>
	/// <typeparam name="TValue">The type of the message value</typeparam>
	/// <returns>A configured Kafka consumer instance</returns>
	IConsumer<TKey, TValue> CreateConsumer<TKey, TValue>();
}
