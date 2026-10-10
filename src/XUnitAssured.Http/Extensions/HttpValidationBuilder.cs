using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Shouldly;
using XUnitAssured.Core.Abstractions;
using XUnitAssured.Core.DSL;
using XUnitAssured.Core.Extensions;
using XUnitAssured.Core.Results;
using XUnitAssured.Http.Results;

namespace XUnitAssured.Http.Extensions;

/// <summary>
/// HTTP-specific validation builder that extends the generic ValidationBuilder.
/// Provides convenient methods for HTTP/REST API testing with fluent assertions.
/// </summary>
public class HttpValidationBuilder : ValidationBuilder<HttpStepResult>
{
	/// <summary>
	/// Initializes a new instance of the HttpValidationBuilder.
	/// </summary>
	/// <param name="scenario">The test scenario containing the executed HTTP step</param>
	public HttpValidationBuilder(ITestScenario scenario) : base(scenario)
	{
	}

	/// <summary>
	/// The one response the assertions below are about. Requests sent with <c>Concurrently</c>
	/// have one response each, and an assertion on "the" body or header of them would pick one at
	/// random — so it says where to go instead.
	/// </summary>
	private new HttpStepResult Result =>
		base.Result is ConcurrentHttpStepResult concorrente
			? throw new InvalidOperationException(
				$"The step sent {concorrente.Responses.Count} requests at once (Concurrently), so there is no " +
				"single response to assert on. Use AssertStatusCode for all of them, AssertEach(r => ...) for " +
				"any other assertion, or GetResult() and its Responses.")
			: base.Result;

	/// <summary>
	/// Marks the transition from "When" (action) to "Then" (assertions) in BDD-style tests.
	/// Returns HttpValidationBuilder to maintain fluent chain type.
	/// </summary>
	/// <returns>The same HTTP validation builder for method chaining</returns>
	public new HttpValidationBuilder Then()
	{
		base.Then();
		return this;
	}

	/// <summary>
	/// Asserts that the HTTP status code matches the expected value.
	/// </summary>
	/// <param name="expectedStatusCode">The expected HTTP status code (e.g., 200, 404, 500)</param>
	/// <returns>The same HTTP validation builder for method chaining</returns>
	/// <exception cref="ShouldAssertException">Thrown when status code doesn't match</exception>
	/// <example>
	/// <code>
	/// .Then()
	///     .AssertStatusCode(200)
	///     .AssertStatusCode(404);
	/// </code>
	/// </example>
	public HttpValidationBuilder AssertStatusCode(int expectedStatusCode)
	{
		if (base.Result is ConcurrentHttpStepResult concorrente)
		{
			// Cada resposta que fugiu do esperado diz por quê; as que acertaram só ocupariam espaço.
			var fora = concorrente.Responses
				.Select((r, i) => (Resposta: r, Posicao: i + 1))
				.Where(x => x.Resposta.StatusCode != expectedStatusCode)
				.Select(x => $"Request {x.Posicao} ({x.Resposta.StatusCode}): {x.Resposta.Explicacao()}");

			concorrente.StatusCodes.ShouldAllBe(codigo => codigo == expectedStatusCode,
				$"Expected every one of the {concorrente.Responses.Count} concurrent requests to get " +
				$"{expectedStatusCode}, but they got: {string.Join(", ", concorrente.StatusCodes)}" +
				Environment.NewLine + string.Join(Environment.NewLine, fora));
			return this;
		}

		Result.StatusCode.ShouldBe(expectedStatusCode,
			$"Expected HTTP status code {expectedStatusCode} but got {Result.StatusCode}. {Result.Explicacao()}");
		return this;
	}

