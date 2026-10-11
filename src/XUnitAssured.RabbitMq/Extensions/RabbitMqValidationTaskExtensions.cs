using XUnitAssured.Geracao;

namespace XUnitAssured.RabbitMq.Extensions;

/// <summary>
/// The assertions of <see cref="RabbitMqValidationBuilder"/> on the task <c>ExecuteAsync()</c> returns, so they
/// chain before the <c>await</c>, without <c>(await ...ExecuteAsync()).Then()</c>.
/// </summary>
/// <remarks>
/// <para>
/// Every public method of the builder has one here, with the same name, generated from the
/// builder when the package compiles: a method added to the builder gets its counterpart in the
/// same build. The ones with an <c>out</c> parameter are left out, since an asynchronous method
/// cannot have one; use them on the builder, after the <c>await</c>.
/// </para>
/// <para>
/// Await the chain. Without <c>await</c> the step still runs, but a failed assertion is never
/// seen.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// await Given().Queue("orders").Consume().ExecuteAsync()
///     .AssertSuccess()
///     .AssertMessage("hello");
/// </code>
/// </example>
[ExtensoesDeTask(typeof(RabbitMqValidationBuilder))]
public static partial class RabbitMqValidationTaskExtensions
{
}
