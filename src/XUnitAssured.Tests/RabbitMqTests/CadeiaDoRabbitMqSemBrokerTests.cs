using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using NSubstitute;

using Shouldly;
using Xunit;

using XUnitAssured.Core.Abstractions;
using XUnitAssured.Core.DSL;
using XUnitAssured.RabbitMq.Abstractions;
using XUnitAssured.RabbitMq.Extensions;
using XUnitAssured.RabbitMq.Steps;

namespace XUnitAssured.Tests.RabbitMqTests;

[Trait("Category", "RabbitMq")]
[Trait("Component", "DSL")]
/// <summary>
/// A cadeia inteira, da descrição à asserção, contra o broker de mentira.
///
/// <para>
/// <c>VerbosDoRabbitMqTests</c> verifica o passo que a cadeia descreve; aqui a cadeia também
/// executa, e o que se afirma é o que quem usa o pacote vê: o construtor de asserções, as
/// mensagens de erro dos verbos chamados fora de ordem, e que cada verbo preserva o que os
/// anteriores configuraram.
/// </para>
/// </summary>
public class CadeiaDoRabbitMqSemBrokerTests
{
	private sealed class Pedido
	{
		public int Id { get; set; }

		public string? Cliente { get; set; }
	}

	private static IRabbitMqScenario Ligada(IRabbitMqScenario cenario, BrokerDeMentira broker)
	{
		broker.Ligar(cenario);
		return cenario;
	}

	// ---------- Executando a cadeia ----------

	[Fact(DisplayName = "A consumed message should be asserted through the RabbitMQ validation builder")]
	public async Task Consumed_Message_Should_Be_Asserted_Through_The_Builder()
	{
		var broker = new BrokerDeMentira()
			.ComMensagem("{\"id\":42,\"cliente\":\"ana\"}", "pedido.criado",
				new Dictionary<string, object?> { ["x-tenant"] = "acme" });

		var cadeia = Ligada(Given().Queue("pedidos").Consume().WithTimeout(TimeSpan.FromSeconds(5)), broker);

		var validacao = await cadeia.ExecuteAsync();

		validacao
			.Then()
			.AssertSuccess()
			.AssertMessage("{\"id\":42,\"cliente\":\"ana\"}")
			.AssertMessage<Pedido>(p => p.Id.ShouldBe(42))
			.AssertRoutingKey("pedido.criado")
			.AssertHeader("x-tenant")
			.AssertMessageCount(1)
			.AssertQueueDrained();
	}

	[Fact(DisplayName = "The builder's assertions should fail with a message that says what was found")]
	public async Task Builder_Assertions_Should_Fail_With_What_Was_Found()
	{
		var broker = new BrokerDeMentira().ComMensagem("a").ComMensagem("b").ContandoORestante();
		var validacao = await Ligada(Given().Queue("q").Consume(), broker).ExecuteAsync();

		Should.Throw<ShouldAssertException>(() => validacao.AssertHeader("x-ausente"))
			.Message.ShouldContain("no header named 'x-ausente'");
		Should.Throw<ShouldAssertException>(() => validacao.AssertQueueDrained())
			.Message.ShouldContain("still queued");
		Should.Throw<ShouldAssertException>(() => validacao.AssertMessageCount(3))
			.Message.ShouldContain("brought 1 message(s)");
		Should.Throw<ShouldAssertException>(() => validacao.AssertFailure());
		Should.Throw<ArgumentNullException>(() => validacao.AssertMessage<Pedido>(null!));
	}

	[Fact(DisplayName = "AssertMessage of a type the body does not fit should say it did not deserialize")]
	public async Task AssertMessage_Should_Say_It_Did_Not_Deserialize()
	{
		// Um corpo "null" é JSON válido que não vira objeto nenhum.
		var broker = new BrokerDeMentira().ComMensagem("null");
		var validacao = await Ligada(Given().Queue("q").Consume(), broker).ExecuteAsync();

		Should.Throw<ShouldAssertException>(() => validacao.AssertMessage<Pedido>(_ => { }))
			.Message.ShouldContain("did not deserialize to Pedido");
	}

