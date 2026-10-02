using System;
using System.Collections.Generic;

using XUnitAssured.Core.Abstractions;
using XUnitAssured.RabbitMq.Abstractions;
using XUnitAssured.RabbitMq.DSL;
using XUnitAssured.RabbitMq.Results;
using XUnitAssured.RabbitMq.Steps;

namespace XUnitAssured.RabbitMq.Extensions;

/// <summary>
/// Os verbos de entrada, e os únicos que tomam o cenário não tipado.
///
/// <para>
/// A convenção está escrita no README: um pacote de protocolo declara seus verbos sobre o próprio
/// cenário tipado, e só o verbo de entrada toma <see cref="ITestScenario"/>. <c>Queue</c> e
/// <c>Exchange</c> são nomes próprios do AMQP e não colidem com o <c>Topic</c> do Kafka, então um
/// projeto pode referenciar os dois pacotes e escrever as duas cadeias no mesmo arquivo.
/// </para>
/// </summary>
public static class RabbitMqScenarioExtensions
{
	/// <summary>
	/// Nomeia a fila com que a cadeia vai trabalhar.
	/// </summary>
	/// <remarks>
	/// Publicar numa fila é publicar na exchange padrão com a routing key igual ao nome dela, que
	/// é como o AMQP entrega "mandar direto para a fila". Consumir é só de fila.
	/// </remarks>
	public static IRabbitMqScenario Queue(this ITestScenario scenario, string name)
	{
		if (scenario == null) throw new ArgumentNullException(nameof(scenario));
		if (string.IsNullOrWhiteSpace(name))
			throw new ArgumentException("Queue name is required.", nameof(name));

		scenario.Context.SetProperty("_RabbitMqQueue", name);
		scenario.Context.SetProperty("_RabbitMqExchange", string.Empty);

		return RabbitMqScenario.De(scenario);
	}

	/// <summary>
	/// Nomeia a exchange com que a cadeia vai trabalhar.
	/// </summary>
	/// <remarks>
	/// Uma exchange roteia e não guarda, então não há o que consumir dela: use
	/// <see cref="Queue"/> para o lado do consumo. A routing key vem de
	/// <c>WithRoutingKey</c>, e sem ela a publicação sai com routing key vazia, que é o que uma
	/// exchange do tipo fanout espera.
	/// </remarks>
	public static IRabbitMqScenario Exchange(this ITestScenario scenario, string name)
	{
		if (scenario == null) throw new ArgumentNullException(nameof(scenario));
		if (name == null) throw new ArgumentNullException(nameof(name));

		scenario.Context.SetProperty("_RabbitMqExchange", name);
		scenario.Context.SetProperty("_RabbitMqQueue", string.Empty);

		return RabbitMqScenario.De(scenario);
	}
}

/// <summary>
/// O corpo dos verbos do cenário tipado.
/// </summary>
/// <remarks>
/// Fica em classe interna própria porque os verbos são métodos do
/// <see cref="IRabbitMqScenario"/>, e não extensões: o invólucro delega para cá em vez de
/// duplicar a lógica. Deixá-los como extensão pública sobre <c>ITestScenario</c> é exatamente o
/// que a convenção do README existe para evitar.
/// </remarks>
internal static class RabbitMqVerbos
{
	internal static IRabbitMqScenario DeclareQueue(ITestScenario scenario, string? deadLetterExchange)
	{
		var fila = scenario.Context.GetProperty<string>("_RabbitMqQueue");

		if (string.IsNullOrWhiteSpace(fila))
			throw new InvalidOperationException(
				"No queue to declare. Call Queue(name) first.");

		scenario.SetCurrentStep(new RabbitMqTopologyStep
		{
			Operation = RabbitMqTopologyOperation.DeclareQueue,
			Queue = fila!,
			DeadLetterExchange = deadLetterExchange
		});

		return RabbitMqScenario.De(scenario);
	}

	internal static IRabbitMqScenario DeclareExchange(ITestScenario scenario, string type)
	{
		if (string.IsNullOrWhiteSpace(type))
			throw new ArgumentException("Exchange type is required.", nameof(type));

		var exchange = scenario.Context.GetProperty<string>("_RabbitMqExchange");

		if (string.IsNullOrWhiteSpace(exchange))
			throw new InvalidOperationException(
				"No exchange to declare. Call Exchange(name) first.");

		scenario.SetCurrentStep(new RabbitMqTopologyStep
		{
			Operation = RabbitMqTopologyOperation.DeclareExchange,
			Exchange = exchange!,
			ExchangeType = type
		});

		return RabbitMqScenario.De(scenario);
	}

