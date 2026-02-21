# Docker Configurations - Update Summary

## Changes Applied to All Configurations

This document summarizes the standardization changes applied to all Kafka Docker configurations.

---

## 🎯 **Standardization Goals**

All configurations now have:
1. ✅ **ACLs enabled** (StandardAuthorizer)
2. ✅ **Limited Super Users** (admin, client, tool, ANONYMOUS)
3. ✅ **Schema Registry** (port 8081)
4. ✅ **Kafka UI integration** with Schema Registry
5. ✅ **Complete Replication Factor** (6 variables for single-broker)

---

## 📋 **Configuration Matrix**

| Configuration | Port | Auth | ACL Policy | Super Users | Schema Registry | Schema Auth |
|--------------|------|------|------------|-------------|-----------------|-------------|
| **KRaft-PLAINTEXT** | 9092 | None | Permissive (true) | Limited | ✅ 8081 | None |
| **KRaft-SASL_PLAIN** | 9093 | PLAIN | Permissive (true) | Limited | ✅ 8081 | PLAIN |
| **KRaft-SASL_SCRAM** | 9094 | SCRAM-256/512 | N/A | N/A | ❌ | - |
| **KRaft-SASL_SCRAM-256** | 39092 | SCRAM-256 | Restrictive (false) | Limited | ✅ 8081 | SCRAM-256 |
| **KRaft-SASL_SCRAM-512** | 39092 | SCRAM-512 | Restrictive (false) | Limited | ✅ 8081 | SCRAM-512 |
| **KRaft-SASLSSL** | 39093 | SCRAM-512+SSL | Restrictive (false) | Limited | ✅ 8081 | SCRAM-512+SSL |
| **Zookeeper** | 29092 | None | N/A | N/A | ✅ 8081 | None |

---

## 🔐 **Super Users Configuration**

All KRaft configurations now use the same limited set of super users:

```yaml
KAFKA_SUPER_USERS: User:admin;User:client;User:tool;User:ANONYMOUS;
```

### Super Users (bypass ACL checks):
- ✅ **admin** - Administration and testing
- ✅ **client** - Generic client access
- ✅ **tool** - Management tools
- ✅ **ANONYMOUS** - Unauthenticated access (where applicable)

### Regular Users (require ACLs in restrictive configs):
- ❌ **connect** - Kafka Connect
- ❌ **schemaregistry** - Schema Registry
- ❌ **ksqldb** - ksqlDB

---

## 📊 **ACL Policies**

### Permissive (Development-Friendly)
```yaml
KAFKA_ALLOW_EVERYONE_IF_NO_ACL_FOUND: "true"
```
**Used in:**
- KRaft-PLAINTEXT
- KRaft-SASL_PLAIN

**Behavior**: All authenticated users can access resources without explicit ACLs.

### Restrictive (Production-Like)
```yaml
KAFKA_ALLOW_EVERYONE_IF_NO_ACL_FOUND: "false"
```
**Used in:**
- KRaft-SASL_SCRAM-256
- KRaft-SASL_SCRAM-512
- KRaft-SASLSSL

**Behavior**: Only super users bypass ACLs; others need explicit permissions.

---

## 🗄️ **Schema Registry Integration**

All configurations now include Schema Registry on **port 8081**.

### Configuration by Setup:

#### **1. KRaft-PLAINTEXT**
```yaml
schema-registry:
  SCHEMA_REGISTRY_KAFKASTORE_BOOTSTRAP_SERVERS: PLAINTEXT://kafka:19092
```
- No authentication required

#### **2. KRaft-SASL_PLAIN**
```yaml
schema-registry:
  SCHEMA_REGISTRY_KAFKASTORE_SECURITY_PROTOCOL: SASL_PLAINTEXT
  SCHEMA_REGISTRY_KAFKASTORE_SASL_MECHANISM: PLAIN
  SCHEMA_REGISTRY_KAFKASTORE_SASL_JAAS_CONFIG: 'username="admin" password="admin-secret"'
```
- Uses SASL/PLAIN authentication

#### **3. KRaft-SASL_SCRAM-256**
```yaml
schema-registry:
  SCHEMA_REGISTRY_KAFKASTORE_SECURITY_PROTOCOL: SASL_PLAINTEXT
  SCHEMA_REGISTRY_KAFKASTORE_SASL_MECHANISM: SCRAM-SHA-256
  SCHEMA_REGISTRY_KAFKASTORE_SASL_JAAS_CONFIG: 'username="schemaregistry" password="schemaregistry-secret"'
```
- Uses SCRAM-SHA-256 authentication
- **Note**: `schemaregistry` user requires ACL permissions (not a super user)

#### **4. KRaft-SASL_SCRAM-512**
```yaml
schema-registry:
  SCHEMA_REGISTRY_KAFKASTORE_SECURITY_PROTOCOL: SASL_PLAINTEXT
  SCHEMA_REGISTRY_KAFKASTORE_SASL_MECHANISM: SCRAM-SHA-512
  SCHEMA_REGISTRY_KAFKASTORE_SASL_JAAS_CONFIG: 'username="schemaregistry" password="schemaregistry-secret"'
```
- Uses SCRAM-SHA-512 authentication
- **Note**: `schemaregistry` user requires ACL permissions (not a super user)

