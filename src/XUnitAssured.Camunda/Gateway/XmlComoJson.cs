using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace XUnitAssured.Camunda.Gateway;

/// <summary>
/// Contorna um defeito do SDK 10.0.1: o <c>GetProcessDefinitionXmlAsync</c> lê como JSON a resposta
/// de <c>/process-definitions/{key}/xml</c>, que é XML, e lança <c>JsonException</c>.
/// </summary>
/// <remarks>
/// Embaixo do SDK, este handler embrulha o XML dessa rota numa string JSON; o SDK a lê como string,
/// e a autenticação continua sendo dele. As outras respostas passam intactas. Sai quando o SDK
/// corrigir o método.
/// </remarks>
internal sealed class XmlComoJson : DelegatingHandler
{
	public XmlComoJson() : base(new SocketsHttpHandler()) { }

	internal XmlComoJson(HttpMessageHandler interno) : base(interno) { }

	protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
	{
		var resposta = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

		var rota = request.RequestUri?.AbsolutePath ?? string.Empty;
		var tipo = resposta.Content.Headers.ContentType?.MediaType ?? string.Empty;
		if (!resposta.IsSuccessStatusCode || !rota.EndsWith("/xml") || !tipo.Contains("xml"))
			return resposta;

		var xml = await resposta.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
		var json = new StringContent(JsonSerializer.Serialize(xml), Encoding.UTF8);
		json.Headers.ContentType = new MediaTypeHeaderValue("application/json");
		resposta.Content = json;
		return resposta;
	}
}
