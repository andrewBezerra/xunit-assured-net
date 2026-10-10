using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Shouldly;

namespace XUnitAssured.Http.Testing;

/// <summary>
/// One request the system under test sent to an outside service.
/// </summary>
/// <param name="Method">The HTTP method</param>
/// <param name="Url">The full URL</param>
/// <param name="Headers">Request and content headers, by name (case-insensitive), values joined by ", "</param>
/// <param name="Body">The raw body; empty when there was none</param>
public sealed record CapturedRequest(
	HttpMethod Method,
	Uri Url,
	IReadOnlyDictionary<string, string> Headers,
	byte[] Body)
{
	/// <summary>The body as UTF-8 text.</summary>
	public string BodyText => Encoding.UTF8.GetString(Body);
}

/// <summary>
/// Stands in for an outside HTTP service the system under test calls — a payment gateway, a
/// push service, an error tracker — answering what the test configures and recording every
/// request, so a test can assert what the system sent.
/// </summary>
/// <remarks>
/// <para>
/// The double sits at the HTTP boundary on purpose. Replacing the component that calls the
/// service would hide the very logic under test — which recipients are picked, what goes in
/// the body; here everything up to the request itself runs for real.
/// </para>
/// <para>
/// Plug it in as the handler of the <see cref="HttpClient"/> the component uses, e.g. in
/// <c>ConfigureTestServices</c>:
/// </para>
/// <code>
/// var gateway = new OutboundHttpCapture().RespondWith(HttpStatusCode.Created);
///
/// services.AddSingleton(new PaymentClient(new HttpClient(gateway)));
/// // or, for a named or typed client:
/// services.AddHttpClient("payments").ConfigurePrimaryHttpMessageHandler(() =&gt; gateway);
/// </code>
/// <para>
/// It keeps working after a client that owns it is disposed, so <c>IHttpClientFactory</c>
/// recycling its handlers does not break the double halfway through a test.
/// </para>
/// </remarks>
public sealed class OutboundHttpCapture : HttpMessageHandler
{
	private readonly List<CapturedRequest> _pedidos = [];
	private readonly List<(Regex Padrao, Func<HttpRequestMessage, HttpResponseMessage> Resposta)> _rotas = [];
	private readonly object _trava = new();
	private Func<HttpRequestMessage, HttpResponseMessage> _padrao = _ => new HttpResponseMessage(HttpStatusCode.OK);

	/// <summary>Every request received so far, in order.</summary>
	public IReadOnlyList<CapturedRequest> Requests
	{
		get { lock (_trava) return [.. _pedidos]; }
	}

	/// <summary>
	/// The response for any request no <see cref="When(string, Func{HttpRequestMessage, HttpResponseMessage})"/>
	/// matches. Without it, every request gets 200 OK with no body.
	/// </summary>
	/// <param name="status">The status code</param>
	/// <param name="body">The body, sent as application/json; null for none</param>
	/// <returns>The capture, for chaining</returns>
	public OutboundHttpCapture RespondWith(HttpStatusCode status, string? body = null)
	{
		_padrao = _ => Resposta(status, body);
		return this;
	}

	/// <summary>
	/// The response for requests whose URL matches <paramref name="urlPattern"/>, where <c>*</c>
	/// stands for any run of characters (e.g. <c>"https://push.example/*"</c>). The first
	/// matching rule wins.
	/// </summary>
	/// <param name="urlPattern">URL pattern with <c>*</c> wildcards</param>
	/// <param name="respond">Builds the response from the request</param>
	/// <returns>The capture, for chaining</returns>
	public OutboundHttpCapture When(string urlPattern, Func<HttpRequestMessage, HttpResponseMessage> respond)
	{
		if (respond == null) throw new ArgumentNullException(nameof(respond));

		lock (_trava) _rotas.Add((Padrao(urlPattern), respond));
		return this;
	}

