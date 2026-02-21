using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Confluent.Kafka;
using XUnitAssured.Core.Abstractions;
using XUnitAssured.Core.Results;
using XUnitAssured.Kafka.Configuration;
using XUnitAssured.Kafka.Handlers;
using XUnitAssured.Kafka.Results;

namespace XUnitAssured.Kafka.Steps;

/// <summary>
/// Represents a Kafka batch message production step in a test scenario.
/// Produces multiple messages simultaneously using the same producer instance.
/// </summary>
public class KafkaBatchProduceStep : ITestStep
{
	/// <inheritdoc />
	public string? Name { get; internal set; }

	/// <inheritdoc />
	public string StepType => "Kafka";

	/// <inheritdoc />
	public ITestStepResult? Result { get; private set; }

	/// <inheritdoc />
	public bool IsExecuted => Result != null;

	/// <inheritdoc />
	public bool IsValid { get; private set; }

	/// <summary>
	/// The Kafka topic to produce to.
	/// </summary>
	public string Topic { get; init; } = string.Empty;

	/// <summary>
	/// The messages to produce. Each item is a (Key, Value) pair.
	/// Key may be null for messages without keys.
	/// </summary>
	public IReadOnlyList<KeyValuePair<object?, object>> Messages { get; init; } = [];

	/// <summary>
	/// Optional Kafka message headers applied to all messages.
	/// </summary>
	public Headers? Headers { get; init; }

	/// <summary>
	/// Timeout for producing the entire batch.
	/// Default is 60 seconds.
	/// </summary>
	public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(60);

	/// <summary>
	/// Kafka producer configuration.
	/// If not provided, will use default configuration.
	/// </summary>
	public ProducerConfig? ProducerConfig { get; init; }

	/// <summary>
	/// Bootstrap servers for Kafka.
	/// Default is "localhost:9092".
	/// </summary>
	public string BootstrapServers { get; init; } = "localhost:9092";

	/// <summary>
	/// Authentication configuration for this Kafka connection.
	/// If null, will try to load from kafkasettings.json.
	/// </summary>
	public KafkaAuthConfig? AuthConfig { get; init; }

	/// <summary>
	/// JSON serialization options for object serialization.
	/// If not provided, uses default options with camelCase naming.
	/// </summary>
	public JsonSerializerOptions? JsonOptions { get; init; }

	/// <inheritdoc />
	public async Task<ITestStepResult> ExecuteAsync(ITestContext context)
	{
		var startTime = DateTimeOffset.UtcNow;

		try
		{
			// Check for shared producer from fixture (via context)
			var sharedProducer = context.GetProperty<IProducer<string, string>>("_KafkaSharedProducer");
			var useSharedProducer = sharedProducer != null && ProducerConfig == null;

			IProducer<string, string> producer;
			if (useSharedProducer)
			{
				producer = sharedProducer!;
			}
			else
			{
				// Resolve bootstrap servers: explicit > context > default
				var resolvedBootstrapServers = BootstrapServers != "localhost:9092"
					? BootstrapServers
					: context.GetProperty<string>("_KafkaBootstrapServers") ?? BootstrapServers;

				// Build producer config
				var config = ProducerConfig ?? new ProducerConfig
				{
					BootstrapServers = resolvedBootstrapServers,
					ClientId = $"xunitassured-batch-producer-{Guid.NewGuid():N}"
				};

				// Apply authentication
				ApplyAuthentication(config);

				producer = new ProducerBuilder<string, string>(config).Build();
			}

			try
			{
				using var cts = new CancellationTokenSource(Timeout);

				// Produce all messages in parallel
				var tasks = Messages.Select(msg =>
				{
					var keyString = SerializeToString(msg.Key);
					var valueString = SerializeToString(msg.Value);

					var message = new Message<string, string>
					{
						Key = keyString,
						Value = valueString,
						Headers = Headers != null ? CloneHeaders(Headers) : null,
						Timestamp = Confluent.Kafka.Timestamp.Default
					};

					return producer.ProduceAsync(Topic, message, cts.Token);
				}).ToList();

				try
				{
					var deliveryResults = await Task.WhenAll(tasks);

					// Flush to ensure all deliveries complete
					producer.Flush(TimeSpan.FromSeconds(10));

					// Create batch success result
					Result = KafkaStepResult.CreateBatchProduceSuccess(deliveryResults);
					return Result;
				}
				catch (OperationCanceledException)
				{
					Result = KafkaStepResult.CreateProduceTimeout(Topic, Timeout);
					return Result;
				}
			}
			finally
			{
				// Only dispose producer if we created it (not shared)
				if (!useSharedProducer)
				{
					producer.Dispose();
				}
			}
		}
		catch (Exception ex)
		{
			Result = KafkaStepResult.CreateFailure(ex);
			return Result;
		}
	}

	/// <inheritdoc />
	public void Validate(Action<ITestStepResult> validation)
	{
		if (Result == null)
			throw new InvalidOperationException("Step must be executed before validation. Call ExecuteAsync first.");

		try
		{
			validation(Result);
			IsValid = true;
		}
		catch
		{
			IsValid = false;
			throw;
		}
	}

	/// <summary>
	/// Serializes an object to string for Kafka message.
	/// If the object is already a string, returns it directly.
	/// Otherwise, serializes to JSON.
	/// </summary>
	private string? SerializeToString(object? obj)
	{
		if (obj == null)
			return null;

		if (obj is string str)
			return str;

		var options = JsonOptions ?? new JsonSerializerOptions
		{
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
			DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
			WriteIndented = false
		};

		return JsonSerializer.Serialize(obj, options);
	}

	/// <summary>
	/// Clones headers so each message gets its own copy (Headers is mutable).
	/// </summary>
	private static Headers CloneHeaders(Headers source)
	{
		var clone = new Headers();
		foreach (var header in source)
		{
			clone.Add(header.Key, header.GetValueBytes());
		}
		return clone;
	}

	/// <summary>
	/// Applies authentication to the producer configuration.
	/// </summary>
	private void ApplyAuthentication(ProducerConfig config)
	{
		var authConfig = AuthConfig;

		if (authConfig == null)
		{
			var settings = KafkaSettings.Load();
			authConfig = settings.Authentication;
		}

		if (authConfig == null || authConfig.Type == KafkaAuthenticationType.None)
			return;

		IKafkaAuthenticationHandler? handler = authConfig.Type switch
		{
			KafkaAuthenticationType.SaslPlain when authConfig.SaslPlain != null => new SaslPlainHandler(authConfig.SaslPlain),
			KafkaAuthenticationType.SaslSsl when authConfig.SaslPlain != null => new SaslPlainHandler(authConfig.SaslPlain),
			KafkaAuthenticationType.SaslScram256 when authConfig.SaslScram != null => new SaslScramHandler(authConfig.SaslScram),
			KafkaAuthenticationType.SaslScram512 when authConfig.SaslScram != null => new SaslScramHandler(authConfig.SaslScram),
			KafkaAuthenticationType.Ssl when authConfig.Ssl != null => new SslHandler(authConfig.Ssl),
			KafkaAuthenticationType.MutualTls when authConfig.Ssl != null => new SslHandler(authConfig.Ssl),
			_ => null
		};

		handler?.ApplyAuthentication(config);
	}
}
