using System;
using System.Collections.Generic;
using System.Text.Json;

using XUnitAssured.Core.Extensions;
using XUnitAssured.Core.Results;

namespace XUnitAssured.RabbitMq.Results;

/// <summary>
/// O que um passo de RabbitMQ deixa para ser verificado.
///
/// <para>
/// As propriedades existem sempre, ainda que vazias. É a mesma decisão de contrato que o pacote
/// Kafka tomou: uma propriedade de diagnóstico que só aparece em alguns casos obriga o teste a
/// afirmar sobre a presença dela em vez de sobre o comportamento, e isso é o que torna um teste
/// dependente de ambiente.
/// </para>
/// </summary>
public class RabbitMqStepResult : TestStepResult
{
	/// <summary>A fila ou exchange que o passo tocou.</summary>
	public string? Destination => GetProperty<string>("Destination");

	/// <summary>A routing key usada na publicação, ou a da mensagem consumida.</summary>
	public string? RoutingKey => GetProperty<string>("RoutingKey");

	/// <summary>O corpo da mensagem consumida, como texto.</summary>
	public string? Message => GetProperty<string>("Message");

	/// <summary>
	/// Quantas mensagens a fila ainda tinha quando esta foi retirada, como o broker informou.
	/// </summary>
	public uint? RemainingMessageCount => GetProperty<uint?>("RemainingMessageCount");

	/// <summary>
	/// As mensagens consumidas, na ordem em que chegaram. Um consumo simples traz uma.
	/// </summary>
	/// <remarks>
	/// Existe sempre, ainda que vazia, como as outras propriedades deste resultado.
	/// <see cref="Message"/> é a primeira delas, e continua sendo o jeito de ler um consumo
	/// simples sem pensar em coleção.
	/// </remarks>
	public IReadOnlyList<string> Messages =>
		GetProperty<IReadOnlyList<string>>("Messages") ?? Array.Empty<string>();

	/// <summary>Os headers AMQP da mensagem consumida.</summary>
	public IReadOnlyDictionary<string, object?> Headers =>
		GetProperty<IReadOnlyDictionary<string, object?>>("Headers")
			?? new Dictionary<string, object?>();

	/// <summary>
	/// Desserializa a mensagem consumida no tipo pedido.
	/// </summary>
	/// <exception cref="InvalidOperationException">Quando não há mensagem.</exception>
	public T? GetMessage<T>() where T : class
	{
		if (string.IsNullOrWhiteSpace(Message))
			throw new InvalidOperationException(
				"No message on this result. Either the step published instead of consuming, or the consume timed out.");

		if (typeof(T) == typeof(string))
			return Message as T;

		return JsonSerializer.Deserialize<T>(Message!, new JsonSerializerOptions
		{
			PropertyNameCaseInsensitive = true
		});
	}

	/// <summary>
	/// Lê um valor da mensagem consumida por caminho JSON, por exemplo <c>$.pedido.id</c>.
	/// </summary>
	/// <remarks>
	/// O prefixo <c>$.</c> é opcional. Usa o mesmo navegador que os pacotes Http e Kafka, que mora
	/// no Core justamente para não haver três interpretações de caminho divergindo com o tempo.
	/// </remarks>
	/// <exception cref="InvalidOperationException">Quando não há mensagem.</exception>
	public T JsonPath<T>(string path)
	{
		if (string.IsNullOrWhiteSpace(Message))
			throw new InvalidOperationException(
				"No message on this result. Either the step published instead of consuming, or the consume timed out.");

		using var documento = JsonDocument.Parse(Message!);
		var raiz = documento.RootElement;

		if (path.StartsWith("$."))
			path = path.Substring(2);

		return JsonPathNavigator.Navigate<T>(raiz, path);
	}

	/// <summary>Resultado de uma publicação que o broker aceitou.</summary>
	public static RabbitMqStepResult CreatePublishSuccess(
		string destination, string routingKey, TimeSpan? elapsed = null) =>
		new()
		{
			Success = true,
			Errors = Array.Empty<string>(),
			DataType = typeof(string),
			Metadata = Metadados(StepStatus.Succeeded, elapsed),
			Properties = new Dictionary<string, object?>
			{
				["Destination"] = destination,
				["RoutingKey"] = routingKey,
				["Message"] = null,
				["Messages"] = (IReadOnlyList<string>)Array.Empty<string>(),
				["RemainingMessageCount"] = null,
				["Headers"] = (IReadOnlyDictionary<string, object?>)new Dictionary<string, object?>()
			}
		};