	[Fact(DisplayName = "A batch consume should be asserted by count, and Execute should run it synchronously")]
	public void Batch_Should_Be_Asserted_By_Count()
	{
		var broker = new BrokerDeMentira().ComMensagem("a").ComMensagem("b");

		var validacao = Ligada(Given().Queue("q").ConsumeBatch(2), broker).Execute();

		validacao.AssertSuccess().AssertMessageCount(2).AssertMessage("a");
		var passo = broker.FilasPerguntadas.Count;
		passo.ShouldBe(2);
	}

	[Fact(DisplayName = "A consume that timed out should be asserted as a failure")]
	public async Task Timed_Out_Consume_Should_Assert_As_Failure()
	{
		var broker = new BrokerDeMentira();

		var validacao = await Ligada(Given().Queue("q").Consume().WithTimeout(TimeSpan.FromMilliseconds(60)), broker)
			.ExecuteAsync();

		validacao.AssertFailure();
		Should.Throw<ShouldAssertException>(() => validacao.AssertSuccess());
	}

	[Fact(DisplayName = "A publish chain should send what the verbs configured")]
	public async Task Publish_Chain_Should_Send_What_The_Verbs_Configured()
	{
		var broker = new BrokerDeMentira();

		var cadeia = Given()
			.Exchange("eventos")
			.Publish(new { id = 1 })
			.WithRoutingKey("pedido.criado")
			.WithHeaders(new Dictionary<string, object?> { ["a"] = "1" })
			.WithHeader("b", "2")
			.AllowingUnroutable()
			.WithConnectionUri("amqp://u:p@broker.teste:5672/");

		await Ligada(cadeia, broker).ExecuteAsync();

		var publicada = broker.Publicadas.ShouldHaveSingleItem();
		publicada.Exchange.ShouldBe("eventos");
		publicada.RoutingKey.ShouldBe("pedido.criado");
		publicada.Mandatory.ShouldBeFalse();
		publicada.Corpo.ShouldBe("{\"id\":1}");
		publicada.Cabecalhos!.Keys.ShouldBe(new[] { "a", "b" }, ignoreOrder: true);
		broker.UriRecebida.ShouldBe("amqp://u:p@broker.teste:5672/");
	}

	[Fact(DisplayName = "Validate and ValidateMessage should run against the step's result when the chain executes")]
	public async Task Validate_And_ValidateMessage_Should_Run_On_Execute()
	{
		var broker = new BrokerDeMentira().ComMensagem("{\"id\":9}");
		var vistos = new List<string>();

		var cadeia = Given()
			.Queue("q")
			.Consume()
			.Validate(r => vistos.Add("validate:" + r.Message))
			.ValidateMessage<Pedido>(p => vistos.Add("message:" + p.Id));

		await Ligada(cadeia, broker).ExecuteAsync();

		vistos.ShouldBe(new[] { "validate:{\"id\":9}", "message:9" });
	}

	[Fact(DisplayName = "ValidateMessage on a body that is not the type should fail the chain saying so")]
	public async Task ValidateMessage_Should_Fail_When_The_Body_Does_Not_Fit()
	{
		var broker = new BrokerDeMentira().ComMensagem("null");

		var cadeia = Given().Queue("q").Consume().ValidateMessage<Pedido>(_ => { });

		var erro = await Should.ThrowAsync<InvalidOperationException>(() => Ligada(cadeia, broker).ExecuteAsync());
		erro.Message.ShouldContain("did not deserialize to Pedido");
	}

	[Fact(DisplayName = "A full topology chain should declare, bind, publish and consume in order")]
	public async Task Topology_Chain_Should_Run_In_Order()
	{
		var broker = new BrokerDeMentira().ComMensagem("ok");

		var cenario = Given().Exchange("eventos").DeclareExchange("fanout");
		broker.Ligar(cenario);
		cenario = cenario.And().Queue("pedidos").DeclareQueue("pedidos.dlx");
		broker.Ligar(cenario);
		cenario = cenario.And().BindQueueTo("eventos", "#");
		broker.Ligar(cenario);
		cenario = cenario.And().Queue("pedidos").Consume();
		broker.Ligar(cenario);

		var validacao = await cenario.ExecuteAsync();

		validacao.AssertSuccess().AssertMessage("ok");
		_ = broker.Canal.ReceivedWithAnyArgs(1).ExchangeDeclareAsync(
			default!, default!, default, default, default, default, default, default);
		_ = broker.Canal.ReceivedWithAnyArgs(1).QueueDeclareAsync(
			default!, default, default, default, default, default, default, default);
		_ = broker.Canal.ReceivedWithAnyArgs(1).QueueBindAsync(
			default!, default!, default!, default, default, default);
	}

