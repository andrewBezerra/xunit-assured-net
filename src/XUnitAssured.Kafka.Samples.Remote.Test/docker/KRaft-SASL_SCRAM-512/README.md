# KRaft-SASL_SCRAM-512 - Maximum Security SCRAM Authentication

## Overview

This configuration provides **SASL/SCRAM-SHA-512 authentication** without SSL encryption:
- ✅ **SCRAM-SHA-512** authentication (strongest hash algorithm)
- ✅ **512-bit SHA hashing** (more secure than SHA-256)
- ✅ **ACL-based authorization** (StandardAuthorizer)
- ✅ **Permissive ACL policy** (ALLOW_EVERYONE_IF_NO_ACL_FOUND = true)
- ❌ **No SSL/TLS encryption** (traffic is not encrypted)
- ✅ **KRaft mode** (no Zookeeper dependency)
- ✅ **Single broker** setup

Perfect for:
- Testing SCRAM-SHA-512 (strongest SCRAM variant)
- Learning advanced SCRAM mechanisms
- Development with maximum authentication security
- Comparing SHA-256 vs SHA-512 performance

⚠️ **Port Conflict**: Uses the same port (39092) as KRaft-SASL_SCRAM-256. Only one can run at a time.

⚠️ **Security Warning**: While using the strongest authentication, traffic is still not encrypted. Use only in trusted networks or for development.

---

## 🚀 Quick Start

### 1. Stop Any Conflicting Kafka

Since this uses port 39092, stop KRaft-SASL_SCRAM-256 if running:

```bash
cd ../KRaft-SASL_SCRAM-256
docker-compose down
```

### 2. Start Kafka

```bash
cd docker/KRaft-SASL_SCRAM-512
docker-compose up -d
```

Or with Podman (WSL):
```bash
podman-compose up -d
```

### 3. Verify Kafka is Running

```bash
# Check container status
docker ps | grep kafka

# Check logs (wait for SCRAM users creation)
docker logs kafka -f

# Wait for message: "Kafka is healthy"
```

### 4. Verify SCRAM-SHA-512 Users Created

```bash
# List SCRAM-SHA-512 users
docker exec kafka kafka-configs \
  --bootstrap-server kafka:19092 \
  --describe --entity-type users
```

### 5. Run Tests

```powershell
cd C:\DEV\ProjetosPessoais\XUnitAssured.Net\src\XUnitAssured.Kafka.Samples.Local.Test
dotnet test --filter "AuthenticationType=ScramSha512"
```

---

## 📊 Configuration Details

### Docker Compose

- **External Port**: `39092` (SASL_PLAINTEXT with SCRAM-SHA-512) ⚠️ **Shared with SCRAM-256**
- **Internal Port**: `19092` (SASL_PLAINTEXT with SCRAM-SHA-512)
- **Controller Port**: `29092` (PLAINTEXT, KRaft consensus)
- **Kafka UI**: `8080` (web interface)

### Listeners

```yaml
BROKER://kafka:19092           # Internal Docker network (SASL_PLAINTEXT/SCRAM-512)
EXTERNAL://0.0.0.0:39092       # External access (SASL_PLAINTEXT/SCRAM-512)
CONTROLLER://kafka:29092       # KRaft controller (PLAINTEXT)
```

### Credentials

Users are created automatically during startup via `update_scram_run.sh`:

| Username | Password | Purpose | Super User |
|----------|----------|---------|------------|
| **admin** | admin-secret | Administration and testing | ✅ |
| connect | connect-secret | Kafka Connect | ❌ |
| schemaregistry | schemaregistry-secret | Schema Registry | ❌ |
| ksqldb | ksqldb-secret | ksqlDB | ❌ |
| tool | tool-secret | Management tools | ✅ |
| client | - | Generic client | ✅ |
| ANONYMOUS | - | Unauthenticated access | ✅ |

### testsettings.json

```json
{
  "kafka": {
    "bootstrapServers": "localhost:39092",
    "securityProtocol": "SaslPlaintext",
    "authentication": {
      "type": "SaslScram512",
      "saslScram": {
        "username": "admin",
        "password": "admin-secret",
        "useSsl": false
      }
    }
  }
}
```

---

## 🔐 SASL/SCRAM-SHA-512

### What is SCRAM-SHA-512?

- **Algorithm**: SHA-512 (512-bit hash)
- **Security**: Stronger than SHA-256
- **Performance**: Slightly slower than SHA-256
- **Use Case**: Maximum authentication security

### SHA-256 vs SHA-512

