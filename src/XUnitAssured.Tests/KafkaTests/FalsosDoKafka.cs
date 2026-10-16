using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Confluent.Kafka;

using NSubstitute;

using XUnitAssured.Core.Abstractions;
using XUnitAssured.Core.Storage;
using XUnitAssured.Kafka.Helpers;

namespace XUnitAssured.Tests.KafkaTests;

/// <summary>
/// Os falsos que deixam os passos do Kafka rodarem sem broker.
///
/// <para>
/// O produtor entra pela porta que a fixture já usa: o produtor compartilhado que ela põe no
/// contexto. O consumidor entra pela <see cref="IFabricaDeConsumidor"/>, a costura interna dos
/// passos de consumo. Em nenhum dos dois casos o passo sabe que está falando com um falso.
/// </para>
/// </summary>
internal static class FalsosDoKafka
{
	/// <summary>
	/// Um produtor que confirma toda mensagem como gravada, na partição e no offset informados.
	/// </summary>
	public static IProducer<string, string> ProdutorQueGrava(
		int particao = 0, long offset = 42, PersistenceStatus status = PersistenceStatus.Persisted)
	{
		var produtor = Substitute.For<IProducer<string, string>>();

		produtor.ProduceAsync(Arg.Any<string>(), Arg.Any<Message<string, string>>(), Arg.Any<CancellationToken>())
			.Returns(ci => Task.FromResult(Entrega(ci.ArgAt<string>(0), particao, offset, ci.ArgAt<Message<string, string>>(1), status)));

		produtor.ProduceAsync(Arg.Any<TopicPartition>(), Arg.Any<Message<string, string>>(), Arg.Any<CancellationToken>())
			.Returns(ci =>
			{
				var destino = ci.ArgAt<TopicPartition>(0);
				return Task.FromResult(Entrega(destino.Topic, destino.Partition.Value, offset, ci.ArgAt<Message<string, string>>(1), status));
			});

		return produtor;
	}

	/// <summary>
	/// Um produtor que nunca confirma: a entrega só termina quando o prazo do passo cancela.
	/// </summary>
	public static IProducer<string, string> ProdutorQueNaoResponde()
	{
		var produtor = Substitute.For<IProducer<string, string>>();

		produtor.ProduceAsync(Arg.Any<string>(), Arg.Any<Message<string, string>>(), Arg.Any<CancellationToken>())
			.Returns(ci => EsperarPeloCancelamento(ci.ArgAt<CancellationToken>(2)));

		return produtor;
	}

	/// <summary>
	/// Um produtor que falha na entrega, como um broker que recusa a mensagem.
	/// </summary>
	public static IProducer<string, string> ProdutorQueFalha(string motivo)
	{
		var produtor = Substitute.For<IProducer<string, string>>();

		produtor.ProduceAsync(Arg.Any<string>(), Arg.Any<Message<string, string>>(), Arg.Any<CancellationToken>())
			.Returns<Task<DeliveryResult<string, string>>>(_ => throw new KafkaException(new Error(ErrorCode.MsgSizeTooLarge, motivo)));

		return produtor;
	}

	/// <summary>
	/// Um contexto como o que a fixture semeia, com o produtor compartilhado.
	/// </summary>
	public static ContextoDeTeste ContextoComProdutor(IProducer<string, string> produtor, params string[] errosPendentes)
	{
		var contexto = new ContextoDeTeste();
		contexto.SetProperty("_KafkaSharedProducer", produtor);
		contexto.SetProperty("_KafkaSharedProducerErrors", new ConcurrentQueue<string>(errosPendentes));
		return contexto;
	}

