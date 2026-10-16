using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

using Flurl.Http.Testing;

using Shouldly;
using Xunit;

using XUnitAssured.Core.DSL;
using XUnitAssured.Core.Storage;
using XUnitAssured.Http.Configuration;
using XUnitAssured.Http.Results;
using XUnitAssured.Http.Steps;

namespace XUnitAssured.Tests.HttpTests;

[Trait("Category", "Http")]
[Trait("Component", "Steps")]
/// <summary>
/// Cada método HTTP pelo caminho do Flurl, sem HttpClient próprio.
///
/// <para>
/// O caminho do HttpClient próprio é o que os testes com o servidor de mentira exercitam; o do
/// Flurl, que é o padrão de quem não passa cliente, tinha só GET e POST cobertos offline. O pedido
/// é falso (HttpTest), e o que se verifica é o verbo e o corpo que saíram, e como uma resposta de
/// erro do servidor vira resultado.
/// </para>
/// </summary>
public class MetodosPeloFlurlTests
{
	private const string Url = "http://api.teste/pedidos/1";

	private static HttpRequestStep Passo(HttpMethod metodo, object? corpo = null) => new()
	{
		Url = Url,
		Method = metodo,
		Body = corpo,
		AuthConfig = new HttpAuthConfig { Type = AuthenticationType.None }
	};

	private static async Task<HttpStepResult> Executar(HttpRequestStep passo) =>
		(HttpStepResult)await passo.ExecuteAsync(new TestContext(new StepStorage()));

	[Fact(DisplayName = "POST, PUT and PATCH with an object should send it as JSON with their own verb")]
	public async Task Json_Verbs_Should_Send_Json()
	{
		using var http = new HttpTest();
		http.RespondWith("{}", 200);

		await Given().ApiResource(Url).Post(new { status = "novo" }).ExecuteAsync().AssertStatusCode(200);
		await Given().ApiResource(Url).Put(new { status = "pago" }).ExecuteAsync().AssertStatusCode(200);
		await Given().ApiResource(Url).Patch(new { status = "enviado" }).ExecuteAsync().AssertStatusCode(200);

		http.ShouldHaveCalled(Url).WithVerb(HttpMethod.Post).WithRequestJson(new { status = "novo" }).Times(1);
		http.ShouldHaveCalled(Url).WithVerb(HttpMethod.Put).WithRequestJson(new { status = "pago" }).Times(1);
		http.ShouldHaveCalled(Url).WithVerb(HttpMethod.Patch).WithRequestJson(new { status = "enviado" }).Times(1);
	}

	// O HttpTest só guarda o corpo que o próprio Flurl serializou; um HttpContent pronto passa sem
	// registro. O pedido que o passo guarda no resultado é lido do HttpRequestMessage, e é ele que
	// mostra o corpo que saiu.
	[Fact(DisplayName = "PUT and PATCH with a raw body should send it as it is, with its content type")]
	public async Task Raw_Verbs_Should_Send_As_Is()
	{
		using var http = new HttpTest();
		http.RespondWith("{}", 200);

		var put = await Given().ApiResource(Url).PutRaw("<pedido/>", "application/xml").ExecuteAsync().GetResult();
		var patch = await Given().ApiResource(Url).PatchRaw("status=pago", "text/plain").ExecuteAsync().GetResult();

		put.Request!.Body.ShouldBe("<pedido/>");
		patch.Request!.Body.ShouldBe("status=pago");
		http.ShouldHaveCalled(Url).WithVerb(HttpMethod.Put).WithContentType("application/xml").Times(1);
		http.ShouldHaveCalled(Url).WithVerb(HttpMethod.Patch).WithContentType("text/plain").Times(1);
	}

	[Fact(DisplayName = "A POST with HttpContent as the body should send that content untouched")]
	public async Task Post_With_HttpContent_Should_Send_It()
	{
		using var http = new HttpTest();
		http.RespondWith("{}", 201);

		var resultado = await Executar(Passo(HttpMethod.Post, new StringContent("a,b", Encoding.UTF8, "text/csv")));

		resultado.StatusCode.ShouldBe(201);
		resultado.Request!.Body.ShouldBe("a,b");
		http.ShouldHaveCalled(Url).WithVerb(HttpMethod.Post).WithContentType("text/csv").Times(1);
	}

