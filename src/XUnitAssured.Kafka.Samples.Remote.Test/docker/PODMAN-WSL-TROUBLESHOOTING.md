# Podman + WSL Ubuntu - Schema Registry Troubleshooting

## Environment
- **Container Runtime**: Podman
- **OS**: WSL Ubuntu
- **Issue**: Schema Registry DNS resolution fails in kafka-ui

---

## Diagnostic Commands (Run in WSL Ubuntu)

### 1. Check Running Containers
```bash
podman ps -a
```

Expected containers:
- ✅ kafka (Up, Healthy)
- ✅ kafka-ui (Up)
- ❓ schema-registry (Check if Up or Exited)

### 2. Check Pod Network
```bash
# Podman creates a pod for docker-compose services
podman pod ls

# Inspect the pod
podman pod inspect <pod-name>
```

### 3. Check Network
```bash
# List networks
podman network ls

# Inspect kafka network
podman network inspect kraft-sasl_scram-512_kafka-network
```

### 4. Check Schema Registry Logs
```bash
podman logs schema-registry
```

Common errors:
- `Authentication failed` - SCRAM user issue
- `Connection refused` - Kafka not ready
- `Authorization failed` - ACL issue

---

## Podman-Specific Issues

### Issue 1: Podman DNS Resolution

Podman uses **CNI plugins** for networking. Sometimes DNS doesn't work properly between containers.

**Check DNS configuration:**
```bash
# From kafka-ui container
podman exec kafka-ui cat /etc/resolv.conf

# Test DNS resolution
podman exec kafka-ui nslookup schema-registry
podman exec kafka-ui ping -c 3 schema-registry
```

**If DNS fails, check CNI config:**
```bash
cat /etc/cni/net.d/*.conflist
```

---

### Issue 2: Podman Compose vs Docker Compose

If using `podman-compose`, there might be compatibility issues.

**Check podman-compose version:**
```bash
podman-compose version
```

**Try using Podman's native docker-compose support:**
```bash
# Instead of podman-compose
podman compose up -d

# Or with docker-compose (if installed)
docker-compose up -d  # Podman can emulate Docker socket
```

---

### Issue 3: ACL Issues with Schema Registry

Schema Registry user `schemaregistry` is **NOT a super user** in ACL config.

**Quick Fix: Use admin user**

Edit `docker-compose.yml`:

```yaml
schema-registry:
  environment:
    # Change this line:
    SCHEMA_REGISTRY_KAFKASTORE_SASL_JAAS_CONFIG: 'org.apache.kafka.common.security.scram.ScramLoginModule required username="admin" password="admin-secret";'
    # Was: username="schemaregistry"
```

**Why?** 
- `admin` is a super user (bypasses ACLs)
- `schemaregistry` requires explicit ACL grants

---

## Solution Steps (Podman + WSL)

### Step 1: Stop Everything
```bash
cd /mnt/c/DEV/ProjetosPessoais/XUnitAssured.Net/src/XUnitAssured.Kafka.Samples.Local.Test/docker/KRaft-SASL_SCRAM-512

# Stop containers
podman-compose down
# or
podman compose down

# Remove volumes
podman volume prune -f

# Remove pod (if exists)
podman pod rm -f <pod-name>
```

### Step 2: Update Schema Registry Auth

Apply the quick fix above (use admin user).

### Step 3: Verify Network Config

Ensure `docker-compose.yml` has proper network configuration:

```yaml
networks:
  kafka-network:
    driver: bridge  # or 'pasta' for rootless Podman

services:
  kafka:
    networks:
      - kafka-network
    # ...

  kafka-ui:
    networks:
      - kafka-network
    # ...

  schema-registry:
    networks:
      - kafka-network
    # ...
```

### Step 4: Start with Verbose Logging
```bash
# Start with logs
podman-compose up -d && podman-compose logs -f

# Watch specific container
podman logs schema-registry -f
```

### Step 5: Verify DNS Resolution
```bash
# After all containers are up
podman exec kafka-ui nslookup schema-registry

# Expected output:
# Server:    10.89.0.1 (or similar)
# Address:   10.89.0.1:53
# 
# Name:      schema-registry
# Address:   10.89.0.x
```

---

## Podman Network Troubleshooting

### If DNS Still Fails

**Option A: Use IP address instead of hostname**

Find schema-registry IP:
```bash
podman inspect schema-registry | grep IPAddress
```

