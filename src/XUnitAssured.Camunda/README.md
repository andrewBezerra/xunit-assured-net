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

## Incidents

A worker that gives up is the technical path, and Camunda opens an incident for it. Test it with
`ThenFail`, and assert the incident:

```csharp
await Given()
    .Deploy("order", "shipping")
    .MockJobWorker("charge-card").ThenFail("payment gateway is down")
    .StartInstance("order")
    .ExecuteAsync()
    .AssertIncident("charge", "JOB_NO_RETRIES", "gateway is down")
    .AssertInstanceActive();
```

- **`ThenFail(errorMessage)`** fails the job with no retries left. The incident shows the message
  in Operate. For the business path, the one a boundary error event catches, use
  `ThenThrowBpmnError`.
- **`AssertIncident(elementId, errorType, messageContains)`** waits for an open incident at that
  element, in the instance or in one it calls. The type and the message are optional. The type is
  the one Operate shows: `JOB_NO_RETRIES`, `UNHANDLED_ERROR_EVENT`, `EXTRACT_VALUE_ERROR`... It
  gives up at once if the instance finishes without the incident.
- **A BPMN error no boundary event catches is an incident too,** of type `UNHANDLED_ERROR_EVENT`:
  `ThenThrowBpmnError` with a code the diagram does not handle tests that path.

## User tasks and timers

A person and a clock are dependencies too: a test cannot wait for someone to approve, or for three
days to pass. Answer them the way you answer a job worker, once in the chain, every time the
instance gets there:

```csharp
await Given()
    .Deploy("approval")
    .SkipTimer("wait-for-analysis")
    .MockUserTask("approve").ThenComplete(new { approved = true })
    .SkipTimer("no-confirmation")
    .StartInstance("approval", new { orderId = "P-1" })
    .ExecuteAsync()
    .AssertInstanceCompleted()
    .AssertCompletedElements("approve", "customer-gave-up")
    .AssertUserTaskMocked("approve")
    .AssertTimerSkipped("no-confirmation");
```

- **`MockUserTask(elementId).ThenComplete(variables)`** completes the user task with what the form
  would return, in the instance and in the ones it calls. It is by element id, not by type: two
  user tasks of a diagram ask for different answers. Camunda user tasks only (`zeebe:userTask`);
  the old job-based ones are jobs of type `io.camunda.zeebe:userTask`, mocked with `MockJobWorker`.
- **`SkipTimer(elementId)`** does not move the clock, which belongs to the whole cluster. It modifies
  the instance: it ends the wait and activates what comes after the timer. It works for:
  - an intermediate timer event;
  - a timer after an event-based gateway: it ends the gateway, which drops the other events;
  - a boundary timer. Pass the boundary event's id. An interrupting one ends the activity it is
    attached to and takes the timer's path; a non-interrupting one leaves the activity running.
- **A skipped timer is `TERMINATED`, not `COMPLETED`,** in the instance and in Operate. Assert it
  with `AssertTimerSkipped`, not `AssertCompletedElements`.
- **A timer that flows into a joining gateway is refused,** and the first assertion fails at once
  saying so. Activated by instance modification, a parallel or inclusive join fires without waiting
  for its other branches (measured on 8.10.4): the test would run a path the process never takes.
  For that timer, put the duration in a variable (`timeDuration="=reviewTimeout"`) and start the
  instance with `PT1S`.

The package reads the structure of the process from the deployed BPMN, so `SkipTimer` also works on
what QA tests on staging, deployed by someone else.

When an instance stops, the failure now says what it is waiting for and what the test declared
but never used:

```
Expected process instance 2251799813687606 (approval version 1) to complete within 15s, but it is ACTIVE.
  Waiting at: 'approve' (USER_TASK)
  Waiting for (not answered in this test): user task 'approve' (instance 2251799813687606)
  Declared in this test but never used: timer 'no-confirmation'
```

## Messages and signals

What another system would send the process is a dependency too. Mock it by its name, once in the
chain:

```csharp
await Given()
    .Deploy("delivery")
    .MockMessage("payment-confirmed").ThenCorrelate(new { amount = 99 })
    .MockSignal("stock-released").ThenTrigger(new { batch = "L-1" })
    .MockUserTask("check").ThenComplete()
    .StartInstance("delivery", new { orderId = $"P-{Guid.NewGuid():N}" })
    .ExecuteAsync()
    .AssertInstanceCompleted()
    .AssertMessageCorrelated("payment-confirmed")
    .AssertSignalTriggered("stock-released");
```

