# Changelog

All notable changes to the XUnitAssured packages. Upgrading from 5.1.x? See [UPGRADING.md](UPGRADING.md).

## v6.32.0 (Current — secrets from user secrets)

New feature: secrets from .NET user secrets in the settings files. Upgrading from 6.31.5 requires no code changes; `${ENV:...}` is unchanged.

- **`${SECRET:Key}` in `testsettings.json` and `httpsettings.json`** reads the test project's user secrets (`secrets.json`, from `dotnet user-secrets`), then the environment variable with the same key and `:` written as `__`. One placeholder works on a developer machine and in CI, and nothing secret goes into the repository (#212).
- **The value is escaped for JSON**, so a password with a quote or a backslash keeps the file valid.
- **Which `secrets.json`**: the `UserSecretsId` of the test project. When the run loads more than one assembly with one (a `WebApplicationFactory` loads the API project), a `"userSecretsId"` at the root of the file chooses; without it, loading fails and names the ids found.
- `XUnitAssured.Core` now depends on `Microsoft.Extensions.Configuration.UserSecrets`.

## v6.31.5 (static readonly arrays)

Maintenance release: constant array arguments become `static readonly` fields (CA1861). No behavior changes; upgrading from 6.31.4 requires no code changes.

- **XUnitAssured.Camunda**: telling a BPMN path from a process id no longer allocates the folder separators on every call.
- **CA1861 is now a build warning in the packages.** It is off in the tests, the samples and the sample API, where the code runs once and the expected value reads best next to its assertion.

## v6.31.4 (no skipped tests in the suite)

Test-only release: no skipped tests left in the framework's test suite. No package changes; upgrading from 6.31.3 requires no code changes.

- **The README, UPGRADING and certificate examples are compiled, not run.** They were skipped tests (xUnit1004); they now live in `…Examples` classes with no test attribute, so they still break the build if the API changes under them, and no longer show up as skipped.
- **`HTTP POST should work with fluent DSL syntax` runs**, with the `Requires=Network` trait, in the network job.
- **The sample projects keep their `Skip`**: it is how a sample is switched on against your own environment. xUnit1004 is turned off for them in `.editorconfig`, with the reason next to it.

## v6.31.3 (community files)

Docs-only release: the repository's community files. No package changes; upgrading from 6.31.2 requires no code changes.

- **`CONTRIBUTING.md`**: building, the tests that need a broker, a cluster or a browser and how to run them, and the version, release notes and changelog entry every pull request adds.
- **`CODE_OF_CONDUCT.md`**: the Contributor Covenant 2.1.
- **`SECURITY.md`**: vulnerabilities are reported privately through GitHub, and fixes go into the latest release.
- **Issue forms** for bugs and feature requests, and a **pull request template** with the release checklist.

## v6.31.2 (ThrowIfNull argument guards)

Maintenance release: argument guards use `ArgumentNullException.ThrowIfNull` (CA1510). No behavior changes; upgrading from 6.31.1 requires no code changes.

- **Null-argument guards in Core, Http, Kafka, RabbitMq, Playwright and Camunda use `ArgumentNullException.ThrowIfNull`** instead of `if (x == null) throw new ArgumentNullException(nameof(x))`. The exception type, its `ParamName` and its message stay the same.
- **CA1510 is now a build warning**, and with `TreatWarningsAsErrors` an explicit null-argument throw fails the build.

## v6.31.1 (downloads badge for every package)

Docs-only release: the README's downloads badge counts every package. No package changes; upgrading from 6.31.0 requires no code changes.

- **The downloads badge sums the downloads of all the XUnitAssured packages**, the total the NuGet profile shows. Before, it counted only `XUnitAssured.Core`, read from a NuGet search replica that can lag behind by thousands of downloads. A daily workflow sums them and publishes the badge on the `badges` branch. Every package depends on Core, so installing one package counts two downloads.

## v6.31.0 (bugs found by unit tests: Kafka, Camunda, Http)

Bug fixes found by the new unit tests, in Kafka, Camunda and Http. Upgrading from 6.30.x requires no code changes. Some tests that passed by accident may now behave differently: see each item.

**XUnitAssured.Kafka**
- **`WithKey`, `WithHeader`, `WithPartition`, `WithTimestamp` and every `With…Auth` verb no longer drop the fixture's broker.** They rebuilt the step field by field and turned the default broker into an explicit one. The step then ignored the fixture's broker and shared producer and connected to `localhost:9092`. They now copy the step as it is.
- **The authentication verbs now work on `ProduceBatch` and `ConsumeBatch`** too.
- **`WithHeader` no longer changes the headers of the step before it.**
- **`ProduceBatch` followed by any `With…` verb kept its messages.** Before, the batch went out empty and failed with `IndexOutOfRangeException`. An empty batch is now refused when it is described.
- **A keyless batch sends null keys**, like a single produce, so the broker spreads it across partitions. Before, every message went to the same partition.
- **A batch uses the fixture's shared producer only when a single produce would**: not with an explicit broker, credential or producer configuration.
- **`ValidateMessage<T>` and `AssertMessage<T>` deserialize a JSON message into a class.** Before, `ValidateMessage<T>` always failed and `AssertMessage<T>` received null.
- **A failed produce keeps the diagnostics gathered before the failure**, such as the broker it used.

**XUnitAssured.Camunda**
- **Element types and incident error types use Camunda's names** (`JOB_NO_RETRIES`, `SERVICE_TASK`), as Operate shows them. Before, messages showed the SDK's enum names (`JOBNORETRIES`). `AssertIncident(errorType: ...)` accepts either form, as before.

**XUnitAssured.Http**
- **`ValidateContract<T>()` accepts a camelCase response** for a PascalCase model, the ASP.NET Core default. Before, every field failed with `NoAdditionalPropertiesAllowed`. PascalCase responses still pass.

**XUnitAssured.Playwright**
- **The documentation of `ByRole` says what it does:** with a name, the name always matches exactly.

**Tests**
- Unit-test coverage goes from 59% to about 83%. The new tests run with no browser, broker or cluster, using NSubstitute fakes and an HTTP handler under the real Camunda SDK. Two internal seams make that possible, and neither changes behavior: the HTTP handler of the Camunda gateway, and the connection factory of the RabbitMQ steps.

## v6.30.6 (coverage measured right)

Build-only release: unit test coverage is measured right. No package changes; upgrading from 6.30.5 requires no code changes.

- **XUnitAssured.Core is measured again.** coverlet.collector 6.0.2 could not instrument it on net10: it did not resolve a DLL that comes from the ASP.NET Core shared framework, and left the whole package out of the report. With 10.1.0, Core shows 87.6% and the total goes from 55% to about 59%.
- **Three files no unit test can run are out of the coverage count**, each with its reason in `sast.yml`: `CamundaContainerFixture` (Docker), `BrowserAppFixture` (a real browser and real ports) and the source generator (it runs inside the compiler).
- **The plan to reach 85%** is in `docs/cobertura-plano.md`.

## v6.30.5 (SonarCloud badges)

Docs-only release: SonarCloud badges in the README. No package changes; upgrading from 6.30.4 requires no code changes.

- **The README shows the SonarCloud quality gate and the coverage**, next to the CI badge. Each one links to the project in SonarCloud.

