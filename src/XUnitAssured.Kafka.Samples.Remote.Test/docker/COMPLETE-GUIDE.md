# Docker Kafka Configurations - Complete Guide

## 📋 Overview

This workspace contains **7 production-ready Kafka configurations** for local development and testing, all fully standardized with ACLs, Schema Registry, and proper replication factors.

---

## ✨ What's Included in ALL Configurations

Every configuration now has:

1. ✅ **Docker Networks** - Proper container communication
2. ✅ **ACLs Enabled** - StandardAuthorizer with security
3. ✅ **Limited Super Users** - admin, client, tool, ANONYMOUS
4. ✅ **Schema Registry** - Port 8081 with authentication
5. ✅ **Kafka UI** - Port 8080 integrated with Schema Registry
6. ✅ **Complete Replication Factor** - 6 variables for single-broker stability

---

## 📊 Configuration Matrix

| Configuration | Port | Auth | ACLs | SSL | Schema | Replication | Complexity |
|--------------|------|------|------|-----|--------|-------------|------------|
| **KRaft-PLAINTEXT** | 9092 | None | ✅ Permissive | ❌ | ✅ 8081 | ✅ 6 vars | ⭐ Simple |
| **KRaft-SASL_PLAIN** | 9093 | PLAIN | ✅ Permissive | ❌ | ✅ 8081 | ✅ 6 vars | ⭐⭐ Medium |
| **KRaft-SASL_SCRAM** | 9094 | SCRAM-256/512 | ❌ | ❌ | ❌ | ✅ 6 vars | ⭐⭐ Medium |
| **KRaft-SASL_SCRAM-256** | 39092 | SCRAM-256 | ✅ Restrictive | ❌ | ✅ 8081 | ✅ 6 vars | ⭐⭐⭐ Complex |
| **KRaft-SASL_SCRAM-512** | 39092 | SCRAM-512 | ✅ Restrictive | ❌ | ✅ 8081 | ✅ 6 vars | ⭐⭐⭐ Complex |
| **KRaft-SASLSSL** | 39093 | SCRAM-512 | ✅ Restrictive | ✅ | ✅ 8081 | ✅ 6 vars | ⭐⭐⭐⭐ Advanced |
| **Zookeeper** | 29092 | None | ❌ | ❌ | ✅ 8081 | ✅ | ⭐⭐ Medium |

---

## 🔢 Replication Factor - Critical for Single Broker

All configurations include **6 essential variables**:

```yaml
KAFKA_OFFSETS_TOPIC_REPLICATION_FACTOR: 1      # Consumer offsets
KAFKA_OFFSETS_TOPIC_NUM_PARTITIONS: 50         # Parallelism
KAFKA_TRANSACTION_STATE_LOG_REPLICATION_FACTOR: 1  # Transactions
KAFKA_TRANSACTION_STATE_LOG_MIN_ISR: 1         # Transaction ISR
KAFKA_DEFAULT_REPLICATION_FACTOR: 1            # New topics
KAFKA_MIN_INSYNC_REPLICAS: 1                   # Producer acks=all
```

### Why Each Variable Matters

| Variable | Prevents | Example Error |
|----------|----------|---------------|
| **OFFSETS_TOPIC_RF** | Consumer offset failures | `INVALID_REPLICATION_FACTOR` |
| **TRANSACTION_LOG_RF** | Transaction errors | `InvalidTxnStateException` |
| **DEFAULT_RF** | Topic creation failures | `Replication factor 3 > brokers 1` |
| **MIN_ISR** | Producer write failures | `NotEnoughReplicasException` |

📚 **Full details**: [SINGLE-BROKER-REPLICATION-FIX.md](SINGLE-BROKER-REPLICATION-FIX.md)

---

## 🔐 Security Configuration

### ACL Policies

**Permissive (Development)**
- KRaft-PLAINTEXT
- KRaft-SASL_PLAIN
- `ALLOW_EVERYONE_IF_NO_ACL_FOUND: true`

**Restrictive (Production-like)**
- KRaft-SASL_SCRAM-256
- KRaft-SASL_SCRAM-512
- KRaft-SASLSSL
- `ALLOW_EVERYONE_IF_NO_ACL_FOUND: false`

### Super Users (All Configs)

