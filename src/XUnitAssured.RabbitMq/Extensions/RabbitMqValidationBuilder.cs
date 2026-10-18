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
		ArgumentNullException.ThrowIfNull(assertion);

		var mensagem = Result.GetMessage<T>();
		mensagem.ShouldNotBeNull($"the message did not deserialize to {typeof(T).Name}");
		assertion(mensagem!);

		return this;
	}

	/// <summary>Afirma quantas mensagens o lote trouxe.</summary>
	public RabbitMqValidationBuilder AssertMessageCount(int expected)
	{
		Result.Messages.Count.ShouldBe(expected,
			$"the batch brought {Result.Messages.Count} message(s) within the timeout");

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
	/// Afirma que um header AMQP veio com a mensagem e tem este valor.
	/// </summary>
	/// <remarks>
	/// Um header de texto chega do RabbitMQ.Client como <c>byte[]</c>: ele é lido como UTF-8. Outro
	/// tipo é comparado pelo texto dele.
	/// </remarks>
	public RabbitMqValidationBuilder AssertHeader(string name, string expected)
	{
		Result.Headers.TryGetValue(name, out var valor).ShouldBeTrue($"no header named '{name}' on the message");
		var texto = valor switch
		{
			byte[] bytes => System.Text.Encoding.UTF8.GetString(bytes),
			null => null,
			_ => Convert.ToString(valor, System.Globalization.CultureInfo.InvariantCulture),
		};
		texto.ShouldBe(expected, $"the header '{name}' should be '{expected}'");
		return this;
	}

	/// <summary>
	/// Afirma sobre um valor da mensagem consumida, lido por caminho JSON, como nos pacotes Http e Kafka.
	/// </summary>
	/// <example>
	/// <code>
	/// .AssertJsonPath&lt;string&gt;("$.status", status =&gt; status == "pago", "the order should be paid")
	/// </code>
	/// </example>
	public RabbitMqValidationBuilder AssertJsonPath<T>(string jsonPath, Func<T, bool> predicate, string? failureMessage = null)
	{
		ArgumentNullException.ThrowIfNull(predicate);

		var valor = Result.JsonPath<T>(jsonPath);
		predicate(valor).ShouldBeTrue(failureMessage ?? $"the value at {jsonPath} did not satisfy the predicate (it was {valor})");
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
