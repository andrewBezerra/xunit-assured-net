using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using NSubstitute;

using Shouldly;
using Xunit;

using XUnitAssured.Core.Abstractions;
using XUnitAssured.Core.DSL;
using XUnitAssured.Core.Storage;
using XUnitAssured.RabbitMq.Extensions;
using XUnitAssured.RabbitMq.Steps;

namespace XUnitAssured.Tests.RabbitMqTests;

/// <summary>
/// A fila temporária de um teste: <c>ExpiringAfter</c> (o argumento <c>x-expires</c>) e
/// <c>DeleteQueue</c>. Um teste que escuta o que o sistema publica liga uma fila própria a uma
/// exchange e a apaga no fim; se parar antes, o broker a apaga sozinho.
/// </summary>
[Trait("Category", "RabbitMq")]
[Trait("Component", "Steps")]
public class FilaTemporariaTests
{
	private const string Uri = "amqp://convidado:senha-secreta@broker.teste:5672/";

	private static ITestContext NovoContexto() => new XUnitAssured.Core.DSL.TestContext(new StepStorage());

	[Fact(DisplayName = "ExpiringAfter should declare the queue with x-expires in milliseconds, next to the dead-letter exchange")]
	public async Task ExpiringAfter_Should_Pass_X_Expires()
	{
		var broker = new BrokerDeMentira();
		IDictionary<string, object?>? argumentos = null;
		broker.Canal
			.When(c => c.QueueDeclareAsync(
				Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<bool>(),
				Arg.Any<IDictionary<string, object?>?>(), Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()))
			.Do(c => argumentos = c.ArgAt<IDictionary<string, object?>?>(4));
		var declarar = (RabbitMqTopologyStep)ScenarioDsl.Given().Queue("escuta").DeclareQueue("dlx").ExpiringAfter(TimeSpan.FromMinutes(10)).CurrentStep!;
		var passo = new RabbitMqTopologyStep(declarar) { ConnectionUri = Uri, CriarFabrica = broker.CriarFabrica };

		(await passo.ExecuteAsync(NovoContexto())).Success.ShouldBeTrue();

		argumentos.ShouldNotBeNull();
		argumentos!["x-expires"].ShouldBe(600_000);
		argumentos["x-dead-letter-exchange"].ShouldBe("dlx");
	}