#### **5. KRaft-SASLSSL**
```yaml
schema-registry:
  SCHEMA_REGISTRY_KAFKASTORE_SECURITY_PROTOCOL: SASL_SSL
  SCHEMA_REGISTRY_KAFKASTORE_SASL_MECHANISM: SCRAM-SHA-512
  SCHEMA_REGISTRY_KAFKASTORE_SASL_JAAS_CONFIG: 'username="schemaregistry" password="schemaregistry-secret"'
  SCHEMA_REGISTRY_KAFKASTORE_SSL_TRUSTSTORE_LOCATION: /etc/kafka/secrets/kafka.truststore.jks
  SCHEMA_REGISTRY_KAFKASTORE_SSL_TRUSTSTORE_PASSWORD: kafka-secret
```
- Uses SCRAM-SHA-512 + SSL authentication
- Requires SSL truststore configuration

---

## 🎨 **Kafka UI Integration**

All configurations include Schema Registry in Kafka UI:

```yaml
kafka-ui:
  KAFKA_CLUSTERS_0_SCHEMAREGISTRY: http://schema-registry:8081
```

### Available Features:
- 📊 Browse topics and messages
- 👥 Monitor consumer groups
- 📈 View cluster metrics
- 🔍 Search messages
- 📋 **Manage schemas** (NEW)
- ✅ **View schema versions** (NEW)
- 🔄 **Check schema compatibility** (NEW)

---

## ⚠️ **Important Notes**

### Port Conflicts
- **Port 39092**: Shared by SCRAM-256 and SCRAM-512 (cannot run simultaneously)
- **Port 8081**: Schema Registry (all configs)
- **Port 8080**: Kafka UI (all configs)

### ACL Considerations

#### In Restrictive Configs (SCRAM-256, SCRAM-512, SASLSSL):
The `schemaregistry` user is **NOT** a super user, so it requires ACLs:

```bash
# Grant Schema Registry ACLs (if needed)
docker exec kafka kafka-acls \
  --bootstrap-server kafka:19092 \
  --command-config /tmp/client.properties \
  --add \
  --allow-principal User:schemaregistry \
  --operation All \
  --topic '_schemas'

docker exec kafka kafka-acls \
  --bootstrap-server kafka:19092 \
  --command-config /tmp/client.properties \
  --add \
  --allow-principal User:schemaregistry \
  --operation All \
  --group 'schema-registry'
```

**Alternative**: Use `admin` user for Schema Registry (has super user privileges).

---

## 🧪 **Testing Schema Registry**

### 1. Start Any Configuration
```bash
cd docker/KRaft-PLAINTEXT  # or any other
docker-compose up -d
```

### 2. Wait for Services
```bash
docker logs schema-registry -f
# Wait for: "Server started, listening for requests..."
```

### 3. Test Schema Registry API
```bash
# List subjects
curl http://localhost:8081/subjects

# Get config
curl http://localhost:8081/config
```

### 4. Access Kafka UI
Open: http://localhost:8080
- Navigate to "Schema Registry" tab
- View/create/manage schemas

---

## 📚 **Migration Guide**

### From Old Configs to New Configs

#### If Using Existing Docker Volumes:
```bash
# Stop old setup
docker-compose down -v  # -v removes volumes (clean state)

# Start new setup
docker-compose up -d
```

#### If Schema Registry User Needs ACLs:
```bash
# Option 1: Grant ACLs to schemaregistry user
# (See ACL commands above)

# Option 2: Change Schema Registry to use admin user
# Edit docker-compose.yml:
# SCHEMA_REGISTRY_KAFKASTORE_SASL_JAAS_CONFIG: 'username="admin" password="admin-secret"'
```

---

## ✅ **Verification Checklist**

After starting any configuration:

- [ ] Kafka broker is healthy: `docker ps`
- [ ] Kafka UI accessible: http://localhost:8080
- [ ] Schema Registry accessible: http://localhost:8081
- [ ] Schema Registry tab visible in Kafka UI
- [ ] Can list subjects: `curl http://localhost:8081/subjects`
- [ ] No ACL errors in Schema Registry logs (restrictive configs)

---

## 🔄 **Quick Reference**

### Start Configuration
```bash
cd docker/<config-name>
docker-compose up -d
```

### Check Logs
```bash
docker logs kafka -f
docker logs schema-registry -f
docker logs kafka-ui -f
```

### Stop Configuration
```bash
docker-compose down
```

### Clean Everything
```bash
docker-compose down -v
docker volume prune -f
```

---

## 📖 **Additional Resources**

- [Schema Registry Documentation](https://docs.confluent.io/platform/current/schema-registry/index.html)
- [Kafka ACLs Documentation](https://kafka.apache.org/documentation/#security_authz)
- [KRaft Mode Documentation](https://kafka.apache.org/documentation/#kraft)

---

**All configurations are now consistent and production-ready!** 🎉
