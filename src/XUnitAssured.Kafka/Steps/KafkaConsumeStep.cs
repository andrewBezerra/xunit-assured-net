using System;
using System.Collections.Generic;
using System.Linq;
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
/// Represents a Kafka message consumption step in a test scenario.
/// Executes Kafka consume operations and returns KafkaStepResult.
/// </summary>
public class KafkaConsumeStep : ITestStep
{
	/// <summary>
	/// Upper bound on broker log entries retained for diagnostics on a single step.
	/// </summary>
	private const int MaxBrokerLogEntries = 200;

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
	/// If null, will try to load from the "kafka" section of testsettings.json.
	/// </summary>
	public KafkaAuthConfig? AuthConfig { get; init; }

	/// <summary>
	/// Quanto se espera entre duas olhadas no que chegou.
	///
	/// Cinquenta milissegundos é menos do que os duzentos e cinquenta que a espera bloqueante
	/// usava, então uma mensagem é notada mais cedo, e não mais tarde.
	/// </summary>
	private static readonly TimeSpan IntervaloEntreTentativas = TimeSpan.FromMilliseconds(50);

	/// <summary>
	/// Cria um passo com os valores padrão, para ser preenchido por um inicializador.
	/// </summary>
	public KafkaConsumeStep()
	{
	}

	/// <summary>
	/// Cria uma cópia do passo, carregando tudo o que estava configurado. Use um
	/// inicializador para trocar só o que muda:
	/// <c>new KafkaConsumeStep(anterior) { Timeout = novoPrazo }</c>.
	/// </summary>
	/// <remarks>
	/// Reconstruir o passo campo por campo é como configuração se perde em silêncio. Foi
	/// assim que cinco verbos do lado do consumo descartaram o <see cref="AuthConfig"/>:
	/// autenticar e depois ajustar o prazo trocava a credencial explícita pela da fixture.
	/// Copiar por aqui faz uma propriedade nova ser carregada por padrão, em vez de
	/// depender de alguém lembrar de cada verbo.
	/// </remarks>
	/// <param name="source">O passo de onde copiar a configuração.</param>
	/// <exception cref="ArgumentNullException">Quando <paramref name="source"/> é nulo.</exception>
	public KafkaConsumeStep(KafkaConsumeStep source)
	{
		if (source == null)
			throw new ArgumentNullException(nameof(source));

		Name = source.Name;
		Topic = source.Topic;
		SchemaType = source.SchemaType;
		Timeout = source.Timeout;
		ConsumerConfig = source.ConsumerConfig;
		GroupId = source.GroupId;
		BootstrapServers = source.BootstrapServers;
		AuthConfig = source.AuthConfig;
	}

	/// <inheritdoc />
	public async Task<ITestStepResult> ExecuteAsync(ITestContext context, CancellationToken cancellationToken = default)
	{
		var startTime = DateTimeOffset.UtcNow;
		var diagnosticProperties = new Dictionary<string, object?>();
		var errorDetails = new List<string>();

		// Broker log messages are diagnostics, not failures. They are kept apart from
		// errorDetails so a successful consume does not report informational chatter
		// as errors, and capped so a chatty broker cannot grow this unbounded.
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
			ApplyAuthentication(config, context);

			// NOTE: librdkafka's Debug option is deliberately left untouched here.
			// Enabling it by default costs measurable CPU and floods the result with
			// broker chatter. Set ConsumerConfig.Debug explicitly when troubleshooting.

			diagnosticProperties["BootstrapServers"] = config.BootstrapServers;
			diagnosticProperties["GroupId"] = config.GroupId;
			diagnosticProperties["SecurityProtocol"] = config.SecurityProtocol.ToString();
			diagnosticProperties["SaslMechanism"] = config.SaslMechanism?.ToString();
			diagnosticProperties["SaslUsername"] = config.SaslUsername;

			// Create consumer
			using var consumer = new ConsumerBuilder<string, string>(config)
				.SetErrorHandler((_, error) => errorDetails.Add(error.ToString()))
				.SetLogHandler((_, log) =>
				{
					if (brokerLogs.Count < MaxBrokerLogEntries)
						brokerLogs.Add($"{log.Level}: {log.Message}");
				})
				.Build();

			// Assign the partitions directly instead of subscribing. A subscription
			// joins the consumer group, which on a default broker waits out
			// group.initial.rebalance.delay.ms (three seconds) before the first
			// message can be read — paid by every consume step, since each one is a
			// fresh consumer. Manual assignment keeps the same starting offsets a
			// subscription would use (committed, else AutoOffsetReset) without the join.
			// This is also what the previous code fell back to when the coordinator
			// was unavailable, so that fallback is now simply the only path.
			// Metadata never needs more than a few seconds against a live broker, and
			// a step that was given a shorter timeout should not wait longer than that
			// for a broker that is not answering at all.
			var metadataTimeout = Timeout < TimeSpan.FromSeconds(5) ? Timeout : TimeSpan.FromSeconds(5);
			var assignment = ConsumerAssignment.AssignAllPartitions(
				consumer, config, Topic, metadataTimeout);
			diagnosticProperties["AssignedPartitions"] = string.Join(",", assignment.Select(a => a.TopicPartition));

			var deadline = DateTime.UtcNow.Add(Timeout);

			while (DateTime.UtcNow <= deadline)
			{
				cancellationToken.ThrowIfCancellationRequested();

				// `Consume` com espera zero olha o que o cliente ja trouxe e volta na hora; a
				// espera vira um Task.Delay, que devolve a thread ao pool em vez de prende-la
				// ate o fim do intervalo. Numa suite paralela essa diferenca e o que separa
				// um teste que espera de um pool que acabou.
				//
				// Nao ha ida a mais ao broker: o cliente mantem um buffer proprio, alimentado
				// por uma thread dele, e isto apenas le desse buffer.
				var consumeResult = consumer.Consume(TimeSpan.Zero);

				if (consumeResult?.Message != null)
				{
					// Create success result
					Result = KafkaStepResult.CreateKafkaConsumeSuccess(consumeResult);
					return Result;
				}

				await Task.Delay(IntervaloEntreTentativas, cancellationToken).ConfigureAwait(false);
			}

			// No message received. Surface the broker chatter here, where it is
			// actually useful for diagnosing why nothing arrived.
			//
			// The key is always present, even with nothing to report: whether
			// librdkafka emits anything within the timeout varies by platform and
			// client version, and a caller should not have to distinguish "no logs"
			// from "no such property".
			diagnosticProperties["BrokerLogs"] = brokerLogs;

			Result = KafkaStepResult.CreateTimeout(Topic, Timeout, errorDetails, diagnosticProperties);
			return Result;
		}
		catch (Exception ex)
		{
			// Network error, Kafka error, etc. The connection settings and broker
			// logs collected above are what explain such a failure, so they are
			// carried into the result rather than discarded with the exception.
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
	/// Uses AuthConfig if provided, otherwise resolves from context or loads from testsettings.json.
	/// </summary>
	private void ApplyAuthentication(ConsumerConfig config, ITestContext context)
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
