using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using XUnitAssured.Core.Abstractions;
using XUnitAssured.RabbitMq.Extensions;
using XUnitAssured.RabbitMq.Results;

namespace XUnitAssured.RabbitMq.Abstractions;

/// <summary>
/// Uma cadeia que está descrevendo trabalho em RabbitMQ.
///
/// <para>
/// <b>Os verbos moram aqui, e não como extensão de <see cref="ITestScenario"/>.</b> É a convenção
/// que o README registra para pacote de protocolo novo, e ela existe por um motivo medido:
/// mensageria é um vocabulário compartilhado. <c>Consume</c>, <c>Publish</c>, <c>WithTimeout</c> e
/// <c>ValidateMessage</c> são palavras que qualquer broker quer, e o pacote Kafka já as declara
/// sobre <c>ITestScenario</c>. Declará-las aqui do mesmo jeito tornaria toda chamada ambígua num
/// projeto que referencie os dois, e referenciar vários pacotes numa cadeia é a tese do framework.
/// </para>
///
/// <para>
/// Com os verbos no tipo, o compilador escolhe pelo receptor e não há o que desambiguar. Só o
/// verbo de entrada, <c>Queue</c> ou <c>Exchange</c>, toma o cenário não tipado, e esses não
/// colidem porque cada broker nomeia a própria coisa: o Kafka tem <c>Topic</c>.
/// </para>
/// </summary>
public interface IRabbitMqScenario : ITestScenario
{
	/// <inheritdoc cref="ITestScenario.And" />
	new IRabbitMqScenario And();

	/// <inheritdoc cref="ITestScenario.On" />
	new IRabbitMqScenario On();

	/// <inheritdoc cref="ITestScenario.When" />
	new IRabbitMqScenario When();

	/// <inheritdoc cref="ITestScenario.Then" />
	new IRabbitMqScenario Then();

	/// <summary>
	/// Declara no broker a fila que a cadeia nomeou. Durável, porque o RabbitMQ 4 recusa fila
	/// transitória não exclusiva.
	/// </summary>
	/// <remarks>Declarar é idempotente: declarar de novo com os mesmos argumentos não é erro.</remarks>
	/// <param name="deadLetterExchange">
	/// A exchange para onde a fila manda o que foi recusado sem requeue, ou nulo para não ter.
	/// </param>
	IRabbitMqScenario DeclareQueue(string? deadLetterExchange = null);

	/// <summary>
	/// Declara no broker a exchange que a cadeia nomeou.
	/// </summary>
	/// <param name="type"><c>direct</c>, <c>topic</c>, <c>fanout</c> ou <c>headers</c>.</param>
	IRabbitMqScenario DeclareExchange(string type = "direct");

	/// <summary>
	/// Liga a fila que a cadeia nomeou a uma exchange, por uma routing key.
	/// </summary>
	/// <remarks>
	/// Sem binding, publicar na exchange não chega a fila nenhuma — e desde que a publicação vai
	/// com <c>mandatory</c>, isso é falha e não silêncio.
	/// </remarks>
	IRabbitMqScenario BindQueueTo(string exchange, string routingKey);

	/// <summary>
	/// Publica uma mensagem no destino que a cadeia nomeou.
	/// </summary>
	/// <param name="value">
	/// O corpo. Uma string vai como está; qualquer outro objeto é serializado em JSON.
	/// </param>
	IRabbitMqScenario Publish(object value);

	/// <summary>
	/// Retira uma mensagem da fila que a cadeia nomeou, esperando até o prazo.
	/// </summary>
	/// <remarks>
	/// Consumir é só de fila. Publicar aceita fila ou exchange, mas uma exchange não guarda nada:
	/// não há o que consumir de lá.
	/// </remarks>
	IRabbitMqScenario Consume();

	/// <summary>
	/// A routing key da publicação. Numa fila nomeada direto, é o próprio nome dela.
	/// </summary>
	IRabbitMqScenario WithRoutingKey(string routingKey);

	/// <summary>Acrescenta um header AMQP à mensagem publicada.</summary>
	IRabbitMqScenario WithHeader(string name, object? value);

	/// <summary>Substitui os headers AMQP da mensagem publicada.</summary>
	IRabbitMqScenario WithHeaders(IDictionary<string, object?> headers);

	/// <summary>
	/// Aceita que a publicação não chegue a fila nenhuma.
	/// </summary>
	/// <remarks>
	/// Por padrão a publicação falha quando o broker devolve a mensagem como não roteável, porque
	/// num teste uma mensagem que não chegou a lugar nenhum é um erro de topologia que passaria
	/// calado. Use isto quando publicar antes de existir binding é justamente o que o teste quer.
	/// </remarks>
	IRabbitMqScenario AllowingUnroutable();

	/// <summary>
	/// Recusa a mensagem em vez de confirmá-la.
	/// </summary>
	/// <param name="requeue">
	/// Se a mensagem volta para a fila. Falso, o padrão, é o que a manda para o dead-letter da fila
	/// quando há um configurado; verdadeiro é o que permite afirmar que um consumo seguinte a
	/// encontra de novo.
	/// </param>
	/// <remarks>
	/// O resultado continua trazendo a mensagem, então dá para afirmar sobre o que foi recusado.
	/// </remarks>
	IRabbitMqScenario Rejecting(bool requeue = false);

	/// <summary>Quanto esperar por uma mensagem ao consumir.</summary>
	IRabbitMqScenario WithTimeout(TimeSpan timeout);

	/// <summary>
	/// A URI de conexão, quando ela não vem do <c>testsettings.json</c> nem da fixture.
	/// </summary>
	IRabbitMqScenario WithConnectionUri(string connectionUri);

	/// <summary>Registra uma verificação sobre o resultado do passo.</summary>
	IRabbitMqScenario Validate(Action<RabbitMqStepResult> validation);

	/// <summary>
	/// Registra uma verificação sobre a mensagem consumida, desserializada no tipo pedido.
	/// </summary>
	IRabbitMqScenario ValidateMessage<T>(Action<T> validation) where T : class;

	/// <summary>Roda a cadeia e devolve as asserções.</summary>
	Task<RabbitMqValidationBuilder> ExecuteAsync(CancellationToken cancellationToken = default);

	/// <summary>
	/// Roda a cadeia e espera por ela.
	/// </summary>
	/// <remarks>
	/// Invólucro fino sobre <see cref="ExecuteAsync"/>, para uma suíte escrita na forma síncrona.
	/// </remarks>
	RabbitMqValidationBuilder Execute();
}
