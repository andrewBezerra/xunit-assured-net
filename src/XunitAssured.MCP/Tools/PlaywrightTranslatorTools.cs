using System.ComponentModel;

using ModelContextProtocol.Server;

using XUnitAssured.Playwright.Codegen;

namespace XUnitAssured.Mcp.Tools;

/// <summary>
/// MCP tools for translating Playwright C# Library code into XUnitAssured fluent DSL.
/// These tools are automatically discovered by the MCP server via WithToolsFromAssembly().
/// </summary>
[McpServerToolType]
public static class PlaywrightTranslatorTools
{
	/// <summary>
	/// Converts code recorded by the Playwright Inspector into the equivalent XUnitAssured
	/// DSL calls, dropping the browser/context/page boilerplate. Lets a tester record a
	/// flow in the browser and paste it into a scenario without hand-translating locators.
	/// </summary>
	[McpServerTool(Name = "translate_playwright_to_dsl"),
	 Description("Translates Playwright C# Library code, as the Inspector or Codegen emits it, into XUnitAssured fluent DSL calls. " +
	             "Strips the boilerplate the fixture already owns (using statements, browser, context and page creation) and converts each locator plus action pair. " +
	             "Returns only the chain of DSL calls as text, with no test method around them, and translates nothing it does not recognise: unsupported calls come back as a commented TODO line rather than being dropped. " +
	             "For a complete pasteable test method, use translate_playwright_to_test.")]
	public static string TranslatePlaywrightToDsl(
		[Description("The Playwright C# Library code to translate. Can be a single line or a full block copied from the Inspector.")] string playwrightCode)
	{
		if (string.IsNullOrWhiteSpace(playwrightCode))
			return "No code provided. Paste Playwright C# Library code to translate.";

		var result = PlaywrightCodeTranslator.Translate(playwrightCode, indent: "\t\t\t");

		return string.IsNullOrWhiteSpace(result)
			? "No translatable Playwright actions found. Make sure the code contains lines like:\n  await page.GetByRole(...).ClickAsync();"
			: result;
	}

	/// <summary>
	/// Same translation as the DSL tool, wrapped in a complete Given/When/Then test method
	/// body, for when the recording should become a whole test rather than a fragment.
	/// </summary>
	[McpServerTool(Name = "translate_playwright_to_test"),
	 Description("Translates Playwright C# Library code into a complete XUnitAssured test method body with Given/When/Then structure. " +
	             "Returns C# source text, ready to paste; unsupported calls come back as commented TODO lines rather than being dropped. " +
	             "It does not write files, compile, or run anything, and it does not emit the test class or its fixture. " +
	             "When the surrounding test already exists and only the chain is wanted, use translate_playwright_to_dsl.")]
	public static string TranslatePlaywrightToTest(
		[Description("The Playwright C# Library code to translate into a complete Given/When/Then test block.")] string playwrightCode)
	{
		if (string.IsNullOrWhiteSpace(playwrightCode))
			return "No code provided. Paste Playwright C# Library code to translate.";

		return PlaywrightCodeTranslator.TranslateToGivenBlock(playwrightCode);
	}

