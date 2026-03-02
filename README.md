# XUnitAssured.Net

XUnitAssured.Net is a fluent testing framework for .NET that helps developers create and maintain test collections with the goal of promoting the development of quality software products. Write expressive integration tests using a natural `Given().When().Then()` DSL for HTTP/REST APIs, Apache Kafka, and browser-based UI testing with Playwright. Includes an MCP (Model Context Protocol) server for AI-assisted test generation.

## 🎯 Features

- **Fluent BDD DSL**: Write tests in a natural, readable way using `Given().When().Then()` syntax
- **HTTP Testing**: Comprehensive HTTP/REST API testing with full CRUD support, JSON path assertions, and schema validation
- **Kafka Testing**: Integration testing with Apache Kafka — produce/consume single and batch messages with header and key support
- **Playwright UI Testing**: Browser-based UI testing with fluent DSL for clicks, fills, checks, navigation, screenshots, and rich assertions — built on Microsoft Playwright
- **MCP Server**: AI-assisted test generation via Model Context Protocol — translate Playwright code, scaffold HTTP/Kafka tests directly from Copilot Chat
- **Modular Architecture**: Install only what you need (Core, Http, Kafka, Playwright)
- **Multiple HTTP Auth Types**: Bearer, BearerWithAutoRefresh, Basic, OAuth2 (Client Credentials, Password, Authorization Code), API Key (Header/Query), Certificate (mTLS), Custom Headers
- **Multiple Kafka Auth Types**: SASL/PLAIN, SASL/SCRAM-SHA-256, SASL/SCRAM-SHA-512, SSL, Mutual TLS (mTLS)
- **Automatic Authentication**: Configure auth once in `testsettings.json` and have it applied automatically to every request
- **Dependency Injection**: Built-in DI support via `DITestFixture` base class
- **Validation & BDD Extensions**: `ValidationBuilder` and BDD scenario extensions consolidated in Core
- **Multi-target Support**: Targets `net7.0`, `net8.0`, `net9.0`, and `net10.0`
- **xUnit Integration**: Seamless integration with xUnit's fixtures and dependency injection

## 📦 Packages

### Core Packages

| Package | Version | Description |
|---------|---------|-------------|
| **XUnitAssured.Core** | 5.0.0 | Core abstractions, DSL infrastructure, DI support (`DITestFixture`), `ValidationBuilder`, and BDD extensions |

### Protocol Packages

| Package | Version | Description |
|---------|---------|-------------|
| **XUnitAssured.Http** | 5.0.0 | HTTP/REST API testing — fluent DSL, authentication handlers, JSON path assertions, schema validation |
| **XUnitAssured.Kafka** | 5.0.0 | Apache Kafka integration testing — produce/consume, batch operations, authentication, Schema Registry support |
| **XUnitAssured.Playwright** | 5.0.0 | Playwright UI testing — fluent DSL for browser interactions, multiple locator strategies, screenshots, and assertions |

### Tooling

| Package | Description |
|---------|-------------|
| **XUnitAssured.Mcp** | MCP server for AI-assisted test generation — integrates with GitHub Copilot Chat, VS Code, and any MCP-compatible client |

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

#### 1. Build the MCP server

```bash
cd src/XunitAssured.MCP
dotnet build -c Debug
```

#### 2. Configure `.mcp.json`

The MCP server uses **stdio** transport. You can configure it at the **repo level** (`.mcp.json` at the repo root) or **globally** (`~/.mcp.json` in your home directory).

**Option A — Repo-level** (relative path, recommended for team use):

Create a `.mcp.json` file at the repository root:

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

**Option B — Global** (absolute path, recommended for personal use):

Create or edit `~/.mcp.json` (e.g., `C:\Users\<you>\.mcp.json` on Windows):

```json
{
  "servers": {
    "xunitassured": {
      "type": "stdio",
      "command": "<full-path-to-repo>/XUnitAssured.Mcp.exe",
      "args": []
    }
  }
}
```

> **Tip:** Pointing directly to the compiled `.exe` is faster than `dotnet run` because it skips project resolution. Use forward slashes or escaped backslashes (`\\`) on Windows.

#### 3. Restart your IDE

Visual Studio / VS Code must be **restarted** after creating or editing `.mcp.json` for the MCP server to be detected.

#### 4. Verify

In GitHub Copilot Chat, the XUnitAssured tools should appear as available. Try:

> "List all XUnitAssured HTTP DSL methods"

### Usage in Copilot Chat

> "Generate CRUD tests for /api/products with fields name:string, price:decimal"

> "Translate this Playwright code to XUnitAssured DSL: `await page.GetByRole(AriaRole.Button, new() { Name = \"Submit\" }).ClickAsync();`"

> "Generate a Kafka produce-consume round-trip test for the orders topic"

## 🔄 Version History

### v5.0.0 (Current — Core, Http, Kafka, Playwright, MCP)
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

This project is licensed under the MIT License - see the [LICENSE.md](LICENSE.md) file for details.

## 👤 Author

**Carlos Andrew Costa Bezerra**
- GitHub: [@andrewBezerra](https://github.com/andrewBezerra)

## 🔗 Links

- [GitHub Repository](https://github.com/andrewBezerra/XUnitAssured.Net)
- [NuGet Packages](https://www.nuget.org/packages?q=XUnitAssured)
- [Report Issues](https://github.com/andrewBezerra/XUnitAssured.Net/issues)