	// Validate se prende ao último passo descrito. Entrar na cadeia do RabbitMQ sem descrever passo
	// deixa a verificação no passo HTTP de antes, e o resultado que chega não é do RabbitMQ.
	[Fact(DisplayName = "Validate on a chain whose last step is not RabbitMQ should name the result it got")]
	public async Task Validate_On_A_Foreign_Step_Should_Name_The_Result()
	{
		using var http = new Flurl.Http.Testing.HttpTest();
		http.RespondWith("{}");

		var cadeia = Given().ApiResource("http://api.teste/x").Get().Queue("q").Validate(_ => { });

		var erro = await Should.ThrowAsync<InvalidOperationException>(() => cadeia.ExecuteAsync());
		erro.Message.ShouldBe("Expected a RabbitMqStepResult but got HttpStepResult.");
	}

	// ---------- O invólucro tipado ----------

	[Fact(DisplayName = "The typed scenario should forward the BDD words and the context to the scenario it wraps")]
	public void Typed_Scenario_Should_Forward_To_The_Wrapped_One()
	{
		var original = Given();
		var cenario = original.Queue("q");

		cenario.Context.ShouldBeSameAs(original.Context);
		cenario.When().ShouldBeSameAs(cenario);
		cenario.Then().ShouldBeSameAs(cenario);
		cenario.On().ShouldBeSameAs(cenario);
		cenario.And().ShouldBeSameAs(cenario);

		ITestScenario comoBase = cenario;
		comoBase.And().ShouldBeSameAs(cenario);
		comoBase.On().ShouldBeSameAs(cenario);
		comoBase.When().ShouldBeSameAs(cenario);
		comoBase.Then().ShouldBeSameAs(cenario);

		// Envolver de novo devolve o mesmo invólucro, em vez de aninhar.
		cenario.Queue("outra").ShouldBeSameAs(cenario);
	}

	[Fact(DisplayName = "AddValidation and SetCurrentStep on the typed scenario should reach the wrapped scenario")]
	public async Task Typed_Scenario_Should_Forward_Steps_And_Validations()
	{
		var broker = new BrokerDeMentira();
		var cenario = Given().Queue("q");
		var passo = new RabbitMqPublishStep { RoutingKey = "q", Value = "x", CriarFabrica = broker.CriarFabrica };
		var validou = false;

		cenario.SetCurrentStep(passo);
		cenario.AddValidation(_ => validou = true);
		await cenario.ExecutePendingAsync();

		cenario.CurrentStep.ShouldBeSameAs(passo);
		validou.ShouldBeTrue();
		broker.Publicadas.ShouldHaveSingleItem();
	}

	// ---------- Verbos fora de ordem ----------

	[Fact(DisplayName = "Entry verbs should refuse a missing scenario or name")]
	public void Entry_Verbs_Should_Refuse_Missing_Arguments()
	{
		Should.Throw<ArgumentNullException>(() => ((ITestScenario)null!).Queue("q"));
		Should.Throw<ArgumentException>(() => Given().Queue(" "));
		Should.Throw<ArgumentNullException>(() => ((ITestScenario)null!).Exchange("ex"));
		Should.Throw<ArgumentNullException>(() => Given().Exchange(null!));
	}

	[Fact(DisplayName = "Topology verbs should say what is missing when called out of order")]
	public void Topology_Verbs_Should_Say_What_Is_Missing()
	{
		Should.Throw<ArgumentException>(() => Given().Exchange("ex").DeclareExchange(" "))
			.ParamName.ShouldBe("type");
		Should.Throw<InvalidOperationException>(() => Given().Queue("q").DeclareExchange())
			.Message.ShouldContain("Call Exchange(name) first");
		Should.Throw<ArgumentException>(() => Given().Queue("q").BindQueueTo(" ", "rk"))
			.ParamName.ShouldBe("exchange");
		Should.Throw<ArgumentNullException>(() => Given().Queue("q").BindQueueTo("ex", null!));
		Should.Throw<InvalidOperationException>(() => Given().Exchange("ex").BindQueueTo("ex", "rk"))
			.Message.ShouldContain("a binding joins a queue to an exchange");
	}

