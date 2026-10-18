using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Confluent.Kafka;

using NSubstitute;

using XUnitAssured.Core.DSL;
using XUnitAssured.Kafka.Abstractions;
using XUnitAssured.Kafka.Extensions;
using XUnitAssured.Kafka.Helpers;
using XUnitAssured.Kafka.Steps;

namespace XUnitAssured.Tests.KafkaTests;

/// <summary>
/// <c>FromTimestamp</c> e <c>WhereKey</c> no consumo: um teste que produz e consome num tópico com
/// histórico precisa ler a mensagem que ele mesmo causou, não a mais antiga do tópico.
/// </summary>
[Trait("Category", "Kafka")]
[Trait("Component", "ConsumeStep")]
public class FiltrosDoConsumoKafkaTests
{
	private static IKafkaScenario Consumo(FabricaFalsa fabrica, Func<IKafkaScenario, IKafkaScenario> filtros)
	{
		var cenario = filtros(ScenarioDsl.Given().Topic("pedidos").Consume().WithBootstrapServers("b:9092"))
			.WithTimeout(TimeSpan.FromSeconds(2));
		var passo = (KafkaConsumeStep)cenario.CurrentStep!;
		cenario.SetCurrentStep(new KafkaConsumeStep(passo) { Fabrica = fabrica });
		return cenario;
	}

	[Fact(DisplayName = "WhereKey should skip messages with another key and return the one with the key")]
	public void WhereKey_Should_Skip_Other_Keys()
	{
		var fabrica = new FabricaFalsa(0).Chegam(
			FalsosDoKafka.Consumida("pedidos", "pedido-1", """{"status":"antigo"}""", offset: 1),
			FalsosDoKafka.Consumida("pedidos", null, """{"status":"sem chave"}""", offset: 2),
			FalsosDoKafka.Consumida("pedidos", "pedido-2", """{"status":"novo"}""", offset: 3));

		Consumo(fabrica, c => c.WhereKey("pedido-2"))
			.When().Execute().Then()
			.AssertSuccess()
			.AssertKey(chave => chave.ShouldBe("pedido-2"))
			.AssertJsonPath<string>("$.status", s => s.ShouldBe("novo"));
	}

	[Fact(DisplayName = "WhereKey with no matching message should time out instead of returning another one")]
	public void WhereKey_Without_Match_Should_Time_Out()
	{
		var fabrica = new FabricaFalsa(0).Chegam(FalsosDoKafka.Consumida("pedidos", "outro", "{}"));

		Consumo(fabrica, c => c.WhereKey("pedido-2")).When().Execute().Then().AssertFailure();
	}

	[Fact(DisplayName = "FromTimestamp should assign each partition at the broker's offset for the moment, or at its end")]
	public void FromTimestamp_Should_Assign_Offsets_For_The_Moment()
	{
		var momento = new DateTimeOffset(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);
		var fabrica = new FabricaFalsa(0, 1).Chegam(FalsosDoKafka.Consumida("pedidos", "k", "{}", particao: 0, offset: 5));
		IEnumerable<TopicPartitionTimestamp>? pedido = null;
		fabrica.Consumidor.OffsetsForTimes(Arg.Do<IEnumerable<TopicPartitionTimestamp>>(p => pedido = p.ToList()), Arg.Any<TimeSpan>())
			.Returns(new List<TopicPartitionOffset>
			{
				new(new TopicPartition("pedidos", 0), new Offset(5)),
				new(new TopicPartition("pedidos", 1), Offset.End),
			});

		Consumo(fabrica, c => c.FromTimestamp(momento)).When().Execute().Then().AssertSuccess();

		pedido!.Select(p => p.Timestamp.UnixTimestampMs).Distinct().ShouldBe([momento.ToUnixTimeMilliseconds()]);
		fabrica.Consumidor.Received(1).Assign(Arg.Is<IEnumerable<TopicPartitionOffset>>(a =>
			a.Count() == 2
			&& a.Single(o => o.Partition.Value == 0).Offset.Value == 5
			&& a.Single(o => o.Partition.Value == 1).Offset == Offset.End));
		fabrica.Consumidor.DidNotReceive().Committed(Arg.Any<IEnumerable<TopicPartition>>(), Arg.Any<TimeSpan>());
	}

