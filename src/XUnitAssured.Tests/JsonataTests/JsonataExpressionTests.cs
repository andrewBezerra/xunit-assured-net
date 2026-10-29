using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

using XUnitAssured.Jsonata;

namespace XUnitAssured.Tests.JsonataTests;

[Trait("Category", "Jsonata")]
[Trait("Component", "Expressions")]
/// <summary>
/// A camada de expressões que o teste à mão e o QualityAssured dividem: o que ela avalia, como
/// falha, e os ajustes sobre a biblioteca (limite de tempo e de recursão, $jwt, formato de data).
/// </summary>
public class JsonataExpressionTests
{
	private static readonly JsonNode Pedido = JsonNode.Parse("""
		{
		  "status": 201,
		  "body": {
		    "id": 987,
		    "cupom": null,
		    "cliente": { "id": "cli-1" },
		    "itens": [
		      { "sku": "A1", "preco": 59.9,  "qtd": 2 },
		      { "sku": "B2", "preco": 249.5, "qtd": 1 },
		      { "sku": "C3", "preco": 12.35, "qtd": 4 }
		    ]
		  },
		  "token": "eyJhbGciOiJub25lIn0.eyJzdWIiOiJhbmEiLCJyb2xlIjoicWEifQ.sig"
		}
		""")!;

	private static JsonNode? Valor(string expressao, IReadOnlyDictionary<string, JsonNode?>? variaveis = null) =>
		JsonataExpression.Evaluate(expressao, Pedido, variaveis).Value;

	[Fact(DisplayName = "Evaluate should select, filter and aggregate over the step output")]
	public void Seleciona()
	{
		Valor("body.itens[preco > 100].sku")!.ToJsonString().ShouldBe("\"B2\"");
		Valor("body.itens[preco > 50].sku")!.ToJsonString().ShouldBe("[\"A1\",\"B2\"]");
		Valor("$round($sum(body.itens.(preco * qtd)), 2)")!.GetValue<double>().ShouldBe(418.7);
		Valor("\"pedido-\" & body.id")!.GetValue<string>().ShouldBe("pedido-987");
	}

	[Fact(DisplayName = "Variables should be read as $name and keep their JSON type")]
	public void Variaveis()
	{
		var variaveis = new Dictionary<string, JsonNode?> { ["pedidoId"] = JsonValue.Create(987), ["textoId"] = JsonValue.Create("987") };

		Valor("body.id = $pedidoId", variaveis)!.GetValue<bool>().ShouldBeTrue();
		Valor("body.id = $textoId", variaveis)!.GetValue<bool>().ShouldBeFalse("a number does not equal its text in JSONata");
	}

	[Fact(DisplayName = "A missing field should be undefined, and a null field should be null")]
	public void IndefinidoNaoEhNull()
	{
		var ausente = JsonataExpression.Evaluate("body.desconto", Pedido);
		ausente.IsUndefined.ShouldBeTrue();
		ausente.ToString().ShouldBe("undefined");

		var nulo = JsonataExpression.Evaluate("body.cupom", Pedido);
		nulo.IsUndefined.ShouldBeFalse();
		nulo.Value.ShouldBeNull();
		nulo.ToString().ShouldBe("null");
	}

	[Fact(DisplayName = "Required should fail on undefined with the name in the message")]
	public void Required()
	{
		JsonataExpression.Evaluate("body.id", Pedido).Required("pedidoId")!.GetValue<int>().ShouldBe(987);

		Should.Throw<JsonataEvaluationException>(() => JsonataExpression.Evaluate("body.desconto", Pedido).Required("desconto"))
			.Message.ShouldContain("'desconto' found no value");
	}