## v6.30.4 (the Kafka SSL sample no longer ships its CA key)

Samples-only release: the Kafka SSL sample no longer ships its CA's private key. No package changes; upgrading from 6.30.3 requires no code changes.

- **The private key of the Kafka SSL sample's CA was committed** (`docker/KRaft-SASLSSL/secrets/ca-key`), found by Semgrep. The sample's `install-ca-cert.ps1` puts that CA in the Windows Root store, so on a machine that ran it, anyone with the key could issue a certificate for any site that the machine would trust. **If you ran `install-ca-cert.ps1`, remove that CA** (`uninstall-ca-cert.ps1`, or by thumbprint `E831AF95C60A9F1FAE28B4BC69DB26AB4C622E1D` from both `LocalMachine\Root` and `CurrentUser\Root`).
- **`secrets/` is no longer in the repository.** Each machine generates its own with `bash scripts/generate_ssl_certs.sh`, and `secrets/` is in `.gitignore`.
- **The generated CA can only sign for `kafka`, `localhost` and `127.0.0.1`** (name constraints, `pathlen:0`). Even installed in the Root store, it is worth nothing for any other site.
- **`generate_ssl_certs.sh` runs in Git Bash on Windows.** The subject was rewritten as a Windows path, and `<(...)` does not work with Windows openssl.

## v6.30.3 (SonarCloud analyzes the packages)

Build-only release: SonarCloud now analyzes the packages. No package changes; upgrading from 6.30.2 requires no code changes.

- **The packages count as production code in SonarCloud.** They reference xunit, and the Sonar scanner took every project that does for a test project. In the first analysis, no line of the packages counted, not even for coverage: only the sample apps did. `SonarQubeTestProject` is now `false` for the shipping libraries.
- **Sources are read as UTF-8.** A file was skipped because the scanner assumed windows-1252.

## v6.30.2 (static analysis: Semgrep and SonarCloud)

Build-only release: static analysis in CI. No package changes; upgrading from 6.30.1 requires no code changes.

- **Semgrep on every pull request and on main.** A pull request fails only on the findings it introduces. On main, the full scan goes to GitHub code scanning (the Security tab).
- **SonarCloud on every pull request and on main**, with the unit tests' coverage. It comments on the pull request and does not block it.
- **Every action in the workflows is pinned by commit SHA**, not by tag. A moved tag can no longer change what runs in CI or in the release.

## v6.30.1 (MCP: the rest of the Camunda tool escaped)

Fix for the Camunda tool of XUnitAssured.Mcp. Upgrading from 6.30.0 requires no code changes; the other packages are unchanged.

- **Three more places of `generate_camunda_test` now escape what the diagram brings.** 6.29.1 escaped the literals and most comments, but left these out:
  - the id of a job-based user task in its comment;
  - the process id and the process ids listed in the "process not found" comment;
  - the XML parser's message.

  A line break in one of them (`&#10;` in an attribute) ended the comment, and the rest became code.
- **The hostile-diagram test now covers both:** a job-based user task with a line break in its id, and a missing process whose name carries a line break. With the old code, the test fails.
- Found by a second automated security review.

## v6.30.0 (Camunda: JSON test cases)

In XUnitAssured.Camunda: JSON test cases in the Camunda Process Test format. Upgrading from 6.29.1 requires no code changes; the other packages are unchanged.

- **A JSON test case runs as it is.** Write it in the format of the Camunda Process Test and of the Web Modeler's Test Studio (`cpt-test-cases`, schema 8.10). `[CamundaTestCases("order.json")]` turns each case of the file into a `[Theory]` row, and `CamundaTestCase.RunAsync("order", "shipping")` deploys and runs it. Inside a `CamundaTestBase`, `RunAsync(Given(), ...)` uses the fixture's cluster.
- **The instructions run in order:**
  - the `MOCK_*` instructions are the package's mocks;
  - `COMPLETE_JOB`, `COMPLETE_USER_TASK`, `CORRELATE_MESSAGE` and the other actions happen once, at their place, waiting for what they need;
  - the assertions wait like the package's.

  A `processDefinitionId` selector also picks the child processes the case's instance called.
- **Supported:**
  - `CREATE_PROCESS_INSTANCE`;
  - the job worker and child process mocks;
  - `COMPLETE_JOB`, `THROW_BPMN_ERROR_FROM_JOB`, `COMPLETE_USER_TASK`, `PUBLISH_MESSAGE`, `CORRELATE_MESSAGE` and `UPDATE_VARIABLES`;
  - `ASSERT_PROCESS_INSTANCE`, `ASSERT_ELEMENT_INSTANCE`, `ASSERT_ELEMENT_INSTANCES` (every state, `IS_COMPLETED_IN_ORDER` included), `ASSERT_VARIABLES`, `ASSERT_USER_TASK` and `ASSERT_PROCESS_INSTANCE_MESSAGE_SUBSCRIPTION`.
- **Refused before the case starts, each with its reason:** what a shared cluster cannot run.
  - `INCREASE_TIME` and `SET_TIME`: the clock is the cluster's.
  - `BROADCAST_SIGNAL`.
  - Decisions.
  - FEEL, judge and similarity assertions.
  - A few options of the supported instructions. The package README lists them.
- **A failure names the case, the instruction and what it saw,** with the Operate links of the case's instances.
- **Verified against Camunda 8.10.4:** four cases in two files, on the new `xa-revisao` diagram too. Samples 17 and 18 run them.
- This closes Phase 4, the last phase of the Camunda plan.

## v6.29.1 (MCP: Camunda tool escapes what the diagram brings)

Fix for the Camunda tool of XUnitAssured.Mcp. Upgrading from 6.29.0 requires no code changes; the other packages are unchanged.

- **`generate_camunda_test` no longer lets a diagram inject code into the generated test.**
  - In 6.29.0, ids, job types, message and signal names, error codes and element names went into the C# as they were. A quote or a line break in one of them could close a string literal or a comment.
  - Now every value is escaped for where it lands: string literals escape quotes, backslashes and control characters, including U+2028, which C# also takes as a line break. Comments drop line breaks, block comments drop `*/`, and the method name keeps only ASCII letters and digits.
- **A new test feeds the tool a hostile diagram.** It checks that what the BPMN brings is never an identifier of the generated code, and that the code still compiles.
- Found by an automated security review of the 6.29.0 commit.

## v6.29.0 (MCP: Camunda test from the diagram)

In XUnitAssured.Mcp: a tool that writes the Camunda test from the diagram. Upgrading from 6.28.0 requires no code changes; the other packages are unchanged.

- **`generate_camunda_test`** reads the XML of a BPMN diagram and writes an XUnitAssured.Camunda test method:
  - `Deploy` with the process and the children its call activities call;
  - a `MockJobWorker` per job type, a `MockUserTask` per user task, a `MockMessage` per message, a `MockSignal` per signal and a `SkipTimer` per intermediate timer, with placeholder answers;
  - `StartInstance`, `AssertInstanceCompleted`, and the end event the start reaches through sequence flows.
- **The other paths come as commented alternatives:** after an event-based gateway, the first path is taken and the others are commented, and so are boundary events (error, timer, message, signal) and mocking a call activity's child. A call activity with `bindingType="latest"` is flagged in the generated code.
- **The MCP server now has 11 tools.**
- **Verified by compiling:** the tests compile what the tool generates for the repository's three test diagrams against the real XUnitAssured.Camunda, with Roslyn. That caught two defects before release:
  - a trailing comment that swallowed the semicolon;
  - the expected end chosen by document order.

