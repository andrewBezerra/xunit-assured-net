using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Confluent.Kafka;

using NSubstitute;

using XUnitAssured.Core.DSL;
using XUnitAssured.Kafka.Results;
using XUnitAssured.Kafka.Steps;

namespace XUnitAssured.Tests.KafkaTests;

[Trait("Category", "Kafka")]
[Trait("Component", "ProduceStep")]
/// <summary>
/// O passo de produção, de ponta a ponta, sem broker: o produtor é o compartilhado que a fixture
/// põe no contexto, aqui um falso.
///
/// <para>
/// O que se verifica é o que o usuário do pacote vê: a mensagem que chega ao produtor (chave,
/// valor serializado, partição, cabeçalhos, instante), o resultado do passo, e as verificações do
/// DSL (<c>ValidateProduceSuccess</c>, <c>AssertTopic</c>, ...) passando ou falhando pelo motivo
/// certo.
/// </para>
/// </summary>
public class ExecucaoDaProducaoKafkaTests
{
	private sealed class Pedido
	{
		public int Id { get; set; }

		public string? Descricao { get; set; }

		public string? Observacao { get; set; }
	}

	// ---------- Produção simples ----------

	[Fact(DisplayName = "A produce should hand the producer the topic, key and value, and report the delivery")]
	public void Produce_Should_Hand_Message_And_Report_Delivery()
	{
		var produtor = FalsosDoKafka.ProdutorQueGrava(particao: 1, offset: 77);
		var cenario = ScenarioDsl.Given(FalsosDoKafka.ContextoComProdutor(produtor));

		cenario.Topic("pedidos").Produce("chave-1", "olá")
			.ValidateProduceSuccess()
			.ValidatePartition(1)
			.ValidateOffset(o => o == 77)
			.ValidateDeliveryStatus(PersistenceStatus.Persisted)
			.When().Execute()
			.Then()
			.AssertSuccess()
			.AssertTopic("pedidos")
			.AssertPartition(1)
			.AssertKey(chave => chave.ShouldBe("chave-1"))
			.AssertMessage(valor => valor.ShouldBe("olá"))
			.AssertMessage<string>(valor => valor.ShouldBe("olá"));

		_ = produtor.Received(1).ProduceAsync(
			"pedidos",
			Arg.Is<Message<string, string>>(m => m.Key == "chave-1" && m.Value == "olá"),
			Arg.Any<CancellationToken>());
		produtor.Received(1).Flush(Arg.Any<TimeSpan>());
	}

	[Fact(DisplayName = "A shared producer belongs to the fixture and must not be disposed by the step")]
	public async Task Shared_Producer_Should_Not_Be_Disposed()
	{
		var produtor = FalsosDoKafka.ProdutorQueGrava();
		var passo = new KafkaProduceStep { Topic = "pedidos", Value = "v" };

		await passo.ExecuteAsync(FalsosDoKafka.ContextoComProdutor(produtor));

		produtor.DidNotReceive().Dispose();
	}

	[Fact(DisplayName = "A keyless produce should reach the producer with a null key")]
	public async Task Keyless_Produce_Should_Send_Null_Key()
	{
		var produtor = FalsosDoKafka.ProdutorQueGrava();
		var passo = new KafkaProduceStep { Topic = "pedidos", Value = "v" };

		var resultado = await passo.ExecuteAsync(FalsosDoKafka.ContextoComProdutor(produtor));

		resultado.Success.ShouldBeTrue();
		_ = produtor.Received(1).ProduceAsync("pedidos", Arg.Is<Message<string, string>>(m => m.Key == null), Arg.Any<CancellationToken>());
	}

