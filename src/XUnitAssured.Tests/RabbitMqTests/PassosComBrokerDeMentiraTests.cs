using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using RabbitMQ.Client;

using Shouldly;
using Xunit;

using XUnitAssured.Core.Abstractions;
using XUnitAssured.Core.Results;
using XUnitAssured.Core.Storage;
using XUnitAssured.RabbitMq.Results;
using XUnitAssured.RabbitMq.Steps;

namespace XUnitAssured.Tests.RabbitMqTests;

[Trait("Category", "RabbitMq")]
[Trait("Component", "Steps")]
/// <summary>
/// O que cada passo diz ao cliente oficial, e como o resultado conta o que aconteceu.
///
/// <para>
/// Sem broker: o passo recebe uma fábrica falsa (<see cref="BrokerDeMentira"/>) e o canal é um
/// falso do NSubstitute. Os testes de ida e volta em <c>IdaEVoltaNoBrokerTests</c> provam que o
/// broker de verdade concorda; estes provam o que o passo pede a ele, inclusive nos caminhos que um
/// broker de verdade raramente mostra, como a devolução da mensagem e a exceção no meio da conversa.
/// </para>
/// </summary>
public class PassosComBrokerDeMentiraTests
{
	private const string Uri = "amqp://convidado:senha-secreta@broker.teste:5672/";

	private static ITestContext NovoContexto() => new XUnitAssured.Core.DSL.TestContext(new StepStorage());

	// ---------- Topologia ----------

