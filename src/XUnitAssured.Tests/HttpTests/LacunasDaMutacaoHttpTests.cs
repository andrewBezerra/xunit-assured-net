using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using Flurl.Http;
using Flurl.Http.Testing;

using Shouldly;
using Xunit;

using XUnitAssured.Core.Abstractions;
using XUnitAssured.Core.DSL;
using XUnitAssured.Core.Results;
using XUnitAssured.Http.Configuration;
using XUnitAssured.Http.Extensions;
using XUnitAssured.Http.Handlers;
using XUnitAssured.Http.Results;

namespace XUnitAssured.Tests.HttpTests;

/// <summary>
/// O que a primeira rodada de mutação (#128) achou sem guarda no Http (#145).
///
/// <para>
/// O código está certo; o que faltava era um teste que notasse se ele quebrasse. O caso mais sério
/// é o de uma asserção que deixa de falhar: os testes tinham só condições que passam, e uma
/// asserção que nunca falha transforma qualquer teste num teste verde.
/// </para>
///
/// <para>
/// Os handlers de autenticação ficam aqui também: os exemplos locais mandam o pedido por um
/// HttpClient próprio, que não usa os handlers (#147), então nada os aplicava.
/// </para>
/// </summary>
[Trait("Category", "Http")]
[Trait("Component", "Assertions")]
public class LacunasDaMutacaoHttpTests
{
	// ---------- Asserções que precisam falhar ----------

	[Fact(DisplayName = "AssertJsonPath with a predicate fails when it is false, with the custom message")]
	public async Task AssertJsonPath_Predicate_Fails()
	{
		var resposta = await Pedir(Json("""{"status":"open","itens":[1,2]}"""));

		Should.Throw<ShouldAssertException>(() => resposta.AssertJsonPath<string>("$.status", s => s == "closed", "o pedido deveria fechar"))
			.Message.ShouldContain("o pedido deveria fechar");
		Should.Throw<ShouldAssertException>(() => resposta.AssertJsonPathAll<HttpValidationBuilder, int>(resposta.GetResult(), "$.itens[*]", i => i > 1, "todo item acima de 1"))
			.Message.ShouldContain("todo item acima de 1");
		Should.Throw<ShouldAssertException>(() => resposta.AssertJsonPathAny<HttpValidationBuilder, int>(resposta.GetResult(), "$.itens[*]", i => i > 5, "algum item acima de 5"))
			.Message.ShouldContain("algum item acima de 5");
	}

	[Fact(DisplayName = "AssertHeader and AssertSetCookie with a predicate fail when it is false, with the custom message")]
	public async Task Response_Header_And_Cookie_Predicates_Fail()
	{
		var resposta = await Pedir(ServidorDeMentira.Responder(HttpStatusCode.OK, "{}", "application/json",
			("X-Versao", "2"), ("Set-Cookie", "sid=abc; Path=/")));

		resposta.AssertHeader("X-Versao", v => v == "2").AssertSetCookie("sid", c => c.Value == "abc");

		var erro = Should.Throw<ShouldAssertException>(() => resposta.AssertHeader("X-Versao", v => v == "3", "a versão 3"));
		erro.Message.ShouldContain("a versão 3");
		erro.Message.ShouldContain("Values: 2");
		Should.Throw<ShouldAssertException>(() => resposta.AssertSetCookie("sid", c => c.Value == "xyz", "a sessão nova"))
			.Message.ShouldContain("a sessão nova");
	}

	[Fact(DisplayName = "AssertRequestHeader and AssertSentCookie with a predicate fail when it is false, with the custom message")]
	public async Task Request_Header_And_Cookie_Predicates_Fail()
	{
		var resposta = (await ScenarioDsl.Given(new ProvedorDeMentira(ServidorDeMentira.Sempre(Json("{}"))))
			.ApiResource("/sessao")
			.WithHeader("X-Rastreio", "t1")
			.WithHeader("Cookie", "sid=abc")
			.Get()
			.ExecuteAsync()).Then();

		resposta.AssertRequestHeader("X-Rastreio", v => v == "t1").AssertSentCookie("sid", v => v == "abc");

		var erro = Should.Throw<ShouldAssertException>(() => resposta.AssertRequestHeader("X-Rastreio", v => v == "t2", "o rastreio t2"));
		erro.Message.ShouldContain("o rastreio t2");
		erro.Message.ShouldContain("Values: t1");
		Should.Throw<ShouldAssertException>(() => resposta.AssertRequestHeader("X-Ausente", _ => true))
			.Message.ShouldContain("Expected the request to be sent with a X-Ausente header");
		Should.Throw<ShouldAssertException>(() => resposta.AssertSentCookie("sid", v => v == "xyz", "a sessão xyz"))
			.Message.ShouldContain("a sessão xyz");
	}

