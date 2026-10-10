# XUnitAssured.Http

HTTP/REST API testing extensions for the [XUnitAssured.Net](https://github.com/andrewBezerra/XUnitAssured.Net) framework. Write expressive integration tests using a fluent `Given().When().Then()` DSL with full support for HTTP methods, headers, authentication, JSON path assertions, and schema validation.

## Installation

```bash
dotnet add package XUnitAssured.Http
```

## Quick Start

```csharp
using XUnitAssured.Http.Extensions;
using XUnitAssured.Http.Testing;

public class ProductTests : HttpTestBase<MyTestFixture>, IClassFixture<MyTestFixture>
{
    public ProductTests(MyTestFixture fixture) : base(fixture) { }

    [Fact]
    public void GetProduct_ShouldReturn200()
    {
        Given()
            .ApiResource("/api/products/1")
            .Get()
        .When()
            .Execute()
        .Then()
            .AssertStatusCode(200)
            .AssertJsonPath<string>("$.name", name => name.ShouldNotBeEmpty())
            .AssertJsonPath<decimal>("$.price", price => price.ShouldBeGreaterThan(0));
    }
}
```

## Fluent DSL Reference

### Request Setup

```csharp
Given()
    .ApiResource("/api/endpoint")          // Set target URL
    .WithHttpClient(client)                // Use custom HttpClient (e.g., WebApplicationFactory)
    .WithHeader("X-Custom", "value")       // Add request header
    .WithQueryParam("page", 1)             // Add query parameter
    .WithTimeout(30)                       // Set timeout in seconds
```

### A URL from an earlier step

A chain is described first and run when it executes, so a string interpolated while writing it is
built before any step has run. When the URL depends on a value an earlier step produces — the
usual case is creating a resource and reading it back — pass a function, built when the step runs:

```csharp
int productId = 0;

var readBack = await Given()
    .ApiResource("/api/products").Post(newProduct)
    .Validate((HttpStepResult created) => productId = created.JsonPath<int>("$.id"))
    .And()
    .ApiResource(() => $"/api/products/{productId}").Get()
    .ExecuteAsync();

readBack.Then().AssertStatusCode(200);
```

A 201 alone proves the API answered; reading the resource back proves it was stored.

### Arranging data

Creating a parent resource and keeping its id fits in one line. `ExtractAsync` runs the chain,
requires a 2xx, and returns the value; a failed arrange fails right there, with the status and the
body, instead of handing the test an empty id:

```csharp
var customerId = await Given().ApiResource("/customers").Post(customer).ExtractAsync<string>("$.id");
var (id, token) = await Given().ApiResource(route).Post(invite).ExtractAsync<string, string>("$.id", "$.token");
await Given().ApiResource($"/orders/{id}").Delete().EnsureSuccessAsync();   // no value needed
```

### HTTP Methods

```csharp
.Get()                                     // HTTP GET
.Post(body)                                // HTTP POST with JSON body
.Post()                                    // HTTP POST without body
.PostFormData(formDictionary)              // POST form-urlencoded
.Put(body)                                 // HTTP PUT with JSON body
.Patch(body)                               // HTTP PATCH with JSON body
.Delete()                                  // HTTP DELETE
```

### Assertions

```csharp
.When()
    .Execute()
.Then()
    .AssertStatusCode(200)                                              // Assert HTTP status code
    .AssertSuccess()                                                    // Assert IsValid = true
    .ValidateContract<Product>()                                        // Validate JSON schema against type
    .AssertJsonPath<int>("$.id", id => id.ShouldBe(1))                  // Assert JSON value with Shouldly
    .AssertJsonPath<string>("$.name", n => n.ShouldNotBeNullOrEmpty())   // Assert JSON string
    .Extract(out var result)                                            // Capture result for later use
    .Extract(r => myVar = r.StatusCode)                                 // Capture via callback
    .JsonPath<int>("$.id")                                              // Extract value from JSON
```

### Lists

A path can start at a root array (`$[0].id`), and `[*]` selects a value from every item
(`$[*].id`, `$.items[*].sku`). A filter is only tested when the test proves what it left out:

```csharp
.Then()
    .AssertJsonPathContains("$[*].id", matchingId)                      // must be in the result
    .AssertJsonPathNotContains("$[*].id", otherCityId)                  // must have been filtered out
    .AssertJsonPathCount("$.items", 2)                                  // array length, or values [*] selects
    .AssertJsonPathAll<string>("$[*].city", c => c == "Rio")           // every value; fails on an empty list
    .JsonPathAll<string>("$[*].id")                                     // extract every selected value
```

`AssertJsonPathAll` fails when the path selects nothing: an empty list satisfies any condition, so a
search that returned nothing would otherwise pass.

### Headers, cookies, Problem Details and body text

```csharp
.Then()
    .AssertHeader("Location", "/api/orders/42")                         // header names are case-insensitive
    .AssertNoHeader("X-Powered-By")
    .AssertSetCookie("session", c => c.HttpOnly && c.Secure && c.Path == "/auth")
    .AssertCookieCleared("session")                                     // expiry in the past or Max-Age=0
    .AssertProblemDetails(409, p => p.Extension<string>("code") == "ScheduleConflict")
    .AssertBodyContains("not found")
    .AssertBodyNotContains("invited@example.com")                       // the response must not leak it
```

`AssertCookieCleared` reads the expiry: checking that the header text mentions `expires=` is not the
same thing, since a date in the future mentions it too.

## Authentication

### Bearer Token

```csharp
Given()
    .ApiResource("/api/secure")
    .WithBearerToken("my-jwt-token")
    .Get()
.When().Execute()
.Then().AssertStatusCode(200);
```

### Basic Auth

```csharp
Given()
    .ApiResource("/api/secure")
    .WithBasicAuth("username", "password")
    .Get()
.When().Execute()
.Then().AssertStatusCode(200);
```

### API Key

```csharp
// Header
Given()
    .ApiResource("/api/secure")
    .WithApiKey("X-API-Key", "my-key")
    .Get()
.When().Execute()
.Then().AssertStatusCode(200);

// Query string
Given()
    .ApiResource("/api/secure")
    .WithApiKey("api_key", "my-key", ApiKeyLocation.Query)
    .Get()
.When().Execute()
.Then().AssertStatusCode(200);
```

### OAuth2 Client Credentials

```csharp
Given()
    .ApiResource("/api/secure")
    .WithOAuth2("https://auth.example.com/token", "client-id", "client-secret")
    .Get()
.When().Execute()
.Then().AssertStatusCode(200);
```

### Certificate (mTLS)

```csharp
Given()
    .ApiResource("/api/secure")
    .WithCertificate(new X509Certificate2("client.pfx", "password"))
    .Get()
.When().Execute()
.Then().AssertStatusCode(200);
```

### Custom Header Auth

```csharp
Given()
    .ApiResource("/api/secure")
    .WithAuthConfig(config => config.UseCustomHeader("X-Auth-Token", "my-token"))
    .Get()
.When().Execute()
.Then().AssertStatusCode(200);
```

### Automatic Authentication via testsettings.json

Configure authentication once and have it applied to every request automatically:

```json
{
  "testMode": "Remote",
  "http": {
    "baseUrl": "https://api.example.com",
    "timeout": 60,
    "authentication": {
      "type": "Bearer",
      "bearer": {
        "token": "your-jwt-token"
      }
    }
  }
}
```

Implement `IHttpClientAuthProvider` in your fixture:

```csharp
public class MyFixture : IHttpClientProvider, IHttpClientAuthProvider, IDisposable
{
    private readonly HttpSettings _settings;
    private readonly HttpClient _client;

    public MyFixture()
    {
        var settings = TestSettings.Load();
        _settings = settings.GetHttpSettings()!;
        _client = new HttpClient { BaseAddress = new Uri(_settings.BaseUrl) };
    }

    public HttpClient CreateClient() => _client;
    public HttpAuthConfig? GetAuthenticationConfig() => _settings.Authentication;

    public void Dispose() => _client?.Dispose();
}
```

Then use `Given(fixture)` — auth is applied automatically:

```csharp
Given(fixture)
    .ApiResource("/api/products")
    .Get()
.When().Execute()
.Then().AssertStatusCode(200);
```

## CRUD Example

```csharp
// GET all
Given()
    .ApiResource("/api/products")
    .Get()
.When().Execute()
.Then().AssertStatusCode(200);

// GET by ID
Given()
    .ApiResource("/api/products/1")
    .Get()
.When().Execute()
.Then()
    .AssertStatusCode(200)
    .AssertJsonPath<int>("$.id", id => id.ShouldBe(1));

// POST create
Given()
    .ApiResource("/api/products")
    .Post(new { name = "Laptop", price = 999.99m })
.When().Execute()
.Then()
    .AssertStatusCode(201)
    .AssertJsonPath<int>("$.id", id => id.ShouldBeGreaterThan(0));

// PUT update
Given()
    .ApiResource("/api/products/1")
    .Put(new { name = "Updated Laptop", price = 899.99m })
.When().Execute()
.Then().AssertStatusCode(200);

// DELETE
Given()
    .ApiResource("/api/products/1")
    .Delete()
.When().Execute()
.Then().AssertStatusCode(204);
```

## Local Testing with WebApplicationFactory

```csharp
public class LocalFixture : IHttpClientProvider, IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;

    public LocalFixture()
    {
        _factory = new WebApplicationFactory<Program>();
    }

    public HttpClient CreateClient() => _factory.CreateClient();
    public void Dispose() => _factory?.Dispose();
}

public class LocalTests : HttpTestBase<LocalFixture>, IClassFixture<LocalFixture>
{
    public LocalTests(LocalFixture fixture) : base(fixture) { }

    [Fact]
    public void GetProducts_ShouldReturn200()
    {
        Given()
            .ApiResource("/api/products")
            .Get()
        .When().Execute()
        .Then().AssertStatusCode(200);
    }
}
```

## Supported Frameworks

- .NET 8
- .NET 9
- .NET 10
- .NET 10

## Dependencies

- [XUnitAssured.Core](https://www.nuget.org/packages/XUnitAssured.Core) — DSL infrastructure and abstractions
- [Flurl.Http](https://www.nuget.org/packages/Flurl.Http) — HTTP client
- [NJsonSchema](https://www.nuget.org/packages/NJsonSchema) — JSON schema validation
- [Shouldly](https://www.nuget.org/packages/Shouldly) — Fluent assertions
- [Microsoft.AspNetCore.Mvc.Testing](https://www.nuget.org/packages/Microsoft.AspNetCore.Mvc.Testing) — WebApplicationFactory support

## Links

- [GitHub Repository](https://github.com/andrewBezerra/XUnitAssured.Net)
- [Full Documentation](https://github.com/andrewBezerra/XUnitAssured.Net#readme)
- [Report Issues](https://github.com/andrewBezerra/XUnitAssured.Net/issues)
- [NuGet Package](https://www.nuget.org/packages/XUnitAssured.Http)

## License

Apache-2.0 — see [LICENSE.md](https://github.com/andrewBezerra/XUnitAssured.Net/blob/main/LICENSE.md)
