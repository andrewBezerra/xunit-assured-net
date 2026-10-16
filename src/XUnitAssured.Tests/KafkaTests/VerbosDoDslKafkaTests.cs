using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;

using Confluent.Kafka;

using XUnitAssured.Core.Abstractions;
using XUnitAssured.Core.DSL;
using XUnitAssured.Kafka.Abstractions;
using XUnitAssured.Kafka.Configuration;
using XUnitAssured.Kafka.Steps;

namespace XUnitAssured.Tests.KafkaTests;

[Trait("Category", "Kafka")]
[Trait("Component", "DSL")]
/// <summary>
/// O que cada verbo do DSL Kafka deixa no passo, nos quatro tipos de passo.
///
/// <para>
/// Os verbos de lote (<c>ProduceBatch</c>, <c>ConsumeBatch</c>) e os ajustes aplicados a eles não
/// tinham teste nenhum: um verbo que esquecesse um dos ramos, ou trocasse o passo de lote por um
/// passo simples, passaria sem ninguém ver. Aqui se olha o passo que a cadeia monta, sem broker.
/// </para>
/// </summary>
public class VerbosDoDslKafkaTests
{
	// ---------- Topic / Consume / ConsumeBatch ----------

	[Fact(DisplayName = "Topic should reject a null scenario")]
	public void Topic_Should_Reject_Null_Scenario()
	{
		Should.Throw<ArgumentNullException>(() => KafkaScenarioExtensions.Topic(null!, "pedidos"));
	}

	[Fact(DisplayName = "Topic should hand back a typed Kafka chain")]
	public void Topic_Should_Return_Typed_Chain()
	{
		ScenarioDsl.Given().Topic("pedidos").ShouldBeAssignableTo<IKafkaScenario>();
	}

	[Fact(DisplayName = "Every verb that starts a step should require Topic() first")]
	public void Starting_Verbs_Should_Require_Topic()
	{
		var verbos = new (string Nome, Action<ITestScenario> Aplicar)[]
		{
			("Consume", c => c.Consume()),
			("ConsumeBatch", c => c.ConsumeBatch(2)),
			("Produce(value)", c => c.Produce("v")),
			("Produce(key, value)", c => c.Produce("k", "v")),
			("ProduceBatch(values)", c => c.ProduceBatch(new object[] { "a" })),
			("ProduceBatch(pairs)", c => c.ProduceBatch(new[] { new KeyValuePair<object, object>("k", "v") })),
		};

		foreach (var (nome, aplicar) in verbos)
		{
			var erro = Should.Throw<InvalidOperationException>(() => aplicar(ScenarioDsl.Given()), nome);
			erro.Message.ShouldContain("Call Topic() first", customMessage: nome);
		}
	}

	[Theory(DisplayName = "ConsumeBatch should reject a count that is not positive")]
	[InlineData(0)]
	[InlineData(-3)]
	public void ConsumeBatch_Should_Reject_Non_Positive_Count(int quantidade)
	{
		var erro = Should.Throw<ArgumentOutOfRangeException>(
			() => ScenarioDsl.Given().Topic("pedidos").ConsumeBatch(quantidade));

		erro.ParamName.ShouldBe("count");
	}

	[Fact(DisplayName = "ConsumeBatch should build a batch consume step for the topic and count")]
	public void ConsumeBatch_Should_Build_Batch_Step()
	{
		var passo = ScenarioDsl.Given().Topic("pedidos").ConsumeBatch(5).CurrentStep
			.ShouldBeOfType<KafkaBatchConsumeStep>();

		passo.Topic.ShouldBe("pedidos");
		passo.MessageCount.ShouldBe(5);
		passo.Timeout.ShouldBe(TimeSpan.FromSeconds(60));
	}

	// ---------- Produce / ProduceBatch ----------

	[Fact(DisplayName = "Produce with a key should keep both the key and the value")]
	public void Produce_With_Key_Should_Keep_Both()
	{
		var passo = ScenarioDsl.Given().Topic("pedidos").Produce("chave-1", new { Id = 7 }).CurrentStep
			.ShouldBeOfType<KafkaProduceStep>();

		passo.Topic.ShouldBe("pedidos");
		passo.Key.ShouldBe("chave-1");
		passo.Value.ShouldNotBeNull();
	}

	[Fact(DisplayName = "ProduceBatch with values should build keyless messages in order")]
	public void ProduceBatch_Values_Should_Build_Keyless_Messages()
	{
		var passo = ScenarioDsl.Given().Topic("pedidos").ProduceBatch(new object[] { "a", "b", "c" }).CurrentStep
			.ShouldBeOfType<KafkaBatchProduceStep>();

		passo.Topic.ShouldBe("pedidos");
		passo.Messages.Select(m => m.Value).ShouldBe(new object[] { "a", "b", "c" });
		passo.Messages.ShouldAllBe(m => m.Key == null);
	}

