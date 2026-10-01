using System;

using Shouldly;
using Xunit;

using XUnitAssured.Core.DSL;
using XUnitAssured.Kafka.Abstractions;
using XUnitAssured.Kafka.Extensions;
using XUnitAssured.Kafka.Results;

namespace XUnitAssured.Tests.KafkaTests;

[Trait("Category", "Kafka")]
[Trait("Component", "DSL")]
/// <summary>
/// A Kafka chain has to stay a Kafka chain all the way to the end.
///
/// <para>
/// Typed scenarios exist so that <c>Execute()</c> resolves on its own when a test project
/// references more than one package. That only works while every verb hands the type along: one
/// verb returning the untyped scenario is enough to break the end of the chain, and it breaks at
/// compile time, on the last line, with a message about type inference that says nothing about
/// the verb that actually caused it.
/// </para>
///
/// <para>
/// <c>ValidateMessage</c> was that verb. Every sibling in its class returned
/// <see cref="IKafkaScenario"/> and it returned <c>ITestScenario</c>, so a Kafka-only chain that
/// ended with it could not be executed at all.
/// </para>
/// </summary>
public class CenarioTipadoDoKafkaTests
{
	private sealed class Pedido
	{
		public int Id { get; set; }
	}

	// A verificação que importa. Antes do conserto, esta linha não compilava.
	[Fact(DisplayName = "A chain ending in ValidateMessage should still be a Kafka chain")]
	public void Chain_Ending_In_ValidateMessage_Should_Still_Be_A_Kafka_Chain()
	{
		var cenario = ScenarioDsl.Given()
			.Topic("orders.created")
			.Consume()
			.ValidateMessage<Pedido>(pedido => pedido.Id.ShouldBeGreaterThan(0));

		cenario.ShouldBeAssignableTo<IKafkaScenario>();
	}

	// Os irmãos, para que a diferença entre eles volte a aparecer aqui se alguém mexer num só.
	[Fact(DisplayName = "Every Kafka validation verb should keep the chain typed")]
	public void Every_Kafka_Validation_Verb_Should_Keep_The_Chain_Typed()
	{
		// Tipo explícito no lambda de propósito. Com `_ => { }`, o compilador escolhe a
		// sobrecarga de `Validate` do pacote Http, que aceita qualquer resultado, e a cadeia sai
		// tipada como HTTP. É uma aresta real do DSL quando um projeto referencia os dois
		// pacotes, e não é o que este teste está medindo.
		Consumo().Validate((KafkaStepResult _) => { }).ShouldBeAssignableTo<IKafkaScenario>();
		Consumo().ValidateMessage<Pedido>(_ => { }).ShouldBeAssignableTo<IKafkaScenario>();
		Consumo().ValidateOffset(_ => true).ShouldBeAssignableTo<IKafkaScenario>();
		Consumo().ValidatePartition(0).ShouldBeAssignableTo<IKafkaScenario>();
	}

	// Registrar não é executar: a verificação só roda quando a cadeia é executada, e é isso que
	// permite afirmar sobre uma mensagem que ainda não chegou.
	[Fact(DisplayName = "ValidateMessage should register the check rather than run it")]
	public void ValidateMessage_Should_Register_Rather_Than_Run()
	{
		var rodou = false;

		Consumo().ValidateMessage<Pedido>(_ => rodou = true);

		rodou.ShouldBeFalse("the chain is only being described");
	}

	// O passo precisa existir antes de haver o que verificar.
	[Fact(DisplayName = "ValidateMessage with no step should say so")]
	public void ValidateMessage_With_No_Step_Should_Say_So()
	{
		Should.Throw<InvalidOperationException>(
			() => ScenarioDsl.Given().ValidateMessage<Pedido>(_ => { }));
	}

	private static XUnitAssured.Core.Abstractions.ITestScenario Consumo() =>
		ScenarioDsl.Given().Topic("orders.created").Consume();
}
