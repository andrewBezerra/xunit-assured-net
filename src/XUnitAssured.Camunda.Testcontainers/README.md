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
- **Only TLS, on REST and gRPC.** On every run the fixture generates a test certificate authority and
  a server certificate for `camunda`, `localhost` and `127.0.0.1`, and the chains trust only that
  authority (`Settings.CaCertificatePath`). There is no `http` address.
- **Basic authentication** with a `xunit` user and a new random password on every run. The fixture
  hands both to the chains, so the test never needs them. Authorizations are off, as in the package's
  CI: the container is the test's own.
- **Ready means the REST API answered an authenticated https request,** not that the process started.
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

## The customer's workers in Docker: `CamundaFixture`

`CamundaContainerFixture` starts the Camunda alone, and the test mocks every job. To run the process with the
customer's own workers, declare them, and what they depend on, in the `camunda` section of `testsettings.json`, and use
`CamundaFixture`:

```json
{
  "camunda": {
    "containers": {
      "authentication": "basic",
      "services": [
        { "name": "postgres", "image": "postgres:16", "environment": { "POSTGRES_PASSWORD": "test" },
          "wait": { "port": 5432 } },
        { "name": "order-worker", "image": "shop/order-worker:1.4", "dependsOn": ["postgres"],
          "environment": {
            "Camunda__Url": "${camunda.restAddress}",
            "ConnectionStrings__Orders": "Host=${postgres.host};Port=${postgres.port:5432};Password=test"
          },
          "wait": { "log": "Worker started" } }
      ]
    }
  }
}
```

```csharp
public class OrderTests(CamundaFixture camunda)
    : CamundaTestBase<CamundaFixture>(camunda), IClassFixture<CamundaFixture>
{
    // The same chains: the jobs the list's workers serve are not mocked.
}
```

- **One network,** with the Camunda on it as `camunda`, and the list started in `dependsOn` order, each ready before
  the next. Testcontainers does not read `docker-compose`: write the list once, from the system's compose file.
- **Every container gets the Camunda's address** as `CAMUNDA_REST_ADDRESS`, `ZEEBE_REST_ADDRESS` and
  `ZEEBE_GRPC_ADDRESS`, and the mode as `CAMUNDA_AUTH_STRATEGY`. A worker that reads other names uses the placeholders
  `${camunda.restAddress}`, `${camunda.grpcAddress}`, `${camunda.username}`, `${camunda.password}`, `${<name>.host}` and
  `${<name>.port:<port>}`.
- **`authentication`:** `basic` (the default) gives the workers and the test the same `xunit` user, with a new password
  on every run, in `CAMUNDA_BASIC_AUTH_USERNAME` and `CAMUNDA_BASIC_AUTH_PASSWORD`; `none` opens the API. `oauth` comes
  in 6.40.0.
- **Without `containers`,** the fixture runs on the cluster in `camunda.restAddress`; with neither, it fails, saying
  what to add.

### What a worker has to accept: TLS from a test authority

The Camunda answers only `https`, with a certificate from an authority generated on every run. The fixture copies the
authority into every container at `/xunitassured/tls/ca.pem` and points the usual variables at it:

| Technology | Variable the fixture sets |
| --- | --- |
| OpenSSL (Go, .NET on Linux, curl) | `SSL_CERT_FILE` |
| Node | `NODE_EXTRA_CA_CERTS` |
| Python (requests) | `REQUESTS_CA_BUNDLE` |
| Camunda .NET SDK | `CAMUNDA_MTLS_CA_PATH` |
| Camunda Java client | `ZEEBE_CA_CERTIFICATE_PATH` |
| Spring Zeebe | `CAMUNDA_CLIENT_CACERTIFICATEPATH` (`camunda.client.ca-certificate-path`) |
| Java (any) | `JAVA_TOOL_OPTIONS` with `-Djavax.net.ssl.trustStore=/xunitassured/tls/truststore.p12`, its password and `-Djavax.net.ssl.trustStoreType=PKCS12` |

A worker that reads another name uses `${camunda.caCertificatePath}`, `${camunda.trustStorePath}` and
`${camunda.trustStorePassword}`. A `JAVA_TOOL_OPTIONS` you declare keeps its options after the truststore's.

`SSL_CERT_FILE`, `REQUESTS_CA_BUNDLE` and the Java truststore *replace* the system's authorities: a worker that also
calls an external https service needs that service's authority added on its own (for example, a bundle of its own in
the list's `environment`).

## Supported Frameworks

- .NET 8
- .NET 9
- .NET 10

## License

MIT. Camunda itself is under the Camunda License 1.0.
