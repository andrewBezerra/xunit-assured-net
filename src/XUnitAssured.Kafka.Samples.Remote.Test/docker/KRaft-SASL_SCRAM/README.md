# KRaft-SASL_SCRAM - SCRAM Authentication without SSL

## Overview

This configuration provides **SASL/SCRAM authentication** without SSL encryption:
- ✅ **SCRAM-SHA-256 and SCRAM-SHA-512** authentication
- ❌ **No SSL/TLS encryption** (traffic is not encrypted)
- ✅ **KRaft mode** (no Zookeeper dependency)
- ✅ **Single broker** setup

Perfect for:
- Testing SCRAM authentication mechanisms
- Learning SASL/SCRAM
- Development environments where encryption is not required
- More secure than PLAIN (credentials are hashed)

⚠️ **Security Warning**: While credentials are hashed (not plaintext like PLAIN), traffic is still not encrypted. Use only in trusted networks or for development.

---

## 🚀 Quick Start

### 1. Start Kafka

```bash
cd docker/KRaft-SASL_SCRAM
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

# Wait for SCRAM users to be created (check logs for "SCRAM Users Created Successfully!")
docker logs kafka | grep "SCRAM Users"
```

### 3. Run Tests

```powershell
cd C:\DEV\ProjetosPessoais\XUnitAssured.Net\src\XUnitAssured.Kafka.Samples.Local.Test
dotnet test --filter "AuthenticationType=ScramSha256"
```

---

## 📊 Configuration Details

### Docker Compose

- **Internal Port**: `9092` (SASL_PLAINTEXT with SCRAM, used by Kafka UI)
- **External Port**: `9094` (SASL_PLAINTEXT with SCRAM, accessible from Windows)
- **Controller Port**: `9095` (PLAINTEXT, KRaft consensus)
- **Kafka UI**: `8080` (web interface)

### Listeners

```yaml
INTERNAL://0.0.0.0:9092         # Docker network (SASL_PLAINTEXT/SCRAM)
EXTERNAL://0.0.0.0:9094         # External access (SASL_PLAINTEXT/SCRAM)
CONTROLLER://0.0.0.0:9095       # KRaft controller (PLAINTEXT)
```

### Credentials

Users are created automatically during startup:

| Username | Password | Mechanisms |
|----------|----------|------------|
| **admin** | admin-secret | SCRAM-SHA-256, SCRAM-SHA-512 |
| user1 | user1-password | SCRAM-SHA-256, SCRAM-SHA-512 |

### testsettings.json

```json
{
  "kafka": {
    "bootstrapServers": "localhost:9094",
    "securityProtocol": "SaslPlaintext",
    "authentication": {
      "type": "SaslScram256",
      "saslScram": {
        "username": "admin",
        "password": "admin-secret",
        "useSsl": false
      }
    }
  }
}
```

For SCRAM-SHA-512, use:
```json
{
  "authentication": {
    "type": "SaslScram512",
    "saslScram": {
      "username": "admin",
      "password": "admin-secret",
      "useSsl": false
    }
  }
}
```

---

## 🔐 SASL/SCRAM Authentication

### How it Works

1. **Client** initiates SCRAM handshake
2. **Server** challenges client with a nonce
3. **Client** computes response using password hash
4. **Server** validates response
5. **Connection** established if valid

### Advantages over PLAIN

- ✅ **Passwords are hashed**, not transmitted
- ✅ **Challenge-response** mechanism
- ✅ **Protection against replay attacks**
- ✅ **Salted hashes** (SCRAM-specific)

### SCRAM-SHA-256 vs SCRAM-SHA-512

| Feature | SCRAM-SHA-256 | SCRAM-SHA-512 |
|---------|---------------|---------------|
| **Hash Size** | 256 bits | 512 bits |
| **Security** | Strong | Stronger |
| **Performance** | Faster | Slightly slower |
| **Compatibility** | Wider | Good |

**Recommendation**: Use **SCRAM-SHA-512** for production, **SCRAM-SHA-256** for development.

---

## 🔧 Available Services

### Kafka Broker
- **Host**: `localhost:9094` (from Windows)
- **Host**: `kafka:9092` (from Docker network)
- **Authentication**: Required (admin/admin-secret)
- **Mechanisms**: SCRAM-SHA-256, SCRAM-SHA-512

