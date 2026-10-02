using System;
using System.Collections.Generic;

using Shouldly;
using Xunit;

using XUnitAssured.Core.DSL;
using XUnitAssured.RabbitMq.Abstractions;
using XUnitAssured.RabbitMq.Extensions;
using XUnitAssured.RabbitMq.Steps;

namespace XUnitAssured.Tests.RabbitMqTests;

[Trait("Category", "RabbitMq")]
[Trait("Component", "DSL")]
/// <summary>
/// O que a cadeia registra, que é o que o passo vai executar.
///
/// <para>
/// Nada aqui executa: a 6.0.0 fez a cadeia só descrever, e é essa separação que permite verificar
/// a montagem sem broker nenhum. O que se afirma é o passo que ficou no cenário, e a mensagem que
/// o DSL dá quando o verbo é chamado fora de ordem.
/// </para>
/// </summary>
public class VerbosDoRabbitMqTests
{
	[Fact(DisplayName = "Queue then Publish should record a publish to the default exchange")]
	public void Queue_Then_Publish_Should_Record_A_Publish()
	{
		var cenario = ScenarioDsl.Given().Queue("pedidos").Publish(new { id = 1 });

		var passo = cenario.CurrentStep.ShouldBeOfType<RabbitMqPublishStep>();
		passo.Exchange.ShouldBe(string.Empty, "publishing to a queue goes through the default exchange");
		passo.RoutingKey.ShouldBe("pedidos", "and the routing key is the queue's own name");
	}

	[Fact(DisplayName = "Exchange then Publish should record the exchange")]
	public void Exchange_Then_Publish_Should_Record_The_Exchange()
	{
		var cenario = ScenarioDsl.Given()
			.Exchange("eventos")
			.Publish("oi")
			.WithRoutingKey("pedidos.criado");

		var passo = cenario.CurrentStep.ShouldBeOfType<RabbitMqPublishStep>();
		passo.Exchange.ShouldBe("eventos");
		passo.RoutingKey.ShouldBe("pedidos.criado");
	}

	[Fact(DisplayName = "Queue then Consume should record a consume from that queue")]
	public void Queue_Then_Consume_Should_Record_A_Consume()
	{
		var cenario = ScenarioDsl.Given().Queue("pedidos").Consume();

		cenario.CurrentStep.ShouldBeOfType<RabbitMqConsumeStep>().Queue.ShouldBe("pedidos");
	}

	// Verbo acrescenta, não substitui: dois headers têm de sobreviver juntos. É o erro que a
	// reconstrução campo por campo comete com mais facilidade.
	[Fact(DisplayName = "Headers should accumulate instead of replacing each other")]
	public void Headers_Should_Accumulate()
	{
		var cenario = ScenarioDsl.Given()
			.Queue("pedidos")
			.Publish("oi")
			.WithHeader("origem", "checkout")
			.WithHeader("tentativa", 2);

		var passo = cenario.CurrentStep.ShouldBeOfType<RabbitMqPublishStep>();
		passo.Headers.ShouldNotBeNull();
		passo.Headers!.Count.ShouldBe(2);
		passo.Headers["origem"].ShouldBe("checkout");
		passo.Headers["tentativa"].ShouldBe(2);
	}

	// A reconstrução do passo tem de preservar o resto. É a lição que custou ao lado Kafka a
	// autenticação em cinco verbos.
	[Fact(DisplayName = "A later verb should preserve what earlier verbs configured")]
	public void A_Later_Verb_Should_Preserve_Earlier_Configuration()
	{
		var cenario = ScenarioDsl.Given()
			.Queue("pedidos")
			.Publish("corpo")
			.WithHeader("origem", "checkout")
			.WithRoutingKey("outra-fila");

		var passo = cenario.CurrentStep.ShouldBeOfType<RabbitMqPublishStep>();
		passo.RoutingKey.ShouldBe("outra-fila");
		passo.Value.ShouldBe("corpo", "the body survived the rebuild");
		passo.Headers!["origem"].ShouldBe("checkout", "and so did the header");
	}

	[Fact(DisplayName = "Every verb should keep the chain typed as a RabbitMQ scenario")]
	public void Every_Verb_Should_Keep_The_Chain_Typed()
	{
		var publicando = ScenarioDsl.Given().Queue("pedidos").Publish("oi");

		publicando.WithRoutingKey("k").ShouldBeAssignableTo<IRabbitMqScenario>();
		publicando.WithHeader("a", 1).ShouldBeAssignableTo<IRabbitMqScenario>();
		publicando.WithHeaders(new Dictionary<string, object?>()).ShouldBeAssignableTo<IRabbitMqScenario>();
		publicando.Validate(_ => { }).ShouldBeAssignableTo<IRabbitMqScenario>();

		ScenarioDsl.Given().Queue("pedidos").Consume()
			.WithTimeout(TimeSpan.FromSeconds(1)).ShouldBeAssignableTo<IRabbitMqScenario>();
	}

	// Não há teste para "publicar sem destino" porque a DSL pública não permite escrever isso:
	// `Publish` é membro de `IRabbitMqScenario`, e só `Queue` ou `Exchange` produzem esse tipo, de
	// modo que a chamada fora de ordem é erro de compilação. É garantia mais forte do que uma
	// exceção, e foi a tentativa de escrever esse teste que a tornou visível.

