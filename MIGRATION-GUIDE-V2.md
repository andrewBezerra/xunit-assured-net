# Migration Guide: From XUnitAssured.Remote to Independent Fixtures

## Overview

In version 2.0, we removed the `XUnitAssured.Remote` package to simplify the architecture and follow SOLID principles. The functionality has been distributed to the appropriate packages:

- **HTTP capabilities** → `XUnitAssured.Http` (with `HttpTestFixture`)
- **Kafka capabilities** → `XUnitAssured.Kafka` (with `KafkaTestFixture`)

Both packages are now **independent** and don't require `XUnitAssured.Base`, making them more modular and reusable.

---

## Why This Change?

### Problems with Remote:
1. **Violation of SRP**: Mixed HTTP + Kafka + configuration responsibilities
2. **Unnecessary coupling**: Forced dependency on both HTTP and Kafka even when only one was needed
3. **No unique value**: Just aggregated existing capabilities without adding anything new
4. **YAGNI violation**: The difference between "local" and "remote" testing is just configuration

### Benefits of New Architecture:
✅ **Install only what you need**: Use HTTP without Kafka, or vice-versa  
✅ **Better separation of concerns**: Each package handles its domain  
✅ **Clearer configuration**: Separate sections for Http and Kafka  
✅ **Independent evolution**: Packages can evolve independently  
✅ **Follows SOLID principles**: Single responsibility, dependency inversion  

---

## Migration Guide

### Before (v1.x - with Remote)

```csharp
using XUnitAssured.Remote;

// Old fixture
public class MyApiTestFixture : RemoteTestFixture
{
    // Configuration was automatic via RemoteSettings
}

// Old test
public class MyApiTests : RemoteTest<MyApiTestFixture>
{
    public MyApiTests(ITestOutputHelper output, MyApiTestFixture fixture) 
        : base(output, fixture)
    {
        // WebClient and Settings were provided
    }

    [Fact]
    public async Task Test_Api()
    {
        var response = await WebClient
            .Request("/api/users")
            .GetAsync();
    }
}
```

**Configuration (remotesettings.json):**
```json
{
  "Remote": {
    "BaseUrl": "https://api.staging.com",
    "TimeoutSeconds": 30,
    "Authentication": {
      "Type": "Bearer",
      "BearerToken": "xyz123"
    }
  }
}
```

---

### After (v2.x - with HttpTestFixture)

```csharp
using XUnitAssured.Http;
using Xunit.Abstractions;

// New fixture
public class MyApiTestFixture : HttpTestFixture
{
    // Configuration is automatic via HttpSettings
}

// New test
public class MyApiTests : IClassFixture<MyApiTestFixture>
{
    private readonly IFlurlClient _httpClient;
    private readonly ITestOutputHelper _output;

    public MyApiTests(ITestOutputHelper output, MyApiTestFixture fixture)
    {
        _output = output;
        _httpClient = fixture.GetFlurlClient();
    }

    [Fact]
    public async Task Test_Api()
    {
        var response = await _httpClient
            .Request("/api/users")
            .GetAsync();
    }
}
```

**Configuration (httpsettings.json):**
```json
{
  "Http": {
    "BaseUrl": "https://api.staging.com",
    "Timeout": 30,
    "Authentication": {
      "Type": "Bearer",
      "Config": {
        "Token": "xyz123"
      }
    }
  }
}
```

---

## Scenario-Specific Migrations

### 1. HTTP Only Tests

**Before:**
```csharp
public class MyFixture : RemoteTestFixture { }
```

**After:**
```csharp
using XUnitAssured.Http;

public class MyFixture : HttpTestFixture { }
```

---

### 2. Kafka Only Tests

**Before:**
```csharp
public class MyFixture : RemoteTestFixture
{
    // Had to deal with HTTP even if not using it
}
```

**After:**
```csharp
using XUnitAssured.Kafka;

public class MyFixture : KafkaTestFixture { }
```

**Configuration (kafkasettings.json):**
```json
{
  "Kafka": {
    "BootstrapServers": "kafka.staging.com:9092",
    "GroupId": "test-group",
    "SecurityProtocol": "SaslSsl"
  }
}
```

---

### 3. HTTP + Kafka Tests

**Before:**
```csharp
public class MyFixture : RemoteTestFixture
{
    // Everything mixed together
}
```

**After (Option A - Compose in your own fixture):**
```csharp
using XUnitAssured.Base;
using XUnitAssured.Http;
using XUnitAssured.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

public class MyIntegrationFixture : TestFixture
{
    protected override void AddServices(IServiceCollection services, IConfiguration? configuration)
    {
        if (configuration == null)
            throw new InvalidOperationException("Configuration required.");

        // Configure HTTP
        var httpSection = configuration.GetSection("Http");
        services.Configure<HttpSettings>(httpSection);
        
        // Configure Kafka
        var kafkaSection = configuration.GetSection("Kafka");
        services.Configure<KafkaSettings>(kafkaSection);

        // Register HTTP client
        var httpSettings = httpSection.Get<HttpSettings>();
        var httpClient = new HttpClient { BaseAddress = new Uri(httpSettings!.BaseUrl!) };
        services.AddSingleton(new FlurlClient(httpClient));

        // Register Kafka producers/consumers as needed
        // ... (see KafkaTestFixture for examples)
    }

    protected override IEnumerable<TestAppSettings> GetTestAppSettings()
    {
        yield return new() { Filename = "httpsettings.json", IsOptional = false };
        yield return new() { Filename = "kafkasettings.json", IsOptional = false };
    }
}
```

