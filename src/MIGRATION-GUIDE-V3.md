# XUnitAssured v3.0 Migration Guide

## Overview

Version 3.0 represents a **major architectural refactoring** of the XUnitAssured framework. This is a **BREAKING CHANGE** release that significantly improves code organization, reduces dependencies, and follows SOLID/DRY/YAGNI principles more strictly.

## TL;DR - Quick Migration

### For HTTP Testing
```csharp
// BEFORE (v2.x)
using XUnitAssured.Base;

public class MyTests : TestFixture
{
    // ...
}

// AFTER (v3.0)
using XUnitAssured.Http;

public class MyTests : HttpTestFixture
{
    // ...
}
```

### For Kafka Testing
```csharp
// BEFORE (v2.x)
using XUnitAssured.Base;

public class MyTests : TestFixture
{
    // ...
}

// AFTER (v3.0)
using XUnitAssured.Kafka;

public class MyTests : KafkaTestFixture
{
    // ...
}
```

### For Custom Testing with DI
```csharp
// BEFORE (v2.x)
using XUnitAssured.Base;

public class MyTests : TestFixture
{
    // ...
}

// AFTER (v3.0)
using XUnitAssured.DependencyInjection;

public class MyTests : DITestFixture
{
    // ...
}
```

---

## Breaking Changes

### 1. ❌ `XUnitAssured` (Base Package) - REMOVED

**What happened:** The `XUnitAssured` base package has been **completely eliminated**.

**Why:** 
- It was redundant and confusing
- Created unnecessary dependencies
- Violated Single Responsibility Principle
- Only contained 2 files that belonged elsewhere

**Migration Path:**

| Old Class | New Location | Action Required |
|-----------|--------------|-----------------|
| `XUnitAssured.Base.TestFixture` | `XUnitAssured.DependencyInjection.DITestFixture` | Change base class |
| `XUnitAssured.Base.InvalidTestSettingsException` | `XUnitAssured.Core.Exceptions.InvalidTestSettingsException` | Update `using` statement |
| `XUnitAssured.Base.BaseSettings` | **REMOVED** | Use `HttpSettings` from `XUnitAssured.Http` |
| `XUnitAssured.Base.Auth.*` | **REMOVED** | Use auth types from `XUnitAssured.Http.Configuration` |

---

### 2. 🔄 Changed Package Structure

#### **Old Architecture (v2.x)**
```
XUnitAssured (BASE) ← Confusing, unnecessary
├── TestFixture
├── BaseSettings
├── AuthenticationSettings
├── InvalidTestSettingsException
└── 7 NuGet dependencies

XUnitAssured.Core
├── DSL and abstractions
└── 0 dependencies

XUnitAssured.Http
├── HTTP testing
└── Depends on Core + Base (redundant)

XUnitAssured.Kafka
├── Kafka testing
└── Depends on Core + Base (redundant)
```

#### **New Architecture (v3.0)**
```
XUnitAssured.Core ⭐ (ACTUAL CORE)
├── DSL and abstractions
├── InvalidTestSettingsException ✅ NEW
└── 0 dependencies

XUnitAssured.DependencyInjection
├── DITestFixture
└── Depends on Core only

XUnitAssured.Http
├── HTTP testing
├── HttpSettings
├── Authentication (all types)
└── Depends on Core + DependencyInjection

XUnitAssured.Kafka
├── Kafka testing
├── KafkaSettings
├── Authentication (Kafka-specific)
└── Depends on Core + DependencyInjection
```

---

## Detailed Migration Steps

### Step 1: Update Package References

#### Remove
```xml
<PackageReference Include="XUnitAssured.Net" Version="2.x" />
```

#### Add (choose what you need)
```xml
<!-- For HTTP/REST API testing -->
<PackageReference Include="XUnitAssured.Http" Version="3.0.0" />

<!-- For Kafka testing -->
<PackageReference Include="XUnitAssured.Kafka" Version="3.0.0" />

<!-- For custom testing with DI (advanced) -->
<PackageReference Include="XUnitAssured.DependencyInjection" Version="3.0.0" />
<PackageReference Include="XUnitAssured.Core" Version="3.0.0" />
```

---

### Step 2: Update Test Fixtures

#### Option A: HTTP Testing

**Before:**
```csharp
using XUnitAssured.Base;

public class MyHttpTests : TestFixture
{
    protected override void AddServices(IServiceCollection services, IConfiguration? configuration)
    {
        base.AddServices(services, configuration);
        // Your HTTP setup
    }

    protected override IEnumerable<TestAppSettings> GetTestAppSettings()
    {
        yield return new() { Filename = "testsettings.json", IsOptional = false };
    }
}
```

