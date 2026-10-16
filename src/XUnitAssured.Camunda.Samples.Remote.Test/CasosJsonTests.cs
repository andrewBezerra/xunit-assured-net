using XUnitAssured.Camunda.TestCases;

namespace XUnitAssured.Camunda.Samples.Remote.Test;

/// <summary>
/// Casos de teste escritos em JSON, no formato do Camunda Process Test e do Test Studio do Web
/// Modeler, rodando como estão: uma linha do <c>[Theory]</c> por caso.
/// </summary>
/// <remarks>
/// Os arquivos são os dos testes do pacote, ligados de <c>src/XUnitAssured.Tests/CamundaTests/casos/</c>.
/// Abra-os ao lado do Operate: cada instrução do JSON vira um passo da instância.
/// </remarks>
[Trait("Requires", "Camunda")]
public class CasosJsonTests
{
	[Theory(DisplayName = "17. JSON test cases of the order")]
	[CamundaTestCases("casos/xa-pedido.json")]
	public Task Pedido(CamundaTestCase caso) => caso.RunAsync("xa-pedido", "xa-envio");

	[Theory(DisplayName = "18. JSON test case of the review: a user task checked before it is completed, then a message")]
	[CamundaTestCases("casos/xa-revisao.json")]
	public Task Revisao(CamundaTestCase caso) => caso.RunAsync("xa-revisao");
}
