using System;

using XUnitAssured.Core.Abstractions;
using XUnitAssured.Playwright.Abstractions;
using XUnitAssured.Playwright.DSL;
using XUnitAssured.Playwright.Results;

namespace XUnitAssured.Playwright.Extensions;

/// <summary>
/// Checks and captures on a browser step's result, run when the chain executes.
/// </summary>
public static class PlaywrightValidationExtensions
{
	/// <summary>
	/// Registers a check or a capture on the current browser step's result, run once the step has
	/// executed — e.g. keeping the token a <c>FetchFromPage</c> returned for the next step.
	/// </summary>
	/// <remarks>
	/// Type the lambda's parameter (<c>(PlaywrightStepResult r) =&gt; ...</c>) when the project also
	/// references another XUnitAssured package: each package has its own <c>Validate</c>, and the
	/// parameter type is what picks this one.
	/// </remarks>
	/// <param name="scenario">The test scenario</param>
	/// <param name="validation">The check or capture</param>
	/// <returns>The scenario, typed as a browser scenario</returns>
	public static IBrowserScenario Validate(this ITestScenario scenario, Action<PlaywrightStepResult> validation)
	{
		if (scenario == null) throw new ArgumentNullException(nameof(scenario));
		if (validation == null) throw new ArgumentNullException(nameof(validation));

		// Registrado, não executado: o passo ainda não rodou enquanto a cadeia é escrita.
		scenario.AddValidation(resultado =>
		{
			if (resultado is not PlaywrightStepResult doNavegador)
				throw new InvalidOperationException(
					$"Expected a PlaywrightStepResult but got {resultado.GetType().Name}. " +
					"Type the lambda's parameter so it binds to the right package's Validate.");

			validation(doNavegador);
		});

		return BrowserScenario.De(scenario);
	}
}
