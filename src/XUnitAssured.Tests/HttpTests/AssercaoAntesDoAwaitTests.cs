using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

using XUnitAssured.Core.DSL;
using XUnitAssured.Http.Extensions;
using XUnitAssured.Http.Results;

namespace XUnitAssured.Tests.HttpTests;

/// <summary>
/// A cadeia comum precisava do await entre parênteses para chegar às asserções:
/// <c>(await Given()...ExecuteAsync()).Then().AssertStatusCode(201)</c>. Uma suíte real escreveu
/// isso 221 vezes. As extensões sobre a Task deixam encadear antes do await.
/// </summary>
[Trait("Category", "Http")]
[Trait("Component", "Assertions")]
public class AssercaoAntesDoAwaitTests
{
	private const string Corpo = """{"id":"abc","name":"Ana","items":[{"sku":"a"},{"sku":"b"}]}""";

	[Fact(DisplayName = "Assertions chain before the await")]
	public async Task Chains_Before_The_Await()
	{
		var resposta = await Pedir(Corpo)
			.Then()
			.AssertStatusCode(200)
			.AssertJsonPath<string>("$.name", n => n == "Ana")
			.AssertJsonPathCount("$.items", 2)
			.AssertHeader("X-Origem", "teste");

		resposta.GetResult().StatusCode.ShouldBe(200);
	}

	[Fact(DisplayName = "A failed assertion before the await surfaces at the await, with the body")]
	public async Task Failure_Surfaces_At_The_Await()
	{
		var erro = await Falha(() => Pedir(Corpo).AssertStatusCode(201));

		erro.Message.ShouldContain("but got 200");
		erro.Message.ShouldContain("\"name\":\"Ana\"");
	}

	[Fact(DisplayName = "An assertion after a failed one does not run")]
	public async Task Stops_At_The_First_Failure()
	{
		var rodou = false;

		await Falha(() => Pedir(Corpo)
			.AssertStatusCode(201)
			.Extract(_ => rodou = true));

		rodou.ShouldBeFalse();
	}

	[Fact(DisplayName = "JsonPath, JsonPathAll and GetResult return the value after the assertions")]
	public async Task Values_After_The_Assertions()
	{
		(await Pedir(Corpo).AssertStatusCode(200).JsonPath<string>("$.id")).ShouldBe("abc");
		(await Pedir(Corpo).AssertStatusCode(200).JsonPathAll<string>("$.items[*].sku")).ShouldBe(["a", "b"]);
		(await Pedir(Corpo).GetResult()).ResponseBody!.ToString().ShouldBe(Corpo);
	}

	[Fact(DisplayName = "The assertions inherited from the generic builder keep the chain on HTTP")]
	public async Task Inherited_Assertions_Keep_The_Http_Chain() =>
		await Pedir(Corpo)
			.AssertSuccess()
			.Validate(r => r.StatusCode.ShouldBe(200))
			.AssertStatusCode(200);

	[Fact(DisplayName = "AssertEach chains before the await on concurrent requests")]
	public async Task AssertEach_On_Concurrent_Requests()
	{
		var servidor = ServidorDeMentira.Sempre(Resposta(Corpo));

		await ScenarioDsl.Given(new ProvedorDeMentira(servidor))
			.ApiResource("/pacientes").Get().Concurrently(3).ExecuteAsync()
			.AssertStatusCode(200)
			.AssertEach(r => r.AssertJsonPath<string>("$.id", id => id == "abc"));

		servidor.Recebidos.Count.ShouldBe(3);
	}

	// Sem isto, um método novo no builder ficaria só na forma com parênteses, e quem escreve
	// na forma nova acharia que ele não existe.
	[Fact(DisplayName = "Every public method of HttpValidationBuilder has its counterpart on the Task")]
	public void Every_Builder_Method_Has_A_Counterpart()
	{
		var extensoes = typeof(HttpValidationTaskExtensions)
			.GetMethods(BindingFlags.Public | BindingFlags.Static)
			.Select(m => Assinatura(m.Name, m.GetParameters().Skip(1)))
			.ToHashSet();

		var faltam = typeof(HttpValidationBuilder)
			.GetMethods(BindingFlags.Public | BindingFlags.Instance)
			.Where(m => m.DeclaringType != typeof(object) && !m.IsSpecialName)
			.Where(m => !m.GetParameters().Any(p => p.IsOut))   // um método async não tem out
			.Select(m => Assinatura(m.Name, m.GetParameters()))
			.Distinct()
			.Where(a => !extensoes.Contains(a))
			.ToList();

		faltam.ShouldBeEmpty("These HttpValidationBuilder methods have no counterpart in HttpValidationTaskExtensions");
	}

	// ---------- Apoio ----------

	// O Should.ThrowAsync do Shouldly deixa passar a própria ShouldAssertException em vez de
	// capturá-la, então a falha esperada é apanhada aqui.
	private static async Task<ShouldAssertException> Falha(Func<Task> cadeia)
	{
		try
		{
			await cadeia();
		}
		catch (ShouldAssertException ex)
		{
			return ex;
		}

		throw new InvalidOperationException("Expected the chain to fail, and it passed.");
	}

	private static string Assinatura(string nome, System.Collections.Generic.IEnumerable<ParameterInfo> parametros) =>
		$"{nome}({string.Join(", ", parametros.Select(p => Nome(p.ParameterType)))})";

	// O tipo genérico do resultado (TResult no builder base) é o HttpStepResult nas extensões.
	private static string Nome(Type tipo) =>
		tipo.IsGenericType
			? $"{tipo.Name}[{string.Join(",", tipo.GetGenericArguments().Select(Nome))}]"
			: tipo.IsGenericParameter && tipo.Name == "TResult" ? nameof(HttpStepResult) : tipo.Name;

	private static HttpResponseMessage Resposta(string corpo) =>
		ServidorDeMentira.Responder(HttpStatusCode.OK, corpo, "application/json", ("X-Origem", "teste"));

	private static Task<HttpValidationBuilder> Pedir(string corpo) =>
		ScenarioDsl.Given(new ProvedorDeMentira(ServidorDeMentira.Sempre(Resposta(corpo))))
			.ApiResource("/pacientes")
			.Get()
			.ExecuteAsync();
}