**After:**
```csharp
using XUnitAssured.Http;

public class MyHttpTests : HttpTestFixture
{
    // HttpTestFixture automatically:
    // - Loads httpsettings.json
    // - Configures HttpClient
    // - Sets up authentication
    // - Provides FlurlClient

    protected override void AddHttpServices(IServiceCollection services, IConfiguration configuration)
    {
        // Add your custom HTTP services here
    }
}
```

#### Option B: Kafka Testing

**Before:**
```csharp
using XUnitAssured.Base;

public class MyKafkaTests : TestFixture
{
    protected override void AddServices(IServiceCollection services, IConfiguration? configuration)
    {
        base.AddServices(services, configuration);
        // Your Kafka setup
    }

    protected override IEnumerable<TestAppSettings> GetTestAppSettings()
    {
        yield return new() { Filename = "kafkasettings.json", IsOptional = false };
    }
}
```

**After:**
```csharp
using XUnitAssured.Kafka;

public class MyKafkaTests : KafkaTestFixture
{
    // KafkaTestFixture automatically:
    // - Loads kafkasettings.json
    // - Provides CreateProducer<TKey, TValue>()
    // - Provides CreateConsumer<TKey, TValue>()
    // - Provides ProduceAsync() and ConsumeOne()

    protected override void AddKafkaServices(IServiceCollection services, IConfiguration configuration)
    {
        // Add your custom Kafka services here
    }
}
```

#### Option C: Custom Testing (Advanced)

**Before:**
```csharp
using XUnitAssured.Base;

public class MyCustomTests : TestFixture
{
    protected override void AddServices(IServiceCollection services, IConfiguration? configuration)
    {
        // Your custom setup
    }

    protected override IEnumerable<TestAppSettings> GetTestAppSettings()
    {
        yield return new() { Filename = "mysettings.json", IsOptional = false };
    }
}
```

**After:**
```csharp
using XUnitAssured.DependencyInjection;

public class MyCustomTests : DITestFixture
{
    protected override void AddServices(IServiceCollection services, IConfiguration? configuration)
    {
        // Your custom setup (same as before)
    }

    protected override IEnumerable<TestAppSettings> GetTestAppSettings()
    {
        yield return new() { Filename = "mysettings.json", IsOptional = false };
    }
}
```

---

### Step 3: Update Configuration Files

#### HTTP Settings

**Before:** `testsettings.json`
```json
{
  "TestSettings": {
    "BaseUrl": "https://api.example.com",
    "OpenApiDocument": "openapi.json",
    "Authentication": {
      "BaseUrl": "https://auth.example.com",
      "ClientId": "client-id",
      "ClientSecret": "secret",
      "AuthenticationType": "Bearer"
    }
  }
}
```

**After:** `httpsettings.json`
```json
{
  "http": {
    "baseUrl": "https://api.example.com",
    "timeout": 30,
    "defaultHeaders": {
      "User-Agent": "XUnitAssured/3.0"
    },
    "authentication": {
      "type": "OAuth2",
      "oauth2": {
        "tokenUrl": "https://auth.example.com/token",
        "clientId": "client-id",
        "clientSecret": "secret",
        "grantType": "ClientCredentials"
      }
    }
  }
}
```

**Key Changes:**
- ✅ More comprehensive authentication options
- ✅ Support for multiple auth types (OAuth2, Bearer, Basic, Certificate, ApiKey, etc.)
- ✅ Token caching built-in
- ✅ Environment-specific overrides

#### Kafka Settings

**Before:** Embedded in `testsettings.json`
```json
{
  "TestSettings": {
    "Kafka": {
      "BootstrapServers": "localhost:9092"
    }
  }
}
```

**After:** `kafkasettings.json`
```json
{
  "Kafka": {
    "bootstrapServers": "localhost:9092",
    "schemaRegistry": {
      "url": "http://localhost:8081"
    },
    "authentication": {
      "type": "SaslPlain",
      "saslPlain": {
        "username": "user",
        "password": "pass"
      }
    }
  }
}
```

---

### Step 4: Update Exception Handling

**Before:**
```csharp
using XUnitAssured.Base;

throw new InvalidTestSettingsException("Invalid config");
```

**After:**
```csharp
using XUnitAssured.Core.Exceptions;

throw new InvalidTestSettingsException("Invalid config");
// Now supports inner exceptions!
throw new InvalidTestSettingsException("Invalid config", innerException);
```