- **`MockMessage(name).ThenCorrelate(variables)`** correlates the message for real, with the
  correlation key the subscription carries, so the event's variable mappings run. It covers:
  - intermediate message events;
  - receive tasks;
  - message events after an event-based gateway;
  - message boundary events.
- **Use a correlation key unique to the test,** such as a Guid in the start variables. Camunda
  delivers a message to whoever subscribed first, in any instance of the cluster. Before
  correlating, the package checks that no instance outside the test awaits the same message with
  the same key. If one does, it does not correlate, and the first assertion fails saying so.
- **`MockSignal(name).ThenTrigger(variables)` is not a broadcast.** A broadcast reaches every
  instance on the cluster that waits for the signal, real ones on staging included. The package
  triggers it only in the test's instances, by instance modification, with the same rules as
  `SkipTimer`:
  - it covers intermediate signal events, signal events after an event-based gateway, and signal
    boundary events;
  - a signal event that flows into a joining gateway is refused;
  - the triggered event is `TERMINATED`, so assert it with `AssertSignalTriggered`.

## Child processes

A call activity can run its real child, or the test can answer for it. The child is often
another team's process, with its own workers. Mock it by the call activity's id in the parent:

```csharp
await Given()
    .Deploy("order", "shipping")
    .MockJobWorker("charge-card").ThenComplete()
    .MockChildProcess("call-shipping").ThenComplete(new { trackingCode = "BR-9" })
    .StartInstance("order")
    .ExecuteAsync()
    .AssertInstanceCompleted()
    .AssertVariable("trackingCode", "BR-9")   // the call activity's output mapping ran
    .AssertChildProcessMocked("call-shipping");
```

- **The id is the call activity's,** not the child process's. It names which call is mocked, even
  when the parent calls the same child in two places.
- **The child starts for real and is intercepted as soon as it shows up.** The package ends what it
  is doing and finishes it at its end event, with the test's variables. The child ends as it would
  have ended, so the call activity's output mapping runs (measured on 8.10.4). The child must be
  deployed, because the call activity starts it.
- **`ThenThrowBpmnError(code, variables)`** finishes the child at its error end event with that
  code. If the child has none, the package ends the call activity and takes the boundary error
  event that catches the code. With neither, the test fails, because the error has no path to
  take.
- **A child with nothing to wait for ends before it can be intercepted:** no job, timer, message
  or user task. The test fails saying it ran for real.

**`bindingType` warning.** A call activity with no `bindingType`, or `bindingType="latest"`,
starts the latest version of the child deployed on the cluster. On a shared cluster, that may be
someone else's version. `Deploy` reports it in `GetResult().Warnings`, and a failed assertion repeats
it. Use `bindingType="deployment"` and deploy the child in the same `Deploy(...)`.

## Following the instance your API started

When the system under test starts the process, as an API does when it receives an order, the
test does not call `StartInstance`. It calls the API, then follows the instance the API started:

```csharp
// The API starts the process and returns the instance key
var key = await Given()
    .ApiResource("/orders").Post(new { orderId = 42 })
    .ExtractAsync<string>("$.processInstanceKey");

await Given()
    .MockJobWorker("charge-card").ThenComplete()
    .FollowInstance(key)
    .ExecuteAsync()
    .AssertInstanceCompleted();

// Or, when the API does not return it, by a variable the test sent
await Given()
    .MockJobWorker("charge-card").ThenComplete()
    .FollowInstance("order", ("orderId", 42))
    .ExecuteAsync()
    .AssertInstanceCompleted();
```

- **The mocks and the assertions work on the followed instance** as on one the chain started, the
  instances it calls included.
- **The instance may show up a moment after the API answered.** `FollowInstance` waits for it, up
  to `assertionTimeoutSeconds`, and fails naming what it waited for.
- **By variable, the value is compared as JSON.** If more than one instance has it, the newest is
  followed and `GetResult().Warnings` says so. Send a value unique to the test.
- **A followed instance still active when the test run ends is cancelled,** as one the chain
  started would be.

