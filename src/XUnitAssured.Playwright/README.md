# XUnitAssured.Playwright

Browser-based UI testing extensions for the [XUnitAssured.Net](https://github.com/andrewBezerra/XUnitAssured.Net) framework. Write expressive end-to-end tests using a fluent `Given().When().Then()` DSL with multiple locator strategies, page interactions, screenshots, tracing, and rich assertions — built on [Microsoft Playwright](https://playwright.dev/dotnet/).

## Installation

```bash
dotnet add package XUnitAssured.Playwright
```

After installing, run the Playwright browser install:

```bash
pwsh bin/Debug/net10.0/playwright.ps1 install
```

## Quick Start

```csharp
using XUnitAssured.Playwright.Extensions;
using XUnitAssured.Playwright.Testing;

public class LoginTests : PlaywrightTestBase<MyPlaywrightFixture>, IClassFixture<MyPlaywrightFixture>
{
    public LoginTests(MyPlaywrightFixture fixture) : base(fixture) { }

    [Fact]
    public void Login_Should_Navigate_To_Dashboard()
    {
        Given()
            .NavigateTo("/login")
            .FillByLabel("Email", "user@test.com")
            .FillByLabel("Password", "secret")
            .ClickByRole(AriaRole.Button, "Sign in")
        .When()
            .Execute()
        .Then()
            .AssertSuccess()
            .AssertUrl("/dashboard");
    }
}
```

## Fluent DSL Reference

### Navigation

```csharp
Given()
    .NavigateTo("/login")                              // Navigate to relative or absolute URL
```

### Click

```csharp
.Click("#submit-btn")                                  // CSS selector
.ClickByRole(AriaRole.Button, "Submit")                // ARIA role + accessible name
.ClickByText("Sign in")                                // Visible text
.ClickByLabel("Remember me")                           // Associated label
.ClickByTestId("submit-btn")                           // data-testid attribute
.ClickByTitle("Close dialog")                          // title attribute
```

### Double Click

```csharp
.DoubleClick("#item")                                  // CSS selector
.DoubleClickByRole(AriaRole.Row, "Item 1")             // ARIA role
.DoubleClickByText("Edit")                             // Visible text
.DoubleClickByTestId("editable-cell")                  // data-testid
```

### Fill (clears then types)

```csharp
.Fill("#email", "user@test.com")                       // CSS selector
.FillByLabel("Email", "user@test.com")                 // Label text
.FillByPlaceholder("Enter email", "user@test.com")     // Placeholder text
.FillByRole(AriaRole.Textbox, "Search", "playwright")  // ARIA role + name
.FillByTestId("email-input", "user@test.com")          // data-testid
```

### Type (character by character)

```csharp
.TypeText("#search", "playwright")                     // CSS selector
.TypeTextByLabel("Search", "playwright")               // Label text
.TypeTextByPlaceholder("Search...", "playwright")       // Placeholder text
.TypeTextByTestId("search-input", "playwright")         // data-testid
.TypeTextByRole(AriaRole.Textbox, "Query", "pw")       // ARIA role
```

### Press (keyboard keys)

```csharp
.Press("#input", "Enter")                              // Press Enter on element
.Press("#editor", "Control+A")                         // Keyboard shortcut
```

### Check / Uncheck

```csharp
.Check("#terms")                                       // Check checkbox by CSS
.CheckByLabel("I agree")                               // Check by label
.CheckByRole(AriaRole.Checkbox, "Newsletter")          // Check by role
.Uncheck("#newsletter")                                // Uncheck by CSS
.UncheckByLabel("Newsletter")                          // Uncheck by label
```

### Select

```csharp
.SelectOption("#country", "Brazil")                    // Select by CSS + value
.SelectOptionByLabel("Country", "Brazil")              // Select by label
.SelectOptionByTestId("country-select", "BR")          // Select by test ID
```

### Hover / Focus

```csharp
.Hover("#menu-item")                                   // Hover by CSS
.HoverByRole(AriaRole.Link, "Settings")                // Hover by role
.Focus("#input")                                       // Focus by CSS
.FocusByTestId("search")                               // Focus by test ID
```

### Screenshot

```csharp
.Screenshot("login-page")                              // Capture named screenshot
```

### Wait

```csharp
.Wait(2000)                                            // Wait 2 seconds
.WaitForSelector("#loading", visible: false)           // Wait until element disappears
```

### Advanced Interactions

```csharp
.DragTo("#source", "#target")                          // Drag and drop
.ScrollIntoView("#footer")                             // Scroll element into view
.RightClick("#context-menu-target")                    // Right-click / context menu
.UploadFile("#file-input", "/path/to/file.pdf")        // File upload
```

### Playwright Inspector (Codegen)

```csharp
.RecordAndPause()                                      // Pause & open Playwright Inspector
```

> Requires `Headless = false`. Use this to record interactions via Codegen and then translate them into the XUnitAssured DSL.

## Assertions

```csharp
.When()
    .Execute()
.Then()
    .AssertSuccess()                                    // Assert step succeeded
    .AssertUrl("/dashboard")                            // Assert exact URL
    .AssertUrlContains("/dash")                         // Assert URL contains
    .AssertUrlEndsWith("/dashboard")                    // Assert URL ends with
    .AssertTitle("Dashboard")                           // Assert page title
    .AssertTitleContains("Dash")                        // Assert title contains
    .AssertVisible("#welcome")                          // Element visible (CSS)
    .AssertVisibleByRole(AriaRole.Heading, "Welcome")   // Element visible (role)
    .AssertVisibleByTestId("welcome-msg")               // Element visible (test ID)
    .AssertVisibleByText("Welcome back")                // Element visible (text)
    .AssertHidden("#spinner")                           // Element hidden (CSS)
    .AssertHiddenByTestId("loader")                     // Element hidden (test ID)
    .AssertHiddenByRole(AriaRole.Progressbar)           // Element hidden (role)
    .AssertText("#msg", "Success!")                      // Assert element text
    .AssertTextByRole(AriaRole.Alert, "Saved")          // Assert text by role
    .AssertTextByTestId("status", "OK")                 // Assert text by test ID
    .TakeScreenshot("login-page")                       // Capture a screenshot
```

## The page's network

Some behaviors only exist inside a browser: SameSite, the cookie `Path`, CORS with credentials, and
an app reacting to several requests failing at once. These verbs reach them without leaving the DSL.

```csharp
// Several requests come back 401 at once; the app should renew the session a single time.
var reloaded = await Given(browser)
    .NavigateTo("/dashboard")
    .InterceptRoute($"{apiUrl}/**", status: 401, times: 10)   // the first 10 matching, then through
    .Reload()
    .ExecuteAsync();

reloaded.Then()
    .AssertIntercepted(atLeast: 2)          // the scenario actually happened
    .AssertRequestedOnce("/auth/refresh");

// A request from inside the page, with its cookies, across origins.
string? token = null;

var renewed = await Given(browser)
    .FetchFromPage($"{apiUrl}/auth/refresh", "POST")
    .Validate((PlaywrightStepResult r) => token = r.LastFetch!.JsonPath<string>("$.accessToken"))
    .And()
    .FetchFromPage(() => $"{apiUrl}/auth/refresh", "POST", () => new { AccessToken = token })
    .ExecuteAsync();

renewed.Then()
    .AssertFetchStatus(200)
    .AssertFetchJsonPath<string>("$.accessToken", t => t != token);
```

- `InterceptRoute` takes a Playwright glob (`**/api/**`) and lasts until the end of its step.
- `AssertIntercepted` is the precondition: a route that matches nothing answers nothing, and a test
  that asserts only the outcome would pass without having provoked it.
- `FetchFromPage` bodies are sent as camelCase JSON. A status of 0 means the browser got no response
  — typically CORS refused it.
- `And()` starts a new step in a browser chain, so a `Validate` between steps runs before the next
  one (until 6.3 browser verbs kept adding to the same step).

## Playwright Codegen Integration

Record tests with Playwright Inspector and translate to XUnitAssured DSL:

```csharp
// Playwright Inspector output:
await page.GetByRole(AriaRole.Button, new() { Name = "Click me" }).ClickAsync();
await page.GetByLabel("Email").FillAsync("user@test.com");
await page.GetByPlaceholder("Search").FillAsync("test");

// XUnitAssured DSL (auto-translated via MCP or manually):
.ClickByRole(AriaRole.Button, "Click me")
.FillByLabel("Email", "user@test.com")
.FillByPlaceholder("Search", "test")
```

## Configuration

Put a `playwright` section in the `testsettings.json` of your test project:

```json
{
  "playwright": {
    "baseUrl": "https://localhost:5001",
    "browser": "Chromium",
    "headless": true,
    "slowMo": 0,
    "defaultTimeout": 30000,
    "navigationTimeout": 30000,
    "viewportWidth": 1280,
    "viewportHeight": 720,
    "screenshotDirectory": "screenshots",
    "tracingEnabled": false,
    "traceDirectory": "traces"
  }
}
```

> **If your project still has `playwrightsettings.json`,** it is still read, and the run prints
> once where its contents should move to. It will be removed in a future major. The old file
> also required `CopyToOutputDirectory`; the section does not.

### Configuration Options

| Option | Default | Description |
|--------|---------|-------------|
| `baseUrl` | — | Base URL for navigation (relative paths resolve against it) |
| `browser` | `Chromium` | Browser engine: `Chromium`, `Firefox`, or `Webkit` |
| `headless` | `true` | Run browser in headless mode |
| `slowMo` | `0` | Slow down actions by N milliseconds (for debugging) |
| `defaultTimeout` | `30000` | Timeout for actions (clicks, fills, waits) |
| `navigationTimeout` | `30000` | Timeout for navigation operations |
| `viewportWidth` | `1280` | Browser viewport width |
| `viewportHeight` | `720` | Browser viewport height |
| `screenshotDirectory` | `screenshots` | Directory for screenshot files |
| `tracingEnabled` | `false` | Enable Playwright tracing per test |
| `traceDirectory` | `traces` | Directory for trace files |

## Test Fixture

```csharp
using XUnitAssured.Playwright.Testing;

public class MyPlaywrightFixture : PlaywrightTestFixture
{
    public MyPlaywrightFixture() : base() { }
}
```

The fixture manages browser lifecycle (one browser per test class) and creates isolated `BrowserContext` + `Page` per test.

## Local Testing with Blazor (WebApplicationFactory)

```csharp
public class BlazorFixture : PlaywrightTestFixture, IAsyncLifetime
{
    private WebApplication? _app;

    public override async Task InitializeAsync()
    {
        // Start local Blazor app
        _app = WebApplication.Create();
        _app.MapFallbackToFile("index.html");
        await _app.StartAsync();

        // Override base URL to local server
        Settings.BaseUrl = _app.Urls.First();
        await base.InitializeAsync();
    }

    public override async Task DisposeAsync()
    {
        await base.DisposeAsync();
        if (_app != null) await _app.DisposeAsync();
    }
}
```

## Supported Frameworks

- .NET 8
- .NET 9
- .NET 10
- .NET 10

## Dependencies

- [XUnitAssured.Core](https://www.nuget.org/packages/XUnitAssured.Core) — DSL infrastructure and abstractions
- [Microsoft.Playwright](https://www.nuget.org/packages/Microsoft.Playwright) — Browser automation engine
- [Shouldly](https://www.nuget.org/packages/Shouldly) — Fluent assertions

## Links

- [GitHub Repository](https://github.com/andrewBezerra/XUnitAssured.Net)
- [Full Documentation](https://github.com/andrewBezerra/XUnitAssured.Net#readme)
- [Report Issues](https://github.com/andrewBezerra/XUnitAssured.Net/issues)
- [NuGet Package](https://www.nuget.org/packages/XUnitAssured.Playwright)

## License

Apache-2.0 — see [LICENSE.md](https://github.com/andrewBezerra/XUnitAssured.Net/blob/main/LICENSE.md)
