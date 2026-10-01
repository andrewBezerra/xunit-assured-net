using System.Threading.Tasks;

using XUnitAssured.Core.Abstractions;
using XUnitAssured.Playwright.Abstractions;
using XUnitAssured.Playwright.Extensions;

namespace XUnitAssured.Playwright.DSL;

/// <summary>
/// Carries the browser type along the chain, delegating everything else to the scenario
/// it wraps.
///
/// <para>
/// There is no second scenario here: the context, the current step and the execution all
/// belong to the original one. This type adds a single thing — being an
/// <see cref="IBrowserScenario"/> — and that is what lets the compiler resolve <c>Execute()</c>
/// without the caller naming a class.
/// </para>
/// </summary>
internal sealed class BrowserScenario : IBrowserScenario
{
	private readonly ITestScenario _cenario;

	private BrowserScenario(ITestScenario cenario) => _cenario = cenario;

	/// <summary>
	/// Wraps the scenario, or hands back the wrapper it already is.
	///
	/// A chain calls this at every step, so wrapping blindly would allocate a wrapper per
	/// call and nest them.
	/// </summary>
	internal static IBrowserScenario De(ITestScenario cenario) =>
		cenario as IBrowserScenario ?? new BrowserScenario(cenario);

	public ITestContext Context => _cenario.Context;

	public ITestStep? CurrentStep => _cenario.CurrentStep;

	public void SetCurrentStep(ITestStep step) => _cenario.SetCurrentStep(step);

	public Task ExecuteCurrentStepAsync() => _cenario.ExecuteCurrentStepAsync();

	public IBrowserScenario And()
	{
		_cenario.And();
		return this;
	}

	public IBrowserScenario On()
	{
		_cenario.On();
		return this;
	}

	public IBrowserScenario When()
	{
		_cenario.When();
		return this;
	}

	public IBrowserScenario Then()
	{
		_cenario.Then();
		return this;
	}

	public PlaywrightValidationBuilder Execute()
	{
		_cenario.ExecuteCurrentStepAsync().GetAwaiter().GetResult();
		return new PlaywrightValidationBuilder(_cenario);
	}

	// Quem enxerga este objeto como ITestScenario continua recebendo o mesmo encadeamento;
	// o tipo de retorno é o único detalhe que muda.
	ITestScenario ITestScenario.And() => And();

	ITestScenario ITestScenario.On() => On();

	ITestScenario ITestScenario.When() => When();

	ITestScenario ITestScenario.Then() => Then();
}
