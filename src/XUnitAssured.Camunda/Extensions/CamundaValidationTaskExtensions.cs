using XUnitAssured.Geracao;

namespace XUnitAssured.Camunda.Extensions;

/// <summary>
/// The assertions of <see cref="CamundaValidationBuilder"/> on the task <c>ExecuteAsync()</c> returns, so they
/// chain before the <c>await</c>, without <c>(await ...ExecuteAsync()).Then()</c>.
/// </summary>
/// <remarks>
/// <para>
/// Every public method of the builder has one here, with the same name, generated from the
/// builder when the package compiles. The ones with an <c>out</c> parameter are left out, since an
/// asynchronous method cannot have one; use them on the builder, after the <c>await</c>.
/// </para>
/// <para>
/// Await the chain. Without <c>await</c> the steps still run, but a failed assertion is never seen.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// await Given()
///     .Deploy("order.bpmn")
///     .MockJobWorker("charge-card").ThenComplete(new { approved = true })
///     .StartInstance("order")
///     .ExecuteAsync()
///     .AssertInstanceCompleted();
/// </code>
/// </example>
[ExtensoesDeTask(typeof(CamundaValidationBuilder))]
public static partial class CamundaValidationTaskExtensions
{
}