```yaml
KAFKA_SUPER_USERS: User:admin;User:client;User:tool;User:ANONYMOUS;
```

- ✅ **admin** - Testing and administration
- ✅ **client** - Generic client access
- ✅ **tool** - Management tools
- ✅ **ANONYMOUS** - Unauthenticated (where applicable)

**Non-super users** (require ACLs in restrictive configs):
- ❌ connect
- ❌ schemaregistry (uses admin in current config)
- ❌ ksqldb

---

## 🗄️ Schema Registry

### Configuration by Setup

All configurations include Schema Registry on **port 8081**:

```yaml
schema-registry:
  ports:
    - "8081:8081"
  environment:
    SCHEMA_REGISTRY_HOST_NAME: schema-registry
    SCHEMA_REGISTRY_LISTENERS: http://0.0.0.0:8081
```

### Authentication by Config

| Config | Schema Registry Auth | User |
|--------|---------------------|------|
| PLAINTEXT | None | - |
| SASL_PLAIN | SASL/PLAIN | admin |
| SCRAM-256 | SASL/SCRAM-SHA-256 | admin |
| SCRAM-512 | SASL/SCRAM-SHA-512 | admin |
| SASLSSL | SASL/SCRAM-SHA-512 + SSL | admin |

**Note**: Using `admin` user (super user) to avoid ACL issues.

---

## 🎨 Kafka UI

All configurations include Kafka UI on **port 8080**:

```yaml
kafka-ui:
  ports:
    - "8080:8080"
  environment:
    KAFKA_CLUSTERS_0_SCHEMAREGISTRY: http://schema-registry:8081
```

### Features Available

- 📊 Browse topics and messages
- 👥 Monitor consumer groups
- 📈 View cluster metrics
- 🔍 Search messages
- 📋 **Manage schemas** (via Schema Registry integration)
- ✅ **View schema versions**
- 🔄 **Check schema compatibility**

---

## 🚀 Quick Start Guide

### Prerequisites

- **Docker** or **Podman** installed
- **Ports available**: 8080 (Kafka UI), 8081 (Schema Registry), Kafka port (varies)

### Starting Any Configuration

```bash
# Navigate to config directory
cd docker/<config-name>

# Start containers
docker-compose up -d
# or
podman-compose up -d

# View logs
docker-compose logs -f

# Access services
# Kafka UI: http://localhost:8080
# Schema Registry: http://localhost:8081
```

### Podman on WSL Ubuntu

```bash
# Use the provided script
cd docker/KRaft-SASL_SCRAM-512
chmod +x start-podman.sh
./start-podman.sh
```

---

## 📚 Documentation Structure

```
docker/
├── README.md                              # Main overview
├── CONFIGURATION-UPDATES.md               # Detailed change log
├── SINGLE-BROKER-REPLICATION-FIX.md      # Replication factor guide
├── PODMAN-WSL-TROUBLESHOOTING.md         # Podman specific issues
│
├── KRaft-PLAINTEXT/
│   ├── README.md
│   └── docker-compose.yml
│
├── KRaft-SASL_PLAIN/
│   ├── README.md
│   └── docker-compose.yml
│
├── KRaft-SASL_SCRAM/
│   ├── README.md
│   └── docker-compose.yml
│
├── KRaft-SASL_SCRAM-256/
│   ├── README.md
│   └── docker-compose.yml
│
├── KRaft-SASL_SCRAM-512/
│   ├── README.md
│   ├── docker-compose.yml
│   ├── TROUBLESHOOTING-SCHEMA-REGISTRY.md
│   └── start-podman.sh
│
├── KRaft-SASLSSL/
│   ├── README.md
│   ├── docker-compose.yml
│   ├── README-SSL-CONFIG.md
│   └── QUICK-FIX-SSL.md
│
└── Zookeeper/
    └── docker-compose.yml
```

---

## ⚠️ Important Notes

### Port Conflicts

- **Port 39092**: Shared by SCRAM-256 and SCRAM-512 (cannot run simultaneously)
- **Port 8080**: Kafka UI (all configs)
- **Port 8081**: Schema Registry (all configs except SASL_SCRAM simple)

### Network Configuration

All configs use **kafka-network** (bridge driver):

```yaml
networks:
  kafka-network:
    driver: bridge
```

