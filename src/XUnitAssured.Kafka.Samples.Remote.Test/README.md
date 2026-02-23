# XUnitAssured.Kafka.Samples.Local.Test

Integration tests for Kafka using XUnitAssured.Kafka framework against a **local Docker Kafka instance**.

## 📋 Prerequisites

1. **Docker & Docker Compose** installed
2. **Kafka running locally** via Docker Compose
3. **.NET 9 SDK** installed

## 🚀 Quick Start

### 1. Start Kafka (Docker Compose)

Ensure your Kafka Docker Compose is running:

```bash
docker-compose up -d
```

**Expected services:**
- Kafka Broker: `localhost:29092` (external access)
- Kafka Internal: `kafka:9092` (internal Docker network)
- Zookeeper: `localhost:2181`
- Kafka UI: `http://localhost:8083`
- Schema Registry: `http://localhost:8081`

### 2. Verify Kafka is Running

**Option 1: Check Docker containers**
```bash
docker ps | grep kafka

# Expected output:
# kafka           confluentinc/cp-kafka:7.6.1           Running
# zookeeper       confluentinc/cp-zookeeper:7.6.1       Running
# schema-registry confluentinc/cp-schema-registry:7.6.1 Running
# kafka-ui        provectuslabs/kafka-ui:latest         Running
```

**Option 2: Check Kafka UI**
- Open http://localhost:8083
- You should see the `local` cluster
- Verify broker is connected

**Option 3: Test connection with console**
```bash
# List topics
docker exec -it kafka kafka-topics --list --bootstrap-server localhost:9092

# Create test topic (optional - auto-created by tests)
docker exec -it kafka kafka-topics --create \
  --bootstrap-server localhost:9092 \
  --topic xunit-test-topic \
  --partitions 1 \
  --replication-factor 1
```

### 3. Run Tests

```bash
# Navigate to project directory
cd XUnitAssured.Kafka.Samples.Local.Test

# Run all tests
dotnet test

# Run specific test
dotnet test --filter "DisplayName~simple string"

# Run with verbose output
dotnet test --logger "console;verbosity=detailed"
```

## 🔧 Troubleshooting

### ❌ Kafka Connection Failed

**Error:**
```
Failed to connect to Kafka at localhost:29092
```

**Solutions:**

1. **Verify Kafka is running:**
   ```bash
   docker ps | grep kafka
   ```

2. **Check Kafka logs:**
   ```bash
   docker logs kafka
   ```

3. **Restart Kafka:**
   ```bash
   docker-compose restart kafka
   ```

4. **Verify port 29092 is exposed:**
   ```bash
   # Windows
   netstat -an | findstr "29092"
   
   # Linux/Mac
   netstat -an | grep 29092
   ```

5. **Test connection manually:**
   ```bash
   # From inside container (should work)
   docker exec -it kafka kafka-broker-api-versions --bootstrap-server localhost:9092
   
   # From host machine (should work)
   docker exec -it kafka kafka-broker-api-versions --bootstrap-server localhost:29092
   ```

### ❌ Port Already in Use

**Error:**
```
Bind for 0.0.0.0:29092 failed: port is already allocated
```

**Solutions:**
1. Stop other Kafka instances
2. Change port in docker-compose.yml
3. Update testsettings.json with new port

### ❌ Topics Not Auto-Created

**Error:**
```
Topic 'xunit-test-topic' not found
```

**Solution - Enable auto-creation in Kafka:**
```bash
# Check current setting
docker exec -it kafka kafka-configs --bootstrap-server localhost:9092 \
  --describe --entity-type brokers --entity-name 0

# Create topic manually
docker exec -it kafka kafka-topics --create \
  --bootstrap-server localhost:9092 \
  --topic xunit-test-topic \
  --partitions 1 \
  --replication-factor 1
```

### ❌ Consumer Timeout

**Error:**
```
Consumer timed out waiting for messages
```

**Solutions:**
1. Increase timeout in testsettings.json
2. Check if producer succeeded
3. Verify consumer is reading from correct offset
4. Use Kafka UI to check messages: http://localhost:8083

### 🐛 Enable Connection Test in Fixture

By default, connection validation is **disabled** to prevent fixture initialization errors.

To enable it, uncomment this line in `KafkaSamplesLocalFixture.cs`:

```csharp
public KafkaSamplesLocalFixture()
{
    // ...
    
    // Uncomment to enable connection validation
    TestKafkaConnection();  // ← Uncomment this
}
```

## ⚙️ Configuration

Edit `testsettings.json`:

```json
{
  "testMode": "Local",
  "kafka": {
    "bootstrapServers": "localhost:29092",
    "groupId": "xunit-test-consumer-group"
  }
}
```

## 📊 Viewing Messages

### Using Kafka UI (Recommended)
1. Open http://localhost:8083
2. Select cluster: `local`
3. Navigate to **Topics**
4. Click on test topic (e.g., `xunit-test-topic`)
5. View messages in **Messages** tab

### Using Command Line
```bash
# Consume messages from beginning
docker exec -it kafka kafka-console-consumer \
  --bootstrap-server localhost:9092 \
  --topic xunit-test-topic \
  --from-beginning

# Produce test message
docker exec -it kafka kafka-console-producer \
  --bootstrap-server localhost:9092 \
  --topic xunit-test-topic
# Type message and press Enter
```

## 🧹 Cleanup

### Clean Test Topics

```bash
# List topics
docker exec -it kafka kafka-topics --list --bootstrap-server localhost:9092

# Delete specific topic
docker exec -it kafka kafka-topics --delete \
  --bootstrap-server localhost:9092 \
  --topic xunit-test-topic
```

