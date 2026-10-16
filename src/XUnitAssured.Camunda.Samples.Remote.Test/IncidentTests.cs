using XUnitAssured.Camunda.Extensions;

using Xunit.Abstractions;

using static XUnitAssured.Core.DSL.ScenarioDsl;

namespace XUnitAssured.Camunda.Samples.Remote.Test;

/// <summary>
/// O caminho técnico: o worker que desiste e o incident que o Camunda abre.
/// </summary>
/// <remarks>
/// No Operate, a instância aparece com o ícone vermelho de incident em <c>cobrar</c>, e o painel de
/// incidents mostra o tipo e a mensagem. Ao fim da rodada a instância é cancelada, como toda
/// instância que ficou ativa.
/// </remarks>
[Trait("Requires", "Camunda")]
public class IncidentTests(ITestOutputHelper saida)
{
	[Fact(DisplayName = "11. The payment worker gives up: Camunda opens a JOB_NO_RETRIES incident at 'cobrar'")]
	public async Task Worker_Desistiu()
	{
		await Given()
			.Deploy("xa-pedido", "xa-envio")
			// Como um worker que esgotou as tentativas
			.MockJobWorker("xa-cobrar").ThenFail("gateway de pagamento fora do ar")
			.StartInstance("xa-pedido", new { pedidoId = 11 })
			.ExecuteAsync()
			.Mostrar(saida, "incident JOB_NO_RETRIES em cobrar, com a mensagem do mock")
			.AssertIncident("cobrar", "JOB_NO_RETRIES", "fora do ar")
			.AssertInstanceActive();
	}

	[Fact(DisplayName = "12. A BPMN error nobody catches: Camunda opens an UNHANDLED_ERROR_EVENT incident")]
	public async Task Erro_Sem_Borda()
	{
		await Given()
			.Deploy("xa-pedido", "xa-envio")
			// A borda de cobrar só pega PAGAMENTO_RECUSADO
			.MockJobWorker("xa-cobrar").ThenThrowBpmnError("CARTAO_CLONADO")
			.StartInstance("xa-pedido", new { pedidoId = 12 })
			.ExecuteAsync()
			.Mostrar(saida, "incident UNHANDLED_ERROR_EVENT em cobrar: o código não é o que a borda pega")
			.AssertIncident("cobrar", "UNHANDLED_ERROR_EVENT", "CARTAO_CLONADO");
	}
}
