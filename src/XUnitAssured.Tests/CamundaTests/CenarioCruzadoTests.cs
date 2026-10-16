using System;
using System.Linq;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

using XUnitAssured.Camunda.Extensions;
using XUnitAssured.Core.DSL;
using XUnitAssured.Http.Extensions;

namespace XUnitAssured.Tests.CamundaTests;

/// <summary>
/// HTTP e Camunda no mesmo teste, contra um Camunda de verdade.
///
/// <para>
/// O caso de quem integra uma API com o Camunda: a API recebe o pedido e inicia o processo, e o teste
/// acompanha essa instância (<c>FollowInstance</c>). Aqui, quem faz o papel da API é um POST direto na
/// REST do próprio Camunda, pelo <c>XUnitAssured.Http</c>: é ele que inicia a instância, e não a cadeia
/// do Camunda. E a composição no outro sentido: o teste chama a API, extrai o que ela devolveu e
/// inicia o processo com isso.
/// </para>
/// </summary>
[Trait("Category", "Camunda")]
[Trait("Component", "Integration")]
[Trait("Requires", "Camunda")]
public class CenarioCruzadoTests
{
	private static readonly string Endereco =
		(Environment.GetEnvironmentVariable("CAMUNDA_REST_ADDRESS") ?? "http://localhost:8080").TrimEnd('/');
	private static readonly string Usuario = Environment.GetEnvironmentVariable("CAMUNDA_BASIC_AUTH_USERNAME") ?? "demo";
	private static readonly string Senha = Environment.GetEnvironmentVariable("CAMUNDA_BASIC_AUTH_PASSWORD") ?? "demo";

	[Fact(DisplayName = "Camunda + HTTP: the instance an HTTP call started is followed by its key, with the mocks working on it")]
	public async Task Follow_The_Instance_The_Api_Started()
	{
		var definicao = await Implantar();

		// "A API": inicia o processo e devolve a chave da instância.
		var chave = await ScenarioDsl.Given()
			.ApiResource($"{Endereco}/v2/process-instances")
			.WithBasicAuth(Usuario, Senha)
			.Post(new { processDefinitionKey = definicao, variables = new { pedidoId = 21 } })
			.ExtractAsync<string>("$.processInstanceKey");

		var execucao = await ScenarioDsl.Given()
			.MockJobWorker("xa-cobrar").ThenComplete(new { pago = true })
			.MockChildProcess("chamar-envio").ThenComplete(new { rastreio = "BR-API" })
			.FollowInstance(chave)
			.ExecuteAsync()
			.AssertInstanceCompleted()
			.AssertVariable("codigoRastreio", "BR-API")
			.AssertVariable("pedidoId", 21);

		execucao.GetResult().ProcessId.ShouldBe("xa-pedido");
	}

	[Fact(DisplayName = "Camunda + HTTP: the instance an HTTP call started is found by the order id the test sent")]
	public async Task Follow_The_Instance_By_A_Variable()
	{
		var definicao = await Implantar();
		var pedidoId = $"P-{Guid.NewGuid():N}";

		// "A API" não devolve a chave: o teste a acha pelo dado que mandou.
		await ScenarioDsl.Given()
			.ApiResource($"{Endereco}/v2/process-instances")
			.WithBasicAuth(Usuario, Senha)
			.Post(new { processDefinitionKey = definicao, variables = new { pedidoId } })
			.ExecuteAsync();

		await ScenarioDsl.Given()
			.MockJobWorker("xa-cobrar").ThenThrowBpmnError("PAGAMENTO_RECUSADO")
			.FollowInstance("xa-pedido", ("pedidoId", pedidoId))
			.ExecuteAsync()
			.AssertInstanceCompleted()
			.AssertCompletedElements("recusado");
	}

#if NET10_0_OR_GREATER
	[Fact(DisplayName = "Camunda + HTTP: the product the API created starts the process, with the id the API returned")]
	public async Task Start_With_What_The_Api_Returned()
	{
		using var api = new ApiDosProdutos();

		var produto = await ScenarioDsl.Given(api)
			.ApiResource("/api/products")
			.Post(new { name = "Cadeira", price = 99.9 })
			.ExtractAsync<int>("$.id");

		await ScenarioDsl.Given()
			.Deploy("xa-pedido", "xa-envio")
			.MockJobWorker("xa-cobrar").ThenComplete(new { pago = true })
			.MockChildProcess("chamar-envio").ThenComplete(new { rastreio = $"BR-{produto}" })
			.StartInstance("xa-pedido", new { pedidoId = produto })
			.ExecuteAsync()
			.AssertInstanceCompleted()
			.AssertVariable("pedidoId", produto)
			.AssertVariable("codigoRastreio", $"BR-{produto}");
	}

	private sealed class ApiDosProdutos : XUnitAssured.Http.AspNetCore.ApiFixture<Program>
	{
		protected override void ConfigureApi(Microsoft.AspNetCore.Hosting.IWebHostBuilder api) =>
			Microsoft.AspNetCore.Hosting.HostingAbstractionsWebHostBuilderExtensions.UseEnvironment(api, "Testing");
	}
#endif

	private static async Task<string> Implantar() =>
		(await ScenarioDsl.Given().Deploy("xa-pedido", "xa-envio").ExecuteAsync())
			.GetResult().DeployedProcesses.Single(p => p.ProcessId == "xa-pedido").DefinitionKey;
}