### Stop Kafka

```bash
# Stop containers (data preserved)
docker-compose stop

# Stop and remove containers (data preserved in volumes)
docker-compose down

# Stop and REMOVE ALL DATA
docker-compose down -v
```

## 📝 Notes

- **Port 29092**: External access from host machine (used by tests)
- **Port 9092**: Internal Docker network access
- **Auto-topic creation**: Enabled by default in local Kafka
- **Replication factor**: 1 (single broker)

## 🤝 Contributing

When adding new tests:
1. Use unique topic names or message IDs
2. Clean up resources in test teardown
3. Handle timeout gracefully
4. Add descriptive test names

## 📚 Additional Resources

- [Confluent Kafka Documentation](https://docs.confluent.io/)
- [XUnitAssured.Kafka Documentation](../../docs/kafka.md)
- [Docker Compose Reference](https://docs.docker.com/compose/)


## 📂 Project Structure

```
XUnitAssured.Kafka.Samples.Local.Test/
├── testsettings.json                    # Kafka configuration
├── KafkaSamplesLocalFixture.cs          # Test fixture (setup/teardown)
├── KafkaSamplesLocalTestBase.cs         # Base class for tests
├── ProducerConsumerBasicTests.cs        # Integration tests
└── README.md                            # This file
```

## 🧪 Test Categories

### ProducerConsumerBasicTests
Integration tests that produce and consume messages in the same test to validate end-to-end flow.

| Test | Description |
|------|-------------|
| `Example01` | Produce and consume simple string message |
| `Example02` | Produce and consume JSON object |
| `Example03` | Produce and consume with headers |
| `Example04` | Produce multiple messages (batch) |
| `Example05` | Consume from empty topic (timeout) |
| `Example06` | Produce message with key |

## ⚙️ Configuration

Configuration is loaded from `testsettings.json`:

```json
{
  "testMode": "Local",
  "environment": "docker-local",
  "kafka": {
    "brokers": ["localhost:29092"],
    "topics": {
      "test": "xunit-test-topic"
    },
    "groupId": "xunit-test-consumer-group",
    "timeout": 10
  }
}
```

### Environment Variables

You can override settings using environment variables:

```bash
# Set custom Kafka broker
export KAFKA_BROKERS="localhost:29092"

# Set custom group ID
export KAFKA_GROUP_ID="my-test-group"
```

## 🎯 Test Pattern

Tests follow the **Given-When-Then** DSL pattern:

```csharp
// PRODUCE
await Given()
    .Topic("my-topic")
    .Produce(myMessage)
.When()
    .Execute()
.Then()
    .AssertProduceSucceeded();

// CONSUME
await Given()
    .Topic("my-topic")
    .Consume()
.When()
    .Execute()
.Then()
    .Validate(result =>
    {
        result.Success.ShouldBeTrue();
        result.GetMessage<string>().ShouldBe("expected");
    });
```

## 🔧 Troubleshooting

### Kafka Connection Failed

**Error:**
```
Failed to connect to Kafka at localhost:29092
```

**Solutions:**
1. Ensure Docker is running: `docker ps`
2. Check Kafka container: `docker ps | grep kafka`
3. Restart Kafka: `docker-compose restart kafka`
4. Check logs: `docker logs kafka`

### Topics Not Found

**Error:**
```
Topic 'xunit-test-topic' not found
```

**Solutions:**
1. Topics are auto-created by default
2. Check Kafka UI: http://localhost:8083
3. Manually create topic:
```bash
docker exec -it kafka kafka-topics --create \
  --bootstrap-server localhost:9092 \
  --topic xunit-test-topic \
  --partitions 1 \
  --replication-factor 1
```

### Consumer Timeout

**Error:**
```
Consumer timed out waiting for messages
```

**Solutions:**
1. Increase timeout in testsettings.json
2. Check if producer succeeded
3. Verify consumer is reading from correct offset
4. Check Kafka UI for messages in topic

### Port Already in Use

**Error:**
```
Bind for 0.0.0.0:29092 failed: port is already allocated
```

**Solutions:**
1. Stop other Kafka instances
2. Change port in docker-compose.yml
3. Update testsettings.json with new port

## 📊 Viewing Messages

Use Kafka UI to inspect messages:

1. Open http://localhost:8083
2. Select cluster: `local`
3. Navigate to **Topics**
4. Click on test topic (e.g., `xunit-test-topic`)
5. View messages in **Messages** tab

## 🧹 Cleanup

### Clean Test Topics

```bash
# List topics
docker exec -it kafka kafka-topics --list --bootstrap-server localhost:9092

# Delete test topics
docker exec -it kafka kafka-topics --delete \
  --bootstrap-server localhost:9092 \
  --topic xunit-test-topic
```

### Stop Kafka

```bash
docker-compose down

# With volume cleanup
docker-compose down -v
```

## 📚 Additional Resources

- [XUnitAssured.Kafka Documentation](../../docs/kafka.md)
- [Confluent Kafka Documentation](https://docs.confluent.io/platform/current/overview.html)
- [xUnit Documentation](https://xunit.net/)
- [Docker Compose Reference](https://docs.docker.com/compose/)

## 🤝 Contributing

When adding new tests:
1. Follow the Given-When-Then pattern
2. Use descriptive test names
3. Add comments explaining test purpose
4. Ensure tests are isolated (use unique topics/keys)
5. Clean up resources in test teardown

## 📝 License

Same as parent project.
