using System;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

using XUnitAssured.Core.DSL;
using XUnitAssured.RabbitMq.Extensions;

namespace XUnitAssured.Tests.RabbitMqTests;

[Trait("Category", "RabbitMq")]
[Trait("Component", "Integration")]
[Trait("Requires", "Broker")]
/// <summary>
/// A ida e volta contra um broker de verdade.
///
/// <para>
/// Todo o resto da cobertura deste pacote roda sem broker, e isso é bom: é rápido e determinístico.
/// Mas nada disso prova que o pacote fala AMQP corretamente. Publicar e consumir contra um broker
/// real é o único teste que prova, e é o que este arquivo faz.
/// </para>
///
/// <para>
/// Precisa de RabbitMQ em <c>localhost:5672</c>. Para rodar:
/// </para>
/// <code>
/// podman run -d --name xa-rabbitmq --pids-limit=0 -p 5672:5672 -p 15672:15672 \
///     -e RABBITMQ_DEFAULT_USER=xa -e RABBITMQ_DEFAULT_PASS=xa-senha \
///     docker.io/library/rabbitmq:4-management
/// </code>
/// <para>
/// O endereço é <c>127.0.0.1</c> e não <c>localhost</c> de propósito: no Windows, a resolução de
/// <c>localhost</c> tenta IPv6 primeiro e cada conexão paga cerca de 57 segundos antes de cair
/// para IPv4. Estes três testes levavam 2m51s com <c>localhost</c> e levam 2s com o endereço
/// literal.
/// </para>
///
/// <para>
/// O <c>--pids-limit=0</c> não é capricho: sem ele o Podman sobre WSL falha a criar o contêiner
/// com erro de controlador de cgroup.
/// </para>
/// </summary>
public class IdaEVoltaNoBrokerTests
{
	/// <summary>
	/// O endereço do broker. Vem de <c>XA_RABBITMQ_URI</c> quando definida, para que o CI aponte
	/// para o contêiner de serviço dele, e cai no endereço local no resto do tempo.
	/// </summary>
	private static readonly string Broker =
		Environment.GetEnvironmentVariable("XA_RABBITMQ_URI")
			?? "amqp://xa:xa-senha@127.0.0.1:5672/";

	private sealed class Endereco
	{
		public ClienteDto Cliente { get; set; } = new();
	}

	private sealed class ClienteDto
	{
		public EnderecoDto Endereco { get; set; } = new();
	}

	private sealed class EnderecoDto
	{
		public string Cidade { get; set; } = string.Empty;
	}

	private sealed class Pedido
	{
		public int Id { get; set; }

		public string Status { get; set; } = string.Empty;
	}

	[Fact(DisplayName = "A published message should come back from the queue")]
	public async Task A_Published_Message_Should_Come_Back()
	{
		var fila = await FilaNova();

		await ScenarioDsl.Given()
			.Queue(fila)
			.WithConnectionUri(Broker)
			.Publish(new Pedido { Id = 42, Status = "Created" })
			.ExecuteAsync();

		var assercoes = await ScenarioDsl.Given()
			.Queue(fila)
			.WithConnectionUri(Broker)
			.Consume()
			.WithTimeout(TimeSpan.FromSeconds(10))
			.ExecuteAsync();

		assercoes
			.Then()
			.AssertSuccess()
			.AssertRoutingKey(fila)
			.AssertMessage<Pedido>(pedido =>
			{
				pedido.Id.ShouldBe(42);
				pedido.Status.ShouldBe("Created");
			});
	}

	// O caminho JSON contra mensagem que atravessou o broker de verdade, e não montada à mão. É o
	// mesmo navegador que os pacotes Http e Kafka usam, que mora no Core.
	[Fact(DisplayName = "JsonPath should read a value from a message that crossed the broker")]
	public async Task JsonPath_Should_Read_From_A_Real_Message()
	{
		var fila = await FilaNova();

		await ScenarioDsl.Given()
			.Queue(fila)
			.WithConnectionUri(Broker)
			.Publish(new { cliente = new { endereco = new { cidade = "Recife" } } })
			.ExecuteAsync();

		var assercoes = await ScenarioDsl.Given()
			.Queue(fila)
			.WithConnectionUri(Broker)
			.Consume()
			.WithTimeout(TimeSpan.FromSeconds(10))
			.ExecuteAsync();

		assercoes.Then().AssertSuccess();
		assercoes.Then().AssertMessage<Endereco>(e => e.Cliente.Endereco.Cidade.ShouldBe("Recife"));
	}