	/// <summary>
	/// Runs the assertions on each response of requests sent with <c>Concurrently</c> — or on the
	/// one response of a single request. Every response is checked, and the failure lists each one
	/// that failed, by position.
	/// </summary>
	/// <param name="assertions">The assertions for one response</param>
	/// <returns>The same HTTP validation builder for method chaining</returns>
	/// <example>
	/// <code>
	/// .Then().AssertEach(r =&gt; r
	///     .AssertStatusCode(201)
	///     .AssertHeader("Location", l =&gt; l.StartsWith("/api/members/")));
	/// </code>
	/// </example>
	public HttpValidationBuilder AssertEach(Action<HttpValidationBuilder> assertions)
	{
		if (assertions == null)
			throw new ArgumentNullException(nameof(assertions));

		IReadOnlyList<HttpStepResult> respostas = base.Result is ConcurrentHttpStepResult concorrente
			? concorrente.Responses
			: new[] { base.Result };

		var falhas = new List<string>();
		for (var i = 0; i < respostas.Count; i++)
		{
			var cenario = new TestScenario();
			cenario.SetCurrentStep(new PassoJaExecutado(respostas[i]));

			try
			{
				assertions(new HttpValidationBuilder(cenario));
			}
			catch (ShouldAssertException ex)
			{
				falhas.Add($"Request {i + 1} of {respostas.Count} (status {respostas[i].StatusCode}): {ex.Message}");
			}
		}

		if (falhas.Count > 0)
			throw new ShouldAssertException(string.Join(Environment.NewLine + Environment.NewLine, falhas));

		return this;
	}

	/// <summary>
	/// Validates that the JSON response matches the schema of the specified type T.
	/// Performs complete contract validation and detects breaking changes automatically.
	/// Delegates to ValidationBuilderExtensions.ValidateContract from XUnitAssured.Http.
	/// </summary>
	/// <typeparam name="T">The type to validate against (e.g., Product, User, ApiResponse)</typeparam>
	/// <returns>The same HTTP validation builder for method chaining</returns>
	/// <exception cref="InvalidOperationException">Thrown when response body is empty</exception>
	/// <exception cref="ShouldAssertException">Thrown when contract validation fails</exception>
	/// <example>
	/// <code>
	/// .Then()
	///     .ValidateContract&lt;Product&gt;()
	///     .AssertStatusCode(200);
	/// </code>
	/// </example>
	public HttpValidationBuilder ValidateContract<T>()
	{
		// Delegate to XUnitAssured.Http extension method
		return this.ValidateContract<HttpValidationBuilder, T>(Result);
	}

	/// <summary>
	/// Asserts a value extracted from the JSON response using a JSON path.
	/// Uses Shouldly for fluent assertions with clear error messages.
	/// Delegates to ValidationBuilderExtensions.AssertJsonPath from XUnitAssured.Http.
	/// </summary>
	/// <typeparam name="T">The expected type of the value (e.g., int, string, decimal)</typeparam>
	/// <param name="jsonPath">JSON path expression (e.g., "$.id", "$.items[0].price")</param>
	/// <param name="assertion">Action containing Shouldly assertions on the extracted value</param>
	/// <returns>The same HTTP validation builder for method chaining</returns>
	/// <example>
	/// <code>
	/// .Then()
	///     .AssertJsonPath&lt;int&gt;("$.id", id => id.ShouldBe(1))
	///     .AssertJsonPath&lt;string&gt;("$.name", name => name.ShouldNotBeNullOrEmpty())
	///     .AssertJsonPath&lt;decimal&gt;("$.price", price => 
	///     {
	///         price.ShouldBeGreaterThan(0);
	///         price.ShouldBeLessThan(10000);
	///     });
	/// </code>
	/// </example>
	public HttpValidationBuilder AssertJsonPath<T>(string jsonPath, Action<T> assertion)
	{
		// Delegate to XUnitAssured.Http extension method
		return this.AssertJsonPath<HttpValidationBuilder, T>(Result, jsonPath, assertion);
	}

