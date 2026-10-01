using System;

using Shouldly;
using Xunit;

using XUnitAssured.Core.DSL;
using XUnitAssured.Kafka.Abstractions;
using XUnitAssured.Kafka.Extensions;
using XUnitAssured.RabbitMq.Abstractions;
using XUnitAssured.RabbitMq.Extensions;

namespace XUnitAssured.Tests.RabbitMqTests;

[Trait("Category", "RabbitMq")]
[Trait("Component", "DSL")]
/// <summary>
/// O teste que justifica o desenho do pacote.
///
/// <para>
/// Este arquivo referencia Kafka e RabbitMQ e usa as duas cadeias, com os dois <c>using</c> no
/// mesmo nível. É o arquivo que não compilaria se o pacote novo tivesse declarado os verbos como
/// extensão de <c>ITestScenario</c>, como o Kafka faz: <c>Consume</c>, <c>WithTimeout</c> e
/// <c>ValidateMessage</c> sairiam ambíguos, com CS0121.
/// </para>
///
/// <para>
/// O escopo dos <c>using</c> importa e por isso está fixado aqui. Resolução de extensão prefere o
/// namespace mais interno, então pôr um dos dois dentro de um bloco de namespace esconderia a
/// colisão e faria o teste passar sem provar nada. Foi um erro que eu cometi ao verificar isto pela
/// primeira vez.
/// </para>
///
/// <para>
/// Nada aqui toca um broker: as cadeias são descritas e não executadas, que é exatamente o que a
/// 6.0.0 permite.
/// </para>
/// </summary>
public class DoisBrokersNoMesmoArquivoTests
{
	private sealed class Pedido
	{
		public int Id { get; set; }
	}

	[Fact(DisplayName = "A Kafka chain and a RabbitMQ chain coexist in one file")]
	public void Both_Chains_Coexist_In_One_File()
	{
		var kafka = ScenarioDsl.Given()
			.Topic("orders.created")
			.Consume()
			.WithTimeout(TimeSpan.FromSeconds(5));

		var rabbit = ScenarioDsl.Given()
			.Queue("orders")
			.Consume()
			.WithTimeout(TimeSpan.FromSeconds(5));

		kafka.ShouldBeAssignableTo<IKafkaScenario>();
		rabbit.ShouldBeAssignableTo<IRabbitMqScenario>();
	}

	// O verbo compartilhado mais perigoso: os dois pacotes querem a palavra, e o receptor é o que
	// decide. Se um dia alguém mover os verbos do RabbitMQ para extensão de ITestScenario, esta
	// linha deixa de compilar e o build diz por quê.
	[Fact(DisplayName = "ValidateMessage resolves by receiver, not by ambiguity")]
	public void ValidateMessage_Resolves_By_Receiver()
	{
		var kafka = ScenarioDsl.Given()
			.Topic("orders.created")
			.Consume()
			.ValidateMessage<Pedido>(pedido => pedido.Id.ShouldBeGreaterThan(0));

		var rabbit = ScenarioDsl.Given()
			.Queue("orders")
			.Consume()
			.ValidateMessage<Pedido>(pedido => pedido.Id.ShouldBeGreaterThan(0));

		kafka.ShouldBeAssignableTo<IKafkaScenario>();
		rabbit.ShouldBeAssignableTo<IRabbitMqScenario>();
	}

	// Os verbos de entrada são o outro lado da convenção: eles tomam o cenário não tipado, e por
	// isso precisam de nomes próprios de cada broker.
	[Fact(DisplayName = "The entry verbs name each broker's own thing")]
	public void The_Entry_Verbs_Name_Each_Brokers_Own_Thing()
	{
		ScenarioDsl.Given().Topic("t").ShouldBeAssignableTo<IKafkaScenario>();
		ScenarioDsl.Given().Queue("q").ShouldBeAssignableTo<IRabbitMqScenario>();
		ScenarioDsl.Given().Exchange("x").ShouldBeAssignableTo<IRabbitMqScenario>();
	}
}
