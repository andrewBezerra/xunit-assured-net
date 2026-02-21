using System;
using System.Collections.Generic;
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
/// Represents a Kafka batch message consumption step in a test scenario.
/// Consumes multiple messages from a topic using a single consumer instance.
/// </summary>
public class KafkaBatchConsumeStep : ITestStep
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
	/// The number of messages to consume.
	/// </summary>
	public int MessageCount { get; init; } = 1;

	/// <summary>
	/// Expected schema type for the consumed messages.
	/// </summary>
	public Type? SchemaType { get; init; }

	/// <summary>
	/// Timeout for consuming all messages.
	/// Default is 60 seconds.
	/// </summary>
	public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(60);

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
		try
		{
			// Resolve bootstrap servers: explicit > context > default
			var resolvedBootstrapServers = BootstrapServers != "localhost:9092"
				? BootstrapServers
				: context.GetProperty<string>("_KafkaBootstrapServers") ?? BootstrapServers;

			var resolvedGroupId = GroupId;
			if (string.Equals(GroupId, "xunitassured-consumer", StringComparison.OrdinalIgnoreCase))
			{
				resolvedGroupId = context.GetProperty<string>("_KafkaGroupId") ?? GroupId;
			}

			// Build consumer config
			var config = ConsumerConfig ?? new ConsumerConfig
			{
				BootstrapServers = resolvedBootstrapServers,
				GroupId = resolvedGroupId,
				AutoOffsetReset = AutoOffsetReset.Earliest,
				EnableAutoCommit = false,
				SessionTimeoutMs = 6000,
				HeartbeatIntervalMs = 2000,
				FetchWaitMaxMs = 100
			};

			// Apply authentication
			ApplyAuthentication(config);

			// Create a single consumer for the entire batch
			using var consumer = new ConsumerBuilder<string, string>(config).Build();

			consumer.Subscribe(Topic);

			using var cts = new CancellationTokenSource(Timeout);

			var consumedResults = new List<ConsumeResult<string, string>>(MessageCount);

			try
			{
				while (consumedResults.Count < MessageCount)
				{
					var consumeResult = consumer.Consume(cts.Token);

					if (consumeResult?.Message != null)
					{
						consumedResults.Add(consumeResult);
					}
				}

				// All N messages received
				Result = KafkaStepResult.CreateBatchConsumeSuccess(consumedResults);
				return Result;
			}
			catch (OperationCanceledException)
			{
				// Timeout before collecting all messages
				Result = KafkaStepResult.CreateBatchConsumePartial(
					Topic, MessageCount, consumedResults, Timeout);
				return Result;
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
	/// Applies authentication to the consumer configuration.
	/// </summary>
	private void ApplyAuthentication(ConsumerConfig config)
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
