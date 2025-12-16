using System;
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
/// Represents a Kafka message consumption step in a test scenario.
/// Executes Kafka consume operations and returns KafkaStepResult.
/// </summary>
public class KafkaConsumeStep : ITestStep
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
	/// The Kafka topic to consume from.
	/// </summary>
	public string Topic { get; init; } = string.Empty;

	/// <summary>
	/// Expected schema type for the consumed message.
	/// </summary>
	public Type? SchemaType { get; init; }

	/// <summary>
	/// Timeout for consuming a message.
	/// Default is 30 seconds.
	/// </summary>
	public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);

	/// <summary>
	/// Kafka consumer configuration.
	/// If not provided, will use default configuration.
	/// </summary>
	public ConsumerConfig? ConsumerConfig { get; init; }

	/// <summary>
	/// Consumer group ID.
	/// Default is "xunitassured-consumer".
	/// </summary>
	public string GroupId { get; init; } = "xunitassured-consumer";

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

	/// <inheritdoc />
	public async Task<ITestStepResult> ExecuteAsync(ITestContext context)
	{
		var startTime = DateTimeOffset.UtcNow;

		try
		{
			// Build consumer config
			var config = ConsumerConfig ?? new ConsumerConfig
			{
				BootstrapServers = BootstrapServers,
				GroupId = GroupId,
				AutoOffsetReset = AutoOffsetReset.Latest,
				EnableAutoCommit = false
			};

			// Apply authentication
			ApplyAuthentication(config);

			// Create consumer
			using var consumer = new ConsumerBuilder<string, string>(config).Build();

			// Subscribe to topic
			consumer.Subscribe(Topic);

			// Try to consume message with timeout
			using var cts = new CancellationTokenSource(Timeout);

			try
			{
				var consumeResult = consumer.Consume(cts.Token);

				if (consumeResult != null && consumeResult.Message != null)
				{
					// Create success result
					Result = KafkaStepResult.CreateKafkaConsumeSuccess(consumeResult);
					return Result;
				}

				// No message received
				Result = KafkaStepResult.CreateTimeout(Topic, Timeout);
				return Result;
			}
			catch (OperationCanceledException)
			{
				// Timeout
				Result = KafkaStepResult.CreateTimeout(Topic, Timeout);
				return Result;
			}
		}
		catch (Exception ex)
		{
			// Network error, Kafka error, etc.
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
	/// Applies authentication to the consumer configuration.
	/// Uses AuthConfig if provided, otherwise loads from kafkasettings.json.
	/// </summary>
	private void ApplyAuthentication(ConsumerConfig config)
	{
		// Get authentication config
		var authConfig = AuthConfig;

		// If no config provided, try to load from settings
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
			_ => null
		};

		handler?.ApplyAuthentication(config);
	}
}
