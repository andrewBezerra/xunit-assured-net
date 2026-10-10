using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace XUnitAssured.Http.Results;

/// <summary>
/// The request as it went out: method, address, headers, cookies and body, read after the client
/// sent it.
/// </summary>
/// <remarks>
/// <para>
/// Read after sending, not from what the step was told to send, because the client adds to it on
/// the way: a client that keeps cookies puts the session in a <c>Cookie</c> header, and a handler
/// may add a correlation id. A session test is about exactly that — whether the revoked cookie is
/// still sent.
/// </para>
/// </remarks>
public sealed class SentRequest
{
	private SentRequest(
		string method,
		Uri? url,
		IReadOnlyDictionary<string, IReadOnlyList<string>> headers,
		IReadOnlyDictionary<string, string> cookies,
		string? body)
	{
		Method = method;
		Url = url;
		Headers = headers;
		Cookies = cookies;
		Body = body;
	}

	/// <summary>The HTTP method, e.g. "POST".</summary>
	public string Method { get; }

	/// <summary>The full address, query string included.</summary>
	public Uri? Url { get; }

	/// <summary>
	/// Every header sent, the content's (<c>Content-Type</c>) included. Names are matched
	/// case-insensitively, as HTTP requires.
	/// </summary>
	public IReadOnlyDictionary<string, IReadOnlyList<string>> Headers { get; }

	/// <summary>The cookies sent in the <c>Cookie</c> header, by name.</summary>
	public IReadOnlyDictionary<string, string> Cookies { get; }

	/// <summary>The body as text, or null when there was none or it could not be read again.</summary>
	public string? Body { get; }

	/// <summary>The values of a header, or an empty list when it was not sent.</summary>
	/// <param name="name">The header name, matched case-insensitively</param>
	/// <returns>Its values, in the order they were sent</returns>
	public IReadOnlyList<string> Header(string name) =>
		Headers.TryGetValue(name, out var valores) ? valores : Array.Empty<string>();

	/// <summary>The value of a cookie sent, or null when it was not sent.</summary>
	/// <param name="name">The cookie name, matched exactly</param>
	/// <returns>Its value, or null</returns>
	public string? Cookie(string name) => Cookies.TryGetValue(name, out var valor) ? valor : null;

	/// <inheritdoc />
	public override string ToString() => $"{Method} {Url}";

	/// <summary>Reads a request that has already been sent.</summary>
	internal static async Task<SentRequest> FromAsync(HttpRequestMessage pedido)
	{
		var cabecalhos = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
		var todos = pedido.Content == null
			? pedido.Headers
			: pedido.Headers.Concat(pedido.Content.Headers);
		foreach (var cabecalho in todos)
		{
			var anteriores = cabecalhos.TryGetValue(cabecalho.Key, out var lista) ? lista : Array.Empty<string>();
			cabecalhos[cabecalho.Key] = anteriores.Concat(cabecalho.Value).ToList();
		}

		var cookies = new Dictionary<string, string>(StringComparer.Ordinal);
		if (cabecalhos.TryGetValue("Cookie", out var linhas))
		{
			foreach (var par in linhas.SelectMany(l => l.Split(';')))
			{
				var igual = par.IndexOf('=');
				if (igual > 0)
					cookies[par.Substring(0, igual).Trim()] = par.Substring(igual + 1).Trim();
			}
		}

		return new SentRequest(pedido.Method.Method, pedido.RequestUri, cabecalhos, cookies, await LerCorpoAsync(pedido.Content));
	}

	// Só o conteúdo em memória pode ser lido de novo: um corpo em stream já foi consumido no envio,
	// e tentar lê-lo trocaria a resposta do teste por uma exceção sobre o diagnóstico.
	private static async Task<string?> LerCorpoAsync(HttpContent? conteudo)
	{
		if (conteudo is not ByteArrayContent)
			return null;

		try
		{
			return await conteudo.ReadAsStringAsync().ConfigureAwait(false);
		}
		catch (ObjectDisposedException)
		{
			return null;
		}
	}
}
