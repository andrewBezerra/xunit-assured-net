using XUnitAssured.Camunda.Extensions;
using XUnitAssured.Http.Extensions;

using Xunit.Abstractions;

using static XUnitAssured.Core.DSL.ScenarioDsl;

namespace XUnitAssured.Camunda.Samples.Remote.Test;

/// <summary>
/// A API inicia o processo, e o teste acompanha a instância que ela abriu.
/// </summary>
/// <remarks>
/// <para>
/// Numa empresa, a API testada recebe o pedido e chama o Camunda. Aqui não há essa API: quem faz o
/// papel dela é um POST direto na REST do Camunda, pelo <c>XUnitAssured.Http</c>. O que importa é que
/// a instância nasce fora da cadeia do Camunda, e o <c>FollowInstance</c> a acha: pela chave que a API
/// devolveu, ou pelo dado que o teste mandou.
/// </para>
/// <para>
/// O POST usa a autenticação Basic das variáveis <c>CAMUNDA_*</c>; com OAuth, estes exemplos não rodam.
/// </para>
/// </remarks>
[Trait("Requires", "Camunda")]
public class ApiQueIniciaTests(ITestOutputHelper saida)
{
	private static readonly string Endereco =
		(Environment.GetEnvironmentVariable("CAMUNDA_REST_ADDRESS") ?? "http://localhost:8080").TrimEnd('/');

	[Fact(DisplayName = "15. The API starts the order and returns its key: the test follows that instance")]
	public async Task Pela_Chave()
	{
		var chave = await (await IniciarPelaApiAsync(new { pedidoId = 15 })).ExtractAsync<string>("$.processInstanceKey");

		await Given()
			.MockJobWorker("xa-cobrar").ThenComplete(new { pago = true })
			.MockChildProcess("chamar-envio").ThenComplete(new { rastreio = "BR-API" })
			// A instância é a que a API abriu, e não uma do StartInstance
			.FollowInstance(chave)
			.ExecuteAsync()
			.Mostrar(saida, "a instância que o POST abriu, completada pelos mocks do teste; codigoRastreio = BR-API")
			.AssertInstanceCompleted()
			.AssertVariable("codigoRastreio", "BR-API");
	}

	[Fact(DisplayName = "16. The API does not return the key: the test finds the instance by the order id it sent")]
	public async Task Pela_Variavel()
	{
		// Um valor só deste teste: com dois iguais, o FollowInstance segue a instância mais nova e avisa
		var pedidoId = $"P-{Guid.NewGuid():N}";
		await (await IniciarPelaApiAsync(new { pedidoId })).ExecuteAsync();

		await Given()
			.MockJobWorker("xa-cobrar").ThenThrowBpmnError("PAGAMENTO_RECUSADO")
			.FollowInstance("xa-pedido", ("pedidoId", pedidoId))
			.ExecuteAsync()
			.Mostrar(saida, "a instância achada pelo pedidoId, terminando em recusado")
			.AssertInstanceCompleted()
			.AssertCompletedElements("recusado");
	}

	// O papel da API: implanta o pedido (como o pipeline faria) e inicia a instância por HTTP.
	private static async Task<Http.Abstractions.IHttpScenario> IniciarPelaApiAsync(object variaveis)
	{
		var definicao = (await Given().Deploy("xa-pedido", "xa-envio").ExecuteAsync())
			.GetResult().DeployedProcesses.Single(p => p.ProcessId == "xa-pedido").DefinitionKey;

		return Given()
			.ApiResource($"{Endereco}/v2/process-instances")
			.WithBasicAuth(
				Environment.GetEnvironmentVariable("CAMUNDA_BASIC_AUTH_USERNAME") ?? "",
				Environment.GetEnvironmentVariable("CAMUNDA_BASIC_AUTH_PASSWORD") ?? "")
			.Post(new { processDefinitionKey = definicao, variables = variaveis });
	}
}
