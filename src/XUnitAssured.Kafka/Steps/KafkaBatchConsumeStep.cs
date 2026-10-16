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
	public string GroupId
	{
		get => _groupId ?? "xunitassured-consumer";
		init => _groupId = value;
	}

	/// <summary>
	/// Se quem montou o passo informou o valor, em vez de ele por acaso ser igual ao
	/// padrão. A resolução decidia isso comparando com "xunitassured-consumer", e as duas coisas
	/// não são a mesma: um projeto que configure o padrão de propósito tinha o valor dele
	/// descartado em favor do contexto.
	/// </summary>
	internal bool GroupIdInformado => _groupId is not null;

	private readonly string? _groupId;

	/// <summary>
	/// Bootstrap servers for Kafka.
	/// Default is "localhost:9092".
	/// </summary>
	public string BootstrapServers
	{
		get => _bootstrapServers ?? "localhost:9092";
		init => _bootstrapServers = value;
	}

	/// <summary>
	/// Se quem montou o passo informou o valor, em vez de ele por acaso ser igual ao
	/// padrão. A resolução decidia isso comparando com "localhost:9092", e as duas coisas
	/// não são a mesma: um projeto que configure o padrão de propósito tinha o valor dele
	/// descartado em favor do contexto.
	/// </summary>
	internal bool BootstrapServersInformado => _bootstrapServers is not null;

	private readonly string? _bootstrapServers;

	/// <summary>
	/// Authentication configuration for this Kafka connection.
	/// If null, will try to load from the "kafka" section of testsettings.json.
	/// </summary>
	public KafkaAuthConfig? AuthConfig { get; init; }

	/// <summary>
	/// De onde vêm o consumidor e as partições do tópico. Fora dos testes do pacote, é sempre
	/// <see cref="FabricaDeConsumidor.Real"/>.
	/// </summary>
	internal IFabricaDeConsumidor Fabrica { get; init; } = FabricaDeConsumidor.Real;

	/// <summary>
	/// Upper bound on broker log entries retained for diagnostics on a single step.
	/// </summary>
	private const int MaxBrokerLogEntries = 200;

	/// <summary>Quanto se espera entre duas olhadas no que chegou.</summary>
	private static readonly TimeSpan IntervaloEntreTentativas = TimeSpan.FromMilliseconds(50);

	/// <summary>
	/// Cria um passo com os valores padrão, para ser preenchido por um inicializador.
	/// </summary>
	public KafkaBatchConsumeStep()
	{
	}

	/// <summary>
	/// Cria uma cópia do passo, carregando tudo o que estava configurado. Use um
	/// inicializador para trocar só o que muda:
	/// <c>new KafkaBatchConsumeStep(anterior) { Timeout = novoPrazo }</c>.
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
	public KafkaBatchConsumeStep(KafkaBatchConsumeStep source)
	{
		if (source == null)
			throw new ArgumentNullException(nameof(source));

		Name = source.Name;
		Topic = source.Topic;
		MessageCount = source.MessageCount;
		SchemaType = source.SchemaType;
		Timeout = source.Timeout;
		ConsumerConfig = source.ConsumerConfig;
		_groupId = source._groupId;
		_bootstrapServers = source._bootstrapServers;
		AuthConfig = source.AuthConfig;
		Fabrica = source.Fabrica;
	}

	/// <inheritdoc />
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
			var config = MontarConfig(context);

			diagnosticProperties["BootstrapServers"] = config.BootstrapServers;
			diagnosticProperties["GroupId"] = config.GroupId;
			diagnosticProperties["SecurityProtocol"] = config.SecurityProtocol.ToString();
			diagnosticProperties["SaslMechanism"] = config.SaslMechanism?.ToString();
			diagnosticProperties["SaslUsername"] = config.SaslUsername;

			// Create a single consumer for the entire batch
			using var consumer = Fabrica.Criar(
				config,
				error => errorDetails.Add(error.ToString()),
				log =>
				{
					if (brokerLogs.Count < MaxBrokerLogEntries)
						brokerLogs.Add($"{log.Level}: {log.Message}");
				});

			// Assign partitions directly rather than subscribing: a subscription joins
			// the consumer group and, on a default broker, waits out a three-second
			// initial rebalance delay before the first message. Starting offsets are
			// resolved the way a subscription would (committed, else AutoOffsetReset).
			// Bounded by the step's own timeout: a step given one second must not wait
			// five for metadata from a broker that is not answering.
			var metadataTimeout = Timeout < TimeSpan.FromSeconds(5) ? Timeout : TimeSpan.FromSeconds(5);
			ConsumerAssignment.AssignAllPartitions(consumer, config, Topic, metadataTimeout, Fabrica.DescobrirParticoes);

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
	/// A configuração do consumidor deste passo — endereço, grupo e autenticação —, sem conectar.
	/// </summary>
	/// <remarks>
	/// Lê a autenticação que a fixture põe no contexto, como o passo simples; até a #164, o passo
	/// em lote ia direto ao arquivo de configuração.
	/// </remarks>
	internal ConsumerConfig MontarConfig(ITestContext context)
	{
		// Precedência: explícito > contexto > testsettings.json > padrão. O "explícito" é
		// registrado no passo, e não deduzido comparando com o padrão: configurar
		// localhost:9092 de propósito é justamente o caso comum de um broker local.
		var resolvedBootstrapServers = BootstrapServersInformado
			? BootstrapServers
			: context.GetProperty<string>("_KafkaBootstrapServers")
				?? KafkaSettings.Load().BootstrapServers;

		var resolvedGroupId = GroupId;
		if (!GroupIdInformado)
		{
			resolvedGroupId = context.GetProperty<string>("_KafkaGroupId") ?? GroupId;
		}

		// A base é o topo da seção kafka — protocolo, SASL, CA, additionalProperties (#172) —; a
		// autenticação do passo, da fixture ou do arquivo vem por cima.
		var config = ConsumerConfig ?? new ConsumerConfig(KafkaSettings.Load().ConfiguracaoBase())
		{
			BootstrapServers = resolvedBootstrapServers,
			GroupId = resolvedGroupId,
			AutoOffsetReset = AutoOffsetReset.Earliest,
			EnableAutoCommit = false,
			SessionTimeoutMs = 6000,
			HeartbeatIntervalMs = 2000,
			FetchWaitMaxMs = 100
		};

		AutenticacaoKafka.Aplicar(config, AutenticacaoKafka.Resolver(AuthConfig, context));
		return config;
	}
}
