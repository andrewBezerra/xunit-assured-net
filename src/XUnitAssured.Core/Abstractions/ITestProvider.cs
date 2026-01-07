namespace XUnitAssured.Core.Abstractions;

/// <summary>
/// Marker interface for unified test providers that support multiple testing capabilities.
/// Fixtures implementing this interface can be used for integrated testing scenarios.
/// </summary>
/// <remarks>
/// This is a marker interface in Core. Specific packages (HTTP, Kafka) extend it via their own interfaces:
/// - IHttpClientProvider (Core) - Provides HttpClient
/// - IKafkaClientProvider (Kafka package) - Provides Kafka producers/consumers
/// 
/// A unified fixture would implement both IHttpClientProvider and the Kafka-specific interface.
/// The Core package remains decoupled from Kafka by not referencing Confluent.Kafka types directly.
/// </remarks>
/// <example>
/// <code>
/// // In XUnitAssured.Kafka package:
/// public interface IKafkaClientProvider
/// {
///     IProducer&lt;TK, TV&gt; CreateProducer&lt;TK, TV&gt;();
///     IConsumer&lt;TK, TV&gt; CreateConsumer&lt;TK, TV&gt;();
/// }
/// 
/// // Unified fixture implementing both:
/// public class UnifiedTestFixture : IHttpClientProvider, IKafkaClientProvider
/// {
///     public HttpClient CreateClient() => ...;
///     public IProducer&lt;TK, TV&gt; CreateProducer&lt;TK, TV&gt;() => ...;
///     public IConsumer&lt;TK, TV&gt; CreateConsumer&lt;TK, TV&gt;() => ...;
/// }
/// </code>
/// </example>
public interface ITestProvider : IHttpClientProvider
{
	// This interface inherits from IHttpClientProvider (Core)
	// Kafka-specific interfaces are defined in XUnitAssured.Kafka package
	// to maintain zero-coupling between Core and Kafka
}
