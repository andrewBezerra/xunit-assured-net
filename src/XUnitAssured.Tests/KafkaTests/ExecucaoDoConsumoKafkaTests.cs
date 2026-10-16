using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Confluent.Kafka;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using XUnitAssured.Core.DSL;
using XUnitAssured.Kafka.Abstractions;
using XUnitAssured.Kafka.Helpers;
using XUnitAssured.Kafka.Results;
using XUnitAssured.Kafka.Steps;

namespace XUnitAssured.Tests.KafkaTests;

[Trait("Category", "Kafka")]
[Trait("Component", "ConsumeStep")]
/// <summary>
/// Os passos de consumo, de ponta a ponta, sem broker: o consumidor e as partições vêm de uma
/// <see cref="FabricaFalsa"/>.
///
/// <para>
/// Até aqui só o caminho de falha tinha teste (broker inalcançável). O laço que espera a
/// mensagem, junta um lote, desiste no prazo e respeita o cancelamento de quem chamou não era
/// exercitado por nada fora dos testes de integração.
/// </para>
/// </summary>
public class ExecucaoDoConsumoKafkaTests
{
	private sealed class Pedido
	{
		public int Id { get; set; }
	}

	// ---------- Consumo de uma mensagem ----------

	[Fact(DisplayName = "A consume should return the first message that arrives, after empty polls")]
	public void Consume_Should_Return_First_Message()
	{
		var cabecalhos = new Headers { { "correlation-id", Encoding.UTF8.GetBytes("abc") } };
		var fabrica = new FabricaFalsa(0, 1).Chegam(
			null!, null!,
			FalsosDoKafka.Consumida("pedidos", "chave-1", """{"id":7,"itens":[{"preco":9.5}]}""", particao: 1, offset: 12, cabecalhos));

		var cenario = Consumo(fabrica);

		cenario.When().Execute()
			.Then()
			.AssertSuccess()
			.AssertTopic("pedidos")
			.AssertPartition(1)
			.AssertKey(chave => chave.ShouldBe("chave-1"))
			.AssertMessage(mensagem => mensagem.ShouldBeOfType<string>())
			.AssertJsonPath<int>("$.id", id => id.ShouldBe(7))
			.AssertJsonPath<decimal>("$.itens[0].preco", preco => preco > 9m, "price should be above 9")
			.AssertHeader("correlation-id", "abc")
			.AssertHeader("correlation-id", bytes => bytes.Length.ShouldBe(3));

		var resultado = (KafkaStepResult)cenario.CurrentStep!.Result!;
		resultado.Offset.ShouldBe(12);
		resultado.Timestamp.ShouldBe(new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc));
		fabrica.Consumidor.Received(1).Dispose();
	}

	[Fact(DisplayName = "A consume should be assigned every partition of the topic from the reset policy")]
	public async Task Consume_Should_Assign_Every_Partition()
	{
		var fabrica = new FabricaFalsa(0, 1, 2).Chegam(FalsosDoKafka.Consumida("pedidos", null, "v"));
		var passo = new KafkaConsumeStep { Topic = "pedidos", BootstrapServers = "b:9092", Fabrica = fabrica };

		await passo.ExecuteAsync(new ContextoDeTeste());

		// O passo monta a configuração com AutoOffsetReset = Earliest: sem offset gravado, começa do início.
		fabrica.Consumidor.Received(1).Assign(Arg.Is<IEnumerable<TopicPartitionOffset>>(a =>
			a.Select(p => p.Partition.Value).SequenceEqual(new[] { 0, 1, 2 })
			&& a.All(p => p.Offset == Offset.Beginning && p.Topic == "pedidos")));
	}

	[Fact(DisplayName = "A consume should use the fixture's broker and group when the step sets none")]
	public async Task Consume_Should_Use_Context_Broker_And_Group()
	{
		var fabrica = new FabricaFalsa(0).Chegam(FalsosDoKafka.Consumida("pedidos", null, "v"));
		var contexto = new ContextoDeTeste();
		contexto.SetProperty("_KafkaBootstrapServers", "broker-da-fixture:9092");
		contexto.SetProperty("_KafkaGroupId", "grupo-da-fixture");

		await new KafkaConsumeStep { Topic = "pedidos", Fabrica = fabrica }.ExecuteAsync(contexto);

		fabrica.ConfigRecebida!.BootstrapServers.ShouldBe("broker-da-fixture:9092");
		fabrica.ConfigRecebida.GroupId.ShouldBe("grupo-da-fixture");
		fabrica.ConfigRecebida.EnableAutoCommit.ShouldBe(false);
	}

	[Fact(DisplayName = "The metadata wait should be bounded by the step timeout, and by five seconds at most")]
	public async Task Metadata_Wait_Should_Be_Bounded()
	{
		var curta = new FabricaFalsa(0);
		await new KafkaConsumeStep { Topic = "t", BootstrapServers = "b:1", Timeout = TimeSpan.FromMilliseconds(150), Fabrica = curta }
			.ExecuteAsync(new ContextoDeTeste());
		curta.PrazoDosMetadados.ShouldBe(TimeSpan.FromMilliseconds(150));

		var longa = new FabricaFalsa(0).Chegam(FalsosDoKafka.Consumida("t", null, "v"));
		await new KafkaConsumeStep { Topic = "t", BootstrapServers = "b:1", Timeout = TimeSpan.FromMinutes(2), Fabrica = longa }
			.ExecuteAsync(new ContextoDeTeste());
		longa.PrazoDosMetadados.ShouldBe(TimeSpan.FromSeconds(5));
	}

	[Fact(DisplayName = "A consume with nothing to read should time out and explain itself")]
	public async Task Consume_Should_Time_Out_With_Diagnostics()
	{
		var fabrica = new FabricaFalsa(0, 1);
		fabrica.LogsAoCriar.Add(new LogMessage("rdkafka", SyslogLevel.Info, "BROKER", "conectando"));
		fabrica.ErrosAoCriar.Add(new Error(ErrorCode.Local_Transport, "transporte caiu"));

		var passo = new KafkaConsumeStep
		{
			Topic = "pedidos", BootstrapServers = "b:9092", GroupId = "g", Timeout = TimeSpan.FromMilliseconds(200), Fabrica = fabrica
		};

		var resultado = (KafkaStepResult)await passo.ExecuteAsync(new ContextoDeTeste());

		resultado.Success.ShouldBeFalse();
		resultado.Errors[0].ShouldStartWith("No message consumed from topic 'pedidos' within timeout");
		resultado.Errors.ShouldContain(e => e.Contains("transporte caiu"), "a client error explains an empty consume");
		resultado.GetProperty<List<string>>("BrokerLogs")!.ShouldBe(new[] { "Info: conectando" });
		resultado.GetProperty<string>("AssignedPartitions").ShouldBe("pedidos [[0]],pedidos [[1]]");
		resultado.GetProperty<string>("GroupId").ShouldBe("g");
		fabrica.Consumidor.Received(1).Dispose();
	}

	[Fact(DisplayName = "Broker logs kept for diagnostics should be capped")]
	public async Task Broker_Logs_Should_Be_Capped()
	{
		var fabrica = new FabricaFalsa(0);
		for (var i = 0; i < 250; i++)
			fabrica.LogsAoCriar.Add(new LogMessage("rdkafka", SyslogLevel.Debug, "FETCH", $"log {i}"));

		var resultado = await new KafkaConsumeStep
		{
			Topic = "t", BootstrapServers = "b:1", Timeout = TimeSpan.FromMilliseconds(100), Fabrica = fabrica
		}.ExecuteAsync(new ContextoDeTeste());

		var logs = resultado.GetProperty<List<string>>("BrokerLogs")!;
		logs.Count.ShouldBe(200);
		logs[0].ShouldBe("Debug: log 0");
	}

	[Fact(DisplayName = "A consume from a topic with no partitions should fail saying so, with diagnostics")]
	public async Task Consume_From_Missing_Topic_Should_Fail()
	{
		var fabrica = new FabricaFalsa();

		var resultado = await new KafkaConsumeStep
		{
			Topic = "nao-existe", BootstrapServers = "b:9092", Timeout = TimeSpan.FromSeconds(1), Fabrica = fabrica
		}.ExecuteAsync(new ContextoDeTeste());

		resultado.Success.ShouldBeFalse();
		resultado.GetProperty<string>("ExceptionMessage")
			.ShouldBe("Topic 'nao-existe' has no partitions. It probably does not exist on the broker.");
		resultado.GetProperty<string>("BootstrapServers").ShouldBe("b:9092");
		resultado.Properties.ContainsKey("BrokerLogs").ShouldBeTrue();
		fabrica.Consumidor.Received(1).Dispose();
	}

	[Fact(DisplayName = "A consume cancelled by the caller should stop and report a failure")]
	public async Task Consume_Cancelled_By_Caller_Should_Stop()
	{
		var fabrica = new FabricaFalsa(0);
		using var cancelamento = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
		var passo = new KafkaConsumeStep { Topic = "t", BootstrapServers = "b:1", Timeout = TimeSpan.FromMinutes(1), Fabrica = fabrica };

		var cronometro = System.Diagnostics.Stopwatch.StartNew();
		var resultado = await passo.ExecuteAsync(new ContextoDeTeste(), cancelamento.Token);

		cronometro.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(30), "cancelling must not wait out the step timeout");
		resultado.Success.ShouldBeFalse();
		resultado.GetProperty<string>("ExceptionType")!.ShouldContain("Canceled");
	}

	[Fact(DisplayName = "The copy constructor should carry the consumer factory along")]
	public void Copy_Should_Carry_Factory()
	{
		var fabrica = new FabricaFalsa(0);

		new KafkaConsumeStep(new KafkaConsumeStep { Fabrica = fabrica }).Fabrica.ShouldBeSameAs(fabrica);
		new KafkaBatchConsumeStep(new KafkaBatchConsumeStep { Fabrica = fabrica }).Fabrica.ShouldBeSameAs(fabrica);
		new KafkaConsumeStep().Fabrica.ShouldBeSameAs(FabricaDeConsumidor.Real);
	}

	[Fact(DisplayName = "Copy constructors should reject a null source")]
	public void Copy_Constructors_Should_Reject_Null()
	{
		Should.Throw<ArgumentNullException>(() => new KafkaConsumeStep(null!));
		Should.Throw<ArgumentNullException>(() => new KafkaBatchConsumeStep(null!));
		Should.Throw<ArgumentNullException>(() => new KafkaProduceStep(null!));
		Should.Throw<ArgumentNullException>(() => new KafkaBatchProduceStep(null!));
	}

	// ---------- Validações sobre a mensagem consumida ----------

	[Fact(DisplayName = "Validate should run against the consumed message once the step has run")]
	public void Validate_Should_Run_After_Execution()
	{
		var fabrica = new FabricaFalsa(0).Chegam(FalsosDoKafka.Consumida("pedidos", "k", "conteúdo"));
		string? visto = null;

		Consumo(fabrica)
			.Validate((KafkaStepResult r) => visto = (string?)r.Message)
			.When().Execute();

		visto.ShouldBe("conteúdo");
	}

	[Fact(DisplayName = "Assertions on the consumed message should fail with what was actually there")]
	public void Assertions_Should_Fail_With_Actual_Values()
	{
		var fabrica = new FabricaFalsa(0).Chegam(FalsosDoKafka.Consumida("pedidos", "k", """{"id":1}""", particao: 0));
		var validacao = Consumo(fabrica).When().Execute();

		Should.Throw<ShouldAssertException>(() => validacao.AssertTopic("outro")).Message.ShouldContain("Expected topic 'outro' but got 'pedidos'");
		Should.Throw<ShouldAssertException>(() => validacao.AssertPartition(3)).Message.ShouldContain("Expected partition 3 but got 0");
		Should.Throw<ShouldAssertException>(() => validacao.AssertHeader("ausente", "x")).Message.ShouldContain("Header 'ausente' not found");
		Should.Throw<ShouldAssertException>(() => validacao.AssertHeader("ausente", _ => { })).Message.ShouldContain("Header 'ausente' not found");
		Should.Throw<ShouldAssertException>(() => validacao.AssertJsonPath<int>("$.id", id => id > 5, "id too small")).Message.ShouldContain("id too small");
		Should.Throw<ShouldAssertException>(() => validacao.AssertFailure()).Message.ShouldContain("Expected step to fail but it succeeded");
	}

	[Fact(DisplayName = "Assertions should reject null delegates")]
	public void Assertions_Should_Reject_Null_Delegates()
	{
		var fabrica = new FabricaFalsa(0).Chegam(FalsosDoKafka.Consumida("pedidos", "k", """{"id":1}"""));
		var validacao = Consumo(fabrica).When().Execute();

		Should.Throw<ArgumentNullException>(() => validacao.AssertKey(null!));
		Should.Throw<ArgumentNullException>(() => validacao.AssertMessage(null!));
		Should.Throw<ArgumentNullException>(() => validacao.AssertMessage<Pedido>(null!));
		Should.Throw<ArgumentNullException>(() => validacao.AssertJsonPath<int>("$.id", (Action<int>)null!));
		Should.Throw<ArgumentNullException>(() => validacao.AssertJsonPath<int>("$.id", (Func<int, bool>)null!));
		Should.Throw<ArgumentNullException>(() => validacao.AssertHeader("h", (Action<byte[]>)null!));
	}

	[Fact(DisplayName = "The validation builder should keep its own type through Then and AssertSuccess")]
	public void Validation_Builder_Should_Keep_Type()
	{
		var fabrica = new FabricaFalsa(0).Chegam(FalsosDoKafka.Consumida("pedidos", "k", "v"));

		var resultado = Consumo(fabrica).When().Execute().Then().AssertSuccess().Then().GetResult();

		resultado.ShouldBeOfType<KafkaStepResult>().Topic.ShouldBe("pedidos");
	}

	// A documentação de ValidateMessage<T> e AssertMessage<T> promete desserializar a mensagem
	// (e o gerador do MCP sugere AssertMessage<Order>). O valor consumido é uma string JSON, e
	// GetMessage<T> só faz conversão de tipos primitivos, então nunca chega a um objeto.
	[Fact(DisplayName = "ValidateMessage should deserialize a JSON message into the requested type")]
	public void ValidateMessage_Should_Deserialize_Json()
	{
		var fabrica = new FabricaFalsa(0).Chegam(FalsosDoKafka.Consumida("pedidos", "k", """{"id":7}"""));
		int? id = null;

		Consumo(fabrica).ValidateMessage<Pedido>(p => id = p.Id).When().Execute();

		id.ShouldBe(7);
	}

	[Fact(DisplayName = "ValidateMessage should hand over a string message as is")]
	public void ValidateMessage_Should_Hand_Over_String()
	{
		var fabrica = new FabricaFalsa(0).Chegam(FalsosDoKafka.Consumida("pedidos", "k", "texto puro"));
		string? visto = null;

		Consumo(fabrica)
			.ValidateMessage<string>(m => visto = m)
			.When().Execute()
			.AssertMessage<string>(m => m.ShouldBe("texto puro"));

		visto.ShouldBe("texto puro");
	}

	// ---------- Consumo em lote ----------

	[Fact(DisplayName = "A batch consume should collect the requested number of messages in order")]
	public void Batch_Consume_Should_Collect_Messages()
	{
		var fabrica = new FabricaFalsa(0).Chegam(
			FalsosDoKafka.Consumida("pedidos", "a", "1", offset: 0),
			null!,
			FalsosDoKafka.Consumida("pedidos", "b", "2", offset: 1),
			FalsosDoKafka.Consumida("pedidos", "c", "3", offset: 2),
			FalsosDoKafka.Consumida("pedidos", "d", "4", offset: 3));

		var cenario = ScenarioDsl.Given().Topic("pedidos").ConsumeBatch(3);
		Trocar(cenario, fabrica);

		var resultado = cenario.When().Execute()
			.Then().AssertSuccess().AssertTopic("pedidos").AssertBatchCount(3)
			.GetResult();

		resultado.GetProperty<List<ConsumeResult<string, string>>>("ConsumeResults")!
			.Select(r => r.Message.Value).ShouldBe(new[] { "1", "2", "3" }, "the fourth message was not asked for");
		fabrica.Consumidor.Received(1).Dispose();
	}

	[Fact(DisplayName = "A batch consume that times out should report how many of how many arrived")]
	public async Task Batch_Consume_Timeout_Should_Report_Partial()
	{
		var fabrica = new FabricaFalsa(0).Chegam(
			FalsosDoKafka.Consumida("pedidos", "a", "1"),
			FalsosDoKafka.Consumida("pedidos", "b", "2"));
		var passo = new KafkaBatchConsumeStep
		{
			Topic = "pedidos", MessageCount = 3, BootstrapServers = "b:1", Timeout = TimeSpan.FromMilliseconds(250), Fabrica = fabrica
		};

		var resultado = (KafkaStepResult)await passo.ExecuteAsync(new ContextoDeTeste());

		resultado.Success.ShouldBeFalse();
		resultado.Errors.Single().ShouldStartWith("Consumed 2 of 3 messages from topic 'pedidos' within timeout");
		resultado.GetProperty<int>("BatchSize").ShouldBe(2);
		resultado.GetProperty<int>("ExpectedBatchSize").ShouldBe(3);
	}

	[Fact(DisplayName = "A batch consume cancelled by the caller should stop with what it had")]
	public async Task Batch_Consume_Cancelled_Should_Stop()
	{
		var fabrica = new FabricaFalsa(0).Chegam(FalsosDoKafka.Consumida("pedidos", "a", "1"));
		var passo = new KafkaBatchConsumeStep
		{
			Topic = "pedidos", MessageCount = 5, BootstrapServers = "b:1", Timeout = TimeSpan.FromMinutes(1), Fabrica = fabrica
		};
		using var cancelamento = new CancellationTokenSource(TimeSpan.FromMilliseconds(150));

		var cronometro = System.Diagnostics.Stopwatch.StartNew();
		var resultado = await passo.ExecuteAsync(new ContextoDeTeste(), cancelamento.Token);

		cronometro.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(30));
		resultado.Success.ShouldBeFalse();
		resultado.GetProperty<int>("BatchSize").ShouldBe(1);
	}

	[Fact(DisplayName = "A batch consume should use the fixture's group and bound the metadata wait")]
	public async Task Batch_Consume_Should_Use_Context_Group()
	{
		var fabrica = new FabricaFalsa(0).Chegam(FalsosDoKafka.Consumida("pedidos", "a", "1"));
		var contexto = new ContextoDeTeste();
		contexto.SetProperty("_KafkaGroupId", "grupo-da-fixture");

		await new KafkaBatchConsumeStep
		{
			Topic = "pedidos", MessageCount = 1, BootstrapServers = "b:1", Timeout = TimeSpan.FromMinutes(1), Fabrica = fabrica
		}.ExecuteAsync(contexto);

		fabrica.ConfigRecebida!.GroupId.ShouldBe("grupo-da-fixture");
		fabrica.PrazoDosMetadados.ShouldBe(TimeSpan.FromSeconds(5));
	}

	[Fact(DisplayName = "A batch consume from a missing topic should fail with diagnostics")]
	public async Task Batch_Consume_From_Missing_Topic_Should_Fail()
	{
		var fabrica = new FabricaFalsa();
		fabrica.LogsAoCriar.Add(new LogMessage("rdkafka", SyslogLevel.Warning, "META", "tópico desconhecido"));
		fabrica.ErrosAoCriar.Add(new Error(ErrorCode.UnknownTopicOrPart, "desconhecido"));

		var resultado = await new KafkaBatchConsumeStep
		{
			Topic = "nao-existe", MessageCount = 2, BootstrapServers = "b:9092", Timeout = TimeSpan.FromSeconds(1), Fabrica = fabrica
		}.ExecuteAsync(new ContextoDeTeste());

		resultado.Success.ShouldBeFalse();
		resultado.GetProperty<string>("ExceptionMessage")!.ShouldContain("Topic 'nao-existe' has no partitions");
		resultado.GetProperty<List<string>>("BrokerLogs")!.ShouldBe(new[] { "Warning: tópico desconhecido" });
		resultado.Errors.ShouldContain(e => e.Contains("desconhecido"));
	}

	[Fact(DisplayName = "A consume step should be valid only after a passing validation")]
	public async Task Consume_Step_Validity()
	{
		var simples = new KafkaConsumeStep
		{
			Topic = "t", BootstrapServers = "b:1", Fabrica = new FabricaFalsa(0).Chegam(FalsosDoKafka.Consumida("t", null, "v"))
		};
		var lote = new KafkaBatchConsumeStep
		{
			Topic = "t", MessageCount = 1, BootstrapServers = "b:1", Fabrica = new FabricaFalsa(0).Chegam(FalsosDoKafka.Consumida("t", null, "v"))
		};
		await simples.ExecuteAsync(new ContextoDeTeste());
		await lote.ExecuteAsync(new ContextoDeTeste());

		foreach (var passo in new XUnitAssured.Core.Abstractions.ITestStep[] { simples, lote })
		{
			passo.IsExecuted.ShouldBeTrue();
			Should.Throw<InvalidOperationException>(() => passo.Validate(_ => throw new InvalidOperationException("reprovado")));
			passo.IsValid.ShouldBeFalse();
			passo.Validate(_ => { });
			passo.IsValid.ShouldBeTrue();
		}
	}

	private static IKafkaScenario Consumo(FabricaFalsa fabrica)
	{
		var cenario = ScenarioDsl.Given().Topic("pedidos").Consume().WithBootstrapServers("b:9092").WithTimeout(TimeSpan.FromSeconds(10));
		Trocar(cenario, fabrica);
		return cenario;
	}

	/// <summary>
	/// Põe a fábrica falsa no passo que a cadeia montou, como um verbo faria: reconstruindo-o.
	/// </summary>
	private static void Trocar(IKafkaScenario cenario, FabricaFalsa fabrica)
	{
		switch (cenario.CurrentStep)
		{
			case KafkaConsumeStep consumo:
				cenario.SetCurrentStep(new KafkaConsumeStep(consumo) { Fabrica = fabrica });
				break;
			case KafkaBatchConsumeStep lote:
				cenario.SetCurrentStep(new KafkaBatchConsumeStep(lote) { Fabrica = fabrica, BootstrapServers = "b:9092", Timeout = TimeSpan.FromSeconds(10) });
				break;
		}
	}
}

