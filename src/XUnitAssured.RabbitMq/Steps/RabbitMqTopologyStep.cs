using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

using RabbitMQ.Client;

using XUnitAssured.Core.Abstractions;
using XUnitAssured.Core.Results;
using XUnitAssured.RabbitMq.Results;

namespace XUnitAssured.RabbitMq.Steps;

/// <summary>O que um passo de topologia declara no broker.</summary>
public enum RabbitMqTopologyOperation
{
	/// <summary>Declara a fila nomeada pela cadeia.</summary>
	DeclareQueue = 0,

	/// <summary>Declara a exchange nomeada pela cadeia.</summary>
	DeclareExchange = 1,

	/// <summary>Liga a fila nomeada pela cadeia a uma exchange, por uma routing key.</summary>
	BindQueue = 2
}

/// <summary>
/// Declara fila, exchange ou binding no broker.
/// </summary>
/// <remarks>
/// <para>
/// Antes deste passo, um teste que precisava de fila descia à API do cliente — os próprios testes
/// de ida e volta deste repositório faziam isso num auxiliar. É o ponto em que um DSL deixa de
/// pagar o próprio preço: ele tinha verbo para publicar e consumir, e nenhum para a topologia sem a
/// qual os dois não têm onde acontecer.
/// </para>
/// <para>
/// Declarar é idempotente no AMQP: declarar duas vezes com os mesmos argumentos não é erro. Com
/// argumentos diferentes é, e o erro vem do broker, que é onde a verdade mora.
/// </para>
/// </remarks>
public class RabbitMqTopologyStep : ITestStep
{
	private readonly string? _connectionUri;

	/// <inheritdoc />
	public string? Name { get; internal set; }

	/// <inheritdoc />
	public string StepType => "RabbitMq";

	/// <inheritdoc />
	public ITestStepResult? Result { get; private set; }

	/// <inheritdoc />
	public bool IsExecuted => Result != null;

	/// <inheritdoc />
	public bool IsValid { get; private set; }

	/// <summary>O que declarar.</summary>
	public RabbitMqTopologyOperation Operation { get; init; }

	/// <summary>A fila, quando a operação é sobre fila.</summary>
	public string Queue { get; init; } = string.Empty;

	/// <summary>A exchange, quando a operação é sobre exchange ou binding.</summary>
	public string Exchange { get; init; } = string.Empty;

	/// <summary>
	/// O tipo da exchange: <c>direct</c>, <c>topic</c>, <c>fanout</c> ou <c>headers</c>.
	/// </summary>
	public string ExchangeType { get; init; } = "direct";

	/// <summary>A routing key do binding.</summary>
	public string RoutingKey { get; init; } = string.Empty;

	/// <summary>
	/// Se a fila sobrevive a um reinício do broker. Padrão: sim.
	/// </summary>
	/// <remarks>
	/// Durável por padrão porque o RabbitMQ 4 recusa fila transitória não exclusiva, com
	/// <c>INTERNAL_ERROR</c> e a mensagem de que <c>transient_nonexcl_queues</c> está em
	/// depreciação. Um padrão que o broker atual rejeita não é padrão, é armadilha.
	/// </remarks>
	public bool Durable { get; init; } = true;

	/// <summary>
	/// A exchange para onde a fila manda o que foi recusado sem requeue, ou nulo para não ter.
	/// </summary>
	/// <remarks>
	/// Vai como o argumento <c>x-dead-letter-exchange</c> da fila. Uma fila já declarada sem ele
	/// não ganha o argumento depois: o broker recusa a redeclaração com argumentos diferentes, e é
	/// ele que está certo.
	/// </remarks>
	public string? DeadLetterExchange { get; init; }

	/// <summary>A URI de conexão, quando não vem do contexto nem do arquivo.</summary>
	public string ConnectionUri
	{
		get => _connectionUri ?? string.Empty;
		init => _connectionUri = value;
	}