The other direction works too: call the API, extract what it returned, and start the process
with it. The verbs of the HTTP and Camunda packages do not collide, so both live in the same test.

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
when the test run ends, or after `mockLifetimeSeconds` (120 s), is cancelled, with or without
mocks: on staging, wait for the end of the flow in the test itself (`AssertInstanceCompleted` with
a longer `assertionTimeoutSeconds`).

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
| `AssertUserTaskMocked(id, times)` | the mock of that user task completed it exactly `times` times |
| `AssertTimerSkipped(id, times)` | that timer was skipped exactly `times` times |
| `AssertMessageCorrelated(name, times)` | the mock correlated that message exactly `times` times |
| `AssertSignalTriggered(name, times)` | that signal was triggered exactly `times` times |
| `AssertChildProcessMocked(id, times)` | the child process of that call activity was mocked exactly `times` times |
| `AssertNoIncidents()` | no open incident now (does not wait) |
| `AssertIncident(id, type, message)` | an incident opened at that element; type and message are optional |

An assertion does not wait the full timeout when waiting cannot help: the instance was
terminated, it has an open incident, a completed element can no longer happen, or a mock found
its job held by another worker. When an assertion fails, the message says what the instance is
waiting at, which jobs have no mock, and where to open it in Operate:

```
Expected process instance 2251799813685899 (order version 4) to complete within 15s, but it is ACTIVE.
  Waiting at: 'ship' (SERVICE_TASK)
  Jobs waiting for a worker (not mocked in this test): 'dispatch' at 'ship' (instance 2251799813685905)
  Operate: http://localhost:8080/operate/processes/2251799813685899
```

## Diagram coverage

Every test run writes a coverage report: what the test instances went through in each diagram,
and what never ran. It lands in `camunda-coverage/` in the test output folder:

- **`index.html`** draws each diagram with [bpmn-js](https://bpmn.io), the reached elements in
  green and the missed ones in red, with the percentage per process and the list of what was
  missed. The page loads bpmn-js from a CDN, pinned with an integrity hash; offline, the list
  still shows, without the drawing.
- **`coverage.json`** has the same data for a pipeline to read: per process version, the flow
  nodes, the reached ones, the missed ones, and the coverage.

What it counts:

- **The flow nodes** (events, activities, gateways) of the deployed version each instance ran.
  Each version of a process is its own entry. Sequence flows do not count: Camunda's search shows
  elements, not flows.
- **An element counts once any test instance went through it,** completed or terminated. The
  instances a test's instance calls count too, under their own process.
- **What the package triggered counts.** A skipped timer, a signal triggered in the instance or
  a mocked child's error taking the call activity's boundary never gets an element of its own in
  Camunda, so the package records it itself.

The report is rewritten as each instance finishes, so it does not depend on the end of the test
run. Turn it off, or move it:

```json
{
  "camunda": {
    "coverage": { "enabled": false, "directory": "camunda-coverage" }
  }
}
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

The loop stops when the instance ends. An instance still running when the test process exits, or
after `mockLifetimeSeconds` (120 s by default), whichever comes first, is cancelled, so a failed
test does not leave an active instance behind on a shared cluster. Its history stays, so you can
still open it in Operate, terminated where it was waiting. A process killed outright (a debugger
stopped, an aborted runner) warns no one, and then the instance stays.

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
    "coverage": { "enabled": true, "directory": "camunda-coverage" }
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

## Camunda inside dotnet test

[XUnitAssured.Camunda.Testcontainers](../XUnitAssured.Camunda.Testcontainers/README.md) starts the same
Camunda in Docker for the test run, and hands its address and credentials to every chain:

```csharp
public class OrderTests(CamundaContainerFixture camunda)
    : CamundaTestBase<CamundaContainerFixture>(camunda), IClassFixture<CamundaContainerFixture>
{
    [Fact]
    public async Task Order_Completes() =>
        await Given().Deploy("order").StartInstance("order").ExecuteAsync().AssertInstanceCompleted();
}
```

`CamundaTestBase<TFixture>` and `CamundaClusterFixture` live in this package: the plain
`CamundaClusterFixture` reads `testsettings.json`, so the same test class can point at a remote
cluster instead.

## Where the verbs live

`Deploy`, `MockJobWorker`, `MockUserTask`, `SkipTimer`, `MockMessage`, `MockSignal`,
`MockChildProcess`, `StartInstance` and `FollowInstance` are the entry verbs, the only ones on
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
