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
	private const string Broker = "amqp://xa:xa-senha@127.0.0.1:5672/";

	private sealed class Pedido
	{
		public int Id { get; set; }

		public string Status { get; set; } = string.Empty;
	}

	[Fact(Skip = "Integration test — requires a RabbitMQ broker on 127.0.0.1:5672",
		DisplayName = "A published message should come back from the queue")]
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

	// Headers AMQP atravessam o broker, e é a parte que mais fácil se quebra numa troca de versão
	// do cliente.
	[Fact(Skip = "Integration test — requires a RabbitMQ broker on 127.0.0.1:5672",
		DisplayName = "An AMQP header should survive the round trip")]
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
	[Fact(Skip = "Integration test — requires a RabbitMQ broker on 127.0.0.1:5672",
		DisplayName = "Consuming from an empty queue should fail with a reason")]
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
