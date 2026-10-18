using System;
using System.Collections.Generic;
using System.Text;

using Confluent.Kafka;

using Shouldly;
using Xunit;

using XUnitAssured.Http.Results;
using XUnitAssured.Kafka.Results;
using XUnitAssured.RabbitMq.Results;

namespace XUnitAssured.Tests.JsonataTests;

[Trait("Category", "Jsonata")]
[Trait("Component", "Result")]
/// <summary>
/// O <c>ToJsonObject()</c> de cada protocolo: o documento que uma expressão lê, com <c>body</c> nos três.
/// </summary>
public class ResultadoComoJsonTests
{
	[Fact(DisplayName = "An HTTP result should become status, lowercase headers and the JSON body")]
	public void Http()
	{
		var resultado = HttpStepResult.CreateHttpSuccess(201, """{"id": 7, "nome": "Caneta"}""",
			new Dictionary<string, IEnumerable<string>>
			{
				["Content-Type"] = ["application/json"],
				["X-Correlation-Id"] = ["c-1"],
				["Set-Cookie"] = ["a=1", "b=2"],
			});

		resultado.ToJsonObject().ToJsonString().ShouldBe(
			"""{"status":201,"headers":{"content-type":"application/json","x-correlation-id":"c-1","set-cookie":"a=1, b=2"},"body":{"id":7,"nome":"Caneta"}}""");
	}

	[Theory(DisplayName = "A body that is not JSON should be its text, and no body should be null")]
	[InlineData("pronto", "\"pronto\"")]
	[InlineData("", "null")]
	[InlineData(null, "null")]
	[InlineData("42", "42")]
	public void CorpoQueNaoEhJson(string? corpo, string esperado)
	{
		HttpStepResult.CreateHttpSuccess(200, corpo).ToJsonObject()["body"]?.ToJsonString().ShouldBe(esperado == "null" ? null : esperado);
	}

	[Fact(DisplayName = "A consumed Kafka message should become its metadata, UTF-8 headers and the JSON body")]
	public void Kafka()
	{
		var headers = new Headers { { "tipo", Encoding.UTF8.GetBytes("PedidoCriado") } };
		var resultado = new KafkaStepResult
		{
			Success = true,
			Data = """{"pedidoId": 987}""",
			Properties = new Dictionary<string, object?>
			{
				["Topic"] = "pedidos",
				["Partition"] = 2,
				["Offset"] = 41L,
				["Timestamp"] = new DateTime(2026, 10, 19, 12, 0, 0, 5, DateTimeKind.Utc),
				["Key"] = "987",
				["Headers"] = headers,
			}
		};

		resultado.ToJsonObject().ToJsonString().ShouldBe(
			"""{"topic":"pedidos","partition":2,"offset":41,"timestamp":"2026-10-19T12:00:00.005Z","key":"987","headers":{"tipo":"PedidoCriado"},"body":{"pedidoId":987},"status":null}""");
	}

	[Fact(DisplayName = "A Kafka produce result should carry the persistence status")]
	public void KafkaAoProduzir()
	{
		var resultado = new KafkaStepResult
		{
			Success = true,
			Data = "texto simples",
			Properties = new Dictionary<string, object?> { ["Topic"] = "pedidos", ["Status"] = "Persisted" }
		};

		var json = resultado.ToJsonObject();
		json["status"]!.GetValue<string>().ShouldBe("Persisted");
		json["body"]!.GetValue<string>().ShouldBe("texto simples");
		json["headers"]!.ToJsonString().ShouldBe("{}");
	}

	[Fact(DisplayName = "A consumed RabbitMQ message should become its routing, text headers and the JSON body")]
	public void RabbitMq()
	{
		var resultado = new RabbitMqStepResult
		{
			Success = true,
			Properties = new Dictionary<string, object?>
			{
				["Destination"] = "qa-1",
				["RoutingKey"] = "pedido.criado",
				["Message"] = """{"pedidoId": 987}""",
				["Headers"] = new Dictionary<string, object?> { ["origem"] = Encoding.UTF8.GetBytes("loja"), ["tentativa"] = 2, ["vazio"] = null },
			}
		};

		resultado.ToJsonObject().ToJsonString().ShouldBe(
			"""{"destination":"qa-1","routingKey":"pedido.criado","headers":{"origem":"loja","tentativa":"2","vazio":null},"body":{"pedidoId":987}}""");
	}
}
