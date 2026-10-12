using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

using XUnitAssured.Core.Abstractions;
using XUnitAssured.Core.DSL;
using XUnitAssured.Core.Results;
using XUnitAssured.RabbitMq.Extensions;
using XUnitAssured.RabbitMq.Results;

namespace XUnitAssured.Tests.RabbitMqTests;

/// <summary>
/// O que cada fábrica de <see cref="RabbitMqStepResult"/> diz sobre o passo: se deu certo, o
/// status e as propriedades que o validador lê (#163).
///
/// <para>
/// A primeira rodada de mutação do RabbitMq (#159) mostrou que nenhum teste conferia isso. O caso
/// mais sério é o do consumo que esgota o prazo: virando sucesso, um teste que não recebeu
/// mensagem nenhuma passaria. São métodos estáticos, então não precisam de broker.
/// </para>
/// </summary>
[Trait("Category", "RabbitMq")]
[Trait("Component", "Results")]
public class ResultadosDoRabbitMqTests
{
	private static readonly TimeSpan Decorrido = TimeSpan.FromSeconds(2);

	[Fact(DisplayName = "A consume that timed out is a failure that names the queue and the timeout, with no message")]
	public void Consume_Timeout_Is_A_Failure()
	{
		var resultado = RabbitMqStepResult.CreateConsumeTimeout("pedidos", TimeSpan.FromSeconds(1.5), Decorrido);

		resultado.Success.ShouldBeFalse();
		resultado.Metadata.Status.ShouldBe(StepStatus.Failed);
		resultado.Errors.ShouldHaveSingleItem().ShouldBe("No message arrived on 'pedidos' within 1.5s.");
		resultado.Destination.ShouldBe("pedidos");
		resultado.Message.ShouldBeNull();
		resultado.Messages.ShouldBeEmpty();
		resultado.RemainingMessageCount.ShouldBeNull();
		resultado.Headers.ShouldBeEmpty();
	}

	// O que importa da falha: o teste que esperava a mensagem não pode passar.
	[Fact(DisplayName = "AssertSuccess fails on a consume that timed out")]
	public void Assert_Success_Fails_On_A_Timeout()
	{
		var validacao = Validacao(RabbitMqStepResult.CreateConsumeTimeout("pedidos", TimeSpan.FromSeconds(1)));

		Should.Throw<ShouldAssertException>(() => validacao.AssertSuccess())
			.Message.ShouldContain("No message arrived on 'pedidos'");
	}

	[Fact(DisplayName = "A publish the broker accepted is a success with its destination and routing key, and no message")]
	public void Publish_Success()
	{
		var resultado = RabbitMqStepResult.CreatePublishSuccess("pedidos.exchange", "pedido.criado", Decorrido);

		resultado.Success.ShouldBeTrue();
		resultado.Errors.ShouldBeEmpty();
		resultado.Metadata.Status.ShouldBe(StepStatus.Succeeded);
		resultado.Destination.ShouldBe("pedidos.exchange");
		resultado.RoutingKey.ShouldBe("pedido.criado");
		resultado.Message.ShouldBeNull();
		resultado.Messages.ShouldBeEmpty();
		resultado.RemainingMessageCount.ShouldBeNull();
		resultado.Headers.ShouldBeEmpty();
		Validacao(resultado).AssertSuccess();
	}

	[Fact(DisplayName = "A consume that got a message is a success with the message, its routing key, headers and what was left")]
	public void Consume_Success()
	{
		var cabecalhos = new Dictionary<string, object?> { ["x-tentativa"] = 2 };

		var resultado = RabbitMqStepResult.CreateConsumeSuccess("pedidos", "pedido.criado", "ola", 3, cabecalhos, Decorrido);

		resultado.Success.ShouldBeTrue();
		resultado.Errors.ShouldBeEmpty();
		resultado.Metadata.Status.ShouldBe(StepStatus.Succeeded);
		resultado.Destination.ShouldBe("pedidos");
		resultado.RoutingKey.ShouldBe("pedido.criado");
		resultado.Message.ShouldBe("ola");
		resultado.Messages.ShouldBe(["ola"]);
		resultado.RemainingMessageCount.ShouldBe(3u);
		resultado.Headers["x-tentativa"].ShouldBe(2);
		resultado.Data.ShouldBe("ola");
	}

