using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Confluent.Kafka;

using XUnitAssured.Core.Abstractions;
using XUnitAssured.Core.Results;
using XUnitAssured.Kafka.Configuration;
using XUnitAssured.Kafka.Handlers;
using XUnitAssured.Kafka.Helpers;
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
	/// If null, will try to load from the "kafka" section of testsettings.json.
	/// </summary>
	public KafkaAuthConfig? AuthConfig { get; init; }

	/// <summary>
	/// Upper bound on broker log entries retained for diagnostics on a single step.
	/// </summary>
	private const int MaxBrokerLogEntries = 200;

	/// <inheritdoc />
	/// <summary>Quanto se espera entre duas olhadas no que chegou.</summary>
	private static readonly TimeSpan IntervaloEntreTentativas = TimeSpan.FromMilliseconds(50);

	public async Task<ITestStepResult> ExecuteAsync(ITestContext context, CancellationToken cancellationToken = default)
	{
		// Collected as the step runs so that a failure can explain itself: which
		// broker, which group, what the broker said. Broker logs are diagnostics,
		// not failures, and are capped so a chatty broker cannot grow them unbounded.
		var diagnosticProperties = new Dictionary<string, object?>();
		var errorDetails = new List<string>();
		var brokerLogs = new List<string>();

		try
		{
			// Resolve bootstrap servers: explicit > context > testsettings.json > default.
			// The last fallback returns "localhost:9092" when nothing is configured, so
			// a project without a settings file behaves exactly as before.
			var resolvedBootstrapServers = BootstrapServers != "localhost:9092"
				? BootstrapServers
				: context.GetProperty<string>("_KafkaBootstrapServers")
					?? KafkaSettings.Load().BootstrapServers;

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

			diagnosticProperties["BootstrapServers"] = config.BootstrapServers;
			diagnosticProperties["GroupId"] = config.GroupId;
			diagnosticProperties["SecurityProtocol"] = config.SecurityProtocol.ToString();
			diagnosticProperties["SaslMechanism"] = config.SaslMechanism?.ToString();
			diagnosticProperties["SaslUsername"] = config.SaslUsername;

			// Create a single consumer for the entire batch
			using var consumer = new ConsumerBuilder<string, string>(config)
				.SetErrorHandler((_, error) => errorDetails.Add(error.ToString()))
				.SetLogHandler((_, log) =>
				{
					if (brokerLogs.Count < MaxBrokerLogEntries)
						brokerLogs.Add($"{log.Level}: {log.Message}");
				})
				.Build();

			// Assign partitions directly rather than subscribing: a subscription joins
			// the consumer group and, on a default broker, waits out a three-second
			// initial rebalance delay before the first message. Starting offsets are
			// resolved the way a subscription would (committed, else AutoOffsetReset).
			// Bounded by the step's own timeout: a step given one second must not wait
			// five for metadata from a broker that is not answering.
			var metadataTimeout = Timeout < TimeSpan.FromSeconds(5) ? Timeout : TimeSpan.FromSeconds(5);
			ConsumerAssignment.AssignAllPartitions(consumer, config, Topic, metadataTimeout);

			// O prazo do passo e o cancelamento de quem chamou valem os dois: o que vier
			// primeiro encerra a espera.
			using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
			cts.CancelAfter(Timeout);

			var consumedResults = new List<ConsumeResult<string, string>>(MessageCount);

			try
			{
				while (consumedResults.Count < MessageCount)
				{
					// Mesma troca do consumo de uma mensagem so: olhar o que ja chegou e
					// devolver a thread entre as olhadas, em vez de prende-la esperando. Ver
					// o comentario em KafkaConsumeStep.
					var consumeResult = consumer.Consume(TimeSpan.Zero);

					if (consumeResult?.Message != null)
					{
						consumedResults.Add(consumeResult);
						continue;
					}

					await Task.Delay(IntervaloEntreTentativas, cts.Token).ConfigureAwait(false);
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
			// The connection settings and broker logs collected above are what
			// explain a failed batch consume, so they travel with the exception
			// instead of being discarded — the same fix the single consume got.
			diagnosticProperties["BrokerLogs"] = brokerLogs;
			Result = KafkaStepResult.CreateFailure(ex, errorDetails, diagnosticProperties);
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
