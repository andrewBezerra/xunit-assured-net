using System;
using System.Collections.Generic;

using Shouldly;
using Xunit;

using XUnitAssured.RabbitMq.Results;

namespace XUnitAssured.Tests.RabbitMqTests;

[Trait("Category", "RabbitMq")]
[Trait("Component", "Results")]
/// <summary>
/// Como se lê a mensagem consumida.
///
/// <para>
/// O caminho JSON usa o navegador que mora no Core, o mesmo dos pacotes Http e Kafka. Era copiado
/// três vezes no repositório antes de subir; esta cobertura é o terceiro lado a exercitar a cópia
/// única, e é o que torna o ganho real em vez de arquitetural.
/// </para>
/// </summary>
public class MensagemDoRabbitMqTests
{
	private sealed class Pedido
	{
		public int Id { get; set; }

		public string Status { get; set; } = string.Empty;
	}

	[Fact(DisplayName = "GetMessage should deserialize the consumed body")]
	public void GetMessage_Should_Deserialize_The_Body()
	{
		var resultado = Consumida(@"{""id"": 42, ""status"": ""Created""}");

		var pedido = resultado.GetMessage<Pedido>();

		pedido.ShouldNotBeNull();
		pedido!.Id.ShouldBe(42);
		pedido.Status.ShouldBe("Created");
	}

	// O corpo costuma vir com a primeira letra minúscula do JSON e a propriedade em C# começa
	// maiúscula; ignorar a caixa é o que faz o caso comum funcionar sem configuração.
	[Fact(DisplayName = "GetMessage should match property names regardless of case")]
	public void GetMessage_Should_Ignore_Case()
	{
		var resultado = Consumida(@"{""Id"": 7, ""STATUS"": ""Shipped""}");

		resultado.GetMessage<Pedido>()!.Id.ShouldBe(7);
	}

	[Fact(DisplayName = "GetMessage of string should hand back the raw body")]
	public void GetMessage_Of_String_Should_Hand_Back_The_Raw_Body()
	{
		var resultado = Consumida("mensagem solta, não é JSON");

		resultado.GetMessage<string>().ShouldBe("mensagem solta, não é JSON");
	}

	[Fact(DisplayName = "JsonPath should read a value from the consumed message")]
	public void JsonPath_Should_Read_A_Value()
	{
		var resultado = Consumida(@"{""id"": 42, ""status"": ""Created""}");

		resultado.JsonPath<int>("$.id").ShouldBe(42);
		resultado.JsonPath<string>("$.status").ShouldBe("Created");
	}

	[Fact(DisplayName = "JsonPath should walk into a nested object and an array")]
	public void JsonPath_Should_Walk_Nested_And_Indexed()
	{
		var resultado = Consumida(
			@"{""cliente"": {""endereco"": {""cidade"": ""Recife""}}, ""itens"": [{""sku"": ""A1""}, {""sku"": ""B2""}]}");

		resultado.JsonPath<string>("$.cliente.endereco.cidade").ShouldBe("Recife");
		resultado.JsonPath<string>("$.itens[1].sku").ShouldBe("B2");
	}

	[Fact(DisplayName = "JsonPath should accept a path with or without the dollar prefix")]
	public void JsonPath_Should_Accept_Either_Prefix_Form()
	{
		var resultado = Consumida(@"{""status"": ""Created""}");

		resultado.JsonPath<string>("$.status").ShouldBe(resultado.JsonPath<string>("status"));
	}

	// Ler de um resultado que não tem mensagem é erro de quem escreveu o teste, e tem de aparecer
	// como tal. Um passo que publicou não tem mensagem, e um consumo que esgotou o prazo também
	// não.
	[Fact(DisplayName = "Reading from a publish result should say there is no message")]
	public void Reading_From_A_Publish_Result_Should_Say_So()
	{
		var publicacao = RabbitMqStepResult.CreatePublishSuccess("pedidos", "pedidos");

		Should.Throw<InvalidOperationException>(() => publicacao.JsonPath<int>("$.id"));
		Should.Throw<InvalidOperationException>(() => publicacao.GetMessage<Pedido>());
	}

	[Fact(DisplayName = "The consumed message should carry its routing key and headers")]
	public void The_Consumed_Message_Should_Carry_Its_Envelope()
	{
		var resultado = RabbitMqStepResult.CreateConsumeSuccess(
			destination: "pedidos",
			routingKey: "pedidos.criado",
			message: "{}",
			remainingMessageCount: 0,
			headers: new Dictionary<string, object?> { ["origem"] = "checkout" });

		resultado.RoutingKey.ShouldBe("pedidos.criado");
		resultado.Headers.ContainsKey("origem").ShouldBeTrue();
		resultado.RemainingMessageCount.ShouldBe(0u);
	}

	private static RabbitMqStepResult Consumida(string corpo) =>
		RabbitMqStepResult.CreateConsumeSuccess("pedidos", "pedidos", corpo, remainingMessageCount: 0);
}
