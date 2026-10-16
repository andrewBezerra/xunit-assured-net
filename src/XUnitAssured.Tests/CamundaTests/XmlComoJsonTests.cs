using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

using XUnitAssured.Camunda.Gateway;

namespace XUnitAssured.Tests.CamundaTests;

/// <summary>
/// O contorno do defeito do SDK 10.0.1, que lê como JSON o XML de <c>/process-definitions/{key}/xml</c>.
/// </summary>
[Trait("Category", "Camunda")]
[Trait("Component", "Gateway")]
public class XmlComoJsonTests
{
	[Fact(DisplayName = "The process XML becomes a JSON string the SDK can read, with the XML intact inside")]
	public async Task Xml_Becomes_A_Json_String()
	{
		const string xml = "<?xml version=\"1.0\"?><bpmn:definitions xmlns:bpmn=\"x\">\"aspas\"</bpmn:definitions>";
		using var cliente = Cliente(xml, "text/xml");

		var resposta = await cliente.GetAsync("http://camunda/v2/process-definitions/123/xml");

		resposta.Content.Headers.ContentType!.MediaType.ShouldBe("application/json");
		JsonSerializer.Deserialize<string>(await resposta.Content.ReadAsStringAsync()).ShouldBe(xml);
	}

	[Fact(DisplayName = "Every other response passes through untouched")]
	public async Task Other_Responses_Pass_Through()
	{
		using var cliente = Cliente("{\"items\":[]}", "application/json");

		var resposta = await cliente.GetAsync("http://camunda/v2/process-instances/search");

		(await resposta.Content.ReadAsStringAsync()).ShouldBe("{\"items\":[]}");
	}

	private static HttpClient Cliente(string corpo, string tipo) =>
		new(new XmlComoJson(new Responde(corpo, tipo)));

	private sealed class Responde(string corpo, string tipo) : HttpMessageHandler
	{
		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
			Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(corpo, Encoding.UTF8, tipo) });
	}
}
