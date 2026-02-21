# Kafka Docker Troubleshooting - Schema Registry DNS Issues

## Problem
The error `Failed to resolve 'schema-registry'` indicates that kafka-ui cannot find the schema-registry container on the network.

## Diagnostic Steps

### 1. Check if all containers are running
```bash
docker ps -a
```

Look for:
- ✅ `kafka` - should be "Up" and healthy
- ✅ `kafka-ui` - should be "Up"
- ❓ `schema-registry` - **Check if it's running or exited**

### 2. Check Docker network
```bash
docker network ls | grep kafka-network
```

Should show:
```
NETWORK ID     NAME                              DRIVER    SCOPE
xxxxx          kraft-sasl_scram-512_kafka-network   bridge    local
```

### 3. Inspect network to see containers
```bash
docker network inspect kraft-sasl_scram-512_kafka-network
```

Should list:
- kafka
- kafka-ui
- schema-registry (if running)

### 4. Check schema-registry logs
```bash
docker logs schema-registry
```

Common issues:
- **Container not starting**: Check for authentication errors with Kafka
- **SCRAM user not found**: User `schemaregistry` might not exist yet
- **Connection refused**: Kafka not ready when schema-registry started

---

## Solutions

### Solution 1: Schema Registry Authentication Issue

The schema-registry uses user `schemaregistry` which is **NOT a super user** in the ACL config.

**Option A: Change to admin user (Quick Fix)**

Edit `docker-compose.yml`:

```yaml
schema-registry:
  environment:
    SCHEMA_REGISTRY_KAFKASTORE_SASL_JAAS_CONFIG: 'org.apache.kafka.common.security.scram.ScramLoginModule required username="admin" password="admin-secret";'
    # Changed from username="schemaregistry"
```

**Option B: Grant ACLs to schemaregistry user**

```bash
# After Kafka is up, grant ACLs:
docker exec kafka kafka-acls \
  --bootstrap-server kafka:19092 \
  --command-config /tmp/client.properties \
  --add \
  --allow-principal User:schemaregistry \
  --operation All \
  --topic '_schemas' \
  --cluster

docker exec kafka kafka-acls \
  --bootstrap-server kafka:19092 \
  --command-config /tmp/client.properties \
  --add \
  --allow-principal User:schemaregistry \
  --operation All \
  --group 'schema-registry'
```

---

### Solution 2: Wait for Schema Registry to Start

Sometimes kafka-ui starts before schema-registry is ready.

**Check startup order:**

```bash
# Check when each container started
docker ps --format "table {{.Names}}\t{{.Status}}"
```

If schema-registry shows "Restarting" or "Exited", check its logs:

```bash
docker logs schema-registry --tail 50
```

---

### Solution 3: Restart with Clean State

```bash
# Stop everything
docker-compose down -v

# Remove old network
docker network prune -f

# Start fresh
docker-compose up -d

# Watch all logs
docker-compose logs -f
```

---

### Solution 4: Test DNS Resolution Manually

```bash
# From kafka-ui container, test if schema-registry is resolvable
docker exec kafka-ui nslookup schema-registry

# Expected output:
# Server:    127.0.0.11
# Address 1: 127.0.0.11
# Name:      schema-registry
# Address 1: 172.x.x.x schema-registry.kraft-sasl_scram-512_kafka-network
```

If this fails, the network configuration is wrong.

---

### Solution 5: Explicit Network Configuration

Add explicit network aliases:

```yaml
schema-registry:
  networks:
    kafka-network:
      aliases:
        - schema-registry
        - registry
```

---

## Quick Fix Configuration

Replace schema-registry section with this (uses admin user):

```yaml
schema-registry:
  image: confluentinc/cp-schema-registry:7.6.1
  container_name: schema-registry
  hostname: schema-registry
  networks:
    kafka-network:
      aliases:
        - schema-registry
  depends_on:
    kafka:
      condition: service_healthy
  ports:
    - "8081:8081"
  environment:
    SCHEMA_REGISTRY_HOST_NAME: schema-registry
    SCHEMA_REGISTRY_LISTENERS: http://0.0.0.0:8081
    SCHEMA_REGISTRY_KAFKASTORE_BOOTSTRAP_SERVERS: SASL_PLAINTEXT://kafka:19092
    SCHEMA_REGISTRY_KAFKASTORE_SECURITY_PROTOCOL: SASL_PLAINTEXT
    SCHEMA_REGISTRY_KAFKASTORE_SASL_MECHANISM: SCRAM-SHA-512
    # Use admin user (super user - bypasses ACLs)
    SCHEMA_REGISTRY_KAFKASTORE_SASL_JAAS_CONFIG: 'org.apache.kafka.common.security.scram.ScramLoginModule required username="admin" password="admin-secret";'
```

---

## Verification After Fix

```bash
# 1. Check all containers are up
docker ps

# 2. Check schema-registry health
curl http://localhost:8081/subjects

# 3. Check kafka-ui logs (should have no DNS errors)
docker logs kafka-ui | grep "schema-registry"

# 4. Access Kafka UI
# http://localhost:8080
# Navigate to Schema Registry tab - should work
```

---

## Common Causes Summary

| Issue | Cause | Solution |
|-------|-------|----------|
| Container not starting | Auth failure with Kafka | Use admin user or grant ACLs |
| DNS resolution fails | Not on same network | Add network config |
| Connection refused | Schema Registry not ready | Check logs, wait for startup |
| 403 Forbidden | ACL permission denied | Grant ACLs or use super user |

---

## Recommended Configuration

For development/testing, **use admin user** for schema-registry to avoid ACL issues:

```yaml
SCHEMA_REGISTRY_KAFKASTORE_SASL_JAAS_CONFIG: 'org.apache.kafka.common.security.scram.ScramLoginModule required username="admin" password="admin-secret";'
```

For production-like testing, **grant proper ACLs** to schemaregistry user.

---

**Next Steps:**
1. Apply Solution 1 Option A (use admin user)
2. Restart: `docker-compose down && docker-compose up -d`
3. Verify: `docker logs schema-registry -f`
4. Test: `curl http://localhost:8081/subjects`