	/// <summary>
	/// Legacy version of AssertJsonPath using Func&lt;T, bool&gt; for backward compatibility.
	/// Consider using the Action&lt;T&gt; overload with Shouldly assertions instead for clearer error messages.
	/// Delegates to ValidationBuilderExtensions.AssertJsonPath from XUnitAssured.Http.
	/// </summary>
	/// <typeparam name="T">The expected type of the value</typeparam>
	/// <param name="jsonPath">JSON path expression</param>
	/// <param name="predicate">Predicate function that returns true if validation passes</param>
	/// <param name="failureMessage">Custom failure message</param>
	/// <returns>The same HTTP validation builder for method chaining</returns>
	public HttpValidationBuilder AssertJsonPath<T>(string jsonPath, Func<T, bool> predicate, string? failureMessage = null)
	{
		// Delegate to XUnitAssured.Http extension method
		return this.AssertJsonPath<HttpValidationBuilder, T>(Result, jsonPath, predicate, failureMessage);
	}

	/// <summary>
	/// Asserts that the values a path selects include <paramref name="expected"/>.
	/// </summary>
	/// <remarks>
	/// Pair it with <see cref="AssertJsonPathNotContains{T}"/>: a filter is only tested when the
	/// test also proves it left something out.
	/// </remarks>
	/// <typeparam name="T">The type of each value</typeparam>
	/// <param name="jsonPath">A path with [*], e.g. "$[*].id" or "$.items[*].id"</param>
	/// <param name="expected">The value that must be among them</param>
	/// <returns>The same HTTP validation builder for method chaining</returns>
	/// <example>
	/// <code>
	/// .Then()
	///     .AssertJsonPathContains("$[*].id", matchingId)
	///     .AssertJsonPathNotContains("$[*].id", otherCityId);
	/// </code>
	/// </example>
	public HttpValidationBuilder AssertJsonPathContains<T>(string jsonPath, T expected) =>
		this.AssertJsonPathContains<HttpValidationBuilder, T>(Result, jsonPath, expected);

	/// <summary>
	/// Asserts that the values a path selects do not include <paramref name="unexpected"/> —
	/// what a filter, a permission or an isolation rule must have left out.
	/// </summary>
	/// <typeparam name="T">The type of each value</typeparam>
	/// <param name="jsonPath">A path with [*], e.g. "$[*].id"</param>
	/// <param name="unexpected">The value that must not be among them</param>
	/// <returns>The same HTTP validation builder for method chaining</returns>
	public HttpValidationBuilder AssertJsonPathNotContains<T>(string jsonPath, T unexpected) =>
		this.AssertJsonPathNotContains<HttpValidationBuilder, T>(Result, jsonPath, unexpected);

	/// <summary>
	/// Asserts how many items there are at a path: the values a [*] selects
	/// ("$.items[*]"), or the length of the array the path points to ("$.items", "$").
	/// </summary>
	/// <param name="jsonPath">A path to an array, or a path with [*]</param>
	/// <param name="expectedCount">The expected number of items</param>
	/// <returns>The same HTTP validation builder for method chaining</returns>
	public HttpValidationBuilder AssertJsonPathCount(string jsonPath, int expectedCount) =>
		this.AssertJsonPathCount<HttpValidationBuilder>(Result, jsonPath, expectedCount);

	/// <summary>
	/// Asserts that every value a path selects satisfies <paramref name="predicate"/>.
	/// Fails when the path selects nothing, since an empty list satisfies any condition.
	/// </summary>
	/// <typeparam name="T">The type of each value</typeparam>
	/// <param name="jsonPath">A path with [*], e.g. "$.items[*].city"</param>
	/// <param name="predicate">The condition every value must satisfy</param>
	/// <param name="failureMessage">Custom failure message</param>
	/// <returns>The same HTTP validation builder for method chaining</returns>
	public HttpValidationBuilder AssertJsonPathAll<T>(string jsonPath, Func<T, bool> predicate, string? failureMessage = null) =>
		this.AssertJsonPathAll<HttpValidationBuilder, T>(Result, jsonPath, predicate, failureMessage);

