using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

using XUnitAssured.Core.Abstractions;
using XUnitAssured.Core.Storage;
using XUnitAssured.RabbitMq.Results;
using XUnitAssured.RabbitMq.Steps;

namespace XUnitAssured.Tests.RabbitMqTests;

[Trait("Category", "RabbitMq")]
[Trait("Component", "Steps")]
/// <summary>
/// Como os passos se comportam quando o broker não está lá.
///
/// <para>
/// Nenhum teste aqui precisa de RabbitMQ. O endereço aponta para uma porta da interface local sem
/// nada escutando, com prazo curto, e o que se verifica é o contrato do resultado: que a falha é
/// falha, que ela diz por quê, que a credencial não vaza no diagnóstico, e que a precedência de
/// configuração respeita quem informou o valor.
/// </para>
/// </summary>
public class PassosDoRabbitMqTests
{
	/// <summary>Porta na interface local sem nada escutando.</summary>
	private const string BrokerInalcancavel = "amqp://convidado:senha-secreta@127.0.0.1:59097/";

	private const string OutroBroker = "amqp://convidado:senha-secreta@127.0.0.1:59096/";

	[Fact(DisplayName = "Publishing to an unreachable broker should fail, not throw")]
	public async Task Publishing_To_An_Unreachable_Broker_Should_Fail()
	{
		var passo = new RabbitMqPublishStep
		{
			RoutingKey = "pedidos",
			Value = new { id = 1 },
			ConnectionUri = BrokerInalcancavel
		};

		var resultado = (RabbitMqStepResult)await passo.ExecuteAsync(NovoContexto());

		resultado.Success.ShouldBeFalse();
		resultado.Errors.ShouldNotBeEmpty("a step that gives up must explain itself");
	}

	// A URI de conexão carrega usuário e senha, e o diagnóstico vai para o resultado do teste, que
	// costuma acabar num log de CI.
	[Fact(DisplayName = "The failure diagnostics must not carry the credential")]
	public async Task Failure_Diagnostics_Must_Not_Carry_The_Credential()
	{
		var passo = new RabbitMqPublishStep
		{
			RoutingKey = "pedidos",
			Value = "oi",
			ConnectionUri = BrokerInalcancavel
		};

		var resultado = (RabbitMqStepResult)await passo.ExecuteAsync(NovoContexto());

		var diagnostico = resultado.GetProperty<string>("ConnectionUri");
		diagnostico.ShouldNotBeNull();
		diagnostico!.Contains("senha-secreta").ShouldBeFalse("the credential must not reach a log");
		diagnostico.Contains("127.0.0.1").ShouldBeTrue("the host still has to be readable to be useful");
	}

	[Fact(DisplayName = "Consuming with nothing on the queue should report the timeout")]
	public async Task Consuming_With_Nothing_Should_Report_The_Timeout()
	{
		var passo = new RabbitMqConsumeStep
		{
			Queue = "pedidos",
			Timeout = TimeSpan.FromMilliseconds(300),
			ConnectionUri = BrokerInalcancavel
		};

		var resultado = (RabbitMqStepResult)await passo.ExecuteAsync(NovoContexto());

		resultado.Success.ShouldBeFalse();
		resultado.Errors.ShouldNotBeEmpty();
	}

	// O contrato que o pacote Kafka aprendeu a manter: propriedade de diagnóstico existe sempre,
	// ainda que vazia, para que o teste afirme comportamento e não presença.
	[Fact(DisplayName = "Result properties exist even when there is no message")]
	public void Result_Properties_Exist_Even_With_No_Message()
	{
		var resultado = RabbitMqStepResult.CreateConsumeTimeout("pedidos", TimeSpan.FromSeconds(1));

		resultado.Destination.ShouldBe("pedidos");
		resultado.Headers.ShouldNotBeNull();
		resultado.Headers.Count.ShouldBe(0);
		resultado.Message.ShouldBeNull();
	}

