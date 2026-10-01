# XUnitAssured.Net

[![CI](https://github.com/andrewBezerra/xunit-assured-net/actions/workflows/ci.yml/badge.svg)](https://github.com/andrewBezerra/xunit-assured-net/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/XUnitAssured.Core.svg?label=nuget)](https://www.nuget.org/packages/XUnitAssured.Core)
[![License: Apache-2.0](https://img.shields.io/badge/License-Apache--2.0-blue.svg)](LICENSE.md)

XUnitAssured is a .NET integration testing framework for describing and validating end-to-end scenarios that span HTTP APIs, messaging systems and browser workflows in distributed applications.

Built on xUnit and Playwright, with AI-assisted test generation through the Model Context Protocol (MCP), so Copilot, Claude and other assistants can scaffold tests for you.

## What a scenario looks like

Most integration tests check one boundary at a time. The interesting bugs live between them: the API answered `201`, but did the event reach the topic, and did the user actually see the result? XUnitAssured describes that whole path as one scenario:

```csharp
var orderId = 0;
await using var browser = await ui.OpenPageAsync();

var scenario = Given(api, kafka, browser);

scenario
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
    .NavigateTo($"/orders/{orderId}");

PlaywrightBddExtensions.Execute(scenario)
    .Then()
    .AssertUrlContains($"/orders/{orderId}")
    .AssertTextContainsByTestId("order-status", "Created");
```

One rough edge, stated plainly: when the Http, Kafka and Playwright packages are all referenced, `Execute()` is ambiguous — each package defines its own — so the last leg names the package. A single entry point is the first item on the roadmap.

`api`, `kafka` and `ui` are ordinary xUnit fixtures; `Given(api, kafka, browser)` hands everything they provide to one scenario. Each leg is a *step*; steps share one context, so a value extracted from the API response drives the Kafka assertion and the page the browser opens. This exact scenario is compiled against the DSL on every build ([`CrossScenarioExampleTests.cs`](src/XUnitAssured.Tests/CrossScenarioExampleTests.cs)), so the README cannot drift from the API.

## What it does

- **One DSL across boundaries** — `Given().When().Then()` over HTTP, Kafka and the browser, with steps that share state (`SaveStep`, `Steps["name"]`, extracted values).
- **HTTP** — full CRUD, JSON path assertions, contract validation, and authentication applied once from `testsettings.json`: Bearer, Basic, OAuth2, API key, client certificate (mTLS), custom headers.
- **Kafka** — produce and consume single messages and batches, headers and keys, SASL/PLAIN, SCRAM, SSL and mTLS, Schema Registry. Consume steps skip the consumer-group join, so a consume costs milliseconds instead of seconds.
- **Browser** — clicks, fills, checks, navigation and screenshots on Playwright, with locators by role, label, test id, text and CSS, and assertions that read like the DSL.
- **AI-assisted authoring** — an MCP server with 10 tools that translate Playwright Inspector recordings into the DSL and scaffold HTTP and Kafka tests from your editor.
- **Diagnostics when things fail** — status codes, broker logs, exception detail and, for browser steps, a screenshot at the moment of failure.
- **Modular** — install only the packages you need; each targets `net8.0` through `net10.0`.

## Supported today

The definition above is where the project is going. This is what it ships now:

| Boundary | Supported | Test runner |
|---|---|---|
| HTTP APIs | Any REST/JSON API (`HttpClient`/Flurl, `WebApplicationFactory` for in-process tests) | xUnit |
| Messaging systems | Apache Kafka (Confluent client) | xUnit |
| Browser workflows | Microsoft Playwright (Chromium, Firefox, WebKit) | xUnit |

## Roadmap

In order of intent, not of promise:

1. **A single `Execute()`** so a cross-boundary scenario ends the same way regardless of which packages are referenced — today the last leg has to name its package. (Getting `HttpClient`, broker and page from one `Given(api, kafka, browser)` call already works.)
2. **A first-class asynchronous API** (`ExecuteAsync`, `CancellationToken` end to end) — the current `Execute()` blocks, which is the most common objection to the DSL. This is the v6 line.
3. **More messaging systems** — RabbitMQ and Azure Service Bus are the natural next ones behind Kafka.
4. **gRPC** alongside HTTP.
5. **One configuration file** — bring Playwright into `testsettings.json` and retire the per-package files, so a cross-boundary project is configured in one place.

## 📦 Packages

### Core Packages

| Package | Version | Description |
|---------|---------|-------------|
| **XUnitAssured.Core** | 5.1.0 | Core abstractions, DSL infrastructure, DI support (`DITestFixture`), `ValidationBuilder`, and BDD extensions |

### Protocol Packages

| Package | Version | Description |
|---------|---------|-------------|
| **XUnitAssured.Http** | 5.1.0 | HTTP/REST API testing — fluent DSL, authentication handlers, JSON path assertions, schema validation |
| **XUnitAssured.Kafka** | 5.1.0 | Apache Kafka integration testing — produce/consume, batch operations, authentication, Schema Registry support |
| **XUnitAssured.Playwright** | 5.1.0 | Playwright UI testing — fluent DSL for browser interactions, multiple locator strategies, screenshots, and assertions |

### Tooling

| Package | Version | Description |
|---------|---------|-------------|
| **XUnitAssured.Mcp** | 5.1.0 | MCP server for AI-assisted test generation — install via `dnx XUnitAssured.Mcp` or `dotnet tool install XUnitAssured.Mcp` |

## 🚀 Quick Start

### HTTP Testing

```bash
dotnet add package XUnitAssured.Http
```

```csharp
using XUnitAssured.Http.Extensions;
using XUnitAssured.Http.Testing;

public class MyApiTests : HttpTestBase<MyTestFixture>, IClassFixture<MyTestFixture>
{
    public MyApiTests(MyTestFixture fixture) : base(fixture) { }

    [Fact]
    public void Get_Users_Returns_Success()
    {
        Given()
            .ApiResource("/api/users")
            .Get()
        .When()
            .Execute()
        .Then()
            .AssertStatusCode(200)
            .AssertJsonPath<string>("$.name", value => value == "John", "Name should be John");
    }
}
```

### HTTP Authentication Examples

```csharp
// Bearer Token
Given().ApiResource("/api/secure")
    .WithBearerToken("my-jwt-token")
    .Get()
.When().Execute()
.Then().AssertStatusCode(200);

// Basic Auth
Given().ApiResource("/api/secure")
    .WithBasicAuth("username", "password")
    .Get()
.When().Execute()
.Then().AssertStatusCode(200);

// API Key (Header or Query)
Given().ApiResource("/api/secure")
    .WithApiKey("X-API-Key", "my-api-key", ApiKeyLocation.Header)
    .Get()
.When().Execute()
.Then().AssertStatusCode(200);

// OAuth2 Client Credentials
Given().ApiResource("/api/secure")
    .WithOAuth2ClientCredentials("https://auth.example.com/token", "client-id", "client-secret")
    .Get()
.When().Execute()
.Then().AssertStatusCode(200);
```

### Playwright UI Testing

```bash
dotnet add package XUnitAssured.Playwright
```

```csharp
using XUnitAssured.Playwright.Extensions;
using XUnitAssured.Playwright.Testing;

public class MyUiTests : PlaywrightTestBase<MyPlaywrightFixture>, IClassFixture<MyPlaywrightFixture>
{
    public MyUiTests(MyPlaywrightFixture fixture) : base(fixture) { }

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
    public void Produce_And_Consume_Message()
    {
        var topic = GenerateUniqueTopic("my-test");
        var groupId = $"test-{Guid.NewGuid():N}";

        // Produce
        Given()
            .Topic(topic)
            .Produce("Hello, Kafka!")
        .When()
            .Execute()
        .Then()
            .AssertSuccess();

        // Consume
        Given()
            .Topic(topic)
            .Consume()
            .WithGroupId(groupId)
        .When()
            .Execute()
        .Then()
            .AssertSuccess()
            .AssertMessage<string>(msg => msg.ShouldBe("Hello, Kafka!"));
    }
}
```

### Kafka Batch Operations

```csharp
// Produce batch
Given()
    .Topic("my-topic")
    .ProduceBatch(messages)
.When()
    .Execute()
.Then()
    .AssertSuccess()
    .AssertBatchCount(5);

// Consume batch
Given()
    .Topic("my-topic")
    .ConsumeBatch(5)
    .WithGroupId(groupId)
.When()
    .Execute()
.Then()
    .AssertSuccess()
    .AssertBatchCount(5);
```

### Kafka Authentication Examples

```csharp
// SASL/PLAIN
Given().Topic("my-topic")
    .Produce("message")
    .WithBootstrapServers("localhost:29093")
    .WithAuth(auth => auth.UseSaslPlain("user", "password", useSsl: false))
.When().Execute()
.Then().AssertSuccess();

// SSL (one-way)
Given().Topic("my-topic")
    .Produce("message")
    .WithBootstrapServers("localhost:29096")
    .WithAuth(auth => auth.UseSsl("certs/ca-cert.pem"))
.When().Execute()
.Then().AssertSuccess();

// Mutual TLS (mTLS)
Given().Topic("my-topic")
    .Produce("message")
    .WithBootstrapServers("localhost:29097")
    .WithAuth(auth => auth.UseMutualTls("client-cert.pem", "client-key.pem", "ca-cert.pem"))
.When().Execute()
.Then().AssertSuccess();
```

## ⚙️ Configuration: `testsettings.json`

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
  }
}
```

Comments are allowed. The full set of keys, with every authentication variant spelled out, is in the samples: [`testsettings.json` for HTTP](src/XUnitAssured.Http.Samples.Remote.Test/testsettings.json) and [for Kafka](src/XUnitAssured.Kafka.Samples.Remote.Test/testsettings.json).

**Where it is looked for.** The current directory and up to three parents — which reaches your project folder from `bin/<Configuration>/<tfm>`, so no `CopyToOutputDirectory` is needed. To point somewhere else, set `TESTSETTINGS_PATH=/path/to/file.json`.

**Per-environment files.** With `TEST_ENV=staging` (or `"environment": "staging"`), `testsettings.staging.json` is loaded instead of `testsettings.json`. Keep secrets out of the file with `${ENV:...}`.

**Kafka on Windows with Docker or Podman.** Prefer `"bootstrapServers": "127.0.0.1:9092"` over `localhost:9092`. On Windows, `localhost` resolves to IPv6 `::1` first, the container only forwards IPv4, and the Kafka client gives up on each attempt only after ~20 s — a test that should fail instantly hangs, and one that should pass may time out. The broker must also advertise the same address: with the official image, set `KAFKA_ADVERTISED_LISTENERS=PLAINTEXT://127.0.0.1:9092`, otherwise its metadata sends the client back to `localhost`. The compose files under `src/XUnitAssured.Kafka.Samples.Remote.Test/docker` are set up this way.

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

**The exception: Playwright.** Browser settings live in their own file, `playwrightsettings.json`, with PascalCase keys (`Headless`, `Browser`, `DefaultTimeout`, `ScreenshotOnFailure`, `RecordTrace`, …), found the same way or via `XUNITASSURED_PLAYWRIGHT_SETTINGS_PATH`. Unlike `testsettings.json`, it must be copied to the output directory — see the [Playwright sample](src/XunitAssured.PlayWright.Samples.Local.Test/playwrightsettings.json) and its `.csproj`. Folding it into `testsettings.json` is on the roadmap.

> **One more name you may meet in the code.** `httpsettings.json` is a fallback read only when an `HttpRequestStep` runs without a fixture or explicit authentication. Kafka has no such file: a Kafka step run without a fixture reads the same `kafka` section of `testsettings.json` the fixture does.

## 🏗️ Architecture

```
                    XUnitAssured.Core
          (DSL + Abstractions + DI + ValidationBuilder)
             ↓              ↓              ↓
  XUnitAssured.Http   XUnitAssured.Kafka   XUnitAssured.Playwright
  (REST API Testing)  (Kafka Testing)      (UI Testing)
                            ↑
                    XUnitAssured.Mcp
               (AI-Assisted Test Generation)
```

**Design Principles:**
- **SOLID**: Each package has a single responsibility
- **KISS**: Simple, straightforward APIs
- **DRY**: Reusable components across tests
- **YAGNI**: Only what you need, when you need it
- **Separation of Concerns**: Clear boundaries between HTTP, Kafka, Playwright, and Core

## 📚 Sample Projects

The repository includes comprehensive sample projects for both local and remote testing:

| Project | Description |
|---------|-------------|
| `XUnitAssured.Http.Samples.Local.Test` | HTTP tests against a local `SampleWebApi` (WebApplicationFactory) |
| `XUnitAssured.Http.Samples.Remote.Test` | HTTP tests against a deployed remote API |
| `XUnitAssured.Kafka.Samples.Remote.Test` | Kafka tests against local Docker or remote Kafka clusters |
| `XUnitAssured.Playwright.Samples.Local.Test` | Playwright UI tests against a local Blazor `SampleWebApp` |
| `XUnitAssured.Playwright.Samples.Remote.Test` | Playwright UI tests against a deployed remote web application |

### HTTP Sample Test Categories

- **SimpleIntegrationTests** — Basic GET/POST/PUT/DELETE operations
- **CrudOperationsTests** — Full CRUD lifecycle with JSON path assertions
- **BearerAuthTests** — Bearer token authentication
- **BasicAuthTests** — Basic authentication
- **ApiKeyAuthTests** — API Key via Header and Query parameter
- **OAuth2AuthTests** — OAuth2 flows (Client Credentials, Password)
- **CertificateAuthTests** — Certificate-based (mTLS) authentication
- **CustomHeaderAuthTests** — Custom header authentication
- **HybridValidationTests** — Mixed validation strategies
- **DiagnosticTests** — Connectivity and diagnostic tests

### Kafka Sample Test Categories

- **ProducerConsumerBasicTests** — Produce/consume strings, JSON, headers, batches, keys, timeouts
- **AuthenticationPlainTextTests** — Plaintext (no auth)
- **AuthenticationSaslPlainTests** — SASL/PLAIN
- **AuthenticationScramSha256Tests** — SASL/SCRAM-SHA-256
- **AuthenticationScramSha512Tests** — SASL/SCRAM-SHA-512
- **AuthenticationScramSha512SslTests** — SASL/SSL
- **AuthenticationTests** — SSL, mTLS, invalid credentials

### Playwright Sample Test Categories

- **HomePageTests** — Page navigation, title verification, element visibility
- **CounterPageTests** — Button clicks, state changes, counter increments
- **LoginPageTests** — Form fills, authentication flows, error validation
- **RegisterPageTests** — Multi-field forms, validation messages
- **NavigationTests** — Menu navigation, URL assertions, page transitions
- **WeatherPageTests** — Data table assertions, loading states
- **TodoCrudTests** — Full CRUD UI operations (create, read, update, delete)

## 🤖 MCP Server (AI-Assisted Test Generation)

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
      "args": ["XUnitAssured.Mcp@5.1.0", "--yes"]
    }
  }
}
```

That's it — `dnx` downloads and runs the MCP server automatically. No build needed.

#### Option B — From source (for contributors)

##### 1. Build the MCP server

```bash
cd src/XunitAssured.MCP
dotnet build -c Debug
```

##### 2. Configure `.mcp.json`

**Repo-level** (relative path, recommended for team use):

```json
{
  "servers": {
    "xunitassured": {
      "type": "stdio",
      "command": "dotnet",
      "args": ["run", "--no-build", "--project", "src/XunitAssured.MCP/XunitAssured.MCP.csproj"]
    }
  }
}
```

**Global** (absolute path to compiled `.exe`, faster):

```json
{
  "servers": {
    "xunitassured": {
      "type": "stdio",
      "command": "<full-path-to-repo>/src/XunitAssured.MCP/bin/Debug/net10.0/XUnitAssured.Mcp.exe",
      "args": []
    }
  }
}
```

> **Tip:** Pointing directly to the `.exe` is faster than `dotnet run` because it skips project resolution.

#### Restart your IDE

Visual Studio / VS Code must be **restarted** after creating or editing `.mcp.json` for the MCP server to be detected.

#### Verify

In GitHub Copilot Chat, the XUnitAssured tools should appear as available. Try:

> "List all XUnitAssured HTTP DSL methods"

### Usage in Copilot Chat

> "Generate CRUD tests for /api/products with fields name:string, price:decimal"

> "Translate this Playwright code to XUnitAssured DSL: `await page.GetByRole(AriaRole.Button, new() { Name = \"Submit\" }).ClickAsync();`"

> "Generate a Kafka produce-consume round-trip test for the orders topic"

## 🔄 Version History

### v5.1.0 (Current — cross-boundary scenarios from one call, fully compatible with 5.0.x)

Additive release; upgrading requires no code changes.

- **`Given(api, kafka, browser)`** — any fixture or per-test object that implements
  the new `ITestContextSeeder` can be passed to `Given(...)`, alone or combined, so a
  scenario spanning HTTP, Kafka and the browser is configured in one place.
  `KafkaClassFixture`, `PlaywrightTestBase` and the new
  `PlaywrightTestFixture.OpenPageAsync()` session implement it; every
  `IHttpClientProvider` is one automatically.
- **Kafka steps read `testsettings.json` without a fixture.** `KafkaSettings.Load()`
  used to return defaults regardless of configuration, which also meant the
  parameterless `WithSaslPlain()`, `WithSaslScram()` and `WithSsl()` always threw.
- **Playwright steps stop serialising the page on every step.** `PageContent` is
  fetched on first read; a step on a 500 KB page went from ~105 ms to ~6 ms.
- **Batch consume keeps its diagnostics on failure**, as the single consume already did.
- Every public member ships XML documentation; the packages build warning-free.
- Configuration guidance for Kafka in Docker/Podman on Windows (`127.0.0.1`, not `localhost`).

### v5.0.2 (Kafka performance, fully compatible with 5.0.1)

No API changed; upgrading requires no code changes.

- **Kafka — consume steps are ~14× faster.** A consume step no longer joins a
  consumer group. Joining cost a coordinator lookup, a rebalance and, on a
  default broker, a three-second `group.initial.rebalance.delay.ms` wait — paid
  on every step, for nothing, since steps never commit offsets. Partitions are
  now assigned directly, starting from the same offsets a subscription would
  use (a committed offset for the group wins; otherwise `AutoOffsetReset`).
  Measured against a local broker, one message per fresh topic and group:
  median **3 220 ms → 236 ms** per consume.
- **Kafka — `Produce` without a key works again.** A keyless message is valid
  Kafka (the broker picks the partition) and is what the quick start does, but
  since 5.0.0 the step rejected it before reaching the broker. Producing with a
  `null` key now goes through; the value is still required.

### v5.0.1 (Reliability fixes, fully compatible with 5.0.0)

No API changed; upgrading requires no code changes.

- **HTTP — configuration was silently dropped when a step was reconfigured.**
  `WithTimeout()` discarded the authentication and the custom `HttpClient`, so a
  call chain that set a timeout lost its credentials and simply received 401.
  Every fluent method now copies the step through a single constructor instead
  of rebuilding it field by field.
- **Kafka — verbose broker tracing is no longer forced on.** `Consume` enabled
  librdkafka's `Debug` output on its own and then reported the resulting broker
  chatter as errors. Tracing is opt-in again, and broker logs moved to the
  `BrokerLogs` diagnostic property.
- **HTTP — response headers are no longer split on commas**, which corrupted
  `Date`, `Set-Cookie` and any header whose value legitimately contains one.
- **Results — value conversion now understands** `Guid`, `DateTime`,
  `DateTimeOffset`, `TimeSpan`, enums and `Nullable<T>`, which previously
  returned `default` without explanation.
- **Failures carry diagnostics.** HTTP and Playwright failures keep the
  exception type and stack trace, and a failing Playwright step captures a
  screenshot when `ScreenshotOnFailure` is enabled.
- Removed debug output that printed on every `WebApplicationFactory` test.
- Packages now ship XML documentation, symbol packages (`.snupkg`) and
  SourceLink, so you can step into the framework while debugging.

### v5.0.0 (Core, Http, Kafka, Playwright, MCP)
- Added .NET 10 support across all packages
- Multi-target support: `net7.0`, `net8.0`, `net9.0`, `net10.0`
- Unified version across all packages (Core, Http, Kafka, Playwright)
- **XUnitAssured.Playwright** — New package for browser-based UI testing with fluent DSL
  - Multiple locator strategies: CSS, ARIA roles, labels, test IDs, placeholders, text, title
  - All interaction types: click, fill, check, hover, focus, select, press, type, drag, scroll
  - Screenshot capture, tracing, and Playwright Inspector integration (`RecordAndPause()`)
  - Codegen translator: converts Playwright Inspector output to XUnitAssured DSL
- **XUnitAssured.Mcp** — New MCP server for AI-assisted test generation
  - 10 tools: Playwright translation (3), HTTP scaffolding (3), Kafka scaffolding (4)
  - Integrates with GitHub Copilot Chat, VS Code, Claude Desktop, and any MCP client
  - stdio transport for zero-config local usage

### v4.2.0 (Core)
- Consolidated DI support from `XUnitAssured.DependencyInjection` into `XUnitAssured.Core` (`DITestFixture`)

### v4.0.0 (Core + Http)
- Added `ValidationBuilder` and BDD extensions (consolidated from `XUnitAssured.Extensions`)
- Added `HttpValidationBuilder` and BDD extensions for HTTP
- Multi-target support: `net7.0`, `net8.0`, `net9.0`

### v3.0.0 (Kafka)
- Aligned with framework architecture refactoring
- Full fluent DSL integration for Kafka produce/consume
- Batch operations (`ProduceBatch`, `ConsumeBatch`)
- Comprehensive authentication support (SASL, SSL, mTLS)
- Schema Registry support with Avro serialization

## 🤝 Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## 📄 License

This project is licensed under the Apache License 2.0 - see the [LICENSE.md](LICENSE.md) file for details.

## 👤 Author

**Carlos Andrew Costa Bezerra**
- GitHub: [@andrewBezerra](https://github.com/andrewBezerra)

## 🔗 Links

- [GitHub Repository](https://github.com/andrewBezerra/xunit-assured-net)
- [NuGet Packages](https://www.nuget.org/packages?q=XUnitAssured)
- [Report Issues](https://github.com/andrewBezerra/xunit-assured-net/issues)
