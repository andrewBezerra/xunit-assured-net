# XUnitAssured.Http.Samples.Remote.Test

Remote integration tests for XUnitAssured.Http demonstrating testing against deployed/remote APIs.

## Overview

This project contains sample tests that demonstrate how to use XUnitAssured.Http to test APIs deployed in remote environments (staging, production, etc.). Unlike the local tests that use `WebApplicationFactory` for in-memory testing, these tests connect to actual remote HTTP endpoints.

## Configuration

Tests are configured via `testsettings.json` file in the project root:

```json
{
  "testMode": "Remote",
  "environment": "staging",
  "http": {
    "baseUrl": "https://your-api.com",
    "timeout": 60,
    "defaultHeaders": {
      "X-Test-Source": "XUnitAssured.Remote.Test"
    },
    "authentication": {
      "type": "None"
    }
  }
}
```

### Environment Variables

You can use environment variables in the configuration:

```json
{
  "http": {
    "baseUrl": "${ENV:REMOTE_API_URL}",
    "authentication": {
      "type": "Bearer",
      "bearer": {
        "token": "${ENV:API_TOKEN}"
      }
    }
  }
}
```

Set the environment variables before running tests:

```bash
# Windows (PowerShell)
$env:REMOTE_API_URL="https://api.staging.example.com"
$env:API_TOKEN="your-token-here"
dotnet test

# Windows (CMD)
set REMOTE_API_URL=https://api.staging.example.com
set API_TOKEN=your-token-here
dotnet test

# Linux/Mac
export REMOTE_API_URL=https://api.staging.example.com
export API_TOKEN=your-token-here
dotnet test
```

### Environment-Specific Configuration

You can create environment-specific configuration files:

- `testsettings.json` - Default configuration
- `testsettings.staging.json` - Staging environment
- `testsettings.prod.json` - Production environment

Load specific environment:

```bash
# Via environment variable
$env:TEST_ENV="staging"
dotnet test

# Or specify when loading in code
var settings = TestSettings.Load("staging");
```

## Authentication

The framework supports multiple authentication types:

### Basic Authentication

```json
{
  "http": {
    "authentication": {
      "type": "Basic",
      "basic": {
        "username": "admin",
        "password": "${ENV:API_PASSWORD}"
      }
    }
  }
}
```

### Bearer Token

```json
{
  "http": {
    "authentication": {
      "type": "Bearer",
      "bearer": {
        "token": "${ENV:API_TOKEN}",
        "prefix": "Bearer"
      }
    }
  }
}
```

### API Key

```json
{
  "http": {
    "authentication": {
      "type": "ApiKey",
      "apiKey": {
        "keyName": "X-API-Key",
        "keyValue": "${ENV:API_KEY}",
        "location": "Header"
      }
    }
  }
}
```

### OAuth2

```json
{
  "http": {
    "authentication": {
      "type": "OAuth2",
      "oauth2": {
        "tokenUrl": "${ENV:OAUTH_TOKEN_URL}",
        "clientId": "${ENV:OAUTH_CLIENT_ID}",
        "clientSecret": "${ENV:OAUTH_CLIENT_SECRET}",
        "grantType": "ClientCredentials"
      }
    }
  }
}
```

## Running Tests

### Run all tests

```bash
dotnet test
```

### Run specific category

```bash
# Run only integration tests
dotnet test --filter "Category=Integration"

# Run only authentication tests
dotnet test --filter "Authentication=Bearer"

# Run only remote tests
dotnet test --filter "Environment=Remote"
```

### Run with logger

```bash
dotnet test --logger "console;verbosity=detailed"
```

## Test Structure

All remote tests follow this structure:

1. **Fixture**: `HttpSamplesRemoteFixture` - Manages HTTP client and configuration
2. **Base Class**: `HttpSamplesRemoteTestBase` - Provides `Given()` method
3. **Test Classes**: Individual test files for different scenarios

