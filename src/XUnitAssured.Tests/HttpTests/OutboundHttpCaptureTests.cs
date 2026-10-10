using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;
using Xunit;

using XUnitAssured.Http.Testing;

namespace XUnitAssured.Tests.HttpTests;

/// <summary>
/// O dublê de um serviço HTTP de fora.
///
/// <para>
/// Numa suíte real, dois testes precisaram de um no limite do HTTP: o transporte de um rastreador
/// de erros, para contar os alertas que de fato sairiam, e um serviço de push, para contar as
/// entregas por aparelho. Trocar o componente que chama o serviço esconderia a lógica sob teste
/// -- quem recebe, o que vai no corpo.
/// </para>
/// </summary>
[Trait("Category", "Http")]
[Trait("Component", "OutboundCapture")]
public class OutboundHttpCaptureTests
{
	[Fact(DisplayName = "A request is captured with its method, URL, headers and body")]
	public async Task Captures_The_Request()
	{
		var servico = new OutboundHttpCapture();
		using var cliente = new HttpClient(servico, disposeHandler: false);

		var pedido = new HttpRequestMessage(HttpMethod.Post, "https://push.example/devices/abc")
		{
			Content = new StringContent("""{"title":"Novo plantão"}""", Encoding.UTF8, "application/json")
		};
		pedido.Headers.Add("TTL", "60");
		var resposta = await cliente.SendAsync(pedido);

		resposta.StatusCode.ShouldBe(HttpStatusCode.OK);
		var capturado = servico.Requests.ShouldHaveSingleItem();
		capturado.Method.ShouldBe(HttpMethod.Post);
		capturado.Url.ToString().ShouldBe("https://push.example/devices/abc");
		capturado.Headers["ttl"].ShouldBe("60");
		capturado.Headers["Content-Type"].ShouldStartWith("application/json");
		capturado.BodyText.ShouldContain("Novo plantão");
	}

	[Fact(DisplayName = "Responses come from the first matching rule, then the default")]
	public async Task Responses_By_Rule()
	{
		var servico = new OutboundHttpCapture()
			.RespondWith(HttpStatusCode.Created)
			.When("https://push.example/devices/morto*", HttpStatusCode.Gone)
			.When("https://push.example/*", _ => new HttpResponseMessage(HttpStatusCode.Accepted));
		using var cliente = new HttpClient(servico, disposeHandler: false);

		(await cliente.PostAsync("https://push.example/devices/morto-1", null)).StatusCode.ShouldBe(HttpStatusCode.Gone);
		(await cliente.PostAsync("https://push.example/devices/vivo", null)).StatusCode.ShouldBe(HttpStatusCode.Accepted);
		(await cliente.PostAsync("https://outro.example/x", null)).StatusCode.ShouldBe(HttpStatusCode.Created);
	}

	[Fact(DisplayName = "AssertSent counts by pattern and condition; AssertNotSent fails and lists what went out")]
	public async Task Assert_Sent_And_Not_Sent()
	{
		var servico = new OutboundHttpCapture();
		using var cliente = new HttpClient(servico, disposeHandler: false);
		await cliente.PostAsync("https://push.example/devices/a", new StringContent("pt"));
		await cliente.PostAsync("https://push.example/devices/b", new StringContent("es"));

		servico.AssertSent("https://push.example/devices/*", times: 2);
		servico.AssertSent("https://push.example/*", times: 1, predicate: p => p.BodyText == "es");
		servico.AssertNotSent("https://push.example/devices/c");

		var erro = Should.Throw<ShouldAssertException>(() => servico.AssertNotSent("*/devices/a"));
		erro.Message.ShouldContain("POST https://push.example/devices/a");

		Should.Throw<ShouldAssertException>(() => servico.AssertSent("https://push.example/*", times: 1));
	}

	// O IHttpClientFactory descarta os handlers quando os recicla. O dublê precisa continuar
	// recebendo e guardando. Medido: vale sem nada especial no Dispose -- o handler base não
	// deixa de atender depois de descartado --, e este teste é o que garante que continue assim.
	[Fact(DisplayName = "Disposing the client does not break the double, so IHttpClientFactory can recycle it")]
	public async Task Survives_Disposal()
	{
		var servico = new OutboundHttpCapture();

		using (var primeiro = new HttpClient(servico, disposeHandler: true))
			await primeiro.GetAsync("https://api.example/um");

		using var segundo = new HttpClient(servico, disposeHandler: true);
		(await segundo.GetAsync("https://api.example/dois")).StatusCode.ShouldBe(HttpStatusCode.OK);

		servico.Requests.Count.ShouldBe(2);
	}

	[Fact(DisplayName = "It works as the primary handler of a named client from IHttpClientFactory")]
	public async Task Works_With_Http_Client_Factory()
	{
		var servico = new OutboundHttpCapture().RespondWith(HttpStatusCode.Created, """{"id":"p-1"}""");

		var provedor = new ServiceCollection()
			.AddHttpClient("pagamentos")
			.ConfigurePrimaryHttpMessageHandler(() => servico)
			.Services.BuildServiceProvider();

		var cliente = provedor.GetRequiredService<IHttpClientFactory>().CreateClient("pagamentos");
		var resposta = await cliente.PostAsync("https://pagamentos.example/cobrancas", new StringContent("{}"));

		resposta.StatusCode.ShouldBe(HttpStatusCode.Created);
		(await resposta.Content.ReadAsStringAsync()).ShouldContain("p-1");
		servico.AssertSent("https://pagamentos.example/cobrancas", times: 1);
	}

	[Fact(DisplayName = "Clear forgets requests but keeps the configured responses")]
	public async Task Clear_Keeps_Responses()
	{
		var servico = new OutboundHttpCapture().RespondWith(HttpStatusCode.NoContent);
		using var cliente = new HttpClient(servico, disposeHandler: false);
		await cliente.GetAsync("https://api.example/antes");

		servico.Clear();
		(await cliente.GetAsync("https://api.example/depois")).StatusCode.ShouldBe(HttpStatusCode.NoContent);

		servico.Requests.ShouldHaveSingleItem().Url.AbsolutePath.ShouldBe("/depois");
	}
}
