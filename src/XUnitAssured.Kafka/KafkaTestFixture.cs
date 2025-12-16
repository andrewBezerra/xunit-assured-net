using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Confluent.Kafka;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Xunit.Microsoft.DependencyInjection;
using Xunit.Microsoft.DependencyInjection.Abstracts;

namespace XUnitAssured.Kafka;

/// <summary>
/// Base test fixture for Kafka integration tests.
/// Provides KafkaSettings configuration and helper methods for Producer/Consumer operations.
/// Does not depend on XUnitAssured.Base.TestFixture, making it independent and reusable.
/// </summary>
public abstract class KafkaTestFixture : TestBedFixture
{
	protected KafkaSettings? KafkaSettings { get; private set; }

	protected override void AddServices(IServiceCollection services, IConfiguration? configuration)
	{
		if (configuration == null)
		{
			throw new InvalidOperationException("Configuration is required for KafkaTestFixture.");
		}

		// Configure KafkaSettings with validation
		services.AddOptions<KafkaSettings>()
			.Bind(configuration.GetSection(KafkaSettings.SectionName))
			.ValidateOnStart();

		// Register validator
		services.AddSingleton<IValidateOptions<KafkaSettings>, KafkaSettingsValidator>();

		// Build a temporary provider to get KafkaSettings
		using var tempProvider = services.BuildServiceProvider();
		KafkaSettings = tempProvider.GetRequiredService<IOptions<KafkaSettings>>().Value;

		// Allow derived classes to add more services
		AddKafkaServices(services, configuration);
	}

	/// <summary>
	/// Override this method to add additional services specific to your Kafka tests.
	/// </summary>
	/// <param name="services">Service collection</param>
	/// <param name="configuration">Configuration</param>
	protected virtual void AddKafkaServices(IServiceCollection services, IConfiguration configuration)
	{
		// Default: no additional services
	}

	/// <summary>
	/// Creates a Kafka producer with default settings.
	/// </summary>
	/// <typeparam name="TKey">Type of the message key</typeparam>
	/// <typeparam name="TValue">Type of the message value</typeparam>
	/// <param name="clientId">Optional client ID</param>
	/// <returns>Configured Kafka producer</returns>
	protected IProducer<TKey, TValue> CreateProducer<TKey, TValue>(string? clientId = null)
	{
		if (KafkaSettings == null)
		{
			throw new InvalidOperationException("KafkaSettings not initialized. Ensure AddServices was called.");
		}

		var config = KafkaSettings.ToProducerConfig(clientId);
		return new ProducerBuilder<TKey, TValue>(config).Build();
	}

	/// <summary>
	/// Creates a Kafka consumer with default settings.
	/// </summary>
	/// <typeparam name="TKey">Type of the message key</typeparam>
	/// <typeparam name="TValue">Type of the message value</typeparam>
	/// <param name="groupId">Optional consumer group ID</param>
	/// <returns>Configured Kafka consumer</returns>
	protected IConsumer<TKey, TValue> CreateConsumer<TKey, TValue>(string? groupId = null)
	{
		if (KafkaSettings == null)
		{
			throw new InvalidOperationException("KafkaSettings not initialized. Ensure AddServices was called.");
		}

		var config = KafkaSettings.ToConsumerConfig(groupId);
		return new ConsumerBuilder<TKey, TValue>(config).Build();
	}

	/// <summary>
	/// Creates a Kafka admin client for administrative operations.
	/// </summary>
	/// <returns>Configured Kafka admin client</returns>
	protected IAdminClient CreateAdminClient()
	{
		if (KafkaSettings == null)
		{
			throw new InvalidOperationException("KafkaSettings not initialized. Ensure AddServices was called.");
		}

		var config = KafkaSettings.ToClientConfig();
		return new AdminClientBuilder(config).Build();
	}

	/// <summary>
	/// Produces a single message to a Kafka topic.
	/// </summary>
	/// <typeparam name="TKey">Type of the message key</typeparam>
	/// <typeparam name="TValue">Type of the message value</typeparam>
	/// <param name="topic">Topic name</param>
	/// <param name="key">Message key</param>
	/// <param name="value">Message value</param>
	/// <returns>Delivery result</returns>
	protected async Task<DeliveryResult<TKey, TValue>> ProduceAsync<TKey, TValue>(
		string topic,
		TKey key,
		TValue value)
	{
		using var producer = CreateProducer<TKey, TValue>();
		var message = new Message<TKey, TValue>
		{
			Key = key,
			Value = value
		};

		return await producer.ProduceAsync(topic, message);
	}

	/// <summary>
	/// Consumes a single message from a Kafka topic with timeout.
	/// </summary>
	/// <typeparam name="TKey">Type of the message key</typeparam>
	/// <typeparam name="TValue">Type of the message value</typeparam>
	/// <param name="topic">Topic name</param>
	/// <param name="timeoutMs">Timeout in milliseconds (default: 30000)</param>
	/// <returns>Consumed message or null if timeout</returns>
	protected ConsumeResult<TKey, TValue>? ConsumeOne<TKey, TValue>(
		string topic,
		int timeoutMs = 30000)
	{
		using var consumer = CreateConsumer<TKey, TValue>();
		consumer.Subscribe(topic);

		try
		{
			var result = consumer.Consume(TimeSpan.FromMilliseconds(timeoutMs));
			return result;
		}
		catch (ConsumeException)
		{
			return null;
		}
	}

	protected override ValueTask DisposeAsyncCore()
	{
		// Clean up any Kafka resources
		return ValueTask.CompletedTask;
	}

	protected override IEnumerable<TestAppSettings> GetTestAppSettings()
	{
		yield return new() { Filename = "kafkasettings.json", IsOptional = false };
	}
}