	[Fact(DisplayName = "Publishing with neither a queue nor an exchange named should say so")]
	public void Publish_Without_Destination_Should_Say_So()
	{
		Should.Throw<InvalidOperationException>(() => Given().Exchange(string.Empty).Publish("x"))
			.Message.ShouldContain("No destination");
	}

	[Fact(DisplayName = "ConsumeBatch should record the count, and refuse zero or an exchange")]
	public void ConsumeBatch_Should_Record_The_Count()
	{
		var passo = Given().Queue("q").ConsumeBatch(3).CurrentStep.ShouldBeOfType<RabbitMqConsumeStep>();

		passo.Queue.ShouldBe("q");
		passo.MessageCount.ShouldBe(3);
		passo.Timeout.ShouldBeGreaterThan(TimeSpan.Zero);
		Should.Throw<ArgumentOutOfRangeException>(() => Given().Queue("q").ConsumeBatch(0));
		Should.Throw<InvalidOperationException>(() => Given().Exchange("ex").ConsumeBatch(2))
			.Message.ShouldContain("an exchange holds nothing to consume");
	}

	[Fact(DisplayName = "Publish-only verbs on a consume step should say they apply to publishing")]
	public void Publish_Only_Verbs_On_A_Consume_Step_Should_Say_So()
	{
		Should.Throw<InvalidOperationException>(() => Given().Queue("q").Consume().WithRoutingKey("rk"))
			.Message.ShouldContain("Call Publish before WithRoutingKey");
		Should.Throw<InvalidOperationException>(() => Given().Queue("q").Consume().WithHeader("h", 1))
			.Message.ShouldContain("Call Publish before WithHeader");
		Should.Throw<InvalidOperationException>(() =>
				Given().Queue("q").Consume().WithHeaders(new Dictionary<string, object?>()))
			.Message.ShouldContain("Call Publish before WithHeaders");
		Should.Throw<InvalidOperationException>(() => Given().Queue("q").Consume().AllowingUnroutable())
			.Message.ShouldContain("Call Publish before AllowingUnroutable");
	}

	[Fact(DisplayName = "Verbs should refuse a missing argument")]
	public void Verbs_Should_Refuse_Missing_Arguments()
	{
		Should.Throw<ArgumentNullException>(() => Given().Queue("q").Publish("x").WithRoutingKey(null!));
		Should.Throw<ArgumentException>(() => Given().Queue("q").Publish("x").WithHeader(" ", 1));
		Should.Throw<ArgumentNullException>(() => Given().Queue("q").Publish("x").WithHeaders(null!));
		Should.Throw<ArgumentException>(() => Given().Queue("q").WithConnectionUri(" "));
		Should.Throw<ArgumentNullException>(() => Given().Queue("q").Consume().Validate(null!));
		Should.Throw<ArgumentNullException>(() => Given().Queue("q").Consume().ValidateMessage<Pedido>(null!));
	}

	[Fact(DisplayName = "WithConnectionUri after Publish or Consume should set it on that step, marked as explicit")]
	public void WithConnectionUri_Should_Set_It_On_The_Step()
	{
		var publicacao = Given().Queue("q").Publish("x").WithConnectionUri("amqp://a:5672/")
			.CurrentStep.ShouldBeOfType<RabbitMqPublishStep>();
		var consumo = Given().Queue("q").Consume().WithConnectionUri("amqp://b:5672/")
			.CurrentStep.ShouldBeOfType<RabbitMqConsumeStep>();

		publicacao.ConnectionUri.ShouldBe("amqp://a:5672/");
		publicacao.ConnectionUriInformada.ShouldBeTrue();
		publicacao.RoutingKey.ShouldBe("q", "rebuilding the step must keep what Publish recorded");
		consumo.ConnectionUri.ShouldBe("amqp://b:5672/");
		consumo.ConnectionUriInformada.ShouldBeTrue();
	}
}
