using System;

using Shouldly;

using XUnitAssured.Core.Abstractions;
using XUnitAssured.Core.Extensions;
using XUnitAssured.RabbitMq.Results;

namespace XUnitAssured.RabbitMq.Extensions;

/// <summary>
/// As asserções que uma cadeia de RabbitMQ devolve.
/// </summary>
public class RabbitMqValidationBuilder : ValidationBuilder<RabbitMqStepResult>
{
	/// <summary>
	/// Cria o construtor sobre o cenário que já executou.
	/// </summary>
	/// <param name="scenario">O cenário com o passo executado.</param>
	public RabbitMqValidationBuilder(ITestScenario scenario) : base(scenario)
	{
	}

	/// <inheritdoc cref="ValidationBuilder{TResult}.Then" />
	public new RabbitMqValidationBuilder Then()
	{
		base.Then();
		return this;
	}

	/// <inheritdoc cref="ValidationBuilder{TResult}.AssertSuccess" />
	public new RabbitMqValidationBuilder AssertSuccess()
	{
		base.AssertSuccess();
		return this;
	}

	/// <inheritdoc cref="ValidationBuilder{TResult}.AssertFailure" />
	public new RabbitMqValidationBuilder AssertFailure()
	{
		base.AssertFailure();
		return this;
	}

	/// <summary>Afirma que a mensagem consumida tem exatamente este corpo.</summary>
	public RabbitMqValidationBuilder AssertMessage(string expected)
	{
		Result.Message.ShouldBe(expected);
		return this;
	}

	/// <summary>Afirma sobre a mensagem consumida, desserializada no tipo pedido.</summary>
	public RabbitMqValidationBuilder AssertMessage<T>(Action<T> assertion) where T : class
	{
		if (assertion == null)
			throw new ArgumentNullException(nameof(assertion));

		var mensagem = Result.GetMessage<T>();
		mensagem.ShouldNotBeNull($"the message did not deserialize to {typeof(T).Name}");
		assertion(mensagem!);

		return this;
	}

	/// <summary>Afirma a routing key com que a mensagem chegou.</summary>
	public RabbitMqValidationBuilder AssertRoutingKey(string expected)
	{
		Result.RoutingKey.ShouldBe(expected);
		return this;
	}

	/// <summary>Afirma que um header AMQP veio com a mensagem.</summary>
	public RabbitMqValidationBuilder AssertHeader(string name)
	{
		Result.Headers.ContainsKey(name).ShouldBeTrue($"no header named '{name}' on the message");
		return this;
	}

	/// <summary>
	/// Afirma que a fila ficou vazia depois desta mensagem.
	/// </summary>
	/// <remarks>
	/// O número vem do broker no momento em que a mensagem foi retirada, então ele responde "o que
	/// sobrou para mim agora", e não "a fila está vazia para sempre". Num teste com um produtor só
	/// as duas coisas coincidem; com produção concorrente, não.
	/// </remarks>
	public RabbitMqValidationBuilder AssertQueueDrained()
	{
		Result.RemainingMessageCount.ShouldBe(0u,
			"the broker reported messages still queued after this one");

		return this;
	}
}
