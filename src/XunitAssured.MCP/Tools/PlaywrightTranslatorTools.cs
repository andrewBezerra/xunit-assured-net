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
	[McpServerTool(Name = "translate_playwright_to_dsl"),
	 Description("Translates Playwright C# Library code (from the Inspector/Codegen) into XUnitAssured fluent DSL method calls. " +
	             "Strips boilerplate (using statements, browser/context/page creation) and converts locator+action patterns. " +
	             "Example input:  await page.GetByRole(AriaRole.Button, new() { Name = \"Click me\" }).ClickAsync(); " +
	             "Example output: .ClickByRole(AriaRole.Button, \"Click me\")")]
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

	[McpServerTool(Name = "translate_playwright_to_test"),
	 Description("Translates Playwright C# Library code into a complete XUnitAssured test block with Given/When/Then structure. " +
	             "Wraps the translated DSL calls in a ready-to-paste test method body.")]
	public static string TranslatePlaywrightToTest(
		[Description("The Playwright C# Library code to translate into a complete Given/When/Then test block.")] string playwrightCode)
	{
		if (string.IsNullOrWhiteSpace(playwrightCode))
			return "No code provided. Paste Playwright C# Library code to translate.";

		return PlaywrightCodeTranslator.TranslateToGivenBlock(playwrightCode);
	}

	[McpServerTool(Name = "list_xunitassured_dsl_methods", ReadOnly = true),
	 Description("Lists all available XUnitAssured Playwright DSL methods with their Playwright C# equivalents. " +
	             "Use this as a reference when manually converting code or understanding the DSL API surface.")]
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
