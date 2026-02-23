# KRaft-SASL_PLAIN - Authentication without Encryption

## Overview

This configuration provides **SASL/PLAIN authentication** without SSL encryption:
- ✅ **Username/Password authentication** (SASL/PLAIN)
- ❌ **No SSL/TLS encryption** (traffic is not encrypted)
- ✅ **KRaft mode** (no Zookeeper dependency)
- ✅ **Single broker** setup
- ✅ **ACLs enabled** (permissive mode for development)
- ✅ **Schema Registry included** (port 8081)

Perfect for:
- Testing authentication logic
- Learning SASL mechanisms
- Development environments where encryption is not required
- Simpler setup than SASL/SSL

⚠️ **Security Warning**: Credentials are transmitted in plaintext. Use only in trusted networks or for development.

---

## 🚀 Quick Start

### 1. Start Kafka

```bash
cd docker/KRaft-SASL_PLAIN
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

# Test connection (will require authentication)
docker exec kafka kafka-broker-api-versions --bootstrap-server kafka:9092 \
  --command-config /etc/kafka/kafka_jaas.conf
```

### 3. Run Tests

```powershell
cd C:\DEV\ProjetosPessoais\XUnitAssured.Net\src\XUnitAssured.Kafka.Samples.Local.Test
dotnet test --filter "Category=Authentication&AuthenticationType=SaslPlain"
```

---

## 📊 Configuration Details

### Docker Compose

- **Internal Port**: `9092` (SASL_PLAINTEXT, used by Kafka UI and Schema Registry)
- **External Port**: `9093` (SASL_PLAINTEXT, accessible from Windows)
- **Controller Port**: `9094` (PLAINTEXT, KRaft consensus)
- **Kafka UI**: `8080` (web interface)
- **Schema Registry**: `8081` (schema management)

### Listeners

```yaml
INTERNAL://0.0.0.0:9092         # Docker network (SASL_PLAINTEXT)
EXTERNAL://0.0.0.0:9093         # External access (SASL_PLAINTEXT)
CONTROLLER://0.0.0.0:9094       # KRaft controller (PLAINTEXT)
```

### Credentials

- **Username**: `admin`
- **Password**: `admin-secret`

These credentials are configured in `kafka_jaas.conf`.

### testsettings.json

```json
{
  "kafka": {
    "bootstrapServers": "localhost:9093",
    "securityProtocol": "SaslPlaintext",
    "authentication": {
      "type": "SaslPlain",
      "saslPlain": {
        "username": "admin",
        "password": "admin-secret",
        "useSsl": false
      }
    }
  }
}
```

---

## 🔐 SASL/PLAIN Authentication

### How it Works

1. Client connects to Kafka broker
2. Client sends username and password during handshake
3. Broker validates credentials against `kafka_jaas.conf`
4. Connection is established if credentials are valid

### Security Considerations

⚠️ **IMPORTANT**: 
- Credentials are transmitted as **base64-encoded plaintext**
- **Not encrypted** - can be intercepted on the network
- **Use only in**:
  - Development/testing environments
  - Trusted internal networks
  - When combined with VPN/network isolation

🔒 **For Production**: Use **SASL/PLAIN with SSL** (SaslSsl) to encrypt the connection.

---

## 🔧 Available Services

### Kafka Broker
- **Host**: `localhost:9093` (from Windows)
- **Host**: `kafka:9092` (from Docker network)
- **Authentication**: Required (admin/admin-secret)

### Kafka UI
- **URL**: http://localhost:8080
- **Authentication**: Pre-configured with admin credentials
- **Features**: 
  - 📊 Browse topics and messages
  - 👥 Monitor consumer groups
  - 📈 View cluster metrics
  - 🔍 Search messages

---

## 🧪 Testing

### Run All Authentication Tests
```powershell
dotnet test --filter "Category=Authentication"
```

### Run SASL/PLAIN Tests Only
```powershell
dotnet test --filter "AuthenticationType=SaslPlain"
```

### Example Test
```csharp
[Fact]
public void Should_ProduceAndConsume_WithSaslPlain()
{
    var topic = "test-topic";
    var message = "Hello Kafka!";

    // Produce with authentication
    Given()
        .Topic(topic)
        .Produce(message)
    .When()
        .Execute()
    .Then()
        .AssertSuccess();

    // Consume with authentication
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

### Produce Messages with Authentication

```bash
# Console Producer (requires authentication)
docker exec -it kafka kafka-console-producer \
  --bootstrap-server kafka:9092 \
  --topic test-topic \
  --producer-property security.protocol=SASL_PLAINTEXT \
  --producer-property sasl.mechanism=PLAIN \
  --producer-property 'sasl.jaas.config=org.apache.kafka.common.security.plain.PlainLoginModule required username="admin" password="admin-secret";'