	/// <summary>Resultado de um consumo que trouxe mensagem.</summary>
	public static RabbitMqStepResult CreateConsumeSuccess(
		string destination,
		string routingKey,
		string message,
		uint remainingMessageCount,
		IReadOnlyDictionary<string, object?>? headers = null,
		TimeSpan? elapsed = null) =>
		new()
		{
			Success = true,
			Errors = Array.Empty<string>(),
			Data = message,
			DataType = typeof(string),
			Metadata = Metadados(StepStatus.Succeeded, elapsed),
			Properties = new Dictionary<string, object?>
			{
				["Destination"] = destination,
				["RoutingKey"] = routingKey,
				["Message"] = message,
				["Messages"] = (IReadOnlyList<string>)new[] { message },
				["RemainingMessageCount"] = remainingMessageCount,
				["Headers"] = headers ?? new Dictionary<string, object?>()
			}
		};

	/// <summary>Resultado de um consumo em lote que trouxe pelo menos uma mensagem.</summary>
	/// <remarks>
	/// Trazer menos do que se pediu é sucesso: o passo consumiu o que havia dentro do prazo, e
	/// quantas havia é justamente o que o teste quer afirmar com <c>AssertMessageCount</c>.
	/// </remarks>
	public static RabbitMqStepResult CreateBatchConsumeSuccess(
		string destination,
		IReadOnlyList<string> messages,
		uint remainingMessageCount,
		TimeSpan? elapsed = null) =>
		new()
		{
			Success = true,
			Errors = Array.Empty<string>(),
			Data = messages.Count > 0 ? messages[0] : null,
			DataType = typeof(string),
			Metadata = Metadados(StepStatus.Succeeded, elapsed),
			Properties = new Dictionary<string, object?>
			{
				["Destination"] = destination,
				["RoutingKey"] = null,
				["Message"] = messages.Count > 0 ? messages[0] : null,
				["Messages"] = messages,
				["RemainingMessageCount"] = remainingMessageCount,
				["Headers"] = (IReadOnlyDictionary<string, object?>)new Dictionary<string, object?>()
			}
		};

	/// <summary>
	/// Resultado de um consumo que esgotou o prazo sem mensagem.
	/// </summary>
	/// <remarks>
	/// É falha, e não sucesso com mensagem vazia: o passo pediu uma mensagem e não houve. Dizer o
	/// contrário faria um teste passar sem ter exercido nada.
	/// </remarks>
	public static RabbitMqStepResult CreateConsumeTimeout(
		string destination, TimeSpan timeout, TimeSpan? elapsed = null) =>
		new()
		{
			Success = false,
			Errors = new[]
			{
				$"No message arrived on '{destination}' within {timeout.TotalSeconds:0.##}s."
			},
			DataType = typeof(string),
			Metadata = Metadados(StepStatus.Failed, elapsed),
			Properties = new Dictionary<string, object?>
			{
				["Destination"] = destination,
				["RoutingKey"] = null,
				["Message"] = null,
				["Messages"] = (IReadOnlyList<string>)Array.Empty<string>(),
				["RemainingMessageCount"] = null,
				["Headers"] = (IReadOnlyDictionary<string, object?>)new Dictionary<string, object?>()
			}
		};

	/// <summary>
	/// Resultado de uma falha de transporte ou de protocolo, com o diagnóstico que o passo juntou.
	/// </summary>
	public static RabbitMqStepResult CreateFailure(
		Exception exception,
		string? destination = null,
		IDictionary<string, object?>? diagnostics = null,
		TimeSpan? elapsed = null)
	{
		if (exception == null)
			throw new ArgumentNullException(nameof(exception));

		var propriedades = new Dictionary<string, object?>
		{
			["Destination"] = destination,
			["RoutingKey"] = null,
			["Message"] = null,
			["Messages"] = (IReadOnlyList<string>)Array.Empty<string>(),
			["RemainingMessageCount"] = null,
			["Headers"] = (IReadOnlyDictionary<string, object?>)new Dictionary<string, object?>(),
			["ExceptionType"] = exception.GetType().FullName,
			["ExceptionDetail"] = exception.ToString()
		};

		if (diagnostics != null)
		{
			foreach (var par in diagnostics)
				propriedades[par.Key] = par.Value;
		}

		return new RabbitMqStepResult
		{
			Success = false,
			Errors = new[] { exception.Message },
			DataType = typeof(string),
			Metadata = Metadados(StepStatus.Failed, elapsed),
			Properties = propriedades
		};
	}

	private static StepMetadata Metadados(StepStatus status, TimeSpan? elapsed) => new()
	{
		StartedAt = DateTimeOffset.UtcNow - (elapsed ?? TimeSpan.Zero),
		CompletedAt = DateTimeOffset.UtcNow,
		Status = status
	};
}
