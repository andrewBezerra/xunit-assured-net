# 🔐 XUnitAssured HTTP Authentication Guide

## Overview

XUnitAssured.Http now supports multiple authentication methods:
- ✅ Basic Authentication
- ✅ Bearer Token
- ✅ Configuration via `httpsettings.json`
- ✅ Environment variables support

---

## Quick Start

### 1. Basic Authentication

```csharp
Given()
    .ApiResource("https://api.example.com/users")
    .WithBasicAuth("username", "password")
    .Get()
    .Validate(response => response.StatusCode.ShouldBe(200));
```

### 2. Bearer Token

```csharp
Given()
    .ApiResource("https://api.example.com/protected")
    .WithBearerToken("your-jwt-token")
    .Get()
    .Validate(response => response.IsSuccessStatusCode.ShouldBeTrue());
```

### 3. Custom Token Prefix

```csharp
Given()
    .ApiResource("https://api.example.com/resource")
    .WithBearerToken("token-value", prefix: "JWT")
    .Get();
```

---

## Configuration File (`httpsettings.json`)

Place this file in the root of your test project:

```json
{
  "http": {
    "baseUrl": "https://api.example.com",
    "timeout": 30,
    "defaultHeaders": {
      "User-Agent": "XUnitAssured/2.0",
      "Accept": "application/json"
    },
    "authentication": {
      "type": "Bearer",
      "bearer": {
        "token": "${ENV:API_TOKEN}",
        "prefix": "Bearer"
      }
    }
  },
  "environments": {
    "dev": {
      "baseUrl": "https://dev-api.example.com",
      "authentication": {
        "type": "Basic",
        "basic": {
          "username": "dev-user",
          "password": "${ENV:DEV_PASSWORD}"
        }
      }
    },
    "prod": {
      "baseUrl": "https://api.example.com",
      "authentication": {
        "type": "Bearer",
        "bearer": {
          "token": "${ENV:PROD_API_TOKEN}"
        }
      }
    }
  }
}
```

### Environment Variables

Set environment variables:
```bash
# Windows (PowerShell)
$env:API_TOKEN = "your-token-here"
$env:DEV_PASSWORD = "dev-password"

# Linux/Mac
export API_TOKEN="your-token-here"
export DEV_PASSWORD="dev-password"
```

---

## Usage Examples

### Automatic Authentication from File

```csharp
// Automatically uses authentication from httpsettings.json
Given()
    .ApiResource("/users")  // BaseUrl from settings
    .Get()
    .Validate(response => response.StatusCode.ShouldBe(200));
```

### Override File Configuration

```csharp
// Override authentication for specific test
Given()
    .ApiResource("/users")
    .WithBearerToken("test-specific-token")  // Overrides httpsettings.json
    .Get();
```

### No Authentication

```csharp
// Explicitly disable authentication
Given()
    .ApiResource("/public-endpoint")
    .WithNoAuth()  // Ignores httpsettings.json
    .Get();
```

### Custom Configuration

```csharp
Given()
    .ApiResource("/resource")
    .WithAuthConfig(config =>
    {
        config.UseBasicAuth("user", "pass");
        // or
        config.UseBearerToken("token");
    })
    .Get();
```

---

## Authentication Types

| Type | DSL Method | Description |
|------|-----------|-------------|
| **Basic** | `.WithBasicAuth(user, pass)` | Username + Password (Base64 encoded) |
| **Bearer** | `.WithBearerToken(token)` | JWT or OAuth token |
| **API Key** | `.WithApiKey(name, value)` | API Key in header or query |
| **OAuth 2.0** | `.WithOAuth2(url, id, secret)` | OAuth 2.0 (4 grant types) |
| **Custom Headers** | `.WithCustomHeader(name, value)` | Custom authentication headers |
| **Certificate (mTLS)** | `.WithCertificate(path, password)` | Client certificate authentication |
| **None** | `.WithNoAuth()` | No authentication |

---

## Certificate Authentication (mTLS)

### From Settings (Recommended)
```csharp
// Automatically loads from httpsettings.json
Given()
    .ApiResource("/secure-endpoint")
    .WithCertificate()  // No parameters needed!
    .Get();
```

**Settings configuration:**
```json
{
  "authentication": {
    "type": "Certificate",
    "certificate": {
      "certificatePath": "${ENV:CLIENT_CERT_PATH}",
      "certificatePassword": "${ENV:CLIENT_CERT_PASSWORD}"
    }
  }
}
```

### From File (Explicit)
```csharp
Given()
    .ApiResource("/secure-endpoint")
    .WithCertificate("path/to/cert.pfx", "password")
    .Get();
```

### From Windows Certificate Store
```csharp
Given()
    .ApiResource("/secure-endpoint")
    .WithCertificateFromStore("THUMBPRINT123")
    .Get();
```

**Note**: Certificates are automatically cached by thumbprint for performance. The framework handles FlurlClient configuration transparently

---

## File Locations

XUnitAssured searches for `httpsettings.json` in:
1. Custom path (if specified)
2. `XUNITASSURED_HTTP_SETTINGS_PATH` environment variable
3. Current directory
4. Parent directories (up to 3 levels)

---

## Environment-Specific Settings

### Option 1: Separate Files

```
MyProject.Tests/
├── httpsettings.json              # Base settings
├── httpsettings.Development.json  # Dev overrides
├── httpsettings.Staging.json      # Staging overrides
└── httpsettings.Production.json   # Prod overrides
```

Load environment-specific settings:
```csharp
var settings = HttpSettings.Load(environment: "Development");
```

### Option 2: Environments Section

Use the `environments` section in `httpsettings.json` (see example above).

---

## Best Practices

### ✅ DO:
- Store sensitive tokens in environment variables
- Use `httpsettings.json` for base configuration
- Override authentication per test when needed
- Clear cache between test sessions if needed:
  ```csharp
  HttpSettingsLoader.ClearCache();
  ```

### ❌ DON'T:
- Commit tokens/passwords in `httpsettings.json`
- Hardcode credentials in test code
- Share authentication across incompatible tests

---

## Troubleshooting

### Authentication Not Applied

1. Check if `httpsettings.json` exists
2. Verify JSON syntax
3. Check environment variables are set
4. Clear cache: `HttpSettingsLoader.ClearCache()`

### Token Format Issues

```csharp
// Correct format
.WithBearerToken("abc123")  
// Results in: Authorization: Bearer abc123

// Custom prefix
.WithBearerToken("abc123", prefix: "JWT")
// Results in: Authorization: JWT abc123
```

---

## Examples Repository

See complete examples in:
- `XUnitAssured.Tests/HttpTests/AuthenticationTests.cs`
- `XUnitAssured.Tests/HttpTests/HttpSettingsTests.cs`

---

## Advanced: Custom Handlers

To implement custom authentication (e.g., OAuth2, API Key):

1. Implement `IAuthenticationHandler`
2. Register in `HttpRequestStep.ApplyAuthentication()`
3. Use via `.WithAuthConfig()`

---

**Need help?** Open an issue on GitHub: https://github.com/andrewBezerra/XUnitAssured.Net
