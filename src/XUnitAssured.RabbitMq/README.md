# XUnitAssured.RabbitMq

RabbitMQ testing for XUnitAssured, on the official `RabbitMQ.Client`.

## Installation

```bash
dotnet add package XUnitAssured.RabbitMq
```

## A round trip

```csharp
await Given()
    .Queue("orders")
    .Publish(new { id = 42, status = "Created" })
    .ExecuteAsync();

var assertions = await Given()
    .Queue("orders")
    .Consume()
    .WithTimeout(TimeSpan.FromSeconds(5))
    .ExecuteAsync();

assertions
    .Then()
    .AssertSuccess()
    .AssertMessage<Order>(order => order.Status.ShouldBe("Created"));
```

## Configuration

The `rabbitmq` section of `testsettings.json`, beside `http`, `kafka` and `playwright`:

```json
{
  "rabbitmq": {
    "connectionUri": "amqp://guest:guest@localhost:5672/",
    "consumeTimeoutSeconds": 30,
    "clientProvidedName": "XUnitAssured"
  }
}
```

The address is a URI, not a `host:port` list: AMQP carries the user, password and virtual host in
the address itself. The virtual host is the last segment, so a URI ending in `/` means the default
one. Omitting that trailing slash is not the same as providing it, and it is the most common
mistake when the URI is written by hand.

An environment is a separate file, `testsettings.{name}.json`, as everywhere else in the
framework.

> **On Windows, prefer `127.0.0.1` to `localhost`.** Resolving `localhost` tries IPv6 first, and
> each connection pays about 57 seconds before falling back to IPv4. Measured here: three
> round-trip tests took 2m51s with `localhost` and 2s with the literal address. The default above
> keeps `localhost` because it is the convention and costs nothing on Linux.

## Running a broker locally

```bash
podman run -d --name xa-rabbitmq --pids-limit=0 -p 5672:5672 -p 15672:15672 \
    -e RABBITMQ_DEFAULT_USER=xa -e RABBITMQ_DEFAULT_PASS=xa-senha \
    docker.io/library/rabbitmq:4-management
```

The management UI is then on `http://127.0.0.1:15672`. Two things that cost time to discover:
`--pids-limit=0` is required under Podman on WSL, which otherwise fails to create the container
with a cgroup controller error; and RabbitMQ 4 refuses a transient non-exclusive queue, so a
queue declared for a test has to be `durable: true`.

The round-trip tests in this repository carry `[Trait("Requires", "Broker")]` and read the
address from `XA_RABBITMQ_URI`, so CI points them at its own service container and a local run
falls back to the address above. They are the tests that caught a regression 916 broker-free
tests had missed, which is why they run on every pull request rather than living behind a skip.

## A publish that reaches no queue is a failure

AMQP has a case Kafka does not: publishing to an exchange that matches no binding. The broker
accepts the publish and drops the message, so the step "succeeded" and nothing arrived. In a test
that is the worst possible outcome, because it is a topology mistake that passes in silence.

So publishing goes out with `mandatory` and the step fails when the broker returns the message,
naming the exchange and the routing key that matched nothing. Publisher confirmations are on, so
the await waits for the broker's answer rather than for the bytes to leave.

When publishing into the void is the point of the test, say so:

```csharp
await Given()
    .Exchange("eventos")
    .Publish("ninguém escuta")
    .WithRoutingKey("rota.sem.fila")
    .AllowingUnroutable()
    .ExecuteAsync();
```

## Where the verbs live

Every verb except the entry one is a member of `IRabbitMqScenario`, not an extension on
`ITestScenario`. That is deliberate and it is the convention the root README documents: messaging
is a shared vocabulary, so `Consume`, `Publish`, `WithTimeout` and `ValidateMessage` are words any
broker wants. Declared as extensions on the untyped scenario in two packages, every one of those
calls becomes ambiguous the moment a test project references both, and referencing several
packages in one scenario is the point of this framework.

`Queue` and `Exchange` are the entry verbs and the only ones taking `ITestScenario`. They do not
collide with Kafka's `Topic`, so both chains can live in the same file.

## What the async client changes

`RabbitMQ.Client` 7 is asynchronous end to end, so the steps await real I/O. The consume step asks
the broker with `BasicGetAsync` and gives the thread back between attempts, every 25ms. That is a
choice here rather than a workaround: on the Kafka side the same shape exists because
`IConsumer` has no asynchronous consume at all.

A consumed message is acknowledged as soon as it arrives. A test that read the message does not
want it back on the queue contaminating the next one, and deferring the acknowledgement until
after the assertion would make the discard depend on the assertion passing.

## Supported Frameworks

- .NET 8
- .NET 9
- .NET 10

## Links

- [Repository](https://github.com/andrewBezerra/XUnitAssured.Net)
- [Upgrade guide](https://github.com/andrewBezerra/xunit-assured-net/blob/main/UPGRADING.md)

## License

Apache-2.0
