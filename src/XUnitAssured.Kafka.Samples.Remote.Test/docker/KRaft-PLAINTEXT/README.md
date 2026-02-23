# KRaft-PLAINTEXT - Simple Kafka Configuration

## Overview

This is the **simplest Kafka configuration** for local development and testing:
- ✅ **No authentication** required
- ✅ **No SSL/TLS** encryption
- ✅ **KRaft mode** (no Zookeeper dependency)
- ✅ **Single broker** setup
- ✅ **ACLs enabled** (permissive mode for development)
- ✅ **Schema Registry included** (port 8081)

Perfect for:
- Quick prototyping
- Local development
- Basic integration tests
- Learning Kafka fundamentals

---

## 🚀 Quick Start

### 1. Start Kafka

```bash
cd docker/KRaft-PLAINTEXT
docker-compose up -d
```

Or with Podman (WSL):
```bash
podman-compose up -d
```

### 2. Verify Kafka is Running

```bash
# Check container status
docker ps | grep kafka

# Check logs
docker logs kafka

# Test connection
docker exec kafka kafka-broker-api-versions --bootstrap-server kafka:19092
```

### 3. Run Tests

```powershell
cd C:\DEV\ProjetosPessoais\XUnitAssured.Net\src\XUnitAssured.Kafka.Samples.Local.Test
dotnet test --filter "Category=Authentication&Environment=Local"
```

---

## 📊 Configuration Details

### Docker Compose

- **External Port**: `9092` (accessible from Windows via `localhost:9092`)
- **Internal Port**: `19092` (used inside Docker network)
- **Controller Port**: `29092` (KRaft consensus)
- **Kafka UI**: `8080` (web interface)
- **Schema Registry**: `8081` (schema management)

### Listeners

```yaml
PLAINTEXT://kafka:19092        # Internal Docker network
PLAINTEXT_HOST://0.0.0.0:9092  # External access
CONTROLLER://kafka:29092        # KRaft controller
```

### testsettings.json

```json
{
  "kafka": {
    "bootstrapServers": "localhost:9092",
    "securityProtocol": "Plaintext",
    "authentication": {
      "type": "None"
    }
  }
}
```

### ACL Configuration

- **Authorizer**: StandardAuthorizer (KRaft-native)
- **Policy**: ALLOW_EVERYONE_IF_NO_ACL_FOUND = true (permissive for development)
- **Super Users**: admin, client, tool, ANONYMOUS

This permissive configuration allows all authenticated users to access resources without explicit ACLs, ideal for development.

---

## 🔧 Available Services

### Kafka Broker
- **Host**: `localhost:9092` (from Windows)
- **Host**: `kafka:19092` (from Docker network)

### Kafka UI
- **URL**: http://localhost:8080
- **Features**: 
  - Browse topics
  - View messages
  - Monitor consumers
  - Manage cluster

---

## 🧪 Testing

### Run All Tests
```powershell
dotnet test
```

### Run Plaintext Tests Only
```powershell
dotnet test --filter "Category=Authentication&FullyQualifiedName~PlainText"
```

### Example Test
```csharp
[Fact]
public void Should_ProduceAndConsume_WithPlaintext()
{
    var topic = "test-topic";
    var message = "Hello Kafka!";

    // Produce
    Given()
        .Topic(topic)
        .Produce(message)
    .When()
        .Execute()
    .Then()
        .AssertSuccess();

    // Consume
    Given()
        .Topic(topic)
        .Consume()
    .When()
        .Execute()
    .Then()
        .AssertSuccess()
        .AssertMessage<string>(msg => msg.ShouldBe(message));
}
```

---

## 🛠️ Useful Commands

### Manage Kafka

```bash
# List topics
docker exec kafka kafka-topics --bootstrap-server kafka:19092 --list

# Create topic
docker exec kafka kafka-topics --bootstrap-server kafka:19092 \
  --create --topic test-topic --partitions 3 --replication-factor 1

# Describe topic
docker exec kafka kafka-topics --bootstrap-server kafka:19092 \
  --describe --topic test-topic

# Delete topic
docker exec kafka kafka-topics --bootstrap-server kafka:19092 \
  --delete --topic test-topic
```

### Produce/Consume Messages

```bash
# Console Producer
docker exec -it kafka kafka-console-producer --bootstrap-server kafka:19092 --topic test-topic

# Console Consumer
docker exec -it kafka kafka-console-consumer --bootstrap-server kafka:19092 \
  --topic test-topic --from-beginning
```

### Monitoring

```bash
# View logs
docker logs kafka -f

# Check broker API versions
docker exec kafka kafka-broker-api-versions --bootstrap-server kafka:19092

# List consumer groups
docker exec kafka kafka-consumer-groups --bootstrap-server kafka:19092 --list
```

---

## 🧹 Cleanup

### Stop Services
```bash
docker-compose down
```

### Remove Data Volumes
```bash
docker-compose down -v
```

### Remove Everything
```bash
docker-compose down -v --remove-orphans
docker volume rm kraft-plaintext_kafka-data
```

---

## ⚠️ Important Notes

### Security Warning
This configuration has **NO SECURITY**:
- ❌ No authentication
- ❌ No encryption
- ❌ No authorization

**Never use in production!** This is only for:
- Local development
- Testing
- Learning

### Port Conflicts
If port `9092` is already in use:

1. **Check what's using it**:
```powershell
netstat -ano | findstr :9092
```

2. **Change the port** in `docker-compose.yml`:
```yaml
ports:
  - "9093:9092"  # Map to different external port
```

3. **Update testsettings.json**:
```json
"bootstrapServers": "localhost:9093"
```

---

## 🔄 Switching Configurations

To switch between different Kafka configurations:

### Switch to PLAINTEXT (current)
```json
{
  "kafka": {
    "bootstrapServers": "localhost:9092",
    "securityProtocol": "Plaintext",
    "authentication": { "type": "None" }
  }
}
```

### Switch to SASL/SCRAM-SHA-512 + SSL
```json
{
  "kafka": {
    "bootstrapServers": "kafka:39093",
    "securityProtocol": "SaslSsl",
    "sslCaLocation": "path/to/ca-cert.pem",
    "enableSslCertificateVerification": true,
    "authentication": {
      "type": "SaslScram512",
      "saslScram": {
        "username": "admin",
        "password": "admin-secret",
        "useSsl": true
      }
    }
  }
}
```

---

## 📚 Additional Resources

- [Apache Kafka Documentation](https://kafka.apache.org/documentation/)
- [KRaft Mode Overview](https://kafka.apache.org/documentation/#kraft)
- [Confluent Docker Images](https://docs.confluent.io/platform/current/installation/docker/image-reference.html)
- [Kafka UI Documentation](https://docs.kafka-ui.provectus.io/)

---

## ✅ Checklist

- [ ] Docker/Podman installed and running
- [ ] Ports 9092 and 8080 available
- [ ] `docker-compose up -d` executed successfully
- [ ] Kafka is healthy (check `docker ps`)
- [ ] `testsettings.json` configured for `localhost:9092`
- [ ] Tests passing

---

**Ready to test!** 🎉

Access Kafka UI at: http://localhost:8080