	[Fact(DisplayName = "An object value should be sent as camelCase JSON without null properties")]
	public async Task Object_Value_Should_Be_CamelCase_Json()
	{
		var produtor = FalsosDoKafka.ProdutorQueGrava();
		var passo = new KafkaProduceStep
		{
			Topic = "pedidos",
			Key = new { Loja = 3 },
			Value = new Pedido { Id = 7, Descricao = "caneta" }
		};

		await passo.ExecuteAsync(FalsosDoKafka.ContextoComProdutor(produtor));

		var mensagem = MensagemEnviada(produtor);
		mensagem.Value.ShouldBe("""{"id":7,"descricao":"caneta"}""");
		mensagem.Key.ShouldBe("""{"loja":3}""");
	}

	[Fact(DisplayName = "Custom JSON options should replace the default serialization")]
	public async Task Custom_Json_Options_Should_Be_Used()
	{
		var produtor = FalsosDoKafka.ProdutorQueGrava();
		var passo = new KafkaProduceStep
		{
			Topic = "pedidos",
			Value = new Pedido { Id = 7 },
			JsonOptions = new JsonSerializerOptions()
		};

		await passo.ExecuteAsync(FalsosDoKafka.ContextoComProdutor(produtor));

		MensagemEnviada(produtor).Value.ShouldBe("""{"Id":7,"Descricao":null,"Observacao":null}""");
	}

	[Fact(DisplayName = "A partition, a timestamp and headers on the step should reach the message")]
	public async Task Step_Partition_Timestamp_And_Headers_Should_Reach_Message()
	{
		var produtor = FalsosDoKafka.ProdutorQueGrava();
		var instante = new DateTime(2026, 3, 4, 5, 6, 7, DateTimeKind.Utc);
		var passo = new KafkaProduceStep
		{
			Topic = "pedidos",
			Value = "v",
			Partition = 4,
			Timestamp = instante,
			Headers = new Headers { { "correlation-id", Encoding.UTF8.GetBytes("abc") } }
		};

		var resultado = (KafkaStepResult)await passo.ExecuteAsync(FalsosDoKafka.ContextoComProdutor(produtor));

		resultado.Success.ShouldBeTrue();
		resultado.Partition.ShouldBe(4);
		_ = produtor.Received(1).ProduceAsync(
			Arg.Is<TopicPartition>(tp => tp.Topic == "pedidos" && tp.Partition.Value == 4),
			Arg.Is<Message<string, string>>(m =>
				m.Timestamp.UtcDateTime == instante
				&& Encoding.UTF8.GetString(m.Headers.GetLastBytes("correlation-id")) == "abc"),
			Arg.Any<CancellationToken>());
	}

	// O mesmo, escrito como o usuário escreve. Hoje WithPartition/WithTimestamp/WithHeader fazem o
	// passo largar o produtor da fixture e ir para localhost:9092 (ver VerbosDoDslKafkaTests).
	[Fact(DisplayName = "A partition, a timestamp and headers set in the DSL should reach the fixture's producer")]
	public void Partition_Timestamp_And_Headers_Should_Reach_Message()
	{
		var produtor = FalsosDoKafka.ProdutorQueGrava();
		var cenario = ScenarioDsl.Given(FalsosDoKafka.ContextoComProdutor(produtor));
		var instante = new DateTime(2026, 3, 4, 5, 6, 7, DateTimeKind.Utc);

		cenario.Topic("pedidos").Produce("v")
			.WithPartition(4)
			.WithTimestamp(instante)
			.WithHeader("correlation-id", Encoding.UTF8.GetBytes("abc"))
			.When().Execute()
			.Then().AssertSuccess().AssertPartition(4);

		_ = produtor.Received(1).ProduceAsync(
			Arg.Is<TopicPartition>(tp => tp.Topic == "pedidos" && tp.Partition.Value == 4),
			Arg.Is<Message<string, string>>(m =>
				m.Timestamp.UtcDateTime == instante
				&& Encoding.UTF8.GetString(m.Headers.GetLastBytes("correlation-id")) == "abc"),
			Arg.Any<CancellationToken>());
	}

