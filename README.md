# XUnitAssured.Net

[![CI](https://github.com/andrewBezerra/xunit-assured-net/actions/workflows/ci.yml/badge.svg)](https://github.com/andrewBezerra/xunit-assured-net/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/XUnitAssured.Core.svg?label=nuget)](https://www.nuget.org/packages/XUnitAssured.Core)
[![NuGet downloads](https://img.shields.io/nuget/dt/XUnitAssured.Core.svg?label=downloads)](https://www.nuget.org/profiles/AndrewBezerra)
[![.NET](https://img.shields.io/badge/.NET-8%20%7C%209%20%7C%2010-512BD4)](https://dotnet.microsoft.com/)
[![License: Apache-2.0](https://img.shields.io/badge/License-Apache--2.0-blue.svg)](LICENSE.md)

XUnitAssured is a .NET integration testing framework for describing and validating end-to-end scenarios that span HTTP APIs, messaging systems and browser workflows in distributed applications.

Built on xUnit and Playwright, with AI-assisted test generation through the Model Context Protocol (MCP), so Copilot, Claude and other assistants can scaffold tests for you.

## What a scenario looks like

Most integration tests check one boundary at a time. The interesting bugs live between them: the API answered `201`, but did the event reach the topic, and did the user actually see the result? XUnitAssured describes that whole path as one scenario:

```csharp
var orderId = 0;
await using var browser = await ui.OpenPageAsync();

var assertions = await Given(api, kafka, browser)
    // HTTP: create the order through the API
    .ApiResource("/api/orders")
    .Post(new { customerId = 42, total = 99.90m })
    .Validate(response =>
    {
        response.StatusCode.ShouldBe(201);
        orderId = response.JsonPath<int>("$.id");
    })

    // Kafka: the API must have published the event
    .And().On()
    .Topic("orders.created")
    .Consume()
    .ValidateMessage<OrderCreated>(message =>
    {
        message.OrderId.ShouldBe(orderId);
        message.Status.ShouldBe("Created");
    })

    // Browser: and the order must be visible to the user
    .And()
    .NavigateTo(() => $"/orders/{orderId}")

    .ExecuteAsync();

assertions
    .Then()
    .AssertUrlContains($"/orders/{orderId}")
    .AssertTextContainsByTestId("order-status", "Created");
```

**One `await`, at the end.** The chain describes the scenario; `ExecuteAsync()` carries it out, running each step in order. `Execute()` is still there as a thin blocking wrapper, so an existing suite keeps working.

The chain ends where it is: the last step was a browser step, so `ExecuteAsync()` is the browser one. Each package's chain methods return their own scenario type, so the compiler picks the builder from the receiver — three packages referenced, and nothing to disambiguate.

**Why the last URL is a function.** While the chain is being written, nothing has run, so `orderId` is still zero — `$"/orders/{orderId}"` would be built from it right there. A lambda is read later, after the step that produces it has run. The checks do not need this: they are already lambdas.

`api`, `kafka` and `ui` are ordinary xUnit fixtures; `Given(api, kafka, browser)` hands everything they provide to one scenario. Each leg is a *step*; steps share one context, so a value extracted from the API response drives the Kafka assertion and the page the browser opens. This exact scenario is compiled against the DSL on every build ([`CrossScenarioExampleTests.cs`](src/XUnitAssured.Tests/CrossScenarioExampleTests.cs)), so the README cannot drift from the API.

## What it does

- **One DSL across boundaries** — `Given().When().Then()` over HTTP, Kafka, RabbitMQ and the browser, with steps that share state (`SaveStep`, `Steps["name"]`, extracted values).
- **HTTP** — full CRUD, JSON path assertions over objects and lists (`$[*].id`), header, `Set-Cookie` and Problem Details assertions, `ExtractAsync` to arrange data in one line, and authentication applied once from `testsettings.json`: Bearer, Basic, OAuth2, API key, client certificate (mTLS), custom headers. `ApiFixture` hosts your own ASP.NET Core API in memory, with a client per identity and one instance for the whole suite.
- **Kafka** — produce and consume single messages and batches, headers and keys, SASL/PLAIN, SCRAM, SSL and mTLS, Schema Registry. Consume steps skip the consumer-group join, so a consume costs milliseconds instead of seconds.
- **RabbitMQ** — publish and consume single messages and batches, declare queues, exchanges and bindings, reject to a dead-letter exchange; a publish that reaches no queue fails instead of passing in silence.
- **Browser** — clicks, fills, checks, navigation and screenshots on Playwright, with locators by role, label, test id, text and CSS, and assertions that read like the DSL. The page's network too: intercept a route and prove it answered (`InterceptRoute`, `AssertIntercepted`), or call the API from inside the page with its cookies (`FetchFromPage`). `BrowserAppFixture` starts your own ASP.NET Core API and front-end inside `dotnet test`.
- **What the system did besides answering** — `LogCapture` records what it logged (message, exception, structured values and scopes, so a leaked value has nowhere to hide), and `OutboundHttpCapture` stands in for the outside services it calls and records every request.
- **AI-assisted authoring** — an MCP server with 10 tools that translate Playwright Inspector recordings into the DSL and scaffold HTTP and Kafka tests from your editor (GitHub Copilot, Claude Code, VS Code and any MCP client).
- **Diagnostics when things fail** — status codes, broker logs, exception detail and, for browser steps, a screenshot at the moment of failure.
- **Modular** — install only the packages you need; each targets `net8.0` through `net10.0`, except `XUnitAssured.Playwright.AspNetCore`, which needs .NET 10.

## Supported today

The definition above is where the project is going. This is what it ships now:

| Boundary | Supported | Test runner |
|---|---|---|
| HTTP APIs | Any REST/JSON API (`HttpClient`/Flurl, `WebApplicationFactory` for in-process tests) | xUnit |
| Messaging systems | Apache Kafka (Confluent client), RabbitMQ (official AMQP client) | xUnit |
| Browser workflows | Microsoft Playwright (Chromium, Firefox, WebKit) | xUnit |

## Roadmap

In order of intent, not of promise:

1. **Azure Service Bus**, the next messaging system behind Kafka and RabbitMQ.
2. **gRPC** alongside HTTP.

## Packages

### Core Packages

| Package | Version | Description |
|---------|---------|-------------|
| **XUnitAssured.Core** | [![NuGet](https://img.shields.io/nuget/v/XUnitAssured.Core.svg?label=)](https://www.nuget.org/packages/XUnitAssured.Core) | Core abstractions, DSL infrastructure, DI support (`DITestFixture`), `ValidationBuilder`, and BDD extensions |

### Protocol Packages

| Package | Version | Description |
|---------|---------|-------------|
| **XUnitAssured.Http** | [![NuGet](https://img.shields.io/nuget/v/XUnitAssured.Http.svg?label=)](https://www.nuget.org/packages/XUnitAssured.Http) | HTTP/REST API testing — fluent DSL, authentication handlers, JSON path assertions, schema validation |
| **XUnitAssured.Http.AspNetCore** | [![NuGet](https://img.shields.io/nuget/v/XUnitAssured.Http.AspNetCore.svg?label=)](https://www.nuget.org/packages/XUnitAssured.Http.AspNetCore) | HTTP tests against your own ASP.NET Core API — `ApiFixture` hosts it in memory with a shared client, a client per identity, clients with and without cookies, its services and its logs |
| **XUnitAssured.Kafka** | [![NuGet](https://img.shields.io/nuget/v/XUnitAssured.Kafka.svg?label=)](https://www.nuget.org/packages/XUnitAssured.Kafka) | Apache Kafka integration testing — produce/consume, batch operations, authentication, Schema Registry support |
| **XUnitAssured.RabbitMq** | [![NuGet](https://img.shields.io/nuget/v/XUnitAssured.RabbitMq.svg?label=)](https://www.nuget.org/packages/XUnitAssured.RabbitMq) | RabbitMQ integration testing — publish/consume steps on the official async client, queues and exchanges, AMQP headers |
| **XUnitAssured.Playwright** | [![NuGet](https://img.shields.io/nuget/v/XUnitAssured.Playwright.svg?label=)](https://www.nuget.org/packages/XUnitAssured.Playwright) | Playwright UI testing — fluent DSL for browser interactions, multiple locator strategies, screenshots, and assertions |
| **XUnitAssured.Playwright.AspNetCore** | [![NuGet](https://img.shields.io/nuget/v/XUnitAssured.Playwright.AspNetCore.svg?label=)](https://www.nuget.org/packages/XUnitAssured.Playwright.AspNetCore) | Browser tests against your own ASP.NET Core API — `BrowserAppFixture` hosts the API and your built front-end on real ports, so they run with `dotnet test` alone (.NET 10) |

### Tooling

| Package | Version | Description |
|---------|---------|-------------|
| **XUnitAssured.Mcp** | [![NuGet](https://img.shields.io/nuget/v/XUnitAssured.Mcp.svg?label=)](https://www.nuget.org/packages/XUnitAssured.Mcp) | MCP server for AI-assisted test generation — install via `dnx XUnitAssured.Mcp` or `dotnet tool install XUnitAssured.Mcp` |

## Quick Start

Every test in this section is compiled against the DSL on every build ([`ReadmeQuickStartExamplesTests.cs`](src/XUnitAssured.Tests/ReadmeQuickStartExamplesTests.cs)); the codegen block is the one exception, since it shows raw Playwright code beside its translation. They use `ExecuteAsync()`; the blocking `Execute()` still works if your suite is synchronous. `.When()` is optional: `Given()...ExecuteAsync()` and `Given()...When().ExecuteAsync()` describe the same scenario.

### HTTP Testing

```bash
dotnet add package XUnitAssured.Http
```

A fixture tells the DSL how to reach your API. This one reads the `http` section of `testsettings.json`, described under Configuration below:

```csharp
using XUnitAssured.Core.Abstractions;
using XUnitAssured.Core.Configuration;
using XUnitAssured.Http.Configuration;

public class MyApiFixture : IHttpClientProvider, IHttpClientAuthProvider
{
    private readonly HttpSettings _http = TestSettings.Load().GetHttpSettings()!;
    public HttpClient CreateClient() => new() { BaseAddress = new Uri(_http.BaseUrl!) };
    public HttpAuthConfig? GetAuthenticationConfig() => _http.Authentication;
}
```

```csharp
using XUnitAssured.Http.Extensions;
using XUnitAssured.Http.Testing;

public class MyApiTests : HttpTestBase<MyApiFixture>, IClassFixture<MyApiFixture>
{
    public MyApiTests(MyApiFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Get_Users_Returns_Success()
    {
        var assertions = await Given()
            .ApiResource("/api/users")
            .Get()
            .ExecuteAsync();

        assertions
            .Then()
            .AssertStatusCode(200)
            .AssertJsonPath<string>("$.name", value => value == "John", "Name should be John");
    }
}
```

### HTTP tests against your own API

```bash
dotnet add package XUnitAssured.Http.AspNetCore
```

`ApiFixture<TProgram>` hosts your API in memory with `WebApplicationFactory` and gives `Given()` its client. Share one instance across the suite with a collection fixture: each instance starts the API once, migrations included, and in a real consumer suite an `IClassFixture` per class spent about 70 of 118 seconds starting it.

```csharp
using XUnitAssured.Http.AspNetCore;

public sealed class MyApi : ApiFixture<Program>
{
    protected override void ConfigureApi(IWebHostBuilder api) => api.UseEnvironment("Testing");

    protected override void ConfigureClient(HttpClient client) =>
        client.DefaultRequestHeaders.Add("X-Test-User", "admin");
}

[CollectionDefinition("API")]
public sealed class ApiCollection : ICollectionFixture<MyApi>;

[Collection("API")]
public class OrderTests(MyApi api) : HttpTestBase<MyApi>(api)
{
    [Fact]
    public async Task Reader_Cannot_Delete()
    {
        var assertions = await Given()
            .WithHttpClient(Fixture.ClientFor(("X-Test-User", "reader")))
            .ApiResource("/orders/1")
            .Delete()
            .ExecuteAsync();

        assertions.Then().AssertStatusCode(403);
    }
}
```

See the [package README](src/XUnitAssured.Http.AspNetCore/README.md) for clients with and without cookies, the API's services, its logs, and when a test needs its own instance.

### HTTP Authentication Examples

```csharp
// Bearer token
var bearer = await Given().ApiResource("/api/secure")
    .WithBearerToken("my-jwt-token")
    .Get()
    .ExecuteAsync();
bearer.Then().AssertStatusCode(200);

// Basic auth
var basic = await Given().ApiResource("/api/secure")
    .WithBasicAuth("username", "password")
    .Get()
    .ExecuteAsync();
basic.Then().AssertStatusCode(200);

// API key, in a header or the query string
var apiKey = await Given().ApiResource("/api/secure")
    .WithApiKey("X-API-Key", "my-api-key", ApiKeyLocation.Header)
    .Get()
    .ExecuteAsync();
apiKey.Then().AssertStatusCode(200);

// OAuth2 client credentials
var oauth = await Given().ApiResource("/api/secure")
    .WithOAuth2("https://auth.example.com/token", "client-id", "client-secret")
    .Get()
    .ExecuteAsync();
oauth.Then().AssertStatusCode(200);
```

Behavior assertions — headers, cookies, Problem Details, lists in JSON paths — and capturing logs and outbound calls are in the [HTTP package README](src/XUnitAssured.Http/README.md).

### Playwright UI Testing

```bash
dotnet add package XUnitAssured.Playwright
```

```csharp
using XUnitAssured.Playwright.Extensions;
using XUnitAssured.Playwright.Testing;

public class MyUiTests : PlaywrightTestBase<PlaywrightTestFixture>, IClassFixture<PlaywrightTestFixture>
{
    public MyUiTests(PlaywrightTestFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Login_Should_Navigate_To_Dashboard()
    {
        var assertions = await Given()
            .NavigateTo("/login")
            .FillByLabel("Email", "user@test.com")
            .FillByLabel("Password", "secret")
            .ClickByRole(AriaRole.Button, "Sign in")
            .ExecuteAsync();

        assertions
            .Then()
            .AssertSuccess()
            .AssertUrl("/dashboard");
    }
}
```

### Browser tests against your own API

```bash
dotnet add package XUnitAssured.Playwright.AspNetCore
```

`BrowserAppFixture<TProgram>` starts your API on a real port (`WebApplicationFactory` + Kestrel), serves your built front-end on another, and launches the browser — no terminals to start by hand, so browser tests run in CI. Two ports means two origins, so SameSite, cookie `Path` and CORS with credentials behave as in production.

```csharp
public sealed class MyApp : BrowserAppFixture<Program>
{
    protected override string? AppDirectory => "../../../../my-app/dist";
}
```

See the [package README](src/XUnitAssured.Playwright.AspNetCore/README.md) for ports, CORS and arranging data through `Api.CreateClient()`.

### Playwright Codegen Integration

Record tests with Playwright Inspector and translate to XUnitAssured DSL:

```csharp
// Playwright Inspector output:
await page.GetByRole(AriaRole.Button, new() { Name = "Click me" }).ClickAsync();
await page.GetByLabel("Email").FillAsync("user@test.com");

// XUnitAssured DSL (auto-translated):
.ClickByRole(AriaRole.Button, "Click me")
.FillByLabel("Email", "user@test.com")
```

### Kafka Testing

```bash
dotnet add package XUnitAssured.Kafka
```

```csharp
using XUnitAssured.Kafka.Extensions;
using XUnitAssured.Kafka.Testing;

public class MyKafkaTests : KafkaTestBase<KafkaClassFixture>, IClassFixture<KafkaClassFixture>
{
    public MyKafkaTests(KafkaClassFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Produce_And_Consume_Message()
    {
        var topic = GenerateUniqueTopic("my-test");

        await Given()
            .Topic(topic)
            .Produce("Hello, Kafka!")
            .ExecuteAsync();

        var assertions = await Given()
            .Topic(topic)
            .Consume()
            .WithGroupId($"test-{Guid.NewGuid():N}")
            .ExecuteAsync();

        assertions
            .Then()
            .AssertSuccess()
            .AssertMessage<string>(msg => msg.ShouldBe("Hello, Kafka!"));
    }
}
```

### Kafka Batch Operations

```csharp
var topic = GenerateUniqueTopic("my-batch");
var messages = new[] { "one", "two", "three", "four", "five" };

var produced = await Given()
    .Topic(topic)
    .ProduceBatch(messages)
    .ExecuteAsync();
produced.Then().AssertSuccess().AssertBatchCount(5);

var consumed = await Given()
    .Topic(topic)
    .ConsumeBatch(5)
    .WithGroupId($"test-{Guid.NewGuid():N}")
    .ExecuteAsync();
consumed.Then().AssertSuccess().AssertBatchCount(5);
```

### Kafka Authentication Examples

```csharp
// SASL/PLAIN
var saslPlain = await Given().Topic("my-topic")
    .Produce("message")
    .WithBootstrapServers("localhost:29093")
    .WithAuth(auth => auth.UseSaslPlain("user", "password", useSsl: false))
    .ExecuteAsync();
saslPlain.Then().AssertSuccess();

// SSL (one-way)
var ssl = await Given().Topic("my-topic")
    .Produce("message")
    .WithBootstrapServers("localhost:29096")
    .WithAuth(auth => auth.UseSsl("certs/ca-cert.pem"))
    .ExecuteAsync();
ssl.Then().AssertSuccess();

// Mutual TLS
var mtls = await Given().Topic("my-topic")
    .Produce("message")
    .WithBootstrapServers("localhost:29097")
    .WithAuth(auth => auth.UseMutualTls("client-cert.pem", "client-key.pem", "ca-cert.pem"))
    .ExecuteAsync();
mtls.Then().AssertSuccess();
```

### RabbitMQ Testing

```bash
dotnet add package XUnitAssured.RabbitMq
```

```csharp
using XUnitAssured.RabbitMq.Extensions;

[Fact]
public async Task Publish_And_Consume_Message()
{
    await Given()
        .Queue("orders")
        .Publish(new { id = 42, status = "Created" })
        .ExecuteAsync();

    var assertions = await Given()
        .Queue("orders")
        .Consume()
        .WithTimeout(TimeSpan.FromSeconds(5))
        .ExecuteAsync();

    assertions
        .Then()
        .AssertSuccess()
        .AssertMessage<Order>(order => order.Status.ShouldBe("Created"));
}
```

Topology, batches, dead-letter, and why prefetch is not a verb: see the [RabbitMQ package README](src/XUnitAssured.RabbitMq/README.md).

## Configuration: `testsettings.json`

Every protocol package reads its connection details and credentials from one file at the root of your test project, so tests never hard-code URLs, brokers or tokens:

```jsonc
{
  // "Local": in-process services (WebApplicationFactory, a Kafka in Docker).
  // "Remote": deployed services (staging, production).
  "testMode": "Remote",

  // Optional. Also settable with the TEST_ENV environment variable.
  "environment": "staging",

  "http": {
    "baseUrl": "${ENV:API_URL}",          // ${ENV:NAME} is replaced by the variable's value
    "timeout": 60,
    "defaultHeaders": { "Accept": "application/json" },
    "authentication": {
      "type": "Bearer",                     // None | Basic | Bearer | ApiKey | OAuth2 | CustomHeader | Certificate
      "bearer": { "token": "${ENV:API_TOKEN}" }
    }
  },

  "kafka": {
    "bootstrapServers": "localhost:9092",
    "groupId": "my-tests",
    "securityProtocol": "Plaintext",       // Plaintext | Ssl | SaslPlaintext | SaslSsl
    "authentication": { "type": "None" }   // None | SaslPlain | SaslScram256 | SaslScram512 | Ssl | MutualTls
  },

  "rabbitmq": {
    "connectionUri": "amqp://guest:guest@127.0.0.1:5672/",  // the last segment is the virtual host
    "consumeTimeoutSeconds": 30
  }
}
```

Comments are allowed. The full set of keys, with every authentication variant spelled out, is in the samples: [`testsettings.json` for HTTP](src/XUnitAssured.Http.Samples.Remote.Test/testsettings.json) and [for Kafka](src/XUnitAssured.Kafka.Samples.Remote.Test/testsettings.json).

**Where it is looked for.** The current directory and up to three parents — which reaches your project folder from `bin/<Configuration>/<tfm>`, so no `CopyToOutputDirectory` is needed. To point somewhere else, set `TESTSETTINGS_PATH=/path/to/file.json`.

**Per-environment files.** With `TEST_ENV=staging` (or `"environment": "staging"`), `testsettings.staging.json` is loaded instead of `testsettings.json`. Keep secrets out of the file with `${ENV:...}`.

**Kafka and RabbitMQ on Windows with Docker or Podman.** Use `127.0.0.1` rather than `localhost` for both — for RabbitMQ, each connection otherwise pays about a minute before falling back to IPv4. For Kafka, prefer `"bootstrapServers": "127.0.0.1:9092"` over `localhost:9092`. On Windows, `localhost` resolves to IPv6 `::1` first, the container only forwards IPv4, and the Kafka client gives up on each attempt only after ~20 s — a test that should fail instantly hangs, and one that should pass may time out. The broker must also advertise the same address: with the official image, set `KAFKA_ADVERTISED_LISTENERS=PLAINTEXT://127.0.0.1:9092`, otherwise its metadata sends the client back to `localhost`. The compose files under `src/XUnitAssured.Kafka.Samples.Remote.Test/docker` are set up this way.

**How it reaches your tests.** A fixture loads the file once and hands it to the DSL:

```csharp
// HTTP: a fixture that implements IHttpClientProvider (and IHttpClientAuthProvider
// to have the configured authentication applied to every request).
public class ApiFixture : IHttpClientProvider, IHttpClientAuthProvider
{
    private readonly HttpSettings _http = TestSettings.Load().GetHttpSettings()!;
    public HttpClient CreateClient() => new() { BaseAddress = new Uri(_http.BaseUrl!) };
    public HttpAuthConfig? GetAuthenticationConfig() => _http.Authentication;
}

// Then in a test:
Given(fixture).ApiResource("/api/orders").Get() ...

// Kafka: KafkaClassFixture already does this for the "kafka" section.
public class OrderTests : KafkaTestBase<KafkaClassFixture>, IClassFixture<KafkaClassFixture> { ... }
```

The reference implementation of an HTTP fixture is [`HttpSamplesRemoteFixture.cs`](src/XUnitAssured.Http.Samples.Remote.Test/HttpSamplesRemoteFixture.cs).

**Combining providers.** Anything that implements `ITestContextSeeder` can be passed to `Given(...)`, alone or together: an HTTP fixture (every `IHttpClientProvider` is one), `KafkaClassFixture`, a `PlaywrightTestBase` test, or the page session returned by `PlaywrightTestFixture.OpenPageAsync()`. `Given(api, kafka, browser)` is how the scenario at the top of this README gets all three.

**Browser settings too.** They live in a `playwright` section of the same file, alongside `http` and `kafka`:

```json
{
  "playwright": {
    "baseUrl": "https://app.example.com",
    "headless": true,
    "browser": "Chromium",
    "defaultTimeout": 30000,
    "screenshotOnFailure": true
  }
}
```

> **If your project still has `playwrightsettings.json`,** it is still read, and the run prints once where its contents should move to. It will be removed in a future major. The old file also required `CopyToOutputDirectory`; the section does not.

> **One more name you may meet in the code.** `httpsettings.json` is a fallback read only when an `HttpRequestStep` runs without a fixture or explicit authentication. Kafka has no such file: a Kafka step run without a fixture reads the same `kafka` section of `testsettings.json` the fixture does.

## Architecture

```
                                XUnitAssured.Core
                  (DSL + Abstractions + DI + ValidationBuilder)
          ↓                  ↓                   ↓                    ↓
  XUnitAssured.Http   XUnitAssured.Kafka   XUnitAssured.RabbitMq   XUnitAssured.Playwright
  (REST APIs)         (Kafka)              (RabbitMQ)              (Browser UI)
          ↓                                                               ↓
  XUnitAssured.Http.AspNetCore                           XUnitAssured.Playwright.AspNetCore
  (your API in memory)                                   (your API + front-end on real ports)

  XUnitAssured.Mcp — MCP server that writes tests in the DSL above (AI-assisted authoring)
```

**What the design optimises for:**
- **Reference only what you test.** Each boundary is its own package on a small core, so an API-only suite never pulls in Playwright or a Kafka client.
- **Packages compose without ambiguity.** Several packages in one scenario is the point, so their verbs must not collide — the rule below is how.
- **Describe first, run once.** A chain is a description and `ExecuteAsync()` runs it, which is what lets one step's output feed the next step's input.
- **Failures explain themselves.** Status codes, broker logs, exception detail and screenshots travel with the result.

### Adding a protocol package

One rule, and it exists for a reason worth stating: **a protocol package declares its verbs as
extensions on its own scenario type, and only the entry verb takes `ITestScenario`.**

```csharp
// The entry verb, and the only one that takes the untyped scenario
public static IRabbitScenario Queue(this ITestScenario scenario, string name)

// Everything after it takes the typed scenario
public static IRabbitScenario Consume(this IRabbitScenario scenario)
public static IRabbitScenario WithTimeout(this IRabbitScenario scenario, TimeSpan timeout)
```

Two messaging packages want the same words. `Consume`, `Produce`, `WithTimeout` and
`ValidateMessage` are the vocabulary of messaging, not of Kafka. Declared on `ITestScenario` in
both packages, every one of those calls becomes ambiguous the moment a test project references
both, and referencing several packages in one scenario is the point of this framework. Declared
on the typed scenario, the compiler picks by receiver and there is nothing to disambiguate.

Entry verbs do not collide because each broker names its own thing: `Topic` for Kafka, `Queue`
or `Exchange` for RabbitMQ.

> **The case this does not cover.** A chain stored in a variable typed `ITestScenario` and then
> continued binds to whichever package still declares that verb on `ITestScenario`, silently.
> Name the package's scenario type, or use `var`. The same applies to `Validate`, which the HTTP
> package declares with an overload accepting any result.

Kafka predates this rule and still declares its verbs on `ITestScenario`. That is why the rule
is written for new packages rather than claimed as true of all of them; moving the existing ones
is a breaking change, and it is the only thing that would close the gap above.

## Sample Projects

| Project | What it covers |
|---------|----------------|
| `XUnitAssured.Http.Samples.Local.Test` | HTTP against a local `SampleWebApi` (WebApplicationFactory): CRUD, every authentication scheme (Bearer, Basic, API key, OAuth2, certificate, custom header), hybrid validation |
| `XUnitAssured.Http.Samples.Remote.Test` | HTTP against a deployed API, configured from `testsettings.json` |
| `XUnitAssured.Kafka.Samples.Remote.Test` | Kafka against Docker or remote clusters: strings, JSON, headers, keys, batches, and each authentication mode (plaintext, SASL/PLAIN, SCRAM-256/512, SSL, mTLS) |
| `XunitAssured.PlayWright.Samples.Local.Test` | Playwright against a local Blazor `SampleWebApp`: navigation, forms, validation messages, data tables |
| `XUnitAssured.Playwright.Samples.Remote.Test` | Playwright against a deployed web app, including a full CRUD flow |

## MCP Server (AI-Assisted Test Generation)

XUnitAssured includes an MCP (Model Context Protocol) server that integrates with GitHub Copilot Chat, VS Code, and any MCP-compatible AI client. It provides **10 tools** for test generation and code translation.

### Available Tools

| Tool | Description |
|------|-------------|
| `translate_playwright_to_dsl` | Translates Playwright C# Inspector code → XUnitAssured fluent DSL |
| `translate_playwright_to_test` | Generates a complete Given/When/Then test from Playwright code |
| `list_xunitassured_dsl_methods` | Lists all Playwright DSL methods with equivalents |
| `generate_http_test` | Scaffolds an HTTP test method (GET, POST, PUT, DELETE) |
| `generate_http_crud_tests` | Generates 5 CRUD test methods for a REST resource |
| `list_http_dsl_methods` | Lists all HTTP DSL methods (request, auth, assert) |
| `generate_kafka_produce_test` | Scaffolds a Kafka produce test method |
| `generate_kafka_consume_test` | Scaffolds a Kafka consume test method |
| `generate_kafka_produce_consume_test` | Generates a round-trip produce→consume test |
| `list_kafka_dsl_methods` | Lists all Kafka DSL methods (produce, consume, auth, assert) |

### Setup

#### Option A — Install from NuGet (recommended)

Requires [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) or later (`dnx` command).

Add to your `.mcp.json` (repo root, `~/.mcp.json`, or `.vscode/mcp.json`):

```json
{
  "servers": {
    "xunitassured": {
      "type": "stdio",
      "command": "dnx",
      "args": ["XUnitAssured.Mcp@6.5.0", "--yes"]
    }
  }
}
```

That's it — `dnx` downloads and runs the MCP server automatically. No build needed.

Building it from source instead is described in the [MCP package README](src/XunitAssured.MCP/README.md#option-c---from-source-contributors).

#### Restart your IDE

Visual Studio / VS Code must be **restarted** after creating or editing `.mcp.json` for the MCP server to be detected.

#### Verify

In GitHub Copilot Chat, the XUnitAssured tools should appear as available. Try:

> "List all XUnitAssured HTTP DSL methods"

### Usage in Copilot Chat

> "Generate CRUD tests for /api/products with fields name:string, price:decimal"

> "Translate this Playwright code to XUnitAssured DSL: `await page.GetByRole(AriaRole.Button, new() { Name = \"Submit\" }).ClickAsync();`"

> "Generate a Kafka produce-consume round-trip test for the orders topic"

## What's New

**6.5.0** — `XUnitAssured.Http.AspNetCore`: `ApiFixture` hosts your own ASP.NET Core API in memory, with a client per identity, clients with and without cookies, its services and its logs — one instance for the whole suite. No changes to existing packages.

**6.4.2** — documentation: the README says only what is true and shows what 6.2 to 6.4 added; every Quick Start test is now compiled on each build. No code changes.

The full history is in **[CHANGELOG.md](https://github.com/andrewBezerra/xunit-assured-net/blob/main/CHANGELOG.md)**. Coming from 5.x? Start with **[UPGRADING.md](https://github.com/andrewBezerra/xunit-assured-net/blob/main/UPGRADING.md)**.

## Contributing

Contributions are welcome — issues and pull requests alike.

```bash
dotnet build src/XUnitAssured.Net.sln
dotnet test src/XUnitAssured.Tests --filter "Requires!=Network&Requires!=Broker&Requires!=Browser"
```

The filter skips the tests that need the network, a running broker or an installed browser; CI runs those separately — the broker ones against its own RabbitMQ service, the browser ones after installing Chromium (`pwsh src/XUnitAssured.Tests/bin/Debug/net10.0/playwright.ps1 install chromium` does the same locally). To run the Kafka samples locally, the compose files under `src/XUnitAssured.Kafka.Samples.Remote.Test/docker` start a broker per authentication mode. The certificates and keys committed there are **test-only fixtures** for those local brokers, not credentials for anything real.

Adding a protocol package? Read [Adding a protocol package](#adding-a-protocol-package) first.

**Releases.** Merging to `main` publishes, so every pull request raises `<Version>` in `src/Directory.Build.props` and adds `docs/releases/<version>.md` with what ships. The pull request check fails without both; on merge, the release workflow creates the tag and the GitHub release from that file, then publishes to NuGet after a manual approval.

## License

This project is licensed under the Apache License 2.0 - see the [LICENSE.md](LICENSE.md) file for details.

## Author

**Carlos Andrew Costa Bezerra**
- GitHub: [@andrewBezerra](https://github.com/andrewBezerra)
- LinkedIn: [andrew-bezerra](https://www.linkedin.com/in/andrew-bezerra/)

## Links

- [GitHub Repository](https://github.com/andrewBezerra/xunit-assured-net)
- [NuGet Packages](https://www.nuget.org/profiles/AndrewBezerra)
- [Changelog](https://github.com/andrewBezerra/xunit-assured-net/blob/main/CHANGELOG.md)
- [Report Issues](https://github.com/andrewBezerra/xunit-assured-net/issues)
