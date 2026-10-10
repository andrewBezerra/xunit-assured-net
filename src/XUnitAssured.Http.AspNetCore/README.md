# XUnitAssured.Http.AspNetCore

HTTP tests against your own ASP.NET Core API. `ApiFixture<TProgram>` hosts the API in memory with `WebApplicationFactory` and hands out what those tests keep writing by hand: a shared client, a new client per identity, clients with and without cookies, the API's services and, if you ask, its logs.

Part of [XUnitAssured.Net](https://github.com/andrewBezerra/xunit-assured-net). .NET 8, 9 and 10.

## Installation

```bash
dotnet add package XUnitAssured.Http.AspNetCore
```

## Why

Every suite that tests its own API in process carries the same fixture: a `WebApplicationFactory<Program>` with a test environment, a client with the test user's headers, another for a user with fewer permissions, one that keeps cookies for session tests, one that does not, and a service scope for the few checks no endpoint shows. In a real consumer suite that fixture was about 120 lines, almost all of it generic.

And the usual way to use it is the slow one. An `IClassFixture` per test class starts the API once per class, running whatever the API does on start — database migrations included. In that suite the first test of every class took 2–6 s and the rest 0.05–0.2 s: the startups were about 70 of the suite's 118 s. One instance shared through a collection fixture brought the suite to about a minute, with no test changes.

## Usage

```csharp
using XUnitAssured.Http.AspNetCore;
using XUnitAssured.Http.Testing;

public sealed class MyApi : ApiFixture<Program>
{
    protected override void ConfigureApi(IWebHostBuilder api) =>
        api.UseEnvironment("Testing");

    // Who the shared client is: here, a test-only header the API reads in Testing.
    protected override void ConfigureClient(HttpClient client) =>
        client.DefaultRequestHeaders.Add("X-Test-User", "admin");
}

// One API for the whole suite.
[CollectionDefinition("API")]
public sealed class ApiCollection : ICollectionFixture<MyApi>;

[Collection("API")]
public class OrderTests(MyApi api) : HttpTestBase<MyApi>(api)
{
    [Fact]
    public async Task Admin_Lists_Orders() =>
        (await Given()                       // the shared client
            .ApiResource("/orders")
            .Get()
            .ExecuteAsync())
        .Then().AssertStatusCode(200);

    [Fact]
    public async Task Reader_Cannot_Delete() =>
        (await Given()
            .WithHttpClient(Fixture.ClientFor(("X-Test-User", "reader")))
            .ApiResource("/orders/1")
            .Delete()
            .ExecuteAsync())
        .Then().AssertStatusCode(403);
}
```

| Member | What it is |
|--------|------------|
| `CreateClient()` | The shared client, the one `Given(fixture)` uses. Created once, with `ConfigureClient` applied |
| `ClientFor(("Name", "value"), ...)` | A new client as someone else: `ConfigureClient`, then these headers, each replacing one of the same name |
| `ClientWithCookies()` | A new client with its own cookie jar, for a session that lives in a cookie |
| `ClientWithoutCookies()` | A new client that never keeps cookies, to see a session from outside |
| `Services`, `CreateScope()` | The API's services, for the checks no endpoint shows |
| `Logs` | What the API logged, when `CaptureLogs` is true — see below |
| `Api` | The `WebApplicationFactory` itself, for anything else |

| Override | What it is for |
|----------|----------------|
| `ConfigureApi(IWebHostBuilder)` | Environment, settings and services of the API under test |
| `ConfigureClient(HttpClient)` | The identity of every client the fixture creates |
| `Authentication` | An `HttpAuthConfig` applied to every request of a `Given(fixture)` scenario |
| `CaptureLogs` | True to record everything the API logs, at every level |

The API starts with the first client or the first use of `Services`, and stops when xUnit disposes the fixture.

## Sharing safely

One instance for the suite works when each test creates the data it reads — its own customer, its own order — and the tests of the collection run one at a time, as xUnit runs them. Two rules keep it that way:

- **Do not change the shared client.** A header added in one test is there in the next. Ask for another client with `ClientFor`.
- **A test that changes how the API is configured needs its own instance.** Derive from your fixture and give it its own collection, or create it inside the test:

```csharp
public sealed class MyApiWithFakeMail : ApiFixture<Program>
{
    public FakeMail Mail { get; } = new();

    protected override void ConfigureApi(IWebHostBuilder api) =>
        api.UseEnvironment("Testing")
           .ConfigureServices(s => s.AddSingleton<IMail>(Mail));
}

[Fact]
public async Task Sends_The_Receipt()
{
    using var api = new MyApiWithFakeMail();
    // ...
}
```

Each instance is one more startup, so keep them to the tests that need them.

## Logs

With `CaptureLogs` on, `Logs` is a `LogCapture` (from `XUnitAssured.Core`) that records every entry the API writes, at every level, without changing what the console shows. A shared host sees every test's entries, so clear it right before the action under test:

```csharp
public sealed class MyApi : ApiFixture<Program>
{
    protected override bool CaptureLogs => true;
}

[Fact]
public async Task Refused_Access_Is_Logged()
{
    Fixture.Logs.Clear();

    (await Given()
        .WithHttpClient(Fixture.ClientFor(("X-Test-User", "reader")))
        .ApiResource("/orders/1")
        .Delete()
        .ExecuteAsync())
    .Then().AssertStatusCode(403);

    Fixture.Logs.AssertLoggedOnce(predicate: l => l.Message.Contains("access denied"));
}
```

## Related

- [XUnitAssured.Http](https://www.nuget.org/packages/XUnitAssured.Http) — the HTTP DSL this fixture plugs into.
- [XUnitAssured.Playwright.AspNetCore](https://www.nuget.org/packages/XUnitAssured.Playwright.AspNetCore) — the same idea for browser tests: the API on a real port and the built front-end on another.
