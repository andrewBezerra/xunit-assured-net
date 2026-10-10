using System;
using System.Text.Json;

using XUnitAssured.Core.Extensions;

namespace XUnitAssured.Playwright.Results;

/// <summary>
/// A request made from inside the page by <c>FetchFromPage</c>, and what came back.
/// </summary>
/// <remarks>
/// It left from the page's origin, with the page's cookies, so the browser applied everything a
/// test process cannot: SameSite, the cookie <c>Path</c>, and CORS with credentials.
/// </remarks>
/// <param name="Url">The address requested</param>
/// <param name="Method">The HTTP method</param>
/// <param name="Status">The status code; 0 when the browser got no response (e.g. blocked by CORS)</param>
/// <param name="Body">The response body as text</param>
public sealed record PageFetch(string Url, string Method, int Status, string Body)
{
	/// <summary>
	/// Reads a value from the JSON body, with the same paths as the HTTP package:
	/// <c>$.accessToken</c>, <c>$.items[0].id</c>, <c>$[0]</c>.
	/// </summary>
	/// <typeparam name="T">The type to read the value as</typeparam>
	/// <param name="path">The JSON path</param>
	/// <returns>The value at the path</returns>
	/// <exception cref="InvalidOperationException">The body is empty.</exception>
	public T JsonPath<T>(string path)
	{
		if (string.IsNullOrWhiteSpace(Body))
			throw new InvalidOperationException($"The response to {Method} {Url} ({Status}) has an empty body.");

		using var documento = JsonDocument.Parse(Body);
		return JsonPathNavigator.Navigate<T>(documento.RootElement, path);
	}
}
