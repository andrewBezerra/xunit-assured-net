using System;
using System.Collections.Generic;
using System.Text.Json;

using Shouldly;
using Xunit;

using XUnitAssured.Http.Extensions;
using XUnitAssured.Http.Results;

namespace XUnitAssured.Tests.HttpTests;

/// <summary>
/// Listas no caminho JSON.
///
/// <para>
/// Até a 6.1 o caminho só lia um valor, e um índice só era reconhecido depois de um nome de
/// propriedade: um endpoint que devolve uma lista na raiz não podia ser verificado, e o teste
/// caía para o HttpClient cru. Numa suíte real foi o principal motivo de abandonar a DSL.
/// </para>
///
/// <para>
/// A razão de ter verbos de coleção, e não só um JsonPathAll: uma busca só é testada quando o
/// teste prova o que ela deixou de fora. "totalCount == 1 e items[0].id == x" depende da ordem e
/// não diz nada sobre o que não podia estar ali.
/// </para>
///
/// Sem rede: o resultado é montado à mão.
/// </summary>
[Trait("Category", "Http")]
[Trait("Component", "Results")]
public class JsonPathColecoesTests
{
	private const string ListaNaRaiz = """[{"id":"a","city":"Rio"},{"id":"b","city":"Rio"},{"id":"c","city":"Recife"}]""";
	private const string ListaNumObjeto = """{"totalCount":2,"items":[{"sku":"A1","tags":["x","y"]},{"sku":"B2","tags":["z"]}]}""";

	// Um objeto qualquer faz o papel do construtor da cadeia: o que se testa é a verificação.
	private static readonly object Cadeia = new();

	// ---------- O caminho ----------

	[Fact(DisplayName = "A root array can be indexed")]
	public void Root_Array_Can_Be_Indexed()
	{
		Resposta(ListaNaRaiz).JsonPath<string>("$[1].id").ShouldBe("b");
		Resposta(ListaNaRaiz).JsonPath<JsonElement>("$[2]").GetProperty("city").GetString().ShouldBe("Recife");
	}

	[Fact(DisplayName = "[*] selects a value from every item of a root array")]
	public void Wildcard_Selects_From_A_Root_Array()
	{
		Resposta(ListaNaRaiz).JsonPathAll<string>("$[*].id").ShouldBe(["a", "b", "c"]);
	}

	[Fact(DisplayName = "[*] selects a value from every item of an array inside an object")]
	public void Wildcard_Selects_From_An_Inner_Array()
	{
		Resposta(ListaNumObjeto).JsonPathAll<string>("$.items[*].sku").ShouldBe(["A1", "B2"]);
	}

	[Fact(DisplayName = "Two [*] flatten nested lists in document order")]
	public void Two_Wildcards_Flatten()
	{
		Resposta(ListaNumObjeto).JsonPathAll<string>("$.items[*].tags[*]").ShouldBe(["x", "y", "z"]);
	}

	[Fact(DisplayName = "JsonPath refuses [*] and points to the verbs that read many values")]
	public void JsonPath_Refuses_Wildcard()
	{
		var erro = Should.Throw<InvalidOperationException>(() => Resposta(ListaNaRaiz).JsonPath<string>("$[*].id"));
		erro.Message.ShouldContain("JsonPathAll");
	}

	[Fact(DisplayName = "[*] on something that is not an array says what it found")]
	public void Wildcard_On_A_Non_Array_Fails()
	{
		var erro = Should.Throw<InvalidOperationException>(() => Resposta(ListaNumObjeto).JsonPathAll<int>("$.totalCount[*]"));
		erro.Message.ShouldContain("Number");
	}

	[Fact(DisplayName = "A malformed index is reported, not guessed")]
	public void Malformed_Index_Is_Reported()
	{
		Should.Throw<FormatException>(() => Resposta(ListaNumObjeto).JsonPath<string>("$.items[x].sku"));
		Should.Throw<FormatException>(() => Resposta(ListaNumObjeto).JsonPath<string>("$.items[0.sku"));
	}

	// ---------- Os verbos ----------

	[Fact(DisplayName = "Contains passes when the value is among those selected")]
	public void Contains_Passes()
	{
		Cadeia.AssertJsonPathContains(Resposta(ListaNaRaiz), "$[*].id", "b");
	}

	[Fact(DisplayName = "Contains fails and lists what was there")]
	public void Contains_Fails_With_The_Values()
	{
		var erro = Should.Throw<ShouldAssertException>(() =>
			Cadeia.AssertJsonPathContains(Resposta(ListaNaRaiz), "$[*].id", "z"));

		erro.Message.ShouldContain("\"a\",\"b\",\"c\"");
	}

	[Fact(DisplayName = "NotContains fails when a value that should have been left out is there")]
	public void Not_Contains_Fails_When_Present()
	{
		Cadeia.AssertJsonPathNotContains(Resposta(ListaNaRaiz), "$[*].id", "z");

		Should.Throw<ShouldAssertException>(() =>
			Cadeia.AssertJsonPathNotContains(Resposta(ListaNaRaiz), "$[*].id", "c"));
	}

	[Fact(DisplayName = "Count reads the length of an array, or how many values [*] selects")]
	public void Count_Reads_Array_Or_Projection()
	{
		Cadeia.AssertJsonPathCount(Resposta(ListaNaRaiz), "$", 3);
		Cadeia.AssertJsonPathCount(Resposta(ListaNumObjeto), "$.items", 2);
		Cadeia.AssertJsonPathCount(Resposta(ListaNumObjeto), "$.items[*].tags[*]", 3);

		var erro = Should.Throw<ShouldAssertException>(() =>
			Cadeia.AssertJsonPathCount(Resposta(ListaNaRaiz), "$", 1));
		erro.Message.ShouldContain("had 3");
	}

	[Fact(DisplayName = "Count on a value that is not an array says so")]
	public void Count_On_A_Non_Array_Fails()
	{
		var erro = Should.Throw<InvalidOperationException>(() =>
			Cadeia.AssertJsonPathCount(Resposta(ListaNumObjeto), "$.totalCount", 2));
		erro.Message.ShouldContain("not an array");
	}

	[Fact(DisplayName = "All passes when every value satisfies the condition, and names the ones that do not")]
	public void All_Checks_Every_Value()
	{
		Cadeia.AssertJsonPathAll<object, string>(Resposta(ListaNaRaiz), "$[*].city", c => c.Length > 0);

		var erro = Should.Throw<ShouldAssertException>(() =>
			Cadeia.AssertJsonPathAll<object, string>(Resposta(ListaNaRaiz), "$[*].city", c => c == "Rio"));
		erro.Message.ShouldContain("Recife");
	}

	// O motivo de o verbo existir com esta regra: uma lista vazia satisfaz qualquer condição, e
	// uma busca que não devolveu nada passaria em "todo resultado é da cidade pedida".
	[Fact(DisplayName = "All fails when the path selects nothing")]
	public void All_Fails_On_An_Empty_Selection()
	{
		var erro = Should.Throw<ShouldAssertException>(() =>
			Cadeia.AssertJsonPathAll<object, string>(Resposta("[]"), "$[*].city", c => c == "Rio"));
		erro.Message.ShouldContain("selected no values");
	}

	private static HttpStepResult Resposta(string corpo) =>
		HttpStepResult.CreateHttpSuccess(statusCode: 200, responseBody: corpo);
}