[Trait("Category", "Kafka")]
[Trait("Component", "ConsumeStep")]
/// <summary>
/// A atribuição manual de partições, com um consumidor falso: o que se atribui, a partir de onde,
/// e o que acontece quando o broker não colabora.
///
/// <para>
/// <see cref="ConsumerAssignmentTests"/> cobre a regra pura (<c>ResolveStartOffsets</c>); aqui é
/// a orquestração em volta dela, que antes só rodava contra um broker de verdade.
/// </para>
/// </summary>
public class AtribuicaoDeParticoesTests
{
	private static readonly ConsumerConfig ConfigEarliest = new() { AutoOffsetReset = AutoOffsetReset.Earliest };

	[Fact(DisplayName = "Assignment should reject missing arguments before talking to the broker")]
	public void Assignment_Should_Reject_Missing_Arguments()
	{
		var fabrica = new FabricaFalsa(0);
		var consumidor = fabrica.Consumidor;

		Should.Throw<ArgumentNullException>(() => ConsumerAssignment.AssignAllPartitions(null!, ConfigEarliest, "t", TimeSpan.FromSeconds(1), fabrica.DescobrirParticoes));
		Should.Throw<ArgumentNullException>(() => ConsumerAssignment.AssignAllPartitions(consumidor, null!, "t", TimeSpan.FromSeconds(1), fabrica.DescobrirParticoes));
		Should.Throw<ArgumentException>(() => ConsumerAssignment.AssignAllPartitions(consumidor, ConfigEarliest, " ", TimeSpan.FromSeconds(1), fabrica.DescobrirParticoes));
		Should.Throw<ArgumentNullException>(() => ConsumerAssignment.AssignAllPartitions(null!, ConfigEarliest, "t", TimeSpan.FromSeconds(1)));

		fabrica.PrazoDosMetadados.ShouldBeNull("nothing should be asked of the broker for an invalid call");
	}