	[Fact(DisplayName = "A produce with no value should fail before reaching the producer")]
	public async Task Produce_Without_Value_Should_Fail_Early()
	{
		var produtor = FalsosDoKafka.ProdutorQueGrava();
		var passo = new KafkaProduceStep { Topic = "pedidos", Key = "k" };

		var resultado = await passo.ExecuteAsync(FalsosDoKafka.ContextoComProdutor(produtor));

		resultado.Success.ShouldBeFalse();
		resultado.Errors.ShouldContain(e => e.Contains("Message value for topic 'pedidos' is null"));
		_ = produtor.DidNotReceiveWithAnyArgs().ProduceAsync(default(string)!, default!, default);
	}

	[Fact(DisplayName = "A produce the broker never confirms should time out and carry the pending producer errors")]
	public async Task Unconfirmed_Produce_Should_Time_Out_With_Errors()
	{
		var produtor = FalsosDoKafka.ProdutorQueNaoResponde();
		var contexto = FalsosDoKafka.ContextoComProdutor(produtor, "Local: Broker transport failure");
		contexto.SetProperty("_KafkaBootstrapServers", "broker-da-fixture:9092");

		var passo = new KafkaProduceStep { Topic = "pedidos", Value = "v", Timeout = TimeSpan.FromMilliseconds(200) };

		var resultado = (KafkaStepResult)await passo.ExecuteAsync(contexto);

		resultado.Success.ShouldBeFalse();
		resultado.Errors[0].ShouldStartWith("Failed to produce message to topic 'pedidos' within timeout");
		resultado.Errors.ShouldContain("Local: Broker transport failure",
			"what the shared producer reported is the explanation of the timeout");
		resultado.GetProperty<string>("BootstrapServers").ShouldBe("broker-da-fixture:9092");
		resultado.Topic.ShouldBe("pedidos");
	}

	// ---------- O cancelamento de quem chama (#218) ----------

	[Fact(DisplayName = "A produce started with a cancelled token should send nothing and report the cancellation (#218)")]
	public async Task Produce_With_Cancelled_Token_Should_Send_Nothing()
	{
		var produtor = FalsosDoKafka.ProdutorQueGrava();
		using var cancelado = new CancellationTokenSource();
		await cancelado.CancelAsync();

		var resultado = (KafkaStepResult)await new KafkaProduceStep { Topic = "pedidos", Value = "v" }
			.ExecuteAsync(FalsosDoKafka.ContextoComProdutor(produtor), cancelado.Token);

		resultado.Success.ShouldBeFalse();
		resultado.GetProperty<string>("ExceptionType")!.ShouldContain("Canceled");
		_ = produtor.DidNotReceiveWithAnyArgs().ProduceAsync(default(string)!, default!, default);
	}

