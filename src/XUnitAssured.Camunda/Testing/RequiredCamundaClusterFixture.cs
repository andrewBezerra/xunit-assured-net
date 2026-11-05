using System;

namespace XUnitAssured.Camunda.Testing;

/// <summary>
/// O cluster obrigatório: como o <see cref="CamundaClusterFixture"/>, mas o fixture falha ao ser criado
/// quando o <c>testsettings.json</c> não diz qual cluster usar.
/// </summary>
/// <remarks>
/// Para a classe de teste que só faz sentido num cluster de verdade, como a que um gerador escreve para
/// rodar o processo no Camunda do cliente. Sem o endereço, os testes da classe falham com a mensagem de
/// <see cref="CamundaSettings.LoadRequired()"/>, em vez de rodar contra o localhost.
/// </remarks>
public class RequiredCamundaClusterFixture : CamundaClusterFixture
{
	/// <summary>Lê as configurações do <c>testsettings.json</c>, e exige o endereço do cluster.</summary>
	/// <exception cref="InvalidOperationException">Não há endereço do cluster.</exception>
	public RequiredCamundaClusterFixture() : base(CamundaSettings.LoadRequired()) { }
}
