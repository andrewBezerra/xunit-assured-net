using System;
using System.Collections.Generic;
using System.Text.Json;

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

	// Falta aqui um JsonPath<T>(caminho), como o KafkaStepResult tem. Ele depende do
	// JsonPathNavigator ter subido para o Core, que é a #56 e ainda não entrou; empilhar os dois
	// numa revisão só atrapalharia a leitura. Entra em seguida.

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
				["RemainingMessageCount"] = remainingMessageCount,
				["Headers"] = headers ?? new Dictionary<string, object?>()
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
