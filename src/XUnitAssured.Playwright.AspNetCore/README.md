# XUnitAssured.Playwright.AspNetCore

Browser tests against your own ASP.NET Core API, with `dotnet test` alone. `BrowserAppFixture<TProgram>` hosts the API on a real port with `WebApplicationFactory`, serves your built front-end on another, and launches the browser — no terminals to start by hand, so the tests run in CI.

Part of [XUnitAssured.Net](https://github.com/andrewBezerra/xunit-assured-net). Requires .NET 10 (`WebApplicationFactory.UseKestrel` arrived in `Microsoft.AspNetCore.Mvc.Testing` 10).

## Installation

```bash
dotnet add package XUnitAssured.Playwright.AspNetCore
```

Then install the browser once, from the test project's output:

```bash
pwsh bin/Debug/net10.0/playwright.ps1 install chromium
```

## Why

The usual `WebApplicationFactory` hosts the API in memory, where no browser can reach it. Browser tests then need the API, the front-end and the tests started separately, and they stay out of CI. Out of CI they go stale without anyone noticing: in a real consumer suite, the front-end changed how it stores the session and a browser test kept passing while provoking nothing.

## Usage

```csharp
using XUnitAssured.Playwright.AspNetCore;

public sealed class MyApp : BrowserAppFixture<Program>
{
    // The built front-end (the folder with index.html), relative to the test output.
    protected override string? AppDirectory => "../../../../my-app/dist";

    protected override void ConfigureApi(IWebHostBuilder api) =>
        api.UseEnvironment("E2ETesting");
}

public class SessionTests(MyApp app) : IClassFixture<MyApp>
{
    [Fact]
    public async Task Signs_In()
    {
        await using var page = await app.OpenPageAsync();

        var result = await ScenarioDsl.Given(page)
            .NavigateTo("/login")              // relative to app.AppUrl
            .FillByLabel("Email", "user@test.com")
            .ClickByRole(AriaRole.Button, "Sign in")
            .WaitForSelector("[data-testid=dashboard]")
            .ExecuteAsync();

        result.Then().AssertUrlContains("/dashboard");
    }
}
```

| Member | What it is |
|--------|------------|
| `ApiUrl` | The API's address, e.g. `http://localhost:5199` |
| `AppUrl` | The front-end's address — the browser's base URL. The API's when there is no front-end |
| `Api` | The `WebApplicationFactory`: `Api.Services` for the API's services, `Api.CreateClient()` to arrange data over HTTP |
| `ApiPort`, `AppPort` | Override to fix a port. 0 (the default) picks a free one |
| `AppDirectory` | Override to serve a built front-end, with a fallback to `index.html` for client-side routes |
| `ConfigureApi` | Override to configure the API host before it starts |

## Two origins, on purpose

The API and the front-end run on different ports, like an app and its API in production, so SameSite, the cookie `Path` and CORS with credentials apply as they would for real. The API must allow `AppUrl` as an origin. If its CORS policy reads the allowed origins from configuration, fix `AppPort` and set it in `ConfigureApi`; otherwise add a policy there:

```csharp
protected override void ConfigureApi(IWebHostBuilder api) =>
    api.ConfigureServices(services => services
        .AddCors()
        .AddTransient<IStartupFilter>(_ => new AllowApp(() => AppUrl)));

sealed class AllowApp(Func<string> origin) : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.UseCors(p => p.SetIsOriginAllowed(o => o == origin()).AllowCredentials().AllowAnyHeader().AllowAnyMethod());
        next(app);
    };
}
```

If the front-end was built with the API address baked in (`VITE_API_URL=http://localhost:5199`, for example), fix `ApiPort` to match.