	[Fact(DisplayName = "A committed offset should be resumed and the other partitions follow the reset policy")]
	public void Committed_Offset_Should_Be_Resumed()
	{
		var fabrica = new FabricaFalsa(0, 1);
		fabrica.Consumidor.Committed(Arg.Any<IEnumerable<TopicPartition>>(), Arg.Any<TimeSpan>())
			.Returns(new List<TopicPartitionOffset> { new("t", new Partition(1), new Offset(40)) });

		var atribuicao = ConsumerAssignment.AssignAllPartitions(
			fabrica.Consumidor, ConfigEarliest, "t", TimeSpan.FromSeconds(2), fabrica.DescobrirParticoes);

		atribuicao.Select(a => (a.Partition.Value, a.Offset.Value))
			.ShouldBe(new[] { (0, Offset.Beginning.Value), (1, 40L) });
		fabrica.Consumidor.Received(1).Assign(atribuicao);
		fabrica.Consumidor.Received(1).Committed(Arg.Any<IEnumerable<TopicPartition>>(), TimeSpan.FromSeconds(2));
	}

	[Fact(DisplayName = "An unreachable group coordinator should mean nothing is committed, not a failure")]
	public void Unreachable_Coordinator_Should_Fall_Back_To_Reset_Policy()
	{
		var fabrica = new FabricaFalsa(0);
		fabrica.Consumidor.Committed(Arg.Any<IEnumerable<TopicPartition>>(), Arg.Any<TimeSpan>())
			.Throws(new KafkaException(ErrorCode.GroupCoordinatorNotAvailable));

		var atribuicao = ConsumerAssignment.AssignAllPartitions(
			fabrica.Consumidor, new ConsumerConfig { AutoOffsetReset = AutoOffsetReset.Latest }, "t", TimeSpan.FromSeconds(1), fabrica.DescobrirParticoes);

		atribuicao.Single().Offset.ShouldBe(Offset.End);
		fabrica.Consumidor.Received(1).Assign(Arg.Any<IEnumerable<TopicPartitionOffset>>());
	}

