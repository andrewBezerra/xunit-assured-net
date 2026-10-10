using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Shouldly;

using XUnitAssured.Http.Abstractions;
using XUnitAssured.Http.Results;

namespace XUnitAssured.Http.Extensions;

/// <summary>
/// Arranging data through the API: run a request that must succeed, and get a value back.
/// </summary>
/// <remarks>
/// <para>
/// Creating a parent resource and keeping its id is the most repeated code in an E2E suite, and
/// getting one value out of <c>ExecuteAsync()</c> takes several steps — so suites write their
/// own helpers on a raw <see cref="System.Net.Http.HttpClient"/>, often blocking with
/// <c>.GetAwaiter().GetResult()</c>.
/// </para>
/// <para>
/// These fail on a non-2xx response, with the status and the body in the message. An arrange
/// that silently failed would let the test that follows pass or fail for the wrong reason.
/// </para>
/// </remarks>
public static class HttpArrangeExtensions
{
	/// <summary>
	/// Executes the chain, requires a 2xx response, and returns the value at a JSON path.
	/// </summary>
	/// <typeparam name="T">The type of the value</typeparam>
	/// <param name="scenario">The HTTP chain to execute</param>
	/// <param name="path">The JSON path of the value, e.g. "$.id"</param>
	/// <param name="cancellationToken">Cancels the request</param>
	/// <returns>The value at the path</returns>
	/// <example>
	/// <code>
	/// var orderId = await Given().ApiResource("/orders").Post(order).ExtractAsync&lt;string&gt;("$.id");
	/// </code>
	/// </example>
	public static async Task<T> ExtractAsync<T>(this IHttpScenario scenario, string path, CancellationToken cancellationToken = default)
	{
		var execucao = await scenario.EnsureSuccessAsync(cancellationToken).ConfigureAwait(false);
		return execucao.JsonPath<T>(path);
	}

	/// <summary>
	/// Executes the chain, requires a 2xx response, and returns the values at two JSON paths.
	/// </summary>
	/// <typeparam name="T1">The type of the first value</typeparam>
	/// <typeparam name="T2">The type of the second value</typeparam>
	/// <param name="scenario">The HTTP chain to execute</param>
	/// <param name="path1">The JSON path of the first value</param>
	/// <param name="path2">The JSON path of the second value</param>
	/// <param name="cancellationToken">Cancels the request</param>
	/// <returns>Both values</returns>
	/// <example>
	/// <code>
	/// var (id, token) = await Given().ApiResource(route).Post(body).ExtractAsync&lt;string, string&gt;("$.id", "$.token");
	/// </code>
	/// </example>
	public static async Task<(T1, T2)> ExtractAsync<T1, T2>(this IHttpScenario scenario, string path1, string path2, CancellationToken cancellationToken = default)
	{
		var execucao = await scenario.EnsureSuccessAsync(cancellationToken).ConfigureAwait(false);
		return (execucao.JsonPath<T1>(path1), execucao.JsonPath<T2>(path2));
	}

	/// <summary>
	/// Executes the chain and requires a 2xx response — for an arrange that needs no value back.
	/// </summary>
	/// <param name="scenario">The HTTP chain to execute</param>
	/// <param name="cancellationToken">Cancels the request</param>
	/// <returns>The validation builder, for further checks if needed</returns>
	public static async Task<HttpValidationBuilder> EnsureSuccessAsync(this IHttpScenario scenario, CancellationToken cancellationToken = default)
	{
		if (scenario == null)
			throw new ArgumentNullException(nameof(scenario));

		var execucao = await scenario.ExecuteAsync(cancellationToken).ConfigureAwait(false);
		ExigirSucesso(execucao.GetResult());
		return execucao;
	}

	private static void ExigirSucesso(HttpStepResult resultado)
	{
		// Sem status não houve resposta: tempo esgotado, conexão recusada, endereço que não
		// resolve. O motivo está nos erros, e é ele que precisa aparecer.
		if (resultado.StatusCode == 0)
			throw new ShouldAssertException($"The arrange request got no response. {resultado.Explicacao()}");

		resultado.IsSuccessStatusCode.ShouldBeTrue(
			$"The arrange request answered {resultado.StatusCode} {resultado.ReasonPhrase}, " +
			$"so the test cannot rely on what it was meant to create. {resultado.Explicacao()}");
	}
}
