# KRaft-SASL_SCRAM-256 - Production-like SCRAM Authentication

## Overview

This configuration provides **SASL/SCRAM-SHA-256 authentication with ACLs** without SSL encryption:
- ✅ **SCRAM-SHA-256** authentication (production-grade hashing)
- ✅ **ACL-based authorization** (StandardAuthorizer)
- ✅ **Multiple pre-configured users** (admin, connect, schemaregistry, tool)
- ❌ **No SSL/TLS encryption** (traffic is not encrypted)
- ✅ **KRaft mode** (no Zookeeper dependency)
- ✅ **Single broker** setup with production-like configuration

Perfect for:
- Testing SCRAM-SHA-256 authentication with ACLs
- Simulating production authorization patterns
- Testing Kafka Connect, Schema Registry integration
- Learning Kafka security best practices
- Development with realistic security setup

⚠️ **Security Warning**: While using strong authentication and authorization, traffic is still not encrypted. Use only in trusted networks or for development.

---

## 🚀 Quick Start

### 1. Start Kafka

```bash
cd docker/KRaft-SASL_SCRAM-256
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

# Check logs (wait for SCRAM users creation)
docker logs kafka -f

# Wait for message: "Kafka is healthy and ready for clients"
```

### 3. Verify SCRAM Users Created

```bash
# List SCRAM-SHA-256 users
docker exec kafka kafka-configs \
  --bootstrap-server kafka:19092 \
  --command-config /tmp/client.properties \
  --describe --entity-type users
```

### 4. Run Tests

```powershell
cd C:\DEV\ProjetosPessoais\XUnitAssured.Net\src\XUnitAssured.Kafka.Samples.Local.Test
dotnet test --filter "AuthenticationType=ScramSha256"
```

---

## 📊 Configuration Details

### Docker Compose

- **External Port**: `39092` (SASL_PLAINTEXT with SCRAM-SHA-256)
- **Internal Port**: `19092` (SASL_PLAINTEXT with SCRAM-SHA-256)
- **Controller Port**: `29092` (PLAINTEXT, KRaft consensus)
- **Kafka UI**: `8080` (web interface)

### Listeners