## v6.28.0 (Camunda: diagram coverage)

In XUnitAssured.Camunda: diagram coverage. Upgrading from 6.27.1 requires no code changes; the other packages are unchanged.

- **A coverage report on every test run,** in `camunda-coverage/` in the test output folder:
  - `index.html` draws each diagram with bpmn-js (loaded from a CDN, pinned with an integrity hash), the reached elements in green and the missed ones in red, with the percentage per process and what was missed;
  - `coverage.json` has the same data for a pipeline to read.
- **What it counts:** the flow nodes of the deployed version each test instance ran, per process version, the instances it calls included.
  - An element counts once any instance went through it, completed or terminated.
  - A timer the package skipped, a signal it triggered, or a mocked child's error taking the call activity's boundary counts too. Camunda creates no element for those, so the package records them itself.
- **The report is rewritten as each instance finishes:** when an assertion sees it complete, or when the mocks' loop does. With the 16 samples, writing it only at the end of the test run lost it, because `dotnet test` ended the process first.
- **On by default,** like the Camunda Process Test's report. Turn it off, or move it, with `camunda.coverage.enabled` and `camunda.coverage.directory`.
- **Verified against Camunda 8.10.4 with the 16 samples:**
  - `xa-pedido`, `xa-envio` and `xa-entrega` at 100%;
  - `xa-aprovacao` at 80%. The customer confirmation path has no sample yet.

## v6.27.1 (Camunda.Testcontainers with its docs and symbols)

Fix for XUnitAssured.Camunda.Testcontainers. Upgrading from 6.27.0 requires no code changes; the other packages are the same as in 6.27.0.

- **XUnitAssured.Camunda.Testcontainers now ships with its XML documentation, symbols and SourceLink.** In 6.27.0 it shipped without them, so IntelliSense showed no comments for `CamundaContainerFixture`. The build took any project whose name contains "Test" for a test project, and this package's name does. Now a test project is one whose name ends in `.Test` or `.Tests`.
- **CI:** the `Pack` job now fails when a package has no `.snupkg` or no XML documentation for its own assembly.

## v6.27.0 (Camunda: following the instance your API started)

In XUnitAssured.Camunda: following the instance your API started. Upgrading from 6.26.0 requires no code changes; the other packages are unchanged.

- **`FollowInstance(processInstanceKey)`** follows an instance the system under test started, such as an API that starts the process when it receives an order. The chain's mocks and assertions work on it as they do on one `StartInstance` started, the instances it calls included. It waits up to `assertionTimeoutSeconds` for the instance to show up, because the API may start it a moment after it answers.
- **`FollowInstance(processId, (name, value))`** finds the instance by a variable the test sent, for an API that does not return the key. The value is compared as JSON. If more than one instance has it, the newest is followed, and `GetResult().Warnings` says so.
- **HTTP and Camunda in the same test, verified against Camunda 8.10.4:**
  - an HTTP call starts the instance and the Camunda chain follows it, by key and by variable;
  - a product the `SampleWebApi` created starts the process with the id the API returned.
- **Two new samples:** 15 follows by key and 16 by variable. The samples project now references `XUnitAssured.Http`.
- This closes Phase 3 of the Camunda plan, together with `XUnitAssured.Camunda.Testcontainers` (6.26.0).

## v6.26.0 (XUnitAssured.Camunda.Testcontainers)

New package: **XUnitAssured.Camunda.Testcontainers**, which starts Camunda inside `dotnet test`. Upgrading from 6.25.0 requires no code changes; the other packages are unchanged except XUnitAssured.Camunda, which gains the two types below.

- **`CamundaContainerFixture`** starts `camunda/camunda:8.10.4` in Docker with Testcontainers:
  - the same Camunda the package's CI uses: no Elasticsearch, no Connectors, H2 in memory with the search flushed at once;
  - Basic authentication with a random password per run;
  - ready when the REST API answers an authenticated request, after about 35 seconds.

  It hands the container's address and credentials to every chain, so a CI runner with Docker needs no Camunda of its own. Share it across the suite with a collection fixture.
- **In XUnitAssured.Camunda: `CamundaTestBase<TFixture>` and `CamundaClusterFixture`.** They work the way `KafkaTestBase` works: `Given()` hands the fixture's cluster to the chain. The plain `CamundaClusterFixture` reads `testsettings.json`, so the same test class can point at a remote cluster instead.
- **CI:** a new non-blocking `Camunda Testcontainers` job runs the `Requires=Docker` tests on `ubuntu-latest`, where `dotnet test` starts Camunda by itself. The main test runs, the release and the mutation runs exclude `Requires=Docker`. The mutation runs now also exclude `Requires=Camunda`, which needs a running cluster.

## v6.25.0 (Camunda: child processes)

In XUnitAssured.Camunda: child processes, the last part of the BPMN coverage. Upgrading from 6.24.0 requires no code changes; the other packages are unchanged.

- **`MockChildProcess(callActivityId).ThenComplete(variables)`** answers for the child process a call activity starts, often another team's process. The id is the call activity's in the parent, so a parent that calls the same child in two places can mock just one call. The child starts for real and is intercepted as soon as it shows up: the package ends what it is doing and finishes it at its end event, with the test's variables. The call activity's output mapping runs, as it would for the real child (measured on 8.10.4).
- **`.ThenThrowBpmnError(code, variables)`** finishes the child at its error end event with that code. If the child has none, the package ends the call activity and takes the boundary error event that catches the code. With neither, the test fails, because the error has no path to take.
- **`AssertChildProcessMocked(callActivityId, times)`.** A child with nothing to wait for (no job, timer, message or user task) ends before it can be intercepted, and the test fails saying it ran for real.
- **`bindingType` warning.** A call activity with no `bindingType`, or `bindingType="latest"`, starts the latest version of the child on the cluster, which on a shared cluster may be someone else's. `Deploy` reports it in the new `CamundaStepResult.Warnings`, and a failed assertion repeats it. It does not fail the test.
- **Verified against Camunda 8.10.4** by two new round trip tests and two new samples:
  - the child finished with the output mapping;
  - its BPMN error taking the call activity's boundary.

  The `xa-pedido` test diagram gained that boundary, `envio-falhou`.

## v6.24.0 (Camunda: incidents)

In XUnitAssured.Camunda: incidents. Upgrading from 6.23.0 requires no code changes; the other packages are unchanged.

- **`MockJobWorker(type).ThenFail(errorMessage)`** fails the job with no retries left, like a worker that gave up, and Camunda opens an incident. It is the technical path; the business path, the one a boundary error event catches, is still `ThenThrowBpmnError`.
- **`AssertIncident(elementId, errorType, messageContains)`** waits for an open incident at that element, in the instance or in one it calls. The type and the message are optional. It gives up at once if the instance finishes without the incident. A BPMN error no boundary catches opens an `UNHANDLED_ERROR_EVENT` incident, so it is asserted the same way.
- **Types and element types read as Camunda writes them.** SDK 10.0.1 names its enums without underscores (`JOBNORETRIES`, `SERVICETASK`). The package now shows them as Operate does (`JOB_NO_RETRIES`, `SERVICE_TASK`), in assertions and in failure messages.
- **Verified against Camunda 8.10.4** by two new round trip tests and two new samples:
  - a failed job;
  - an uncaught BPMN error.