	[Fact(DisplayName = "ProduceBatch with pairs should keep each key with its value")]
	public void ProduceBatch_Pairs_Should_Keep_Keys()
	{
		var pares = new[]
		{
			new KeyValuePair<object, object>("k1", "v1"),
			new KeyValuePair<object, object>("k2", "v2"),
		};

		var passo = ScenarioDsl.Given().Topic("pedidos").ProduceBatch(pares).CurrentStep
			.ShouldBeOfType<KafkaBatchProduceStep>();

		passo.Messages.Select(m => (m.Key, m.Value))
			.ShouldBe(new (object?, object)[] { ("k1", "v1"), ("k2", "v2") });
	}

	// ---------- Ajustes nos passos de lote ----------

	[Fact(DisplayName = "Consume settings should apply to a batch consume step and keep it a batch")]
	public void Consume_Settings_Should_Apply_To_Batch_Consume()
	{
		var config = new ConsumerConfig { GroupId = "explicito" };

		var passo = ScenarioDsl.Given().Topic("pedidos").ConsumeBatch(3)
			.WithSchema(typeof(int))
			.WithTimeout(TimeSpan.FromSeconds(9))
			.WithBootstrapServers("broker:19092")
			.WithGroupId("grupo")
			.WithConsumerConfig(config)
			.WithAuth(a => a.UseSaslPlain("ana", "segredo"))
			.CurrentStep.ShouldBeOfType<KafkaBatchConsumeStep>();

		passo.MessageCount.ShouldBe(3, "adjusting the step must not lose how many messages to wait for");
		passo.SchemaType.ShouldBe(typeof(int));
		passo.Timeout.ShouldBe(TimeSpan.FromSeconds(9));
		passo.BootstrapServers.ShouldBe("broker:19092");
		passo.GroupId.ShouldBe("grupo");
		passo.ConsumerConfig.ShouldBeSameAs(config);
		passo.AuthConfig!.SaslPlain!.Username.ShouldBe("ana");
	}

	[Fact(DisplayName = "Produce settings should apply to a batch produce step and keep its messages")]
	public void Produce_Settings_Should_Apply_To_Batch_Produce()
	{
		var cabecalhos = new Headers { { "origem", Encoding.UTF8.GetBytes("teste") } };
		var config = new ProducerConfig { Acks = Acks.All };
		var opcoes = new JsonSerializerOptions();

		var passo = ScenarioDsl.Given().Topic("pedidos").ProduceBatch(new object[] { "a", "b" })
			.WithTimeout(TimeSpan.FromSeconds(11))
			.WithBootstrapServers("broker:19092")
			.WithHeaders(cabecalhos)
			.WithProducerConfig(config)
			.WithJsonOptions(opcoes)
			.WithAuth(a => a.UseSaslScram512("ana", "segredo"))
			.CurrentStep.ShouldBeOfType<KafkaBatchProduceStep>();

		passo.Timeout.ShouldBe(TimeSpan.FromSeconds(11));
		passo.BootstrapServers.ShouldBe("broker:19092");
		passo.Headers.ShouldBeSameAs(cabecalhos);
		passo.ProducerConfig.ShouldBeSameAs(config);
		passo.JsonOptions.ShouldBeSameAs(opcoes);
		passo.AuthConfig!.Type.ShouldBe(KafkaAuthenticationType.SaslScram512);
		passo.Topic.ShouldBe("pedidos");
	}

	// Até a 6.30.6 o construtor de cópia do lote não copiava Messages, e ajustar o prazo produzia um
	// lote vazio.
	[Fact(DisplayName = "Adjusting a batch produce step should not drop its messages")]
	public void Adjusting_Batch_Produce_Should_Not_Drop_Messages()
	{
		var passo = ScenarioDsl.Given().Topic("pedidos").ProduceBatch(new object[] { "a", "b" })
			.WithTimeout(TimeSpan.FromSeconds(11))
			.CurrentStep.ShouldBeOfType<KafkaBatchProduceStep>();

		passo.Messages.Count.ShouldBe(2, "a batch adjusted after being described must still carry its messages");
	}

	// Um lote vazio quebrava só na execução, com IndexOutOfRangeException ao montar o resultado.
	[Fact(DisplayName = "An empty batch should be refused when it is described")]
	public void Empty_Batch_Should_Be_Refused()
	{
		Should.Throw<ArgumentException>(() => ScenarioDsl.Given().Topic("pedidos").ProduceBatch(Array.Empty<object>()))
			.Message.ShouldContain("at least one message");
		Should.Throw<ArgumentException>(() => ScenarioDsl.Given().Topic("pedidos").ProduceBatch(new List<KeyValuePair<object, object>>()))
			.Message.ShouldContain("at least one message");
	}