	/// <summary>
	/// Asserts that the response has a header with this exact value.
	/// The name is matched case-insensitively, as HTTP requires.
	/// </summary>
	/// <param name="name">The header name, e.g. "Location"</param>
	/// <param name="expectedValue">The value one of its entries must have</param>
	/// <returns>The same HTTP validation builder for method chaining</returns>
	public HttpValidationBuilder AssertHeader(string name, string expectedValue) =>
		AssertHeader(name, valor => valor == expectedValue, $"Expected header {name} to be '{expectedValue}'");

	/// <summary>
	/// Asserts that the response has a header with an entry that satisfies <paramref name="predicate"/>.
	/// </summary>
	/// <param name="name">The header name, matched case-insensitively</param>
	/// <param name="predicate">The condition one of its entries must satisfy</param>
	/// <param name="failureMessage">Custom failure message</param>
	/// <returns>The same HTTP validation builder for method chaining</returns>
	public HttpValidationBuilder AssertHeader(string name, Func<string, bool> predicate, string? failureMessage = null)
	{
		var valores = ValoresDoCabecalho(name);
		valores.ShouldNotBeEmpty($"Expected the response to have a {name} header, but it had: {NomesDosCabecalhos()}");
		valores.Any(predicate).ShouldBeTrue(
			$"{failureMessage ?? $"Header {name} did not satisfy the condition"}. Values: {string.Join(" | ", valores)}");
		return this;
	}

	/// <summary>
	/// Asserts that the response does not have the header at all.
	/// </summary>
	/// <param name="name">The header name, matched case-insensitively</param>
	/// <returns>The same HTTP validation builder for method chaining</returns>
	public HttpValidationBuilder AssertNoHeader(string name)
	{
		var valores = ValoresDoCabecalho(name);
		valores.ShouldBeEmpty($"Expected no {name} header, but it was there: {string.Join(" | ", valores)}");
		return this;
	}

	/// <summary>
	/// Asserts that the response sets the cookie, and optionally that its attributes satisfy
	/// <paramref name="predicate"/> — e.g. <c>c =&gt; c.HttpOnly &amp;&amp; c.Path == "/auth"</c>.
	/// </summary>
	/// <param name="name">The cookie name</param>
	/// <param name="predicate">The condition on the cookie's attributes; null to only require it</param>
	/// <param name="failureMessage">Custom failure message</param>
	/// <returns>The same HTTP validation builder for method chaining</returns>
	public HttpValidationBuilder AssertSetCookie(string name, Func<SetCookie, bool>? predicate = null, string? failureMessage = null)
	{
		var cookie = CookieDefinido(name);
		cookie.ShouldNotBeNull($"Expected the response to set cookie {name}, but it set: {NomesDosCookies()}");

		if (predicate != null)
			predicate(cookie).ShouldBeTrue(
				$"{failureMessage ?? $"Cookie {name} did not satisfy the condition"}. It was: {cookie}");

		return this;
	}

	/// <summary>
	/// Asserts that the response tells the client to forget the cookie: a <c>Set-Cookie</c> for
	/// it with an expiry in the past or a <c>Max-Age</c> of zero.
	/// </summary>
	/// <remarks>
	/// Checking that the header text mentions <c>expires=</c> is not the same thing: a date in
	/// the future also mentions it.
	/// </remarks>
	/// <param name="name">The cookie name</param>
	/// <returns>The same HTTP validation builder for method chaining</returns>
	public HttpValidationBuilder AssertCookieCleared(string name) =>
		AssertSetCookie(name, c => c.IsExpired, $"Expected cookie {name} to be cleared (expiry in the past or Max-Age=0)");

	/// <summary>
	/// Asserts that the request was sent with a header of this exact value — read after the client
	/// sent it, so it includes what the client added on the way.
	/// </summary>
	/// <param name="name">The header name, matched case-insensitively</param>
	/// <param name="expectedValue">The value one of its entries must have</param>
	/// <returns>The same HTTP validation builder for method chaining</returns>
	public HttpValidationBuilder AssertRequestHeader(string name, string expectedValue) =>
		AssertRequestHeader(name, valor => valor == expectedValue, $"Expected request header {name} to be '{expectedValue}'");

