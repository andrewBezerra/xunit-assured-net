using System;

using XUnitAssured.Core.Abstractions;
using XUnitAssured.Core.DSL;

namespace XUnitAssured.Camunda.Testing;

/// <summary>
/// Base de uma classe de teste cujas cadeias usam o cluster do fixture.
/// </summary>
/// <typeparam name="TFixture">O fixture: <see cref="CamundaClusterFixture"/> ou um derivado.</typeparam>
/// <example>
/// <code>
/// public class OrderTests(CamundaContainerFixture camunda)
///     : CamundaTestBase&lt;CamundaContainerFixture&gt;(camunda), IClassFixture&lt;CamundaContainerFixture&gt;
/// {
///     [Fact]
///     public async Task Order_Completes() =>
///         await Given()
///             .Deploy("order")
///             .MockJobWorker("charge-card").ThenComplete()
///             .StartInstance("order")
///             .ExecuteAsync()
///             .AssertInstanceCompleted();
/// }
/// </code>
/// </example>
public abstract class CamundaTestBase<TFixture> where TFixture : CamundaClusterFixture
{
	/// <summary>O fixture que o xUnit injetou na classe de teste.</summary>
	protected readonly TFixture Fixture;

	/// <summary>Recebe o fixture que o xUnit injeta.</summary>
	/// <param name="fixture">O fixture da classe, por <c>IClassFixture&lt;TFixture&gt;</c> ou uma coleção.</param>
	protected CamundaTestBase(TFixture fixture) =>
		Fixture = fixture ?? throw new ArgumentNullException(nameof(fixture));

	/// <summary>Começa uma cadeia que usa o cluster do fixture.</summary>
	protected ITestScenario Given()
	{
		var scenario = ScenarioDsl.Given();
		Fixture.Seed(scenario.Context);
		return scenario;
	}
}