	internal static IRabbitMqScenario BindQueueTo(
		ITestScenario scenario, string exchange, string routingKey)
	{
		if (string.IsNullOrWhiteSpace(exchange))
			throw new ArgumentException("Exchange name is required.", nameof(exchange));
		if (routingKey == null)
			throw new ArgumentNullException(nameof(routingKey));

		var fila = scenario.Context.GetProperty<string>("_RabbitMqQueue");

		if (string.IsNullOrWhiteSpace(fila))
			throw new InvalidOperationException(
				"No queue to bind. Call Queue(name) first; a binding joins a queue to an exchange.");

		scenario.SetCurrentStep(new RabbitMqTopologyStep
		{
			Operation = RabbitMqTopologyOperation.BindQueue,
			Queue = fila!,
			Exchange = exchange,
			RoutingKey = routingKey
		});

		return RabbitMqScenario.De(scenario);
	}

	internal static IRabbitMqScenario Publish(ITestScenario scenario, object value)
	{
		var exchange = scenario.Context.GetProperty<string>("_RabbitMqExchange") ?? string.Empty;
		var fila = scenario.Context.GetProperty<string>("_RabbitMqQueue") ?? string.Empty;

		// Inalcançável pela DSL pública: `Publish` é membro de IRabbitMqScenario, e só os verbos de
		// entrada produzem esse tipo, então chamar fora de ordem é erro de compilação. Fica como
		// rede para um cenário reaproveitado entre cadeias, onde o contexto pode não ter destino.
		if (string.IsNullOrEmpty(exchange) && string.IsNullOrEmpty(fila))
			throw new InvalidOperationException(
				"No destination. Call Queue(name) or Exchange(name) before Publish.");

		scenario.SetCurrentStep(new RabbitMqPublishStep
		{
			Exchange = exchange,
			RoutingKey = fila,
			Value = value
		});

		return RabbitMqScenario.De(scenario);
	}

	internal static IRabbitMqScenario Consume(ITestScenario scenario)
	{
		var fila = scenario.Context.GetProperty<string>("_RabbitMqQueue");

		if (string.IsNullOrWhiteSpace(fila))
			throw new InvalidOperationException(
				"No queue to consume from. Call Queue(name) first; an exchange holds nothing to consume.");

		scenario.SetCurrentStep(new RabbitMqConsumeStep
		{
			Queue = fila!,
			Timeout = TimeSpan.FromSeconds(RabbitMqSettings.Load().ConsumeTimeoutSeconds)
		});

		return RabbitMqScenario.De(scenario);
	}

	internal static IRabbitMqScenario ConsumeBatch(ITestScenario scenario, int count)
	{
		if (count <= 0)
			throw new ArgumentOutOfRangeException(nameof(count), "Message count must be greater than zero.");

		var fila = scenario.Context.GetProperty<string>("_RabbitMqQueue");

		if (string.IsNullOrWhiteSpace(fila))
			throw new InvalidOperationException(
				"No queue to consume from. Call Queue(name) first; an exchange holds nothing to consume.");

		scenario.SetCurrentStep(new RabbitMqConsumeStep
		{
			Queue = fila!,
			MessageCount = count,
			Timeout = TimeSpan.FromSeconds(RabbitMqSettings.Load().ConsumeTimeoutSeconds)
		});

		return RabbitMqScenario.De(scenario);
	}

	internal static IRabbitMqScenario WithRoutingKey(ITestScenario scenario, string routingKey)
	{
		if (routingKey == null) throw new ArgumentNullException(nameof(routingKey));

		if (scenario.CurrentStep is RabbitMqPublishStep publicacao)
			scenario.SetCurrentStep(new RabbitMqPublishStep(publicacao) { RoutingKey = routingKey });
		else
			throw new InvalidOperationException(
				"A routing key only applies to a publish step. Call Publish before WithRoutingKey.");

		return RabbitMqScenario.De(scenario);
	}