	[Fact(DisplayName = "DeclareQueue should declare a durable, shared, non-auto-delete queue and close what it opened")]
	public async Task DeclareQueue_Should_Declare_A_Durable_Queue()
	{
		var broker = new BrokerDeMentira();
		var passo = new RabbitMqTopologyStep
		{
			Operation = RabbitMqTopologyOperation.DeclareQueue,
			Queue = "pedidos",
			ConnectionUri = Uri,
			CriarFabrica = broker.CriarFabrica
		};

		var resultado = (RabbitMqStepResult)await passo.ExecuteAsync(NovoContexto());

		resultado.Success.ShouldBeTrue(string.Join("; ", resultado.Errors));
		resultado.Destination.ShouldBe("pedidos");
		passo.IsValid.ShouldBeTrue();
		passo.IsExecuted.ShouldBeTrue();
		_ = broker.Canal.Received(1).QueueDeclareAsync(
			"pedidos", Arg.Is(true), Arg.Is(false), Arg.Is(false), null, Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
		_ = broker.Canal.Received(1).DisposeAsync();
		_ = broker.Conexao.Received(1).DisposeAsync();
	}

	[Fact(DisplayName = "DeclareQueue with a dead-letter exchange should pass it as x-dead-letter-exchange")]
	public async Task DeclareQueue_Should_Pass_The_Dead_Letter_Exchange()
	{
		var broker = new BrokerDeMentira();
		IDictionary<string, object?>? argumentos = null;
		broker.Canal
			.When(c => c.QueueDeclareAsync(
				Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<bool>(),
				Arg.Any<IDictionary<string, object?>?>(), Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()))
			.Do(c => argumentos = c.ArgAt<IDictionary<string, object?>?>(4));

		var passo = new RabbitMqTopologyStep
		{
			Operation = RabbitMqTopologyOperation.DeclareQueue,
			Queue = "pedidos",
			DeadLetterExchange = "pedidos.dlx",
			Durable = false,
			ConnectionUri = Uri,
			CriarFabrica = broker.CriarFabrica
		};

		(await passo.ExecuteAsync(NovoContexto())).Success.ShouldBeTrue();

		argumentos.ShouldNotBeNull();
		argumentos!["x-dead-letter-exchange"].ShouldBe("pedidos.dlx");
		_ = broker.Canal.Received(1).QueueDeclareAsync(
			"pedidos", Arg.Is(false), Arg.Is(false), Arg.Is(false), Arg.Any<IDictionary<string, object?>?>(),
			Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
	}

	[Fact(DisplayName = "DeclareExchange should declare the exchange with its type, and name it as the destination")]
	public async Task DeclareExchange_Should_Declare_With_The_Type()
	{
		var broker = new BrokerDeMentira();
		var passo = new RabbitMqTopologyStep
		{
			Operation = RabbitMqTopologyOperation.DeclareExchange,
			Exchange = "eventos",
			ExchangeType = "topic",
			Queue = "ignorada",
			ConnectionUri = Uri,
			CriarFabrica = broker.CriarFabrica
		};

		var resultado = (RabbitMqStepResult)await passo.ExecuteAsync(NovoContexto());

		resultado.Success.ShouldBeTrue(string.Join("; ", resultado.Errors));
		resultado.Destination.ShouldBe("eventos", "an exchange declaration is about the exchange, not the queue");
		_ = broker.Canal.Received(1).ExchangeDeclareAsync(
			"eventos", "topic", Arg.Is(true), Arg.Is(false), Arg.Any<IDictionary<string, object?>?>(),
			Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
		_ = broker.Canal.DidNotReceiveWithAnyArgs().QueueDeclareAsync(
			default!, default, default, default, default, default, default, default);
	}

	[Fact(DisplayName = "BindQueue should bind the queue to the exchange by the routing key")]
	public async Task BindQueue_Should_Bind_By_The_Routing_Key()
	{
		var broker = new BrokerDeMentira();
		var passo = new RabbitMqTopologyStep
		{
			Operation = RabbitMqTopologyOperation.BindQueue,
			Queue = "pedidos",
			Exchange = "eventos",
			RoutingKey = "pedido.criado",
			ConnectionUri = Uri,
			CriarFabrica = broker.CriarFabrica
		};

		var resultado = (RabbitMqStepResult)await passo.ExecuteAsync(NovoContexto());

		resultado.Success.ShouldBeTrue(string.Join("; ", resultado.Errors));
		resultado.Destination.ShouldBe("pedidos");
		resultado.RoutingKey.ShouldBe("pedido.criado");
		_ = broker.Canal.Received(1).QueueBindAsync(
			"pedidos", "eventos", "pedido.criado", Arg.Any<IDictionary<string, object?>?>(),
			Arg.Any<bool>(), Arg.Any<CancellationToken>());
	}

	// Redeclarar com argumentos diferentes é o erro de topologia mais comum, e quem o diz é o broker.
	[Fact(DisplayName = "A declaration the broker refuses should fail with the broker's reason and masked diagnostics")]
	public async Task Refused_Declaration_Should_Fail_With_The_Reason()
	{
		var broker = new BrokerDeMentira();
		broker.Canal
			.QueueDeclareAsync(
				Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<bool>(),
				Arg.Any<IDictionary<string, object?>?>(), Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
			.ThrowsAsync(new InvalidOperationException("PRECONDITION_FAILED - inequivalent arg 'durable'"));

		var passo = new RabbitMqTopologyStep
		{
			Operation = RabbitMqTopologyOperation.DeclareQueue,
			Queue = "pedidos",
			ConnectionUri = Uri,
			CriarFabrica = broker.CriarFabrica
		};

		var resultado = (RabbitMqStepResult)await passo.ExecuteAsync(NovoContexto());

		resultado.Success.ShouldBeFalse();
		passo.IsValid.ShouldBeFalse();
		resultado.Errors.ShouldContain(e => e.Contains("PRECONDITION_FAILED"));
		resultado.GetProperty<string>("Operation").ShouldBe("DeclareQueue");
		var uri = resultado.GetProperty<string>("ConnectionUri");
		uri.ShouldNotBeNull();
		uri!.ShouldNotContain("senha-secreta");
		uri.ShouldContain("broker.teste");
	}

	[Fact(DisplayName = "An operation the step does not know should fail, not declare something else")]
	public async Task Unknown_Operation_Should_Fail()
	{
		var broker = new BrokerDeMentira();
		var passo = new RabbitMqTopologyStep
		{
			Operation = (RabbitMqTopologyOperation)99,
			Queue = "pedidos",
			ConnectionUri = Uri,
			CriarFabrica = broker.CriarFabrica
		};

		var resultado = await passo.ExecuteAsync(NovoContexto());

		resultado.Success.ShouldBeFalse();
		resultado.Errors.ShouldContain(e => e.Contains("Unknown topology operation"));
		broker.Canal.ReceivedCalls().ShouldAllBe(c => c.GetMethodInfo().Name == "DisposeAsync");
	}

	[Fact(DisplayName = "A topology step should take the fixture's URI when none was set, and an explicit one over it")]
	public async Task Topology_Step_Should_Resolve_The_Uri()
	{
		var contexto = NovoContexto();
		contexto.SetProperty("_RabbitMqConnectionUri", "amqp://fixture:5672/");

		var semUri = new BrokerDeMentira();
		await new RabbitMqTopologyStep { Queue = "q", CriarFabrica = semUri.CriarFabrica }.ExecuteAsync(contexto);

		var comUri = new BrokerDeMentira();
		await new RabbitMqTopologyStep { Queue = "q", ConnectionUri = Uri, CriarFabrica = comUri.CriarFabrica }
			.ExecuteAsync(contexto);

		semUri.UriRecebida.ShouldBe("amqp://fixture:5672/");
		comUri.UriRecebida.ShouldBe(Uri);
	}

	[Fact(DisplayName = "Copying a topology step should carry everything it was configured with")]
	public void Copying_A_Topology_Step_Should_Carry_Everything()
	{
		var broker = new BrokerDeMentira();
		var original = new RabbitMqTopologyStep
		{
			Name = "declara",
			Operation = RabbitMqTopologyOperation.BindQueue,
			Queue = "q",
			Exchange = "ex",
			ExchangeType = "fanout",
			RoutingKey = "rk",
			Durable = false,
			DeadLetterExchange = "dlx",
			ConnectionUri = Uri,
			CriarFabrica = broker.CriarFabrica
		};

		var copia = new RabbitMqTopologyStep(original);

		copia.Name.ShouldBe("declara");
		copia.Operation.ShouldBe(RabbitMqTopologyOperation.BindQueue);
		copia.Queue.ShouldBe("q");
		copia.Exchange.ShouldBe("ex");
		copia.ExchangeType.ShouldBe("fanout");
		copia.RoutingKey.ShouldBe("rk");
		copia.Durable.ShouldBeFalse();
		copia.DeadLetterExchange.ShouldBe("dlx");
		copia.ConnectionUri.ShouldBe(Uri);
		copia.ConnectionUriInformada.ShouldBeTrue();
		copia.CriarFabrica.ShouldBeSameAs(original.CriarFabrica);
		copia.StepType.ShouldBe("RabbitMq");
		copia.IsExecuted.ShouldBeFalse();
		Should.Throw<ArgumentNullException>(() => new RabbitMqTopologyStep(null!));
	}

	[Fact(DisplayName = "Validating a topology step before it runs should say to execute first, and after it runs should apply the check")]
	public async Task Topology_Validate_Before_And_After()
	{
		var broker = new BrokerDeMentira();
		var passo = new RabbitMqTopologyStep { Queue = "q", ConnectionUri = Uri, CriarFabrica = broker.CriarFabrica };

		Should.Throw<InvalidOperationException>(() => passo.Validate(_ => { }))
			.Message.ShouldContain("has not been executed");

		await passo.ExecuteAsync(NovoContexto());
		ITestStepResult? visto = null;
		passo.Validate(r => visto = r);

		visto.ShouldBeSameAs(passo.Result);
		passo.IsValid.ShouldBeTrue();
	}

	// ---------- Publicação ----------

	[Fact(DisplayName = "Publishing to a queue should go through the default exchange, mandatory, with confirmations on")]
	public async Task Publish_Should_Go_Mandatory_With_Confirmations()
	{
		var broker = new BrokerDeMentira();
		var passo = new RabbitMqPublishStep
		{
			RoutingKey = "pedidos",
			Value = "olá",
			ConnectionUri = Uri,
			CriarFabrica = broker.CriarFabrica
		};

		var resultado = (RabbitMqStepResult)await passo.ExecuteAsync(NovoContexto());

		resultado.Success.ShouldBeTrue(string.Join("; ", resultado.Errors));
		resultado.Destination.ShouldBe("pedidos", "with no exchange, the destination is the queue the key names");
		resultado.RoutingKey.ShouldBe("pedidos");
		passo.IsValid.ShouldBeTrue();

		var publicada = broker.Publicadas.ShouldHaveSingleItem();
		publicada.Exchange.ShouldBe(string.Empty);
		publicada.RoutingKey.ShouldBe("pedidos");
		publicada.Mandatory.ShouldBeTrue("an unroutable message must not pass in silence");
		publicada.Corpo.ShouldBe("olá", "a string goes as it is, in UTF-8");
		publicada.Cabecalhos.ShouldBeNull("no headers were asked for");

		broker.OpcoesDoCanal.ShouldNotBeNull();
		broker.OpcoesDoCanal!.PublisherConfirmationsEnabled.ShouldBeTrue(
			"without confirmations the await returns before a basic.return can arrive");
		broker.OpcoesDoCanal.PublisherConfirmationTrackingEnabled.ShouldBeTrue();
		_ = broker.Canal.Received(1).DisposeAsync();
	}

	[Fact(DisplayName = "Publishing an object should serialize it as JSON with the options given")]
	public async Task Publish_Should_Serialize_An_Object()
	{
		var broker = new BrokerDeMentira();
		var passo = new RabbitMqPublishStep
		{
			Exchange = "eventos",
			RoutingKey = "pedido.criado",
			Value = new { PedidoId = 7, Total = 10.5m },
			JsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase },
			ConnectionUri = Uri,
			CriarFabrica = broker.CriarFabrica
		};

		var resultado = (RabbitMqStepResult)await passo.ExecuteAsync(NovoContexto());

		resultado.Destination.ShouldBe("eventos", "with an exchange, the destination is the exchange");
		var publicada = broker.Publicadas.ShouldHaveSingleItem();
		publicada.Exchange.ShouldBe("eventos");
		publicada.Corpo.ShouldBe("{\"pedidoId\":7,\"total\":10.5}");
	}

	[Fact(DisplayName = "Publishing with no value should send an empty body")]
	public async Task Publish_Without_Value_Should_Send_Empty_Body()
	{
		var broker = new BrokerDeMentira();
		var passo = new RabbitMqPublishStep { RoutingKey = "q", ConnectionUri = Uri, CriarFabrica = broker.CriarFabrica };

		(await passo.ExecuteAsync(NovoContexto())).Success.ShouldBeTrue();

		broker.Publicadas.ShouldHaveSingleItem().Corpo.ShouldBe(string.Empty);
	}

	[Fact(DisplayName = "Headers set on the step should travel as AMQP headers, and AllowingUnroutable should drop mandatory")]
	public async Task Publish_Should_Send_Headers_And_Honor_Unroutable()
	{
		var broker = new BrokerDeMentira();
		var passo = new RabbitMqPublishStep
		{
			RoutingKey = "q",
			Value = "x",
			Headers = new Dictionary<string, object?> { ["x-tenant"] = "acme", ["x-tentativa"] = 2 },
			RequireRouting = false,
			ConnectionUri = Uri,
			CriarFabrica = broker.CriarFabrica
		};

		(await passo.ExecuteAsync(NovoContexto())).Success.ShouldBeTrue();

		var publicada = broker.Publicadas.ShouldHaveSingleItem();
		publicada.Mandatory.ShouldBeFalse();
		publicada.Cabecalhos.ShouldNotBeNull();
		publicada.Cabecalhos!["x-tenant"].ShouldBe("acme");
		publicada.Cabecalhos["x-tentativa"].ShouldBe(2);
	}

	// O pior resultado num teste: o broker aceita, descarta, e o passo diria que deu certo.
	[Fact(DisplayName = "A message the broker returns as unroutable should fail the publish and say how to fix it")]
	public async Task Returned_Message_Should_Fail_The_Publish()
	{
		var broker = new BrokerDeMentira { DevolverComo = (312, "NO_ROUTE") };
		var passo = new RabbitMqPublishStep
		{
			Exchange = "eventos",
			RoutingKey = "ninguem.escuta",
			Value = "x",
			ConnectionUri = Uri,
			CriarFabrica = broker.CriarFabrica
		};

		var resultado = (RabbitMqStepResult)await passo.ExecuteAsync(NovoContexto());

		resultado.Success.ShouldBeFalse();
		passo.IsValid.ShouldBeFalse();
		var erro = resultado.Errors.ShouldHaveSingleItem();
		erro.ShouldContain("312 NO_ROUTE");
		erro.ShouldContain("'eventos'");
		erro.ShouldContain("'ninguem.escuta'");
		erro.ShouldContain("AllowingUnroutable()");
		resultado.GetProperty<string>("BasicReturn").ShouldBe("312 NO_ROUTE");
		resultado.GetProperty<string>("Exchange").ShouldBe("eventos");
		resultado.GetProperty<string>("RoutingKey").ShouldBe("ninguem.escuta");
	}

	[Fact(DisplayName = "A connection the broker refuses should fail the publish with the reason, not throw")]
	public async Task Refused_Connection_Should_Fail_The_Publish()
	{
		var broker = new BrokerDeMentira();
		broker.Fabrica.CreateConnectionAsync(Arg.Any<CancellationToken>())
			.ThrowsAsync(new InvalidOperationException("ACCESS_REFUSED - Login was refused"));
		var passo = new RabbitMqPublishStep { RoutingKey = "q", Value = "x", ConnectionUri = Uri, CriarFabrica = broker.CriarFabrica };

		var resultado = (RabbitMqStepResult)await passo.ExecuteAsync(NovoContexto());

		resultado.Success.ShouldBeFalse();
		resultado.Errors.ShouldContain(e => e.Contains("ACCESS_REFUSED"));
		broker.Publicadas.ShouldBeEmpty();
	}

	[Fact(DisplayName = "Validating a publish step before it runs should say to execute first, and after it runs should apply the check")]
	public async Task Publish_Validate_Before_And_After()
	{
		var broker = new BrokerDeMentira();
		var passo = new RabbitMqPublishStep { RoutingKey = "q", ConnectionUri = Uri, CriarFabrica = broker.CriarFabrica };

		Should.Throw<InvalidOperationException>(() => passo.Validate(_ => { }));

		await passo.ExecuteAsync(NovoContexto());
		passo.Validate(r => r.Success.ShouldBeTrue());

		passo.IsValid.ShouldBeTrue();
		passo.StepType.ShouldBe("RabbitMq");
		Should.Throw<ArgumentNullException>(() => new RabbitMqPublishStep(null!));
	}

	[Theory(DisplayName = "Masking a URI should drop the credential and keep the host, port and virtual host")]
	[InlineData("amqp://u:p@broker:5672/vh", "amqp://***@broker:5672/vh")]
	[InlineData("amqp://broker:5673/", "amqp://broker:5673/")]
	[InlineData("isto não é uri", "(uri inválida)")]
	public void Masking_Should_Drop_The_Credential(string uri, string esperado)
	{
		RabbitMqPublishStep.Mascarar(uri).ShouldBe(esperado);
	}

	// ---------- Consumo ----------

	[Fact(DisplayName = "Consuming should hand back the message with its routing key, headers and what is left, acknowledged on arrival")]
	public async Task Consume_Should_Hand_Back_The_Message()
	{
		var broker = new BrokerDeMentira()
			.ComMensagem("{\"id\":1}", "pedido.criado", new Dictionary<string, object?> { ["x-tenant"] = "acme" })
			.ComMensagem("{\"id\":2}")
			.ContandoORestante();
		var passo = new RabbitMqConsumeStep
		{
			Queue = "pedidos",
			Timeout = TimeSpan.FromSeconds(5),
			ConnectionUri = Uri,
			CriarFabrica = broker.CriarFabrica
		};

		var resultado = (RabbitMqStepResult)await passo.ExecuteAsync(NovoContexto());

		resultado.Success.ShouldBeTrue(string.Join("; ", resultado.Errors));
		passo.IsValid.ShouldBeTrue();
		resultado.Destination.ShouldBe("pedidos");
		resultado.Message.ShouldBe("{\"id\":1}");
		resultado.RoutingKey.ShouldBe("pedido.criado");
		resultado.Headers["x-tenant"].ShouldBe("acme");
		resultado.RemainingMessageCount.ShouldBe(1u, "one message was still behind this one");
		broker.FilasPerguntadas.ShouldHaveSingleItem("one message was asked for, so the step asks once").ShouldBe("pedidos");
		broker.AutoAck.ShouldHaveSingleItem().ShouldBeTrue("a consumed message is acknowledged on arrival");
		_ = broker.Canal.DidNotReceiveWithAnyArgs().BasicNackAsync(default, default, default, default);
		_ = broker.Conexao.Received(1).DisposeAsync();
	}

	[Fact(DisplayName = "A message without headers should come back with an empty header set, not null")]
	public async Task Consume_Without_Headers_Should_Have_Empty_Headers()
	{
		var broker = new BrokerDeMentira().ComMensagem("oi");
		var passo = new RabbitMqConsumeStep { Queue = "q", ConnectionUri = Uri, CriarFabrica = broker.CriarFabrica };

		var resultado = (RabbitMqStepResult)await passo.ExecuteAsync(NovoContexto());

		resultado.Headers.ShouldBeEmpty();
		resultado.RemainingMessageCount.ShouldBe(0u);
	}

	[Theory(DisplayName = "Rejecting should take the message without auto-ack and nack it with the requeue choice")]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Rejecting_Should_Nack_With_The_Requeue_Choice(bool requeue)
	{
		var broker = new BrokerDeMentira().ComMensagem("ruim");
		var passo = new RabbitMqConsumeStep
		{
			Queue = "q",
			RejectMessage = true,
			RequeueRejected = requeue,
			ConnectionUri = Uri,
			CriarFabrica = broker.CriarFabrica
		};

		var resultado = (RabbitMqStepResult)await passo.ExecuteAsync(NovoContexto());

		resultado.Success.ShouldBeTrue(string.Join("; ", resultado.Errors));
		resultado.Message.ShouldBe("ruim", "a rejected message is still what the test reads");
		broker.AutoAck.ShouldHaveSingleItem().ShouldBeFalse("a message to reject cannot be acknowledged on arrival");
		_ = broker.Canal.Received(1).BasicNackAsync(1UL, Arg.Is(false), Arg.Is(requeue), Arg.Any<CancellationToken>());
	}

	[Fact(DisplayName = "A batch should bring the messages in order and stop asking once it has them all")]
	public async Task Batch_Should_Stop_When_Complete()
	{
		var broker = new BrokerDeMentira().ComMensagem("a").ComMensagem("b").ComMensagem("c").ComMensagem("d")
			.ContandoORestante();
		var passo = new RabbitMqConsumeStep
		{
			Queue = "q",
			MessageCount = 3,
			Timeout = TimeSpan.FromSeconds(5),
			ConnectionUri = Uri,
			CriarFabrica = broker.CriarFabrica
		};

		var resultado = (RabbitMqStepResult)await passo.ExecuteAsync(NovoContexto());

		resultado.Success.ShouldBeTrue(string.Join("; ", resultado.Errors));
		resultado.Messages.ShouldBe(new[] { "a", "b", "c" });
		resultado.Message.ShouldBe("a");
		resultado.RemainingMessageCount.ShouldBe(1u);
		broker.FilasPerguntadas.Count.ShouldBe(3, "the fourth message is not the step's to take");
	}

	[Fact(DisplayName = "A batch that finds fewer messages than asked should succeed with what there was when the time is up")]
	public async Task Batch_With_Fewer_Should_Succeed_With_What_There_Was()
	{
		var broker = new BrokerDeMentira().ComMensagem("a").ComMensagem("b");
		var passo = new RabbitMqConsumeStep
		{
			Queue = "q",
			MessageCount = 5,
			Timeout = TimeSpan.FromMilliseconds(200),
			ConnectionUri = Uri,
			CriarFabrica = broker.CriarFabrica
		};

		var resultado = (RabbitMqStepResult)await passo.ExecuteAsync(NovoContexto());

		resultado.Success.ShouldBeTrue();
		passo.IsValid.ShouldBeTrue();
		resultado.Messages.ShouldBe(new[] { "a", "b" });
		broker.FilasPerguntadas.Count.ShouldBeGreaterThan(2, "an empty queue is asked again until the timeout");
	}

	[Fact(DisplayName = "An empty queue should end in a timeout failure that names the queue")]
	public async Task Empty_Queue_Should_Time_Out()
	{
		var broker = new BrokerDeMentira();
		var passo = new RabbitMqConsumeStep
		{
			Queue = "vazia",
			Timeout = TimeSpan.FromMilliseconds(120),
			ConnectionUri = Uri,
			CriarFabrica = broker.CriarFabrica
		};

		var resultado = (RabbitMqStepResult)await passo.ExecuteAsync(NovoContexto());

		resultado.Success.ShouldBeFalse();
		passo.IsValid.ShouldBeFalse();
		resultado.Message.ShouldBeNull();
		resultado.Errors.ShouldContain(e => e.Contains("vazia"));
	}

	[Fact(DisplayName = "A cancelled consume should fail with the cancellation, not wait for the timeout")]
	public async Task Cancelled_Consume_Should_Fail()
	{
		var broker = new BrokerDeMentira();
		var passo = new RabbitMqConsumeStep
		{
			Queue = "q",
			Timeout = TimeSpan.FromSeconds(30),
			ConnectionUri = Uri,
			CriarFabrica = broker.CriarFabrica
		};
		using var cancelamento = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
		var cronometro = System.Diagnostics.Stopwatch.StartNew();

		var resultado = (RabbitMqStepResult)await passo.ExecuteAsync(NovoContexto(), cancelamento.Token);

		resultado.Success.ShouldBeFalse();
		cronometro.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(10));
		resultado.GetProperty<double>("TimeoutSeconds").ShouldBe(30);
	}

	[Fact(DisplayName = "Copying a consume step should carry everything, and validating before it runs should say to execute first")]
	public void Copying_A_Consume_Step_Should_Carry_Everything()
	{
		var broker = new BrokerDeMentira();
		var original = new RabbitMqConsumeStep
		{
			Name = "consome",
			Queue = "q",
			MessageCount = 4,
			Timeout = TimeSpan.FromSeconds(3),
			RejectMessage = true,
			RequeueRejected = true,
			CriarFabrica = broker.CriarFabrica
		};

		var copia = new RabbitMqConsumeStep(original);

		copia.Name.ShouldBe("consome");
		copia.Queue.ShouldBe("q");
		copia.MessageCount.ShouldBe(4);
		copia.Timeout.ShouldBe(TimeSpan.FromSeconds(3));
		copia.RejectMessage.ShouldBeTrue();
		copia.RequeueRejected.ShouldBeTrue();
		copia.ConnectionUriInformada.ShouldBeFalse("a URI nobody set must stay unset in the copy");
		copia.CriarFabrica.ShouldBeSameAs(original.CriarFabrica);
		copia.StepType.ShouldBe("RabbitMq");
		Should.Throw<InvalidOperationException>(() => copia.Validate(_ => { }));
		Should.Throw<ArgumentNullException>(() => new RabbitMqConsumeStep(null!));
	}

	[Fact(DisplayName = "Validating a consume step after it runs should apply the check and mark it valid")]
	public async Task Consume_Validate_After_Run()
	{
		var broker = new BrokerDeMentira();
		var passo = new RabbitMqConsumeStep
		{
			Queue = "q",
			Timeout = TimeSpan.FromMilliseconds(50),
			ConnectionUri = Uri,
			CriarFabrica = broker.CriarFabrica
		};

		await passo.ExecuteAsync(NovoContexto());
		passo.IsValid.ShouldBeFalse("a timeout is not a valid consume");

		passo.Validate(r => r.Success.ShouldBeFalse());

		passo.IsValid.ShouldBeTrue("the check the test wrote passed");
	}
}
