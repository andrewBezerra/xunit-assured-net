using System;

using XUnitAssured.Core.Abstractions;
using XUnitAssured.Playwright.Abstractions;
using XUnitAssured.Playwright.DSL;
using XUnitAssured.Playwright.Steps;

namespace XUnitAssured.Playwright.Extensions;

/// <summary>
/// The network as the page sees it: answering requests from the test, reloading, and making a
/// request from inside the page.
/// </summary>
public static class PageNetworkExtensions
{
	/// <summary>
	/// Answers the first <paramref name="times"/> requests matching <paramref name="urlPattern"/>
	/// with <paramref name="status"/>, and lets the following ones through — for the rest of the
	/// step. Assert with <c>AssertIntercepted</c> that it actually answered something.
	/// </summary>
	/// <remarks>
	/// Reproduces what is hard to provoke for real, e.g. an access token expiring while a screen
	/// loads: several requests come back 401 at once, and the retry reaches the real API.
	/// </remarks>
	/// <param name="scenario">The test scenario</param>
	/// <param name="urlPattern">A Playwright glob, e.g. <c>"**/api/**"</c>, or a full URL</param>
	/// <param name="status">The status code to answer with</param>
	/// <param name="times">How many matching requests to answer before letting them through</param>
	/// <param name="body">The body to answer with, sent as application/json; null for none</param>
	/// <returns>The scenario, typed as a browser scenario</returns>
	/// <example>
	/// <code>
	/// .InterceptRoute($"{apiUrl}/**", status: 401, times: 10)
	/// .Reload()
	/// .Then().AssertIntercepted(atLeast: 2).AssertRequestedOnce("/auth/refresh")
	/// </code>
	/// </example>
	public static IBrowserScenario InterceptRoute(this ITestScenario scenario, string urlPattern, int status, int times = 1, string? body = null)
	{
		if (string.IsNullOrWhiteSpace(urlPattern)) throw new ArgumentException("A URL pattern is required.", nameof(urlPattern));
		if (times < 1) throw new ArgumentOutOfRangeException(nameof(times), "A route has to answer at least one request.");

		PlaywrightScenarioExtensions.GetOrCreateStep(scenario).AddAction(new PageAction
		{
			ActionType = PageActionType.InterceptRoute,
			Value = urlPattern,
			Status = status,
			Times = times,
			SecondValue = body
		});

		return BrowserScenario.De(scenario);
	}

	/// <summary>
	/// Reloads the page and waits for the network to settle — the moment an app restores its
	/// session and loads a screen all at once.
	/// </summary>
	/// <param name="scenario">The test scenario</param>
	/// <returns>The scenario, typed as a browser scenario</returns>
	public static IBrowserScenario Reload(this ITestScenario scenario)
	{
		PlaywrightScenarioExtensions.GetOrCreateStep(scenario).AddAction(new PageAction
		{
			ActionType = PageActionType.Reload
		});

		return BrowserScenario.De(scenario);
	}

	/// <summary>
	/// Makes a request from inside the page, with its cookies (<c>credentials: 'include'</c>).
	/// Read the response with <c>AssertFetchStatus</c> and <c>AssertFetchJsonPath</c>.
	/// </summary>
	/// <remarks>
	/// Cross-origin rules — SameSite, the cookie <c>Path</c>, CORS with credentials — only apply
	/// to a request that leaves the page. A request from the test process proves none of them.
	/// </remarks>
	/// <param name="scenario">The test scenario</param>
	/// <param name="url">The address; relative to the page, or absolute for another origin</param>
	/// <param name="method">The HTTP method</param>
	/// <param name="body">The body, serialized as camelCase JSON; null for none</param>
	/// <returns>The scenario, typed as a browser scenario</returns>
	public static IBrowserScenario FetchFromPage(this ITestScenario scenario, string url, string method = "GET", object? body = null)
	{
		if (string.IsNullOrWhiteSpace(url)) throw new ArgumentException("A URL is required.", nameof(url));

		return AdicionarBusca(scenario, new PageAction
		{
			ActionType = PageActionType.FetchFromPage,
			Value = url,
			SecondValue = method,
			BodyProvider = body == null ? null : () => body
		});
	}

	/// <summary>
	/// Makes a request from inside the page, with the URL and the body built when the step runs —
	/// for a value an earlier step produced, such as the token a previous request returned.
	/// </summary>
	/// <param name="scenario">The test scenario</param>
	/// <param name="url">Builds the address at execution time</param>
	/// <param name="method">The HTTP method</param>
	/// <param name="body">Builds the body at execution time; null for none</param>
	/// <returns>The scenario, typed as a browser scenario</returns>
	/// <example>
	/// <code>
	/// string? token = null;
	///
	/// await Given(browser)
	///     .FetchFromPage($"{api}/auth/refresh", "POST")
	///     .Validate((PlaywrightStepResult r) =&gt; token = r.LastFetch!.JsonPath&lt;string&gt;("$.accessToken"))
	///     .And()
	///     .FetchFromPage(() =&gt; $"{api}/auth/refresh", "POST", () =&gt; new { accessToken = token })
	///     .ExecuteAsync();
	/// </code>
	/// </example>
	public static IBrowserScenario FetchFromPage(this ITestScenario scenario, Func<string> url, string method = "GET", Func<object?>? body = null)
	{
		if (url == null) throw new ArgumentNullException(nameof(url));

		return AdicionarBusca(scenario, new PageAction
		{
			ActionType = PageActionType.FetchFromPage,
			ValueProvider = url,
			SecondValue = method,
			BodyProvider = body
		});
	}

	private static IBrowserScenario AdicionarBusca(ITestScenario scenario, PageAction acao)
	{
		PlaywrightScenarioExtensions.GetOrCreateStep(scenario).AddAction(acao);
		return BrowserScenario.De(scenario);
	}
}
