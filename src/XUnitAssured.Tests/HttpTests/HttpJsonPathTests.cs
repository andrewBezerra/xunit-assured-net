using System;
using System.Collections.Generic;

using Shouldly;
using Xunit;

using XUnitAssured.Http.Extensions;
using XUnitAssured.Http.Results;

namespace XUnitAssured.Tests.HttpTests;

[Trait("Category", "Http")]
[Trait("Component", "Results")]
/// <summary>
/// O caminho JSON pelo lado do HTTP.
///
/// <para>
/// O navegador de caminho JSON existia em três cópias, e só a do Kafka tinha teste. As três foram
/// consolidadas numa única no Core, e esta é a cobertura que faltava do outro lado: o ponto de
/// entrada do HTTP, que é justamente o que foi religado para a cópia compartilhada.
/// </para>
///
/// <para>
/// O que se verifica aqui é a navegação e a conversão, não o transporte: o resultado é montado à
/// mão, sem API no ar.
/// </para>
/// </summary>
public class HttpJsonPathTests
{
	[Fact(DisplayName = "JsonPath should read a string property from the response body")]
	public void JsonPath_Should_Read_A_String_Property()
	{
		var resultado = Resposta(@"{""name"": ""Laptop"", ""price"": 999.90}");

		resultado.JsonPath<string>("$.name").ShouldBe("Laptop");
	}

	[Fact(DisplayName = "JsonPath should convert to the requested numeric type")]
	public void JsonPath_Should_Convert_To_The_Requested_Numeric_Type()
	{
		var resultado = Resposta(@"{""id"": 42, ""price"": 999.90}");

		resultado.JsonPath<int>("$.id").ShouldBe(42);
		resultado.JsonPath<decimal>("$.price").ShouldBe(999.90m);
	}

	[Fact(DisplayName = "JsonPath should walk into a nested object")]
	public void JsonPath_Should_Walk_Into_A_Nested_Object()
	{
		var resultado = Resposta(@"{""customer"": {""address"": {""city"": ""Recife""}}}");

		resultado.JsonPath<string>("$.customer.address.city").ShouldBe("Recife");
	}

	[Fact(DisplayName = "JsonPath should index into an array")]
	public void JsonPath_Should_Index_Into_An_Array()
	{
		var resultado = Resposta(@"{""items"": [{""sku"": ""A1""}, {""sku"": ""B2""}]}");

		resultado.JsonPath<string>("$.items[1].sku").ShouldBe("B2");
	}

	// O prefixo é opcional, e as duas formas têm de dar no mesmo lugar.
	[Fact(DisplayName = "JsonPath should accept a path with or without the dollar prefix")]
	public void JsonPath_Should_Accept_Either_Prefix_Form()
	{
		var resultado = Resposta(@"{""name"": ""Laptop""}");

		resultado.JsonPath<string>("$.name").ShouldBe(resultado.JsonPath<string>("name"));
	}

	// Um caminho que não existe é erro de quem escreveu o teste, e tem de aparecer como tal em
	// vez de devolver o valor padrão em silêncio.
	[Fact(DisplayName = "A path that does not exist should fail rather than return a default")]
	public void A_Path_That_Does_Not_Exist_Should_Fail()
	{
		var resultado = Resposta(@"{""name"": ""Laptop""}");

		Should.Throw<KeyNotFoundException>(() => resultado.JsonPath<string>("$.missing"));
	}

	[Fact(DisplayName = "An empty response body should say so")]
	public void An_Empty_Response_Body_Should_Say_So()
	{
		var erro = Should.Throw<InvalidOperationException>(
			() => Resposta("").JsonPath<string>("$.name"));

		erro.Message.ShouldContain("empty");
	}

	private static HttpStepResult Resposta(string corpo) =>
		HttpStepResult.CreateHttpSuccess(statusCode: 200, responseBody: corpo);
}