```

### Consume Messages with Authentication

```bash
# Console Consumer (requires authentication)
docker exec -it kafka kafka-console-consumer \
  --bootstrap-server kafka:9092 \
  --topic test-topic \
  --from-beginning \
  --consumer-property security.protocol=SASL_PLAINTEXT \
  --consumer-property sasl.mechanism=PLAIN \
  --consumer-property 'sasl.jaas.config=org.apache.kafka.common.security.plain.PlainLoginModule required username="admin" password="admin-secret";'
```

### Manage Topics (with authentication)

```bash
# List topics
docker exec kafka kafka-topics \
  --bootstrap-server kafka:9092 \
  --command-config /etc/kafka/kafka_jaas.conf \
  --list

# Create topic
docker exec kafka kafka-topics \
  --bootstrap-server kafka:9092 \
  --command-config /etc/kafka/kafka_jaas.conf \
  --create --topic test-topic --partitions 3 --replication-factor 1

# Describe topic
docker exec kafka kafka-topics \
  --bootstrap-server kafka:9092 \
  --command-config /etc/kafka/kafka_jaas.conf \
  --describe --topic test-topic
```

---

## 🔑 JAAS Configuration

The `kafka_jaas.conf` file configures authentication:

```conf
KafkaServer {
    org.apache.kafka.common.security.plain.PlainLoginModule required
    username="admin"
    password="admin-secret"
    user_admin="admin-secret";
};

KafkaClient {
    org.apache.kafka.common.security.plain.PlainLoginModule required
    username="admin"
    password="admin-secret";
};
```

### Add More Users

Edit `kafka_jaas.conf` and add more user entries:

```conf
KafkaServer {
    org.apache.kafka.common.security.plain.PlainLoginModule required
    username="admin"
    password="admin-secret"
    user_admin="admin-secret"
    user_user1="password1"
    user_user2="password2";
};
```

Then restart Kafka:
```bash
docker-compose restart kafka
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

---

## ⚠️ Important Notes

### Security Warning
This configuration provides **authentication but NOT encryption**:
- ✅ Verifies user identity
- ❌ Does NOT encrypt traffic
- ⚠️ Credentials can be intercepted

**For Production**: Use **SASL/PLAIN + SSL** (KRaft-SASLSSL configuration).

### Port Conflicts

If port `9093` is already in use:

1. **Check what's using it**:
```powershell
netstat -ano | findstr :9093
```

2. **Change the port** in `docker-compose.yml`:
```yaml
ports:
  - "9094:9093"  # Map to different external port
```

3. **Update testsettings.json**:
```json
"bootstrapServers": "localhost:9094"
```

---

## 🔄 Switching Configurations

### Current: SASL/PLAIN (No SSL)
```json
{
  "kafka": {
    "bootstrapServers": "localhost:9093",
    "securityProtocol": "SaslPlaintext",
    "authentication": {
      "type": "SaslPlain",
      "saslPlain": {
        "username": "admin",
        "password": "admin-secret",
        "useSsl": false
      }
    }
  }
}
```

### Upgrade to: SASL/PLAIN + SSL
```json
{
  "kafka": {
    "bootstrapServers": "kafka:39093",
    "securityProtocol": "SaslSsl",
    "sslCaLocation": "path/to/ca-cert.pem",
    "enableSslCertificateVerification": true,
    "authentication": {
      "type": "SaslPlain",
      "saslPlain": {
        "username": "admin",
        "password": "admin-secret",
        "useSsl": true
      }
    }
  }
}
```

### Downgrade to: PLAINTEXT (No Auth)
```json
{
  "kafka": {
    "bootstrapServers": "localhost:9092",
    "securityProtocol": "Plaintext",
    "authentication": { "type": "None" }
  }
}
```

---

## 📚 Additional Resources

- [SASL/PLAIN Authentication](https://docs.confluent.io/platform/current/kafka/authentication_sasl/authentication_sasl_plain.html)
- [Kafka Security](https://kafka.apache.org/documentation/#security)
- [KRaft Mode](https://kafka.apache.org/documentation/#kraft)

---

## ✅ Checklist

- [ ] Docker/Podman installed and running
- [ ] Ports 9093 and 8080 available
- [ ] `docker-compose up -d` executed successfully
- [ ] Kafka is healthy (check `docker ps`)
- [ ] `testsettings.json` configured for `localhost:9093`
- [ ] Credentials configured: `admin/admin-secret`
- [ ] Tests passing

---

**Ready to test with authentication!** 🔐

Access Kafka UI at: http://localhost:8080