	[Fact(DisplayName = "AssertTrue should pass only on true, and say what the expression gave")]
	public void AssertTrue()
	{
		JsonataExpression.AssertTrue("status = 201 and $exists(body.id)", Pedido);

		Should.Throw<JsonataAssertionException>(() => JsonataExpression.AssertTrue("status = 200", Pedido))
			.Message.ShouldBe("expected status = 200 to be true, but it was false");
		Should.Throw<JsonataAssertionException>(() => JsonataExpression.AssertTrue("body.desconto", Pedido))
			.Message.ShouldContain("but it was undefined");
		Should.Throw<JsonataAssertionException>(() => JsonataExpression.AssertTrue("body.id", Pedido))
			.Message.ShouldContain("but it was 987");
		Should.Throw<JsonataAssertionException>(() => JsonataExpression.AssertTrue("\"true\"", Pedido),
			"the text \"true\" is not true");
	}

	[Fact(DisplayName = "A syntax error should be reported with its position, by Validate and by Evaluate")]
	public void Sintaxe()
	{
		JsonataExpression.Validate("body.itens[preco > 100].sku").ShouldBeNull();

		var erro = JsonataExpression.Validate("body.itens[preco > ");
		erro.ShouldNotBeNull();
		erro.ShouldContain("position");

		Should.Throw<JsonataEvaluationException>(() => JsonataExpression.Evaluate("body.itens[preco > ", Pedido))
			.Message.ShouldContain("not valid JSONata");
	}

	[Fact(DisplayName = "A variable should not take the name of a function")]
	public void NomeDeFuncao()
	{
		JsonataExpression.IsFunctionName("sum").ShouldBeTrue();
		JsonataExpression.IsFunctionName("count").ShouldBeTrue();
		JsonataExpression.IsFunctionName("jwt").ShouldBeTrue();
		JsonataExpression.IsFunctionName("total").ShouldBeFalse();

		Should.Throw<ArgumentException>(() => Valor("$sum(body.itens.qtd)", new Dictionary<string, JsonNode?> { ["sum"] = JsonValue.Create(1) }))
			.ParamName.ShouldBe("variables");
	}

	[Fact(DisplayName = "A recursion that never ends should fail the expression, not the process")]
	public void Recursao()
	{
		Should.Throw<JsonataEvaluationException>(() => Valor("($f := function($n){ $n + $f($n - 1) }; $f(5))"))
			.Message.ShouldContain("U1001");
	}

	// No net8.0 do Windows do CI, as 250 chamadas da recursão, com o tratamento da exceção subindo por elas, passaram da
	// pilha de uma thread do pool e derrubaram o processo (#221). A folga não pode depender de onde a avaliação roda.
	[Fact(DisplayName = "The evaluation should run on a thread of its own, with room for the recursion limit, not on the pool (#221)")]
	public async Task ThreadPropria()
	{
		var quemChamou = Environment.CurrentManagedThreadId;

		var (doPool, outra, niveis) = await JsonataExpression.NumaThreadPropria(() =>
		{
			var doPool = Thread.CurrentThread.IsThreadPoolThread;
			// Uns 4 MB de pilha, mais que a de uma thread do pool. Só fora do pool: antes da correção, o teste falha
			// pela asserção, em vez de derrubar o processo.
			return (doPool, Environment.CurrentManagedThreadId != quemChamou, doPool ? 0 : Descer(256));
		});

		doPool.ShouldBeFalse("a pool thread has the platform's stack, not one sized for the recursion limit");
		outra.ShouldBeTrue("the evaluation must not run on the caller's thread, whatever stack it has");
		niveis.ShouldBe(256);
	}

	// Uma chamada com 16 KB de pilha por nível.
	private static int Descer(int niveis)
	{
		Span<byte> quadro = stackalloc byte[16 * 1024];
		quadro[^1] = (byte)niveis;
		return niveis == 0 ? 0 : 1 + Descer(niveis - 1) + (quadro[^1] == (byte)niveis ? 0 : 1);
	}