	[Fact(DisplayName = "AssertProblemDetails with a predicate fails when it is false, with the custom message")]
	public async Task ProblemDetails_Predicate_Fails()
	{
		var resposta = await Pedir(ServidorDeMentira.Responder(HttpStatusCode.Conflict,
			"""{"title":"Conflito","status":409,"code":"ScheduleConflict"}""", "application/problem+json"));

		resposta.AssertProblemDetails(409, p => p.Title == "Conflito");
		Should.Throw<ShouldAssertException>(() => resposta.AssertProblemDetails(409, p => p.Title == "Outro", "o título do conflito"))
			.Message.ShouldContain("o título do conflito");
	}

	[Fact(DisplayName = "Before the await, AssertSuccess fails a failed response and Validate runs its check")]
	public async Task Task_Assertions_Run()
	{
		var erro = await Falha(() => Executar(ServidorDeMentira.Responder(HttpStatusCode.InternalServerError, "{}")).AssertSuccess());
		erro.Message.ShouldContain("Expected step to succeed");

		HttpStepResult? visto = null;
		await Executar(Json("{}")).Validate(r => visto = r);
		visto.ShouldNotBeNull();
	}

	// ---------- Comportamento ----------

	[Fact(DisplayName = "A query parameter joins a URL that already has a query with &")]
	public async Task Query_Joins_With_Ampersand()
	{
		var servidor = ServidorDeMentira.Sempre(Json("{}"));
		await ScenarioDsl.Given(new ProvedorDeMentira(servidor))
			.ApiResource("/busca?a=1")
			.WithQueryParam("b", 2)
			.Get()
			.ExecuteAsync();

		servidor.Recebidos.ShouldHaveSingleItem().Consulta.ShouldBe("?a=1&b=2");
	}

	// O último verbo vale, e um GET não leva corpo: o corpo só vai em POST, PUT e PATCH.
	[Fact(DisplayName = "Get() after Post(body) sends a GET, without the body")]
	public async Task Get_After_Post_Sends_No_Body()
	{
		HttpRequestMessage? recebido = null;
		var servidor = new ServidorDeMentira(p =>
		{
			recebido = p;
			return Json("{}");
		});

		await ScenarioDsl.Given(new ProvedorDeMentira(servidor))
			.ApiResource("/itens")
			.Post(new { nome = "a" })
			.Get()
			.ExecuteAsync();

		recebido.ShouldNotBeNull().Method.ShouldBe(HttpMethod.Get);
		recebido.Content.ShouldBeNull();
	}

	[Fact(DisplayName = "The response's content headers, such as Content-Type, are among its headers")]
	public async Task Content_Headers_Are_Headers()
	{
		var resposta = await Pedir(Json("{}"));

		resposta.AssertHeader("Content-Type", v => v.StartsWith("application/json", StringComparison.Ordinal));
	}

	[Theory(DisplayName = "Status classes include both ends of their range")]
	[InlineData(199, false, false, false, false)]
	[InlineData(200, true, false, false, false)]
	[InlineData(299, true, false, false, false)]
	[InlineData(300, false, true, false, false)]
	[InlineData(399, false, true, false, false)]
	[InlineData(400, false, false, true, false)]
	[InlineData(499, false, false, true, false)]
	[InlineData(500, false, false, false, true)]
	[InlineData(599, false, false, false, true)]
	[InlineData(600, false, false, false, false)]
	public void Status_Classes(int status, bool sucesso, bool redirecionamento, bool erroDoCliente, bool erroDoServidor)
	{
		var resultado = HttpStepResult.CreateHttpSuccess(statusCode: status, responseBody: "{}");

		resultado.IsSuccessStatusCode.ShouldBe(sucesso);
		resultado.Success.ShouldBe(sucesso);
		resultado.IsRedirect.ShouldBe(redirecionamento);
		resultado.IsClientError.ShouldBe(erroDoCliente);
		resultado.IsServerError.ShouldBe(erroDoServidor);
	}

