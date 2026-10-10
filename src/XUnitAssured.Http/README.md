# XUnitAssured.Http

HTTP/REST API testing extensions for the [XUnitAssured.Net](https://github.com/andrewBezerra/XUnitAssured.Net) framework. Write expressive integration tests using a fluent `Given()` … `ExecuteAsync()` … `Then()` DSL with full support for HTTP methods, headers, authentication, JSON path assertions, and schema validation.

## Installation

```bash
dotnet add package XUnitAssured.Http
```

> Testing your own ASP.NET Core API? [XUnitAssured.Http.AspNetCore](https://www.nuget.org/packages/XUnitAssured.Http.AspNetCore) hosts it in memory with a client per identity, shared across the suite.

## Quick Start

```csharp
using XUnitAssured.Http.Extensions;
using XUnitAssured.Http.Testing;

public class ProductTests : HttpTestBase<MyTestFixture>, IClassFixture<MyTestFixture>
{
    public ProductTests(MyTestFixture fixture) : base(fixture) { }

    [Fact]
    public async Task GetProduct_ShouldReturn200()
    {
        var response = await Given()
            .ApiResource("/api/products/1")
            .Get()
            .ExecuteAsync();

        response.Then()
            .AssertStatusCode(200)
            .AssertJsonPath<string>("$.name", name => name.ShouldNotBeEmpty())
            .AssertJsonPath<decimal>("$.price", price => price.ShouldBeGreaterThan(0));
    }
}
```

`ExecuteAsync()` sends the request; `Then()` starts the assertions. A failed `AssertStatusCode`
shows the response body, which is usually where the reason is.

`Execute()`, the synchronous form, still exists so older suites keep compiling, but it blocks the
test thread while it waits — write new tests with `ExecuteAsync()`. `.When()` before it is
optional and changes nothing: keep it only if you like the Given/When/Then reading.

## Fluent DSL Reference

### Request Setup

```csharp
Given()                                    // Given(client): every request of the scenario goes through client
    .ApiResource("/api/endpoint")          // Set target URL
    .WithHttpClient(client)                // Use a custom HttpClient for this request only
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
.PostRaw(text, "application/json")         // POST the text as written, no serialization
.PutRaw(text, contentType)                 // and PUT, PATCH: PatchRaw
```

The raw verbs are for what a serializer never produces: malformed JSON on purpose, a field of the
wrong type, XML, plain text. The Content-Type goes as given, parameters included.

### Assertions

```csharp
(await Given()
    .ApiResource("/api/products/1")
    .Get()
    .ExecuteAsync())
.Then()
    .AssertStatusCode(200)                                              // the body is shown when it fails
    .AssertStatusCode(403, 404)                                         // any of these, e.g. access denied by design
    .AssertSuccess()                                                    // Assert IsValid = true
    .ValidateContract<Product>()                                        // Validate JSON schema against type
    .AssertJsonPath<int>("$.id", id => id.ShouldBe(1))                  // Assert JSON value with Shouldly
    .AssertJsonPath<string>("$.name", n => n.ShouldNotBeNullOrEmpty())   // Assert JSON string
    .Extract(out var result)                                            // Capture result for later use
    .Extract(r => myVar = r.StatusCode)                                 // Capture via callback
    .JsonPath<int>("$.id")                                              // Extract value from JSON
```

### Null or missing

A field present with `null` and a field that is not there are different answers, and each has its
own assertion. The failure says which of the two the response had:

```csharp
.Then()
    .AssertJsonPathNull("$.contactId")                                  // "contactId": null
    .AssertJsonPathMissing("$.password")                                // no "password" at all
```

`AssertJsonPathMissing` needs everything before the last part of the path to exist. If an
earlier part is missing too, the path is more likely wrong than the field absent, so the assertion
fails.

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

To use a cookie the response set, for example to replay a session from another client, read it from
the result. `SetCookie` returns null when the response did not set it:

```csharp
var signIn = (await Given().ApiResource("/auth/login").Post(credentials).ExecuteAsync()).GetResult();
var session = signIn.SetCookie("sid")!.Value;                           // attributes too: .HttpOnly, .Path, .Expires
```

### The request that was sent

`result.Request` is the request as it went out, read after the client sent it: method, address,
headers, cookies and body. It includes what the client added on the way — a client that keeps
cookies puts the session in the `Cookie` header, and that is what a session test is about:

```csharp
using var browser = api.ClientWithCookies();
// ... sign in, then sign out ...

(await Given(browser)
    .ApiResource("/auth/refresh")
    .Post()
    .ExecuteAsync())
.Then()
    .AssertStatusCode(401)
    .AssertNoSentCookie("refresh")                                      // the revoked session is gone
    .AssertRequestHeader("X-Correlation-Id", id => id.Length > 0);     // names are case-insensitive
```

`AssertSentCookie("name", v => ...)` requires the cookie, and optionally checks its value.

### Requests at once

For a test whose subject is the concurrency itself — simultaneous creates under one parent, a
double submit, a race on a counter — `Concurrently` sends the request several times at once,
through the same client. Every copy is started before any is awaited.

```csharp
var names = Enumerable.Range(1, 6).Select(i => $"Member {i}").ToList();

(await Given()
    .ApiResource($"/api/teams/{teamId}/members")
    .Post()
    .Concurrently(names.Select(n => new { Name = n }))                  // one request per body
    .ExecuteAsync())
.Then()
    .AssertStatusCode(201)                                              // every one of the six
    .AssertEach(r => r.AssertHeader("Location", l => l.StartsWith("/api/members/")));
```

`Concurrently(3)` sends the same body three times. The result is a `ConcurrentHttpStepResult`: its
`Responses` come in the order of the bodies, `AssertStatusCode` checks all of them and lists every
code when one differs, and `AssertEach` runs any other assertion on each response, naming the ones
that failed. An assertion about a single body or header on it says to use `AssertEach` instead.

## What the system did besides answering

Some of the most valuable E2E tests are about side effects: what the system logged, and which
outside services it called.

### Logs: `LogCapture`

```csharp
using XUnitAssured.Core.Logging;

var logs = new LogCapture();
var app = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
    b.ConfigureLogging(log => { log.SetMinimumLevel(LogLevel.Trace); log.AddProvider(logs); }));

logs.Clear();                                                   // only what the action below logs
// ... the request under test ...
logs.AssertLoggedOnce("MyApp.Errors", LogLevel.Error)           // one error, not three
    .AssertLogged("MyApp.Security", LogLevel.Warning, e => e.EventId.Id == 9001)
    .AssertNothingContains(patientName);                        // message, exception, values and scopes
```

`AssertNothingContains` looks at everything an entry carries — the message, the exception with
its stack trace, the structured values and the active scopes — since a value kept out of the
message can still ride in a scope.

### Outbound calls: `OutboundHttpCapture`

Stands in for an outside service at the HTTP boundary: it answers what the test configures and
records every request. Replacing the component that calls the service would hide the logic under
test; here everything up to the request itself runs for real.

```csharp
using XUnitAssured.Http.Testing;

var push = new OutboundHttpCapture()
    .RespondWith(HttpStatusCode.Created)
    .When("https://push.example/devices/gone*", HttpStatusCode.Gone);

// in ConfigureTestServices:
services.AddHttpClient("push").ConfigurePrimaryHttpMessageHandler(() => push);

// ... the request under test ...
push.AssertSent("https://push.example/devices/*", times: 2)
    .AssertSent("https://push.example/*", predicate: r => r.BodyText.Contains("\"lang\":\"es\""))
    .AssertNotSent("https://push.example/devices/removed*");
```

`*` stands for any run of characters. It keeps working after a client that owns it is disposed, so
`IHttpClientFactory` recycling handlers does not break it mid-test.

## Authentication

### Bearer Token

```csharp
(await Given()
    .ApiResource("/api/secure")
    .WithBearerToken("my-jwt-token")
    .Get()
    .ExecuteAsync())
.Then().AssertStatusCode(200);
```

### Basic Auth

```csharp
(await Given()
    .ApiResource("/api/secure")
    .WithBasicAuth("username", "password")
    .Get()
    .ExecuteAsync())
.Then().AssertStatusCode(200);
```

### API Key

```csharp
// Header
(await Given()
    .ApiResource("/api/secure")
    .WithApiKey("X-API-Key", "my-key")
    .Get()
    .ExecuteAsync())
.Then().AssertStatusCode(200);

// Query string
(await Given()
    .ApiResource("/api/secure")
    .WithApiKey("api_key", "my-key", ApiKeyLocation.Query)
    .Get()
    .ExecuteAsync())
.Then().AssertStatusCode(200);
```

### OAuth2 Client Credentials

```csharp
(await Given()
    .ApiResource("/api/secure")
    .WithOAuth2("https://auth.example.com/token", "client-id", "client-secret")
    .Get()
    .ExecuteAsync())
.Then().AssertStatusCode(200);
```

### Certificate (mTLS)

```csharp
(await Given()
    .ApiResource("/api/secure")
    .WithCertificate("client.pfx", "password")
    .Get()
    .ExecuteAsync())
.Then().AssertStatusCode(200);
```

### Custom Header Auth

```csharp
(await Given()
    .ApiResource("/api/secure")
    .WithAuthConfig(config => config.UseCustomHeader("X-Auth-Token", "my-token"))
    .Get()
    .ExecuteAsync())
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
(await Given(fixture)
    .ApiResource("/api/products")
    .Get()
    .ExecuteAsync())
.Then().AssertStatusCode(200);
```

## CRUD Example

```csharp
// GET all
(await Given()
    .ApiResource("/api/products")
    .Get()
    .ExecuteAsync())
.Then().AssertStatusCode(200);

// GET by ID
(await Given()
    .ApiResource("/api/products/1")
    .Get()
    .ExecuteAsync())
.Then()
    .AssertStatusCode(200)
    .AssertJsonPath<int>("$.id", id => id.ShouldBe(1));

// POST create
(await Given()
    .ApiResource("/api/products")
    .Post(new { name = "Laptop", price = 999.99m })
    .ExecuteAsync())
.Then()
    .AssertStatusCode(201)
    .AssertJsonPath<int>("$.id", id => id.ShouldBeGreaterThan(0));

// PUT update
(await Given()
    .ApiResource("/api/products/1")
    .Put(new { name = "Updated Laptop", price = 899.99m })
    .ExecuteAsync())
.Then().AssertStatusCode(200);

// DELETE
(await Given()
    .ApiResource("/api/products/1")
    .Delete()
    .ExecuteAsync())
.Then().AssertStatusCode(204);
```

## Local Testing with WebApplicationFactory

To test your own ASP.NET Core API in memory, use
[XUnitAssured.Http.AspNetCore](https://www.nuget.org/packages/XUnitAssured.Http.AspNetCore):
`ApiFixture<Program>` hosts it with `WebApplicationFactory`, gives `Given()` its client, and adds a
client per identity, clients with and without cookies, its services and its logs.

```csharp
public sealed class MyApi : ApiFixture<Program>
{
    protected override void ConfigureApi(IWebHostBuilder api) => api.UseEnvironment("Testing");
}

[CollectionDefinition("API")]
public sealed class ApiCollection : ICollectionFixture<MyApi>;

[Collection("API")]
public class LocalTests(MyApi api) : HttpTestBase<MyApi>(api)
{
    [Fact]
    public async Task GetProducts_ShouldReturn200() =>
        (await Given()
            .ApiResource("/api/products")
            .Get()
            .ExecuteAsync())
        .Then().AssertStatusCode(200);
}
```

## Supported Frameworks

- .NET 8
- .NET 9
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
