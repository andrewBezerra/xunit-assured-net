using XUnitAssured.Camunda.Extensions;

using Xunit.Abstractions;

using static XUnitAssured.Core.DSL.ScenarioDsl;

namespace XUnitAssured.Camunda.Samples.Remote.Test;

/// <summary>
/// O processo filho de outra equipe: o teste responde no lugar do <c>xa-envio</c>.
/// </summary>
/// <remarks>
/// <para>
/// O mock é pelo id da call activity no pai (<c>chamar-envio</c>), e não pelo id do filho. O filho
/// nasce de verdade e é interceptado: o pacote encerra o que ele fazia (o <c>despachar</c>) e o
/// termina pelo evento de fim, com as variáveis do teste. O mapeamento de saída da call activity roda.
/// </para>
/// <para>
/// No Operate, a instância filha aparece completada, com <c>despachar</c> encerrado (TERMINATED).
/// </para>
/// </remarks>
[Trait("Requires", "Camunda")]
public class ProcessoFilhoTests(ITestOutputHelper saida)
{
	[Fact(DisplayName = "13. The shipping child process is mocked: its tracking code reaches the order through the output mapping")]
	public async Task Envio_Mockado()
	{
		await Given()
			.Deploy("xa-pedido", "xa-envio")
			.MockJobWorker("xa-cobrar").ThenComplete(new { pago = true })
			// Ninguém mocka xa-despachar: quem responde é o filho inteiro
			.MockChildProcess("chamar-envio").ThenComplete(new { rastreio = "BR-FILHO" })
			.StartInstance("xa-pedido", new { pedidoId = 13 })
			.ExecuteAsync()
			.Mostrar(saida, "a filha xa-envio completada, com despachar encerrado; codigoRastreio = BR-FILHO no pai")
			.AssertInstanceCompleted()
			// O mapeamento de saída da call activity copiou rastreio para codigoRastreio
			.AssertVariable("codigoRastreio", "BR-FILHO")
			.AssertChildProcessMocked("chamar-envio");
	}

	[Fact(DisplayName = "14. The shipping fails with a BPMN error: the call activity's boundary takes the order to 'not shipped'")]
	public async Task Envio_Falhou()
	{
		await Given()
			.Deploy("xa-pedido", "xa-envio")
			.MockJobWorker("xa-cobrar").ThenComplete(new { pago = true })
			// O xa-envio não tem evento de fim de erro: o pacote encerra a call activity e segue pela borda
			.MockChildProcess("chamar-envio").ThenThrowBpmnError("ENVIO_FALHOU", new { motivo = "transportadora fora do ar" })
			.StartInstance("xa-pedido", new { pedidoId = 14 })
			.ExecuteAsync()
			.Mostrar(saida, "chamar-envio encerrada; a borda envio-falhou leva a nao-enviado")
			.AssertInstanceCompleted()
			.AssertCompletedElements("nao-enviado")
			.AssertVariable("motivo", "transportadora fora do ar")
			.AssertChildProcessMocked("chamar-envio");
	}
}
