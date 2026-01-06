# XUnitAssured.Extensions

[![NuGet](https://img.shields.io/nuget/v/XUnitAssured.Extensions.svg)](https://www.nuget.org/packages/XUnitAssured.Extensions/)
[![License](https://img.shields.io/github/license/andrewBezerra/XUnitAssured.Net)](https://github.com/andrewBezerra/XUnitAssured.Net/blob/main/LICENSE.md)

**XUnitAssured.Extensions** is an optional extension library that provides BDD-style fluent APIs and validation builders for XUnitAssured. It reduces boilerplate code and improves developer/QA experience with convenient helper methods.

## 🎯 Purpose

This library bridges the gap between XUnitAssured's powerful core functionality and developer convenience. It provides:

- **Generic ValidationBuilder Pattern** - Extensible validation builders for any result type
- **BDD-Style Fluent API** - `Given().When().Execute().Then()` syntax for readable tests
- **HTTP-Specific Helpers** - Specialized `HttpValidationBuilder` for REST API testing
- **Reduced Boilerplate** - Focus on testing logic, not infrastructure code
- **Type-Safe Extensions** - Generic constraints ensure compile-time safety

## 📦 Installation

```bash
dotnet add package XUnitAssured.Extensions
```

Or via Package Manager Console:

```powershell
Install-Package XUnitAssured.Extensions
```

## 🚀 Quick Start

### Basic HTTP Testing

```csharp
using XUnitAssured.Extensions.Http;
using static XUnitAssured.Core.DSL.ScenarioDsl;

public class ProductTests
{
    [Fact]
    public void GetProduct_ShouldReturnValidProduct()
    {
        Given()
            .ApiResource("https://api.example.com/products/1")
            .Get()
        .When()
            .Execute()  // ← Returns HttpValidationBuilder
        .Then()
            .AssertStatusCode(200)
            .ValidateContract<Product>()
            .AssertJsonPath<int>("$.id", id => id.ShouldBe(1))
            .AssertJsonPath<string>("$.name", name => name.ShouldNotBeNullOrEmpty());
    }
}
```

### Generic Validation Builder

```csharp
using XUnitAssured.Extensions.Core;

// For custom result types (Kafka, FileSystem, S3, etc.)
var result = scenario.Execute<KafkaStepResult>()
    .Then()
    .AssertSuccess()
    .AssertProperty<string>("Topic", topic => topic.ShouldBe("orders"))
    .Validate(r => r.Offset.ShouldBeGreaterThan(0));
```

## 📚 Key Features

### 1. HTTP Validation Builder

The `HttpValidationBuilder` provides HTTP-specific assertions:

```csharp
.Execute()
    .Then()
    .AssertStatusCode(200)                  // Status code validation
    .ValidateContract<Product>()             // JSON schema validation
    .AssertJsonPath<decimal>("$.price",      // JSON path assertions
        price => price.ShouldBeGreaterThan(0))
    .JsonPath<int>("$.id");                  // Extract values
```

### 2. Generic Validation Builder

The base `ValidationBuilder<TResult>` works with any `ITestStepResult`:

```csharp
scenario.Execute<CustomStepResult>()
    .Then()
    .AssertSuccess()                         // Common assertions
    .AssertFailure()
    .AssertProperty<T>("key", assertion)
    .Validate(result => { /* custom */ })
    .GetResult();                            // Access raw result
```

### 3. BDD-Style Syntax

Execute steps synchronously with fluent BDD syntax:

```csharp
Given()
    .ApiResource("/api/products")
    .Get()
.When()
    .Execute()  // ← Synchronous execution wrapper
.Then()
    .AssertStatusCode(200);
```

## 🏗️ Architecture

```
XUnitAssured.Extensions/
├── Core/
│   ├── BddScenarioExtensions.cs      - Generic Execute<TResult>()
│   └── ValidationBuilder.cs          - Base validation builder
└── Http/
    ├── HttpBddExtensions.cs          - HTTP-specific Execute()
    └── HttpValidationBuilder.cs      - HTTP validation builder
```

### Design Philosophy

1. **Optional** - Core XUnitAssured works without this library
2. **Generic-First** - Base classes support any result type
3. **Specialized Builders** - Technology-specific builders inherit from base
4. **Delegate to Core** - Extensions delegate to XUnitAssured.Http/Core/Kafka for reusable logic
5. **Clear Separation** - Extensions focus on DX, Core focuses on functionality

## 🔄 Migration from TestExtensions

If you were using `TestExtensions.cs` from samples:

### Before (Old Way)
```csharp
using XUnitAssured.Http.Samples.Test;

.When()
    .Execute()
.Then()
    .AssertStatusCode(200);
```

### After (New Way)
```csharp
using XUnitAssured.Extensions.Http;

.When()
    .Execute()
.Then()
    .AssertStatusCode(200);
```

**Changes:**
- Replace `using XUnitAssured.Http.Samples.Test;` → `using XUnitAssured.Extensions.Http;`
- No code changes needed - API is compatible
- Remove local `TestExtensions.cs` file

## 📖 API Reference

### HttpValidationBuilder

| Method | Description |
|--------|-------------|
| `AssertStatusCode(int)` | Asserts HTTP status code |
| `ValidateContract<T>()` | Validates JSON schema against type T |
| `AssertJsonPath<T>(path, assertion)` | Asserts JSON path value using Action |
| `AssertJsonPath<T>(path, predicate, message)` | Asserts JSON path value using Func (legacy) |
| `JsonPath<T>(path)` | Extracts value from JSON response |
| `GetResult()` | Gets HttpStepResult for custom assertions |
| `Then()` | BDD transition marker (pass-through) |

### ValidationBuilder&lt;TResult&gt;

| Method | Description |
|--------|-------------|
| `AssertSuccess()` | Asserts step succeeded |
| `AssertFailure()` | Asserts step failed |
| `AssertErrors(assertion)` | Asserts on error collection |
| `AssertProperty<T>(key, assertion)` | Asserts on Properties dictionary |
| `Validate(action)` | Custom validation logic |
| `GetResult()` | Gets typed result |
| `Then()` | BDD transition marker (pass-through) |

## 🔮 Future Extensions

This architecture is designed to support additional technologies:

```csharp
// Kafka (future)
XUnitAssured.Extensions.Kafka.KafkaValidationBuilder

// FileSystem (future)
XUnitAssured.Extensions.FileSystem.FileValidationBuilder

// S3 (future)
XUnitAssured.Extensions.S3.S3ValidationBuilder
```

## 🤝 Contributing

Contributions are welcome! Please:

1. Follow the existing pattern: `ValidationBuilder<TResult>` for generic, specialized builders for technologies
2. Add XML documentation comments
3. Include examples in this README
4. Write tests demonstrating usage

## 📄 License

This project is licensed under the MIT License - see the [LICENSE.md](../../LICENSE.md) file for details.

## 🔗 Related Packages

- **XUnitAssured.Core** - Core abstractions and DSL
- **XUnitAssured.Http** - HTTP/REST testing functionality
- **XUnitAssured.Kafka** - Kafka testing functionality
- **XUnitAssured.DependencyInjection** - DI integration

## 📞 Support

- 🐛 [Report Issues](https://github.com/andrewBezerra/XUnitAssured.Net/issues)
- 💬 [Discussions](https://github.com/andrewBezerra/XUnitAssured.Net/discussions)
- 📧 Email: [contact information]

---

**Made with ❤️ by the XUnitAssured community**
