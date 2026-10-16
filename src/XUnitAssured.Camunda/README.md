# XUnitAssured.Camunda

Camunda 8 process testing for XUnitAssured, on the official `Camunda.Orchestration.Sdk`.

Start a real process instance and answer the diagram's external dependencies (the job workers,
connectors included) from the test. Each test touches only its own instance, so it runs on a
cluster other tests share, in parallel. Two ways to use it:

- **The developer tests the BPMN of the repository.** The chain deploys it, then starts the
  version it deployed. See [A process, end to end](#a-process-end-to-end).
- **QA tests what is already deployed**, on a staging cluster, end to end with the real workers
  or mocking only what has no worker there. See
  [Testing what is already deployed](#testing-what-is-already-deployed).

> **Before anything else: do not mock a job type that has a real worker on the cluster.** Camunda
> hands the job to whoever asks first, and a real worker asks before the test sees the job. See
> [What it cannot protect you from](#what-it-cannot-protect-you-from).

## Installation

```bash
dotnet add package XUnitAssured.Camunda
```

Camunda 8.10. The SDK's major version follows the server's minor (10.x is 8.10), and the package
pins it.

## A process, end to end

```csharp
using XUnitAssured.Camunda.Extensions;

await Given()
    .Deploy("order", "shipping")
    .MockJobWorker("charge-card").ThenComplete(new { approved = true })
    .MockJobWorker("dispatch").ThenComplete(new { trackingCode = "BR-1" })
    .StartInstance("order", new { orderId = 42 })
    .ExecuteAsync()
    .AssertInstanceCompleted()
    .AssertCompletedElements("charge", "ship")
    .AssertVariable("trackingCode", "BR-1")
    .AssertJobMocked("dispatch")
    .AssertNoIncidents();
```

- **`Deploy`** sends the files of the repository in one deployment. Name each by its process id
  (`"order"`) or by its path (`"bpmn/order.bpmn"`). With the id, the file is the `.bpmn` in the
  test output folder that declares `<bpmn:process id="order">`, whatever the file is called; an
  id declared in two files is refused, naming both. Copy the files to the output:

  ```xml
  <None Include="bpmn\*.bpmn" CopyToOutputDirectory="PreserveNewest" />
  ```

  Put the process and the ones it calls together: a call activity with
  `bindingType="deployment"` calls the child deployed with it, and Camunda refuses a parent
  deployed without its child. Deploying identical content again creates no new version, so
  deploying in every test costs one round trip.
- **`StartInstance`**, in a chain that deploys, starts the instance from the definition this
  chain deployed, not from the latest version of the process id: on a shared cluster, the latest
  version may be the one someone else just deployed. Starting a process the chain did not deploy
  fails the step.
- **`MockJobWorker(type)`** answers jobs of that type with `ThenComplete(variables)` or
  `ThenThrowBpmnError(code, message, variables)`. It covers the instance and every instance it
  calls. A mock that does not say what it answers does not compile.

The chain is awaited once, and the assertions chain right after `ExecuteAsync()`. Await it:
without `await` the steps still run, but a failed assertion is never seen.

## The error path

```csharp
await Given()
    .Deploy("bpmn/order.bpmn", "bpmn/shipping.bpmn")
    .MockJobWorker("charge-card").ThenThrowBpmnError("PAYMENT_REFUSED", "insufficient funds")
    .MockJobWorker("dispatch").ThenComplete()
    .StartInstance("order")
    .ExecuteAsync()
    .AssertInstanceCompleted()
    .AssertCompletedElements("payment-refused", "order-refused")
    .AssertJobMocked("dispatch", 0);
```

A boundary error event with that code catches the error. Without one, the instance opens an
incident, and `AssertInstanceCompleted` fails at once saying so.

## Testing what is already deployed

A chain without `Deploy` starts the version already on the cluster: what was deployed to
staging, tested as it is there.

```csharp
// The latest version on the cluster
await Given()
    .StartInstance("order", new { orderId = 42 })
    .ExecuteAsync()
    .AssertInstanceCompleted();

// A version, by the number the cluster gave it or by its version tag
.StartInstance("order", new { orderId = 42 }, version: 3)
.StartInstance("order", new { orderId = 42 }, versionTag: "1.4.0")
```

- **`versionTag`** is the `zeebe:versionTag` the team sets in the Modeler. Prefer it to
  `version`: the number is the cluster's own count, so version 3 on staging is not version 3 in
  production. When several versions carry the tag, the newest of them runs.
- **The result says which version ran:** `Version`, `VersionTag` and `DefinitionKey` on
  `GetResult()`, and a failed assertion names it, `(order version 7, tag '1.4.0')`. "The latest"
  changes when someone deploys.
- **A version the cluster does not have** fails the step, listing the versions it has, newest
  first.
- **`version` or `versionTag` in a chain that also deploys** starts that version from the cluster,
  not the deployed one: the test asked for it by name.

On staging the real workers are running, and the package does not stop them. Two ways to test
there:

1. **End to end, with no mock.** The real workers move the instance, and the assertions wait for
   them, up to `assertionTimeoutSeconds`.
2. **Mock only the job types that have no worker there**, an external system staging does not
   have, for example. The mock answers those, and the real workers the rest:

   ```csharp
   await Given()
       .MockJobWorker("credit-bureau").ThenComplete(new { score = 700 })
       .StartInstance("order", new { orderId = 42 }, versionTag: "1.4.0")
       .ExecuteAsync()
       .AssertInstanceCompleted()
       .AssertJobMocked("credit-bureau");
   ```

Mocking a type that has a real worker there makes the two race for the job: see
[What it cannot protect you from](#what-it-cannot-protect-you-from). And an instance still running
after `mockLifetimeSeconds` (120 s) is cancelled, with or without mocks: raise it for a flow that
takes longer on staging.

## Assertions wait, and give up early

Camunda's searches read the secondary storage, which trails the engine: an instance completes in
the engine and shows up completed in the search a moment later. Every assertion asks again every
`pollIntervalMilliseconds` until `assertionTimeoutSeconds` (100 ms and 15 s by default).

| Assertion | Passes when |
|---|---|
| `AssertInstanceCompleted()` | the instance reached an end event |
| `AssertInstanceActive()` | the instance is still running |
| `AssertCompletedElements(ids)` | each element id of the BPMN completed in the instance |
| `AssertActiveElements(ids)` | the instance is waiting at each element |
| `AssertVariable(name, value)` | the variable equals the value, compared as JSON |
| `AssertVariable<T>(name, assertion)` | the assertion passes on the variable read as `T` |
| `AssertJobMocked(type, times)` | the mock of that type resolved exactly `times` jobs |
| `AssertNoIncidents()` | no open incident now (does not wait) |

An assertion does not wait the full timeout when waiting cannot help: the instance was
terminated, it has an open incident, a completed element can no longer happen, or a mock found
its job held by another worker. When an assertion fails, the message says what the instance is
waiting at, which jobs have no mock, and where to open it in Operate:

```
Expected process instance 2251799813685899 (order version 4) to complete within 15s, but it is ACTIVE.
  Waiting at: 'ship' (SERVICETASK)
  Jobs waiting for a worker (not mocked in this test): 'dispatch' at 'ship' (instance 2251799813685905)
  Operate: http://localhost:8080/operate/processes/2251799813685899
```

## Why the mocks are not job workers

Camunda's job activation does not filter by process instance. A job worker for `charge-card`
would take the `charge-card` job of every test running on the cluster, and two tests mocking
the same type with different answers would answer each other's jobs.

So a mock is not a worker. For each instance the chain starts, a loop searches the jobs of
*that instance* (and of the instances it called), and resolves each one by its job key without
activating it. The engine accepts completing, or throwing a BPMN error on, a job no worker
activated; the Camunda Process Test does the same in its `completeJob`. Five instances in
parallel, mocking the same type with five different answers, each got its own.

The loop stops when the instance ends. An instance still running after `mockLifetimeSeconds`
(120 s by default) is cancelled, so a failed test does not leave an active instance behind on a
shared cluster. Its history stays, so you can still open it in Operate.

## What it cannot protect you from

A real worker for a mocked job type, running on the same cluster, activates the job before the
mock finds it. The job search does not show activations, so the package cannot see this
directly:

- **The worker activated the job with a lease** (`WithLease`, new in the 10.x SDK): completing it
  without the lease token is refused with `INVALID_STATE`, and the assertion fails at once naming
  the job and the cause.
- **The worker activated it without a lease:** the mock completes the job over it and the test
  passes, while the real worker may already have done its work (charged the card). The real
  worker then fails to complete it.

The same goes for the Connectors runtime: it is a worker for the `io.camunda:*` job types. Run the
tests on a cluster without Connectors, or do not mock connector job types there.

On staging this is the rule, not the exception: mock only the job types that have no worker
there, or none at all.

## Configuration

The `camunda` section of `testsettings.json`, beside `http`, `kafka` and `rabbitmq`:

```json
{
  "camunda": {
    "restAddress": "http://localhost:8080",
    "authentication": {
      "type": "Basic",
      "username": "tests",
      "password": "${ENV:CAMUNDA_PASSWORD}"
    },
    "assertionTimeoutSeconds": 15,
    "pollIntervalMilliseconds": 100,
    "mockLifetimeSeconds": 120
  }
}
```

`restAddress` has no `/v2`. `authentication.type` is `None`, `Basic` or `OAuth` (with `clientId`,
`clientSecret`, `oauthUrl` and, when the provider asks, `audience`). What the section leaves out
comes from the `CAMUNDA_*` variables the SDK reads (`CAMUNDA_REST_ADDRESS`,
`CAMUNDA_BASIC_AUTH_USERNAME`, `CAMUNDA_BASIC_AUTH_PASSWORD`, ...), so a CI job can configure it
with environment variables alone.

With no `type`, and no `CAMUNDA_AUTH_STRATEGY`, a username means `Basic` and a client id means
`OAuth`. The SDK on its own does not infer `Basic`: without the strategy it sends no credentials,
and the cluster answers 401.

## Running Camunda locally

```bash
podman run -d --name camunda -p 8080:8080 \
    -e CAMUNDA_DATA_SECONDARYSTORAGE_TYPE=rdbms \
    -e CAMUNDA_DATA_SECONDARYSTORAGE_RDBMS_URL="jdbc:h2:mem:tests;DB_CLOSE_DELAY=-1;MODE=PostgreSQL" \
    -e CAMUNDA_DATA_SECONDARYSTORAGE_RDBMS_USERNAME=sa \
    -e CAMUNDA_DATA_SECONDARYSTORAGE_RDBMS_FLUSHINTERVAL=PT0S \
    -e CAMUNDA_SECURITY_AUTHENTICATION_UNPROTECTEDAPI=false \
    -e CAMUNDA_SECURITY_INITIALIZATION_USERS_0_USERNAME=tests \
    -e CAMUNDA_SECURITY_INITIALIZATION_USERS_0_PASSWORD=tests-password \
    -e CAMUNDA_SECURITY_INITIALIZATION_USERS_0_NAME=tests \
    -e CAMUNDA_SECURITY_INITIALIZATION_USERS_0_EMAIL=tests@localhost \
    docker.io/camunda/camunda:8.10.4
```

Ready in about 35 s. The H2 in memory and `FLUSHINTERVAL=PT0S` are what the Camunda Process Test
uses: the search sees a change as soon as the engine makes it. Measured against the 0.5 s of the
official Docker Compose, it cut each scenario by 20 to 60%. Without a license
key, Camunda runs with a "Non-Production License" banner, which its license allows for testing.

## Not yet

Timers, user tasks, messages and signals, mocking a child process or a DMN decision, and a
Testcontainers mode that starts Camunda inside `dotnet test` are planned, in that order. Until a
timer verb exists, a test that reaches a timer waits for it.

## Where the verbs live

`Deploy`, `MockJobWorker` and `StartInstance` are the entry verbs, the only ones on
`ITestScenario`; the rest are members of `ICamundaScenario`, the convention the root README
documents. They do not collide with `ApiResource`, `Topic` or `Queue`, so a chain can call the
API, then follow the process it started.

## Supported Frameworks

- .NET 8
- .NET 9
- .NET 10

## Links

- [Repository](https://github.com/andrewBezerra/XUnitAssured.Net)
- [Upgrade guide](https://github.com/andrewBezerra/xunit-assured-net/blob/main/UPGRADING.md)

## License

Apache-2.0