	/// <summary>Se quem montou o passo informou a URI.</summary>
	internal bool ConnectionUriInformada => _connectionUri is not null;

	/// <summary>
	/// Cria um passo com os valores padrão, para ser preenchido por um inicializador.
	/// </summary>
	public RabbitMqTopologyStep()
	{
	}

	/// <summary>
	/// Cria uma cópia do passo, carregando tudo o que estava configurado.
	/// </summary>
	public RabbitMqTopologyStep(RabbitMqTopologyStep source)
	{
		if (source == null)
			throw new ArgumentNullException(nameof(source));

		Name = source.Name;
		Operation = source.Operation;
		Queue = source.Queue;
		Exchange = source.Exchange;
		ExchangeType = source.ExchangeType;
		RoutingKey = source.RoutingKey;
		Durable = source.Durable;
		DeadLetterExchange = source.DeadLetterExchange;
		_connectionUri = source._connectionUri;
	}

	/// <inheritdoc />
	public async Task<ITestStepResult> ExecuteAsync(
		ITestContext context, CancellationToken cancellationToken = default)
	{
		var cronometro = Stopwatch.StartNew();
		var destino = Operation == RabbitMqTopologyOperation.DeclareExchange ? Exchange : Queue;
		var diagnostico = new Dictionary<string, object?>();

		try
		{
			var uri = ResolverUri(context);
			diagnostico["ConnectionUri"] = RabbitMqPublishStep.Mascarar(uri);
			diagnostico["Operation"] = Operation.ToString();

			var fabrica = new ConnectionFactory
			{
				Uri = new Uri(uri),
				ClientProvidedName = RabbitMqSettings.Load().ClientProvidedName
			};

			await using var conexao = await fabrica.CreateConnectionAsync(cancellationToken)
				.ConfigureAwait(false);
			await using var canal = await conexao.CreateChannelAsync(cancellationToken: cancellationToken)
				.ConfigureAwait(false);

			switch (Operation)
			{
				case RabbitMqTopologyOperation.DeclareQueue:
					var argumentos = DeadLetterExchange is null
						? null
						: new Dictionary<string, object?>
						{
							["x-dead-letter-exchange"] = DeadLetterExchange
						};

					await canal.QueueDeclareAsync(
							Queue, durable: Durable, exclusive: false, autoDelete: false,
							arguments: argumentos, cancellationToken: cancellationToken)
						.ConfigureAwait(false);
					break;

				case RabbitMqTopologyOperation.DeclareExchange:
					await canal.ExchangeDeclareAsync(
							Exchange, ExchangeType, durable: Durable, autoDelete: false,
							cancellationToken: cancellationToken)
						.ConfigureAwait(false);
					break;

				case RabbitMqTopologyOperation.BindQueue:
					await canal.QueueBindAsync(
							Queue, Exchange, RoutingKey, cancellationToken: cancellationToken)
						.ConfigureAwait(false);
					break;

				default:
					throw new NotSupportedException($"Unknown topology operation '{Operation}'.");
			}

			Result = RabbitMqStepResult.CreatePublishSuccess(destino, RoutingKey, cronometro.Elapsed);
			IsValid = true;
			return Result;
		}
		catch (Exception ex)
		{
			Result = RabbitMqStepResult.CreateFailure(ex, destino, diagnostico, cronometro.Elapsed);
			IsValid = false;
			return Result;
		}
	}

	/// <inheritdoc />
	public void Validate(Action<ITestStepResult> validation)
	{
		if (Result == null)
			throw new InvalidOperationException("Step has not been executed. Execute the chain before validating.");

		validation(Result);
		IsValid = true;
	}

	private string ResolverUri(ITestContext context) =>
		ConnectionUriInformada
			? ConnectionUri
			: context.GetProperty<string>("_RabbitMqConnectionUri")
				?? RabbitMqSettings.Load().ConnectionUri;
}