	// ---------- Ajustes no passo de produção simples ----------

	[Fact(DisplayName = "Produce settings should apply to a single produce step")]
	public void Produce_Settings_Should_Apply_To_Single_Produce()
	{
		var config = new ProducerConfig { Acks = Acks.Leader };
		var opcoes = new JsonSerializerOptions();
		var instante = new DateTime(2026, 5, 6, 7, 8, 9, DateTimeKind.Utc);

		var passo = ScenarioDsl.Given().Topic("pedidos").Produce("valor")
			.WithKey("chave")
			.WithPartition(3)
			.WithTimestamp(instante)
			.WithHeader("correlation-id", Encoding.UTF8.GetBytes("abc"))
			.WithProducerConfig(config)
			.WithJsonOptions(opcoes)
			.WithTimeout(TimeSpan.FromSeconds(4))
			.WithBootstrapServers("broker:19092")
			.CurrentStep.ShouldBeOfType<KafkaProduceStep>();

		passo.Key.ShouldBe("chave");
		passo.Value.ShouldBe("valor");
		passo.Partition.ShouldBe(3);
		passo.Timestamp.ShouldBe(instante);
		Encoding.UTF8.GetString(passo.Headers!.GetLastBytes("correlation-id")).ShouldBe("abc");
		passo.ProducerConfig.ShouldBeSameAs(config);
		passo.JsonOptions.ShouldBeSameAs(opcoes);
		passo.Timeout.ShouldBe(TimeSpan.FromSeconds(4));
		passo.BootstrapServers.ShouldBe("broker:19092");
	}

	[Fact(DisplayName = "WithHeader called twice should keep both headers")]
	public void WithHeader_Twice_Should_Keep_Both()
	{
		var passo = ScenarioDsl.Given().Topic("pedidos").Produce("valor")
			.WithHeader("a", Encoding.UTF8.GetBytes("1"))
			.WithHeader("b", Encoding.UTF8.GetBytes("2"))
			.CurrentStep.ShouldBeOfType<KafkaProduceStep>();

		passo.Headers!.Select(h => h.Key).ShouldBe(new[] { "a", "b" });
	}

	[Fact(DisplayName = "Field-by-field produce verbs should keep the authentication and the rest")]
	public void Field_By_Field_Verbs_Should_Keep_Settings()
	{
		var verbos = new (string Nome, Func<ITestScenario, ITestScenario> Aplicar)[]
		{
			("WithKey", c => c.WithKey("k")),
			("WithHeader", c => c.WithHeader("h", new byte[] { 1 })),
			("WithPartition", c => c.WithPartition(1)),
			("WithTimestamp", c => c.WithTimestamp(DateTime.UtcNow)),
		};

		foreach (var (nome, aplicar) in verbos)
		{
			var cenario = ScenarioDsl.Given().Topic("pedidos").Produce("valor")
				.WithTimeout(TimeSpan.FromSeconds(4))
				.WithAuth(a => a.UseSaslPlain("ana", "segredo"));

			var passo = aplicar(cenario).CurrentStep.ShouldBeOfType<KafkaProduceStep>(nome);

			passo.Value.ShouldBe("valor", nome);
			passo.Timeout.ShouldBe(TimeSpan.FromSeconds(4), nome);
			passo.AuthConfig!.SaslPlain!.Username.ShouldBe("ana", nome);
		}
	}

	// Um passo em que ninguém informou o endereço deve continuar usando o da fixture depois de
	// um verbo que reconstrói o passo. Estes quatro reconstroem campo a campo e copiam a
	// PROPRIEDADE BootstrapServers, que devolve o padrão "localhost:9092"; o passo passa a achar
	// que o endereço foi informado. É a armadilha que PrecedenciaDeConfiguracaoTests descreve.
	[Fact(DisplayName = "Field-by-field produce verbs should not turn the default broker into an explicit one")]
	public void Field_By_Field_Verbs_Should_Not_Invent_Explicit_Broker()
	{
		var verbos = new (string Nome, Func<ITestScenario, ITestScenario> Aplicar)[]
		{
			("WithKey", c => c.WithKey("k")),
			("WithHeader", c => c.WithHeader("h", new byte[] { 1 })),
			("WithPartition", c => c.WithPartition(1)),
			("WithTimestamp", c => c.WithTimestamp(DateTime.UtcNow)),
		};

		foreach (var (nome, aplicar) in verbos)
		{
			var cenario = ScenarioDsl.Given();
			cenario.Context.SetProperty("_KafkaBootstrapServers", "broker-da-fixture:9092");

			var passo = aplicar(cenario.Topic("pedidos").Produce("valor")).CurrentStep
				.ShouldBeOfType<KafkaProduceStep>(nome);

			passo.MontarConfig(cenario.Context).BootstrapServers.ShouldBe("broker-da-fixture:9092", nome);
		}
	}

