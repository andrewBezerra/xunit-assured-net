using Shouldly;

using XUnitAssured.Camunda.Extensions;
using XUnitAssured.Core.DSL;

using Xunit.Abstractions;

namespace XUnitAssured.Camunda.Samples.Remote.Test;

/// <summary>
/// O QA testa o que já está publicado no cluster, sem implantar nada.
/// </summary>
/// <remarks>
/// <para>
/// Numa cadeia sem <c>Deploy</c>, o <c>StartInstance</c> usa a versão que está no Camunda: a última,
/// ou a pedida por <c>version:</c> (o número que o cluster deu) ou <c>versionTag:</c> (o
/// <c>zeebe:versionTag</c> do Modeler; o <c>xa-pedido</c> tem <c>1.0.0</c>).
/// </para>
/// <para>
/// Na homologação os workers de verdade estão rodando: lá, teste ponta a ponta sem mock, ou mocke só
/// os tipos que não têm worker. Os exemplos supõem um cluster sem worker de <c>xa-cobrar</c> nem de
/// <c>xa-despachar</c>, então os dois tipos são mockados.
/// </para>
/// </remarks>
[Trait("Requires", "Camunda")]
public class QaTests(ITestOutputHelper saida) : IClassFixture<QaTests.Publicacao>
{
	[Fact(DisplayName = "5. With no Deploy, the version published with tag 1.0.0 runs, and the result says which")]
	public async Task Versao_Pelo_Rotulo()
	{
		var execucao = await AteAparecerNaBusca(() => ScenarioDsl.Given()
			.MockJobWorker("xa-cobrar").ThenComplete(new { pago = true })
			.MockJobWorker("xa-despachar").ThenComplete(new { rastreio = "BR-QA" })
			.StartInstance("xa-pedido", new { pedidoId = 5 }, versionTag: "1.0.0")
			.ExecuteAsync());

		execucao
			.Mostrar(saida, "a versão e a tag no cabeçalho do Operate; codigoRastreio = BR-QA")
			.AssertInstanceCompleted()
			.AssertVariable("codigoRastreio", "BR-QA");

		execucao.GetResult().VersionTag.ShouldBe("1.0.0");
	}

	[Fact(DisplayName = "6. With no Deploy and no version, the latest version on the cluster runs")]
	public async Task Ultima_Versao()
	{
		var execucao = await AteAparecerNaBusca(() => ScenarioDsl.Given()
			.MockJobWorker("xa-cobrar").ThenThrowBpmnError("PAGAMENTO_RECUSADO")
			.StartInstance("xa-pedido", new { pedidoId = 6 })
			.ExecuteAsync());

		execucao
			.Mostrar(saida, "a última versão do xa-pedido, terminando em recusado")
			.AssertInstanceCompleted()
			.AssertCompletedElements("recusado");

		// "A última" muda quando alguém publica: o resultado diz qual rodou
		saida.WriteLine($"  Rodou a versão {execucao.GetResult().Version}");
	}

	/// <summary>
	/// A publicação, como o pipeline faria antes dos testes do QA. Em homologação de verdade ela já
	/// aconteceu, e esta classe não precisaria implantar nada.
	/// </summary>
	public sealed class Publicacao
	{
		public Publicacao() =>
			ScenarioDsl.Given().Deploy("xa-pedido", "xa-envio").ExecuteAsync().GetAwaiter().GetResult().AssertSuccess();
	}

	// A busca de versões fica um pouco atrás de um deploy recém-feito. Na homologação a versão foi
	// publicada bem antes, e isto não seria preciso.
	private static async Task<CamundaValidationBuilder> AteAparecerNaBusca(Func<Task<CamundaValidationBuilder>> cadeia)
	{
		var prazo = DateTime.UtcNow.AddSeconds(15);
		while (true)
		{
			var execucao = await cadeia();
			if (execucao.GetResult().Success || DateTime.UtcNow > prazo)
				return execucao;

			await Task.Delay(500);
		}
	}
}
