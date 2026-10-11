using System;
using System.Linq;
using System.Text.Json;

using Shouldly;
using Xunit;

using XUnitAssured.Http.Extensions;
using XUnitAssured.Http.Results;

namespace XUnitAssured.Tests.HttpTests;

/// <summary>
/// O filtro no caminho JSON: achar o item de uma lista pelo valor de um campo.
///
/// <para>
/// Sem ele, uma suíte real lia a lista inteira como JsonElement, escolhia o item com LINQ e lia
/// cada campo com GetProperty, em 7 lugares; cinco comentários diziam "o caminho JSON não filtra".
/// </para>
///
/// Sem rede: o resultado é montado à mão.
/// </summary>
[Trait("Category", "Http")]
[Trait("Component", "Results")]
public class JsonPathFiltroTests
{
	private const string Escala = """
		{"drafts":[
			{"weekStart":"2026-10-05","memberIds":["a","b"],"incomplete":{"requested":3},"order":1,"published":true},
			{"weekStart":"2026-10-12","memberIds":["c","d","e"],"incomplete":null,"order":2,"published":false},
			{"weekStart":"2026-10-19","memberIds":["f"],"order":2.0,"published":false,"note":"a]b == 'x'"}
		],"tags":["rio","recife"]}
		""";

	// Um objeto qualquer faz o papel do construtor da cadeia: o que se testa é a verificação.
	private static readonly object Cadeia = new();

	// ---------- Achar um item ----------

	[Fact(DisplayName = "A filter finds the item by a text field, and the path goes on inside it")]
	public void Finds_The_Item_By_A_Text_Field()
	{
		Ler<int>("$.drafts[?(@.weekStart=='2026-10-05')].incomplete.requested").ShouldBe(3);
		Ler<string>("$.drafts[?(@.weekStart=='2026-10-12')].memberIds[2]").ShouldBe("e");
	}

	[Fact(DisplayName = "A filter compares numbers by value, and true, false and null by kind")]
	public void Compares_Numbers_Booleans_And_Null()
	{
		Ler<string>("$.drafts[?(@.order==1)].weekStart").ShouldBe("2026-10-05");
		Ler<string>("$.drafts[?(@.published==true)].weekStart").ShouldBe("2026-10-05");
		Ler<string>("$.drafts[?(@.incomplete==null)].weekStart").ShouldBe("2026-10-12");
		Resposta(Escala).JsonPathAll<string>("$.drafts[?(@.order==2)].weekStart").ShouldBe(["2026-10-12", "2026-10-19"]);
	}

	[Fact(DisplayName = "A filter reads double quotes, no parentheses, a nested field, and @ alone")]
	public void Accepts_The_Common_Spellings()
	{
		Ler<string>("$.drafts[?(@.weekStart==\"2026-10-19\")].memberIds[0]").ShouldBe("f");
		Ler<string>("$.drafts[?@.weekStart=='2026-10-19'].memberIds[0]").ShouldBe("f");
		Ler<string>("$.drafts[?(@.incomplete.requested==3)].weekStart").ShouldBe("2026-10-05");
		Ler<string>("$.tags[?(@=='recife')]").ShouldBe("recife");
	}

	[Fact(DisplayName = "A ] or == inside the quoted value belongs to the value")]
	public void Brackets_And_Operators_Inside_Quotes()
	{
		Ler<string>("$.drafts[?(@.note=='a]b == \\'x\\'')].weekStart").ShouldBe("2026-10-19");
	}

	[Fact(DisplayName = "Text and number do not mix: '1' is not 1")]
	public void Text_Is_Not_A_Number()
	{
		Resposta(Escala).JsonPathAll<JsonElement>("$.drafts[?(@.order=='1')]").ShouldBeEmpty();
	}

	[Fact(DisplayName = "!= is the opposite of ==: an item without the field is selected")]
	public void Not_Equal_Includes_Items_Without_The_Field()
	{
		Resposta(Escala).JsonPathAll<string>("$.drafts[?(@.incomplete!=null)].weekStart")
			.ShouldBe(["2026-10-05", "2026-10-19"]);
	}

	// ---------- Nenhum ou mais de um ----------

	[Fact(DisplayName = "Reading one value, a filter that matches nothing says how many items there were")]
	public void No_Match_Says_How_Many_There_Were()
	{
		Should.Throw<InvalidOperationException>(() => Ler<string>("$.drafts[?(@.weekStart=='2027-01-04')].weekStart"))
			.Message.ShouldBe("The filter [?(@.weekStart=='2027-01-04')] in '$.drafts[?(@.weekStart=='2027-01-04')].weekStart' matched none of the 3 item(s) there.");
	}

