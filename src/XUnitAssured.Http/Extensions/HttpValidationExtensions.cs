using XUnitAssured.Http.Abstractions;
using XUnitAssured.Http.DSL;
using System;
using System.Linq;
using XUnitAssured.Core.Abstractions;
using XUnitAssured.Core.Results;
using XUnitAssured.Http.Results;

namespace XUnitAssured.Http.Extensions;

/// <summary>
/// Extension methods for validating HTTP responses in the fluent DSL.
/// </summary>
public static class HttpValidationExtensions
{
	/// <summary>
	/// Validates the HTTP response using a custom validation function.
	/// The validation function receives an HttpStepResult with typed access to HTTP-specific properties.
	/// Usage: .Validate(response => response.StatusCode.ShouldBe(200))
	/// </summary>
	public static IHttpScenario Validate(this ITestScenario scenario, Action<HttpStepResult> validation)
	{
		if (scenario == null)
			throw new ArgumentNullException(nameof(scenario));

		if (validation == null)
			throw new ArgumentNullException(nameof(validation));

		// Registered, not run: the step has not run yet when the chain is being written.
		// The lambda closes over whatever it needs and is called once the result exists.

		// Validate with the generic ITestStepResult, then cast to HttpStepResult
		scenario.AddValidation(result =>
		{
			if (result is not HttpStepResult httpResult)
				throw new InvalidOperationException($"Expected HttpStepResult but got {result.GetType().Name}");

			validation(httpResult);
		});

		return HttpScenario.De(scenario);
	}

	/// <summary>
	/// Expects the status code of the step being described, checked right after it runs — for a
	/// step before the last one of a scenario, such as the POST that creates what the next step
	/// reads back. When the status is another, the scenario stops there, and the later steps do
	/// not run.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The failure says which request got the status, then what <c>AssertStatusCode</c> says: the
	/// accepted codes, the status, and the body.
	/// </para>
	/// <para>
	/// Named <c>Expect</c> and not <c>Assert</c> because it checks nothing yet: it registers a check
	/// for when the step runs. The last step is asserted after <c>ExecuteAsync()</c>, as always.
	/// </para>
	/// </remarks>
	/// <param name="scenario">The scenario whose current step is being described</param>
	/// <param name="accepted">An accepted status code</param>
	/// <param name="alsoAccepted">The other accepted status codes</param>
	/// <returns>The scenario, to go on describing it</returns>
	/// <example>
	/// <code>
	/// var readBack = await Given()
	///     .ApiResource("/orders").Post(order)
	///     .ExpectStatusCode(201)
	///     .Validate((HttpStepResult created) =&gt; id = created.JsonPath&lt;string&gt;("$.id"))
	/// .And()
	///     .ApiResource(() =&gt; $"/orders/{id}").Get()
	///     .ExecuteAsync();
	/// </code>
	/// </example>
	public static IHttpScenario ExpectStatusCode(this ITestScenario scenario, int accepted, params int[] alsoAccepted)
	{
		if (scenario == null)
			throw new ArgumentNullException(nameof(scenario));

		var aceitos = new[] { accepted }.Concat(alsoAccepted ?? Array.Empty<int>()).ToArray();
		scenario.AddValidation(result => VerificacaoDeStatus.Verificar(result, aceitos, dizerOPedido: true));

		return HttpScenario.De(scenario);
	}

	/// <summary>
	/// Validates the HTTP response using a generic validation function.
	/// The validation function receives the base ITestStepResult.
	/// </summary>
	public static IHttpScenario Validate(this ITestScenario scenario, Action<ITestStepResult> validation)
	{
		if (scenario == null)
			throw new ArgumentNullException(nameof(scenario));

		if (validation == null)
			throw new ArgumentNullException(nameof(validation));

		// Registered, not run: the step has not run yet when the chain is being written.
		// The lambda closes over whatever it needs and is called once the result exists.

		scenario.AddValidation(validation);

		return HttpScenario.De(scenario);
	}
}