Example test:

```csharp
public class MyRemoteTests : HttpSamplesRemoteTestBase, IClassFixture<HttpSamplesRemoteFixture>
{
    public MyRemoteTests(HttpSamplesRemoteFixture fixture) : base(fixture) { }
    
    [Fact]
    public void GetProduct_Should_ReturnProduct()
    {
        Given()
            .ApiResource("/api/products/1")
            .Get()
        .When()
            .Execute()
        .Then()
            .AssertStatusCode(200)
            .AssertJsonPath<string>("$.name", name => !string.IsNullOrEmpty(name));
    }
}
```

## Test Categories

### ✅ Integration Tests (100% Complete)
- `SimpleIntegrationTests.cs` - Basic HTTP requests and client configuration
- `CrudOperationsTests.cs` - Complete CRUD operations (GET, POST, PUT, DELETE) with 13 test scenarios

### ✅ Authentication Tests (100% Complete)
- `BasicAuthTests.cs` - Basic Authentication (10 tests)
- `BearerAuthTests.cs` - Bearer Token Authentication (10 tests)
- `ApiKeyAuthTests.cs` - API Key in Header and Query Parameter (13 tests)
- `CustomHeaderAuthTests.cs` - Multiple Custom Headers (12 tests)
- `OAuth2AuthTests.cs` - OAuth2 Client Credentials Flow (12 tests)
- `CertificateAuthTests.cs` - Certificate-based Authentication/mTLS (9 tests, most skipped - require cert setup)

### ✅ Validation Tests (100% Complete)
- `HybridValidationTests.cs` - Combined validation strategies with Shouldly (8 tests)
- `DiagnosticTests.cs` - Connectivity, configuration and troubleshooting tests (7 tests)

### 📊 Test Statistics
- **Total Test Files**: 12
- **Total Test Methods**: ~84 tests
- **Authentication Scenarios**: 6 different types
- **CRUD Operations**: Complete workflow coverage
- **Validation Strategies**: Hybrid approach (structure + assertions)

## Differences from Local Tests

| Aspect | Local Tests | Remote Tests |
|--------|-------------|--------------|
| **Server** | In-memory (WebApplicationFactory) | Remote HTTP endpoint |
| **Configuration** | Hardcoded | testsettings.json |
| **Fixture** | `HttpSamplesFixture` | `HttpSamplesRemoteFixture` |
| **Base URL** | Auto-generated | Configured |
| **Authentication** | Test-specific | Configured globally (optional) |
| **Dependencies** | References SampleWebApi project | No API project reference |

## Best Practices

1. **Never commit secrets**: Use environment variables for sensitive data
2. **Use test accounts**: Don't use production credentials
3. **Clean up resources**: Delete test data after tests complete
4. **Idempotent tests**: Tests should be repeatable without side effects
5. **Proper timeouts**: Configure appropriate timeouts for remote calls
6. **Network resilience**: Handle transient network failures

## Troubleshooting

### Connection Refused
- Verify the `baseUrl` in testsettings.json
- Ensure the remote API is running and accessible
- Check firewall/network settings

### Authentication Failures
- Verify credentials/tokens are correct
- Check if tokens have expired
- Ensure authentication type matches API requirements

### Timeout Errors
- Increase timeout in testsettings.json
- Check network latency
- Verify API response times

### SSL/TLS Errors
- Ensure valid SSL certificate on remote API
- For dev/testing, you may need to configure cert validation

## Related Projects

- **XUnitAssured.Http.Samples.Test** - Local in-memory tests
- **XUnitAssured.Http** - Core HTTP testing framework
- **XUnitAssured.Extensions** - Extension methods and utilities

## Documentation

For more information, visit:
- [XUnitAssured Documentation](https://github.com/andrewBezerra/XUnitAssured.Net)
- [XUnitAssured.Http Guide](../XUnitAssured.Http/README.md)