	[Fact(DisplayName = "When one concurrent request gets no response, the combined step is recorded as failed")]
	public async Task Concurrent_Step_Fails_With_One_Failure()
	{
		var vez = 0;
		var servidor = new ServidorDeMentira(_ => Interlocked.Increment(ref vez) == 2
			? throw new HttpRequestException("conexão recusada")
			: Json("{}"));

		var resultado = (await ScenarioDsl.Given(new ProvedorDeMentira(servidor))
			.ApiResource("/x")
			.Get()
			.Concurrently(3)
			.ExecuteAsync()).GetResult();

		resultado.Metadata.Status.ShouldBe(StepStatus.Failed);
	}

	[Fact(DisplayName = "An arrange request that got no response says so")]
	public async Task Arrange_Without_Response_Says_So()
	{
		var servidor = new ServidorDeMentira(_ => throw new HttpRequestException("conexão recusada"));

		var erro = await Falha(() => ScenarioDsl.Given(new ProvedorDeMentira(servidor))
			.ApiResource("/preparo")
			.Post(new { })
			.EnsureSuccessAsync());

		erro.Message.ShouldContain("The arrange request got no response");
		erro.Message.ShouldContain("conexão recusada");
	}

	// A configuração de um ambiente sobrepõe a base: o que ela define vale, o que não define fica.
	// "Não define" é null (e 0 no Timeout). O construtor de HttpSettings preenche Authentication,
	// DefaultHeaders e Timeout, então uma sobreposição criada por ele, ou lida de um JSON que omite
	// esses campos, troca os valores da base pelos padrões -- registrado na #146.
	[Fact(DisplayName = "Merging settings keeps what the override leaves out and takes what it sets")]
	public void Settings_Merge()
	{
		var autenticacao = new HttpAuthConfig { Type = AuthenticationType.Bearer };
		var basica = new HttpSettings
		{
			BaseUrl = "https://base",
			Timeout = 30,
			DefaultHeaders = new Dictionary<string, string> { ["A"] = "1", ["B"] = "1" },
			Authentication = autenticacao
		};

		var mantida = basica.Merge(new HttpSettings { Timeout = 0, Authentication = null, DefaultHeaders = null });
		mantida.BaseUrl.ShouldBe("https://base");
		mantida.Timeout.ShouldBe(30);
		mantida.Authentication.ShouldBeSameAs(autenticacao);
		mantida.DefaultHeaders.ShouldNotBeNull()["A"].ShouldBe("1");

		var outra = new HttpAuthConfig { Type = AuthenticationType.Basic };
		var trocada = basica.Merge(new HttpSettings
		{
			BaseUrl = "https://outro",
			Timeout = 5,
			DefaultHeaders = new Dictionary<string, string> { ["B"] = "2" },
			Authentication = outra
		});
		trocada.BaseUrl.ShouldBe("https://outro");
		trocada.Timeout.ShouldBe(5);
		trocada.Authentication.ShouldBeSameAs(outra);
		trocada.DefaultHeaders.ShouldNotBeNull()["A"].ShouldBe("1");
		trocada.DefaultHeaders["B"].ShouldBe("2");

		new HttpSettings { DefaultHeaders = null }.Merge(new HttpSettings { DefaultHeaders = null }).DefaultHeaders.ShouldBeNull();
	}

	[Fact(DisplayName = "A Set-Cookie with no name is not a cookie, and an attribute with no value reads as null")]
	public void SetCookie_Edges()
	{
		SetCookie.Parse("=abc").ShouldBeNull();

		var cookie = SetCookie.Parse("sid=abc; Path; Domain=exemplo.com; HttpOnly").ShouldNotBeNull();
		cookie.Path.ShouldBeNull();
		cookie.Domain.ShouldBe("exemplo.com");
		cookie.HttpOnly.ShouldBeTrue();
	}

	[Fact(DisplayName = "A problem details status that is not a number reads as null")]
	public void ProblemDetails_Status_Not_A_Number()
	{
		ProblemDetailsResponse.Parse("""{"status":409}""").Status.ShouldBe(409);
		ProblemDetailsResponse.Parse("""{"status":"409"}""").Status.ShouldBeNull();
	}

	// ---------- Handlers de autenticação ----------

