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
    public void Produce_And_Consume_Order()
    {
        var topic = GenerateUniqueTopic("orders");
        var groupId = $"test-{Guid.NewGuid():N}";

        // Produce
        Given()
            .Topic(topic)
            .Produce(new { id = 1, product = "Laptop", price = 999.99m })
        .When()
            .Execute()
        .Then()
            .AssertSuccess();

        // Consume
        Given()
            .Topic(topic)
            .Consume()
            .WithGroupId(groupId)
        .When()
            .Execute()
        .Then()
            .AssertSuccess()
            .AssertMessage<string>(msg => msg.ShouldNotBeNullOrEmpty());
    }
}
```

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
.When()
    .Execute()
.Then()
    .AssertSuccess()                                   // Assert operation succeeded
    .AssertMessage<string>(msg => msg.ShouldBe("expected"))  // Assert message content
    .AssertBatchCount(5)                               // Assert batch message count
```

## Authentication

### SASL/PLAIN

```csharp
Given()
    .Topic("my-topic")
    .Produce("message")
    .WithBootstrapServers("localhost:29093")
    .WithAuth(auth => auth.UseSaslPlain("user", "password", useSsl: false))
.When().Execute()
.Then().AssertSuccess();
```

### SASL/SCRAM-SHA-256

```csharp
Given()
    .Topic("my-topic")
    .Produce("message")
    .WithBootstrapServers("localhost:29094")
    .WithAuth(auth => auth.UseSaslScram256("user", "password"))
.When().Execute()
.Then().AssertSuccess();
```

### SASL/SCRAM-SHA-512

```csharp
Given()
    .Topic("my-topic")
    .Produce("message")
    .WithBootstrapServers("localhost:29095")
    .WithAuth(auth => auth.UseSaslScram512("user", "password"))
.When().Execute()
.Then().AssertSuccess();
```

### SSL (One-Way)

```csharp
Given()
    .Topic("my-topic")
    .Produce("message")
    .WithBootstrapServers("localhost:29096")
    .WithAuth(auth => auth.UseSsl("certs/ca-cert.pem"))
.When().Execute()
.Then().AssertSuccess();
```

### Mutual TLS (mTLS)

```csharp
Given()
    .Topic("my-topic")
    .Produce("message")
    .WithBootstrapServers("localhost:29097")
    .WithAuth(auth => auth.UseMutualTls("client-cert.pem", "client-key.pem", "ca-cert.pem"))
.When().Execute()
.Then().AssertSuccess();
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

Given()
    .Topic("orders")
    .ProduceBatch(messages)
.When()
    .Execute()
.Then()
    .AssertSuccess()
    .AssertBatchCount(5);

// Consume batch
Given()
    .Topic("orders")
    .ConsumeBatch(5)
    .WithGroupId("batch-consumer")
.When()
    .Execute()
.Then()
    .AssertSuccess()
    .AssertBatchCount(5);
```

## Message Keys and Headers

```csharp
// Produce with key
Given()
    .Topic("user-events")
    .Produce(new { action = "login", userId = 42 })
    .WithKey("user-42")
.When().Execute()
.Then().AssertSuccess();

// Produce with headers
Given()
    .Topic("user-events")
    .Produce("event-data")
    .WithHeader("correlation-id", "abc-123")
    .WithHeader("source", "test-suite")
.When().Execute()
.Then().AssertSuccess();
```

## Round-Trip Example (Produce → Consume)

```csharp
[Fact]
public void RoundTrip_ProduceAndConsume()
{
    var topic = GenerateUniqueTopic("roundtrip");
    var groupId = $"test-{Guid.NewGuid():N}";

    // Produce
    Given()
        .Topic(topic)
        .Produce("Hello, Kafka!")
    .When().Execute()
    .Then().AssertSuccess();

    // Consume and validate
    Given()
        .Topic(topic)
        .Consume()
        .WithGroupId(groupId)
    .When().Execute()
    .Then()
        .AssertSuccess()
        .AssertMessage<string>(msg => msg.ShouldBe("Hello, Kafka!"));
}
```

## Supported Frameworks

- .NET 7
- .NET 8
- .NET 9
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

MIT — see [LICENSE.md](https://github.com/andrewBezerra/XUnitAssured.Net/blob/main/LICENSE.md)
