using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;

namespace XUnitAssured.Http.Steps;

/// <summary>
/// Um corpo que vai como foi escrito, sem passar pelo serializador: o JSON malformado de propósito,
/// o XML, o texto que a API tem de recusar.
/// </summary>
/// <remarks>
/// Guarda o texto, e não o HttpContent, para que cada envio tenha o seu: um conteúdo é lido ao ser
/// enviado, e o mesmo passo pode sair mais de uma vez (Concurrently).
/// </remarks>
internal sealed record CorpoCru(string Conteudo, string TipoDeConteudo)
{
	public HttpContent Criar()
	{
		var conteudo = new StringContent(Conteudo, Encoding.UTF8);
		// O tipo vai como veio: "application/json; charset=utf-8" é aceito, e o StringContent
		// recusaria qualquer coisa além do media type no construtor.
		conteudo.Headers.ContentType = MediaTypeHeaderValue.Parse(TipoDeConteudo);
		return conteudo;
	}
}
