using XUnitAssured.Core.Results;
using System.Threading;
using System;
using System.Threading.Tasks;

using XUnitAssured.Core.Abstractions;
using XUnitAssured.Kafka.Abstractions;
using XUnitAssured.Kafka.Extensions;

namespace XUnitAssured.Kafka.DSL;

/// <summary>
/// Carries the Kafka type along the chain, delegating everything else to the scenario
/// it wraps.
///
/// <para>
/// There is no second scenario here: the context, the current step and the execution all
/// belong to the original one. This type adds a single thing — being an
/// <see cref="IKafkaScenario"/> — and that is what lets the compiler resolve <c>Execute()</c>
/// without the caller naming a class.
/// </para>
/// </summary>
internal sealed class KafkaScenario : IKafkaScenario
{
	private readonly ITestScenario _cenario;

	private KafkaScenario(ITestScenario cenario) => _cenario = cenario;

	/// <summary>
	/// Wraps the scenario, or hands back the wrapper it already is.
	///
	/// A chain calls this at every step, so wrapping blindly would allocate a wrapper per
	/// call and nest them.
	/// </summary>
	internal static IKafkaScenario De(ITestScenario cenario) =>
		cenario as IKafkaScenario ?? new KafkaScenario(cenario);

	public ITestContext Context => _cenario.Context;

	public ITestStep? CurrentStep => _cenario.CurrentStep;

	public void SetCurrentStep(ITestStep step) => _cenario.SetCurrentStep(step);

	public void AddValidation(Action<ITestStepResult> validation) => _cenario.AddValidation(validation);

	public Task ExecutePendingAsync(CancellationToken cancellationToken = default) =>
		_cenario.ExecutePendingAsync(cancellationToken);

	public IKafkaScenario And()
	{
		_cenario.And();
		return this;
	}

	public IKafkaScenario On()
	{
		_cenario.On();
		return this;
	}

	public IKafkaScenario When()
	{
		_cenario.When();
		return this;
	}

	public IKafkaScenario Then()
	{
		_cenario.Then();
		return this;
	}

	public async Task<KafkaValidationBuilder> ExecuteAsync(CancellationToken cancellationToken = default)
	{
		await _cenario.ExecutePendingAsync(cancellationToken).ConfigureAwait(false);
		return new KafkaValidationBuilder(_cenario);
	}

	public KafkaValidationBuilder Execute() => ExecuteAsync().GetAwaiter().GetResult();

	// Quem enxerga este objeto como ITestScenario continua recebendo o mesmo encadeamento;
	// o tipo de retorno é o único detalhe que muda.
	ITestScenario ITestScenario.And() => And();

	ITestScenario ITestScenario.On() => On();

	ITestScenario ITestScenario.When() => When();

	ITestScenario ITestScenario.Then() => Then();
}
