using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using RabbitMQ.Client;

using XUnitAssured.Core.Abstractions;
using XUnitAssured.Core.Results;
using XUnitAssured.RabbitMq.Results;

namespace XUnitAssured.RabbitMq.Steps;

/// <summary>
/// Publica uma mensagem numa fila ou exchange.
/// </summary>
/// <remarks>
/// <para>
/// A conexão é aberta e fechada no passo. Um teste publica uma ou poucas mensagens, e manter
/// conexão viva entre passos trocaria simplicidade por um ganho que não aparece nessa escala. A
/// fixture é quem compartilha conexão quando vale.
/// </para>
/// <para>
/// O cliente oficial na versão 7 é assíncrono de ponta a ponta, então aqui o <c>await</c> é de
/// verdade: nada de laço devolvendo a thread, que foi o que o lado Kafka precisou fazer porque o
/// <c>IConsumer</c> não tem consumo assíncrono.
/// </para>
/// </remarks>
public class RabbitMqPublishStep : ITestStep
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

	/// <summary>A exchange de destino. Vazio publica na fila nomeada pela routing key.</summary>
	public string Exchange { get; init; } = string.Empty;

	/// <summary>A routing key. Com a exchange vazia, é o nome da fila.</summary>
	public string RoutingKey { get; init; } = string.Empty;

	/// <summary>O corpo. String vai como está; outro objeto é serializado em JSON.</summary>
	public object? Value { get; init; }

	/// <summary>Headers AMQP da mensagem.</summary>
	public IDictionary<string, object?>? Headers { get; init; }

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
	/// Se quem montou o passo informou a URI, em vez de ela por acaso ser igual ao padrão. A
	/// ausência é registrada e não deduzida por comparação, que foi o defeito do lado Kafka.
	/// </summary>
	internal bool ConnectionUriInformada => _connectionUri is not null;

	/// <summary>
	/// Se a publicação exige que a mensagem chegue a pelo menos uma fila. Padrão: sim.
	/// </summary>
	/// <remarks>
	/// O AMQP tem um caso que o Kafka não tem: publicar numa exchange que não casa nenhuma
	/// binding. O broker aceita a publicação e descarta a mensagem, então o passo "deu certo" e
	/// nada chegou. Num teste isso é o pior resultado possível, porque é um erro de topologia que
	/// passa calado. Com isto ligado, a publicação vai com <c>mandatory</c> e o passo falha quando
	/// o broker devolve a mensagem.
	/// <para>
	/// Desligue quando o teste publica de propósito antes de existir binding, com
	/// <c>AllowingUnroutable()</c>.
	/// </para>
	/// </remarks>
	public bool RequireRouting { get; init; } = true;

	/// <summary>Opções de serialização do corpo, quando ele não é string.</summary>
	public JsonSerializerOptions? JsonOptions { get; init; }

	/// <summary>
	/// Cria um passo com os valores padrão, para ser preenchido por um inicializador.
	/// </summary>
	public RabbitMqPublishStep()
	{
	}

	/// <summary>
	/// Cria uma cópia do passo, carregando tudo o que estava configurado.
	/// </summary>
	/// <remarks>
	/// Copiar por aqui é o que faz uma propriedade nova ser carregada por padrão. Reconstruir o
	/// passo campo por campo em cada verbo é como configuração se perde em silêncio, e já custou
	/// ao lado Kafka a autenticação em cinco verbos. O campo de apoio da URI é copiado, e não a
	/// propriedade: copiar a propriedade gravaria o padrão e marcaria como informado.
	/// </remarks>
	public RabbitMqPublishStep(RabbitMqPublishStep source)
	{
		if (source == null)
			throw new ArgumentNullException(nameof(source));

		Name = source.Name;
		Exchange = source.Exchange;
		RoutingKey = source.RoutingKey;
		Value = source.Value;
		Headers = source.Headers;
		_connectionUri = source._connectionUri;
		RequireRouting = source.RequireRouting;
		JsonOptions = source.JsonOptions;
	}

	/// <inheritdoc />
	public async Task<ITestStepResult> ExecuteAsync(
		ITestContext context, CancellationToken cancellationToken = default)
	{
		var cronometro = Stopwatch.StartNew();
		var destino = string.IsNullOrEmpty(Exchange) ? RoutingKey : Exchange;
		var diagnostico = new Dictionary<string, object?>();

		try
		{
			var uri = ResolverUri(context);
			diagnostico["ConnectionUri"] = Mascarar(uri);
			diagnostico["Exchange"] = Exchange;
			diagnostico["RoutingKey"] = RoutingKey;

			var fabrica = new ConnectionFactory
			{
				Uri = new Uri(uri),
				ClientProvidedName = RabbitMqSettings.Load().ClientProvidedName
			};

			await using var conexao = await fabrica.CreateConnectionAsync(cancellationToken)
				.ConfigureAwait(false);
			// Confirmação do publicador ligada: sem ela o await da publicação volta assim que o byte
			// sai, e um retorno por mensagem não roteável chegaria depois de o passo já ter dito que
			// deu certo. Com ela, o await espera o broker responder.
			await using var canal = await conexao.CreateChannelAsync(
					new CreateChannelOptions(
						publisherConfirmationsEnabled: true,
						publisherConfirmationTrackingEnabled: true),
					cancellationToken)
				.ConfigureAwait(false);

			string? devolvida = null;
			canal.BasicReturnAsync += (_, argumentos) =>
			{
				devolvida = $"{argumentos.ReplyCode} {argumentos.ReplyText}";
				return Task.CompletedTask;
			};

			var propriedades = new BasicProperties();
			if (Headers is { Count: > 0 })
			{
				propriedades.Headers = new Dictionary<string, object?>(Headers);
			}

			await canal.BasicPublishAsync(
					exchange: Exchange,
					routingKey: RoutingKey,
					mandatory: RequireRouting,
					basicProperties: propriedades,
					body: CorpoEmBytes(),
					cancellationToken: cancellationToken)
				.ConfigureAwait(false);

			if (devolvida is not null)
			{
				diagnostico["BasicReturn"] = devolvida;
				Result = RabbitMqStepResult.CreateFailure(
					new InvalidOperationException(
						$"The broker returned the message as unroutable: {devolvida}. Nothing on " +
						$"'{destino}' matched routing key '{RoutingKey}'. Declare the binding, or call " +
						"AllowingUnroutable() when that is the point of the test."),
					destino, diagnostico, cronometro.Elapsed);
				IsValid = false;
				return Result;
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

	private ReadOnlyMemory<byte> CorpoEmBytes() => Value switch
	{
		null => ReadOnlyMemory<byte>.Empty,
		string texto => Encoding.UTF8.GetBytes(texto),
		_ => JsonSerializer.SerializeToUtf8Bytes(Value, JsonOptions)
	};

	private string ResolverUri(ITestContext context) =>
		ConnectionUriInformada
			? ConnectionUri
			: context.GetProperty<string>("_RabbitMqConnectionUri")
				?? RabbitMqSettings.Load().ConnectionUri;

	/// <summary>
	/// A URI carrega usuário e senha, e o diagnóstico vai para o resultado do teste, que costuma
	/// acabar num log. A credencial sai antes.
	/// </summary>
	internal static string Mascarar(string uri)
	{
		if (!Uri.TryCreate(uri, UriKind.Absolute, out var analisada))
			return "(uri inválida)";

		var autoridade = string.IsNullOrEmpty(analisada.UserInfo)
			? analisada.Host
			: $"***@{analisada.Host}";

		return $"{analisada.Scheme}://{autoridade}:{analisada.Port}{analisada.AbsolutePath}";
	}
}