---

## Benefits of v3.0

### 1. **70% Fewer Dependencies**

**Before (v2.x):**
```
Installing XUnitAssured.Http:
├── XUnitAssured (7 dependencies)
├── XUnitAssured.Core (0 dependencies)
└── Flurl.Http (1 dependency)
Total: ~10 dependencies
```

**After (v3.0):**
```
Installing XUnitAssured.Http:
├── XUnitAssured.Core (0 dependencies)
├── XUnitAssured.DependencyInjection (2 dependencies)
└── Flurl.Http (1 dependency)
Total: ~3 dependencies (-70%!)
```

### 2. **Clearer Architecture**

| Package | Responsibility | Dependencies |
|---------|----------------|--------------|
| `XUnitAssured.Core` | DSL, abstractions, exceptions | 0 |
| `XUnitAssured.DependencyInjection` | DI infrastructure | Core |
| `XUnitAssured.Http` | HTTP/REST testing | Core + DI |
| `XUnitAssured.Kafka` | Kafka testing | Core + DI |

### 3. **Better Separation of Concerns**

- ✅ HTTP authentication is ONLY in `XUnitAssured.Http`
- ✅ Kafka authentication is ONLY in `XUnitAssured.Kafka`
- ✅ Core has ONLY technology-agnostic code
- ✅ No more confusion about where code belongs

### 4. **Improved Features**

- ✅ **JSON Enum Deserialization Fixed**: `AuthenticationType` now deserializes correctly from string values
- ✅ **Better Error Messages**: `InvalidTestSettingsException` now supports inner exceptions
- ✅ **Enhanced Documentation**: All classes have comprehensive XML docs
- ✅ **Specialized Fixtures**: `HttpTestFixture` and `KafkaTestFixture` provide domain-specific methods

---

## Common Migration Issues

### Issue 1: "Type 'TestFixture' could not be found"

**Error:**
```
CS0246: The type or namespace name 'TestFixture' could not be found
```

**Solution:**
Change to `DITestFixture` and update package reference:
```csharp
// Add package
<PackageReference Include="XUnitAssured.DependencyInjection" Version="3.0.0" />

// Update code
using XUnitAssured.DependencyInjection;
public class MyTests : DITestFixture { }
```

### Issue 2: "Type 'BaseSettings' could not be found"

**Error:**
```
CS0246: The type or namespace name 'BaseSettings' could not be found
```

**Solution:**
Use `HttpSettings` from `XUnitAssured.Http`:
```csharp
<PackageReference Include="XUnitAssured.Http" Version="3.0.0" />

using XUnitAssured.Http.Configuration;
var settings = HttpSettings.Load();
```

### Issue 3: "Package 'XUnitAssured.Net' not found"

**Error:**
```
error NU1101: Unable to find package XUnitAssured.Net
```

**Solution:**
The package was split. Use specialized packages:
```xml
<!-- For HTTP -->
<PackageReference Include="XUnitAssured.Http" Version="3.0.0" />

<!-- For Kafka -->
<PackageReference Include="XUnitAssured.Kafka" Version="3.0.0" />
```

---

## Version Matrix

| Component | v2.x | v3.0 | Status |
|-----------|------|------|--------|
| `XUnitAssured` (base) | 1.0.0 | ❌ **REMOVED** | Deprecated |
| `XUnitAssured.Core` | 2.0.0 | ✅ 3.0.0 | Enhanced |
| `XUnitAssured.DependencyInjection` | 1.0.0 | ✅ 3.0.0 | Enhanced |
| `XUnitAssured.Http` | 2.0.0 | ✅ 3.0.0 | Enhanced |
| `XUnitAssured.Kafka` | 1.0.0 | ✅ 3.0.0 | Enhanced |

---

## Timeline

- **v2.x**: Current stable version (deprecated)
- **v3.0**: New architecture (current)
- **v4.0**: Future enhancements (TBD)

---

## Need Help?

- 📖 **Documentation**: [GitHub Wiki](https://github.com/andrewBezerra/XUnitAssured.Net/wiki)
- 🐛 **Issues**: [GitHub Issues](https://github.com/andrewBezerra/XUnitAssured.Net/issues)
- 💬 **Discussions**: [GitHub Discussions](https://github.com/andrewBezerra/XUnitAssured.Net/discussions)

---

## Acknowledgments

Special thanks to the community for feedback that drove this architectural improvement!

**Version 3.0 is the most significant release in XUnitAssured history. Welcome to a cleaner, faster, and more maintainable testing framework! 🚀**