	[Fact(DisplayName = "A queue without ExpiringAfter should have no x-expires")]
	public async Task Without_ExpiringAfter_There_Is_No_X_Expires()
	{
		var broker = new BrokerDeMentira();
		var passo = new RabbitMqTopologyStep
		{
			Operation = RabbitMqTopologyOperation.DeclareQueue,
			Queue = "pedidos",
			ConnectionUri = Uri,
			CriarFabrica = broker.CriarFabrica
		};

		(await passo.ExecuteAsync(NovoContexto())).Success.ShouldBeTrue();

		_ = broker.Canal.Received(1).QueueDeclareAsync(
			"pedidos", Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<bool>(), null, Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
	}

	[Fact(DisplayName = "DeleteQueue should delete the named queue and close what it opened")]
	public async Task DeleteQueue_Should_Delete_The_Queue()
	{
		var broker = new BrokerDeMentira();
		var apagar = (RabbitMqTopologyStep)ScenarioDsl.Given().Queue("escuta").DeleteQueue().CurrentStep!;
		var passo = new RabbitMqTopologyStep(apagar) { ConnectionUri = Uri, CriarFabrica = broker.CriarFabrica };

		(await passo.ExecuteAsync(NovoContexto())).Success.ShouldBeTrue();

		passo.Operation.ShouldBe(RabbitMqTopologyOperation.DeleteQueue);
		_ = broker.Canal.Received(1).QueueDeleteAsync("escuta", Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
		_ = broker.Conexao.Received(1).DisposeAsync();
	}

	[Fact(DisplayName = "ExpiringAfter should refuse a step that is not a queue declaration, and a non-positive expiry")]
	public void ExpiringAfter_Should_Refuse_Misuse()
	{
		Should.Throw<InvalidOperationException>(() => ScenarioDsl.Given().Queue("q").Publish("x").ExpiringAfter(TimeSpan.FromMinutes(1)));
		Should.Throw<ArgumentOutOfRangeException>(() => ScenarioDsl.Given().Queue("q").DeclareQueue().ExpiringAfter(TimeSpan.Zero));
		Should.Throw<InvalidOperationException>(() => ScenarioDsl.Given().Exchange("e").DeleteQueue());
	}

	[Fact(DisplayName = "The expiry should survive the verbs that rebuild the step, such as WithConnectionUri")]
	public void Copy_Should_Keep_The_Expiry()
	{
		var passo = (RabbitMqTopologyStep)ScenarioDsl.Given().Queue("q").DeclareQueue().ExpiringAfter(TimeSpan.FromSeconds(90))
			.WithConnectionUri(Uri).CurrentStep!;

		passo.ExpiresAfter.ShouldBe(TimeSpan.FromSeconds(90));
	}
}

/// <summary>
/// <c>AssertJsonPath</c> e <c>AssertHeader(nome, valor)</c> no consumo, como nos pacotes Http e Kafka:
/// o que se verifica numa mensagem que um teste escutou.
/// </summary>
[Trait("Category", "RabbitMq")]
[Trait("Component", "Validation")]
public class AsercoesDoRabbitMqTests
{
	private static async Task<XUnitAssured.RabbitMq.Extensions.RabbitMqValidationBuilder> Consumir(BrokerDeMentira broker)
	{
		var cadeia = ScenarioDsl.Given().Queue("pedidos").Consume().WithTimeout(TimeSpan.FromSeconds(5));
		broker.Ligar(cadeia);
		return await cadeia.ExecuteAsync();
	}

	[Fact(DisplayName = "AssertJsonPath and AssertHeader with a value should pass on the consumed message, with a byte[] header read as UTF-8")]
	public async Task Assertions_Should_Pass()
	{
		var broker = new BrokerDeMentira().ComMensagem("""{"pedido":{"id":42,"status":"pago"}}""", "pedido.pago",
			new Dictionary<string, object?> { ["origem"] = System.Text.Encoding.UTF8.GetBytes("loja"), ["tentativa"] = 2 });

		(await Consumir(broker)).Then().AssertSuccess()
			.AssertJsonPath<int>("$.pedido.id", id => id == 42)
			.AssertJsonPath<string>("$.pedido.status", s => s == "pago", "should be paid")
			.AssertHeader("origem", "loja")
			.AssertHeader("tentativa", "2");
	}

	[Fact(DisplayName = "AssertJsonPath and AssertHeader should fail saying what was found")]
	public async Task Assertions_Should_Fail_With_What_Was_Found()
	{
		var broker = new BrokerDeMentira().ComMensagem("""{"status":"cancelado"}""", "r",
			new Dictionary<string, object?> { ["origem"] = System.Text.Encoding.UTF8.GetBytes("outra") });
		var validacao = (await Consumir(broker)).Then();

		Should.Throw<ShouldAssertException>(() => validacao.AssertJsonPath<string>("$.status", s => s == "pago", "should be paid"))
			.Message.ShouldContain("should be paid");
		Should.Throw<ShouldAssertException>(() => validacao.AssertHeader("origem", "loja"))
			.Message.ShouldContain("outra");
		Should.Throw<ShouldAssertException>(() => validacao.AssertHeader("ausente", "x"))
			.Message.ShouldContain("no header named 'ausente'");
	}
}

/// <summary>A fila temporária num RabbitMQ de verdade: escuta uma exchange, recebe, e some ao ser apagada.</summary>
[Trait("Category", "RabbitMq")]
[Trait("Component", "Integration")]
[Trait("Requires", "Broker")]
public class FilaTemporariaNoBrokerTests
{
	private static readonly string Broker =
		Environment.GetEnvironmentVariable("XA_RABBITMQ_URI")
			?? "amqp://xa:xa-senha@127.0.0.1:5672/";

	[Fact(DisplayName = "A temporary queue bound to an exchange should get what is published there, and be gone after DeleteQueue")]
	public async Task A_Temporary_Queue_Should_Listen_And_Be_Deleted()
	{
		var sufixo = Guid.NewGuid().ToString("N");
		var exchange = $"xa-eventos-{sufixo}";
		var escuta = $"xa-escuta-{sufixo}";

		(await ScenarioDsl.Given()
			.Exchange(exchange).WithConnectionUri(Broker).DeclareExchange("topic")
			.And()
			.Queue(escuta).DeclareQueue().ExpiringAfter(TimeSpan.FromMinutes(1))
			.And()
			.BindQueueTo(exchange, "pedido.*")
			.ExecuteAsync()).Then().AssertSuccess();

		(await ScenarioDsl.Given()
			.Exchange(exchange).WithConnectionUri(Broker).Publish("""{"status":"pago"}""").WithRoutingKey("pedido.pago")
			.ExecuteAsync()).Then().AssertSuccess();

		var recebida = await ScenarioDsl.Given()
			.Queue(escuta).WithConnectionUri(Broker).Consume().WithTimeout(TimeSpan.FromSeconds(10))
			.ExecuteAsync();
		recebida.Then().AssertSuccess().AssertRoutingKey("pedido.pago");

		(await ScenarioDsl.Given().Queue(escuta).WithConnectionUri(Broker).DeleteQueue().ExecuteAsync()).Then().AssertSuccess();

		// Apagada, publicar na exchange não acha fila: com mandatory, é falha.
		(await ScenarioDsl.Given()
			.Exchange(exchange).WithConnectionUri(Broker).Publish("depois").WithRoutingKey("pedido.pago")
			.ExecuteAsync()).Then().AssertFailure();
	}
}
