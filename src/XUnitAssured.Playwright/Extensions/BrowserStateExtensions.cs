using XUnitAssured.Core.Abstractions;
using XUnitAssured.Playwright.Abstractions;
using XUnitAssured.Playwright.DSL;
using XUnitAssured.Playwright.Steps;

namespace XUnitAssured.Playwright.Extensions;

/// <summary>
/// The state the browser holds, rather than what is on the screen.
///
/// <para>
/// The DSL had verbs for interacting with a page — click, fill, wait for an element — and none
/// for cookies or storage. A test about a session had to drop to <c>IPage</c> and hand-write
/// JavaScript, which is the point at which a DSL stops earning its place.
/// </para>
///
/// <para>
/// These came out of testing a real sign-in: a refresh token that moved out of
/// <c>localStorage</c> and into an <c>HttpOnly</c> cookie, where the whole question was
/// whether the page could still reach it. See <c>AssertCookieIsHttpOnly</c> on the validation
/// builder for the assertion that has no workaround.
/// </para>
/// </summary>
public static class BrowserStateExtensions
{
	/// <summary>
	/// Discards every cookie the browser context holds.
	///
	/// This is what a signed-out visitor looks like, and also what a site attacking from
	/// another origin faces: it can make the browser send a request, but not carry a session
	/// into it.
	/// </summary>
	public static IBrowserScenario ClearCookies(this ITestScenario scenario)
	{
		PlaywrightScenarioExtensions.GetOrCreateStep(scenario).AddAction(new PageAction
		{
			ActionType = PageActionType.ClearCookies
		});

		return BrowserScenario.De(scenario);
	}

	/// <summary>
	/// Writes one entry into the page's local storage.
	///
	/// Useful as arrangement: starting a test from a state the app would have reached after
	/// several screens, without walking through them.
	/// </summary>
	public static IBrowserScenario SetLocalStorage(this ITestScenario scenario, string key, string value)
	{
		PlaywrightScenarioExtensions.GetOrCreateStep(scenario).AddAction(new PageAction
		{
			ActionType = PageActionType.SetLocalStorage,
			Value = key,
			SecondValue = value
		});

		return BrowserScenario.De(scenario);
	}

	/// <summary>
	/// Empties the page's local storage.
	/// </summary>
	public static IBrowserScenario ClearLocalStorage(this ITestScenario scenario)
	{
		PlaywrightScenarioExtensions.GetOrCreateStep(scenario).AddAction(new PageAction
		{
			ActionType = PageActionType.ClearLocalStorage
		});

		return BrowserScenario.De(scenario);
	}
}