	/// <summary>
	/// Uma mensagem consumida, como o cliente a entrega.
	/// </summary>
	public static ConsumeResult<string, string> Consumida(
		string topico, string? chave, string valor, int particao = 0, long offset = 0, Headers? cabecalhos = null) =>
		new()
		{
			Topic = topico,
			Partition = new Partition(particao),
			Offset = new Offset(offset),
			Message = new Message<string, string>
			{
				Key = chave!,
				Value = valor,
				Headers = cabecalhos ?? new Headers(),
				Timestamp = new Timestamp(new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc))
			}
		};

	private static DeliveryResult<string, string> Entrega(
		string topico, int particao, long offset, Message<string, string> mensagem, PersistenceStatus status) =>
		new()
		{
			Topic = topico,
			Partition = new Partition(particao),
			Offset = new Offset(offset),
			Message = mensagem,
			Status = status
		};

	private static async Task<DeliveryResult<string, string>> EsperarPeloCancelamento(CancellationToken token)
	{
		await Task.Delay(Timeout.Infinite, token).ConfigureAwait(false);
		throw new InvalidOperationException("unreachable: the delay only ends by cancellation");
	}
}

/// <summary>
/// Um consumidor falso e as partições do tópico, no lugar do cliente e do broker.
/// </summary>
internal sealed class FabricaFalsa : IFabricaDeConsumidor
{
	private readonly Queue<ConsumeResult<string, string>> _fila = new();

	public FabricaFalsa(params int[] particoes)
	{
		Particoes = particoes;

		Consumidor = Substitute.For<IConsumer<string, string>>();
		Consumidor.Consume(Arg.Any<TimeSpan>()).Returns(_ =>
		{
			lock (_fila)
				return _fila.Count > 0 ? _fila.Dequeue() : null!;
		});
		Consumidor.Committed(Arg.Any<IEnumerable<TopicPartition>>(), Arg.Any<TimeSpan>())
			.Returns(new List<TopicPartitionOffset>());
	}

	/// <summary>As partições que a descoberta devolve.</summary>
	public int[] Particoes { get; }

	/// <summary>O consumidor que o passo recebe.</summary>
	public IConsumer<string, string> Consumidor { get; }

	/// <summary>A configuração com que o passo pediu o consumidor.</summary>
	public ConsumerConfig? ConfigRecebida { get; private set; }

	/// <summary>O prazo que o passo deu à consulta de metadados.</summary>
	public TimeSpan? PrazoDosMetadados { get; private set; }

	/// <summary>Logs que o cliente "emite" assim que é criado.</summary>
	public List<LogMessage> LogsAoCriar { get; } = new();

	/// <summary>Erros que o cliente "emite" assim que é criado.</summary>
	public List<Error> ErrosAoCriar { get; } = new();

	/// <summary>Põe mensagens na fila; cada olhada do passo leva uma.</summary>
	public FabricaFalsa Chegam(params ConsumeResult<string, string>[] mensagens)
	{
		lock (_fila)
		{
			foreach (var mensagem in mensagens)
				_fila.Enqueue(mensagem);
		}

		return this;
	}

	public IConsumer<string, string> Criar(ConsumerConfig config, Action<Error> aoErrar, Action<LogMessage> aoLogar)
	{
		ConfigRecebida = config;

		foreach (var log in LogsAoCriar)
			aoLogar(log);
		foreach (var erro in ErrosAoCriar)
			aoErrar(erro);

		return Consumidor;
	}

	public List<TopicPartition> DescobrirParticoes(ConsumerConfig config, string topico, TimeSpan prazo)
	{
		PrazoDosMetadados = prazo;

		var particoes = new List<TopicPartition>();
		foreach (var particao in Particoes)
			particoes.Add(new TopicPartition(topico, new Partition(particao)));
		return particoes;
	}
}

/// <summary>
/// Um contexto de teste simples, com as propriedades num dicionário.
/// </summary>
internal sealed class ContextoDeTeste : ITestContext
{
	public IStepStorage Steps { get; } = new StepStorage();

	public IDictionary<string, object?> Properties { get; } = new Dictionary<string, object?>();

	public T? GetProperty<T>(string key) =>
		Properties.TryGetValue(key, out var valor) && valor is T tipado ? tipado : default;

	public void SetProperty<T>(string key, T? value) => Properties[key] = value;
}