	// Headers AMQP atravessam o broker, e é a parte que mais fácil se quebra numa troca de versão
	// do cliente.
	[Fact(DisplayName = "An AMQP header should survive the round trip")]
	public async Task An_Amqp_Header_Should_Survive()
	{
		var fila = await FilaNova();

		await ScenarioDsl.Given()
			.Queue(fila)
			.WithConnectionUri(Broker)
			.Publish("corpo")
			.WithHeader("origem", "checkout")
			.ExecuteAsync();

		var assercoes = await ScenarioDsl.Given()
			.Queue(fila)
			.WithConnectionUri(Broker)
			.Consume()
			.WithTimeout(TimeSpan.FromSeconds(10))
			.ExecuteAsync();

		assercoes.Then().AssertSuccess().AssertHeader("origem");
	}

	// Consumir de fila vazia é falha com motivo, e não sucesso com mensagem nula. Contra o broker
	// real isso exercita o laço de espera até o prazo.
	[Fact(DisplayName = "Consuming from an empty queue should fail with a reason")]
	public async Task Consuming_From_An_Empty_Queue_Should_Fail()
	{
		var fila = await FilaNova();

		var assercoes = await ScenarioDsl.Given()
			.Queue(fila)
			.WithConnectionUri(Broker)
			.Consume()
			.WithTimeout(TimeSpan.FromSeconds(1))
			.ExecuteAsync();

		assercoes.Then().AssertFailure();
	}

	// O caso que o Kafka não tem: publicar numa exchange que não casa binding nenhum. O broker
	// aceita e descarta, então sem `mandatory` o passo diria que deu certo e nada teria chegado.
	[Fact(DisplayName = "Publishing where nothing is bound should fail, not report success")]
	public async Task Publishing_Where_Nothing_Is_Bound_Should_Fail()
	{
		var exchange = await ExchangeSemBinding();

		var assercoes = await ScenarioDsl.Given()
			.Exchange(exchange)
			.WithConnectionUri(Broker)
			.Publish("ninguém escuta")
			.WithRoutingKey("rota.sem.fila")
			.ExecuteAsync();

		assercoes.Then().AssertFailure();
	}

	// E o contrário, para que o padrão seja escolha e não imposição: quando publicar no vazio é o
	// ponto do teste, o verbo aceita.
	[Fact(DisplayName = "AllowingUnroutable should accept a publish that reaches no queue")]
	public async Task AllowingUnroutable_Should_Accept_It()
	{
		var exchange = await ExchangeSemBinding();

		var assercoes = await ScenarioDsl.Given()
			.Exchange(exchange)
			.WithConnectionUri(Broker)
			.Publish("ninguém escuta")
			.WithRoutingKey("rota.sem.fila")
			.AllowingUnroutable()
			.ExecuteAsync();

		assercoes.Then().AssertSuccess();
	}

	// O caminho completo de topologia, que antes não dava para escrever só com o DSL: declarar a
	// exchange, declarar a fila, ligar as duas, publicar e consumir.
	[Fact(DisplayName = "A message published through a bound exchange should arrive in the queue")]
	public async Task A_Message_Through_A_Bound_Exchange_Should_Arrive()
	{
		var exchange = $"xa-ex-{Guid.NewGuid():N}";
		var fila = $"xa-{Guid.NewGuid():N}";
		const string rota = "pedidos.criado";

		var topologia = await ScenarioDsl.Given()
			.Exchange(exchange)
			.WithConnectionUri(Broker)
			.DeclareExchange("topic")
			.And()
			.Queue(fila)
			.DeclareQueue()
			.And()
			.BindQueueTo(exchange, rota)
			.ExecuteAsync();

		topologia.Then().AssertSuccess();

		await ScenarioDsl.Given()
			.Exchange(exchange)
			.WithConnectionUri(Broker)
			.Publish("pelo caminho longo")
			.WithRoutingKey(rota)
			.ExecuteAsync();

		var assercoes = await ScenarioDsl.Given()
			.Queue(fila)
			.WithConnectionUri(Broker)
			.Consume()
			.WithTimeout(TimeSpan.FromSeconds(10))
			.ExecuteAsync();

		assercoes.Then().AssertSuccess().AssertMessage("pelo caminho longo").AssertRoutingKey(rota);
	}

	// Recusar com requeue devolve a mensagem à fila, então o consumo seguinte a encontra de novo.
	// Antes da recusa explícita isto não dava para escrever: o consumo confirmava ao chegar.
	[Fact(DisplayName = "A rejected message with requeue should be found again")]
	public async Task A_Rejected_Message_With_Requeue_Should_Be_Found_Again()
	{
		var fila = await FilaNova();

		await ScenarioDsl.Given()
			.Queue(fila).WithConnectionUri(Broker)
			.Publish("volta pra fila")
			.ExecuteAsync();

		var recusa = await ScenarioDsl.Given()
			.Queue(fila).WithConnectionUri(Broker)
			.Consume()
			.WithTimeout(TimeSpan.FromSeconds(10))
			.Rejecting(requeue: true)
			.ExecuteAsync();

		recusa.Then().AssertSuccess().AssertMessage("volta pra fila");

		var segundo = await ScenarioDsl.Given()
			.Queue(fila).WithConnectionUri(Broker)
			.Consume()
			.WithTimeout(TimeSpan.FromSeconds(10))
			.ExecuteAsync();

		segundo.Then().AssertSuccess().AssertMessage("volta pra fila");
	}

