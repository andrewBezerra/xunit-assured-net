using XUnitAssured.Core.Abstractions;
using XUnitAssured.Playwright.Extensions;

namespace XUnitAssured.Playwright.Abstractions;

/// <summary>
/// A scenario that is currently describing a browser step.
///
/// <para>
/// It exists so that <c>Execute()</c> can mean one thing. Each package used to define
/// <c>Execute(this ITestScenario)</c> returning its own builder, which is fine on its own and
/// ambiguous the moment a test project references two of them — and referencing two of them is
/// the point of this framework. A scenario that crosses an API, a topic and a screen had to
/// end with the extension class spelled out, which reads like an apology.
/// </para>
///
/// <para>
/// With the step carrying its own type, the compiler picks the right builder from the receiver
/// and the chain ends the way it reads: <c>.Execute()</c>.
/// </para>
///
/// <para>
/// The chaining methods are redeclared because an instance method wins over an extension
/// method. Without them, <c>When()</c> would resolve to the one on <see cref="ITestScenario"/>,
/// hand back the untyped scenario, and lose exactly what this interface exists to carry.
/// </para>
/// </summary>
public interface IBrowserScenario : ITestScenario
{
	/// <inheritdoc cref="ITestScenario.And" />
	new IBrowserScenario And();

	/// <inheritdoc cref="ITestScenario.On" />
	new IBrowserScenario On();

	/// <inheritdoc cref="ITestScenario.When" />
	new IBrowserScenario When();

	/// <inheritdoc cref="ITestScenario.Then" />
	new IBrowserScenario Then();

	/// <summary>
	/// Runs the step and hands back the browser assertions.
	/// </summary>
	PlaywrightValidationBuilder Execute();
}
