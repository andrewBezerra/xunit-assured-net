# Breaking Changes - XUnitAssured v2.0.0

## Overview

Version 2.0 introduces significant improvements by adopting native .NET types and the **Options Pattern** (`IOptions<T>`). These changes improve type safety, testability, and integration with the .NET ecosystem.

---

## 🔴 Breaking Changes

### 1. **BaseSettings: String URLs → Uri**

#### ❌ Before (v1.x):
```csharp
// appsettings.json
{
  "TestSettings": {
    "BaseUrl": "https://api.example.com",
    "OpenApiDocument": "https://api.example.com/swagger.json"
  }
}

// Code
public class MyTest
{
    private readonly BaseSettings _settings;
    
    public MyTest()
    {
        _settings = new BaseSettings
        {
            BaseUrl = "https://api.example.com",
            OpenApiDocument = "https://api.example.com/swagger.json"
        };
    }
}
```

#### ✅ After (v2.0):
```csharp
// appsettings.json (NO CHANGE - same format!)
{
  "TestSettings": {
    "BaseUrl": "https://api.example.com",
    "OpenApiDocument": "https://api.example.com/swagger.json"
  }
}

// Code - use Options pattern
using Microsoft.Extensions.Options;

public class MyTest
{
    private readonly BaseSettings _settings;
    
    public MyTest(IOptions<BaseSettings> options)
    {
        _settings = options.Value;
        
        // BaseUrl is now Uri (not string)
        Uri apiUrl = _settings.BaseUrl;
        
        // OpenApiDocument is now Uri? (not string)
        Uri? swaggerUrl = _settings.OpenApiDocument;
    }
}
```

**Migration:**
- **appsettings.json**: No changes needed! ✅
- **Code**: Properties are now `Uri` instead of `string`
- **Validation**: Automatic via Options pattern

---

### 2. **AuthenticationSettings: String URLs → Uri**

#### ❌ Before (v1.x):
```csharp
var authSettings = new AuthenticationSettings
{
    BaseUrl = "https://auth.example.com",
    ClientId = "my-client",
    ClientSecret = "secret",
    AuthenticationType = AuthenticationType.Bearer
};
```

#### ✅ After (v2.0):
```csharp
// Via Options Pattern (recommended)
services.AddOptions<BaseSettings>()
    .Bind(configuration.GetSection(BaseSettings.SectionName))
    .ValidateOnStart();

// Or programmatically
var authSettings = new AuthenticationSettings
{
    BaseUrl = new Uri("https://auth.example.com"),  // Now Uri!
    ClientId = "my-client",
    ClientSecret = "secret",
    AuthenticationType = AuthenticationType.Bearer
};
```

---

### 3. **Validation: FluentValidation → IValidateOptions<T>**

#### ❌ Before (v1.x):
```csharp
var settings = new BaseSettings { /* ... */ };
settings.Validate();  // Throws if invalid
```

#### ✅ After (v2.0):
```csharp
// Configuration in Startup/Fixture
services.AddOptions<BaseSettings>()
    .Bind(configuration.GetSection(BaseSettings.SectionName))
    .ValidateOnStart();  // Validates at startup!

services.AddSingleton<IValidateOptions<BaseSettings>, BaseSettingsValidator>();

// Validation happens automatically at startup
// Tests fail fast if configuration is invalid
```

**Migration:**
- Remove manual `settings.Validate()` calls
- Configure validation in DI container
- App fails at startup if configuration is invalid (fail-fast ✅)

---

### 4. **Constructors Removed**

#### ❌ Before (v1.x):
```csharp
var settings = new BaseSettings(
    baseUrl: "https://api.example.com",
    openApiDocument: "swagger.json",
    authentication: authSettings,
    kafka: kafkaSettings
);
```

#### ✅ After (v2.0):
```csharp
// Use Options pattern with configuration binding
services.AddOptions<BaseSettings>()
    .Bind(configuration.GetSection(BaseSettings.SectionName));

// Or object initializer
var settings = new BaseSettings
{
    BaseUrl = new Uri("https://api.example.com"),
    OpenApiDocument = new Uri("swagger.json", UriKind.Relative),
    Authentication = authSettings
};
```

---

### 5. **Kafka Settings [DEPRECATED]**

`KafkaSecurity` is now **obsolete** and will be removed in v3.0.

#### ⚠️ Current (v2.0) - Still works but shows warnings:
```csharp
var settings = new BaseSettings
{
    Kafka = new KafkaSecurity  // ⚠️ Obsolete warning
    {
        SecurityProtocol = "SaslSsl",
        Username = "user",
        Password = "pass"
    }
};
```

