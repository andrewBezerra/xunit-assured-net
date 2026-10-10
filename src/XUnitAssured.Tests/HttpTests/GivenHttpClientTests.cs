using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

using XUnitAssured.Core.Abstractions;
using XUnitAssured.Core.DSL;
using XUnitAssured.Http.Extensions;
using XUnitAssured.Http.Testing;

namespace XUnitAssured.Tests.HttpTests;

/// <summary>
/// Mandar um cenário por um cliente específico -- um usuário com menos permissões, um cliente
/// que guarda cookies -- pedia <c>Given().WithHttpClient(client)</c>. Uma suíte real escreveu isso
/// 74 vezes e criou três atalhos locais para encurtar. <c>Given(client)</c> é o atalho no pacote.
/// </summary>
[Trait("Category", "Http")]
[Trait("Component", "Scenario")]
public class GivenHttpClientTests
{
	[Fact(DisplayName = "Given(httpClient) sends through that client")]
	public async Task Sends_Through_The_Client()
	{
		var servidor = Servidor();

		var resposta = await ScenarioDsl.Given(Cliente(servidor, "leitor"))
			.ApiResource("/pacientes")
			.Get()
			.ExecuteAsync();

		resposta.Then().AssertStatusCode(200);
		servidor.Recebidos.ShouldHaveSingleItem().Cabecalhos["X-Usuario"].ShouldBe("leitor");
	}

	[Fact(DisplayName = "Given(httpClient) is used by every HTTP step, after And() too")]
	public async Task Every_Step_Uses_The_Client()
	{
		var servidor = Servidor();

		await ScenarioDsl.Given(Cliente(servidor, "leitor"))
			.ApiResource("/pacientes").Get()
			.And()
			.ApiResource("/consultas").Get()
			.ExecuteAsync();

		servidor.Recebidos.Select(r => $"{r.Caminho} {r.Cabecalhos["X-Usuario"]}")
			.ShouldBe(["/pacientes leitor", "/consultas leitor"]);
	}

	[Fact(DisplayName = "WithHttpClient later in the chain overrides Given(httpClient) for that step")]
	public async Task WithHttpClient_Overrides()
	{
		var servidor = Servidor();

		await ScenarioDsl.Given(Cliente(servidor, "leitor"))
			.ApiResource("/pacientes")
			.WithHttpClient(Cliente(servidor, "admin"))
			.Get()
			.ExecuteAsync();

		servidor.Recebidos.ShouldHaveSingleItem().Cabecalhos["X-Usuario"].ShouldBe("admin");
	}

	[Fact(DisplayName = "The scenario does not dispose the client it was given")]
	public async Task Client_Is_Not_Disposed()
	{
		var servidor = Servidor();
		var cliente = Cliente(servidor, "leitor");

		await ScenarioDsl.Given(cliente).ApiResource("/pacientes").Get().ExecuteAsync();

		(await cliente.GetAsync("/depois")).StatusCode.ShouldBe(HttpStatusCode.OK);
		servidor.Recebidos.Count.ShouldBe(2);
	}

	[Fact(DisplayName = "Given(httpClient) rejects null")]
	public void Rejects_Null()
	{
		Should.Throw<ArgumentNullException>(() => ScenarioDsl.Given((HttpClient)null!))
			.ParamName.ShouldBe("httpClient");
	}

	[Fact(DisplayName = "HttpTestBase.Given(httpClient) sends through that client, not the fixture's")]
	public async Task Test_Base_Uses_The_Client_Not_The_Fixture()
	{
		var daFixture = Servidor();
		var doTeste = Servidor();
		var teste = new TesteComFixture(new ProvedorDeMentira(daFixture));

		await teste.Cenario(Cliente(doTeste, "leitor")).ApiResource("/pacientes").Get().ExecuteAsync();

		doTeste.Recebidos.ShouldHaveSingleItem().Cabecalhos["X-Usuario"].ShouldBe("leitor");
		daFixture.Recebidos.ShouldBeEmpty();
	}

	// ---------- Apoio ----------

	private static ServidorDeMentira Servidor() =>
		new(_ => ServidorDeMentira.Responder(HttpStatusCode.OK, "{}"));

	/// <summary>Um cliente que se identifica por cabeçalho, para o servidor dizer qual deles chegou.</summary>
	private static HttpClient Cliente(ServidorDeMentira servidor, string usuario)
	{
		var cliente = new HttpClient(servidor, disposeHandler: false) { BaseAddress = new Uri("http://servidor.teste") };
		cliente.DefaultRequestHeaders.Add("X-Usuario", usuario);
		return cliente;
	}

	private sealed class TesteComFixture(ProvedorDeMentira fixture) : HttpTestBase<ProvedorDeMentira>(fixture)
	{
		public ITestScenario Cenario(HttpClient cliente) => Given(cliente);
	}
}