	/// <summary>
	/// Asserts that the request was sent with a header that has an entry satisfying
	/// <paramref name="predicate"/>.
	/// </summary>
	/// <param name="name">The header name, matched case-insensitively</param>
	/// <param name="predicate">The condition one of its entries must satisfy</param>
	/// <param name="failureMessage">Custom failure message</param>
	/// <returns>The same HTTP validation builder for method chaining</returns>
	public HttpValidationBuilder AssertRequestHeader(string name, Func<string, bool> predicate, string? failureMessage = null)
	{
		var pedido = PedidoEnviado();
		var valores = pedido.Header(name);
		valores.ShouldNotBeEmpty(
			$"Expected the request to be sent with a {name} header, but it had: {string.Join(", ", pedido.Headers.Keys)}");
		valores.Any(predicate).ShouldBeTrue(
			$"{failureMessage ?? $"Request header {name} did not satisfy the condition"}. Values: {string.Join(" | ", valores)}");
		return this;
	}

	/// <summary>
	/// Asserts that the client sent the cookie with the request, and optionally that its value
	/// satisfies <paramref name="predicate"/> — the session a client that keeps cookies carries.
	/// </summary>
	/// <param name="name">The cookie name, matched exactly</param>
	/// <param name="predicate">The condition on its value; null to only require it</param>
	/// <param name="failureMessage">Custom failure message</param>
	/// <returns>The same HTTP validation builder for method chaining</returns>
	public HttpValidationBuilder AssertSentCookie(string name, Func<string, bool>? predicate = null, string? failureMessage = null)
	{
		var pedido = PedidoEnviado();
		var valor = pedido.Cookie(name);
		valor.ShouldNotBeNull($"Expected the request to carry cookie {name}, but it carried: {NomesDosCookiesEnviados(pedido)}");

		if (predicate != null)
			predicate(valor).ShouldBeTrue(
				$"{failureMessage ?? $"Cookie {name} sent did not satisfy the condition"}. It was: {valor}");

		return this;
	}

	/// <summary>
	/// Asserts that the client did not send the cookie — e.g. a session the server told it to
	/// forget is no longer carried.
	/// </summary>
	/// <param name="name">The cookie name, matched exactly</param>
	/// <returns>The same HTTP validation builder for method chaining</returns>
	public HttpValidationBuilder AssertNoSentCookie(string name)
	{
		var valor = PedidoEnviado().Cookie(name);
		valor.ShouldBeNull($"Expected the request not to carry cookie {name}, but it did: {name}={valor}");
		return this;
	}

	/// <summary>
	/// Asserts an RFC 7807 / 9457 error response: the status code, and optionally its body —
	/// e.g. <c>p =&gt; p.Extension&lt;string&gt;("code") == "ScheduleConflict"</c>.
	/// </summary>
	/// <param name="expectedStatusCode">The expected HTTP status code</param>
	/// <param name="predicate">The condition on the problem details; null to only check the status</param>
	/// <param name="failureMessage">Custom failure message</param>
	/// <returns>The same HTTP validation builder for method chaining</returns>
	public HttpValidationBuilder AssertProblemDetails(int expectedStatusCode, Func<ProblemDetailsResponse, bool>? predicate = null, string? failureMessage = null)
	{
		AssertStatusCode(expectedStatusCode);

		var corpo = Result.ResponseBody?.ToString() ?? string.Empty;
		var problema = ProblemDetailsResponse.Parse(corpo);

		if (predicate != null)
			predicate(problema).ShouldBeTrue(
				$"{failureMessage ?? "Problem details did not satisfy the condition"}. Body: {corpo}");

		return this;
	}

	/// <summary>
	/// Asserts that the response body contains the text (ordinal comparison).
	/// </summary>
	/// <param name="text">The text that must appear</param>
	/// <returns>The same HTTP validation builder for method chaining</returns>
	public HttpValidationBuilder AssertBodyContains(string text)
	{
		(Result.ResponseBody?.ToString() ?? string.Empty).ShouldContain(text, Case.Sensitive);
		return this;
	}

