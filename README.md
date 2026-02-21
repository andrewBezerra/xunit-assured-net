# XUnitAssured.Net

XUnitAssured.Net is a fluent testing framework for .NET that helps developers create and maintain test collections with the goal of promoting the development of quality software products. Write expressive integration tests using a natural `Given().When().Then()` DSL for both HTTP/REST APIs and Apache Kafka.

## 🎯 Features

- **Fluent BDD DSL**: Write tests in a natural, readable way using `Given().When().Then()` syntax
- **HTTP Testing**: Comprehensive HTTP/REST API testing with full CRUD support, JSON path assertions, and schema validation
- **Kafka Testing**: Integration testing with Apache Kafka — produce/consume single and batch messages with header and key support
- **Modular Architecture**: Install only what you need (Core, Http, Kafka)
- **Multiple HTTP Auth Types**: Bearer, BearerWithAutoRefresh, Basic, OAuth2 (Client Credentials, Password, Authorization Code), API Key (Header/Query), Certificate (mTLS), Custom Headers
- **Multiple Kafka Auth Types**: SASL/PLAIN, SASL/SCRAM-SHA-256, SASL/SCRAM-SHA-512, SSL, Mutual TLS (mTLS)
- **Automatic Authentication**: Configure auth once in `testsettings.json` and have it applied automatically to every request
- **Dependency Injection**: Built-in DI support via `DITestFixture` base class
- **Validation & BDD Extensions**: `ValidationBuilder` and BDD scenario extensions consolidated in Core
- **Multi-target Support**: Targets `net7.0`, `net8.0`, and `net9.0`
- **xUnit Integration**: Seamless integration with xUnit's fixtures and dependency injection

## 📦 Packages

### Core Packages

| Package | Version | Description |
|---------|---------|-------------|
| **XUnitAssured.Core** | 4.2.0 | Core abstractions, DSL infrastructure, DI support (`DITestFixture`), `ValidationBuilder`, and BDD extensions |

### Protocol Packages

| Package | Version | Description |
|---------|---------|-------------|
| **XUnitAssured.Http** | 4.0.0 | HTTP/REST API testing — fluent DSL, authentication handlers, JSON path assertions, schema validation |
| **XUnitAssured.Kafka** | 3.0.0 | Apache Kafka integration testing — produce/consume, batch operations, authentication, Schema Registry support |

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
XUnitAssured.Core  (DSL + Abstractions + DI + ValidationBuilder + BDD Extensions)
       ↓                              ↓
XUnitAssured.Http             XUnitAssured.Kafka
(HTTP/REST Testing)           (Kafka Integration Testing)
```

**Design Principles:**
- **SOLID**: Each package has a single responsibility
- **KISS**: Simple, straightforward APIs
- **DRY**: Reusable components across tests
- **YAGNI**: Only what you need, when you need it
- **Separation of Concerns**: Clear boundaries between HTTP, Kafka, and Core

## 📚 Sample Projects

The repository includes comprehensive sample projects for both local and remote testing:

| Project | Description |
|---------|-------------|
| `XUnitAssured.Http.Samples.Local.Test` | HTTP tests against a local `SampleWebApi` (WebApplicationFactory) |
| `XUnitAssured.Http.Samples.Remote.Test` | HTTP tests against a deployed remote API |
| `XUnitAssured.Kafka.Samples.Remote.Test` | Kafka tests against local Docker or remote Kafka clusters |

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

## 🔄 Version History

### v4.2.0 (Current — Core)
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
- Company: R2A Sistemas
- GitHub: [@andrewBezerra](https://github.com/andrewBezerra)

## 🔗 Links

- [GitHub Repository](https://github.com/andrewBezerra/XUnitAssured.Net)
- [NuGet Packages](https://www.nuget.org/packages?q=XUnitAssured)
- [Report Issues](https://github.com/andrewBezerra/XUnitAssured.Net/issues)