	[Fact(DisplayName = "Warming the leader metadata should query the first partition and tolerate a refusal")]
	public void Warming_Should_Tolerate_Refusal()
	{
		var fabrica = new FabricaFalsa(3, 4);
		fabrica.Consumidor.QueryWatermarkOffsets(Arg.Any<TopicPartition>(), Arg.Any<TimeSpan>())
			.Throws(new KafkaException(ErrorCode.NotLeaderForPartition));

		var atribuicao = ConsumerAssignment.AssignAllPartitions(
			fabrica.Consumidor, ConfigEarliest, "t", TimeSpan.FromSeconds(1), fabrica.DescobrirParticoes);

		atribuicao.Count.ShouldBe(2);
		fabrica.Consumidor.Received(1).QueryWatermarkOffsets(new TopicPartition("t", new Partition(3)), TimeSpan.FromSeconds(1));
		fabrica.Consumidor.Received(1).Assign(Arg.Any<IEnumerable<TopicPartitionOffset>>());
	}

	[Fact(DisplayName = "A topic with no partitions should be reported as probably missing, without assigning")]
	public void Topic_Without_Partitions_Should_Be_Reported()
	{
		var fabrica = new FabricaFalsa();

		Should.Throw<InvalidOperationException>(() => ConsumerAssignment.AssignAllPartitions(
				fabrica.Consumidor, ConfigEarliest, "sumido", TimeSpan.FromSeconds(1), fabrica.DescobrirParticoes))
			.Message.ShouldBe("Topic 'sumido' has no partitions. It probably does not exist on the broker.");

		fabrica.Consumidor.DidNotReceive().Assign(Arg.Any<IEnumerable<TopicPartitionOffset>>());
	}
}