	/// <summary>
	/// Asserts that the response body does not contain the text — for "this response must not
	/// leak X" (ordinal comparison).
	/// </summary>
	/// <param name="text">The text that must not appear</param>
	/// <returns>The same HTTP validation builder for method chaining</returns>
	public HttpValidationBuilder AssertBodyNotContains(string text)
	{
		(Result.ResponseBody?.ToString() ?? string.Empty).ShouldNotContain(text, Case.Sensitive);
		return this;
	}

	private List<string> ValoresDoCabecalho(string nome) =>
		(Result.Headers ?? new Dictionary<string, IEnumerable<string>>())
			.Where(h => string.Equals(h.Key, nome, StringComparison.OrdinalIgnoreCase))
			.SelectMany(h => h.Value)
			.ToList();

	private string NomesDosCabecalhos() =>
		string.Join(", ", (Result.Headers ?? new Dictionary<string, IEnumerable<string>>()).Keys);

	private List<SetCookie> CookiesDefinidos() =>
		ValoresDoCabecalho("Set-Cookie").Select(SetCookie.Parse).OfType<SetCookie>().ToList();

	// O último Set-Cookie de um nome é o que vale para o cliente.
	private SetCookie? CookieDefinido(string nome) =>
		CookiesDefinidos().LastOrDefault(c => string.Equals(c.Name, nome, StringComparison.Ordinal));

	private SentRequest PedidoEnviado() =>
		Result.Request ?? throw new InvalidOperationException(
			"No request was sent, so there is nothing to assert about it. " +
			$"The step failed before sending: {string.Join("; ", Result.Errors)}");

	private static string NomesDosCookiesEnviados(SentRequest pedido) =>
		pedido.Cookies.Count == 0 ? "no cookies" : string.Join(", ", pedido.Cookies.Keys);

	private string NomesDosCookies()
	{
		var nomes = CookiesDefinidos().Select(c => c.Name).ToList();
		return nomes.Count == 0 ? "no cookies" : string.Join(", ", nomes);
	}

	/// <summary>
	/// Extracts every value a path with [*] selects, e.g. the ids of a list.
	/// </summary>
	/// <typeparam name="T">The type of each value</typeparam>
	/// <param name="path">A path with [*], e.g. "$[*].id"</param>
	/// <returns>The selected values, in document order</returns>
	public IReadOnlyList<T> JsonPathAll<T>(string path) => Result.JsonPathAll<T>(path);

	/// <summary>
	/// Extracts a value from the JSON response using a simplified JSON path.
	/// Supports: $.propertyName, $.property.nested, $.array[0], a root array ($[0].id). For [*], use JsonPathAll.
	/// Delegates to HttpStepResultExtensions.JsonPath from XUnitAssured.Http.
	/// </summary>
	/// <typeparam name="T">The expected type of the value</typeparam>
	/// <param name="path">JSON path expression (e.g., "$.id", "$.items[0].price")</param>
	/// <returns>The extracted value converted to type T</returns>
	/// <exception cref="InvalidOperationException">Thrown when response body is empty</exception>
	/// <exception cref="System.Collections.Generic.KeyNotFoundException">Thrown when the JSON path doesn't exist</exception>
	/// <example>
	/// <code>
	/// var productId = builder.JsonPath&lt;int&gt;("$.id");
	/// var productName = builder.JsonPath&lt;string&gt;("$.name");
	/// var firstItemPrice = builder.JsonPath&lt;decimal&gt;("$.items[0].price");
	/// </code>
	/// </example>
	public T JsonPath<T>(string path)
	{
		// Delegate to XUnitAssured.Http extension method
		return Result.JsonPath<T>(path);
	}

	/// <summary>
	/// Gets the HTTP step result for additional custom assertions.
	/// Use this when you need direct access to the result beyond the fluent API.
	/// </summary>
	/// <returns>The HTTP step result</returns>
	public new HttpStepResult GetResult()
	{
		return base.Result;
	}

