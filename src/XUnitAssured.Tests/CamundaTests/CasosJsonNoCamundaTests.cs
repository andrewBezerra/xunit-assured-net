using System.Threading.Tasks;

using Xunit;

using XUnitAssured.Camunda.TestCases;

namespace XUnitAssured.Tests.CamundaTests;

/// <summary>
/// Casos de teste JSON, no formato do Camunda Process Test, rodando contra um Camunda de verdade:
/// uma linha do <c>[Theory]</c> por caso do arquivo.
/// </summary>
[Trait("Category", "Camunda")]
[Trait("Component", "Integration")]
[Trait("Requires", "Camunda")]
public class CasosJsonNoCamundaTests
{
	[Theory(DisplayName = "Camunda JSON test case: xa-pedido")]
	[CamundaTestCases("CamundaTests/casos/xa-pedido.json")]
	public Task Pedido(CamundaTestCase caso) => caso.RunAsync("xa-pedido", "xa-envio");

	[Theory(DisplayName = "Camunda JSON test case: xa-revisao")]
	[CamundaTestCases("CamundaTests/casos/xa-revisao.json")]
	public Task Revisao(CamundaTestCase caso) => caso.RunAsync("xa-revisao");
}