	// Uma exchange roteia e não guarda, então consumir dela não tem significado. Dizer isso é
	// melhor do que falhar na conexão por um motivo que parece outro.
	[Fact(DisplayName = "Consuming from an exchange should say an exchange holds nothing")]
	public void Consuming_From_An_Exchange_Should_Say_So()
	{
		var erro = Should.Throw<InvalidOperationException>(
			() => ScenarioDsl.Given().Exchange("eventos").Consume());

		erro.Message.Contains("queue").ShouldBeTrue();
	}

	[Fact(DisplayName = "A timeout on a publish step should say publishing does not wait")]
	public void A_Timeout_On_A_Publish_Should_Say_Publishing_Does_Not_Wait()
	{
		var erro = Should.Throw<InvalidOperationException>(
			() => ScenarioDsl.Given().Queue("pedidos").Publish("oi").WithTimeout(TimeSpan.FromSeconds(1)));

		erro.Message.Contains("consume").ShouldBeTrue();
	}

	[Fact(DisplayName = "DeclareQueue should record a queue declaration")]
	public void DeclareQueue_Should_Record_A_Declaration()
	{
		var cenario = ScenarioDsl.Given().Queue("pedidos").DeclareQueue();

		var passo = cenario.CurrentStep.ShouldBeOfType<RabbitMqTopologyStep>();
		passo.Operation.ShouldBe(RabbitMqTopologyOperation.DeclareQueue);
		passo.Queue.ShouldBe("pedidos");
		passo.Durable.ShouldBeTrue("RabbitMQ 4 refuses a transient non-exclusive queue");
	}

	[Fact(DisplayName = "DeclareExchange should record the type it was given")]
	public void DeclareExchange_Should_Record_The_Type()
	{
		var cenario = ScenarioDsl.Given().Exchange("eventos").DeclareExchange("topic");

		var passo = cenario.CurrentStep.ShouldBeOfType<RabbitMqTopologyStep>();
		passo.Operation.ShouldBe(RabbitMqTopologyOperation.DeclareExchange);
		passo.Exchange.ShouldBe("eventos");
		passo.ExchangeType.ShouldBe("topic");
	}

	[Fact(DisplayName = "BindQueueTo should record both ends and the routing key")]
	public void BindQueueTo_Should_Record_Both_Ends()
	{
		var cenario = ScenarioDsl.Given().Queue("pedidos").BindQueueTo("eventos", "pedidos.criado");

		var passo = cenario.CurrentStep.ShouldBeOfType<RabbitMqTopologyStep>();
		passo.Operation.ShouldBe(RabbitMqTopologyOperation.BindQueue);
		passo.Queue.ShouldBe("pedidos");
		passo.Exchange.ShouldBe("eventos");
		passo.RoutingKey.ShouldBe("pedidos.criado");
	}

	// Declarar fila sem nomear fila é erro de ordem, e a mensagem diz o verbo que falta.
	[Fact(DisplayName = "Declaring with nothing named should say which verb is missing")]
	public void Declaring_With_Nothing_Named_Should_Say_So()
	{
		var erro = Should.Throw<InvalidOperationException>(
			() => ScenarioDsl.Given().Exchange("eventos").DeclareQueue());

		erro.Message.Contains("Queue").ShouldBeTrue();
	}

	[Fact(DisplayName = "Rejecting should record the rejection and its requeue choice")]
	public void Rejecting_Should_Record_The_Choice()
	{
		var semRequeue = ScenarioDsl.Given().Queue("pedidos").Consume().Rejecting();
		var comRequeue = ScenarioDsl.Given().Queue("pedidos").Consume().Rejecting(requeue: true);

		var a = semRequeue.CurrentStep.ShouldBeOfType<RabbitMqConsumeStep>();
		a.RejectMessage.ShouldBeTrue();
		a.RequeueRejected.ShouldBeFalse("not requeueing is what sends it to the dead-letter");

		comRequeue.CurrentStep.ShouldBeOfType<RabbitMqConsumeStep>().RequeueRejected.ShouldBeTrue();
	}

	[Fact(DisplayName = "DeclareQueue should record the dead-letter exchange it was given")]
	public void DeclareQueue_Should_Record_The_Dead_Letter_Exchange()
	{
		var cenario = ScenarioDsl.Given().Queue("pedidos").DeclareQueue(deadLetterExchange: "dlx");

		cenario.CurrentStep.ShouldBeOfType<RabbitMqTopologyStep>()
			.DeadLetterExchange.ShouldBe("dlx");
	}

	// Recusar sem ter consumido é erro de ordem, e a mensagem diz o verbo que falta.
	[Fact(DisplayName = "Rejecting without consuming should say which verb is missing")]
	public void Rejecting_Without_Consuming_Should_Say_So()
	{
		var erro = Should.Throw<InvalidOperationException>(
			() => ScenarioDsl.Given().Queue("pedidos").Publish("oi").Rejecting());

		erro.Message.Contains("Consume").ShouldBeTrue();
	}

	// Antes de Publish ou Consume não há passo onde guardar a URI, então ela vai para o contexto e
	// o passo a encontra quando nascer.
	[Fact(DisplayName = "A connection URI set before the step should reach the step")]
	public void A_Uri_Set_Before_The_Step_Should_Reach_It()
	{
		var cenario = ScenarioDsl.Given()
			.Queue("pedidos")
			.WithConnectionUri("amqp://convidado:convidado@127.0.0.1:5672/")
			.Consume();

		cenario.Context.GetProperty<string>("_RabbitMqConnectionUri")
			.ShouldBe("amqp://convidado:convidado@127.0.0.1:5672/");
		cenario.CurrentStep.ShouldBeOfType<RabbitMqConsumeStep>();
	}
}