	[Fact(DisplayName = "Reading a message that is not there should say so")]
	public void Reading_A_Message_That_Is_Not_There_Should_Say_So()
	{
		var resultado = RabbitMqStepResult.CreateConsumeTimeout("pedidos", TimeSpan.FromSeconds(1));

		Should.Throw<InvalidOperationException>(() => resultado.GetMessage<string>());
	}

	// A mesma regra de precedência que o lado Kafka passou a seguir: informado ganha do contexto,
	// e a ausência é registrada em vez de deduzida comparando com o padrão.
	[Fact(DisplayName = "An explicit connection URI wins over the fixture's")]
	public async Task An_Explicit_Uri_Wins_Over_The_Fixtures()
	{
		var contexto = NovoContexto();
		contexto.SetProperty("_RabbitMqConnectionUri", OutroBroker);

		var passo = new RabbitMqConsumeStep
		{
			Queue = "pedidos",
			Timeout = TimeSpan.FromMilliseconds(200),
			ConnectionUri = BrokerInalcancavel
		};

		var resultado = (RabbitMqStepResult)await passo.ExecuteAsync(contexto);

		resultado.GetProperty<string>("ConnectionUri")!.Contains("59097").ShouldBeTrue(
			"the step was told which broker to use");
	}

	[Fact(DisplayName = "With no URI configured, the fixture's wins")]
	public async Task With_No_Uri_Configured_The_Fixtures_Wins()
	{
		var contexto = NovoContexto();
		contexto.SetProperty("_RabbitMqConnectionUri", OutroBroker);

		var passo = new RabbitMqConsumeStep
		{
			Queue = "pedidos",
			Timeout = TimeSpan.FromMilliseconds(200)
		};

		var resultado = (RabbitMqStepResult)await passo.ExecuteAsync(contexto);

		resultado.GetProperty<string>("ConnectionUri")!.Contains("59096").ShouldBeTrue(
			"nothing was configured on the step, so the fixture's value is the right one");
	}

	// A armadilha herdada do lado Kafka, guardada antes de alguém tropeçar nela: o construtor de
	// cópia tem de copiar o campo e não a propriedade.
	[Fact(DisplayName = "Rebuilding a step must not invent an explicit URI")]
	public async Task Rebuilding_Must_Not_Invent_An_Explicit_Uri()
	{
		var contexto = NovoContexto();
		contexto.SetProperty("_RabbitMqConnectionUri", OutroBroker);

		var semNada = new RabbitMqConsumeStep { Queue = "pedidos" };
		var reconstruido = new RabbitMqConsumeStep(semNada)
		{
			Timeout = TimeSpan.FromMilliseconds(200)
		};

		var resultado = (RabbitMqStepResult)await reconstruido.ExecuteAsync(contexto);

		resultado.GetProperty<string>("ConnectionUri")!.Contains("59096").ShouldBeTrue(
			"nothing was ever configured, so rebuilding must not have invented a value");
	}

	[Fact(DisplayName = "Cancellation should reach the consume step")]
	public async Task Cancellation_Should_Reach_The_Consume_Step()
	{
		using var fonte = new CancellationTokenSource();
		fonte.Cancel();

		var passo = new RabbitMqConsumeStep
		{
			Queue = "pedidos",
			Timeout = TimeSpan.FromSeconds(30),
			ConnectionUri = BrokerInalcancavel
		};

		var resultado = (RabbitMqStepResult)await passo.ExecuteAsync(NovoContexto(), fonte.Token);

		resultado.Success.ShouldBeFalse();
	}

	private static ContextoDeTeste NovoContexto() => new();

	private sealed class ContextoDeTeste : ITestContext
	{
		public IStepStorage Steps { get; } = new StepStorage();

		public IDictionary<string, object?> Properties { get; } = new Dictionary<string, object?>();

		public T? GetProperty<T>(string key) =>
			Properties.TryGetValue(key, out var valor) && valor is T tipado ? tipado : default;

		public void SetProperty<T>(string key, T? value) => Properties[key] = value;
	}
}