## v6.23.0 (Camunda: messages and signals)

In XUnitAssured.Camunda: messages and signals. Upgrading from 6.22.0 requires no code changes; the other packages are unchanged.

- **`MockMessage(name).ThenCorrelate(variables)`** correlates a message every time the instance, or one it calls, waits for it. The correlation is real: it uses the correlation key the subscription carries, so the event's variable mappings run. It covers:
  - intermediate message events;
  - receive tasks;
  - message events after an event-based gateway;
  - message boundary events. The wait states search does not show them, so the package reads the message subscriptions.
- **No message goes to someone else's instance.** Camunda delivers a correlated message to whoever subscribed first, in any instance of the cluster (measured on 8.10.4). Before correlating, the package checks that no instance outside the test awaits the same message with the same key. If one does, it does not correlate, and the first assertion fails asking for a correlation key unique to the test. After correlating, it checks which instance received the message.
- **`MockSignal(name).ThenTrigger(variables)` triggers the signal only in the test's instances, never as a broadcast.** A broadcast would reach every instance on the cluster that waits for the signal. The package modifies the instance instead, with the rules of `SkipTimer`:
  - it covers intermediate signal events, signal events after an event-based gateway, and signal boundary events;
  - the variables go to the instance's scope;
  - a signal event that flows into a joining gateway is refused.
- **`AssertMessageCorrelated(name, times)` and `AssertSignalTriggered(name, times)`.** A failure message now names the messages, with their correlation keys, and the signals the instance waits for and the test does not answer.
- **Verified against Camunda 8.10.4,** on the new `xa-entrega` diagram, by two new round trip tests and two new samples:
  - a message, a signal and a user task;
  - a boundary message cancelling a user task.

## v6.22.0 (Camunda: user tasks and timers)

In XUnitAssured.Camunda: user tasks and timers. Upgrading from 6.21.3 requires no code changes; the other packages are unchanged.

- **`MockUserTask(elementId).ThenComplete(variables)`** completes a user task with what the form would return, every time the instance, or one it calls, reaches it. The mock is by element id, so two user tasks of a diagram get different answers. It covers Camunda user tasks (`zeebe:userTask`); the old job-based ones are still mocked with `MockJobWorker("io.camunda.zeebe:userTask")`.
- **`SkipTimer(elementId)`** skips a timer without moving the cluster's clock, which is shared by everyone on the cluster. It modifies the instance instead: it ends the wait and activates what follows. It covers:
  - intermediate timers;
  - timers after an event-based gateway (the gateway ends);
  - boundary timers, interrupting or not.

  A timer that flows into a joining parallel or inclusive gateway is refused, with the reason. Activated by modification, the join fires without waiting for its other branches: measured on 8.10.4, the process reached its end with a branch still open.
- **`AssertUserTaskMocked(id, times)` and `AssertTimerSkipped(id, times)`.** A skipped timer is `TERMINATED`, not `COMPLETED`, so `AssertCompletedElements` cannot say the instance passed it.
- **Failure messages say what the instance waits for:** the user tasks, messages and signals this test does not answer, and what it declared but never used, such as a timer the instance never reached.
- The package reads the process structure from the deployed BPMN. This works around an SDK 10.0.1 defect: `GetProcessDefinitionXmlAsync` parses the XML response as JSON. A handler under the SDK hands it the XML as a JSON string, and authentication stays with the SDK.
- **Verified against Camunda 8.10.4:** two new round trip tests and two new samples, on the new `xa-aprovacao` diagram:
  - an intermediate timer, a user task and a timer after an event-based gateway;
  - an interrupting boundary timer.

## v6.21.3 (Camunda: no instance left behind)

In XUnitAssured.Camunda: an instance left active is cancelled when the test run ends. Upgrading from 6.21.2 requires no code changes; the other packages are unchanged.

- **An instance still active when the test process exits is cancelled.** Before, it was cancelled only after `mockLifetimeSeconds` (120 s). `dotnet test` usually ends well before that, and the mocks' loop died with it, so the instance of a failed test stayed active on the cluster for good. Measured against Camunda 8.10.4: the instance of the samples' missing-mock test now ends `TERMINATED`, its child process too, and Operate shows where it was waiting.
- An instance the loop already saw finish is not touched. Each cancellation is sent without asking the state first, and all of them wait at most 10 s together, because the process is already exiting.
- **What it cannot do:** a process killed outright (a debugger stopped, an aborted runner) warns no one, and then the instance stays.

## v6.21.2 (Camunda samples on any cluster)

Maintenance. Upgrading from 6.21.1 requires no code changes; the packages are the same as in 6.21.1.

- **The Camunda samples run against any cluster.** `rodar.ps1` takes `-Endereco` and `-Usuario`, or the `CAMUNDA_*` variables the official SDK reads: Basic (the password comes from `CAMUNDA_BASIC_AUTH_PASSWORD` or is asked for without being shown), OAuth client credentials, or no authentication. The default address is `http://localhost:8080`. The variables the script sets last only while it runs.
- **No server-specific detail left.** The samples no longer fetch the password over SSH from one machine. `testsettings.json` keeps only the timeouts, and the address and credentials come from the variables.
- The samples' README says what the cluster needs: Camunda 8.10, and no worker for `xa-cobrar` or `xa-despachar`.

## v6.21.1 (Camunda samples)

Maintenance. Upgrading from 6.21.0 requires no code changes; the packages are the same as in 6.21.0.

- **Camunda samples** (`src/XUnitAssured.Camunda.Samples.Remote.Test`, not packaged). Six tests against a real Camunda 8.10, written to study the package and to check each new version by eye in the Modeler and Operate:
  - the order process with its child process;
  - deploy by process id and by path;
  - the BPMN error path;
  - three instances in parallel;
  - the failure message of a missing mock;
  - QA starting the deployed version by tag and the latest.
- Each sample writes its instance, Operate link and what to look at. `rodar.ps1` runs the samples and then lists every instance they created, with version, state, variables and Operate link. The samples use the same BPMN files as the package's round trip tests, linked rather than copied.

## v6.21.0 (Camunda: deploy by id, test what is deployed)

In XUnitAssured.Camunda: deploy by process id, and test the version already on the cluster. Upgrading from 6.20.0 requires no code changes, except for one assertion message; the other packages are unchanged.

- **`Deploy("order", "shipping")`.** `Deploy` takes process ids as well as paths. With an id, the file is the `.bpmn` in the test output folder that declares `<bpmn:process id="order">`, whatever the file is called. An id declared in two files is refused, naming both; an unknown id lists the processes the folder has. A path works as before.
- **Test what is already deployed.** A chain without `Deploy` starts the version already on the cluster: the latest, or the one named by `StartInstance(..., version: 3)` or `StartInstance(..., versionTag: "1.4.0")`. This is how QA tests what was deployed to staging, end to end with the real workers or mocking only the job types that have no worker there. A chain that deploys still starts only what it deployed, and a failed deploy never falls back to the version on the cluster.
- **The result says which version ran.** `CamundaStepResult` gained `Version`, `VersionTag` and `DefinitionKey`, and a failed assertion names the version: `(order version 7, tag '1.4.0')`. A version the cluster does not have fails the step, listing the versions it has, newest first.
- **One message changed:** the failure line `Jobs waiting with no mock` is now `Jobs waiting for a worker (not mocked in this test)`, because with the real workers of staging, a job without a mock is not a mistake. A test that compared that text needs the new one.
- Verified against Camunda 8.10.4: the version and tag search, and a chain without `Deploy` running by tag and by latest version, pass in the `Requires=Camunda` round trip.