	// Trazer menos do que se pediu é sucesso: quantas havia é o que AssertMessageCount afirma.
	[Fact(DisplayName = "A batch consume is a success with the messages in order, the first as Message")]
	public void Batch_Consume_Success()
	{
		var resultado = RabbitMqStepResult.CreateBatchConsumeSuccess("pedidos", ["um", "dois"], 0, Decorrido);

		resultado.Success.ShouldBeTrue();
		resultado.Errors.ShouldBeEmpty();
		resultado.Metadata.Status.ShouldBe(StepStatus.Succeeded);
		resultado.Destination.ShouldBe("pedidos");
		resultado.RoutingKey.ShouldBeNull();
		resultado.Message.ShouldBe("um");
		resultado.Messages.ShouldBe(["um", "dois"]);
		resultado.RemainingMessageCount.ShouldBe(0u);
		resultado.Headers.ShouldBeEmpty();
		Validacao(resultado).AssertMessageCount(2);
	}

	[Fact(DisplayName = "A failure carries the exception, the destination and the step's diagnostics, which may replace a property")]
	public void Failure_Carries_The_Diagnostics()
	{
		var erro = new TimeoutException("o broker não respondeu");

		var resultado = RabbitMqStepResult.CreateFailure(
			erro,
			"pedidos",
			new Dictionary<string, object?> { ["Broker"] = "amqp://***@broker:5672/", ["RoutingKey"] = "pedido.criado" },
			Decorrido);

		resultado.Success.ShouldBeFalse();
		resultado.Metadata.Status.ShouldBe(StepStatus.Failed);
		resultado.Errors.ShouldHaveSingleItem().ShouldBe("o broker não respondeu");
		resultado.Destination.ShouldBe("pedidos");
		resultado.Message.ShouldBeNull();
		resultado.Messages.ShouldBeEmpty();
		resultado.Headers.ShouldBeEmpty();
		resultado.GetProperty<string>("ExceptionType").ShouldBe(typeof(TimeoutException).FullName);
		resultado.GetProperty<string>("ExceptionDetail")!.ShouldContain("o broker não respondeu");
		resultado.GetProperty<string>("Broker").ShouldBe("amqp://***@broker:5672/");
		resultado.RoutingKey.ShouldBe("pedido.criado");
	}

	[Fact(DisplayName = "A failure needs the exception")]
	public void Failure_Needs_The_Exception() =>
		Should.Throw<ArgumentNullException>(() => RabbitMqStepResult.CreateFailure(null!));

	// Os quatro caminhos passam pelos mesmos metadados; um teste por fábrica seria o mesmo teste.
	[Theory(DisplayName = "A result starts the elapsed time before it completes, or when it completes when none is given")]
	[InlineData(true)]
	[InlineData(false)]
	public void Started_At_Is_The_Elapsed_Time_Before(bool comDecorrido)
	{
		var resultado = RabbitMqStepResult.CreateConsumeTimeout("pedidos", TimeSpan.FromSeconds(1), comDecorrido ? Decorrido : null);

		var duracao = resultado.Metadata.CompletedAt!.Value - resultado.Metadata.StartedAt;
		duracao.ShouldBeInRange(
			comDecorrido ? Decorrido : TimeSpan.Zero,
			(comDecorrido ? Decorrido : TimeSpan.Zero) + TimeSpan.FromSeconds(1));
	}

	// ---------- Apoio ----------

	/// <summary>O validador sobre um cenário cujo passo já executou e deu este resultado.</summary>
	private static RabbitMqValidationBuilder Validacao(RabbitMqStepResult resultado)
	{
		var cenario = ScenarioDsl.Given();
		cenario.SetCurrentStep(new PassoPronto(resultado));
		return new RabbitMqValidationBuilder(cenario);
	}

	private sealed class PassoPronto(ITestStepResult resultado) : ITestStep
	{
		public string? Name { get; set; }
		public string StepType => "Pronto";
		public ITestStepResult? Result { get; } = resultado;
		public bool IsExecuted => true;
		public bool IsValid { get; private set; }

		public Task<ITestStepResult> ExecuteAsync(ITestContext context, CancellationToken cancellationToken = default) =>
			Task.FromResult(Result!);

		public void Validate(Action<ITestStepResult> validation)
		{
			validation(Result!);
			IsValid = true;
		}
	}
}
