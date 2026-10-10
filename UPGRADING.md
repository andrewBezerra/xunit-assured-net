# Upgrading to 6.0.0

From 5.1.x. Most chains compile and run unchanged, because the shape of the DSL did not change.
What changed is **when** a chain runs, and that is the part worth reading carefully.

Every corrected form below is compiled as part of the test suite, in
`src/XUnitAssured.Tests/UpgradingExamplesTests.cs`. If one of them stops being true, the build
fails and this page gets fixed with the code.

## The short version

| Symptom | Section |
|---|---|
| A test passes but clearly never touched the system | [A chain that never runs](#a-chain-that-never-runs) |
| A URL or topic comes out with `0` or an empty string in it | [A value an earlier step produced](#a-value-an-earlier-step-produced) |
| `CS0411` on `Execute()` or `ExecuteAsync()` | [A chain kept in a variable](#a-chain-kept-in-a-variable) |
| A custom `ITestStep` no longer compiles | [A custom step](#a-custom-step) |
| `ExecuteCurrentStepAsync` does not exist | [A custom step](#a-custom-step) |
| A warning about `playwrightsettings.json` | [Configuration](#configuration) |
| The project targets `net7.0` | [Targets](#targets) |
| A test about a timeout or a refused connection now fails | [A request that gets no answer](#a-request-that-gets-no-answer) |

## A chain that never runs

**This is the only change that can make a green test lie, so it comes first.**

In 5.x a step ran where it was written: `And()`, `On()` and `Validate(...)` each carried out
whatever was pending. A chain that never called `Execute()` still did most of its work.

In 6.0.0 a chain **describes**, and executing carries it out. A chain without a final
`ExecuteAsync()` or `Execute()` compiles, runs, asserts nothing and passes.

```csharp
// Before — the two steps ran at the And()
Given(api, kafka)
    .ApiResource("/api/orders").Post(order).Validate(r => r.StatusCode.ShouldBe(201))
    .And().On().Topic("orders.created").Consume().ValidateMessage<OrderCreated>(m => ...);

// After — one await, at the end
var assertions = await Given(api, kafka)
    .ApiResource("/api/orders").Post(order).Validate(r => r.StatusCode.ShouldBe(201))
    .And().On().Topic("orders.created").Consume().ValidateMessage<OrderCreated>(m => ...)
    .ExecuteAsync();
```

Nothing in the compiler will find these for you. Search your suite for chains that start with
`Given(` and reach a `;` without an `Execute`.

The reason for the change: an honestly asynchronous DSL that ran each step where it was written
would need an `await` at every `And()`, and `await x.And()` in the middle of a fluent chain is not
something anyone writes.

## A value an earlier step produced

A check is a lambda, so it closes over the variable and runs later, after the step produced its
result. Those are unaffected, and they are most of what tests do.

A value **interpolated into a string** is different: that string is built while the chain is being
described, before the step that produces the value has run. In 5.x it worked by accident, because
the earlier step had already run.

```csharp
// Before
.And().NavigateTo($"/orders/{orderId}")      // orderId is still 0 here

// After
.And().NavigateTo(() => $"/orders/{orderId}")
```

`NavigateTo` and, since 6.2.0, `ApiResource` have a deferred overload, so creating a resource and
reading it back fits in one chain:

```csharp
.And().ApiResource(() => $"/api/orders/{orderId}").Get()
```

`Topic`, `Fill` and the rest take a string, and there is no version of them that takes a function.
When a later step needs a value from an earlier one in a position like that, use two chains — shown
here with `ApiResource`, where it also still works:

```csharp
var orderId = 0;

var criado = await Given(api)
    .ApiResource("/api/orders")
    .Post(order)
    .Validate((HttpStepResult r) => orderId = r.JsonPath<int>("$.id"))
    .ExecuteAsync();

criado.Then().AssertStatusCode(201);

var lido = await Given(api)
    .ApiResource($"/api/orders/{orderId}")
    .Get()
    .ExecuteAsync();
```

Two chains are not a workaround for a missing feature. They say plainly that the second request
depends on the first having happened.

## A chain kept in a variable

`Execute()` is now an instance method on the scenario type each package returns, which is what
lets the compiler pick the right assertion builder when a project references more than one
package. The `Execute(this ITestScenario)` extension methods are gone.

A chain written as one expression is unaffected. A chain stored in a variable **typed as
`ITestScenario`** no longer resolves:

```csharp
// Before
ITestScenario scenario = Given().ApiResource("/orders").Get();
scenario.Execute();                     // 6.0.0: CS0411

// After — name the package's type, or use var
IHttpScenario scenario = Given().ApiResource("/orders").Get();
scenario.Execute().Then().AssertStatusCode(200);
```

The types are `IHttpScenario`, `IKafkaScenario` and `IBrowserScenario`. `var` works and is usually
what you want.

In exchange, a cross-boundary chain now ends with a plain `.Execute()`. It used to need
`PlaywrightBddExtensions.Execute(scenario)` spelled out, because each package defined its own
extension and referencing two of them made the call ambiguous.

## A custom step

`ITestStep.ExecuteAsync` takes a `CancellationToken`. If you implement the interface, add the
parameter:

```csharp
// Before
public Task<ITestStepResult> ExecuteAsync(ITestContext context)

// After
public Task<ITestStepResult> ExecuteAsync(
    ITestContext context, CancellationToken cancellationToken = default)
```

Pass it to whatever you await inside. A step that accepts the token and ignores it compiles and
silently cannot be cancelled.

`ITestScenario.ExecuteCurrentStepAsync()` is gone, replaced by two members that split what it did:
`AddValidation(Action<ITestStepResult>)` registers a check, and
`ExecutePendingAsync(CancellationToken)` runs everything described so far. You only need these if
you wrote your own DSL verbs; the built-in ones call them for you.

## Configuration

Browser settings moved into `testsettings.json`, under a `playwright` section next to `http` and
`kafka`:

```json
{
  "playwright": {
    "baseUrl": "https://app.example.com",
    "headless": true,
    "browser": "Chromium"
  }
}
```

`playwrightsettings.json` is still read, and the run prints once where its contents should move
to. It will be removed in a future major. The old file also required `CopyToOutputDirectory`; the
section does not, which is one less thing to get wrong.

`httpsettings.json` is unchanged: it is a fallback read only when an `HttpRequestStep` runs
without a fixture or explicit authentication. There is no `kafkasettings.json` and never was one
that the framework read — Kafka settings live in the `kafka` section of `testsettings.json`.

## Targets

`net7.0` is gone, out of support since May 2024. The targets are `net8.0`, `net9.0` and `net10.0`.

## A request that gets no answer

A behaviour fix that can turn a passing test red, which is why it is listed here.

A timeout, a cancellation, a refused connection or a name that does not resolve used to be
reported as an HTTP error response: status `0`, `Success` false, an **empty** error list, and the
step marked `Succeeded`. They are now failures, with the reason in `Errors` and the step marked
`Failed`. The rule is that no status code means no response arrived.

If a test of yours asserted the old shape — an empty `Errors`, or `Metadata.Status` being
`Succeeded` after a timeout — it was codifying a defect and will now fail. Assert the failure
instead.

Two smaller behaviour changes in the same area:

- `TimeoutSeconds` now applies when you supply your own `HttpClient`. It used to be stored and
  ignored on that path, so the wait was the client's own default of 100 seconds.
- `CancellationToken` now reaches the HTTP call instead of stopping at the step's signature.

## Kafka consume

Consuming now reads without waiting and gives the thread back between attempts, every 50ms,
instead of holding it for the whole timeout. `IConsumer` has no asynchronous consume, so this is
not "never blocks": the thread is held for microseconds at a time rather than seconds.

A side effect worth knowing: a message is noticed sooner than it used to be, because the previous
implementation waited 250ms before looking. A test that depended on that delay may see the message
earlier than before.
