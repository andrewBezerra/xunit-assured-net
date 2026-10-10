using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using XUnitAssured.Core.Results;
using XUnitAssured.Http.Results;

namespace XUnitAssured.Http.Extensions;

/// <summary>
/// The assertions of <see cref="HttpValidationBuilder"/>, chained before the <c>await</c>, so a
/// test does not need the <c>(await ...ExecuteAsync())</c> parenthesis to reach them.
/// </summary>
/// <remarks>
/// <para>
/// Each method waits for the execution and then runs the assertion of the same name on the
/// builder; a failure surfaces at the <c>await</c>. Await the chain: without it, the request
/// still goes out, but a failed assertion is never seen and the test passes.
/// </para>
/// <para>
/// The names are the builder's, without <c>Async</c>, so the chain reads the same in both forms.
/// The two methods with an <c>out</c> parameter — <c>Extract(out ...)</c> and
/// <c>ExtractJsonPath(..., out ...)</c> — cannot be asynchronous; read the value with
/// <see cref="JsonPath{T}(Task{HttpValidationBuilder}, string)"/> or
/// <see cref="GetResult(Task{HttpValidationBuilder})"/> instead.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// await Given()
///     .ApiResource("/orders")
///     .Post(order)
///     .ExecuteAsync()
///     .AssertStatusCode(201)
///     .AssertJsonPath&lt;string&gt;("$.status", s =&gt; s == "open");
///
/// var id = await Given().ApiResource("/orders").Post(order).ExecuteAsync()
///     .AssertStatusCode(201)
///     .JsonPath&lt;string&gt;("$.id");
/// </code>
/// </example>
public static class HttpValidationTaskExtensions
{
	/// <inheritdoc cref="HttpValidationBuilder.Then"/>
	public static async Task<HttpValidationBuilder> Then(this Task<HttpValidationBuilder> validation) =>
		(await Pronto(validation)).Then();

	/// <inheritdoc cref="HttpValidationBuilder.AssertStatusCode(int)"/>
	public static async Task<HttpValidationBuilder> AssertStatusCode(this Task<HttpValidationBuilder> validation, int expectedStatusCode) =>
		(await Pronto(validation)).AssertStatusCode(expectedStatusCode);

	/// <inheritdoc cref="HttpValidationBuilder.AssertStatusCode(int, int[])"/>
	public static async Task<HttpValidationBuilder> AssertStatusCode(this Task<HttpValidationBuilder> validation, int accepted, params int[] alsoAccepted) =>
		(await Pronto(validation)).AssertStatusCode(accepted, alsoAccepted);

	/// <inheritdoc cref="HttpValidationBuilder.AssertEach"/>
	public static async Task<HttpValidationBuilder> AssertEach(this Task<HttpValidationBuilder> validation, Action<HttpValidationBuilder> assertions) =>
		(await Pronto(validation)).AssertEach(assertions);

	/// <inheritdoc cref="HttpValidationBuilder.ValidateContract{T}"/>
	public static async Task<HttpValidationBuilder> ValidateContract<T>(this Task<HttpValidationBuilder> validation) =>
		(await Pronto(validation)).ValidateContract<T>();

	/// <inheritdoc cref="HttpValidationBuilder.AssertJsonPath{T}(string, Action{T})"/>
	public static async Task<HttpValidationBuilder> AssertJsonPath<T>(this Task<HttpValidationBuilder> validation, string jsonPath, Action<T> assertion) =>
		(await Pronto(validation)).AssertJsonPath(jsonPath, assertion);

	/// <inheritdoc cref="HttpValidationBuilder.AssertJsonPath{T}(string, Func{T, bool}, string?)"/>
	public static async Task<HttpValidationBuilder> AssertJsonPath<T>(this Task<HttpValidationBuilder> validation, string jsonPath, Func<T, bool> predicate, string? failureMessage = null) =>
		(await Pronto(validation)).AssertJsonPath(jsonPath, predicate, failureMessage);

	/// <inheritdoc cref="HttpValidationBuilder.AssertJsonPathNull"/>
	public static async Task<HttpValidationBuilder> AssertJsonPathNull(this Task<HttpValidationBuilder> validation, string jsonPath) =>
		(await Pronto(validation)).AssertJsonPathNull(jsonPath);

	/// <inheritdoc cref="HttpValidationBuilder.AssertJsonPathMissing"/>
	public static async Task<HttpValidationBuilder> AssertJsonPathMissing(this Task<HttpValidationBuilder> validation, string jsonPath) =>
		(await Pronto(validation)).AssertJsonPathMissing(jsonPath);

