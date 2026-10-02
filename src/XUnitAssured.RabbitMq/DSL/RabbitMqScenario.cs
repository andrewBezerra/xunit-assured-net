using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using XUnitAssured.Core.Abstractions;
using XUnitAssured.Core.Results;
using XUnitAssured.RabbitMq.Abstractions;
using XUnitAssured.RabbitMq.Extensions;
using XUnitAssured.RabbitMq.Results;

namespace XUnitAssured.RabbitMq.DSL;

/// <summary>
/// Carrega o tipo de RabbitMQ pela cadeia, delegando o resto ao cenário que envolve.
/// </summary>
/// <remarks>
/// Não há um segundo cenário aqui: o contexto, o passo atual e a execução pertencem ao original.
/// Este tipo acrescenta uma coisa só, ser um <see cref="IRabbitMqScenario"/>, e é isso que faz o
/// compilador resolver os verbos sem o chamador nomear classe nenhuma.
/// </remarks>
internal sealed class RabbitMqScenario : IRabbitMqScenario
{
	private readonly ITestScenario _cenario;

	private RabbitMqScenario(ITestScenario cenario) => _cenario = cenario;

	/// <summary>
	/// Envolve o cenário, ou devolve o invólucro que ele já é. Uma cadeia chama isto a cada
	/// passo, então envolver às cegas aninharia invólucros.
	/// </summary>
	internal static IRabbitMqScenario De(ITestScenario cenario) =>
		cenario as IRabbitMqScenario ?? new RabbitMqScenario(cenario);

	public ITestContext Context => _cenario.Context;

	public ITestStep? CurrentStep => _cenario.CurrentStep;

	public void SetCurrentStep(ITestStep step) => _cenario.SetCurrentStep(step);

	public void AddValidation(Action<ITestStepResult> validation) => _cenario.AddValidation(validation);

	public Task ExecutePendingAsync(CancellationToken cancellationToken = default) =>
		_cenario.ExecutePendingAsync(cancellationToken);

	public IRabbitMqScenario And()
	{
		_cenario.And();
		return this;
	}

	public IRabbitMqScenario On()
	{
		_cenario.On();
		return this;
	}

	public IRabbitMqScenario When()
	{
		_cenario.When();
		return this;
	}

	public IRabbitMqScenario Then()
	{
		_cenario.Then();
		return this;
	}

	public IRabbitMqScenario DeclareQueue(string? deadLetterExchange = null) =>
		RabbitMqVerbos.DeclareQueue(_cenario, deadLetterExchange);

	public IRabbitMqScenario DeclareExchange(string type = "direct") =>
		RabbitMqVerbos.DeclareExchange(_cenario, type);

	public IRabbitMqScenario BindQueueTo(string exchange, string routingKey) =>
		RabbitMqVerbos.BindQueueTo(_cenario, exchange, routingKey);

	public IRabbitMqScenario Publish(object value) => RabbitMqVerbos.Publish(_cenario, value);

	public IRabbitMqScenario Consume() => RabbitMqVerbos.Consume(_cenario);

	public IRabbitMqScenario ConsumeBatch(int count) =>
		RabbitMqVerbos.ConsumeBatch(_cenario, count);

	public IRabbitMqScenario WithRoutingKey(string routingKey) =>
		RabbitMqVerbos.WithRoutingKey(_cenario, routingKey);

	public IRabbitMqScenario WithHeader(string name, object? value) =>
		RabbitMqVerbos.WithHeader(_cenario, name, value);

	public IRabbitMqScenario WithHeaders(IDictionary<string, object?> headers) =>
		RabbitMqVerbos.WithHeaders(_cenario, headers);

	public IRabbitMqScenario AllowingUnroutable() => RabbitMqVerbos.AllowingUnroutable(_cenario);

	public IRabbitMqScenario Rejecting(bool requeue = false) =>
		RabbitMqVerbos.Rejecting(_cenario, requeue);

	public IRabbitMqScenario WithTimeout(TimeSpan timeout) =>
		RabbitMqVerbos.WithTimeout(_cenario, timeout);

	public IRabbitMqScenario WithConnectionUri(string connectionUri) =>
		RabbitMqVerbos.WithConnectionUri(_cenario, connectionUri);

	public IRabbitMqScenario Validate(Action<RabbitMqStepResult> validation) =>
		RabbitMqVerbos.Validate(_cenario, validation);

	public IRabbitMqScenario ValidateMessage<T>(Action<T> validation) where T : class =>
		RabbitMqVerbos.ValidateMessage(_cenario, validation);

	public async Task<RabbitMqValidationBuilder> ExecuteAsync(
		CancellationToken cancellationToken = default)
	{
		await _cenario.ExecutePendingAsync(cancellationToken).ConfigureAwait(false);
		return new RabbitMqValidationBuilder(_cenario);
	}

	public RabbitMqValidationBuilder Execute() => ExecuteAsync().GetAwaiter().GetResult();

	// Quem enxerga este objeto como ITestScenario continua recebendo o mesmo encadeamento; o tipo
	// de retorno é o único detalhe que muda.
	ITestScenario ITestScenario.And() => And();

	ITestScenario ITestScenario.On() => On();

	ITestScenario ITestScenario.When() => When();

	ITestScenario ITestScenario.Then() => Then();
}