## v6.20.0 (XUnitAssured.Camunda)

New package: **XUnitAssured.Camunda**, for Camunda 8.10. Upgrading from 6.19.1 requires no code changes; the other packages are unchanged.

- **Run a real process instance and mock what the diagram depends on.** `Deploy(...)` sends the BPMN files in one deployment, `MockJobWorker(type).ThenComplete(...)` or `.ThenThrowBpmnError(...)` answers a job type, `StartInstance(processId, variables)` starts the instance from the definition the chain deployed. Built on the official `Camunda.Orchestration.Sdk` 10.0.1.
- **Made for a shared cluster.** Camunda's job activation does not filter by instance, so the mocks are not job workers. They search the jobs of their own instance, and of the instances it calls, and resolve them by key without activating them. Three tests in parallel, mocking the same job types with different answers, each got its own answer against a real Camunda 8.10.4.
- **Assertions that wait for the cluster.** `AssertInstanceCompleted`, `AssertInstanceActive`, `AssertCompletedElements`, `AssertActiveElements`, `AssertVariable`, `AssertJobMocked` and `AssertNoIncidents`. They ask again until `assertionTimeoutSeconds`, because the search trails the engine. They give up at once when waiting cannot help: the instance was terminated, has an incident, or a mock found its job leased by another worker. A failure says what the instance waits at, which jobs have no mock, and its Operate address.
- **Configuration** in the `camunda` section of `testsettings.json`, or the SDK's `CAMUNDA_*` variables. With no authentication type, a username means Basic: the SDK alone sends no credentials then, and the cluster answered 401.
- **One limit, documented:** the cluster must not run real workers for the mocked job types. The job search does not show activations, so the package detects one only when the worker used a lease.
- **CI:** a non-blocking `Camunda round trip` job runs the `Requires=Camunda` tests against a `camunda/camunda:8.10.4` service container. The main test runs and the release exclude `Requires=Camunda`.

## v6.19.1 (plan for XUnitAssured.Camunda)

Maintenance. Upgrading from 6.19.0 requires no code changes; the packages are the same as in 6.19.0.

- **Plan for an XUnitAssured.Camunda package** (`docs/camunda-plano.md`, in Portuguese). It would test BPMN process instances on a shared Camunda 8.10 cluster, with the diagram's external dependencies (job workers, connectors, child processes, timers) mocked per process instance.
- **Proof of concept** (`spikes/CamundaFase0/`, outside the solution and not packaged). Six scenarios against a real Camunda 8.10.4 with the official `Camunda.Orchestration.Sdk` 10.0.1, all passing:
  - jobs completed or failed with a BPMN error by key, without being activated;
  - a child process mocked through instance modification, with the call activity's output mapping still applied;
  - timers skipped;
  - five instances in parallel, each mocking the same job type with a different answer.
- **One limit found:** a job activated by another worker does not show up as activated in the job search. The test can detect it only when that worker activated the job with a lease.

## v6.19.0 (Kafka OAUTHBEARER with OpenID Connect)

New in Kafka: SASL/OAUTHBEARER with OpenID Connect. Upgrading from 6.18.1 requires no code changes.