	/// <inheritdoc cref="HttpValidationBuilder.AssertJsonPathContains{T}"/>
	public static async Task<HttpValidationBuilder> AssertJsonPathContains<T>(this Task<HttpValidationBuilder> validation, string jsonPath, T expected) =>
		(await Pronto(validation)).AssertJsonPathContains(jsonPath, expected);

	/// <inheritdoc cref="HttpValidationBuilder.AssertJsonPathNotContains{T}"/>
	public static async Task<HttpValidationBuilder> AssertJsonPathNotContains<T>(this Task<HttpValidationBuilder> validation, string jsonPath, T unexpected) =>
		(await Pronto(validation)).AssertJsonPathNotContains(jsonPath, unexpected);

	/// <inheritdoc cref="HttpValidationBuilder.AssertJsonPathCount"/>
	public static async Task<HttpValidationBuilder> AssertJsonPathCount(this Task<HttpValidationBuilder> validation, string jsonPath, int expectedCount) =>
		(await Pronto(validation)).AssertJsonPathCount(jsonPath, expectedCount);

	/// <inheritdoc cref="HttpValidationBuilder.AssertJsonPathAll{T}"/>
	public static async Task<HttpValidationBuilder> AssertJsonPathAll<T>(this Task<HttpValidationBuilder> validation, string jsonPath, Func<T, bool> predicate, string? failureMessage = null) =>
		(await Pronto(validation)).AssertJsonPathAll(jsonPath, predicate, failureMessage);

	/// <inheritdoc cref="HttpValidationBuilder.AssertHeader(string, string)"/>
	public static async Task<HttpValidationBuilder> AssertHeader(this Task<HttpValidationBuilder> validation, string name, string expectedValue) =>
		(await Pronto(validation)).AssertHeader(name, expectedValue);

	/// <inheritdoc cref="HttpValidationBuilder.AssertHeader(string, Func{string, bool}, string?)"/>
	public static async Task<HttpValidationBuilder> AssertHeader(this Task<HttpValidationBuilder> validation, string name, Func<string, bool> predicate, string? failureMessage = null) =>
		(await Pronto(validation)).AssertHeader(name, predicate, failureMessage);

	/// <inheritdoc cref="HttpValidationBuilder.AssertNoHeader"/>
	public static async Task<HttpValidationBuilder> AssertNoHeader(this Task<HttpValidationBuilder> validation, string name) =>
		(await Pronto(validation)).AssertNoHeader(name);

	/// <inheritdoc cref="HttpValidationBuilder.AssertSetCookie"/>
	public static async Task<HttpValidationBuilder> AssertSetCookie(this Task<HttpValidationBuilder> validation, string name, Func<SetCookie, bool>? predicate = null, string? failureMessage = null) =>
		(await Pronto(validation)).AssertSetCookie(name, predicate, failureMessage);

	/// <inheritdoc cref="HttpValidationBuilder.AssertCookieCleared"/>
	public static async Task<HttpValidationBuilder> AssertCookieCleared(this Task<HttpValidationBuilder> validation, string name) =>
		(await Pronto(validation)).AssertCookieCleared(name);

	/// <inheritdoc cref="HttpValidationBuilder.AssertRequestHeader(string, string)"/>
	public static async Task<HttpValidationBuilder> AssertRequestHeader(this Task<HttpValidationBuilder> validation, string name, string expectedValue) =>
		(await Pronto(validation)).AssertRequestHeader(name, expectedValue);

	/// <inheritdoc cref="HttpValidationBuilder.AssertRequestHeader(string, Func{string, bool}, string?)"/>
	public static async Task<HttpValidationBuilder> AssertRequestHeader(this Task<HttpValidationBuilder> validation, string name, Func<string, bool> predicate, string? failureMessage = null) =>
		(await Pronto(validation)).AssertRequestHeader(name, predicate, failureMessage);

	/// <inheritdoc cref="HttpValidationBuilder.AssertSentCookie"/>
	public static async Task<HttpValidationBuilder> AssertSentCookie(this Task<HttpValidationBuilder> validation, string name, Func<string, bool>? predicate = null, string? failureMessage = null) =>
		(await Pronto(validation)).AssertSentCookie(name, predicate, failureMessage);

	/// <inheritdoc cref="HttpValidationBuilder.AssertNoSentCookie"/>
	public static async Task<HttpValidationBuilder> AssertNoSentCookie(this Task<HttpValidationBuilder> validation, string name) =>
		(await Pronto(validation)).AssertNoSentCookie(name);

