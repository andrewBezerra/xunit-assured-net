using System;
using System.Collections.Generic;
using System.Text;
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
/// Represents a Kafka message production step in a test scenario.
/// Executes Kafka produce operations and returns KafkaStepResult.
/// </summary>
public class KafkaProduceStep : ITestStep
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
	/// The message key. Can be string or object (will be JSON serialized).
	/// </summary>
	public object? Key { get; init; }

	/// <summary>
	/// The message value. Can be string or object (will be JSON serialized).
	/// </summary>
	public object? Value { get; init; }

	/// <summary>
	/// Optional Kafka message headers.
	/// </summary>
	public Headers? Headers { get; init; }

	/// <summary>
	/// Optional specific partition to produce to.
	/// If null, Kafka will choose partition based on key or round-robin.
	/// </summary>
	public int? Partition { get; init; }

	/// <summary>
	/// Optional custom timestamp for the message.
	/// If null, Kafka will use current timestamp.
	/// </summary>
	public DateTime? Timestamp { get; init; }

	/// <summary>
	/// Timeout for producing a message.
	/// Default is 30 seconds.
	/// </summary>
	public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);

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
		var diagnosticProperties = new Dictionary<string, object?>();
		var errorDetails = new List<string>();

		try
		{
			// Check for shared producer from fixture (via context)
			var sharedProducer = context.GetProperty<IProducer<string, string>>("_KafkaSharedProducer");
			var contextAuthConfig = context.GetProperty<KafkaAuthConfig>("_KafkaAuthConfig");
			var useSharedProducer = sharedProducer != null
				&& ProducerConfig == null
				&& AuthConfig == null
				&& BootstrapServers == "localhost:9092"
				&& (contextAuthConfig == null || contextAuthConfig.Type == KafkaAuthenticationType.None);

			IProducer<string, string> producer;
			if (useSharedProducer)
			{
				producer = sharedProducer!;

				var sharedErrors = context.GetProperty<System.Collections.Concurrent.ConcurrentQueue<string>>("_KafkaSharedProducerErrors");
				if (sharedErrors != null)
				{
					while (sharedErrors.TryDequeue(out var error))
						errorDetails.Add(error);
				}

				diagnosticProperties["BootstrapServers"] = context.GetProperty<string>("_KafkaBootstrapServers") ?? BootstrapServers;
				var authConfig = context.GetProperty<KafkaAuthConfig>("_KafkaAuthConfig");
				diagnosticProperties["AuthType"] = authConfig?.Type.ToString();
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
					ClientId = $"xunitassured-producer-{Guid.NewGuid():N}"
				};

				// Apply authentication
				ApplyAuthentication(config, context);

				diagnosticProperties["BootstrapServers"] = config.BootstrapServers;
				diagnosticProperties["SecurityProtocol"] = config.SecurityProtocol.ToString();
				diagnosticProperties["SaslMechanism"] = config.SaslMechanism?.ToString();
				diagnosticProperties["SaslUsername"] = config.SaslUsername;

				producer = new ProducerBuilder<string, string>(config)
					.SetErrorHandler((_, error) => errorDetails.Add(error.ToString()))
					.SetLogHandler((_, log) =>
					{
						if (log.Level <= SyslogLevel.Error)
							errorDetails.Add($"{log.Level}: {log.Message}");
					})
					.Build();
			}

			try
			{
				// Serialize key and value to string
				var keyString = SerializeToString(Key);
				var valueString = SerializeToString(Value);

				// Build message
				var message = new Message<string, string>
				{
					Key = keyString!,
					Value = valueString!,
					Headers = Headers,
					Timestamp = Timestamp.HasValue 
						? new Confluent.Kafka.Timestamp(Timestamp.Value) 
						: Confluent.Kafka.Timestamp.Default
				};

				// Produce message with timeout
				DeliveryResult<string, string> deliveryResult;

				using var cts = new CancellationTokenSource(Timeout);

				try
				{
					if (Partition.HasValue)
					{
						var topicPartition = new TopicPartition(Topic, new Confluent.Kafka.Partition(Partition.Value));
						deliveryResult = await producer.ProduceAsync(topicPartition, message, cts.Token);
					}
					else
					{
						deliveryResult = await producer.ProduceAsync(Topic, message, cts.Token);
					}

					// Flush to ensure delivery
					producer.Flush(TimeSpan.FromSeconds(5));

					// Create success result
					Result = KafkaStepResult.CreateKafkaProduceSuccess(deliveryResult);
					return Result;
				}
				catch (OperationCanceledException)
				{
					// Timeout
					Result = KafkaStepResult.CreateProduceTimeout(Topic, Timeout, errorDetails, diagnosticProperties);
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
			// Network error, Kafka error, etc.
			errorDetails.Add(ex.ToString());
			diagnosticProperties["ExceptionMessage"] = ex.Message;
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

		// Serialize object to JSON
		var options = JsonOptions ?? new JsonSerializerOptions
		{
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
			DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
			WriteIndented = false
		};

		return JsonSerializer.Serialize(obj, options);
	}

	/// <summary>
	/// Applies authentication to the producer configuration.
	/// Uses AuthConfig if provided, otherwise resolves from context or loads from kafkasettings.json.
	/// </summary>
	private void ApplyAuthentication(ProducerConfig config, ITestContext context)
	{
		// Get authentication config
		var authConfig = AuthConfig;

		// If no config provided, try to resolve from context (fixture settings)
		authConfig ??= context.GetProperty<KafkaAuthConfig>("_KafkaAuthConfig");

		// If still no config, try to load from settings
		if (authConfig == null)
		{
			var settings = KafkaSettings.Load();
			authConfig = settings.Authentication;
		}

		// Skip if no authentication configured
		if (authConfig == null || authConfig.Type == KafkaAuthenticationType.None)
			return;

		// Create appropriate handler and apply authentication
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