	/// <summary>Shorthand for <see cref="When(string, Func{HttpRequestMessage, HttpResponseMessage})"/> with a fixed response.</summary>
	public OutboundHttpCapture When(string urlPattern, HttpStatusCode status, string? body = null) =>
		When(urlPattern, _ => Resposta(status, body));

	/// <summary>The requests whose URL matches <paramref name="urlPattern"/> (<c>*</c> = any run of characters).</summary>
	public IReadOnlyList<CapturedRequest> SentTo(string urlPattern)
	{
		var padrao = Padrao(urlPattern);
		return Requests.Where(p => padrao.IsMatch(p.Url.ToString())).ToList();
	}

	/// <summary>
	/// Asserts that requests matching the pattern were sent — at least once, or exactly
	/// <paramref name="times"/> times. The failure lists every URL that was sent.
	/// </summary>
	/// <param name="urlPattern">URL pattern with <c>*</c> wildcards</param>
	/// <param name="times">The exact number expected; null for "at least once"</param>
	/// <param name="predicate">Extra condition on each request, e.g. on the body</param>
	/// <returns>The capture, for chaining</returns>
	public OutboundHttpCapture AssertSent(string urlPattern, int? times = null, Func<CapturedRequest, bool>? predicate = null)
	{
		var enviados = SentTo(urlPattern).Where(p => predicate == null || predicate(p)).ToList();
		var contexto = $"Sent: {Descrever()}";

		if (times is { } vezes)
			enviados.Count.ShouldBe(vezes, $"Expected {vezes} request(s) to {urlPattern}, but there were {enviados.Count}. {contexto}");
		else
			enviados.ShouldNotBeEmpty($"Expected a request to {urlPattern}. {contexto}");

		return this;
	}

	/// <summary>Asserts that no request matching the pattern was sent.</summary>
	/// <param name="urlPattern">URL pattern with <c>*</c> wildcards</param>
	/// <returns>The capture, for chaining</returns>
	public OutboundHttpCapture AssertNotSent(string urlPattern)
	{
		var enviados = SentTo(urlPattern);
		enviados.ShouldBeEmpty($"Expected no request to {urlPattern}, but there were {enviados.Count}. Sent: {Descrever()}");
		return this;
	}

	/// <summary>Forgets the captured requests; the configured responses stay.</summary>
	public void Clear()
	{
		lock (_trava) _pedidos.Clear();
	}

	/// <inheritdoc />
	protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
	{
		var corpo = request.Content == null
			? []
			: await request.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);

		var cabecalhos = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		foreach (var h in request.Headers.Concat(request.Content?.Headers ?? Enumerable.Empty<KeyValuePair<string, IEnumerable<string>>>()))
			cabecalhos[h.Key] = string.Join(", ", h.Value);

		Func<HttpRequestMessage, HttpResponseMessage> responder;
		lock (_trava)
		{
			_pedidos.Add(new CapturedRequest(request.Method, request.RequestUri!, cabecalhos, corpo));

			var url = request.RequestUri!.ToString();
			responder = _rotas.FirstOrDefault(r => r.Padrao.IsMatch(url)).Resposta ?? _padrao;
		}

		var resposta = responder(request);
		resposta.RequestMessage ??= request;
		return resposta;
	}

	private string Descrever()
	{
		var pedidos = Requests;
		return pedidos.Count == 0 ? "nothing" : string.Join(" | ", pedidos.Select(p => $"{p.Method} {p.Url}"));
	}

	private static HttpResponseMessage Resposta(HttpStatusCode status, string? corpo) =>
		corpo == null
			? new HttpResponseMessage(status)
			: new HttpResponseMessage(status) { Content = new StringContent(corpo, Encoding.UTF8, "application/json") };

	private static Regex Padrao(string padrao)
	{
		if (string.IsNullOrWhiteSpace(padrao)) throw new ArgumentException("URL pattern cannot be empty.", nameof(padrao));
		return new Regex("^" + string.Join(".*", padrao.Split('*').Select(Regex.Escape)) + "$", RegexOptions.CultureInvariant);
	}
}