	/// <inheritdoc cref="HttpValidationBuilder.AssertProblemDetails"/>
	public static async Task<HttpValidationBuilder> AssertProblemDetails(this Task<HttpValidationBuilder> validation, int expectedStatusCode, Func<ProblemDetailsResponse, bool>? predicate = null, string? failureMessage = null) =>
		(await Pronto(validation)).AssertProblemDetails(expectedStatusCode, predicate, failureMessage);

	/// <inheritdoc cref="HttpValidationBuilder.AssertBodyContains"/>
	public static async Task<HttpValidationBuilder> AssertBodyContains(this Task<HttpValidationBuilder> validation, string text) =>
		(await Pronto(validation)).AssertBodyContains(text);

	/// <inheritdoc cref="HttpValidationBuilder.AssertBodyNotContains"/>
	public static async Task<HttpValidationBuilder> AssertBodyNotContains(this Task<HttpValidationBuilder> validation, string text) =>
		(await Pronto(validation)).AssertBodyNotContains(text);

	/// <inheritdoc cref="HttpValidationBuilder.Extract(Action{HttpStepResult})"/>
	public static async Task<HttpValidationBuilder> Extract(this Task<HttpValidationBuilder> validation, Action<HttpStepResult> extractor) =>
		(await Pronto(validation)).Extract(extractor);

	// Os que vêm do ValidationBuilder genérico devolvem o tipo base; aqui a cadeia continua no HTTP.

	/// <inheritdoc cref="Core.Extensions.ValidationBuilder{TResult}.AssertSuccess"/>
	public static async Task<HttpValidationBuilder> AssertSuccess(this Task<HttpValidationBuilder> validation)
	{
		var builder = await Pronto(validation);
		builder.AssertSuccess();
		return builder;
	}

	/// <inheritdoc cref="Core.Extensions.ValidationBuilder{TResult}.AssertFailure"/>
	public static async Task<HttpValidationBuilder> AssertFailure(this Task<HttpValidationBuilder> validation)
	{
		var builder = await Pronto(validation);
		builder.AssertFailure();
		return builder;
	}

	/// <inheritdoc cref="Core.Extensions.ValidationBuilder{TResult}.AssertErrors"/>
	public static async Task<HttpValidationBuilder> AssertErrors(this Task<HttpValidationBuilder> validation, Action<ITestStepResult> errorAssertion)
	{
		var builder = await Pronto(validation);
		builder.AssertErrors(errorAssertion);
		return builder;
	}

	/// <inheritdoc cref="Core.Extensions.ValidationBuilder{TResult}.AssertProperty{TProperty}"/>
	public static async Task<HttpValidationBuilder> AssertProperty<TProperty>(this Task<HttpValidationBuilder> validation, string propertyKey, Action<TProperty?> assertion)
	{
		var builder = await Pronto(validation);
		builder.AssertProperty(propertyKey, assertion);
		return builder;
	}

	/// <inheritdoc cref="Core.Extensions.ValidationBuilder{TResult}.Validate"/>
	public static async Task<HttpValidationBuilder> Validate(this Task<HttpValidationBuilder> validation, Action<HttpStepResult> assertion)
	{
		var builder = await Pronto(validation);
		builder.Validate(assertion);
		return builder;
	}

	// ---------- Os que devolvem um valor ----------

	/// <inheritdoc cref="HttpValidationBuilder.JsonPath{T}"/>
	public static async Task<T> JsonPath<T>(this Task<HttpValidationBuilder> validation, string path) =>
		(await Pronto(validation)).JsonPath<T>(path);

	/// <inheritdoc cref="HttpValidationBuilder.JsonPathAll{T}"/>
	public static async Task<IReadOnlyList<T>> JsonPathAll<T>(this Task<HttpValidationBuilder> validation, string path) =>
		(await Pronto(validation)).JsonPathAll<T>(path);

	/// <inheritdoc cref="HttpValidationBuilder.GetResult"/>
	public static async Task<HttpStepResult> GetResult(this Task<HttpValidationBuilder> validation) =>
		(await Pronto(validation)).GetResult();

	// A execução não depende do contexto de quem chamou, como no resto da biblioteca.
	private static ConfiguredTaskAwaitable<HttpValidationBuilder> Pronto(Task<HttpValidationBuilder> validation) =>
		(validation ?? throw new ArgumentNullException(nameof(validation))).ConfigureAwait(false);
}