```yaml
BROKER://kafka:19092           # Internal Docker network (SASL_PLAINTEXT/SCRAM)
EXTERNAL://0.0.0.0:39092       # External access (SASL_PLAINTEXT/SCRAM)
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

### testsettings.json

```json
{
  "kafka": {
    "bootstrapServers": "localhost:39092",
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

---

## 🔐 SASL/SCRAM-SHA-256 + ACLs

### Authentication

- **Mechanism**: SCRAM-SHA-256
- **Hashing**: SHA-256 with salt
- **Challenge-Response**: Yes (prevents replay attacks)
- **Password Storage**: Hashed in Kafka's metadata

### Authorization (ACLs)

- **Authorizer**: `StandardAuthorizer` (KRaft-native)
- **Default Policy**: `ALLOW_EVERYONE_IF_NO_ACL_FOUND = false` (secure by default)
- **Super Users**: admin, client, tool, ANONYMOUS

### Super Users

Super users bypass ACL checks. Configured via:
```yaml
KAFKA_SUPER_USERS: User:admin;User:client;User:tool;User:ANONYMOUS;
```

---

## 🔧 Available Services

### Kafka Broker
- **Host**: `localhost:39092` (from Windows)
- **Host**: `kafka:19092` (from Docker network)
- **Authentication**: Required (admin/admin-secret)
- **Authorization**: ACLs enabled

### Kafka UI
- **URL**: http://localhost:8080
- **Authentication**: Pre-configured with admin/SCRAM-SHA-256
- **Features**: 
  - 📊 Browse topics and messages
  - 👥 Monitor consumer groups
  - 📈 View cluster metrics
  - 🔐 View ACLs (if UI supports)

---

## 🧪 Testing

### Run SCRAM-SHA-256 Tests
```powershell
dotnet test --filter "AuthenticationType=ScramSha256"
```

### Example Test
```csharp
[Fact]
public void Should_ProduceAndConsume_WithScramSha256()
{
    var topic = "test-topic";
    var message = "Hello Kafka!";

    // Produce with SCRAM-SHA-256 authentication
    Given()
        .Topic(topic)
        .Produce(message)
    .When()
        .Execute()
    .Then()
        .AssertSuccess();

    // Consume with SCRAM-SHA-256 authentication
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
# List all SCRAM-SHA-256 users
docker exec kafka kafka-configs \
  --bootstrap-server kafka:19092 \
  --command-config /tmp/client.properties \
  --describe --entity-type users

# Add new SCRAM-SHA-256 user
docker exec kafka kafka-configs \
  --bootstrap-server kafka:19092 \
  --command-config /tmp/client.properties \
  --alter \
  --add-config 'SCRAM-SHA-256=[password=newpass]' \
  --entity-type users \
  --entity-name newuser

# Delete SCRAM user
docker exec kafka kafka-configs \
  --bootstrap-server kafka:19092 \
  --command-config /tmp/client.properties \
  --alter \
  --delete-config 'SCRAM-SHA-256' \
  --entity-type users \
  --entity-name newuser
```

### Manage ACLs

```bash
# List all ACLs
docker exec kafka kafka-acls \
  --bootstrap-server kafka:19092 \
  --command-config /tmp/client.properties \
  --list

# Grant read access to topic
docker exec kafka kafka-acls \
  --bootstrap-server kafka:19092 \
  --command-config /tmp/client.properties \
  --add \
  --allow-principal User:newuser \
  --operation Read \
  --topic test-topic

# Grant write access to topic
docker exec kafka kafka-acls \
  --bootstrap-server kafka:19092 \
  --command-config /tmp/client.properties \
  --add \
  --allow-principal User:newuser \
  --operation Write \
  --topic test-topic

# Grant consumer group access
docker exec kafka kafka-acls \
  --bootstrap-server kafka:19092 \
  --command-config /tmp/client.properties \
  --add \
  --allow-principal User:newuser \
  --operation Read \
  --group test-group

# Remove ACL
docker exec kafka kafka-acls \
  --bootstrap-server kafka:19092 \
  --command-config /tmp/client.properties \
  --remove \
  --allow-principal User:newuser \
  --operation Read \
  --topic test-topic
```

### Manage Topics (with authentication)

```bash
# Create client.properties file for authentication
cat > /tmp/client.properties << EOF
security.protocol=SASL_PLAINTEXT
sasl.mechanism=SCRAM-SHA-256
sasl.jaas.config=org.apache.kafka.common.security.scram.ScramLoginModule required username="admin" password="admin-secret";
EOF

# List topics
docker exec kafka kafka-topics \
  --bootstrap-server kafka:19092 \
  --command-config /tmp/client.properties \
  --list

# Create topic
docker exec kafka kafka-topics \
  --bootstrap-server kafka:19092 \
  --command-config /tmp/client.properties \
  --create --topic test-topic \
  --partitions 3 --replication-factor 1

# Describe topic
docker exec kafka kafka-topics \
  --bootstrap-server kafka:19092 \
  --command-config /tmp/client.properties \
  --describe --topic test-topic
```

### Produce/Consume with SCRAM

```bash
# Console Producer (SCRAM-SHA-256)
docker exec -it kafka kafka-console-producer \
  --bootstrap-server kafka:19092 \
  --topic test-topic \
  --producer-property security.protocol=SASL_PLAINTEXT \
  --producer-property sasl.mechanism=SCRAM-SHA-256 \
  --producer-property 'sasl.jaas.config=org.apache.kafka.common.security.scram.ScramLoginModule required username="admin" password="admin-secret";'

# Console Consumer (SCRAM-SHA-256)
docker exec -it kafka kafka-console-consumer \
  --bootstrap-server kafka:19092 \
  --topic test-topic \
  --from-beginning \
  --consumer-property security.protocol=SASL_PLAINTEXT \
  --consumer-property sasl.mechanism=SCRAM-SHA-256 \
  --consumer-property 'sasl.jaas.config=org.apache.kafka.common.security.scram.ScramLoginModule required username="admin" password="admin-secret";'
```

---

## 🔑 Client Configuration

### /tmp/client.properties

This file is automatically created during container startup for command-line tools:

```properties
security.protocol=SASL_PLAINTEXT
sasl.mechanism=SCRAM-SHA-256
sasl.jaas.config=org.apache.kafka.common.security.scram.ScramLoginModule required \
  username="admin" \
  password="admin-secret";
```

### Application Configuration (.NET)

```csharp
var config = new ProducerConfig
{
    BootstrapServers = "localhost:39092",
    SecurityProtocol = SecurityProtocol.SaslPlaintext,
    SaslMechanism = SaslMechanism.ScramSha256,
    SaslUsername = "admin",
    SaslPassword = "admin-secret"
};
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
docker volume rm kraft-sasl_scram-256_kafka1-data
```

---

## ⚠️ Important Notes

### Security Configuration

This setup demonstrates **production-like security patterns**:

✅ **What's Included**:
- Strong authentication (SCRAM-SHA-256)
- Authorization with ACLs
- Multiple service accounts
- Super users for administration
- Secure by default (ACLs required)

❌ **What's NOT Included**:
- SSL/TLS encryption (traffic is plaintext)
- Certificate-based authentication
- Mutual TLS (mTLS)

### ACL Default Policy

With `KAFKA_ALLOW_EVERYONE_IF_NO_ACL_FOUND: "false"`:
- **New topics** require explicit ACLs
- **Non-super users** must be granted permissions
- **Super users** bypass all ACL checks

This is **more secure** but requires **explicit permission management**.

### Port Differences

⚠️ This configuration uses **port 39092**, different from:
- `KRaft-PLAINTEXT`: 9092
- `KRaft-SASL_PLAIN`: 9093
- `KRaft-SASL_SCRAM`: 9094
- `KRaft-SASLSSL`: 39093

Update your `testsettings.json` accordingly!

---

## 🔄 Switching Configurations

### To Test Different Users

Update `testsettings.json`:
```json
{
  "authentication": {
    "type": "SaslScram256",
    "saslScram": {
      "username": "connect",           // Changed from "admin"
      "password": "connect-secret",    // Changed password
      "useSsl": false
    }
  }
}
```

### To Enable SSL (Upgrade to KRaft-SASLSSL)

1. Stop this configuration: `docker-compose down`
2. Switch to KRaft-SASLSSL: `cd ../KRaft-SASLSSL`
3. Start with SSL: `docker-compose up -d`
4. Update `testsettings.json`:
```json
{
  "bootstrapServers": "kafka:39093",
  "securityProtocol": "SaslSsl",
  "sslCaLocation": "path/to/ca-cert.pem",
  "enableSslCertificateVerification": true
}
```

---

## 📚 Additional Resources

- [SASL/SCRAM Authentication](https://docs.confluent.io/platform/current/kafka/authentication_sasl/authentication_sasl_scram.html)
- [Kafka Authorization (ACLs)](https://kafka.apache.org/documentation/#security_authz)
- [StandardAuthorizer in KRaft](https://kafka.apache.org/documentation/#security_authz_kraft)
- [RFC 5802 - SCRAM Specification](https://tools.ietf.org/html/rfc5802)

---

## 🎯 Production Readiness

### What Makes This Configuration Production-Like?

1. ✅ **SCRAM-SHA-256** - Industry-standard authentication
2. ✅ **ACLs Enabled** - Authorization layer
3. ✅ **Multiple Service Accounts** - Separation of concerns
4. ✅ **Super Users** - Administrative access control
5. ✅ **Secure by Default** - Explicit permissions required

### What's Missing for Production?

1. ❌ **SSL/TLS Encryption** - Add with KRaft-SASLSSL
2. ❌ **High Availability** - Add more brokers
3. ❌ **External CA Certificates** - Use trusted CA
4. ❌ **Monitoring & Alerting** - Add Prometheus/Grafana
5. ❌ **Backup & Recovery** - Implement DR strategy

---

## 📋 Troubleshooting

### Issue: "Not authorized to access topics"

**Cause**: ACLs are enabled and user doesn't have permissions

**Solution**: Grant ACLs or use super user (admin)
```bash
docker exec kafka kafka-acls \
  --bootstrap-server kafka:19092 \
  --command-config /tmp/client.properties \
  --add \
  --allow-principal User:myuser \
  --operation All \
  --topic '*'
```

### Issue: "Authentication failed"

**Cause**: Wrong username/password or SCRAM user not created

**Solution**: Verify users exist
```bash
docker exec kafka kafka-configs \
  --bootstrap-server kafka:19092 \
  --command-config /tmp/client.properties \
  --describe --entity-type users
```

### Issue: "Connection refused on port 39092"

**Cause**: Kafka not fully started or port conflict

**Solution**: Check logs and verify port availability
```bash
docker logs kafka -f
netstat -ano | findstr :39092
```

---

## ✅ Checklist

- [ ] Docker/Podman installed and running
- [ ] Port 39092 and 8080 available
- [ ] `docker-compose up -d` executed successfully
- [ ] Kafka is healthy (check `docker ps`)
- [ ] SCRAM users created (check logs)
- [ ] `testsettings.json` configured for `localhost:39092`
- [ ] Super users configured: admin, client, tool, ANONYMOUS
- [ ] Tests passing

---

**Ready to test production-like SCRAM-SHA-256 with ACLs!** 🔐  
Realistic security setup for Kafka development and testing.

Access Kafka UI at: http://localhost:8080
