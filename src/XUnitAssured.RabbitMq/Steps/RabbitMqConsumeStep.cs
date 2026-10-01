using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using RabbitMQ.Client;

using XUnitAssured.Core.Abstractions;
using XUnitAssured.Core.Results;
using XUnitAssured.RabbitMq.Results;

namespace XUnitAssured.RabbitMq.Steps;

/// <summary>
/// Retira uma mensagem de uma fila, esperando até o prazo.
/// </summary>
/// <remarks>
/// <para>
/// Usa <c>BasicGetAsync</c>, que pergunta ao broker se há mensagem e volta na hora. Entre
/// tentativas o passo devolve a thread com <c>await Task.Delay</c>, e aqui isso é escolha e não
/// contorno: o cliente é assíncrono de verdade, então nenhuma thread fica parada esperando rede.
/// </para>
/// <para>
/// A mensagem é confirmada ao broker assim que chega. Um teste que leu a mensagem não quer que ela
/// volte para a fila e contamine o teste seguinte, e deixar a confirmação para depois da asserção
/// faria o descarte depender de a asserção passar.
/// </para>
/// </remarks>
public class RabbitMqConsumeStep : ITestStep
{
	private readonly string? _connectionUri;

	/// <summary>
	/// Quanto se espera entre duas perguntas ao broker. Vinte e cinco milissegundos mantém a
	/// latência imperceptível num teste sem transformar a espera em laço cheio.
	/// </summary>
	private static readonly TimeSpan IntervaloEntreTentativas = TimeSpan.FromMilliseconds(25);

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

	/// <summary>A fila de onde retirar.</summary>
	public string Queue { get; init; } = string.Empty;

	/// <summary>Quanto esperar por uma mensagem.</summary>
	public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);

	/// <summary>
	/// A URI de conexão. Quando não informada, vem do contexto da fixture e depois do
	/// <c>testsettings.json</c>.
	/// </summary>
	public string ConnectionUri
	{
		get => _connectionUri ?? string.Empty;
		init => _connectionUri = value;
	}

	/// <summary>
	/// Se quem montou o passo informou a URI, em vez de ela por acaso ser igual ao padrão.
	/// </summary>
	internal bool ConnectionUriInformada => _connectionUri is not null;

	/// <summary>
	/// Cria um passo com os valores padrão, para ser preenchido por um inicializador.
	/// </summary>
	public RabbitMqConsumeStep()
	{
	}

	/// <summary>
	/// Cria uma cópia do passo, carregando tudo o que estava configurado.
	/// </summary>
	/// <remarks>
	/// O campo de apoio da URI é copiado, e não a propriedade: copiar a propriedade gravaria o
	/// padrão e o marcaria como informado, e aí o contexto da fixture deixaria de valer depois do
	/// primeiro verbo que reconstrói o passo.
	/// </remarks>
	public RabbitMqConsumeStep(RabbitMqConsumeStep source)
	{
		if (source == null)
			throw new ArgumentNullException(nameof(source));

		Name = source.Name;
		Queue = source.Queue;
		Timeout = source.Timeout;
		_connectionUri = source._connectionUri;
	}

	/// <inheritdoc />
	public async Task<ITestStepResult> ExecuteAsync(
		ITestContext context, CancellationToken cancellationToken = default)
	{
		var cronometro = Stopwatch.StartNew();
		var diagnostico = new Dictionary<string, object?>();

		try
		{
			var uri = ResolverUri(context);
			diagnostico["ConnectionUri"] = RabbitMqPublishStep.Mascarar(uri);
			diagnostico["Queue"] = Queue;
			diagnostico["TimeoutSeconds"] = Timeout.TotalSeconds;

			var fabrica = new ConnectionFactory
			{
				Uri = new Uri(uri),
				ClientProvidedName = RabbitMqSettings.Load().ClientProvidedName
			};

			await using var conexao = await fabrica.CreateConnectionAsync(cancellationToken)
				.ConfigureAwait(false);
			await using var canal = await conexao.CreateChannelAsync(cancellationToken: cancellationToken)
				.ConfigureAwait(false);

			var limite = cronometro.Elapsed + Timeout;
			while (cronometro.Elapsed < limite)
			{
				cancellationToken.ThrowIfCancellationRequested();

				var entrega = await canal.BasicGetAsync(Queue, autoAck: true, cancellationToken)
					.ConfigureAwait(false);

				if (entrega != null)
				{
					Result = RabbitMqStepResult.CreateConsumeSuccess(
						destination: Queue,
						routingKey: entrega.RoutingKey,
						message: Encoding.UTF8.GetString(entrega.Body.Span),
						remainingMessageCount: entrega.MessageCount,
						headers: Cabecalhos(entrega.BasicProperties),
						elapsed: cronometro.Elapsed);

					IsValid = true;
					return Result;
				}

				await Task.Delay(IntervaloEntreTentativas, cancellationToken).ConfigureAwait(false);
			}

			Result = RabbitMqStepResult.CreateConsumeTimeout(Queue, Timeout, cronometro.Elapsed);
			IsValid = false;
			return Result;
		}
		catch (Exception ex)
		{
			Result = RabbitMqStepResult.CreateFailure(ex, Queue, diagnostico, cronometro.Elapsed);
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

	private static IReadOnlyDictionary<string, object?> Cabecalhos(IReadOnlyBasicProperties propriedades)
	{
		if (propriedades.Headers is null)
			return new Dictionary<string, object?>();

		return new Dictionary<string, object?>(propriedades.Headers);
	}

	private string ResolverUri(ITestContext context) =>
		ConnectionUriInformada
			? ConnectionUri
			: context.GetProperty<string>("_RabbitMqConnectionUri")
				?? RabbitMqSettings.Load().ConnectionUri;
}