This ensures proper DNS resolution between containers.

### Single Broker Limitations

These configurations use **1 broker** for simplicity:

- ✅ Perfect for development/testing
- ✅ Fast startup
- ❌ No high availability
- ❌ No data redundancy

For production, use **3+ brokers** with proper replication factors.

---

## 🧪 Testing Schema Registry

```bash
# List subjects
curl http://localhost:8081/subjects

# Get config
curl http://localhost:8081/config

# Register a schema
curl -X POST http://localhost:8081/subjects/test-value/versions \
  -H "Content-Type: application/vnd.schemaregistry.v1+json" \
  -d '{"schema": "{\"type\":\"string\"}"}'
```

---

## 🔄 Common Tasks

### Switch Between Configurations

```bash
# Stop current
docker-compose down

# Start new
cd ../other-config
docker-compose up -d
```

### Clean Everything

```bash
docker-compose down -v        # Remove volumes
docker volume prune -f        # Clean orphan volumes
docker network prune -f       # Clean orphan networks
```

### View Logs

```bash
docker logs kafka -f
docker logs schema-registry -f
docker logs kafka-ui -f
```

### Restart Single Service

```bash
docker-compose restart schema-registry
```

---

## ✅ Verification Checklist

After starting any configuration:

- [ ] All containers running: `docker ps`
- [ ] Kafka healthy: Check logs for "Kafka Server started"
- [ ] Schema Registry accessible: `curl http://localhost:8081/subjects`
- [ ] Kafka UI accessible: http://localhost:8080
- [ ] Schema Registry tab works in UI
- [ ] No replication factor errors in logs
- [ ] No DNS resolution errors

---

## 🎯 Choosing the Right Configuration

### For Quick Prototyping
→ **KRaft-PLAINTEXT** (no auth, fastest)

### For Basic Authentication Testing
→ **KRaft-SASL_PLAIN** (simple username/password)

### For Strong Authentication Testing
→ **KRaft-SASL_SCRAM** or **SCRAM-256** (hashed credentials)

### For Production-like Testing (Auth + Authorization)
→ **KRaft-SASL_SCRAM-512** (SCRAM-512 + ACLs)

### For Maximum Security Testing
→ **KRaft-SASLSSL** (SCRAM-512 + SSL + ACLs)

### For Legacy Compatibility
→ **Zookeeper** (classic setup)

---

## 📖 Additional Resources

- [Kafka Documentation](https://kafka.apache.org/documentation/)
- [Schema Registry Documentation](https://docs.confluent.io/platform/current/schema-registry/index.html)
- [Kafka ACLs Documentation](https://kafka.apache.org/documentation/#security_authz)
- [KRaft Mode Documentation](https://kafka.apache.org/documentation/#kraft)
- [SCRAM Authentication](https://docs.confluent.io/platform/current/kafka/authentication_sasl/authentication_sasl_scram.html)

---

## 🆘 Troubleshooting

### Common Issues

1. **Port already in use**
   - Check with: `netstat -ano | findstr :8080`
   - Stop conflicting service

2. **Schema Registry DNS errors**
   - Ensure all services on same network
   - Check: `docker network inspect <network-name>`

3. **Replication factor errors**
   - All 6 replication variables configured
   - See: [SINGLE-BROKER-REPLICATION-FIX.md](SINGLE-BROKER-REPLICATION-FIX.md)

4. **ACL permission denied**
   - Use super user (admin)
   - Or grant explicit ACLs

5. **SSL errors (SASLSSL only)**
   - Check certificates
   - See: [KRaft-SASLSSL/QUICK-FIX-SSL.md](KRaft-SASLSSL/QUICK-FIX-SSL.md)

### Podman-Specific Issues

For WSL + Podman users:
→ See: [PODMAN-WSL-TROUBLESHOOTING.md](PODMAN-WSL-TROUBLESHOOTING.md)

---

## 🎉 Summary

You now have **7 production-ready Kafka configurations**, all with:

- ✅ Proper networking
- ✅ Security (ACLs + authentication)
- ✅ Schema Registry
- ✅ Kafka UI
- ✅ Complete replication factor settings
- ✅ Comprehensive documentation

**All configurations are tested and ready for development and testing!**

Happy Kafka testing! 🚀