- **OAUTHBEARER (OIDC)** (#179). The Kafka client asks an OAuth 2.0 token endpoint for a token with its client credentials, presents it to the broker, and renews it by itself: Keycloak, Okta, Azure AD, Confluent Cloud.
  - DSL: `.WithOAuthBearer(tokenEndpointUrl, clientId, clientSecret, scope: null, useSsl: true)`.
  - Config: `KafkaAuthConfig.UseOAuthBearer(...)`, or in `testsettings.json`: `"authentication": { "type": "OAuthBearer", "oAuthBearer": { "tokenEndpointUrl": "...", "clientId": "...", "clientSecret": "...", "useSsl": false } }`.
  - It reaches every step, `KafkaClassFixture` and `ToClientConfig`, like the other authentication types. A missing endpoint, client id or secret fails naming it.
- **New sample `KRaft-OAUTH_OIDC`**: a Keycloak and a broker that validates its tokens, checked end to end with the new type. Adapting it from the maintainer's local setup needed four fixes, listed in its README: the broker's `sasl.oauthbearer.*` settings instead of Strimzi's `oauth.*` JAAS options, an expected audience, listener names without `_`, and waiting for the realm's keys before the broker starts.
- **New sample `KRaft-SASL_MULTI`**: one broker with PLAIN, SCRAM-SHA-256 and SCRAM-SHA-512 on the same listener, on port 9096.
- **The sample scripts are executable in git.** They were stored without the executable bit, so on a Linux or macOS clone the brokers that run them directly (PLAINTEXT, SASL/SSL, SCRAM-256, SCRAM-512) exited with 126. Windows hid it. Checked from a fresh Linux clone.

## v6.18.1 (every Kafka sample broker starts)

Maintenance. Upgrading from 6.18.0 requires no code changes; the packages behave as in 6.18.0.

- **Every Kafka sample broker starts from a fresh clone, and its `testsettings.json` connects** (#177). Each of the seven brokers was started from the repository and reached through `ToClientConfig`, a step and `KafkaClassFixture`.
- **KRaft-SASL_SCRAM never started.** Its command was a folded YAML block with comments inside, so every line became part of a comment and the container exited at once. It also had no JAAS configuration, and its healthcheck used a file that was never written. It now formats the storage with the SCRAM users (admin and user1, SHA-256 and SHA-512), as the SCRAM-512 sample does, and has a `testsettings.json`.
- **KRaft-PLAINTEXT and KRaft-SASL_SCRAM-256** mounted a `clusterID` folder that git did not keep, since it was empty, so podman refused to start them. They now keep their cluster ID, as SCRAM-512 and SASL/SSL do.
- **Zookepper** has a `testsettings.json`, with `localhost:29092`, the address its broker advertises to the host.

## v6.18.0 (certificate paths relative to the settings file)

Kafka certificate paths, and the SASL/SSL sample. Upgrading from 6.17.0 requires no code changes.

- **Relative certificate paths are read from the settings file's folder** (#175). `sslCaLocation`, `authentication.ssl` (`sslCaLocation`, `sslCertificateLocation`, `sslKeyLocation`) and `schemaRegistry.sslCaLocation` went to librdkafka as written, so a relative path depended on the folder the test runner started in, which differs between runners. Now `"sslCaLocation": "certs/ca.pem"` next to `testsettings.json` works from any of them. When no file exists there, the path is kept as written, as before. An absolute path is unchanged.
- **The SASL/SSL sample connects as committed.** Tested against its broker:
  - Its CA path was an absolute path on one machine, pointing at a renamed project, so the sample could not connect. It is now relative.
  - The `"sslEndpointIdentificationAlgorithm": "None"` that 6.17.0 added to it is not needed: the broker certificate names `kafka`, `localhost` and `127.0.0.1`. It was removed, so the sample now verifies the hostname.
- **The sample brokers start from a Windows checkout.** With `core.autocrlf=true`, the scripts and cluster IDs mounted into the containers came out with CRLF, and the broker would not start: `/bin/sh^M: bad interpreter`, `Illegal base64 character d`. A new `.gitattributes` keeps `*.sh`, `clusterID` and `*.conf` with LF.

## v6.17.0 (one meaning for the kafka section)

Behaviour changes in Kafka. **If you connect over SSL to a broker whose certificate names another host** (for example a local broker issued for `kafka` and reached as `localhost`), **add `"sslEndpointIdentificationAlgorithm": "None"` to the `kafka` section**: `KafkaClassFixture` no longer turns the hostname check off by itself.

- **One meaning for the `kafka` section** (#172). It turned into a client config in three ways — the steps, `KafkaClassFixture`, and `KafkaSettings.ToClientConfig()` (used by `KafkaTestFixture`, `ToProducerConfig`, `ToConsumerConfig` and the helpers) — and each read a different part of it. Tested against a SASL broker: credentials at the top of the section connected through `ToClientConfig` and failed from a step with "1/1 brokers are down"; credentials in `authentication`, the other way round. Now all three start from the same base and apply the authentication over it:
  - SASL credentials can be written at the top, with the Confluent names (`securityProtocol`, `saslMechanism`, `saslUsername`, `saslPassword`), or in `authentication`. Both reach every path; when `authentication` has a type other than `None`, it wins.
  - `ToClientConfig`, `ToProducerConfig` and `ToConsumerConfig` now apply `authentication`. They ignored it.
  - The steps now apply the top-level `sslCaLocation`, `enableSslCertificateVerification` and `additionalProperties`, as `ToClientConfig` did.
- **The broker hostname check is turned off only when asked.** `KafkaClassFixture` turned it off whenever the protocol was SSL; the steps did not. Now nothing does unless the section has the new `"sslEndpointIdentificationAlgorithm": "None"`. The SASL/SSL samples, which reach a certificate issued for `kafka` as `localhost`, now say so.
- **The `KRaft-SASL_PLAIN` sample settings** were a copy of the SASL/SSL ones and could not connect to their own broker. They now match its `docker-compose.yml`, checked against it.

## v6.16.0 (one way for Kafka authentication)

Fix, with behaviour changes in Kafka. Upgrading from 6.15.7 requires no code changes, but **check your Kafka batch steps and SASL over SSL setups**.

- **One way for a Kafka authentication to reach the client** (#164). The four steps and `KafkaClassFixture` each had their own copy of how a `KafkaAuthConfig` becomes producer or consumer configuration, and the copies had drifted. Now there is one.
- **The batch steps use the fixture's authentication.** `KafkaBatchProduceStep` and `KafkaBatchConsumeStep` ignored the authentication a `KafkaClassFixture` puts in the context and went straight to the settings file; the single steps did not. Now every step resolves it the same way: its own, then the fixture's, then the settings file.
- **The steps apply the `ssl` section of a SASL authentication.** With SASL over SSL, `authentication.ssl` holds the broker's CA and whether to verify its certificate. `KafkaClassFixture` applied it; the steps did not, so a broker with a self-signed certificate worked through the fixture and failed from a consume step.
- **Tests without a broker** check that each kind of authentication (SASL/PLAIN, SASL/SCRAM-256 and 512, SSL, mutual TLS) reaches both producer and consumer configuration, through every step and the fixture.

**Behaviour changes:** a batch step run with a fixture now uses the fixture's authentication where it used the settings file's; and a step with SASL over SSL now applies `authentication.ssl`. Both follow what the single steps and the fixture already did.

Not in this release: the top-level SSL and SASL settings of the `kafka` section still reach only some paths (#172).

## v6.15.7 (no test depends on httpbin.org)

Maintenance. Upgrading from 6.15.6 requires no code changes; the packages behave as in 6.15.6.

- **No test depends on httpbin.org any more** (#169). The custom-headers test failed on the CI runners in 2 of 3 PRs because of httpbin, and it only checked for a 200. It now runs offline with Flurl's `HttpTest`, in the main suite, and checks that `X-Custom-Header: TestValue` was sent.
- The Basic authentication test pointed at httpbin but never sent the request, and checked nothing about authentication. It now sends it offline and checks `Authorization: Basic dXNlcjpwYXNz`. The skipped Basic integration test against httpbin, which never ran, was removed.

## v6.15.6 (packages published in dependency order)

Maintenance. Upgrading from 6.15.5 requires no code changes; the packages behave as in 6.15.5.

- **The release publishes the packages in dependency order** (#168). In 6.15.5, one push of every package timed out on `XUnitAssured.Mcp`, the largest, and stopped; `XUnitAssured.Core` came after it and was not sent. For about ten minutes, the 6.15.5 packages already on NuGet depended on a `XUnitAssured.Core 6.15.5` that did not exist. Re-running the job fixed it.
- Now the packages go one at a time: Core first, Mcp last. The job stops at the first that fails, so a failure leaves packages missing, never a published package without its dependency. The push timeout went from 300 s to 900 s, and a package missing from the order, or one the build did not produce, stops the job before anything is sent.

## v6.15.5 (timeout messages without the machine's culture)

Fix, with a small behaviour change. Upgrading from 6.15.4 requires no code changes.

- **Timeout messages write the seconds the same on every machine** (#166). They are in English, but the number was formatted with the machine's culture: a 1.5 s timeout read "within 1,5s" on a pt-BR machine and "within 1.5s" on a CI runner. The Kafka ones also printed every digit of a timeout that was not a round number ("1.2345678s"). Now all four use a dot and at most two decimal places:
  - RabbitMq: `No message arrived on '…' within 1.5s.`
  - Kafka: `No message consumed from topic '…' within timeout of 1.5s`, `Failed to produce message to topic '…' within timeout of 1.5s`, and `Consumed N of M messages from topic '…' within timeout of 1.5s`.
- **Behaviour change:** the text of these four messages changes on machines whose culture uses a decimal comma, and the Kafka ones are rounded. A suite that compares them on such a machine needs the new text. Whole-second timeouts, the common case, read as before.

## v6.15.4 (tests for the RabbitMq step results)

Maintenance. Upgrading from 6.15.3 requires no code changes; the packages behave as in 6.15.3.

- **Tests for the RabbitMq step results** (#163), which the first mutation run on RabbitMq (#159) found unguarded. Each factory of `RabbitMqStepResult` is now checked for `Success`, `Metadata.Status`, the errors and every property the validation builder reads.
- The most important one: **a consume that timed out is a failure**, and `AssertSuccess()` fails on it. If that ever turned into a success, a test that received no message would pass.

## v6.15.3 (mutation testing for Kafka and RabbitMq)

Maintenance. Upgrading from 6.15.2 requires no code changes; the packages behave as in 6.15.2.

- **The nightly mutation run covers Kafka and RabbitMq** (#159), with the same offline suite and filter as Core and Http. Until now they had never been mutation-tested.
- First scores: Kafka 27.5%, RabbitMq 32.5%. Most undetected mutants are in code that talks to the broker, which only the `Requires=Broker` tests reach. Where the offline tests do reach the code, they catch 62% (Kafka) and 65% (RabbitMq) of the mutants, against about 83% for Core and Http.

## v6.15.2 (tests for concurrent results)

Maintenance. Upgrading from 6.15.1 requires no code changes; the packages behave as in 6.15.1.

- **Tests for the concurrent result** (#157), which the nightly mutation run found unguarded. `Concurrently(...)` returns one result for all the requests; nothing checked three of its values:
  - `Metadata.Status`: `Succeeded` when every request succeeds, `Failed` when one fails;
  - the request number in `Errors` ("Request 2: ...");
  - `StatusCode` when every response has the same code.

  `Success`, `ExpectStatusCode` and the assertions were already covered.

## v6.15.1 (OAuth2 tests on the network path)

Maintenance. Upgrading from 6.15.0 requires no code changes; the packages behave as in 6.15.0.

- **Offline tests for OAuth2 on the network path and the token response edge cases** (#158), which the nightly mutation run found unguarded:
  - an OAuth2 step without a custom `HttpClient` asks for the token once, and the next step reuses it;
  - a token response without `access_token` fails the step, says so, and sends nothing to the API;
  - a token without `expires_in` (or with 0) is kept for an hour;
  - the `refresh_token` of the response is kept with the token;
  - a configuration missing `TokenUrl`, `ClientId` or `ClientSecret` fails naming it, before any token request.
- Documented by a test, unchanged: a token is reused only while it has more than five minutes left, so one with `expires_in` under 300 s is asked for again on every request.

## v6.15.0 (one OAuth2 token per fixture)

Performance, with a behaviour change. Upgrading from 6.14.0 requires no code changes.

- **The scenarios of one fixture share its OAuth2 token** (#152). `Given(fixture)` and `HttpTestBase.Given()` ask the fixture for a client in each scenario, and since 6.13.0 a token was kept per client, so each scenario asked for its own. A token is now kept by the fixture that gave the client: a class of 20 OAuth2 tests against a `WebApplicationFactory` makes one token request, not 20. Different credentials still get different tokens, and a token still lives no longer than the fixture, so it is never offered to the next test server.
- `Given(HttpClient)` keeps the token with that client: scenarios given the same client share it.
- `.WithHttpClient(...)` in a fixture's scenario keeps the token with that client, not the fixture's: another client may be another server.
- `TokenCache.Enabled = false` still asks for a token on every request.
- **Behaviour change:** a fixture whose `CreateClient()` returns clients for different servers, each issuing its own tokens, now offers the first server's token to the others. Turn the cache off for it, or start those scenarios with `Given(fixture.CreateClient())`.

## v6.14.0 (environment files reach every section)

Fix, with a behaviour change. Upgrading from 6.13.1 requires no code changes, but **check your environment files: they now take effect**.

- **`TEST_ENV` reaches every section** (#154). With `TEST_ENV=staging` or `TestSettings.Load("staging")`, `testsettings.staging.json` was read only for `testMode` and `environment`. The `http`, `kafka`, `rabbitmq` and `playwright` sections — `GetHttpSettings`, `GetKafkaSettings`, `KafkaSettings.Load`, `RabbitMqSettings.Load`, the Playwright settings — came from the base file, and the environment file was silently ignored. The README promised otherwise.
- **The environment file is laid over the base file**, in every section and at any depth: what it writes wins, what it leaves out is kept. A staging file needs only what differs; `{ "http": { "baseUrl": "..." } }` keeps the base authentication. With no environment file, the base file is used (before, `TestSettings.Load("name")` with no such file gave the defaults). With `TESTSETTINGS_PATH`, the environment file is the one next to it: `custom.staging.json` for `custom.json`.
- **The `"environment"` key inside a file is a label**: it never chose a file, and the README said it did. Corrected.
- A malformed environment file makes `TestSettings.Load` fail naming it; the package sections stay unread, as for a malformed base file.

## v6.13.1 (one settings-file search)

Fix. Upgrading from 6.13.0 requires no code changes.

- **One settings-file search for every package** (#146). Where `testsettings.json`, `httpsettings.json` and the old `playwrightsettings.json` are looked for was written in five places, and they had drifted: Kafka, RabbitMQ and Playwright also walked up from the test assembly's output directory, while `TestSettings.Load`, `GetHttpSettings` and `httpsettings.json` did not — the same file next to the project was found by one package and not by the next. Now every package looks in the current directory and up to three parents, then up from the output directory. A file that was not found before may be now; `TESTSETTINGS_PATH`, `XUNITASSURED_HTTP_SETTINGS_PATH` and `XUNITASSURED_PLAYWRIGHT_SETTINGS_PATH` still win.
- **An `httpsettings.json` environment overrides only what it writes.** An `environments` entry, or an `httpsettings.{environment}.json` file, that left out `authentication` erased the base authentication, and one that left out `timeout` reset it to 30. This only reached code calling `HttpSettings.Load(path, environment)`.

## v6.13.0 (the same authentication on both request paths)

Fix, with one behaviour change. Upgrading from 6.12.2 requires no code changes.

- **Authentication is the same on both request paths** (#147). A request with a custom `HttpClient` — `WebApplicationFactory`, `HttpTestFixture` — had its own copy of the authentication, which did not apply OAuth2 (the request went out with no token) and read a query ApiKey only from the step, not from `httpsettings.json`. Both paths now go through the same handlers.
- **OAuth2 with `WebApplicationFactory`**: the token is requested through the test client, since the server lives in memory and does not answer over the network. Tokens are reused between requests: per client on that path, one cache for the network. The cache key carries everything sent for the token, so a wrong secret or other scopes never get a cached one. `TokenCache.Enabled = false`, documented and ignored until now, turns the cache off.
- **Behaviour change: an empty credential is sent as it is** — an empty Basic user or password, Bearer token or ApiKey value — so a test can check that the API refuses it. The custom `HttpClient` path always did this; the Flurl path threw before sending. An ApiKey with no name still throws.

## v6.12.2 (what the mutation run found unguarded in Http)

Maintenance. Upgrading from 6.12.1 requires no code changes; the packages behave as before.

- **Tests for the Http behaviours the nightly mutation run found unguarded** (#145). Assertions that could stop failing, because the tests only had conditions that pass: `AssertJsonPath` with a predicate, `AssertJsonPathAll`, `AssertJsonPathAny`, the predicates of the header, cookie and problem details assertions, and `AssertSuccess` and `Validate` before the await. Behaviour: query parameters on a URL that already has one, the last verb winning, content headers, status class edges, concurrent and arrange failures, settings merging, `Set-Cookie` and problem details parsing. The authentication handlers, which nothing applied, now are, with OAuth2's token call faked.
- **CI runs the HTTP local samples** (#144), the suite that sends requests through a custom `HttpClient` with each kind of authentication; the nightly mutation run includes them for Http.
- Found on the way, for a later release: authentication differs between the Flurl and the custom `HttpClient` paths (#147), and settings merging cannot tell a field left out from its default (#146).

## v6.12.1 (what the mutation run found unguarded)

Maintenance. Upgrading from 6.12.0 requires no code changes; the packages behave as before.

- **Tests for the Core behaviours the first nightly mutation run found unguarded** (#141): the `Validate` that Kafka, RabbitMQ and Playwright inherit running its check; a chain executed again not re-running the checks of a step that already ran; `JsonPath<T>` of an object or a list; `AssertJsonPathNull` and `AssertJsonPathMissing` through an index; the exception type and message `AssertSuccess` lists; the custom message and the list of what was logged in the log assertions; `TEST_ENV` choosing `testsettings.{env}.json`. Each was checked against its mutant applied by hand. Equivalent mutants and message strings with no information were left out on purpose.

## v6.12.0 (generated tests in the documented form, and 7.0 ahead of time)

Additive. Upgrading from 6.11.x requires no code changes.

- **The MCP server and the Playwright recording translator generate async tests** (#134): `public async Task`, `await Given()...ExecuteAsync()` and the assertions right after it, the form every README teaches since 6.11.0. They wrote `.When().Execute().Then()`, which keeps working. The `list_http_dsl_methods` and `list_kafka_dsl_methods` tables show `ExecuteAsync` first. `PlaywrightCodeTranslator.TranslateToGivenBlock` returns the awaited block, for the body of an async test method.
- **`UPGRADING.md` opens with 7.0.0, planned** (#129): what the major removes or changes, and for each the form that works in 6.x and keeps working in 7.0, compiled in the test suite. A test fails when an `[Obsolete]` in a package does not say when it is removed, or breaks the build.
- **Mutation testing, nightly** (#128): Stryker.NET runs on Core and Http on a schedule, with the surviving mutants published as a report. Not on pull requests, and with no score gate.

## v6.11.0 (assertions before the await in every package)

Additive. Upgrading from 6.10.x requires no code changes.

- **Kafka, RabbitMQ and Playwright: the assertions chain before the `await`** (#127), as HTTP has since 6.8.0 (#115): `await Given()...ExecuteAsync().AssertSuccess().AssertJsonPath(...)`, without `(await ...ExecuteAsync()).Then()`. The browser tests of the consumer suite wrote `var x = await ...ExecuteAsync(); x.Then().AssertFetchStatus(200);` 5 times in 3 tests (#126). The extensions are generated from each builder at compile time — the Playwright builder has 181 public methods, and a hand-written copy would drift on the first new one — so every public method has its counterpart on the `Task`, with the same name and documentation; methods with an `out` parameter stay on the builder. The generator runs only while the packages compile and is not a package dependency. The Kafka, RabbitMQ, Playwright and Playwright.AspNetCore READMEs teach `ExecuteAsync`; the Kafka one taught the synchronous `Execute()` 14 times.
- **Playwright: `ExpectFetchStatus`** (#132) checks the status of the last `FetchFromPage` of a step before the last one of a scenario, right after it runs, and stops the scenario there when it is another — the counterpart of `ExpectStatusCode` (#121). Same message as `AssertFetchStatus`, which now shares its check; accepts several codes.

## v6.10.1 (a build with no warnings)

Maintenance. Upgrading from 6.10.0 requires no code changes.

- **The build has no warnings, and a new one fails it** (#124). The Release build carried 380: a build-time dependency with a moderate advisory (`Microsoft.Build.Tasks.Git` 8.0.0, through SourceLink, now 10.0.401; it never reached the packages), the sample API on EF Core and ASP.NET Core 7 (out of support, now 8.0.31, which drops a vulnerable `Azure.Identity`), an xUnit version mismatch, nullable and documentation warnings. `TreatWarningsAsErrors` now holds the line, except for package vulnerability warnings, so a new advisory does not break a build nobody changed.
- **Kafka: `KafkaStepResultExtensions.JsonPath` is obsolete** (#125), and removed in 7.0. `KafkaStepResult` has its own `JsonPath<T>`, which a call written as `result.JsonPath<T>(...)` always reached; the extension was only reachable by its class name, with another implementation and other messages. It now delegates, so both give the same result.

## v6.10.0 (the status of a step before the last)

Additive. Upgrading from 6.9.x requires no code changes.

- **`ExpectStatusCode` checks the status of a step before the last one of a scenario** (#121). Creating and reading back is a two-step scenario, and the consumer suite checked the creation's status by hand in 3 places, in a `Validate` with the body appended to the message; the README example did not check it at all, and a failed creation made the read-back go to `/products/0` and fail for a reason that was not its own. `ExpectStatusCode(201)` registers the check on the step being described, run right after that step: when the status is another, the scenario stops there, and the failure is `AssertStatusCode`'s, prefixed with the request — `After POST /orders: Expected HTTP status code 201 but got 400. Body: ...`. It accepts several codes and checks every response of `Concurrently` requests. Named `Expect` because it registers a check instead of making one: `Given()...Get().AssertStatusCode(200)` without `ExecuteAsync` keeps failing to compile. The create-and-read-back example in the HTTP README and the local sample check the creation's 201.

## v6.9.0 (a filter in the JSON path)

Additive. Upgrading from 6.8.x requires no code changes.

- **A filter in the JSON path finds the item of a list by a field** (#118). The consumer suite read whole lists as `JsonElement`, picked the item with LINQ and read each field with `GetProperty` in 7 places; five comments said "the JSON path does not filter". The path now takes `[?(@.field=='value')]` and goes on inside the item: `$.drafts[?(@.weekStart=='2026-10-05')].incomplete.requested`. Equality only, `==` and `!=`, against a text in quotes, a number, `true`, `false` or `null`; `@.a.b` reads a nested field and `@` alone the item itself. As in RFC 9535, `!=` is the opposite of `==`, and text and number do not mix. Where one value is read, the filter must match exactly one item, and the error says how many it matched out of how many; `JsonPathAll` and the list assertions take every match. `AssertJsonPathCount` counts the matches of a filter at the end, and the list of the item a filter in the middle finds. The navigator is shared, so Kafka, RabbitMQ and Playwright paths filter too.
- **`AssertJsonPathAny`** (#118), the counterpart of `AssertJsonPathAll`, for a condition on several fields of the same item, which a filter cannot combine. Fails listing the values when none satisfies it, and when the path selects nothing.
- JSON path errors show the path as the test wrote it, with the `$.`; the HTTP, Kafka and RabbitMQ results used to strip it before navigating.

## v6.8.0 (assertions before the await)

Additive. Upgrading from 6.7.x requires no code changes.

- **The assertions chain before the `await`** (#115). The common HTTP chain needed the `await` in parentheses to reach them — `(await Given()...ExecuteAsync()).Then().AssertStatusCode(201)` — and the consumer suite wrote that 221 times. `HttpValidationTaskExtensions` has, on `Task<HttpValidationBuilder>`, a method for every public method of `HttpValidationBuilder`, with the same name, so the chain reads `await Given()...ExecuteAsync().AssertStatusCode(201).AssertJsonPath(...)`. `JsonPath<T>`, `JsonPathAll<T>` and `GetResult` end the chain with the value; the assertions inherited from the generic builder keep the chain on HTTP. `Extract(out ...)` and `ExtractJsonPath(..., out ...)` cannot be asynchronous and stay on the builder. A reflection test fails when a builder method is added without its counterpart. The HTTP and ASP.NET Core package READMEs use the new form.

## v6.7.0 (a client per scenario, null or missing, the cookie set)

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
