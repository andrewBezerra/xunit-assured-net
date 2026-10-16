# XUnitAssured.Camunda.Testcontainers

Starts Camunda 8.10 inside `dotnet test` for
[XUnitAssured.Camunda](https://www.nuget.org/packages/XUnitAssured.Camunda): a CI runner with
Docker needs no Camunda of its own.

## Installation

```bash
dotnet add package XUnitAssured.Camunda.Testcontainers
```

## Usage

```csharp
using XUnitAssured.Camunda.Extensions;
using XUnitAssured.Camunda.Testcontainers;
using XUnitAssured.Camunda.Testing;

public class OrderTests(CamundaContainerFixture camunda)
    : CamundaTestBase<CamundaContainerFixture>(camunda), IClassFixture<CamundaContainerFixture>
{
    [Fact]
    public async Task Order_Completes() =>
        await Given()
            .Deploy("order", "shipping")
            .MockJobWorker("charge-card").ThenComplete()
            .StartInstance("order")
            .ExecuteAsync()
            .AssertInstanceCompleted();
}
```

`CamundaTestBase.Given()` hands the container's address and credentials to the chain. Every verb
and assertion of XUnitAssured.Camunda works the same as against a remote cluster.

## What it starts

- **The same Camunda as the package's own CI:** `camunda/camunda:8.10.4` alone, with no
  Elasticsearch and no Connectors. Without Connectors, no runtime competes for the jobs you mock.
- **Secondary storage in H2, in memory, flushed at once** (`FLUSHINTERVAL=PT0S`), as the Camunda
  Process Test does: a search sees a change as soon as the engine makes it.
- **Basic authentication** with a `xunit` user and a new random password on every run. The fixture
  hands both to the chains, so the test never needs them.
- **Ready means the REST API answered an authenticated request,** not that the process started.
  That takes about 35 seconds.

The timeouts (`assertionTimeoutSeconds`, `pollIntervalMilliseconds`, `mockLifetimeSeconds`) still
come from the `camunda` section of `testsettings.json`.

## One Camunda for the whole suite

Starting Camunda for every test class would cost 35 seconds each. Share one container through a
collection fixture:

```csharp
[CollectionDefinition("Camunda")]
public sealed class CamundaCollection : ICollectionFixture<CamundaContainerFixture>;

[Collection("Camunda")]
public class OrderTests(CamundaContainerFixture camunda) : CamundaTestBase<CamundaContainerFixture>(camunda)
{
    // ...
}
```

The package's isolation by process instance key still applies: tests sharing the container never
see each other's jobs.

## Customizing

Derive from the fixture to change the image, or to add settings to the container:

```csharp
public sealed class MyCamunda : CamundaContainerFixture
{
    protected override string Image => "camunda/camunda:8.10.5";

    protected override ContainerBuilder Configure(ContainerBuilder builder) =>
        builder.WithEnvironment("JAVA_TOOL_OPTIONS", "-Xmx768m");
}
```

`Fixture.RestAddress` is the REST address on the host. Operate is at `{RestAddress}/operate`; log
in with the fixture's `Settings.Authentication`.

## Requirements

A Docker that Testcontainers can reach. The GitHub `ubuntu-latest` runner has one. Without Docker,
the fixture fails to start, and the whole test class fails with it. In the repository, these tests
carry `[Trait("Requires", "Docker")]`, so a filter can leave them out where there is no Docker.

Without a license key, Camunda shows a "Non-Production License" banner. Its license allows that
for testing.

## Supported Frameworks

- .NET 8
- .NET 9
- .NET 10

## License

MIT. Camunda itself is under the Camunda License 1.0.