	[Fact(DisplayName = "DELETE, HEAD and OPTIONS should go out with their own verb and no body")]
	public async Task Bodiless_Verbs_Should_Go_Out()
	{
		using var http = new HttpTest();
		http.RespondWith("", 204);

		await Given().ApiResource(Url).Delete().ExecuteAsync().AssertStatusCode(204);
		var cabeca = await Executar(Passo(HttpMethod.Head));
		var opcoes = await Executar(Passo(HttpMethod.Options));

		cabeca.StatusCode.ShouldBe(204);
		opcoes.StatusCode.ShouldBe(204);
		http.ShouldHaveCalled(Url).WithVerb(HttpMethod.Delete).Times(1);
		http.ShouldHaveCalled(Url).WithVerb(HttpMethod.Head).Times(1);
		http.ShouldHaveCalled(Url).WithVerb(HttpMethod.Options).Times(1);
		cabeca.Request.ShouldNotBeNull();
		cabeca.Request!.Method.ShouldBe("HEAD");
	}

	[Fact(DisplayName = "A method the step does not support should fail the step and name the method, sending nothing")]
	public async Task Unsupported_Method_Should_Fail()
	{
		using var http = new HttpTest();

		var resultado = await Executar(Passo(HttpMethod.Trace));

		resultado.Success.ShouldBeFalse();
		resultado.Errors.ShouldContain(e => e.Contains("'TRACE' is not supported"));
		http.ShouldNotHaveMadeACall();
	}

	// Um tipo de autenticação sem a sua seção (Basic sem usuário e senha) não tem o que aplicar: o
	// pedido sai sem credencial, e quem recusa é a API.
	[Fact(DisplayName = "An auth type with no settings section should send the request without credentials")]
	public async Task Auth_Type_Without_Section_Should_Send_No_Credentials()
	{
		using var http = new HttpTest();
		http.RespondWith("{}");
		var passo = new HttpRequestStep { Url = Url, AuthConfig = new HttpAuthConfig { Type = AuthenticationType.Basic } };

		var resultado = await Executar(passo);

		resultado.Success.ShouldBeTrue(string.Join("; ", resultado.Errors));
		http.ShouldHaveCalled(Url).WithoutHeader("Authorization").Times(1);
	}

	[Fact(DisplayName = "Query parameters should be added to the URL on the Flurl path")]
	public async Task Query_Parameters_Should_Reach_The_Url()
	{
		using var http = new HttpTest();
		http.RespondWith("[]");

		await Given().ApiResource("http://api.teste/pedidos").WithQueryParam("status", "pago").WithQueryParam("pagina", 3)
			.Get().ExecuteAsync();

		http.ShouldHaveCalled("http://api.teste/pedidos")
			.WithQueryParam("status", "pago")
			.WithQueryParam("pagina", 3)
			.Times(1);
	}

	// Uma resposta de erro é resposta: o resultado traz o status, o corpo, os cabeçalhos e o pedido,
	// para a asserção ler. Só a falta de resposta é falha sem status.
	[Fact(DisplayName = "An error response on the Flurl path should become a result with its status, body, headers and request")]
	public async Task Error_Response_Should_Become_A_Result()
	{
		using var http = new HttpTest();
		http.RespondWith(
			"{\"title\":\"Pedido não encontrado\"}", 404,
			headers: new System.Collections.Generic.Dictionary<string, string> { ["X-Motivo"] = "sem-pedido" });

		var resultado = await Given()
			.ApiResource(Url)
			.WithHeader("X-Correlacao", "c-1")
			.Get()
			.ExecuteAsync()
			.AssertStatusCode(404)
			.AssertJsonPath<string>("$.title", t => t.ShouldBe("Pedido não encontrado"))
			.AssertRequestHeader("X-Correlacao", "c-1")
			.GetResult();

		resultado.Success.ShouldBeFalse("a 404 is a response, but not a successful one");
		resultado.IsClientError.ShouldBeTrue();
		resultado.Headers!.ContainsKey("X-Motivo").ShouldBeTrue("the error response headers must reach the result");
		resultado.Request.ShouldNotBeNull();
		resultado.Request!.Url!.ToString().ShouldBe(Url);
	}

	[Fact(DisplayName = "A server error on the Flurl path should be readable the same way")]
	public async Task Server_Error_Should_Be_Readable()
	{
		using var http = new HttpTest();
		http.RespondWith("falhou", 503);

		var resultado = await Executar(Passo(HttpMethod.Put, new { a = 1 }));

		resultado.StatusCode.ShouldBe(503);
		resultado.IsServerError.ShouldBeTrue();
		resultado.ResponseBody.ShouldBe("falhou");
		resultado.Request!.Method.ShouldBe("PUT");
	}
}
