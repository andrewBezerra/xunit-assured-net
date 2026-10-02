using System;
using System.Threading.Tasks;

using RabbitMQ.Client;

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

	/// <summary>
	/// Declara uma exchange sem nenhuma fila ligada a ela.
	/// </summary>
	private static async Task<string> ExchangeSemBinding()
	{
		var nome = $"xa-ex-{Guid.NewGuid():N}";

		var fabrica = new ConnectionFactory { Uri = new Uri(Broker) };
		await using var conexao = await fabrica.CreateConnectionAsync();
		await using var canal = await conexao.CreateChannelAsync();

		await canal.ExchangeDeclareAsync(nome, type: "direct", durable: true, autoDelete: false);

		return nome;
	}

	/// <summary>
	/// Declara uma fila com nome único para este teste, para que uma execução não veja a mensagem
	/// da outra. O pacote ainda não tem verbo de declaração, então aqui usa-se o cliente direto.
	/// </summary>
	private static async Task<string> FilaNova()
	{
		var nome = $"xa-{Guid.NewGuid():N}";

		var fabrica = new ConnectionFactory { Uri = new Uri(Broker) };
		await using var conexao = await fabrica.CreateConnectionAsync();
		await using var canal = await conexao.CreateChannelAsync();

		// Durável de propósito: o RabbitMQ 4 recusa fila transitória não exclusiva, com
		// INTERNAL_ERROR e a mensagem de que `transient_nonexcl_queues` está em depreciação.
		await canal.QueueDeclareAsync(nome, durable: true, exclusive: false, autoDelete: false);

		return nome;
	}
}
