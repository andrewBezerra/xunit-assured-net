using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using NSubstitute;

using RabbitMQ.Client;
using RabbitMQ.Client.Events;

using XUnitAssured.Core.Abstractions;
using XUnitAssured.RabbitMq.Steps;

namespace XUnitAssured.Tests.RabbitMqTests;

/// <summary>
/// Um broker de mentira para os passos do RabbitMQ: entrega uma fábrica de conexão falsa, registra
/// o que o passo publicou e devolve as mensagens que o teste deixou na fila.
/// </summary>
/// <remarks>
/// O passo recebe a fábrica pela propriedade interna <c>CriarFabrica</c>. O canal é um falso do
/// NSubstitute, então o que se verifica é a conversa do passo com o cliente oficial: o que ele
/// declarou, com que argumentos publicou, o que confirmou ou recusou, e se fechou o que abriu.
/// </remarks>
internal sealed class BrokerDeMentira
{
	private readonly Queue<BasicGetResult> _fila = new();
	private ulong _proximaEntrega = 1;

	public BrokerDeMentira()
	{
		Fabrica = Substitute.For<IConnectionFactory>();
		Conexao = Substitute.For<IConnection>();
		Canal = Substitute.For<IChannel>();

		Fabrica.CreateConnectionAsync(Arg.Any<CancellationToken>())
			.Returns(_ => Task.FromResult(Conexao));

		Conexao.CreateChannelAsync(Arg.Any<CreateChannelOptions?>(), Arg.Any<CancellationToken>())
			.Returns(chamada =>
			{
				OpcoesDoCanal = chamada.ArgAt<CreateChannelOptions?>(0);
				return Task.FromResult(Canal);
			});

		Canal.BasicGetAsync(Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
			.Returns(chamada =>
			{
				FilasPerguntadas.Add(chamada.ArgAt<string>(0));
				AutoAck.Add(chamada.ArgAt<bool>(1));
				return Task.FromResult(_fila.Count > 0 ? _fila.Dequeue() : null);
			});

		Canal
			.When(c => c.BasicPublishAsync(
				Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(),
				Arg.Any<BasicProperties>(), Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<CancellationToken>()))
			.Do(chamada =>
			{
				var propriedades = chamada.ArgAt<BasicProperties>(3);
				Publicadas.Add(new Publicada(
					chamada.ArgAt<string>(0),
					chamada.ArgAt<string>(1),
					chamada.ArgAt<bool>(2),
					propriedades.Headers,
					Encoding.UTF8.GetString(chamada.ArgAt<ReadOnlyMemory<byte>>(4).Span)));

				// O broker devolve a mensagem antes de confirmar a publicação, quando nada casou.
				if (DevolverComo is { } devolucao)
				{
					Canal.BasicReturnAsync += Raise.Event<AsyncEventHandler<BasicReturnEventArgs>>(
						Canal,
						new BasicReturnEventArgs(
							devolucao.Codigo, devolucao.Texto, chamada.ArgAt<string>(0), chamada.ArgAt<string>(1),
							propriedades, chamada.ArgAt<ReadOnlyMemory<byte>>(4)));
				}
			});
	}

	public IConnectionFactory Fabrica { get; }

	public IConnection Conexao { get; }

	public IChannel Canal { get; }

	/// <summary>A URI que o passo resolveu e entregou à fábrica.</summary>
	public string? UriRecebida { get; private set; }

	/// <summary>As opções com que o passo abriu o canal.</summary>
	public CreateChannelOptions? OpcoesDoCanal { get; private set; }

	public List<Publicada> Publicadas { get; } = new();

	public List<string> FilasPerguntadas { get; } = new();

	public List<bool> AutoAck { get; } = new();

	/// <summary>Quando posto, o broker devolve cada publicação como não roteável.</summary>
	public (ushort Codigo, string Texto)? DevolverComo { get; set; }

	/// <summary>A função que o passo chama para obter a fábrica, registrando a URI.</summary>
	public Func<string, IConnectionFactory> CriarFabrica => uri =>
	{
		UriRecebida = uri;
		return Fabrica;
	};

	/// <summary>Deixa uma mensagem na fila, como se um produtor a tivesse publicado.</summary>
	public BrokerDeMentira ComMensagem(
		string corpo, string routingKey = "pedidos", IDictionary<string, object?>? cabecalhos = null)
	{
		var propriedades = new BasicProperties { Headers = cabecalhos };
		_fila.Enqueue(new BasicGetResult(
			_proximaEntrega++, redelivered: false, exchange: string.Empty, routingKey: routingKey,
			messageCount: 0, basicProperties: propriedades, body: Encoding.UTF8.GetBytes(corpo)));

		return this;
	}

	/// <summary>
	/// Acerta o número de mensagens que restam em cada entrega, do jeito que o broker conta: a
	/// primeira diz quantas ficaram atrás dela.
	/// </summary>
	public BrokerDeMentira ContandoORestante()
	{
		var entregas = _fila.ToArray();
		_fila.Clear();
		for (var i = 0; i < entregas.Length; i++)
		{
			var e = entregas[i];
			_fila.Enqueue(new BasicGetResult(
				e.DeliveryTag, e.Redelivered, e.Exchange, e.RoutingKey,
				(uint)(entregas.Length - i - 1), e.BasicProperties, e.Body));
		}

		return this;
	}

	/// <summary>Põe este broker no passo que a cadeia descreveu por último.</summary>
	public void Ligar(ITestScenario cenario)
	{
		ITestStep passo = cenario.CurrentStep switch
		{
			RabbitMqPublishStep p => new RabbitMqPublishStep(p) { CriarFabrica = CriarFabrica },
			RabbitMqConsumeStep c => new RabbitMqConsumeStep(c) { CriarFabrica = CriarFabrica },
			RabbitMqTopologyStep t => new RabbitMqTopologyStep(t) { CriarFabrica = CriarFabrica },
			_ => throw new InvalidOperationException("The chain has no RabbitMQ step to connect.")
		};

		cenario.SetCurrentStep(passo);
	}
}

internal sealed record Publicada(
	string Exchange, string RoutingKey, bool Mandatory, IDictionary<string, object?>? Cabecalhos, string Corpo);