	[Fact(DisplayName = "Reading one value, a filter that matches several points to JsonPathAll")]
	public void Several_Matches_Point_To_JsonPathAll()
	{
		var erro = Should.Throw<InvalidOperationException>(() => Ler<string>("$.drafts[?(@.published==false)].weekStart"));

		erro.Message.ShouldContain("matched 2 of the 3 item(s)");
		erro.Message.ShouldContain("JsonPathAll");
	}

	[Fact(DisplayName = "A filter on something that is not a list says what it found")]
	public void Filter_On_An_Object()
	{
		Should.Throw<InvalidOperationException>(() => Ler<string>("$.drafts[0].incomplete[?(@.requested==3)]"))
			.Message.ShouldContain("expects an array, but the value there is Object");
	}

	[Theory(DisplayName = "A filter this library does not read is reported, not guessed")]
	[InlineData("$.drafts[?(@.order>1)]", "is not a filter this library reads")]
	[InlineData("$.drafts[?(weekStart=='x')]", "is not a filter this library reads")]
	[InlineData("$.drafts[?(@.==1)]", "empty field name")]
	[InlineData("$.drafts[?(@.weekStart==x)]", "is not a value")]
	[InlineData("$.drafts[?(@.weekStart=='x')", "opens '[' without closing it")]
	public void Malformed_Filter(string caminho, string trecho)
	{
		Should.Throw<FormatException>(() => Resposta(Escala).JsonPathAll<JsonElement>(caminho))
			.Message.ShouldContain(trecho);
	}

	// ---------- Os verbos ----------

	[Fact(DisplayName = "Count counts the matches of a filter at the end, and the list of the item a filter finds")]
	public void Count_With_A_Filter()
	{
		var resposta = Resposta(Escala);

		Cadeia.AssertJsonPathCount(resposta, "$.drafts[?(@.published==false)]", 2);
		Cadeia.AssertJsonPathCount(resposta, "$.drafts[?(@.weekStart=='2026-10-12')].memberIds", 3);
	}

	[Fact(DisplayName = "Null reads a field of the item a filter finds")]
	public void Null_Through_A_Filter()
	{
		Cadeia.AssertJsonPathNull(Resposta(Escala), "$.drafts[?(@.weekStart=='2026-10-12')].incomplete");
	}

	[Fact(DisplayName = "Missing passes when a filter at the end finds nothing, and fails when one in the middle finds nothing")]
	public void Missing_Through_A_Filter()
	{
		var resposta = Resposta(Escala);

		Cadeia.AssertJsonPathMissing(resposta, "$.drafts[?(@.weekStart=='2027-01-04')]");
		Cadeia.AssertJsonPathMissing(resposta, "$.drafts[?(@.weekStart=='2026-10-19')].incomplete");
		Should.Throw<ShouldAssertException>(() =>
				Cadeia.AssertJsonPathMissing(resposta, "$.drafts[?(@.weekStart=='2027-01-04')].incomplete"))
			.Message.ShouldContain("the path already stops at $.drafts, which is an array of 3 item(s)");
	}

	[Fact(DisplayName = "Any passes when one value satisfies the condition")]
	public void Any_Passes()
	{
		Cadeia.AssertJsonPathAny<object, JsonElement>(Resposta(Escala), "$.drafts[*]", d =>
			d.GetProperty("published").GetBoolean() == false &&
			d.GetProperty("memberIds").EnumerateArray().Any(m => m.GetString() == "f"));
	}

	[Fact(DisplayName = "Any fails and shows what was there")]
	public void Any_Fails_With_The_Values()
	{
		Should.Throw<ShouldAssertException>(() =>
				Cadeia.AssertJsonPathAny<object, string>(Resposta(Escala), "$.tags[*]", t => t == "salvador"))
			.Message.ShouldContain("Some value at $.tags[*] should satisfy the condition. None of the 2 did: [\"rio\",\"recife\"]");
	}

	[Fact(DisplayName = "Any fails when the path selects nothing")]
	public void Any_Fails_On_An_Empty_Selection()
	{
		Should.Throw<ShouldAssertException>(() =>
				Cadeia.AssertJsonPathAny<object, JsonElement>(Resposta(Escala), "$.drafts[?(@.order==9)]", _ => true))
			.Message.ShouldContain("The path selected no values.");
	}

	// ---------- Apoio ----------

	private static T Ler<T>(string caminho) => Resposta(Escala).JsonPath<T>(caminho);

	private static HttpStepResult Resposta(string corpo) =>
		HttpStepResult.CreateHttpSuccess(statusCode: 200, responseBody: corpo);
}
