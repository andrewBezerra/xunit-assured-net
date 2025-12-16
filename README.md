# XUnitAssured.Net

XUnitAssured.Net is a fluent testing framework for .NET that helps developers create and maintain test collections with the goal of promoting the development of quality software products.

## 🎯 Features

- **Fluent DSL**: Write tests in a natural, readable way using `Given().When().Then()` syntax
- **HTTP Testing**: Comprehensive HTTP/REST API testing with authentication support
- **Kafka Testing**: Integration testing with Apache Kafka (producers and consumers)
- **Modular Architecture**: Install only what you need (Core, Http, Kafka)
- **Multiple Auth Types**: Bearer, Basic, OAuth2, API Key, Certificate, Custom headers
- **Retry Logic**: Built-in retry mechanisms for resilient tests
- **xUnit Integration**: Seamless integration with xUnit's dependency injection

## 📦 Packages

### Core Packages

| Package | Version | Description |
|---------|---------|-------------|
| **XUnitAssured.Core** | 2.0.0 | Core abstractions and DSL infrastructure |
| **XUnitAssured** | 1.0.0 | Base test fixtures and settings |

### Protocol Packages

| Package | Version | Description |
|---------|---------|-------------|
| **XUnitAssured.Http** | 2.0.0 | HTTP/REST API testing with Flurl |
| **XUnitAssured.Kafka** | 1.0.0 | Apache Kafka integration testing |

## 🚀 Quick Start

### HTTP Testing

```bash
dotnet add package XUnitAssured.Http
```

```csharp
using XUnitAssured.Http;
using Xunit;
using Xunit.Abstractions;

public class MyApiTestFixture : HttpTestFixture { }

public class MyApiTests : IClassFixture<MyApiTestFixture>
{
    private readonly IFlurlClient _client;
    
    public MyApiTests(ITestOutputHelper output, MyApiTestFixture fixture)
    {
        _client = fixture.GetFlurlClient();
    }

    [Fact]
    public async Task Get_Users_Returns_Success()
    {
        var response = await _client
            .Request("/api/users")
            .GetAsync();

        Assert.True(response.ResponseMessage.IsSuccessStatusCode);
    }
}
```

**Configuration (httpsettings.json):**
```json
{
  "Http": {
    "BaseUrl": "https://api.example.com",
    "Timeout": 30,
    "Authentication": {
      "Type": "Bearer",
      "Config": {
        "Token": "your-token-here"
      }
    }
  }
}
```

### Kafka Testing

```bash
dotnet add package XUnitAssured.Kafka
```

```csharp
using XUnitAssured.Kafka;
using Xunit;
using Xunit.Abstractions;

public class MyKafkaTestFixture : KafkaTestFixture { }

public class MyKafkaTests : IClassFixture<MyKafkaTestFixture>
{
    private readonly MyKafkaTestFixture _fixture;
    
    public MyKafkaTests(ITestOutputHelper output, MyKafkaTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Produce_And_Consume_Message()
    {
        // Produce
        await _fixture.ProduceAsync("my-topic", "key1", "value1");

        // Consume
        var result = _fixture.ConsumeOne<string, string>("my-topic");
        
        Assert.NotNull(result);
        Assert.Equal("value1", result.Message.Value);
    }
}
```

## 📚 Documentation

- [Migration Guide v2.0](MIGRATION-GUIDE-V2.md) - Upgrading from v1.x (Remote package removal)
- [HTTP Testing Guide](docs/http-testing.md) - Detailed HTTP testing scenarios
- [Kafka Testing Guide](docs/kafka-testing.md) - Kafka integration patterns
- [Authentication Guide](docs/authentication.md) - All supported authentication types

## 🔄 Version 2.0 Changes

**Breaking Changes:**
- ❌ Removed `XUnitAssured.Remote` package
- ✅ `HttpTestFixture` added to `XUnitAssured.Http`
- ✅ `KafkaTestFixture` now independent (no dependency on Base)
- 🔄 Configuration split: `httpsettings.json` and `kafkasettings.json`

See [MIGRATION-GUIDE-V2.md](MIGRATION-GUIDE-V2.md) for detailed migration instructions.

## 🏗️ Architecture

```
XUnitAssured.Core (DSL + Abstractions)
    ↓
XUnitAssured.Http (HTTP Testing)    XUnitAssured.Kafka (Kafka Testing)
    ↓                                    ↓
XUnitAssured (Base Fixtures)
```

**Design Principles:**
- **SOLID**: Each package has a single responsibility
- **KISS**: Simple, straightforward APIs
- **DRY**: Reusable components across tests
- **YAGNI**: Only what you need, when you need it
- **Separation of Concerns**: Clear boundaries between HTTP, Kafka, and Core

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