	// ---------- Verbos no passo errado ----------

	[Fact(DisplayName = "Produce-only verbs should refuse a consume step")]
	public void Produce_Only_Verbs_Should_Refuse_Consume_Step()
	{
		var verbos = new (string Nome, Action<ITestScenario> Aplicar)[]
		{
			("WithKey", c => c.WithKey("k")),
			("WithHeaders", c => c.WithHeaders(new Headers())),
			("WithHeader", c => c.WithHeader("h", new byte[] { 1 })),
			("WithPartition", c => c.WithPartition(0)),
			("WithTimestamp", c => c.WithTimestamp(DateTime.UtcNow)),
			("WithProducerConfig", c => c.WithProducerConfig(new ProducerConfig())),
			("WithJsonOptions", c => c.WithJsonOptions(new JsonSerializerOptions())),
		};

		foreach (var (nome, aplicar) in verbos)
		{
			var erro = Should.Throw<InvalidOperationException>(
				() => aplicar(ScenarioDsl.Given().Topic("pedidos").Consume()), nome);
			erro.Message.ShouldBe("Current step is not a Kafka produce step.", nome);
		}
	}

	[Fact(DisplayName = "Consume-only verbs should refuse a produce step")]
	public void Consume_Only_Verbs_Should_Refuse_Produce_Step()
	{
		var verbos = new (string Nome, Action<ITestScenario> Aplicar)[]
		{
			("WithSchema", c => c.WithSchema(typeof(int))),
			("WithGroupId", c => c.WithGroupId("g")),
			("WithConsumerConfig", c => c.WithConsumerConfig(new ConsumerConfig())),
		};

		foreach (var (nome, aplicar) in verbos)
		{
			var erro = Should.Throw<InvalidOperationException>(
				() => aplicar(ScenarioDsl.Given().Topic("pedidos").Produce("v")), nome);
			erro.Message.ShouldBe("Current step is not a Kafka consume step.", nome);
		}
	}

	[Fact(DisplayName = "Verbs shared by every Kafka step should refuse a scenario with no Kafka step")]
	public void Shared_Verbs_Should_Refuse_Non_Kafka_Step()
	{
		var verbos = new (string Nome, Action<ITestScenario> Aplicar)[]
		{
			("WithTimeout", c => c.WithTimeout(TimeSpan.FromSeconds(1))),
			("WithBootstrapServers", c => c.WithBootstrapServers("b:1")),
			("WithAuth", c => c.WithAuth(_ => { })),
		};

		foreach (var (nome, aplicar) in verbos)
		{
			var erro = Should.Throw<InvalidOperationException>(() => aplicar(ScenarioDsl.Given()), nome);
			erro.Message.ShouldBe("Current step is not a Kafka step.", nome);
		}
	}

	// ---------- WithAuth ----------

	[Fact(DisplayName = "WithAuth should reject a null configuration action")]
	public void WithAuth_Should_Reject_Null_Action()
	{
		Should.Throw<ArgumentNullException>(
			() => ScenarioDsl.Given().Topic("pedidos").Produce("v").WithAuth(null!));
	}

	[Fact(DisplayName = "WithAuth should configure every kind of Kafka step and keep its kind")]
	public void WithAuth_Should_Configure_Every_Step_Kind()
	{
		var cenarios = new (Type Tipo, Func<ITestScenario> Montar)[]
		{
			(typeof(KafkaProduceStep), () => ScenarioDsl.Given().Topic("t").Produce("v")),
			(typeof(KafkaBatchProduceStep), () => ScenarioDsl.Given().Topic("t").ProduceBatch(new object[] { "v" })),
			(typeof(KafkaConsumeStep), () => ScenarioDsl.Given().Topic("t").Consume()),
			(typeof(KafkaBatchConsumeStep), () => ScenarioDsl.Given().Topic("t").ConsumeBatch(2)),
		};

		foreach (var (tipo, montar) in cenarios)
		{
			var passo = montar().WithAuth(a => a.UseSsl("/certs/ca.pem")).CurrentStep!;

			passo.GetType().ShouldBe(tipo);
			var auth = (KafkaAuthConfig?)tipo.GetProperty("AuthConfig")!.GetValue(passo);
			auth.ShouldNotBeNull(tipo.Name);
			auth!.Type.ShouldBe(KafkaAuthenticationType.Ssl, tipo.Name);
			auth.Ssl!.SslCaLocation.ShouldBe("/certs/ca.pem", tipo.Name);
		}
	}
}
