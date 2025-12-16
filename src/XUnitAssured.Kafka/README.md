# XUnitAssured.Kafka

Kafka testing utilities for XUnitAssured using Confluent.Kafka. Provides settings, fixtures, and helpers for integration testing with Apache Kafka.

## Installation

```bash
dotnet add package XUnitAssured.Kafka
```

## Features

- **KafkaSettings**: Strongly-typed configuration using Confluent.Kafka native types
- **KafkaTestFixture**: Base test fixture for Kafka integration tests
- **KafkaProducerHelper**: Helper methods for producing test messages
- **KafkaConsumerHelper**: Helper methods for consuming test messages
- **Schema Registry Support**: Built-in support for Avro serialization with Schema Registry
- **Validation**: Automatic settings validation using IValidateOptions pattern

## Configuration

Add Kafka settings to your `appsettings.json`:

```json
{
  "Kafka": {
    "BootstrapServers": "localhost:9092",
    "GroupId": "test-consumer-group",
    "SecurityProtocol": "Plaintext",
    "SchemaRegistry": {
      "Url": "https://schema-registry:8081"
    }
  }
}
```

## Usage

Create a test fixture by inheriting from `KafkaTestFixture`:

```csharp
using XUnitAssured.Kafka;
using Xunit;

public class MyKafkaTestFixture : KafkaTestFixture
{
    public MyKafkaTestFixture() : base()
    {
    }
}

public class MyKafkaTests : IClassFixture<MyKafkaTestFixture>
{
    private readonly MyKafkaTestFixture _fixture;

    public MyKafkaTests(MyKafkaTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Should_Produce_And_Consume_Message()
    {
        // Produce
        await _fixture.ProduceAsync("test-topic", "key", "value");

        // Consume
        var result = _fixture.ConsumeOne<string, string>("test-topic");
        
        Assert.NotNull(result);
        Assert.Equal("value", result.Message.Value);
    }
}
```

## Documentation

For more information, visit the [GitHub repository](https://github.com/andrewBezerra/xunit-assured-net).

## License

This project is licensed under the MIT License - see the LICENSE file for details.