	[Fact(DisplayName = "A produce cancelled while it waits for the broker should stop, and not be reported as a timeout (#218)")]
	public async Task Produce_Cancelled_While_Waiting_Should_Stop()
	{
		var produtor = FalsosDoKafka.ProdutorQueNaoResponde();
		using var cancelamento = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

		var cronometro = System.Diagnostics.Stopwatch.StartNew();
		var resultado = (KafkaStepResult)await new KafkaProduceStep { Topic = "pedidos", Value = "v", Timeout = TimeSpan.FromMinutes(1) }
			.ExecuteAsync(FalsosDoKafka.ContextoComProdutor(produtor), cancelamento.Token);

		cronometro.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(30), "cancelling must not wait out the step timeout");
		resultado.Success.ShouldBeFalse();
		resultado.GetProperty<string>("ExceptionType")!.ShouldContain("Canceled");
		resultado.Errors.ShouldNotContain(e => e.Contains("within timeout"));
	}

	[Fact(DisplayName = "A produce the broker rejects should fail with the broker's reason")]
	public void Rejected_Produce_Should_Fail_With_Reason()
	{
		var produtor = FalsosDoKafka.ProdutorQueFalha("mensagem grande demais");
		var cenario = ScenarioDsl.Given(FalsosDoKafka.ContextoComProdutor(produtor));

		var resultado = cenario.Topic("pedidos").Produce("v")
			.When().Execute()
			.Then().AssertFailure()
			.GetResult();

		resultado.GetProperty<string>("ExceptionMessage").ShouldBe("mensagem grande demais");
		resultado.GetProperty<string>("ExceptionType").ShouldBe(typeof(KafkaException).FullName);
		// Até a 6.30.6 a falha descartava o diagnóstico juntado antes dela, como o broker usado.
		resultado.GetProperty<string>("BootstrapServers").ShouldNotBeNullOrEmpty();
	}

	[Fact(DisplayName = "ValidateProduceSuccess should fail the scenario when the message was not persisted")]
	public void ValidateProduceSuccess_Should_Fail_When_Not_Persisted()
	{
		var produtor = FalsosDoKafka.ProdutorQueGrava(status: PersistenceStatus.PossiblyPersisted);
		var cenario = ScenarioDsl.Given(FalsosDoKafka.ContextoComProdutor(produtor));

		var erro = Should.Throw<InvalidOperationException>(
			() => cenario.Topic("pedidos").Produce("v").ValidateProduceSuccess().When().Execute());

		// A entrega incerta não é sucesso; a mensagem de erro diz qual verificação reprovou.
		erro.Message.ShouldStartWith("Produce operation failed");
	}

	[Fact(DisplayName = "Produce validations should fail with a message that names the mismatch")]
	public void Produce_Validations_Should_Name_Mismatch()
	{
		var produtor = FalsosDoKafka.ProdutorQueGrava(particao: 1, offset: 5);

		Falha(produtor, c => c.ValidatePartition(2)).ShouldBe("Expected partition 2 but got 1");
		Falha(produtor, c => c.ValidateOffset(o => o > 10)).ShouldBe("Offset validation failed for offset 5");
		Falha(produtor, c => c.ValidateDeliveryStatus(PersistenceStatus.NotPersisted))
			.ShouldBe("Expected delivery status NotPersisted but got Persisted");
	}

	[Fact(DisplayName = "Produce validations should say when the result has no delivery information")]
	public void Produce_Validations_Should_Say_When_Information_Is_Missing()
	{
		// Um resultado de falha não tem partição, offset nem status.
		var produtor = FalsosDoKafka.ProdutorQueFalha("fora do ar");

		Falha(produtor, c => c.ValidatePartition(0)).ShouldBe("Partition information is not available in the result.");
		Falha(produtor, c => c.ValidateOffset(_ => true)).ShouldBe("Offset information is not available in the result.");
		Falha(produtor, c => c.ValidateDeliveryStatus(PersistenceStatus.Persisted)).ShouldBe("Delivery status is not available in the result.");
	}

	[Fact(DisplayName = "ValidateOffset should reject a null predicate")]
	public void ValidateOffset_Should_Reject_Null()
	{
		Should.Throw<ArgumentNullException>(() => ScenarioDsl.Given().Topic("t").Produce("v").ValidateOffset(null!));
	}

	// ---------- Quando o produtor compartilhado não deve ser usado ----------

	[Fact(DisplayName = "An explicit broker or credential should bypass the fixture's shared producer")]
	public async Task Explicit_Settings_Should_Bypass_Shared_Producer()
	{
		var passos = new (string Nome, KafkaProduceStep Passo)[]
		{
			("BootstrapServers", new KafkaProduceStep { Topic = "t", Value = "v", BootstrapServers = "127.0.0.1:59099", Timeout = TimeSpan.FromMilliseconds(300) }),
			("AuthConfig", new KafkaProduceStep { Topic = "t", Value = "v", BootstrapServers = "127.0.0.1:59099", Timeout = TimeSpan.FromMilliseconds(300), AuthConfig = new XUnitAssured.Kafka.Configuration.KafkaAuthConfig() }),
		};

		foreach (var (nome, passo) in passos)
		{
			var produtor = FalsosDoKafka.ProdutorQueGrava();

			var resultado = await passo.ExecuteAsync(FalsosDoKafka.ContextoComProdutor(produtor));

			_ = produtor.DidNotReceiveWithAnyArgs().ProduceAsync(default(string)!, default!, default);
			resultado.Success.ShouldBeFalse($"{nome}: the step talked to the unreachable broker it was given");
		}
	}

	// ---------- Produção em lote ----------

	[Fact(DisplayName = "A batch produce should send every message and report the batch")]
	public void Batch_Produce_Should_Send_Every_Message()
	{
		var produtor = FalsosDoKafka.ProdutorQueGrava();
		var cenario = ScenarioDsl.Given(FalsosDoKafka.ContextoComProdutor(produtor));
		var pares = new[]
		{
			new KeyValuePair<object, object>("k1", "v1"),
			new KeyValuePair<object, object>(10, new Pedido { Id = 2 }),
		};

		var resultado = cenario.Topic("pedidos").ProduceBatch(pares)
			.When().Execute()
			.Then().AssertSuccess().AssertTopic("pedidos").AssertBatchCount(2)
			.GetResult();

		resultado.GetProperty<DeliveryResult<string, string>[]>("DeliveryResults")!.Length.ShouldBe(2);

		var enviadas = MensagensEnviadas(produtor);
		enviadas.Select(m => (m.Key, m.Value)).OrderBy(m => m.Key).ShouldBe(new[]
		{
			("10", """{"id":2}"""),
			("k1", "v1"),
		});
		produtor.Received(1).Flush(Arg.Any<TimeSpan>());
		produtor.DidNotReceive().Dispose();
	}

	[Fact(DisplayName = "AssertBatchCount should fail with the actual count")]
	public void AssertBatchCount_Should_Fail_With_Actual_Count()
	{
		var produtor = FalsosDoKafka.ProdutorQueGrava();
		var cenario = ScenarioDsl.Given(FalsosDoKafka.ContextoComProdutor(produtor));

		var validacao = cenario.Topic("pedidos").ProduceBatch(new object[] { "a", "b" }).When().Execute();

		Should.Throw<ShouldAssertException>(() => validacao.AssertBatchCount(3))
			.Message.ShouldContain("Expected batch size 3 but got 2");
	}

	[Fact(DisplayName = "Each message of a batch should get its own copy of the headers")]
	public async Task Batch_Headers_Should_Be_Copied_Per_Message()
	{
		var produtor = FalsosDoKafka.ProdutorQueGrava();
		var cabecalhos = new Headers { { "origem", Encoding.UTF8.GetBytes("lote") } };
		var passo = new KafkaBatchProduceStep
		{
			Topic = "pedidos",
			Headers = cabecalhos,
			Messages = new List<KeyValuePair<object?, object>> { new(null, "a"), new(null, "b") }
		};

		await passo.ExecuteAsync(FalsosDoKafka.ContextoComProdutor(produtor));

		var enviadas = MensagensEnviadas(produtor);
		enviadas.Count.ShouldBe(2);
		enviadas.ShouldAllBe(m => Encoding.UTF8.GetString(m.Headers.GetLastBytes("origem")) == "lote");
		enviadas[0].Headers.ShouldNotBeSameAs(enviadas[1].Headers, "Headers is mutable; sharing one instance across messages leaks changes");
		enviadas[0].Headers.ShouldNotBeSameAs(cabecalhos);
	}

	[Fact(DisplayName = "Batch JSON options should replace the default serialization")]
	public async Task Batch_Json_Options_Should_Be_Used()
	{
		var produtor = FalsosDoKafka.ProdutorQueGrava();
		var passo = new KafkaBatchProduceStep
		{
			Topic = "pedidos",
			JsonOptions = new JsonSerializerOptions(),
			Messages = new List<KeyValuePair<object?, object>> { new("k", new Pedido { Id = 1 }) }
		};

		await passo.ExecuteAsync(FalsosDoKafka.ContextoComProdutor(produtor));

		MensagensEnviadas(produtor).Single().Value.ShouldBe("""{"Id":1,"Descricao":null,"Observacao":null}""");
	}

	// O passo simples manda a chave nula, e o broker distribui as mensagens entre as partições.
	// O lote troca nula por "", que é uma chave: todas as mensagens caem na mesma partição.
	[Fact(DisplayName = "A keyless batch should reach the producer with null keys, like a single produce")]
	public async Task Keyless_Batch_Should_Send_Null_Keys()
	{
		var produtor = FalsosDoKafka.ProdutorQueGrava();
		var cenario = ScenarioDsl.Given(FalsosDoKafka.ContextoComProdutor(produtor));

		await cenario.Topic("pedidos").ProduceBatch(new object[] { "a", "b" }).ExecuteAsync();

		MensagensEnviadas(produtor).ShouldAllBe(m => m.Key == null);
	}

	[Fact(DisplayName = "A batch the broker never confirms should time out")]
	public async Task Unconfirmed_Batch_Should_Time_Out()
	{
		var produtor = FalsosDoKafka.ProdutorQueNaoResponde();
		var passo = new KafkaBatchProduceStep
		{
			Topic = "pedidos",
			Timeout = TimeSpan.FromMilliseconds(200),
			Messages = new List<KeyValuePair<object?, object>> { new("k", "a") }
		};

		var resultado = (KafkaStepResult)await passo.ExecuteAsync(FalsosDoKafka.ContextoComProdutor(produtor));

		resultado.Success.ShouldBeFalse();
		resultado.Errors.ShouldContain(e => e.StartsWith("Failed to produce message to topic 'pedidos' within timeout"));
		resultado.Topic.ShouldBe("pedidos");
	}

	[Fact(DisplayName = "A batch started with a cancelled token should send nothing and report the cancellation (#218)")]
	public async Task Batch_With_Cancelled_Token_Should_Send_Nothing()
	{
		var produtor = FalsosDoKafka.ProdutorQueGrava();
		using var cancelado = new CancellationTokenSource();
		await cancelado.CancelAsync();
		var passo = new KafkaBatchProduceStep
		{
			Topic = "pedidos",
			Messages = new List<KeyValuePair<object?, object>> { new("k", "a"), new("k", "b") }
		};

		var resultado = (KafkaStepResult)await passo.ExecuteAsync(FalsosDoKafka.ContextoComProdutor(produtor), cancelado.Token);

		resultado.Success.ShouldBeFalse();
		resultado.GetProperty<string>("ExceptionType")!.ShouldContain("Canceled");
		_ = produtor.DidNotReceiveWithAnyArgs().ProduceAsync(default(string)!, default!, default);
	}

	[Fact(DisplayName = "A batch cancelled while it waits for the broker should stop, and not be reported as a timeout (#218)")]
	public async Task Batch_Cancelled_While_Waiting_Should_Stop()
	{
		var produtor = FalsosDoKafka.ProdutorQueNaoResponde();
		using var cancelamento = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
		var passo = new KafkaBatchProduceStep
		{
			Topic = "pedidos",
			Timeout = TimeSpan.FromMinutes(1),
			Messages = new List<KeyValuePair<object?, object>> { new("k", "a") }
		};

		var cronometro = System.Diagnostics.Stopwatch.StartNew();
		var resultado = (KafkaStepResult)await passo.ExecuteAsync(FalsosDoKafka.ContextoComProdutor(produtor), cancelamento.Token);

		cronometro.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(30), "cancelling must not wait out the step timeout");
		resultado.Success.ShouldBeFalse();
		resultado.GetProperty<string>("ExceptionType")!.ShouldContain("Canceled");
		resultado.Errors.ShouldNotContain(e => e.Contains("within timeout"));
	}

	[Fact(DisplayName = "A batch the broker rejects should fail with the broker's reason")]
	public async Task Rejected_Batch_Should_Fail()
	{
		var produtor = FalsosDoKafka.ProdutorQueFalha("tópico só leitura");
		var passo = new KafkaBatchProduceStep
		{
			Topic = "pedidos",
			Messages = new List<KeyValuePair<object?, object>> { new("k", "a") }
		};

		var resultado = await passo.ExecuteAsync(FalsosDoKafka.ContextoComProdutor(produtor));

		resultado.Success.ShouldBeFalse();
		resultado.GetProperty<string>("ExceptionMessage").ShouldBe("tópico só leitura");
	}

	[Fact(DisplayName = "A batch with explicit producer configuration should not use the shared producer")]
	public async Task Batch_With_Producer_Config_Should_Bypass_Shared_Producer()
	{
		var produtor = FalsosDoKafka.ProdutorQueGrava();
		var passo = new KafkaBatchProduceStep
		{
			Topic = "pedidos",
			Timeout = TimeSpan.FromMilliseconds(300),
			ProducerConfig = new ProducerConfig { BootstrapServers = "127.0.0.1:59099" },
			Messages = new List<KeyValuePair<object?, object>> { new("k", "a") }
		};

		var resultado = await passo.ExecuteAsync(FalsosDoKafka.ContextoComProdutor(produtor));

		_ = produtor.DidNotReceiveWithAnyArgs().ProduceAsync(default(string)!, default!, default);
		resultado.Success.ShouldBeFalse("the configured broker is unreachable");
	}

	// O passo simples deixa de usar o produtor compartilhado quando o usuário informa endereço ou
	// credencial (KafkaProduceStep.cs:166-170). O lote só olha o ProducerConfig.
	[Fact(DisplayName = "A batch with an explicit broker or credential should not use the shared producer")]
	public async Task Batch_With_Explicit_Broker_Should_Bypass_Shared_Producer()
	{
		var produtor = FalsosDoKafka.ProdutorQueGrava();
		var passo = new KafkaBatchProduceStep
		{
			Topic = "pedidos",
			Timeout = TimeSpan.FromMilliseconds(300),
			BootstrapServers = "127.0.0.1:59099",
			Messages = new List<KeyValuePair<object?, object>> { new("k", "a") }
		};

		await passo.ExecuteAsync(FalsosDoKafka.ContextoComProdutor(produtor));

		_ = produtor.DidNotReceiveWithAnyArgs().ProduceAsync(default(string)!, default!, default);
	}

	// ---------- Validate / Validate antes de executar ----------

	[Fact(DisplayName = "Validating a step that has not run should say so")]
	public void Validate_Before_Execute_Should_Say_So()
	{
		var passos = new XUnitAssured.Core.Abstractions.ITestStep[]
		{
			new KafkaProduceStep(), new KafkaBatchProduceStep(), new KafkaConsumeStep(), new KafkaBatchConsumeStep()
		};

		foreach (var passo in passos)
		{
			Should.Throw<InvalidOperationException>(() => passo.Validate(_ => { }), passo.GetType().Name)
				.Message.ShouldContain("must be executed before validation");
			passo.IsExecuted.ShouldBeFalse();
			passo.StepType.ShouldBe("Kafka");
		}
	}

	[Fact(DisplayName = "A failing validation should mark the step invalid, a passing one valid")]
	public async Task Validation_Should_Mark_Step_Validity()
	{
		var produtor = FalsosDoKafka.ProdutorQueGrava();
		var contexto = FalsosDoKafka.ContextoComProdutor(produtor);

		var simples = new KafkaProduceStep { Topic = "t", Value = "v" };
		var lote = new KafkaBatchProduceStep { Topic = "t", Messages = new List<KeyValuePair<object?, object>> { new("k", "v") } };
		await simples.ExecuteAsync(contexto);
		await lote.ExecuteAsync(contexto);

		foreach (var passo in new XUnitAssured.Core.Abstractions.ITestStep[] { simples, lote })
		{
			passo.IsExecuted.ShouldBeTrue();
			Should.Throw<InvalidOperationException>(() => passo.Validate(_ => throw new InvalidOperationException("reprovado")));
			passo.IsValid.ShouldBeFalse();
			passo.Validate(_ => { });
			passo.IsValid.ShouldBeTrue();
		}
	}

	[Fact(DisplayName = "Kafka validations should refuse a result from another package")]
	public void Kafka_Validations_Should_Refuse_Other_Results()
	{
		var cenario = ScenarioDsl.Given();
		cenario.SetCurrentStep(new PassoComResultado(XUnitAssured.Core.Results.TestStepResult.CreateSuccess("x")));

		Should.Throw<InvalidOperationException>(() => cenario.Validate((KafkaStepResult _) => { }))
			.Message.ShouldBe("Expected KafkaStepResult but got TestStepResult");
		Should.Throw<InvalidOperationException>(() => cenario.ValidateMessage<Pedido>(_ => { }))
			.Message.ShouldBe("Expected KafkaStepResult but got TestStepResult");
	}

	[Fact(DisplayName = "Kafka validations should reject null arguments")]
	public void Kafka_Validations_Should_Reject_Nulls()
	{
		Should.Throw<ArgumentNullException>(() => KafkaValidationExtensions.Validate(null!, _ => { }));
		Should.Throw<ArgumentNullException>(() => ScenarioDsl.Given().Topic("t").Consume().Validate((Action<KafkaStepResult>)null!));
		Should.Throw<ArgumentNullException>(() => KafkaValidationExtensions.ValidateMessage<Pedido>(null!, _ => { }));
		Should.Throw<ArgumentNullException>(() => ScenarioDsl.Given().Topic("t").Consume().ValidateMessage<Pedido>(null!));
	}

	private static string Falha(IProducer<string, string> produtor, Func<XUnitAssured.Core.Abstractions.ITestScenario, XUnitAssured.Core.Abstractions.ITestScenario> validar)
	{
		var cenario = ScenarioDsl.Given(FalsosDoKafka.ContextoComProdutor(produtor));
		var kafka = cenario.Topic("pedidos").Produce("v");
		validar(kafka);

		return Should.Throw<InvalidOperationException>(() => kafka.When().Execute()).Message;
	}

	private static Message<string, string> MensagemEnviada(IProducer<string, string> produtor) =>
		MensagensEnviadas(produtor).Single();

	private static List<Message<string, string>> MensagensEnviadas(IProducer<string, string> produtor) =>
		produtor.ReceivedCalls()
			.Where(c => c.GetMethodInfo().Name == nameof(IProducer<string, string>.ProduceAsync))
			.Select(c => (Message<string, string>)c.GetArguments()[1]!)
			.ToList();

	/// <summary>
	/// Um passo que já rodou, com o resultado dado; serve para exercitar as verificações do DSL
	/// sobre um resultado exato.
	/// </summary>
	private sealed class PassoComResultado : XUnitAssured.Core.Abstractions.ITestStep
	{
		public PassoComResultado(XUnitAssured.Core.Results.ITestStepResult resultado) => Result = resultado;

		public string? Name => null;

		public string StepType => "Teste";

		public XUnitAssured.Core.Results.ITestStepResult? Result { get; }

		public bool IsExecuted => true;

		public bool IsValid { get; private set; }

		public Task<XUnitAssured.Core.Results.ITestStepResult> ExecuteAsync(
			XUnitAssured.Core.Abstractions.ITestContext context, CancellationToken cancellationToken = default) =>
			Task.FromResult(Result!);

		public void Validate(Action<XUnitAssured.Core.Results.ITestStepResult> validation)
		{
			validation(Result!);
			IsValid = true;
		}
	}
}