| Feature | SHA-256 | SHA-512 |
|---------|---------|---------|
| **Hash Size** | 256 bits | 512 bits |
| **Security** | Strong | Stronger |
| **Collision Resistance** | Very High | Higher |
| **Performance** | Faster | Slightly slower |
| **Recommendation** | General use | Maximum security |

### Authorization (ACLs)

- **Authorizer**: `StandardAuthorizer` (KRaft-native)
- **Default Policy**: `ALLOW_EVERYONE_IF_NO_ACL_FOUND = false` (secure by default)
- **Super Users**: admin, client, tool, ANONYMOUS (limited set)

This configuration is **more secure** and **production-like**, requiring explicit ACLs for non-super users.

---

## 🔧 Available Services

### Kafka Broker
- **Host**: `localhost:39092` (from Windows)
- **Host**: `kafka:19092` (from Docker network)
- **Authentication**: Required (admin/admin-secret)
- **Mechanism**: SCRAM-SHA-512

### Kafka UI
- **URL**: http://localhost:8080
- **Authentication**: Pre-configured with admin/SCRAM-SHA-512
- **Features**: 
  - 📊 Browse topics and messages
  - 👥 Monitor consumer groups
  - 📈 View cluster metrics

---

## 🧪 Testing

### Run SCRAM-SHA-512 Tests
```powershell
dotnet test --filter "AuthenticationType=ScramSha512"
```

### Example Test
```csharp
[Fact]
public void Should_ProduceAndConsume_WithScramSha512()
{
    var topic = "test-topic";
    var message = "Hello Kafka!";

    // Produce with SCRAM-SHA-512 authentication
    Given()
        .Topic(topic)
        .Produce(message)
    .When()
        .Execute()
    .Then()
        .AssertSuccess();

    // Consume with SCRAM-SHA-512 authentication
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

### Manage SCRAM-SHA-512 Users

```bash
# List all SCRAM-SHA-512 users
docker exec kafka kafka-configs \
  --bootstrap-server kafka:19092 \
  --describe --entity-type users

# Add new SCRAM-SHA-512 user
docker exec kafka kafka-configs \
  --bootstrap-server kafka:19092 \
  --alter \
  --add-config 'SCRAM-SHA-512=[password=newpass]' \
  --entity-type users \
  --entity-name newuser

# Delete SCRAM user
docker exec kafka kafka-configs \
  --bootstrap-server kafka:19092 \
  --alter \
  --delete-config 'SCRAM-SHA-512' \
  --entity-type users \
  --entity-name newuser
```

### Produce/Consume with SCRAM-SHA-512

```bash
# Console Producer (SCRAM-SHA-512)
docker exec -it kafka kafka-console-producer \
  --bootstrap-server kafka:19092 \
  --topic test-topic \
  --producer-property security.protocol=SASL_PLAINTEXT \
  --producer-property sasl.mechanism=SCRAM-SHA-512 \
  --producer-property 'sasl.jaas.config=org.apache.kafka.common.security.scram.ScramLoginModule required username="admin" password="admin-secret";'

# Console Consumer (SCRAM-SHA-512)
docker exec -it kafka kafka-console-consumer \
  --bootstrap-server kafka:19092 \
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

### Port Conflict with SCRAM-256

⚠️ **Both KRaft-SASL_SCRAM-256 and KRaft-SASL_SCRAM-512 use port 39092!**

This means:
- ❌ **Cannot run both simultaneously**
- ✅ **Must stop one before starting the other**
- ✅ **Same `testsettings.json` port** (just change `type`)

**To switch between them:**
```bash
# Stop SCRAM-256
cd docker/KRaft-SASL_SCRAM-256
docker-compose down

# Start SCRAM-512
cd ../KRaft-SASL_SCRAM-512
docker-compose up -d

# Update testsettings.json
# Change: "type": "SaslScram256" → "type": "SaslScram512"
```

### ACL Policy Difference

Both SCRAM-256 and SCRAM-512 now use the **same secure policy**:

| Config | Policy | Effect |
|--------|--------|--------|
| **SCRAM-256** | ALLOW_EVERYONE_IF_NO_ACL_FOUND = false | Secure by default, requires ACLs |
| **SCRAM-512** | ALLOW_EVERYONE_IF_NO_ACL_FOUND = false | Secure by default, requires ACLs |

Both configurations are now **equally secure** with explicit ACL requirements.

### Super Users

Super users bypass ACL checks. Configured via:
```yaml
KAFKA_SUPER_USERS: User:admin;User:client;User:tool;User:ANONYMOUS;
```

