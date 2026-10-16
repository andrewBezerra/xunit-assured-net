using System;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

using XUnitAssured.Camunda;
using XUnitAssured.Camunda.Extensions;
using XUnitAssured.Camunda.Testcontainers;
using XUnitAssured.Camunda.Testing;
using XUnitAssured.Core.DSL;

namespace XUnitAssured.Tests.CamundaTests;

/// <summary>
/// O modo gerenciado: o próprio <c>dotnet test</c> sobe o Camunda no Docker, sem nenhuma variável
/// <c>CAMUNDA_*</c> nem serviço do CI. É o que uma empresa roda num runner que só tem Docker.
/// </summary>
[Trait("Category", "Camunda")]
[Trait("Component", "Integration")]
[Trait("Requires", "Docker")]
public class CamundaNoContainerTests(CamundaContainerFixture camunda)
	: CamundaTestBase<CamundaContainerFixture>(camunda), IClassFixture<CamundaContainerFixture>
{
	[Fact(DisplayName = "Testcontainers: the order runs end to end on a Camunda that dotnet test started")]
	public async Task Order_Runs_On_The_Container()
	{
		Fixture.RestAddress.ShouldStartWith("http://");

		await Given()
			.Deploy("xa-pedido", "xa-envio")
			.MockJobWorker("xa-cobrar").ThenComplete(new { pago = true })
			.MockChildProcess("chamar-envio").ThenComplete(new { rastreio = "BR-TC" })
			.StartInstance("xa-pedido", new { pedidoId = 1 })
			.ExecuteAsync()
			.AssertInstanceCompleted()
			.AssertVariable("codigoRastreio", "BR-TC");
	}

	[Fact(DisplayName = "Testcontainers: user task, timers, message and signal on the same container")]
	public async Task Everything_Else_On_The_Container()
	{
		await Given()
			.Deploy("xa-entrega")
			.MockMessage("xa-pagamento-confirmado").ThenCorrelate(new { valor = 1 })
			.MockSignal("xa-estoque-liberado").ThenTrigger()
			.MockUserTask("conferir").ThenComplete()
			.StartInstance("xa-entrega", new { pedidoId = $"P-{Guid.NewGuid():N}" })
			.ExecuteAsync()
			.AssertInstanceCompleted()
			.AssertCompletedElements("entregue");
	}
}

/// <summary>
/// O fixture de cluster, sem cluster: o que ele entrega às cadeias.
/// </summary>
[Trait("Category", "Camunda")]
[Trait("Component", "Configuration")]
public class CamundaClusterFixtureTests
{
	[Fact(DisplayName = "The cluster fixture hands its settings to every chain the test base starts")]
	public void Fixture_Settings_Reach_The_Chain()
	{
		var proprias = new CamundaSettings { RestAddress = "http://camunda.da.empresa:8080", AssertionTimeoutSeconds = 7 };
		var teste = new UmTeste(new CamundaClusterFixture(proprias));

		var cenario = teste.Cadeia();

		CamundaSettings.De(cenario.Context).ShouldBeSameAs(proprias);
	}

	[Fact(DisplayName = "Without settings, the cluster fixture reads testsettings.json, as a chain with no fixture would")]
	public void Default_Reads_The_File() =>
		new CamundaClusterFixture().Settings.AssertionTimeoutSeconds.ShouldBe(CamundaSettings.Load().AssertionTimeoutSeconds);

	[Fact(DisplayName = "A chain started outside the test base keeps reading the file")]
	public void Plain_Chain_Is_Untouched() =>
		CamundaSettings.De(ScenarioDsl.Given().Context).RestAddress.ShouldBe(CamundaSettings.Load().RestAddress);

	private sealed class UmTeste(CamundaClusterFixture fixture) : CamundaTestBase<CamundaClusterFixture>(fixture)
	{
		public Core.Abstractions.ITestScenario Cadeia() => Given();
	}
}