	[Fact(DisplayName = "A .NET exception from the library should fail the expression with what happened")]
	public void ExcecaoDaBiblioteca()
	{
		// Válida na JSONata, e o jsonata-js filtra o array; o Jsonata.Net.Native 3.0.0 lança um
		// InvalidCastException por dentro. O que importa aqui é a mensagem dizer isso.
		Should.Throw<JsonataEvaluationException>(() => Valor("[body][id > 5].id"))
			.Message.ShouldContain("the evaluation failed: InvalidCastException");
	}

	[Fact(DisplayName = "An expression past the time limit should fail with the limit in the message")]
	public void LimiteDeTempo()
	{
		// Leva cerca de 1,3 s e 120 MB: passa de 100 ms com folga e não pesa no que roda depois.
		var relogio = Stopwatch.StartNew();
		Should.Throw<JsonataEvaluationException>(() =>
				JsonataExpression.Evaluate("[1..1000].([1..1000].($ * 2)) ~> $count()", Pedido, options: new JsonataOptions { Timeout = TimeSpan.FromMilliseconds(100) }))
			.Message.ShouldContain("took longer than 0.1 s");
		relogio.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(1));
	}

	[Fact(DisplayName = "$jwt should return the payload claims, and refuse what is not a JWT")]
	public void Jwt()
	{
		Valor("$jwt(token).sub")!.GetValue<string>().ShouldBe("ana");
		Valor("$jwt(token).role")!.GetValue<string>().ShouldBe("qa");
		JsonataExpression.Evaluate("$jwt(semToken)", Pedido).IsUndefined.ShouldBeTrue();

		Should.Throw<JsonataEvaluationException>(() => Valor("$jwt('isto-nao-e-um-jwt')")).Message.ShouldContain("expects a JWT");
		Should.Throw<JsonataEvaluationException>(() => Valor("$jwt('a.@@@.b')")).Message.ShouldContain("not base64url");
	}

	[Theory(DisplayName = "A date picture in the XPath format of the JSONata docs should fail with an error that says so")]
	[InlineData("$fromMillis(1792411200000, '[Y0001]-[M01]-[D01]')")]
	[InlineData("$toMillis('18/10/2026', '[D01]/[M01]/[Y0001]')")]
	[InlineData("$now('[H01]:[m01]')")]
	public void FormatoDaDocumentacao(string expressao)
	{
		Should.Throw<JsonataEvaluationException>(() => Valor(expressao))
			.Message.ShouldContain("XPath format of the JSONata docs");
	}

	[Fact(DisplayName = "Date functions should take .NET formats, and default to ISO 8601 in UTC")]
	public void FormatoDoNet()
	{
		Valor("$fromMillis(1792411200000)")!.GetValue<string>().ShouldBe("2026-10-19T12:00:00.000Z");
		Valor("$fromMillis(1792411200000, 'dd/MM/yyyy')")!.GetValue<string>().ShouldBe("19/10/2026");
		Valor("$toMillis('2026-10-19T12:00:00.000Z')")!.GetValue<long>().ShouldBe(1792411200000);
		Valor("$toMillis('19/10/2026', 'dd/MM/yyyy')")!.GetValue<long>().ShouldBe(1792368000000);
		Valor("$toMillis($now()) > 0")!.GetValue<bool>().ShouldBeTrue();
		JsonataExpression.Evaluate("$fromMillis(semData)", Pedido).IsUndefined.ShouldBeTrue();
	}

	[Fact(DisplayName = "Text should write a string without quotes, nothing for null, and JSON for the rest")]
	public void Texto()
	{
		JsonataExpression.Text(JsonValue.Create("cli-1")).ShouldBe("cli-1");
		JsonataExpression.Text(JsonValue.Create(987)).ShouldBe("987");
		JsonataExpression.Text(JsonNode.Parse("59.90")).ShouldBe("59.90");
		JsonataExpression.Text(JsonValue.Create(true)).ShouldBe("true");
		JsonataExpression.Text(null).ShouldBe("");
		JsonataExpression.Text(JsonNode.Parse("""["A1", "B2"]""")).ShouldBe("""["A1","B2"]""");
	}
}
