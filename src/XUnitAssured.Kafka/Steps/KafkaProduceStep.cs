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
	/// JSON serialization options for object serialization.
	/// If not provided, uses default options with camelCase naming.
	/// </summary>
	public JsonSerializerOptions? JsonOptions { get; init; }

	/// <summary>
	/// Cria um passo com os valores padrão, para ser preenchido por um inicializador.
	/// </summary>
	public KafkaProduceStep()
	{
	}

	/// <summary>
	/// Cria uma cópia do passo, carregando tudo o que estava configurado. Use um
	/// inicializador para trocar só o que muda:
	/// <c>new KafkaProduceStep(anterior) { Timeout = novoPrazo }</c>.
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
	public KafkaProduceStep(KafkaProduceStep source)
	{
		ArgumentNullException.ThrowIfNull(source);

		Name = source.Name;
		Topic = source.Topic;
		Key = source.Key;
		Value = source.Value;
		Headers = source.Headers;
		Partition = source.Partition;
		Timestamp = source.Timestamp;
		Timeout = source.Timeout;
		ProducerConfig = source.ProducerConfig;
		_bootstrapServers = source._bootstrapServers;
		AuthConfig = source.AuthConfig;
		JsonOptions = source.JsonOptions;
	}

	/// <inheritdoc />
	public async Task<ITestStepResult> ExecuteAsync(ITestContext context, CancellationToken cancellationToken = default)
	{
		var startTime = DateTimeOffset.UtcNow;
		var diagnosticProperties = new Dictionary<string, object?>();
		var errorDetails = new List<string>();

		try
		{
			// Cancelado antes de começar, o passo não envia nada (#218): o ProduceAsync do cliente só para de esperar com
			// o token, e a mensagem ainda pode ir.
			cancellationToken.ThrowIfCancellationRequested();

			// Check for shared producer from fixture (via context)
			var sharedProducer = context.GetProperty<IProducer<string, string>>("_KafkaSharedProducer");
			var contextAuthConfig = context.GetProperty<KafkaAuthConfig>("_KafkaAuthConfig");
			var useSharedProducer = sharedProducer != null
				&& ProducerConfig == null
				&& AuthConfig == null
				&& !BootstrapServersInformado
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
				var config = MontarConfig(context);

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

				// A null key is a valid Kafka message: the broker assigns the partition
				// round-robin. Rejecting it here broke every produce that did not set a
				// key, including the README's own quick-start example.
				if (valueString is null)
					throw new InvalidOperationException($"Message value for topic '{Topic}' is null or its serialization returned null. Provide a non-null value.");

				// Build message
				var message = new Message<string, string>
				{
					// Null is a legitimate key (see above); the string serializer accepts it.
					Key = keyString!,
					Value = valueString,
					Headers = Headers,
					Timestamp = Timestamp.HasValue 
						? new Confluent.Kafka.Timestamp(Timestamp.Value) 
						: Confluent.Kafka.Timestamp.Default
				};

				// Produce message with timeout
				DeliveryResult<string, string> deliveryResult;

				// O prazo do passo e o cancelamento de quem chamou valem os dois (#218), como no consumo.
				using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
				cts.CancelAfter(Timeout);

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
				catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
				{
					// Timeout. O cancelamento de quem chamou não é timeout: ele segue para a falha, abaixo.
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
			// Com o que foi juntado até a falha (broker, autenticação, erros do librdkafka), como no consumo.
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
	/// A configuração do produtor deste passo — endereço, ClientId e autenticação —, sem conectar.
	/// </summary>
	internal ProducerConfig MontarConfig(ITestContext context)
	{
		// Precedência: explícito > contexto > testsettings.json > padrão. O "explícito" é
		// registrado no passo, e não deduzido comparando com o padrão: configurar
		// localhost:9092 de propósito é justamente o caso comum de um broker local.
		var resolvedBootstrapServers = BootstrapServersInformado
			? BootstrapServers
			: context.GetProperty<string>("_KafkaBootstrapServers")
				?? KafkaSettings.Load().BootstrapServers;

		// A base é o topo da seção kafka — protocolo, SASL, CA, additionalProperties (#172) —; a
		// autenticação do passo, da fixture ou do arquivo vem por cima.
		var config = ProducerConfig ?? new ProducerConfig(KafkaSettings.Load().ConfiguracaoBase())
		{
			BootstrapServers = resolvedBootstrapServers,
			ClientId = $"xunitassured-producer-{Guid.NewGuid():N}"
		};

		AutenticacaoKafka.Aplicar(config, AutenticacaoKafka.Resolver(AuthConfig, context));
		return config;
	}
}
