using Shouldly;

using XUnitAssured.Camunda.Extensions;
using static XUnitAssured.Core.DSL.ScenarioDsl;

using Xunit.Abstractions;

namespace XUnitAssured.Camunda.Samples.Remote.Test;

/// <summary>
/// O desenvolvedor mudou o diagrama e quer testá-lo antes de publicar.
/// </summary>
/// <remarks>
/// <para>
/// Cada teste implanta os BPMN do repositório (<c>bpmn/xa-pedido.bpmn</c> e <c>bpmn/xa-envio.bpmn</c>),
/// inicia uma instância e responde no lugar dos workers. O pedido cobra o cartão (<c>xa-cobrar</c>) e
/// chama o processo de envio, que despacha na transportadora (<c>xa-despachar</c>); se a cobrança
/// lança o erro <c>PAGAMENTO_RECUSADO</c>, a borda de erro leva a <c>recusado</c>.
/// </para>
/// <para>
/// Abra os dois diagramas no Camunda Modeler para acompanhar os ids dos elementos que as asserções
/// citam, e a instância no Operate pelo link que cada teste escreve na saída.
/// </para>
/// </remarks>
[Trait("Requires", "Camunda")]
public class DesenvolvedorTests(ITestOutputHelper saida)
{
	[Fact(DisplayName = "1. The order runs end to end, deployed by process id, with payment and dispatch mocked")]
	public async Task Pedido_Completo()
	{
		await Given()
			// Pelo id do processo: o pacote acha o .bpmn que o declara na pasta de saída
			.Deploy("xa-pedido", "xa-envio")
			// O worker da cobrança e o da transportadora não rodam: o teste responde por eles
			.MockJobWorker("xa-cobrar").ThenComplete(new { pago = true })
			.MockJobWorker("xa-despachar").ThenComplete(new { rastreio = "BR-1" })
			.StartInstance("xa-pedido", new { pedidoId = 1 })
			.ExecuteAsync()
			.Mostrar(saida, "cobrar → chamar-envio → concluido; a filha xa-envio completa; codigoRastreio = BR-1")
			.AssertInstanceCompleted()
			.AssertCompletedElements("cobrar", "chamar-envio", "concluido")
			// O mapeamento de saída da call activity copia rastreio da filha para codigoRastreio
			.AssertVariable("codigoRastreio", "BR-1")
			.AssertJobMocked("xa-cobrar")
			.AssertJobMocked("xa-despachar")
			.AssertNoIncidents();
	}

	[Fact(DisplayName = "2. A refused payment takes the error boundary, and the dispatch is never called")]
	public async Task Pagamento_Recusado()
	{
		await Given()
			// Pelo caminho também funciona
			.Deploy("bpmn/xa-pedido.bpmn", "bpmn/xa-envio.bpmn")
			.MockJobWorker("xa-cobrar").ThenThrowBpmnError("PAGAMENTO_RECUSADO", "sem saldo")
			.MockJobWorker("xa-despachar").ThenComplete(new { rastreio = "nunca" })
			.StartInstance("xa-pedido", new { pedidoId = 2 })
			.ExecuteAsync()
			.Mostrar(saida, "cobrar sai pela borda pagamento-recusado e termina em recusado; nenhuma filha")
			.AssertInstanceCompleted()
			.AssertCompletedElements("pagamento-recusado", "recusado")
			// O mock existe, mas nunca foi chamado: o fluxo não chegou ao envio
			.AssertJobMocked("xa-despachar", 0);
	}

	[Fact(DisplayName = "3. Three instances in parallel, mocking the same job types, each get their own answer")]
	public async Task Instancias_Em_Paralelo()
	{
		// Num cluster compartilhado, os mocks de cada teste só enxergam a própria instância
		var execucoes = Enumerable.Range(1, 3).Select(async n =>
		{
			var execucao = await Given()
				.Deploy("xa-pedido", "xa-envio")
				.MockJobWorker("xa-cobrar").ThenComplete(new { pago = true })
				.MockJobWorker("xa-despachar").ThenComplete(new { rastreio = $"BR-P{n}" })
				.StartInstance("xa-pedido", new { pedidoId = 300 + n })
				.ExecuteAsync();

			execucao
				.Mostrar(saida, $"pedidoId = {300 + n} com codigoRastreio = BR-P{n}, e não o de outra instância")
				.AssertInstanceCompleted()
				.AssertVariable("codigoRastreio", $"BR-P{n}")
				.AssertJobMocked("xa-despachar", 1);
		});

		await Task.WhenAll(execucoes);
	}

	[Fact(DisplayName = "4. A job nobody mocked: the failure says which job the instance waits for")]
	public async Task Job_Sem_Mock()
	{
		var execucao = await Given()
			.Deploy("xa-pedido", "xa-envio")
			// Só a cobrança é mockada: o envio fica esperando um worker de xa-despachar
			.MockJobWorker("xa-cobrar").ThenComplete(new { pago = true })
			.StartInstance("xa-pedido", new { pedidoId = 4 })
			.ExecuteAsync();

		execucao.Mostrar(saida, "a filha xa-envio parada em despachar; ao fim da rodada, as duas canceladas (TERMINATED)");

		// A asserção espera assertionTimeoutSeconds (15 s) antes de desistir: a instância ainda podia andar
		var falha = Should.Throw<ShouldAssertException>(() => execucao.AssertInstanceCompleted());

		// A mensagem que o desenvolvedor veria se esquecesse o mock
		saida.WriteLine("");
		saida.WriteLine(falha.Message);

		falha.Message.ShouldContain("Jobs waiting for a worker (not mocked in this test): 'xa-despachar' at 'despachar'");
	}
}