	[Fact(DisplayName = "The filters should survive the verbs that rebuild the step, such as WithTimeout")]
	public void Filters_Should_Survive_Other_Verbs()
	{
		var momento = DateTimeOffset.UtcNow;

		var passo = (KafkaConsumeStep)ScenarioDsl.Given().Topic("pedidos").Consume()
			.FromTimestamp(momento).WhereKey("pedido-2").WithGroupId("g").WithTimeout(TimeSpan.FromSeconds(3))
			.CurrentStep!;

		passo.ConsumeFrom.ShouldBe(momento);
		passo.KeyFilter.ShouldBe("pedido-2");
	}

	[Fact(DisplayName = "FromTimestamp and WhereKey should refuse a step that is not a consume")]
	public void Filters_Should_Refuse_A_Produce()
	{
		Should.Throw<InvalidOperationException>(() => ScenarioDsl.Given().Topic("t").Produce("x").FromTimestamp(DateTimeOffset.UtcNow));
		Should.Throw<InvalidOperationException>(() => ScenarioDsl.Given().Topic("t").Produce("x").WhereKey("k"));
	}

	[Fact(DisplayName = "ResolveTimestampOffsets should use the broker's offset and fall back to the end")]
	public void ResolveTimestampOffsets_Should_Fall_Back_To_End()
	{
		var p0 = new TopicPartition("t", 0);
		var p1 = new TopicPartition("t", 1);
		var p2 = new TopicPartition("t", 2);

		var resolvido = ConsumerAssignment.ResolveTimestampOffsets(
			[p0, p1, p2],
			[new TopicPartitionOffset(p0, new Offset(7)), new TopicPartitionOffset(p1, new Offset(-1))]);

		resolvido.Select(o => o.Offset).ShouldBe([new Offset(7), Offset.End, Offset.End]);
	}
}

/// <summary>
/// Os filtros contra um Kafka de verdade: num tópico que já tem uma mensagem da mesma chave,
/// produzir e consumir com <c>FromTimestamp</c> e <c>WhereKey</c> devolve a nova.
/// </summary>
[Trait("Category", "Kafka")]
[Trait("Component", "Integration")]
[Trait("Requires", "Broker")]
public class FiltrosDoConsumoNoKafkaTests
{
	private static readonly string Broker = Environment.GetEnvironmentVariable("XA_KAFKA_BOOTSTRAP") ?? "127.0.0.1:9092";

	[Fact(DisplayName = "On a topic with history, FromTimestamp and WhereKey should return the message the test produced")]
	public async Task Should_Return_The_Message_The_Test_Produced()
	{
		var topico = "xa-filtros-" + Guid.NewGuid().ToString("N");
		await ScenarioDsl.Given().Topic(topico).Produce("pedido-1", """{"status":"antigo"}""").WithBootstrapServers(Broker).ExecuteAsync();
		await Task.Delay(1500);

		var inicio = DateTimeOffset.UtcNow;
		await ScenarioDsl.Given().Topic(topico).Produce("outro", """{"status":"de outro"}""").WithBootstrapServers(Broker).ExecuteAsync();
		await ScenarioDsl.Given().Topic(topico).Produce("pedido-1", """{"status":"novo"}""").WithBootstrapServers(Broker).ExecuteAsync();

		var semFiltro = await ScenarioDsl.Given().Topic(topico).Consume()
			.WithBootstrapServers(Broker).WithGroupId("xa-" + Guid.NewGuid().ToString("N"))
			.WithConsumerConfig(new ConsumerConfig { BootstrapServers = Broker, AutoOffsetReset = AutoOffsetReset.Earliest, GroupId = "xa-" + Guid.NewGuid().ToString("N") })
			.WithTimeout(TimeSpan.FromSeconds(15)).ExecuteAsync();
		var comFiltro = await ScenarioDsl.Given().Topic(topico).Consume()
			.WithConsumerConfig(new ConsumerConfig { BootstrapServers = Broker, AutoOffsetReset = AutoOffsetReset.Earliest, GroupId = "xa-" + Guid.NewGuid().ToString("N") })
			.FromTimestamp(inicio).WhereKey("pedido-1")
			.WithTimeout(TimeSpan.FromSeconds(15)).ExecuteAsync();

		// Sem os filtros, o consumo lê a mais antiga: é o problema que eles resolvem.
		semFiltro.Then().AssertSuccess().AssertJsonPath<string>("$.status", s => s.ShouldBe("antigo"));
		comFiltro.Then().AssertSuccess().AssertKey(k => k.ShouldBe("pedido-1")).AssertJsonPath<string>("$.status", s => s.ShouldBe("novo"));
	}
}