	/// <summary>
	/// Returns the Playwright DSL surface next to each method's Playwright C# equivalent,
	/// so an assistant — or a person converting code by hand — can map one to the other.
	/// </summary>
	[McpServerTool(Name = "list_xunitassured_dsl_methods", ReadOnly = true),
	 Description("Lists the XUnitAssured Playwright DSL methods next to their Playwright C# equivalents, as a reference for converting code by hand or for understanding the DSL surface. " +
	             "Returns a static reference table as text; it reads nothing from the caller's project and generates no code. " +
	             "Covers the browser DSL only. HTTP and Kafka have their own list tools. " +
	             "Despite the generic name, this is the Playwright table; the siblings are list_http_dsl_methods and list_kafka_dsl_methods.")]
	public static string ListDslMethods(
		[Description("Optional filter: 'click', 'fill', 'check', 'hover', 'navigation', 'all'. Default is 'all'.")] string filter = "all")
	{
		filter = (filter ?? "all").Trim().ToLowerInvariant();

		var sections = new List<string>();

		if (filter is "all" or "navigation")
		{
			sections.Add("""
			== Navigation ==
			  .NavigateTo("/path")              ← await page.GotoAsync("/path")
			""");
		}

		if (filter is "all" or "click")
		{
			sections.Add("""
			== Click ==
			  .Click("#css")                    ← page.Locator("#css").ClickAsync()
			  .ClickByRole(AriaRole.X, "name")  ← page.GetByRole(AriaRole.X, new() { Name = "name" }).ClickAsync()
			  .ClickByText("text")              ← page.GetByText("text").ClickAsync()
			  .ClickByLabel("label")            ← page.GetByLabel("label").ClickAsync()
			  .ClickByTestId("id")              ← page.GetByTestId("id").ClickAsync()
			  .ClickByTitle("title")            ← page.GetByTitle("title").ClickAsync()
			  .DoubleClickByRole(...)           ← .DblClickAsync()
			  .RightClickByRole(...)            ← .ClickAsync(button: Right)
			  .ForceClick("#css")               ← .ClickAsync(force: true)
			""");
		}

		if (filter is "all" or "fill")
		{
			sections.Add("""
			== Fill / Type / Press ==
			  .Fill("#css", "value")            ← page.Locator("#css").FillAsync("value")
			  .FillByLabel("label", "value")    ← page.GetByLabel("label").FillAsync("value")
			  .FillByPlaceholder("ph", "value") ← page.GetByPlaceholder("ph").FillAsync("value")
			  .FillByRole(AriaRole.X, "n", "v") ← page.GetByRole(AriaRole.X, new() { Name = "n" }).FillAsync("v")
			  .FillByTestId("id", "value")      ← page.GetByTestId("id").FillAsync("value")
			  .TypeText("#css", "text")         ← .PressSequentiallyAsync("text")
			  .Press("#css", "Enter")           ← .PressAsync("Enter")
			  .Clear("#css")                    ← .ClearAsync()
			""");
		}

		if (filter is "all" or "check")
		{
			sections.Add("""
			== Check / Uncheck / Select ==
			  .Check("#css")                    ← .CheckAsync()
			  .CheckByLabel("label")            ← page.GetByLabel("label").CheckAsync()
			  .CheckByRole(AriaRole.X)          ← page.GetByRole(AriaRole.X).CheckAsync()
			  .Uncheck("#css")                  ← .UncheckAsync()
			  .SelectOption("#css", "value")    ← .SelectOptionAsync("value")
			  .SetChecked("#css", true/false)   ← .SetCheckedAsync(true/false)
			""");
		}

		if (filter is "all" or "hover")
		{
			sections.Add("""
			== Hover / Focus / Scroll ==
			  .Hover("#css")                    ← .HoverAsync()
			  .HoverByText("text")              ← page.GetByText("text").HoverAsync()
			  .Focus("#css")                    ← .FocusAsync()
			  .ScrollIntoView("#css")           ← .ScrollIntoViewIfNeededAsync()
			""");
		}

		if (filter is "all")
		{
			sections.Add("""
			== Browser state (6.0.0; no Playwright one-liner equivalent) ==
			  .ClearCookies()                                     Clear the context's cookies
			  .SetLocalStorage("key", "value")                    Write to local storage
			  .ClearLocalStorage()                                Clear local storage
			  .AssertCookie("name")                               Assert a cookie exists
			  .AssertNoCookie("name")                             Assert a cookie is gone
			  .AssertCookieIsHttpOnly("name")                     Assert the page's JS cannot read it
			  .AssertLocalStorage("key", "value")                 Assert a stored value
			  .AssertNoLocalStorage("key")                        Assert nothing is stored there

			== Observed requests (6.0.0) ==
			  .AssertRequested("/v1/orders/*")                    Assert the page requested it (* wildcard)
			  .AssertRequestedOnce("/v1/auth/refresh")            Assert it was requested exactly once

			== The page's network (6.4.0) ==
			  .InterceptRoute("**/api/**", status: 401, times: 3) Answer the first N matching requests, then let them through (rest of the step)
			  .Reload()                                           Reload and wait for the network to settle
			  .FetchFromPage($"{api}/me")                         Request from inside the page, with its cookies
			  .FetchFromPage(() => url, "POST", () => new { t })  URL and body built when the step runs
			  .AssertIntercepted(atLeast: 2)                      The interception actually answered something
			  .AssertFetchStatus(200)                             Status of the last FetchFromPage (0 = no response, e.g. CORS)
			  .AssertFetchJsonPath<string>("$.token", t => ...)   A value in the last FetchFromPage body
			  .Validate((PlaywrightStepResult r) => ...)          Capture from the step result, e.g. r.LastFetch

			== Other ==
			  .Wait(1000)                       ← await Task.Delay(1000)
			  .WaitForSelector(".class")        ← page.WaitForSelectorAsync(".class")
			  .TakeScreenshot("name.png")       ← page.ScreenshotAsync(...)
			  .DragTo("#src", "#tgt")           ← source.DragToAsync(target)
			  .UploadFile("#input", "path")     ← .SetInputFilesAsync("path")
			  .RecordAndPause()                 ← page.PauseAsync() — opens Inspector
			""");
		}

		return sections.Count > 0
			? string.Join("\n", sections)
			: $"Unknown filter '{filter}'. Use: click, fill, check, hover, navigation, or all.";
	}
}