	internal static IRabbitMqScenario WithHeader(ITestScenario scenario, string name, object? value)
	{
		if (string.IsNullOrWhiteSpace(name))
			throw new ArgumentException("Header name is required.", nameof(name));

		if (scenario.CurrentStep is not RabbitMqPublishStep publicacao)
			throw new InvalidOperationException(
				"Headers only apply to a publish step. Call Publish before WithHeader.");

		var cabecalhos = publicacao.Headers is null
			? new Dictionary<string, object?>()
			: new Dictionary<string, object?>(publicacao.Headers);

		cabecalhos[name] = value;
		scenario.SetCurrentStep(new RabbitMqPublishStep(publicacao) { Headers = cabecalhos });

		return RabbitMqScenario.De(scenario);
	}

	internal static IRabbitMqScenario WithHeaders(
		ITestScenario scenario, IDictionary<string, object?> headers)
	{
		if (headers == null) throw new ArgumentNullException(nameof(headers));

		if (scenario.CurrentStep is not RabbitMqPublishStep publicacao)
			throw new InvalidOperationException(
				"Headers only apply to a publish step. Call Publish before WithHeaders.");

		scenario.SetCurrentStep(new RabbitMqPublishStep(publicacao)
		{
			Headers = new Dictionary<string, object?>(headers)
		});

		return RabbitMqScenario.De(scenario);
	}

	internal static IRabbitMqScenario AllowingUnroutable(ITestScenario scenario)
	{
		if (scenario.CurrentStep is not RabbitMqPublishStep publicacao)
			throw new InvalidOperationException(
				"Routing only applies to a publish step. Call Publish before AllowingUnroutable.");

		scenario.SetCurrentStep(new RabbitMqPublishStep(publicacao) { RequireRouting = false });

		return RabbitMqScenario.De(scenario);
	}

	internal static IRabbitMqScenario Rejecting(ITestScenario scenario, bool requeue)
	{
		if (scenario.CurrentStep is not RabbitMqConsumeStep consumo)
			throw new InvalidOperationException(
				"Only a consumed message can be rejected. Call Consume before Rejecting.");

		scenario.SetCurrentStep(new RabbitMqConsumeStep(consumo)
		{
			RejectMessage = true,
			RequeueRejected = requeue
		});

		return RabbitMqScenario.De(scenario);
	}

	internal static IRabbitMqScenario WithTimeout(ITestScenario scenario, TimeSpan timeout)
	{
		if (scenario.CurrentStep is RabbitMqConsumeStep consumo)
			scenario.SetCurrentStep(new RabbitMqConsumeStep(consumo) { Timeout = timeout });
		else
			throw new InvalidOperationException(
				"A timeout only applies to a consume step: publishing does not wait for anything.");

		return RabbitMqScenario.De(scenario);
	}

	internal static IRabbitMqScenario WithConnectionUri(ITestScenario scenario, string connectionUri)
	{
		if (string.IsNullOrWhiteSpace(connectionUri))
			throw new ArgumentException("Connection URI is required.", nameof(connectionUri));

		switch (scenario.CurrentStep)
		{
			case RabbitMqPublishStep publicacao:
				scenario.SetCurrentStep(new RabbitMqPublishStep(publicacao) { ConnectionUri = connectionUri });
				break;
			case RabbitMqConsumeStep consumo:
				scenario.SetCurrentStep(new RabbitMqConsumeStep(consumo) { ConnectionUri = connectionUri });
				break;
			default:
				// Antes de Publish ou Consume não há passo para configurar, então o valor vai para
				// o contexto e o passo o encontra quando nascer.
				scenario.Context.SetProperty("_RabbitMqConnectionUri", connectionUri);
				break;
		}

		return RabbitMqScenario.De(scenario);
	}

	internal static IRabbitMqScenario Validate(
		ITestScenario scenario, Action<RabbitMqStepResult> validation)
	{
		if (validation == null) throw new ArgumentNullException(nameof(validation));

		scenario.AddValidation(resultado =>
		{
			if (resultado is not RabbitMqStepResult rabbit)
				throw new InvalidOperationException(
					$"Expected a RabbitMqStepResult but got {resultado.GetType().Name}.");

			validation(rabbit);
		});

		return RabbitMqScenario.De(scenario);
	}

	internal static IRabbitMqScenario ValidateMessage<T>(
		ITestScenario scenario, Action<T> validation) where T : class
	{
		if (validation == null) throw new ArgumentNullException(nameof(validation));

		return Validate(scenario, resultado =>
		{
			var mensagem = resultado.GetMessage<T>();
			if (mensagem is null)
				throw new InvalidOperationException(
					$"The message did not deserialize to {typeof(T).Name}.");

			validation(mensagem);
		});
	}
}
