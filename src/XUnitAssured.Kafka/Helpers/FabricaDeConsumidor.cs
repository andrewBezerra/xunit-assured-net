using System;
using System.Collections.Generic;

using Confluent.Kafka;

namespace XUnitAssured.Kafka.Helpers;

/// <summary>
/// De onde os passos de consumo tiram o consumidor e as partições do tópico.
/// </summary>
/// <remarks>
/// Existe para que o laço de consumo — esperar, juntar mensagens, desistir no prazo — possa ser
/// verificado sem broker. Em produção há uma só implementação, <see cref="FabricaDeConsumidor.Real"/>, que faz o que
/// os passos sempre fizeram: <see cref="ConsumerBuilder{TKey, TValue}"/> e uma consulta de
/// metadados pelo cliente de administração.
/// </remarks>
internal interface IFabricaDeConsumidor
{
	/// <summary>
	/// Cria o consumidor, ligando os erros e os logs do cliente aos callbacks.
	/// </summary>
	IConsumer<string, string> Criar(ConsumerConfig config, Action<Error> aoErrar, Action<LogMessage> aoLogar);

	/// <summary>
	/// As partições do tópico; vazia quando o tópico não existe.
	/// </summary>
	List<TopicPartition> DescobrirParticoes(ConsumerConfig config, string topico, TimeSpan prazo);
}

/// <summary>
/// A fábrica de verdade: fala com o broker.
/// </summary>
internal sealed class FabricaDeConsumidor : IFabricaDeConsumidor
{
	/// <summary>A instância que os passos usam quando ninguém informa outra.</summary>
	public static readonly IFabricaDeConsumidor Real = new FabricaDeConsumidor();

	private FabricaDeConsumidor()
	{
	}

	public IConsumer<string, string> Criar(ConsumerConfig config, Action<Error> aoErrar, Action<LogMessage> aoLogar) =>
		new ConsumerBuilder<string, string>(config)
			.SetErrorHandler((_, error) => aoErrar(error))
			.SetLogHandler((_, log) => aoLogar(log))
			.Build();

	public List<TopicPartition> DescobrirParticoes(ConsumerConfig config, string topico, TimeSpan prazo) =>
		ConsumerAssignment.DiscoverPartitions(config, topico, prazo);
}
