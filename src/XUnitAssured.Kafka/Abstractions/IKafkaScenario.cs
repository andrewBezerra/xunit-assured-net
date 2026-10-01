using System.Threading.Tasks;
using System.Threading;
using XUnitAssured.Core.Abstractions;
using XUnitAssured.Kafka.Extensions;

namespace XUnitAssured.Kafka.Abstractions;

/// <summary>
/// A scenario that is currently describing a Kafka produce or consume step.
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
public interface IKafkaScenario : ITestScenario
{
	/// <inheritdoc cref="ITestScenario.And" />
	new IKafkaScenario And();

	/// <inheritdoc cref="ITestScenario.On" />
	new IKafkaScenario On();

	/// <inheritdoc cref="ITestScenario.When" />
	new IKafkaScenario When();

	/// <inheritdoc cref="ITestScenario.Then" />
	new IKafkaScenario Then();

	/// <summary>
	/// Runs the chain and hands back the assertions.
	/// </summary>
	Task<KafkaValidationBuilder> ExecuteAsync(CancellationToken cancellationToken = default);

	/// <summary>
	/// Runs the chain and waits for it.
	/// </summary>
	/// <remarks>
	/// A thin wrapper over <see cref="ExecuteAsync"/>, kept so a suite written against the
	/// synchronous DSL keeps working. It blocks the calling thread, which in a test runner
	/// is the thread that would be waiting anyway.
	/// </remarks>
	KafkaValidationBuilder Execute();
}