### Kafka UI
- **URL**: http://localhost:8080
- **Authentication**: Pre-configured with admin/SCRAM-SHA-256
- **Features**: 
  - 📊 Browse topics and messages
  - 👥 Monitor consumer groups
  - 📈 View cluster metrics

---

## 🧪 Testing

### Run SCRAM-SHA-256 Tests
```powershell
dotnet test --filter "AuthenticationType=ScramSha256"
```

### Run SCRAM-SHA-512 Tests
```powershell
dotnet test --filter "AuthenticationType=ScramSha512"
```

### Example Test
```csharp
[Fact]
public void Should_ProduceAndConsume_WithScramSha256()
{
    var topic = "test-topic";
    var message = "Hello Kafka!";

    // Produce with SCRAM-SHA-256
    Given()
        .Topic(topic)
        .Produce(message)
    .When()
        .Execute()
    .Then()
        .AssertSuccess();

    // Consume with SCRAM-SHA-256
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

### Manage SCRAM Users

```bash
# List all SCRAM users
docker exec kafka kafka-configs \
  --bootstrap-server kafka:9092 \
  --describe --entity-type users

# Add new SCRAM-SHA-256 user
docker exec kafka kafka-configs \
  --bootstrap-server kafka:9092 \
  --alter \
  --add-config 'SCRAM-SHA-256=[password=newpass]' \
  --entity-type users \
  --entity-name newuser

# Delete SCRAM user
docker exec kafka kafka-configs \
  --bootstrap-server kafka:9092 \
  --alter \
  --delete-config 'SCRAM-SHA-256' \
  --entity-type users \
  --entity-name newuser
```

### Produce/Consume with SCRAM

```bash
# Console Producer (SCRAM-SHA-256)
docker exec -it kafka kafka-console-producer \
  --bootstrap-server kafka:9092 \
  --topic test-topic \
  --producer-property security.protocol=SASL_PLAINTEXT \
  --producer-property sasl.mechanism=SCRAM-SHA-256 \
  --producer-property 'sasl.jaas.config=org.apache.kafka.common.security.scram.ScramLoginModule required username="admin" password="admin-secret";'

# Console Consumer (SCRAM-SHA-512)
docker exec -it kafka kafka-console-consumer \
  --bootstrap-server kafka:9092 \
  --topic test-topic \
  --from-beginning \
  --consumer-property security.protocol=SASL_PLAINTEXT \
  --consumer-property sasl.mechanism=SCRAM-SHA-512 \
  --consumer-property 'sasl.jaas.config=org.apache.kafka.common.security.scram.ScramLoginModule required username="admin" password="admin-secret";'
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
This configuration provides **strong authentication but NOT encryption**:
- ✅ Hashed credentials (SCRAM)
- ✅ Challenge-response mechanism
- ❌ Does NOT encrypt traffic
- ⚠️ Messages can be intercepted

**For Production**: Use **SASL/SCRAM + SSL** (KRaft-SASLSSL configuration).

### User Creation
Users are created automatically during container startup via the `create_scram_users.sh` script. Wait ~15-20 seconds after `docker-compose up` for users to be ready.

---

## 🔄 Switching Between SCRAM Mechanisms

### From SCRAM-SHA-256 to SCRAM-SHA-512

Update `testsettings.json`:
```json
{
  "authentication": {
    "type": "SaslScram512",  // Changed from SaslScram256
    "saslScram": {
      "username": "admin",
      "password": "admin-secret",
      "useSsl": false
    }
  }
}
```

Both mechanisms use the same users (created for both).

---

## 📚 Additional Resources

- [SASL/SCRAM Authentication](https://docs.confluent.io/platform/current/kafka/authentication_sasl/authentication_sasl_scram.html)
- [RFC 5802 - SCRAM](https://tools.ietf.org/html/rfc5802)
- [Kafka Security](https://kafka.apache.org/documentation/#security_sasl_scram)

---

## ✅ Checklist

- [ ] Docker/Podman installed and running
- [ ] Ports 9094 and 8080 available
- [ ] `docker-compose up -d` executed successfully
- [ ] Kafka is healthy (check `docker ps`)
- [ ] SCRAM users created (check `docker logs kafka`)
- [ ] `testsettings.json` configured for `localhost:9094`
- [ ] Tests passing

---

**Ready to test with SCRAM authentication!** 🔐

Access Kafka UI at: http://localhost:8080