	/// <summary>
	/// Captures the full HTTP step result into a local variable while continuing the fluent chain.
	/// Use this to store results for later comparison between different API calls.
	/// </summary>
	/// <param name="result">The variable that will receive the HTTP step result</param>
	/// <returns>The same HTTP validation builder for method chaining</returns>
	/// <example>
	/// <code>
	/// (await Given()
	///     .ApiResource("/api/products")
	///     .Post(newProduct)
	///     .ExecuteAsync())
	///     .Then()
	///         .AssertStatusCode(201)
	///         .Extract(out var createResult);
	///
	/// // Later, use createResult to compare with another call
	/// var createdId = createResult.JsonPath&lt;int&gt;("$.id");
	/// </code>
	/// </example>
	public new HttpValidationBuilder Extract(out HttpStepResult result)
	{
		result = base.Result;
		return this;
	}

	/// <summary>
	/// Captures the HTTP step result via callback while continuing the fluent chain.
	/// Useful when you need to extract multiple values or perform complex capture logic.
	/// </summary>
	/// <param name="extractor">Action that receives the HTTP step result for extraction</param>
	/// <returns>The same HTTP validation builder for method chaining</returns>
	/// <example>
	/// <code>
	/// int statusCode = 0;
	/// (await Given()
	///     .ApiResource("/api/products/1")
	///     .Get()
	///     .ExecuteAsync())
	///     .Then()
	///         .Extract(result => statusCode = result.StatusCode)
	///         .AssertStatusCode(200);
	/// </code>
	/// </example>
	public new HttpValidationBuilder Extract(Action<HttpStepResult> extractor)
	{
		if (extractor == null)
			throw new ArgumentNullException(nameof(extractor));

		extractor(base.Result);
		return this;
	}

	/// <summary>
	/// Extracts a value from the JSON response using a JSON path into a local variable,
	/// while continuing the fluent chain.
	/// Use this to capture specific response values for comparison between API calls.
	/// </summary>
	/// <typeparam name="T">The expected type of the value (e.g., int, string, decimal)</typeparam>
	/// <param name="jsonPath">JSON path expression (e.g., "$.id", "$.items[0].price")</param>
	/// <param name="value">The variable that will receive the extracted value</param>
	/// <returns>The same HTTP validation builder for method chaining</returns>
	/// <example>
	/// <code>
	/// // Create a product and capture its ID
	/// (await Given()
	///     .ApiResource("/api/products")
	///     .Post(newProduct)
	///     .ExecuteAsync())
	///     .Then()
	///         .AssertStatusCode(201)
	///         .ExtractJsonPath&lt;int&gt;("$.id", out var createdId)
	///         .ExtractJsonPath&lt;string&gt;("$.name", out var createdName);
	///
	/// // Use captured values in subsequent calls
	/// (await Given()
	///     .ApiResource($"/api/products/{createdId}")
	///     .Get()
	///     .ExecuteAsync())
	///     .Then()
	///         .AssertStatusCode(200)
	///         .AssertJsonPath&lt;string&gt;("$.name", name =&gt; name == createdName);
	/// </code>
	/// </example>
	public HttpValidationBuilder ExtractJsonPath<T>(string jsonPath, out T value)
	{
		value = Result.JsonPath<T>(jsonPath);
		return this;
	}

	// Uma resposta já recebida, posta num cenário só para que o AssertEach entregue a ela o
	// construtor inteiro de asserções.
	private sealed class PassoJaExecutado(HttpStepResult resultado) : ITestStep
	{
		public string? Name => null;

		public string StepType => "Http";

		public ITestStepResult? Result => resultado;

		public bool IsExecuted => true;

		public bool IsValid => resultado.Success;

		public Task<ITestStepResult> ExecuteAsync(ITestContext context, CancellationToken cancellationToken = default) =>
			Task.FromResult<ITestStepResult>(resultado);

		public void Validate(Action<ITestStepResult> validation) => validation(resultado);
	}
}