	// O teste que as pessoas querem escrever e não conseguiam: recusar sem requeue manda a mensagem
	// para o dead-letter da fila, e ela aparece do outro lado.
	[Fact(DisplayName = "A rejected message without requeue should land in the dead-letter queue")]
	public async Task A_Rejected_Message_Should_Land_In_The_Dead_Letter_Queue()
	{
		var sufixo = Guid.NewGuid().ToString("N");
		var dlx = $"xa-dlx-{sufixo}";
		var filaMorta = $"xa-morta-{sufixo}";
		var fila = $"xa-{sufixo}";

		var topologia = await ScenarioDsl.Given()
			.Exchange(dlx).WithConnectionUri(Broker)
			.DeclareExchange("fanout")
			.And()
			.Queue(filaMorta).DeclareQueue()
			.And()
			.BindQueueTo(dlx, string.Empty)
			.And()
			.Queue(fila).DeclareQueue(deadLetterExchange: dlx)
			.ExecuteAsync();

		topologia.Then().AssertSuccess();

		await ScenarioDsl.Given()
			.Queue(fila).WithConnectionUri(Broker)
			.Publish("vai morrer")
			.ExecuteAsync();

		var recusa = await ScenarioDsl.Given()
			.Queue(fila).WithConnectionUri(Broker)
			.Consume()
			.WithTimeout(TimeSpan.FromSeconds(10))
			.Rejecting()
			.ExecuteAsync();

		recusa.Then().AssertSuccess();

		var noDeadLetter = await ScenarioDsl.Given()
			.Queue(filaMorta).WithConnectionUri(Broker)
			.Consume()
			.WithTimeout(TimeSpan.FromSeconds(10))
			.ExecuteAsync();

		noDeadLetter.Then().AssertSuccess().AssertMessage("vai morrer");
	}

	[Fact(DisplayName = "A batch consume should bring every message published")]
	public async Task A_Batch_Consume_Should_Bring_Every_Message()
	{
		var fila = await FilaNova();

		for (var i = 1; i <= 3; i++)
		{
			await ScenarioDsl.Given()
				.Queue(fila).WithConnectionUri(Broker)
				.Publish($"mensagem {i}")
				.ExecuteAsync();
		}

		var assercoes = await ScenarioDsl.Given()
			.Queue(fila).WithConnectionUri(Broker)
			.ConsumeBatch(3)
			.WithTimeout(TimeSpan.FromSeconds(10))
			.ExecuteAsync();

		assercoes.Then().AssertSuccess().AssertMessageCount(3);
	}

	// Pedir mais do que existe é sucesso com o que havia, e não falha: o teste afirma a contagem, e
	// é essa afirmação que tem significado.
	[Fact(DisplayName = "A batch that finds fewer than asked should succeed with what there was")]
	public async Task A_Batch_With_Fewer_Should_Succeed_With_What_There_Was()
	{
		var fila = await FilaNova();

		await ScenarioDsl.Given()
			.Queue(fila).WithConnectionUri(Broker)
			.Publish("só uma")
			.ExecuteAsync();

		var assercoes = await ScenarioDsl.Given()
			.Queue(fila).WithConnectionUri(Broker)
			.ConsumeBatch(5)
			.WithTimeout(TimeSpan.FromSeconds(2))
			.ExecuteAsync();

		assercoes.Then().AssertSuccess().AssertMessageCount(1);
	}

	/// <summary>
	/// Declara uma exchange sem nenhuma fila ligada a ela, pelos verbos do próprio pacote.
	/// </summary>
	private static async Task<string> ExchangeSemBinding()
	{
		var nome = $"xa-ex-{Guid.NewGuid():N}";

		var resultado = await ScenarioDsl.Given()
			.Exchange(nome)
			.WithConnectionUri(Broker)
			.DeclareExchange()
			.ExecuteAsync();

		resultado.Then().AssertSuccess();

		return nome;
	}

	/// <summary>
	/// Declara uma fila com nome único para este teste, para que uma execução não veja a mensagem da
	/// outra. Antes dos verbos de topologia isto descia à API do cliente, que era o sinal de que
	/// faltava verbo.
	/// </summary>
	private static async Task<string> FilaNova()
	{
		var nome = $"xa-{Guid.NewGuid():N}";

		var resultado = await ScenarioDsl.Given()
			.Queue(nome)
			.WithConnectionUri(Broker)
			.DeclareQueue()
			.ExecuteAsync();

		resultado.Then().AssertSuccess();

		return nome;
	}
}
