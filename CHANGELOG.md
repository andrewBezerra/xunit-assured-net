# Changelog

All notable changes to the XUnitAssured packages. Upgrading from 5.1.x? See [UPGRADING.md](UPGRADING.md).

## v6.7.0 (Current — a client per scenario, null or missing, the cookie set)

Additive. Upgrading from 6.6.x requires no code changes. The rest of the usability pass driven by a real consumer suite's workarounds.

- **`Given(httpClient)`** (#108), on `ScenarioDsl` and `HttpTestBase`, in place of `Given().WithHttpClient(client)`, which the consumer suite wrote 74 times and shortened with three local helpers. Every HTTP step of the scenario goes through the client, steps after `.And()` included; `WithHttpClient` only set it for the current step. The scenario does not dispose the client, and `HttpTestBase.Given(client)` leaves the fixture's client and authentication out. `WithHttpClient` stays. The READMEs use the new form.
- **`AssertStatusCode(403, 404)`** (#109) accepts any of the codes, for a path that answers either way by design, and lists them on failure along with the body. With `Concurrently`, every response must be one of them. A single code works as before.
- **`AssertJsonPathNull` and `AssertJsonPathMissing`** (#109) tell a field present with `null` from a field that is not there; each failure says which one the response had. `AssertJsonPathMissing` requires everything before the last part of the path to exist, so a typo earlier in the path fails instead of passing.
- **`HttpStepResult.SetCookie(name)`** (#109) returns the cookie a response set, value and attributes, or null — to replay a session from another client without parsing `Set-Cookie` by hand. When the name is set twice, the last one wins, as for the client. `SetCookies` lists them all.

## v6.6.1 (failures that say why)

No API changes. Upgrading from 6.6.0 requires no code changes. The first two items of a usability pass driven by a real consumer suite's workarounds.

- **A failed `AssertStatusCode` shows the response body** (#106). The message used to say only "expected 201 but got 400"; the reason was in the body, and the consumer suite replaced the assertion with a `Validate` that repeated the check to add it. The body is cut at 1,000 characters, saying how many were left out; with no response, the message says why (connection refused, timeout). With `Concurrently`, each response that differed is listed with its position, status and body. `EnsureSuccessAsync` and `ExtractAsync` use the same description.
- **The `XUnitAssured.Http` README teaches `ExecuteAsync`** (#107). It showed the synchronous `Execute()` 16 times and `.When()` in 17 examples, while the root README uses only `ExecuteAsync`. It now uses `ExecuteAsync` throughout, says once what `Execute()` is for and that `.When()` is optional, and points to `ApiFixture` for testing your own API. The XML doc examples follow. The mTLS example called a `WithCertificate` overload that does not exist, and ".NET 10" was listed twice; both fixed.

## v6.6.0 (requests at once, the request sent, raw bodies)

Additive. Upgrading from 6.5.x requires no code changes. The three things that still sent a real consumer suite from the DSL to a raw `HttpClient` (#101).

- **`Concurrently(n)` and `Concurrently(bodies)`** (`XUnitAssured.Http`). Sends the request several times at once through the same client, every copy started before any is awaited — for tests whose subject is the concurrency itself. The result is a `ConcurrentHttpStepResult` with the `Responses` in the order of the bodies. `AssertStatusCode` checks every response and lists all codes when one differs; `AssertEach(r => ...)` runs any assertion on each response and names the ones that failed. An assertion about a single body or header on it fails saying to use `AssertEach`, instead of picking one response at random.
- **`HttpStepResult.Request`**: the request as it went out — method, address, headers, cookies and body — read after sending, so it includes what the client added on the way, such as the session cookie a cookie-keeping client sends. New assertions `AssertRequestHeader`, `AssertSentCookie` and `AssertNoSentCookie`. A request that got no answer still shows what was being sent.
- **`PostRaw`, `PutRaw`, `PatchRaw`**: a body sent exactly as written, with the Content-Type given — malformed JSON on purpose, a wrong type, XML.

## v6.5.0 (testing your own API)

Additive: a new package. Upgrading from 6.4.x requires no code changes.

- **`ApiFixture<TProgram>`** (#100, new package `XUnitAssured.Http.AspNetCore`, net8.0 to net10.0). Hosts your own ASP.NET Core API in memory with `WebApplicationFactory` and implements `IHttpClientProvider` and `IHttpClientAuthProvider`, so `Given(fixture)` and `HttpTestBase` work as before. `CreateClient()` is the shared client, with the identity `ConfigureClient` gives it; `ClientFor(("Header", "value"), ...)` is a new client as someone else, its headers replacing those of the same name; `ClientWithCookies()` and `ClientWithoutCookies()` for sessions that live in a cookie; `Services` and `CreateScope()`; `ConfigureApi` and `Authentication` to override. In a real consumer suite this replaced a ~140-line fixture with ~80, most of it the environment's own settings.
- **Sharing is the documented default.** The package README and XML docs lead with a collection fixture: with an `IClassFixture` per class, the consumer suite spent about 70 of its 118 seconds starting the API, migrations included.
- **Log capture that works with any logging library.** With `CaptureLogs` on, `Logs` (a `LogCapture`) records every entry the API writes, at every level. It wraps the API's `ILoggerFactory` instead of adding a provider: a provider is ignored under `UseSerilog`, and the environment's minimum level would filter the rest. The console output is unchanged.

## v6.4.2 (documentation and samples)

Documentation and samples. No package code changes; upgrading from 6.4.1 requires no code changes.

- **The README says only what is true.** "Every Quick Start snippet is compiled" held for four of nine blocks; the HTTP and Kafka authentication, Kafka batch and `BrowserAppFixture` snippets are now compiled on every build too, and written async like the rest. The supported .NET versions name the one net10-only package, the contributing command skips browser tests as CI does, and the architecture diagram and the configuration example include `Playwright.AspNetCore` and the `rabbitmq` section.
- **The README shows what 6.2 to 6.4 added**, which had dropped out with the release notes: header, cookie and Problem Details assertions, list JSON paths, `ExtractAsync`, `LogCapture` and `OutboundHttpCapture`, and the page's network. Shorter, too — the sample lists folded into one table, and the MCP from-source setup moved to the MCP package README.
- **Samples no longer fail one run in ten** (#88). The sample API kept its products in static fields, shared by every test host in the process, so one class's reset could remove a product another class had just created. Each host now has its own store: 7 failures in 60 runs before, 0 in 60 after.

## v6.4.1 (service workers and fixed ports)

Fixes from migrating a real consumer suite's browser tests to 6.4.0. Upgrading from 6.4.0 requires no code changes.

- **`PlaywrightSettings.BlockServiceWorkers`** (`XUnitAssured.Playwright`, default `false`). The consumer app is a PWA whose service worker handles data requests, and a request a worker makes for the page bypasses the page's routes: from the first reload on, `InterceptRoute` answered nothing. With the setting on, the browser context blocks service workers and the route sees the requests again. `AssertIntercepted` failing with nothing answered now points to the setting.
- **A clear error when a fixed port is taken** (`XUnitAssured.Playwright.AspNetCore`). With a fixed `ApiPort`, an `IClassFixture` per test class makes xUnit start two fixtures in parallel on the same port. `BrowserAppFixture` now says so and points to a collection fixture, instead of Kestrel's bare "address already in use". The package README shows the collection and the service worker setting.

## v6.4.0 (the page's network, and browser tests in CI)

Additive, plus one behavior fix: browser tests that reach the network, and browser tests that run in CI. Upgrading from 6.3.x requires no code changes, except where a browser-only chain relied on `And()` not starting a new step (see below).

In a real consumer suite, the four browser tests for the session used the raw Playwright API because the DSL had nothing for the network, needed the API and the front-end started by hand, and stayed out of CI. One of them went stale there: the front-end changed how it stores the session, the route it intercepted matched nothing, and the test kept passing.

- **The page's network in the DSL** (#82, `XUnitAssured.Playwright`). `InterceptRoute(pattern, status, times, body)` answers matching requests and is removed when its step ends; `AssertIntercepted(pattern, atLeast)` fails when the route matched nothing — the check the stale test lacked. `Reload()`. `FetchFromPage(url, method, body)` makes a request from inside the page with its cookies (`credentials: 'include'`), so SameSite, the cookie `Path` and CORS with credentials apply; it also takes `Func`s, built when the step runs, to carry a value an earlier step returned. Results in `InterceptedRequests`, `Fetches` and `LastFetch` (`PageFetch`), with `AssertFetchStatus`, `AssertFetchJsonPath<T>` and `FetchJsonPath<T>`. A request CORS refuses comes back with status 0. `Validate(Action<PlaywrightStepResult>)` to read a step's result mid-chain.
- **`BrowserAppFixture<TProgram>`** (#82, new package `XUnitAssured.Playwright.AspNetCore`, net10.0 only — `WebApplicationFactory.UseKestrel` arrived in Mvc.Testing 10). Hosts the API on a real port, serves the built front-end on another with a fallback to `index.html`, and points the browser at it. `ApiUrl`, `AppUrl`, `Api` (services and `CreateClient()` for arranging data), `ApiPort` / `AppPort` (0 picks a free port), `AppDirectory`, `ConfigureApi`. Two ports are two origins, as in production.
- **Fix: `And()` starts a new step in browser-only chains.** Browser verbs kept adding to the current browser step regardless of `And()`, so a `Validate` written between actions only ran after all of them. A chain that put `And()` between browser verbs and expected one step now gets two.

## v6.3.0 (capturing logs and outbound calls)

Additive: capturing what the system did besides answering — what it logged, and which outside services it called. Upgrading from 6.2.x requires no code changes.

Some of the most valuable E2E tests guard privacy, security and observability, and in a real consumer suite each of them rebuilt the capture by hand: three test classes wrote their own `ILoggerProvider`, and two tests built a stand-in for an outside service at the HTTP boundary.

- **`LogCapture`** (#81, `XUnitAssured.Core.Logging`). An `ILoggerProvider` that records category, level, event id, message, exception, structured values and scopes. `AssertLogged`, `AssertLoggedOnce` (“one error opens one alert, not three”), `AssertNotLogged`, and `AssertNothingContains`, which looks at the message, the exception, the structured values and the scopes — a value kept out of the message can still ride in a scope. `Clear()` for fixtures shared by many tests.
- **`OutboundHttpCapture`** (#81, `XUnitAssured.Http.Testing`). An `HttpMessageHandler` that stands in for an outside service: it answers what the test configures (`RespondWith`, `When(pattern, ...)` with `*` wildcards) and records every request. `AssertSent(pattern, times, predicate)` and `AssertNotSent`, listing what was sent on failure. It works as the primary handler of a named `IHttpClientFactory` client and keeps working after a client that owns it is disposed.

`XUnitAssured.Core` now depends on `Microsoft.Extensions.Logging.Abstractions` 9.0.8.

## v6.2.0 (HTTP behavior tests in the DSL)

Additive: four HTTP additions for writing behavior tests in the DSL, and one fix. Upgrading from 6.1.x requires no code changes.

The four came from measuring a real consumer suite (266 E2E tests): where tests left the DSL for a raw `HttpClient`, and why.

- **`ApiResource(Func<string>)`** (#78). The URL is built when the step runs, as `NavigateTo(Func<string>)` already did, so creating a resource and reading it back fits in one chain. A 201 alone proves the API answered; reading the resource back proves it was stored.
- **Lists in JSON paths** (#77). A path can start at a root array (`$[0].id`), and `[*]` selects a value from every item (`$[*].id`, `$.items[*].sku`). New verbs: `AssertJsonPathContains`, `AssertJsonPathNotContains`, `AssertJsonPathCount`, `AssertJsonPathAll`, and `JsonPathAll` to extract. A filter is only tested when the test proves what it left out. `AssertJsonPathAll` fails when the path selects nothing, since an empty list satisfies any condition.
- **Response assertions** (#79). `AssertHeader` / `AssertNoHeader` (names case-insensitive), `AssertSetCookie` with the cookie read into its attributes, `AssertCookieCleared` (expiry in the past or `Max-Age=0` — a future `Expires` fails it), `AssertProblemDetails` for RFC 7807 / 9457 bodies with `Extension<T>("code")`, and `AssertBodyContains` / `AssertBodyNotContains`.
- **`ExtractAsync` and `EnsureSuccessAsync`** (#80). Arrange data in one line and get the value back: `var id = await Given().ApiResource("/orders").Post(order).ExtractAsync<string>("$.id");`. A failed arrange fails right there, with the status and the body, instead of handing the test an empty id.
- **Fix: `JsonPath<JsonElement>` is usable after it returns.** It returned an element bound to a `JsonDocument` that had already been disposed, so the first use threw `ObjectDisposedException`. It now returns a clone.

The JSON path navigator is shared by Http, Kafka and RabbitMQ, so root-array paths (`$[0]`) now also work in the Kafka and RabbitMQ `JsonPath`. Every path that worked before reads the same, including the `KeyNotFoundException` for a missing property.

## v6.1.1 (documentation and release process)

Documentation and release process. No API changes; upgrading from 6.1.0 requires no code changes.

- **README rewritten for the first visit.** The Quick Start is asynchronous (`ExecuteAsync()`), like the scenario at the top, and shows the HTTP fixture it needs, so the first example runs as written. RabbitMQ, added in 6.1.0, now appears everywhere the other boundaries do: features, Quick Start and the architecture diagram.
- **The Quick Start is compiled on every build**, like the cross-boundary scenario: `ReadmeQuickStartExamplesTests.cs` fails the build if a snippet stops matching the DSL.
- **Version history moved to [CHANGELOG.md](https://github.com/andrewBezerra/xunit-assured-net/blob/main/CHANGELOG.md).** The README keeps a short "What's New".
- **Package versions in the README come from NuGet badges** instead of a hard-coded number that went stale on each release.
- **Every release now has a tag and a GitHub release.** Merging to `main` checks that the version is new, builds and tests, creates the annotated tag `v<version>` and a release with these notes, and only then publishes to NuGet, still behind a manual approval. Until 6.1.0 tags were created by hand and none became a GitHub release.

## v6.1.0 (RabbitMQ, and the 6.0.0 regressions)

Additive: one new package, and correctness fixes with no API changes. Upgrading from 6.0.0 requires no code changes.

- **A verb that reconfigures a step no longer adds a second execution.** Steps are `init`-only,
  so every verb that changes one value rebuilds the whole step; when 6.0.0 made the chain
  describe, that rebuild started appending a planned step instead of replacing the one being
  described. A consume chain then consumed twice: the first step took the message and the second
  found nothing, and the assertions read the last result. `And()` and `On()` are what start a new
  step, which is what they already meant. This affects every package and was found by running a
  round trip against a real broker.
  A step of another **type** is always a new step, with or without `And()`: mixing packages
  without the boundary verb used to replace the previous step, so
  `ApiResource(...).Get().NavigateTo(...)` dropped the HTTP request and said nothing.
- **New package: `XUnitAssured.RabbitMq`.** Publish and consume steps for RabbitMQ on the
  official client, configured from a `rabbitmq` section of `testsettings.json`. Its verbs are
  members of `IRabbitMqScenario` rather than extensions on `ITestScenario`, which is what lets a
  test project reference it alongside Kafka and write both chains in one file: `Consume`,
  `WithTimeout` and `ValidateMessage` are the vocabulary of messaging, not of one broker.
  `RabbitMQ.Client` 7 is asynchronous end to end, so the steps await real I/O. Its
  result reads the consumed message by type or by JSON path, through the same navigator the Http
  and Kafka packages use.
  A publish that reaches no queue fails instead of reporting success: AMQP accepts a publish to an
  exchange matching no binding and drops the message, which is a topology mistake that otherwise
  passes in silence. `AllowingUnroutable()` opts out where that is the point of the test.
  Topology verbs complete the picture: `DeclareQueue()`, `DeclareExchange(type)` and
  `BindQueueTo(exchange, routingKey)`. Without them a test that needed a queue dropped to the
  client API, which this repository's own round-trip tests were doing in a helper.
  `Rejecting(requeue)` and `DeclareQueue(deadLetterExchange:)` make the discard path testable: a
  message rejected without requeue lands in the queue's dead-letter exchange, which is a test
  people want to write and could not.
  `ConsumeBatch(n)` brings the package level with Kafka's batch consume, and `AssertMessageCount`
  is how a test says how many it expected.
- **Authentication survives the consume verbs.** The Kafka steps are `init`-only, so each verb
  that changes one value rebuilt the whole step by hand, and five of them left `AuthConfig`
  out: `WithTimeout`, `WithGroupId`, `WithBootstrapServers`, `WithSchema` and
  `WithConsumerConfig`. Authenticating and then adjusting any of those replaced the explicit
  credential with the fixture's, or connected with none at all. The four steps now have a copy
  constructor and the verbs use it, so a property added later is carried over by default.
- **An explicitly configured broker or group is no longer discarded.** The Kafka steps decided
  "not configured" by comparing the value against the default, so a project that set
  `localhost:9092` on purpose had its value replaced by the fixture's. Absence is now recorded
  on the step instead of inferred, and `localhost:9092` is exactly the address a local broker
  uses, so it was the value most likely to be written by hand.
- **The MCP server's reference tables name methods that exist.** `list_http_dsl_methods`
  advertised `WithApiKeyInQuery`, `WithOAuth2ClientCredentials` and `WithCustomHeaderAuth`, none
  of which are in the API, while its own description says to use it as a reference. The tables
  also now cover the 6.0.0 surface, and each tool description says when not to use it and what
  it does not return.
- **The result-type error message names the cause.** It used to say to pick the right
  `Execute<T>()`, which stopped being the mechanism in 6.0.0. It now names both result types
  and points at the usual cause, a lambda with no parameter type binding to another package's
  overload.
- **Fragments in the authentication guides are marked as such**, by ending without a semicolon,
  so copying one does not compile instead of becoming a test that asserts nothing.

## v6.0.0 (one `Execute()`, one configuration file)

> Upgrading from 5.1.x? **[UPGRADING.md](UPGRADING.md)** says what to change in your code, with
> the one silent change first. Its examples are compiled as part of the test suite.

Breaking. Source-compatible for a chain written the usual way; the changes bite where a
scenario was stored in a variable, a custom step was implemented, or an old file name was
relied on.

- **A cross-boundary chain ends with a plain `.Execute()`.** Each package's chain methods
  return their own scenario type — `IHttpScenario`, `IKafkaScenario`, `IBrowserScenario` —
  and `Execute()` is an instance method on it, so the compiler picks the builder from the
  receiver. The cross-boundary scenario at the top of the README used to end with
  `PlaywrightBddExtensions.Execute(scenario)` spelled out.
  *Binary-breaking:* 157 extension methods changed their return type.
- **`ValidateMessage` keeps the chain typed**, like every sibling verb. It returned the untyped
  scenario, so a Kafka-only chain that ended with it would not compile on `ExecuteAsync()` —
  the exact failure typed scenarios exist to remove.
- **Browser settings moved into `testsettings.json`**, under a `playwright` section next to
  `http` and `kafka`. `playwrightsettings.json` is still read, and says once where its
  contents should move to; it will be removed in a future major.
- **`net7.0` dropped** — out of support since May 2024. Targets are now `net8.0`, `net9.0`
  and `net10.0`. This also retires the Confluent.Kafka 2.3.0 pin that only that target used.
- **An asynchronous API.** `ExecuteAsync()` runs the chain and takes a `CancellationToken`,
  threaded through `ITestStep`. `Execute()` stays as a thin blocking wrapper, so an existing
  suite keeps working unchanged.
  *Breaking:* a chain now **describes** and runs on execution — `And()`, `On()` and
  `Validate(...)` no longer run anything where they are written. Checks are lambdas and are
  unaffected; a value interpolated into a string from an earlier step needs the
  `Func<string>` overload, because that string is built while the chain is being written.
  `ITestStep.ExecuteAsync` takes a `CancellationToken`, which custom steps must accept.
- **Kafka consume returns the thread between attempts** instead of holding it for the whole
  timeout. `IConsumer` has no asynchronous consume, so this is not "never blocks" — it is a
  non-waiting read every 50ms with the thread back in the pool in between, which also notices
  a message sooner than the 250ms wait it replaces.
- **Browser-state verbs** — `ClearCookies`, `SetLocalStorage`, `ClearLocalStorage`, and the
  assertions `AssertCookie`, `AssertNoCookie`, `AssertCookieIsHttpOnly`, `AssertLocalStorage`,
  `AssertNoLocalStorage`. The last ones read the browser context rather than
  `document.cookie`, which is the only way to assert about a cookie the page cannot see.
- **A request that gets no answer now says so.** A timeout, a cancellation, a refused
  connection or a name that does not resolve were reported as an HTTP error response with
  status `0`, an empty error list and the step marked `Succeeded`. They are now failures,
  with the reason in `Errors`. The rule is that no status code means no response arrived.
- **`CancellationToken` reaches the HTTP call**, instead of stopping at the step's signature,
  and `TimeoutSeconds` is applied on the custom-`HttpClient` path, where it used to be stored
  and ignored while the client's own 100-second default did the waiting.
- **Observed requests** — `AssertRequested(urlPattern)` and `AssertRequestedOnce(urlPattern)`
  assert about what the page asked for while the step ran, with `*` standing for any run of
  characters. The second one is the point: a renewal that fires twice still leaves the user
  signed in, so nothing on screen gives it away, and on a server that rotates a token on use
  it is the second request that ends the session.

## v5.1.0 (cross-boundary scenarios from one call, fully compatible with 5.0.x)

Additive release; upgrading requires no code changes.

- **`Given(api, kafka, browser)`** — any fixture or per-test object that implements
  the new `ITestContextSeeder` can be passed to `Given(...)`, alone or combined, so a
  scenario spanning HTTP, Kafka and the browser is configured in one place.
  `KafkaClassFixture`, `PlaywrightTestBase` and the new
  `PlaywrightTestFixture.OpenPageAsync()` session implement it; every
  `IHttpClientProvider` is one automatically.
- **Kafka steps read `testsettings.json` without a fixture.** `KafkaSettings.Load()`
  used to return defaults regardless of configuration, which also meant the
  parameterless `WithSaslPlain()`, `WithSaslScram()` and `WithSsl()` always threw.
- **Playwright steps stop serialising the page on every step.** `PageContent` is
  fetched on first read; a step on a 500 KB page went from ~105 ms to ~6 ms.
- **Batch consume keeps its diagnostics on failure**, as the single consume already did.
- Every public member ships XML documentation; the packages build warning-free.
- Configuration guidance for Kafka in Docker/Podman on Windows (`127.0.0.1`, not `localhost`).

## v5.0.2 (Kafka performance, fully compatible with 5.0.1)

No API changed; upgrading requires no code changes.

- **Kafka — consume steps are ~14× faster.** A consume step no longer joins a
  consumer group. Joining cost a coordinator lookup, a rebalance and, on a
  default broker, a three-second `group.initial.rebalance.delay.ms` wait — paid
  on every step, for nothing, since steps never commit offsets. Partitions are
  now assigned directly, starting from the same offsets a subscription would
  use (a committed offset for the group wins; otherwise `AutoOffsetReset`).
  Measured against a local broker, one message per fresh topic and group:
  median **3 220 ms → 236 ms** per consume.
- **Kafka — `Produce` without a key works again.** A keyless message is valid
  Kafka (the broker picks the partition) and is what the quick start does, but
  since 5.0.0 the step rejected it before reaching the broker. Producing with a
  `null` key now goes through; the value is still required.

## v5.0.1 (Reliability fixes, fully compatible with 5.0.0)

No API changed; upgrading requires no code changes.

- **HTTP — configuration was silently dropped when a step was reconfigured.**
  `WithTimeout()` discarded the authentication and the custom `HttpClient`, so a
  call chain that set a timeout lost its credentials and simply received 401.
  Every fluent method now copies the step through a single constructor instead
  of rebuilding it field by field.
- **Kafka — verbose broker tracing is no longer forced on.** `Consume` enabled
  librdkafka's `Debug` output on its own and then reported the resulting broker
  chatter as errors. Tracing is opt-in again, and broker logs moved to the
  `BrokerLogs` diagnostic property.
- **HTTP — response headers are no longer split on commas**, which corrupted
  `Date`, `Set-Cookie` and any header whose value legitimately contains one.
- **Results — value conversion now understands** `Guid`, `DateTime`,
  `DateTimeOffset`, `TimeSpan`, enums and `Nullable<T>`, which previously
  returned `default` without explanation.
- **Failures carry diagnostics.** HTTP and Playwright failures keep the
  exception type and stack trace, and a failing Playwright step captures a
  screenshot when `ScreenshotOnFailure` is enabled.
- Removed debug output that printed on every `WebApplicationFactory` test.
- Packages now ship XML documentation, symbol packages (`.snupkg`) and
  SourceLink, so you can step into the framework while debugging.

## v5.0.0 (Core, Http, Kafka, Playwright, MCP)
- Added .NET 10 support across all packages
- Multi-target support: `net7.0`, `net8.0`, `net9.0`, `net10.0`
- Unified version across all packages (Core, Http, Kafka, Playwright)
- **XUnitAssured.Playwright** — New package for browser-based UI testing with fluent DSL
  - Multiple locator strategies: CSS, ARIA roles, labels, test IDs, placeholders, text, title
  - All interaction types: click, fill, check, hover, focus, select, press, type, drag, scroll
  - Screenshot capture, tracing, and Playwright Inspector integration (`RecordAndPause()`)
  - Codegen translator: converts Playwright Inspector output to XUnitAssured DSL
- **XUnitAssured.Mcp** — New MCP server for AI-assisted test generation
  - 10 tools: Playwright translation (3), HTTP scaffolding (3), Kafka scaffolding (4)
  - Integrates with GitHub Copilot Chat, VS Code, Claude Desktop, and any MCP client
  - stdio transport for zero-config local usage

## v4.2.0 (Core)
- Consolidated DI support from `XUnitAssured.DependencyInjection` into `XUnitAssured.Core` (`DITestFixture`)

## v4.0.0 (Core + Http)
- Added `ValidationBuilder` and BDD extensions (consolidated from `XUnitAssured.Extensions`)
- Added `HttpValidationBuilder` and BDD extensions for HTTP
- Multi-target support: `net7.0`, `net8.0`, `net9.0`

## v3.0.0 (Kafka)
- Aligned with framework architecture refactoring
- Full fluent DSL integration for Kafka produce/consume
- Batch operations (`ProduceBatch`, `ConsumeBatch`)
- Comprehensive authentication support (SASL, SSL, mTLS)
- Schema Registry support with Avro serialization
