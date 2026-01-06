# XUnitAssured.Http - Sample Tests

This project contains comprehensive sample tests demonstrating how to use **XUnitAssured.Http** for testing REST APIs with various authentication methods.

## 📋 Table of Contents

- [Overview](#overview)
- [Getting Started](#getting-started)
- [Sample Test Categories](#sample-test-categories)
- [Running the Tests](#running-the-tests)
- [Test Structure](#test-structure)
- [Authentication Examples](#authentication-examples)
- [CRUD Operations](#crud-operations)
- [Tips and Best Practices](#tips-and-best-practices)

## 🎯 Overview

XUnitAssured.Http provides a fluent, BDD-style DSL for testing HTTP/REST APIs in .NET. These samples demonstrate:

- **CRUD Operations**: GET, POST, PUT, DELETE
- **Authentication Types**: Basic, Bearer, API Key, Custom Header, OAuth2, Certificate (mTLS)
- **Response Validation**: Status codes, JSON path assertions, response structure validation
- **Error Handling**: Invalid data, authentication failures, not found scenarios
- **Complete Workflows**: End-to-end testing scenarios

## 🚀 Getting Started

### Prerequisites

- .NET 8.0 SDK or later
- Visual Studio 2022 or JetBrains Rider (optional)
- Basic understanding of xUnit and REST APIs

### Installation

1. Clone the repository
2. Restore NuGet packages:
   ```bash
   dotnet restore
   ```
3. Build the solution:
   ```bash
   dotnet build
   ```

### Quick Start

Run all sample tests:
```bash
dotnet test XUnitAssured.Http.Samples.Test.csproj
```

Run a specific test class:
```bash
dotnet test --filter "FullyQualifiedName~CrudOperationsTests"
```

## 📚 Sample Test Categories

### 1. **CrudOperationsTests.cs**
Demonstrates basic CRUD operations using the fluent DSL.

**Examples include:**
- ✅ Get all products
- ✅ Get product by ID
- ✅ Create new product
- ✅ Update existing product
- ✅ Delete product
- ✅ Validation error handling
- ✅ Complete CRUD workflow

**Sample Code:**
```csharp
[Fact]
public void Example01_GetAllProducts_ShouldReturnListOfProducts()
{
    Given()
        .ApiResource($"{_fixture.BaseUrl}/api/products")
        .Get()
    .When()
        .Execute()
    .Then()
        .AssertStatusCode(200)
        .AssertJsonPath("$", value => ((JsonElement)value).GetArrayLength() > 0);
}
```

### 2. **BasicAuthTests.cs**
Demonstrates HTTP Basic Authentication (username + password).

**Examples include:**
- ✅ Valid credentials authentication
- ✅ Invalid credentials handling
- ✅ Missing credentials error
- ✅ Case-sensitive validation
- ✅ Multiple requests with same credentials

**Sample Code:**
```csharp
[Fact]
public void Example01_BasicAuth_WithValidCredentials_ShouldReturnSuccess()
{
    Given()
        .ApiResource($"{_fixture.BaseUrl}/api/auth/basic")
        .WithBasicAuth("admin", "secret123")
        .Get()
    .When()
        .Execute()
    .Then()
        .AssertStatusCode(200)
        .AssertJsonPath("$.authenticated", value => (bool)value == true);
}
```

### 3. **BearerAuthTests.cs**
Demonstrates Bearer Token Authentication (JWT, OAuth2 tokens).

**Examples include:**
- ✅ Valid token authentication
- ✅ Invalid token handling
- ✅ Missing token error
- ✅ Custom prefix support
- ✅ Token case-sensitivity

**Sample Code:**
```csharp
[Fact]
public void Example01_BearerAuth_WithValidToken_ShouldReturnSuccess()
{
    Given()
        .ApiResource($"{_fixture.BaseUrl}/api/auth/bearer")
        .WithBearerToken("my-super-secret-token-12345")
        .Get()
    .When()
        .Execute()
    .Then()
        .AssertStatusCode(200)
        .AssertJsonPath("$.authenticated", value => (bool)value == true);
}
```

### 4. **ApiKeyAuthTests.cs**
Demonstrates API Key Authentication in headers and query parameters.

**Examples include:**
- ✅ API Key in HTTP header
- ✅ API Key in query parameter
- ✅ Invalid API key handling
- ✅ Missing API key error
- ✅ Multiple requests with same key

**Sample Code:**
```csharp
// API Key in Header
[Fact]
public void Example01_ApiKeyHeader_WithValidKey_ShouldReturnSuccess()
{
    Given()
        .ApiResource($"{_fixture.BaseUrl}/api/auth/apikey-header")
        .WithApiKey("X-API-Key", "api-key-header-abc123xyz", ApiKeyLocation.Header)
        .Get()
    .When()
        .Execute()
    .Then()
        .AssertStatusCode(200);
}

// API Key in Query Parameter
[Fact]
public void Example07_ApiKeyQuery_WithValidKey_ShouldReturnSuccess()
{
    Given()
        .ApiResource($"{_fixture.BaseUrl}/api/auth/apikey-query")
        .WithApiKey("api_key", "api-key-query-xyz789abc", ApiKeyLocation.Query)
        .Get()
    .When()
        .Execute()
    .Then()
        .AssertStatusCode(200);
}
```

### 5. **CustomHeaderAuthTests.cs**
Demonstrates custom HTTP header authentication.

**Examples include:**
- ✅ Multiple custom headers
- ✅ Single custom header
- ✅ Missing headers error
- ✅ Invalid header values
- ✅ Case-sensitive validation

**Sample Code:**
```csharp
[Fact]
public void Example01_CustomHeader_WithValidHeaders_ShouldReturnSuccess()
{
    Given()
        .ApiResource($"{_fixture.BaseUrl}/api/auth/custom-header")
        .WithCustomHeaders(new Dictionary<string, string>
        {
            ["X-Custom-Auth-Token"] = "custom-token-12345",
            ["X-Custom-Session-Id"] = "session-abc-xyz"
        })
        .Get()
    .When()
        .Execute()
    .Then()
        .AssertStatusCode(200)
        .AssertJsonPath("$.authenticated", value => (bool)value == true);
}
```

### 6. **OAuth2AuthTests.cs**
Demonstrates OAuth2 Client Credentials flow with automatic token management.

**Examples include:**
- ✅ Token endpoint request
- ✅ Invalid credentials handling
- ✅ Complete OAuth2 flow (token + protected resource)
- ✅ Automatic token management with `WithOAuth2()`
- ✅ Custom OAuth2 configuration
- ✅ Token caching and reuse

**Sample Code:**
```csharp
// Manual OAuth2 Flow
[Fact]
public void Example05_OAuth2_CompleteFlow_GetTokenAndAccessProtectedEndpoint()
{
    // Step 1: Get token
    var tokenResponse = Given()
        .ApiResource($"{_fixture.BaseUrl}/api/auth/oauth2/token")
        .Post(new { grant_type = "client_credentials", client_id = "test-client-id", client_secret = "test-client-secret" })
    .When()
        .Execute()
    .Then()
        .AssertStatusCode(200)
        .GetResult();

    var accessToken = tokenResponse.JsonPath<string>("$.access_token");

    // Step 2: Access protected endpoint
    Given()
        .ApiResource($"{_fixture.BaseUrl}/api/auth/oauth2/protected")
        .WithBearerToken(accessToken)
        .Get()
    .When()
        .Execute()
    .Then()
        .AssertStatusCode(200);
}

// Automatic OAuth2 (Recommended)
[Fact]
public void Example08_OAuth2_UsingXUnitAssuredOAuth2Extension()
{
    Given()
        .ApiResource($"{_fixture.BaseUrl}/api/auth/oauth2/protected")
        .WithOAuth2($"{_fixture.BaseUrl}/api/auth/oauth2/token", "test-client-id", "test-client-secret")
        .Get()
    .When()
        .Execute()
    .Then()
        .AssertStatusCode(200);
}
```

### 7. **CertificateAuthTests.cs**
Demonstrates Certificate-based Authentication (mTLS - Mutual TLS).

**Examples include:**
- ✅ Certificate from file (.pfx)
- ✅ Certificate from Windows Certificate Store
- ✅ Certificate instance
- ✅ Certificate from configuration (httpsettings.json)
- ✅ Setup documentation and PowerShell commands

**Note:** Most certificate tests are marked as `Skip` because they require actual certificate setup. The tests serve as API documentation and can be enabled when certificates are configured.

**Sample Code:**
```csharp
// From File
[Fact]
public void Example01_Certificate_FromFile_ShouldAuthenticate()
{
    Given()
        .ApiResource($"{_fixture.BaseUrl}/api/auth/certificate")
        .WithCertificate("path/to/client-certificate.pfx", "password")
        .Get()
    .When()
        .Execute()
    .Then()
        .AssertStatusCode(200);
}

// From Windows Certificate Store
[Fact]
public void Example02_Certificate_FromStore_ShouldAuthenticate()
{
    Given()
        .ApiResource($"{_fixture.BaseUrl}/api/auth/certificate")
        .WithCertificateFromStore("thumbprint", StoreLocation.CurrentUser, StoreName.My)
        .Get()
    .When()
        .Execute()
    .Then()
        .AssertStatusCode(200);
}
```

## 🏃 Running the Tests

### Run All Tests
```bash
dotnet test
```

### Run Specific Test Class
```bash
dotnet test --filter "FullyQualifiedName~BasicAuthTests"
```

### Run Single Test
```bash
dotnet test --filter "Example01_BasicAuth_WithValidCredentials_ShouldReturnSuccess"
```

### Run with Verbose Output
```bash
dotnet test --logger "console;verbosity=detailed"
```

## 🏗️ Test Structure

All tests follow the **Given-When-Then** BDD pattern:

```csharp
Given()                                      // Setup (preconditions)
    .ApiResource("http://api.com/resource")  // Define the endpoint
    .WithBasicAuth("user", "pass")           // Configure authentication
    .Post(data)                              // Set HTTP method and body
.When()                                      // Action
    .Execute()                               // Execute the request
.Then()                                      // Assertions
    .AssertStatusCode(200)                   // Validate status code
    .AssertJsonPath("$.id", ...)             // Validate response body
```

### Key Components

1. **HttpSamplesFixture**: Test fixture that hosts the SampleWebApi using `WebApplicationFactory`
2. **Given()**: Entry point for test scenarios
3. **ApiResource()**: Defines the API endpoint to test
4. **Authentication Methods**: `.WithBasicAuth()`, `.WithBearerToken()`, `.WithOAuth2()`, etc.
5. **HTTP Methods**: `.Get()`, `.Post()`, `.Put()`, `.Delete()`, `.Patch()`
6. **Execute()**: Executes the HTTP request
7. **Assertions**: `.AssertStatusCode()`, `.AssertJsonPath()`, etc.

## 🔐 Authentication Examples

### Basic Authentication
```csharp
.WithBasicAuth("username", "password")
```

### Bearer Token
```csharp
.WithBearerToken("my-jwt-token")
```

### API Key (Header)
```csharp
.WithApiKey("X-API-Key", "abc123", ApiKeyLocation.Header)
```

### API Key (Query)
```csharp
.WithApiKey("api_key", "abc123", ApiKeyLocation.Query)
```

### Custom Headers
```csharp
.WithCustomHeaders(new Dictionary<string, string>
{
    ["X-Custom-Auth"] = "token123",
    ["X-Session-Id"] = "session-xyz"
})
```

### OAuth2 (Automatic)
```csharp
.WithOAuth2("https://auth.com/token", "client-id", "client-secret")
```

### Certificate (mTLS)
```csharp
.WithCertificate("path/to/cert.pfx", "password")
// or
.WithCertificateFromStore("thumbprint", StoreLocation.CurrentUser, StoreName.My)
```

## 📝 CRUD Operations

### GET Request
```csharp
Given()
    .ApiResource($"{baseUrl}/api/products")
    .Get()
.When()
    .Execute()
.Then()
    .AssertStatusCode(200);
```

### POST Request
```csharp
Given()
    .ApiResource($"{baseUrl}/api/products")
    .Post(new { name = "Product", price = 99.99m })
.When()
    .Execute()
.Then()
    .AssertStatusCode(201);
```

### PUT Request
```csharp
Given()
    .ApiResource($"{baseUrl}/api/products/1")
    .Put(new { name = "Updated Product", price = 149.99m })
.When()
    .Execute()
.Then()
    .AssertStatusCode(200);
```

### DELETE Request
```csharp
Given()
    .ApiResource($"{baseUrl}/api/products/1")
    .Delete()
.When()
    .Execute()
.Then()
    .AssertStatusCode(204);
```

## 💡 Tips and Best Practices

### 1. **Use Fixtures for Test Setup**
The `HttpSamplesFixture` handles the web application lifecycle, ensuring the API is available for all tests.

### 2. **Name Tests Descriptively**
Use the pattern: `ExampleXX_Scenario_ExpectedResult`
```csharp
Example01_GetAllProducts_ShouldReturnListOfProducts()
Example02_BasicAuth_WithInvalidCredentials_ShouldReturn401()
```

### 3. **Test Both Success and Failure Scenarios**
Always include tests for:
- Valid data (happy path)
- Invalid data (validation errors)
- Missing data (required field errors)
- Authentication failures

### 4. **Validate Response Structure**
Use `AssertJsonPath()` to validate response body:
```csharp
.AssertJsonPath("$.id", value => (int)value > 0, "ID should be positive")
.AssertJsonPath("$.name", value => !string.IsNullOrEmpty(value?.ToString()), "Name required")
```

### 5. **Reset State Between Tests**
If tests modify data, use a reset endpoint or fixture setup/teardown:
```csharp
Given()
    .ApiResource($"{_fixture.BaseUrl}/api/products/reset")
    .Post(new { })
.When()
    .Execute()
.Then()
    .AssertStatusCode(200);
```

### 6. **Use Test Categories**
Organize tests logically by authentication type or functionality.

### 7. **Document Complex Scenarios**
Use inline comments or `Skip` attribute with documentation for complex setups like certificate authentication.

## 📖 Additional Resources

- **XUnitAssured.Http GitHub**: [github.com/andrewBezerra/xunit-assured-net](https://github.com/andrewBezerra/xunit-assured-net)
- **xUnit Documentation**: [xunit.net](https://xunit.net/)
- **REST API Testing Best Practices**: Various online resources

## 🤝 Contributing

These samples are meant to demonstrate XUnitAssured.Http capabilities. Feel free to:
- Add more examples
- Improve existing tests
- Report issues or suggest improvements

## 📄 License

This sample project follows the same license as the XUnitAssured.Http package.

---

**Happy Testing! 🚀**

For questions or issues, please visit the [GitHub repository](https://github.com/andrewBezerra/xunit-assured-net).