**Limited set of super users** for better security. Non-super users (connect, schemaregistry, ksqldb) require explicit ACL permissions.

---

## 🔄 Switching Between Configurations

### From SCRAM-256 to SCRAM-512

1. **Stop SCRAM-256**:
```bash
cd docker/KRaft-SASL_SCRAM-256
docker-compose down
```

2. **Start SCRAM-512**:
```bash
cd ../KRaft-SASL_SCRAM-512
docker-compose up -d
```

3. **Update testsettings.json**:
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

4. **Run tests**:
```powershell
dotnet test --filter "AuthenticationType=ScramSha512"
```

### To Enable SSL (Upgrade to KRaft-SASLSSL)

The **KRaft-SASLSSL** configuration provides SCRAM-SHA-512 + SSL:

1. Stop this configuration: `docker-compose down`
2. Switch to KRaft-SASLSSL: `cd ../KRaft-SASLSSL`
3. Follow the [SASLSSL setup guide](../KRaft-SASLSSL/README.md)

---

## 📊 Comparison: SCRAM-256 vs SCRAM-512

| Aspect | SCRAM-256 | SCRAM-512 (This) |
|--------|-----------|------------------|
| **Port** | 39092 | 39092 (conflict!) |
| **Hash Algorithm** | SHA-256 | SHA-512 |
| **Hash Size** | 256 bits | 512 bits |
| **Security** | Strong | Stronger |
| **Performance** | Faster | Slightly slower |
| **ACL Policy** | Secure (false) | Secure (false) |
| **Super Users** | Limited | Limited |
| **Use Case** | Realistic auth + authz | Maximum auth security |

---

## 📚 Additional Resources

- [SASL/SCRAM Authentication](https://docs.confluent.io/platform/current/kafka/authentication_sasl/authentication_sasl_scram.html)
- [SHA-512 Specification](https://en.wikipedia.org/wiki/SHA-2)
- [RFC 5802 - SCRAM](https://tools.ietf.org/html/rfc5802)
- [Kafka Authorization](https://kafka.apache.org/documentation/#security_authz)

---

## 🎯 When to Use SCRAM-512

### Use **SCRAM-SHA-512** when:
- 🔒 You need **maximum authentication security**
- 📈 Performance difference is negligible for your use case
- ✅ You want to test **SHA-512 specifically**
- 🧪 Learning different SCRAM mechanisms
- 🔐 Compliance requires SHA-512

### Use **SCRAM-SHA-256** when:
- ⚡ **Performance** is critical
- ✅ SHA-256 security is sufficient (it is for most cases)
- 🎯 You need **realistic ACL testing** (secure by default)
- 📊 Industry standard (more widely used)

### Recommendation
For **production**, **SCRAM-SHA-256 is recommended** by most experts as it provides:
- ✅ Excellent security
- ✅ Better performance
- ✅ Wider compatibility

Use **SCRAM-SHA-512** when:
- Regulations require it
- Maximum security is paramount
- Performance impact is negligible

---

## 📋 Troubleshooting

### Issue: "Port 39092 already in use"

**Cause**: KRaft-SASL_SCRAM-256 is still running

**Solution**: Stop SCRAM-256 first
```bash
cd ../KRaft-SASL_SCRAM-256
docker-compose down
cd ../KRaft-SASL_SCRAM-512
docker-compose up -d
```

### Issue: "Authentication failed"

**Cause**: Wrong SCRAM mechanism in testsettings.json

**Solution**: Ensure `type` is `SaslScram512` (not `SaslScram256`)
```json
{
  "authentication": {
    "type": "SaslScram512"
  }
}
```

### Issue: "Test still uses SCRAM-256"

**Cause**: testsettings.json not updated

**Solution**: Clear build cache and rebuild
```powershell
dotnet clean
dotnet build
```

---

## ✅ Checklist

- [ ] Docker/Podman installed and running
- [ ] Port 39092 available (SCRAM-256 stopped)
- [ ] `docker-compose up -d` executed successfully
- [ ] Kafka is healthy (check `docker ps`)
- [ ] SCRAM-SHA-512 users created (check logs)
- [ ] `testsettings.json` configured with `SaslScram512`
- [ ] Tests passing

---

**Ready to test with SCRAM-SHA-512 - Maximum authentication security!** 🔐

Access Kafka UI at: http://localhost:8080

**Note**: Remember to stop KRaft-SASL_SCRAM-256 before starting this configuration (port conflict on 39092).