**After (Option B - Use separate fixtures for HTTP and Kafka tests):**
```csharp
// HTTP tests
public class HttpTestsFixture : HttpTestFixture { }

public class MyHttpTests : IClassFixture<HttpTestsFixture>
{
    private readonly IFlurlClient _client;
    
    public MyHttpTests(ITestOutputHelper output, HttpTestsFixture fixture)
    {
        _client = fixture.GetFlurlClient();
    }
}

// Kafka tests
public class KafkaTestsFixture : KafkaTestFixture { }

public class MyKafkaTests : IClassFixture<KafkaTestsFixture>
{
    private readonly KafkaTestsFixture _fixture;
    
    public MyKafkaTests(ITestOutputHelper output, KafkaTestsFixture fixture)
    {
        _fixture = fixture;
    }
}
```

---

## New Features in Http Package

### Retry Logic Extensions

The `FlurlClientExtensions` class (migrated from Remote) provides retry logic:

```csharp
using XUnitAssured.Http.Extensions;

// GET with retry
var response = await client.GetWithRetryAsync(
    "/api/users",
    maxRetries: 5,
    retryDelay: 2000);

// POST with retry
var response = await client.PostWithRetryAsync(
    "/api/users",
    body: new { name = "John" },
    maxRetries: 3);
```

### Authentication Helpers

```csharp
using XUnitAssured.Http.Extensions;

// Bearer token
var response = await client
    .Request("/api/protected")
    .WithBearerToken("my-token")
    .GetAsync();

// Basic auth
var response = await client
    .Request("/api/protected")
    .WithBasicAuth("username", "password")
    .GetAsync();

// API Key
var response = await client
    .Request("/api/protected")
    .WithApiKey("my-api-key", "X-Custom-Key")
    .GetAsync();
```

---

## Configuration Changes

### Remote vs Http Settings Comparison

| Remote (Old) | Http (New) | Notes |
|--------------|------------|-------|
| `Remote:BaseUrl` | `Http:BaseUrl` | Same concept |
| `Remote:TimeoutSeconds` | `Http:Timeout` | Same concept |
| `Remote:Authentication:Type` | `Http:Authentication:Type` | Same values |
| `Remote:Authentication:BearerToken` | `Http:Authentication:Config:Token` | Nested config |
| `Remote:MaxRetryAttempts` | (removed) | Use `GetWithRetryAsync()` method |
| `Remote:Kafka:*` | `Kafka:*` | Moved to separate section |

---

## Testing Different Environments

The concept of "remote" testing (staging, production) is now just **configuration**:

### Local Development
```json
{
  "Http": {
    "BaseUrl": "http://localhost:5000",
    "Timeout": 5
  }
}
```

### Staging
```json
{
  "Http": {
    "BaseUrl": "https://api.staging.mycompany.com",
    "Timeout": 30,
    "Authentication": {
      "Type": "Bearer",
      "Config": { "Token": "${STAGING_TOKEN}" }
    }
  }
}
```

### Production
```json
{
  "Http": {
    "BaseUrl": "https://api.mycompany.com",
    "Timeout": 60,
    "Authentication": {
      "Type": "Certificate",
      "CertificateConfig": {
        "CertificatePath": "/path/to/cert.pfx",
        "CertificatePassword": "${CERT_PASSWORD}"
      }
    }
  }
}
```

**Tip**: Use environment-specific configuration files:
- `httpsettings.json` (default)
- `httpsettings.Development.json`
- `httpsettings.Staging.json`
- `httpsettings.Production.json`

---

## Breaking Changes Summary

### Removed
- ❌ `XUnitAssured.Remote` package
- ❌ `RemoteTestFixture` class
- ❌ `RemoteTest<T>` class
- ❌ `RemoteSettings` class
- ❌ `remotesettings.json` configuration file

### Added
- ✅ `HttpTestFixture` in `XUnitAssured.Http`
- ✅ `FlurlClientExtensions` with retry methods
- ✅ `KafkaTestFixture` now independent (no dependency on Base)

### Changed
- 🔄 `KafkaTestFixture` now inherits from `TestBedFixture` directly
- 🔄 Configuration split into `httpsettings.json` and `kafkasettings.json`

---

## Troubleshooting

### Error: "RemoteTestFixture not found"

**Solution**: Replace with `HttpTestFixture` or `KafkaTestFixture`

```csharp
// Change this:
using XUnitAssured.Remote;
public class MyFixture : RemoteTestFixture { }

// To this:
using XUnitAssured.Http;
public class MyFixture : HttpTestFixture { }
```

---

### Error: "WebClient not found"

**Solution**: Use `GetFlurlClient()` method

```csharp
// Change this:
public MyTests(ITestOutputHelper output, MyFixture fixture) 
    : base(output, fixture)
{
    var client = WebClient; // Old way
}

// To this:
public MyTests(ITestOutputHelper output, MyFixture fixture)
{
    var client = fixture.GetFlurlClient(); // New way
}
```

---

### Error: "remotesettings.json not found"

**Solution**: Rename and restructure configuration

```bash
# Rename file
mv remotesettings.json httpsettings.json

# Update structure (see Configuration Changes section)
```

---

## Support

If you encounter issues during migration:

1. Check this migration guide
2. Review examples in the test project
3. Open an issue on GitHub: https://github.com/andrewBezerra/XUnitAssured.Net/issues

---

## Architecture Philosophy

This change aligns with our core principles:

- **SOLID**: Each package has a single responsibility
- **KISS**: Simpler architecture, easier to understand
- **DRY**: No duplication between Remote and Http/Kafka
- **YAGNI**: Removed unnecessary abstraction (Remote)
- **Separation of Concerns**: HTTP and Kafka are separate domains

The "remote" concept was just configuration, not a technical capability that warranted a separate package.
