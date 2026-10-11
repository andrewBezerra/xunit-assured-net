# XUnitAssured.Kafka

Apache Kafka integration testing extensions for the [XUnitAssured.Net](https://github.com/andrewBezerra/XUnitAssured.Net) framework. Write expressive Kafka tests using a fluent `Given().When().Then()` DSL with full support for produce/consume operations, batch messaging, authentication (SASL, SSL, mTLS), and Schema Registry.

## Installation

```bash
dotnet add package XUnitAssured.Kafka
```

## Quick Start

```csharp
using XUnitAssured.Kafka.Extensions;
using XUnitAssured.Kafka.Testing;

public class OrderTests : KafkaTestBase<KafkaClassFixture>, IClassFixture<KafkaClassFixture>
{
    public OrderTests(KafkaClassFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Produce_And_Consume_Order()
    {
        var topic = GenerateUniqueTopic("orders");
        var groupId = $"test-{Guid.NewGuid():N}";

        // Produce
        await Given()
            .Topic(topic)
            .Produce(new { id = 1, product = "Laptop", price = 999.99m })
            .ExecuteAsync()
            .AssertSuccess();

        // Consume
        await Given()
            .Topic(topic)
            .Consume()
            .WithGroupId(groupId)
            .ExecuteAsync()
            .AssertSuccess()
            .AssertMessage<string>(msg => msg.ShouldNotBeNullOrEmpty());
    }
}
```

The assertions chain right after `ExecuteAsync()`, and the chain is awaited once, at the start.
Await it: without `await` the step still runs, but a failed assertion is never seen. `.When()`
and `.Then()` still read the same, and are optional. The synchronous `Execute()` keeps working; it
holds the thread until the broker answers.

The methods with an `out` parameter, such as `Extract(out var result)`, cannot be asynchronous:
use them on the builder, after the `await` — `(await ...ExecuteAsync()).Extract(out var result)` —
or end the chain with `GetResult()`, which returns the result.

## Fluent DSL Reference

### Produce

```csharp
Given()
    .Topic("my-topic")
    .Produce("Hello, Kafka!")                          // Produce string message
    .Produce(new { id = 1, name = "Test" })            // Produce JSON object
    .WithKey("my-key")                                 // Set message key
    .WithHeader("correlation-id", "abc-123")           // Add message header
    .WithBootstrapServers("localhost:9092")             // Override bootstrap servers
```

### Produce Batch

```csharp
Given()
    .Topic("my-topic")
    .ProduceBatch(new[] { "msg1", "msg2", "msg3" })    // Produce multiple messages
```

### Consume

```csharp
Given()
    .Topic("my-topic")
    .Consume()                                         // Consume single message
    .WithGroupId("my-group")                           // Set consumer group
    .WithTimeout(30)                                   // Set timeout in seconds
    .WithBootstrapServers("localhost:9092")             // Override bootstrap servers
```

### Consume Batch

```csharp
Given()
    .Topic("my-topic")
    .ConsumeBatch(5)                                   // Consume up to 5 messages
    .WithGroupId("my-group")
```

### Assertions

```csharp
await Given().Topic("my-topic").Consume().ExecuteAsync()
    .AssertSuccess()                                   // Assert operation succeeded
    .AssertMessage<string>(msg => msg.ShouldBe("expected"))  // Assert message content
    .AssertBatchCount(5)                               // Assert batch message count
```

## Authentication

### SASL/PLAIN

```csharp
await Given()
    .Topic("my-topic")
    .Produce("message")
    .WithBootstrapServers("localhost:29093")
    .WithAuth(auth => auth.UseSaslPlain("user", "password", useSsl: false))
    .ExecuteAsync()
    .AssertSuccess();
```

### SASL/SCRAM-SHA-256

```csharp
await Given()
    .Topic("my-topic")
    .Produce("message")
    .WithBootstrapServers("localhost:29094")
    .WithAuth(auth => auth.UseSaslScram256("user", "password"))
    .ExecuteAsync()
    .AssertSuccess();
```

### SASL/SCRAM-SHA-512

```csharp
await Given()
    .Topic("my-topic")
    .Produce("message")
    .WithBootstrapServers("localhost:29095")
    .WithAuth(auth => auth.UseSaslScram512("user", "password"))
    .ExecuteAsync()
    .AssertSuccess();
```

### SSL (One-Way)

```csharp
await Given()
    .Topic("my-topic")
    .Produce("message")
    .WithBootstrapServers("localhost:29096")
    .WithAuth(auth => auth.UseSsl("certs/ca-cert.pem"))
    .ExecuteAsync()
    .AssertSuccess();
```

### Mutual TLS (mTLS)

```csharp
await Given()
    .Topic("my-topic")
    .Produce("message")
    .WithBootstrapServers("localhost:29097")
    .WithAuth(auth => auth.UseMutualTls("client-cert.pem", "client-key.pem", "ca-cert.pem"))
    .ExecuteAsync()
    .AssertSuccess();
```

### Automatic Authentication via testsettings.json

```json
{
  "kafka": {
    "bootstrapServers": "localhost:9092",
    "authentication": {
      "type": "SaslPlain",
      "sasl": {
        "username": "user",
        "password": "password"
      }
    }
  }
}
```

## Batch Operations

```csharp
// Produce batch
var messages = new[] { "order-1", "order-2", "order-3", "order-4", "order-5" };

await Given()
    .Topic("orders")
    .ProduceBatch(messages)
    .ExecuteAsync()
    .AssertSuccess()
    .AssertBatchCount(5);

// Consume batch
await Given()
    .Topic("orders")
    .ConsumeBatch(5)
    .WithGroupId("batch-consumer")
    .ExecuteAsync()
    .AssertSuccess()
    .AssertBatchCount(5);
```

## Message Keys and Headers

```csharp
// Produce with key
await Given()
    .Topic("user-events")
    .Produce(new { action = "login", userId = 42 })
    .WithKey("user-42")
    .ExecuteAsync()
    .AssertSuccess();

// Produce with headers
await Given()
    .Topic("user-events")
    .Produce("event-data")
    .WithHeader("correlation-id", "abc-123")
    .WithHeader("source", "test-suite")
    .ExecuteAsync()
    .AssertSuccess();
```

## Round-Trip Example (Produce → Consume)

```csharp
[Fact]
public async Task RoundTrip_ProduceAndConsume()
{
    var topic = GenerateUniqueTopic("roundtrip");
    var groupId = $"test-{Guid.NewGuid():N}";

    // Produce
    await Given()
        .Topic(topic)
        .Produce("Hello, Kafka!")
        .ExecuteAsync()
        .AssertSuccess();

    // Consume and validate
    await Given()
        .Topic(topic)
        .Consume()
        .WithGroupId(groupId)
        .ExecuteAsync()
        .AssertSuccess()
        .AssertMessage<string>(msg => msg.ShouldBe("Hello, Kafka!"));
}
```

## One sharp edge: `Validate` with an untyped lambda

In a test project that references **both** this package and `XUnitAssured.Http`, a lambda with
no parameter type binds to the HTTP overload, because that one accepts any result. The chain is
then typed for HTTP, and the failure only shows up at the end, when the assertions are read:

```csharp
// Binds to the Http overload, and types the chain for HTTP
Given(kafka).Topic("orders").Consume().Validate(r => r.Success.ShouldBeTrue())

// Names the type, and stays a Kafka chain
Given(kafka).Topic("orders").Consume().Validate((KafkaStepResult r) => r.Success.ShouldBeTrue())
```

`ValidateMessage<T>`, `ValidateOffset`, `ValidatePartition` and the rest are unaffected: no other
package declares them, so there is nothing to compete with. Only the bare name `Validate` is
shared.

The error message names both result types and suggests the typed form, so a run that hits this
says what to change. Moving the protocol-agnostic overload out of the HTTP package would remove
the ambiguity altogether, and that is a breaking change held for the next major.

## Supported Frameworks

- .NET 8
- .NET 9
- .NET 10
- .NET 10

## Dependencies

- [XUnitAssured.Core](https://www.nuget.org/packages/XUnitAssured.Core) — DSL infrastructure and abstractions
- [Confluent.Kafka](https://www.nuget.org/packages/Confluent.Kafka) — Apache Kafka .NET client
- [Confluent.SchemaRegistry](https://www.nuget.org/packages/Confluent.SchemaRegistry) — Schema Registry client
- [Confluent.SchemaRegistry.Serdes.Avro](https://www.nuget.org/packages/Confluent.SchemaRegistry.Serdes.Avro) — Avro serialization

## Links

- [GitHub Repository](https://github.com/andrewBezerra/XUnitAssured.Net)
- [Full Documentation](https://github.com/andrewBezerra/XUnitAssured.Net#readme)
- [Report Issues](https://github.com/andrewBezerra/XUnitAssured.Net/issues)
- [NuGet Package](https://www.nuget.org/packages/XUnitAssured.Kafka)

## License

Apache-2.0 — see [LICENSE.md](https://github.com/andrewBezerra/XUnitAssured.Net/blob/main/LICENSE.md)