#### ✅ Recommended (migrate now):
```csharp
// Use Confluent.Kafka directly
using Confluent.Kafka;

var kafkaConfig = new ClientConfig
{
    BootstrapServers = "localhost:9092",
    SecurityProtocol = SecurityProtocol.SaslSsl,
    SaslMechanism = SaslMechanism.Plain,
    SaslUsername = "user",
    SaslPassword = "pass"
};

// Or wait for XUnitAssured.Kafka package (v3.0)
```

---

## 📚 Migration Guide

### Step 1: Update Package

```xml
<PackageReference Include="XUnitAssured.Net" Version="2.0.0" />
```

### Step 2: Update Fixture/Startup

```csharp
public class MyTestFixture : TestFixture
{
    protected override void AddServices(IServiceCollection services, IConfiguration? configuration)
    {
        // Add Options pattern
        services.AddOptions<BaseSettings>()
            .Bind(configuration!.GetSection(BaseSettings.SectionName))
            .ValidateOnStart();  // Fail-fast validation
        
        // Add validator
        services.AddSingleton<IValidateOptions<BaseSettings>, BaseSettingsValidator>();
    }
}
```

### Step 3: Update Test Classes

```csharp
public class MyIntegrationTest : RemoteTest<MyTestFixture>
{
    private readonly BaseSettings _settings;
    
    // Inject IOptions<BaseSettings>
    public MyIntegrationTest(IOptions<BaseSettings> options)
    {
        _settings = options.Value;
    }
    
    [Fact]
    public async Task TestApiCall()
    {
        // _settings.BaseUrl is now Uri (not string)
        var response = await WebClient
            .Request(_settings.BaseUrl.AbsolutePath)
            .GetAsync();
        
        response.StatusCode.Should().Be(200);
    }
}
```

### Step 4: Update appsettings.json (Optional)

No changes required! But you can use the new section name:

```json
{
  "TestSettings": {  // Can use BaseSettings.SectionName
    "BaseUrl": "https://api.example.com",
    "OpenApiDocument": "https://api.example.com/swagger.json",
    "Authentication": {
      "BaseUrl": "https://auth.example.com",
      "ClientId": "client-id",
      "ClientSecret": "client-secret",
      "AuthenticationType": "Bearer"
    }
  }
}
```

---

## 🔧 Compiler Errors and Fixes

### Error 1: Cannot convert string to Uri

```csharp
// ❌ Error
settings.BaseUrl = "https://api.example.com";

// ✅ Fix
settings.BaseUrl = new Uri("https://api.example.com");
```

### Error 2: BaseSettings constructor not found

```csharp
// ❌ Error
var settings = new BaseSettings("https://api.example.com", "swagger.json", auth, kafka);

// ✅ Fix - Use object initializer
var settings = new BaseSettings
{
    BaseUrl = new Uri("https://api.example.com"),
    OpenApiDocument = new Uri("swagger.json", UriKind.Relative)
};
```

### Error 3: Validate() method not found

```csharp
// ❌ Error
settings.Validate();

// ✅ Fix - Use Options pattern
services.AddOptions<BaseSettings>()
    .ValidateOnStart();
```

---

## ✅ Benefits of v2.0

1. **Type Safety**: `Uri` catches invalid URLs at compile-time
2. **Better IntelliSense**: Native types have better tooling support
3. **Options Pattern**: Standard .NET configuration approach
4. **Fail-Fast**: Validation at startup (not runtime)
5. **Testability**: Easier to mock `IOptions<T>`
6. **Performance**: No string-to-Uri conversions at runtime

---

## 🆘 Need Help?

If you encounter issues during migration:

1. Check compiler warnings (marked with `[Obsolete]`)
2. Refer to this guide
3. Check example projects in the repository
4. Open an issue: https://github.com/andrewBezerra/XUnitAssured.Net/issues

---

## 📅 Timeline

- **v2.0.0** (Current): Breaking changes, Obsolete warnings
- **v2.x.x**: Bug fixes, no new breaking changes
- **v3.0.0** (Future): Remove all obsolete APIs (Kafka, TestSettingsValidator)

---

## 🎉 Conclusion

While v2.0 introduces breaking changes, the migration is straightforward and the benefits are significant. The new API is more aligned with .NET best practices and provides better type safety and validation.

**Happy Testing!** 🚀
