using XUnitAssured.Camunda.Extensions;

using Xunit.Abstractions;

using static XUnitAssured.Core.DSL.ScenarioDsl;

namespace XUnitAssured.Camunda.Samples.Remote.Test;

/// <summary>
/// O que outros sistemas mandam: a confirmação do pagamento (mensagem), o estoque liberado (sinal) e
/// o cliente que cancela (mensagem de borda).
/// </summary>
/// <remarks>
/// <para>
/// O <c>xa-entrega</c> espera a mensagem <c>xa-pagamento-confirmado</c> com a chave <c>=pedidoId</c>,
/// depois o sinal <c>xa-estoque-liberado</c>, e então a conferência do pacote (<c>conferir</c>), que a
/// mensagem <c>xa-cliente-cancelou</c> interrompe pela borda.
/// </para>
/// <para>
/// A mensagem é correlacionada de verdade; por isso cada teste usa um <c>pedidoId</c> só dele. O sinal
/// não é broadcast: o pacote o dispara só na instância do teste.
/// </para>
/// </remarks>
[Trait("Requires", "Camunda")]
public class MensagemESinalTests(ITestOutputHelper saida)
{
	[Fact(DisplayName = "9. The payment message and the stock signal arrive, the package is checked, the order is delivered")]
	public async Task Entrega_Completa()
	{
		await Given()
			.Deploy("xa-entrega")
			// Correlacionada com a chave que a instância assinou: o pedidoId desta instância
			.MockMessage("xa-pagamento-confirmado").ThenCorrelate(new { valor = 99 })
			// Só nesta instância: nenhuma outra do cluster que espere o sinal é atingida
			.MockSignal("xa-estoque-liberado").ThenTrigger(new { lote = "L-1" })
			.MockUserTask("conferir").ThenComplete()
			.StartInstance("xa-entrega", new { pedidoId = $"P-{Guid.NewGuid():N}" })
			.ExecuteAsync()
			.Mostrar(saida, "aguardar-pagamento completado; aguardar-estoque encerrado (sinal só na instância); fim em entregue")
			.AssertInstanceCompleted()
			.AssertCompletedElements("aguardar-pagamento", "conferir", "entregue")
			.AssertVariable("valor", 99)
			.AssertVariable("lote", "L-1")
			.AssertMessageCorrelated("xa-pagamento-confirmado")
			.AssertSignalTriggered("xa-estoque-liberado");
	}

	[Fact(DisplayName = "10. The customer cancels while the package is being checked: the boundary message ends the user task")]
	public async Task Cliente_Cancelou()
	{
		await Given()
			.Deploy("xa-entrega")
			.MockMessage("xa-pagamento-confirmado").ThenCorrelate()
			.MockSignal("xa-estoque-liberado").ThenTrigger()
			// Ninguém confere o pacote: a mensagem de borda chega enquanto a user task espera
			.MockMessage("xa-cliente-cancelou").ThenCorrelate(new { motivo = "desistiu" })
			.StartInstance("xa-entrega", new { pedidoId = $"P-{Guid.NewGuid():N}" })
			.ExecuteAsync()
			.Mostrar(saida, "conferir encerrada pela borda cliente-cancelou; fim em cancelado")
			.AssertInstanceCompleted()
			.AssertCompletedElements("cliente-cancelou", "cancelado")
			.AssertVariable("motivo", "desistiu")
			.AssertMessageCorrelated("xa-cliente-cancelou");
	}
}