Update kafka-ui config:
```yaml
kafka-ui:
  environment:
    KAFKA_CLUSTERS_0_SCHEMAREGISTRY: http://10.89.0.4:8081  # Use actual IP
```

**Option B: Add explicit hostname mapping**

```yaml
kafka-ui:
  extra_hosts:
    - "schema-registry:10.89.0.4"  # Use actual schema-registry IP
```

**Option C: Use host networking (not recommended for multi-container)**

```yaml
schema-registry:
  network_mode: host
```

---

## Rootless Podman Considerations

If running rootless Podman:

### Check User Namespace
```bash
podman unshare cat /proc/self/uid_map
```

### Use Pasta Network Driver
```yaml
networks:
  kafka-network:
    driver: pasta  # Better for rootless Podman
```

### Check Port Mapping
```bash
# Rootless Podman uses slirp4netns or pasta
podman port schema-registry
```

---

## Quick Fix Script

Create `fix-schema-registry.sh`:

```bash
#!/bin/bash

cd "$(dirname "$0")"

echo "🛑 Stopping containers..."
podman-compose down

echo "🧹 Cleaning volumes..."
podman volume prune -f

echo "📝 Updating schema-registry to use admin user..."
# This assumes you've already edited docker-compose.yml

echo "🚀 Starting containers..."
podman-compose up -d

echo "⏳ Waiting for containers to be healthy..."
sleep 10

echo "🔍 Checking schema-registry status..."
podman logs schema-registry --tail 20

echo "🧪 Testing DNS resolution..."
podman exec kafka-ui nslookup schema-registry

echo "✅ Testing Schema Registry API..."
curl -s http://localhost:8081/subjects || echo "❌ Schema Registry not accessible"

echo ""
echo "📊 Container Status:"
podman ps

echo ""
echo "🌐 Access Kafka UI: http://localhost:8080"
```

Run:
```bash
chmod +x fix-schema-registry.sh
./fix-schema-registry.sh
```

---

## Verification Checklist

After applying fixes:

- [ ] All containers running: `podman ps`
- [ ] Schema Registry healthy: `podman logs schema-registry | grep "Server started"`
- [ ] DNS works: `podman exec kafka-ui nslookup schema-registry`
- [ ] API accessible: `curl http://localhost:8081/subjects`
- [ ] Kafka UI no errors: `podman logs kafka-ui | grep -i error`
- [ ] Schema Registry tab works in UI: http://localhost:8080

---

## Common Podman Issues

| Issue | Cause | Solution |
|-------|-------|----------|
| DNS resolution fails | CNI networking issue | Use IP address or check CNI config |
| Port not accessible | Rootless port mapping | Check `podman port <container>` |
| Permission denied | SELinux/AppArmor | Add `:Z` to volume mounts |
| Container restart loop | Auth failure | Use admin user or grant ACLs |
| Network not found | Podman compose issue | Use `podman compose` instead of `podman-compose` |

---

## Recommended Configuration for Podman

```yaml
version: "3.5"

networks:
  kafka-network:
    driver: bridge

services:
  # ... kafka config ...

  schema-registry:
    image: docker.io/confluentinc/cp-schema-registry:7.6.1
    container_name: schema-registry
    hostname: schema-registry
    networks:
      - kafka-network
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

## WSL-Specific Tips

### Access from Windows
```powershell
# From Windows PowerShell/Terminal
# Use WSL IP or localhost
curl http://localhost:8081/subjects
```

### WSL IP Address
```bash
# Get WSL IP (from Windows)
wsl hostname -I

# Access from Windows browser
http://<wsl-ip>:8080
```

### Port Forwarding
If localhost doesn't work from Windows:
```bash
# In WSL, add port forwarding (Windows admin PowerShell)
netsh interface portproxy add v4tov4 listenport=8080 listenaddress=0.0.0.0 connectport=8080 connectaddress=<wsl-ip>
netsh interface portproxy add v4tov4 listenport=8081 listenaddress=0.0.0.0 connectport=8081 connectaddress=<wsl-ip>
```

---

## Next Steps

1. **Apply Quick Fix**: Change schema-registry to use `admin` user
2. **Restart**: `podman-compose down && podman-compose up -d`
3. **Verify**: Check logs and DNS resolution
4. **Test**: Access http://localhost:8080 and Schema Registry tab

---

**For immediate resolution, use the admin user fix. For production-like testing, grant ACLs to schemaregistry user after startup.**