	[Fact(DisplayName = "Basic, Bearer, ApiKey and custom header handlers put the credentials on the request")]
	public void Handlers_Apply_Credentials()
	{
		Cabecalho(new BasicAuthHandler(new BasicAuthConfig { Username = "ana", Password = "s3nha" }), "Authorization")
			.ShouldBe($"Basic {Convert.ToBase64String("ana:s3nha"u8.ToArray())}");
		Cabecalho(new BearerAuthHandler(new BearerAuthConfig { Token = "tk" }), "Authorization").ShouldBe("Bearer tk");
		Cabecalho(new BearerAuthHandler(new BearerAuthConfig { Token = "tk", Prefix = "Token" }), "Authorization").ShouldBe("Token tk");
		Cabecalho(new ApiKeyAuthHandler(new ApiKeyAuthConfig { KeyName = "X-Chave", KeyValue = "k1" }), "X-Chave").ShouldBe("k1");

		var naConsulta = new FlurlRequest("http://api.teste/itens");
		new ApiKeyAuthHandler(new ApiKeyAuthConfig { KeyName = "chave", KeyValue = "k1", Location = ApiKeyLocation.Query }).ApplyAuthentication(naConsulta);
		naConsulta.Url.QueryParams.FirstOrDefault("chave").ShouldBe("k1");
		naConsulta.Headers.Contains("chave").ShouldBeFalse();

		var personalizado = new CustomHeaderAuthConfig();
		personalizado.AddHeader("X-Tenant", "colih");
		personalizado.AddHeader("X-Origem", "teste");
		var pedido = new FlurlRequest("http://api.teste/itens");
		new CustomHeaderAuthHandler(personalizado).ApplyAuthentication(pedido);
		pedido.Headers.FirstOrDefault("X-Tenant").ShouldBe("colih");
		pedido.Headers.FirstOrDefault("X-Origem").ShouldBe("teste");
	}

	// O pedido do token é falso (HttpTest do Flurl): sem servidor, e dá para ver o que foi pedido.
	[Fact(DisplayName = "OAuth2 asks for a token with client credentials, sends it as Bearer, and reuses it while valid")]
	public void OAuth2_Gets_And_Caches_The_Token()
	{
		using var http = new HttpTest();
		http.RespondWithJson(new { access_token = "tk-1", token_type = "Bearer", expires_in = 3600 });

		var handler = new OAuth2Handler(new OAuth2Config
		{
			TokenUrl = "http://auth.teste/token",
			ClientId = "cliente",
			ClientSecret = "segredo"
		});

		var primeiro = new FlurlRequest("http://api.teste/a");
		handler.ApplyAuthentication(primeiro);
		var segundo = new FlurlRequest("http://api.teste/b");
		handler.ApplyAuthentication(segundo);

		primeiro.Headers.FirstOrDefault("Authorization").ShouldBe("Bearer tk-1");
		segundo.Headers.FirstOrDefault("Authorization").ShouldBe("Bearer tk-1");
		http.ShouldHaveCalled("http://auth.teste/token")
			.WithVerb(HttpMethod.Post)
			.WithRequestBody("*grant_type=client_credentials*")
			.WithRequestBody("*client_id=cliente*")
			.Times(1);
	}

	[Fact(DisplayName = "OAuth2 says why when the token request is refused")]
	public void OAuth2_Refused_Says_Why()
	{
		using var http = new HttpTest();
		http.RespondWith("""{"error":"invalid_client"}""", 401);

		var handler = new OAuth2Handler(new OAuth2Config { TokenUrl = "http://auth.teste/token", ClientId = "c", ClientSecret = "errado" });

		Should.Throw<InvalidOperationException>(() => handler.ApplyAuthentication(new FlurlRequest("http://api.teste/a")))
			.Message.ShouldContain("invalid_client");
	}

	// ---------- Apoio ----------

	private static HttpResponseMessage Json(string corpo) => ServidorDeMentira.Responder(HttpStatusCode.OK, corpo);

	private static Task<HttpValidationBuilder> Executar(HttpResponseMessage resposta) =>
		ScenarioDsl.Given(new ProvedorDeMentira(ServidorDeMentira.Sempre(resposta)))
			.ApiResource("/qualquer")
			.Get()
			.ExecuteAsync();

	private static async Task<HttpValidationBuilder> Pedir(HttpResponseMessage resposta) => (await Executar(resposta)).Then();

	private static string? Cabecalho(IAuthenticationHandler handler, string nome)
	{
		var pedido = new FlurlRequest("http://api.teste/itens");
		handler.ApplyAuthentication(pedido);
		return pedido.Headers.FirstOrDefault(nome);
	}

	// O Should.ThrowAsync do Shouldly deixa passar a própria ShouldAssertException em vez de
	// capturá-la, então a falha esperada é apanhada aqui.
	private static async Task<ShouldAssertException> Falha(Func<Task> acao)
	{
		try
		{
			await acao();
		}
		catch (ShouldAssertException ex)
		{
			return ex;
		}

		throw new InvalidOperationException("Expected a failure, and it passed.");
	}
}
