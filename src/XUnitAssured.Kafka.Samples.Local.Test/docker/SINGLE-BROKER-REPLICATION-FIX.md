# Single Broker Replication Factor Issue

## Problem

When running Kafka with a **single broker**, you may encounter this error:

```
INVALID_REPLICATION_FACTOR (Unable to replicate the partition 3 time(s): 
The target replication factor of 3 cannot be reached because only 1 broker(s) are registered.)
```

This happens for internal topics like:
- `__consumer_offsets`
- `__transaction_state`

## Why This Happens

Kafka's **default replication factor** is typically **3** for production-grade clusters. This ensures:
- High availability
- Fault tolerance
- Data redundancy

However, in a **single-broker setup** (development/testing):
- Only 1 broker is available
- Cannot replicate to 3 brokers
- Topic creation fails

## Solution

Add these environment variables to force **replication factor = 1**:

```yaml
kafka:
  environment:
    # Replication factors for single broker
    KAFKA_OFFSETS_TOPIC_REPLICATION_FACTOR: 1
    KAFKA_OFFSETS_TOPIC_NUM_PARTITIONS: 50
    KAFKA_TRANSACTION_STATE_LOG_REPLICATION_FACTOR: 1
    KAFKA_TRANSACTION_STATE_LOG_MIN_ISR: 1
    KAFKA_DEFAULT_REPLICATION_FACTOR: 1
    KAFKA_MIN_INSYNC_REPLICAS: 1
```

## Affected Configurations

This fix has been applied to:
- ✅ KRaft-SASL_SCRAM-256
- ✅ KRaft-SASL_SCRAM-512

Other configurations already had these settings or don't need them.

## What Each Setting Does

| Setting | Purpose | Value for Single Broker |
|---------|---------|------------------------|
| `KAFKA_OFFSETS_TOPIC_REPLICATION_FACTOR` | Replication for consumer offsets topic | 1 |
| `KAFKA_OFFSETS_TOPIC_NUM_PARTITIONS` | Partitions for consumer offsets | 50 (default) |
| `KAFKA_TRANSACTION_STATE_LOG_REPLICATION_FACTOR` | Replication for transaction log | 1 |
| `KAFKA_TRANSACTION_STATE_LOG_MIN_ISR` | Minimum in-sync replicas for transactions | 1 |
| `KAFKA_DEFAULT_REPLICATION_FACTOR` | Default replication for new topics | 1 |
| `KAFKA_MIN_INSYNC_REPLICAS` | Minimum replicas for writes | 1 |

## Multi-Broker Setup

If you plan to add more brokers later:

### 3 Brokers (Production-like)
```yaml
KAFKA_OFFSETS_TOPIC_REPLICATION_FACTOR: 3
KAFKA_TRANSACTION_STATE_LOG_REPLICATION_FACTOR: 3
KAFKA_TRANSACTION_STATE_LOG_MIN_ISR: 2
KAFKA_DEFAULT_REPLICATION_FACTOR: 3
KAFKA_MIN_INSYNC_REPLICAS: 2
```

### 2 Brokers (Minimal Redundancy)
```yaml
KAFKA_OFFSETS_TOPIC_REPLICATION_FACTOR: 2
KAFKA_TRANSACTION_STATE_LOG_REPLICATION_FACTOR: 2
KAFKA_TRANSACTION_STATE_LOG_MIN_ISR: 1
KAFKA_DEFAULT_REPLICATION_FACTOR: 2
KAFKA_MIN_INSYNC_REPLICAS: 1
```

## Impact on Schema Registry

The Schema Registry error:
```
SchemaRegistryTimeoutException: Timed out waiting for join group to complete
```

Was caused by the `__consumer_offsets` topic not being created. With the fix:
- ✅ `__consumer_offsets` topic created with replication factor 1
- ✅ Schema Registry can join consumer group
- ✅ Schema Registry starts successfully

## Verification

After applying the fix:

```bash
# Wait for Kafka to start
podman logs kafka -f

# Should see:
# [KafkaServer id=1] started (kafka.server.KafkaServer)

# Verify __consumer_offsets topic
podman exec kafka kafka-topics --bootstrap-server kafka:19092 --list

# Should include:
# __consumer_offsets
# _schemas (created by Schema Registry)
```

## Best Practices

### Development/Testing (Single Broker)
- ✅ Replication Factor: 1
- ✅ Min ISR: 1
- ✅ Fast startup
- ⚠️ No redundancy

### Production (Multi-Broker)
- ✅ Replication Factor: 3
- ✅ Min ISR: 2
- ✅ High availability
- ✅ Data durability

## Common Errors Without This Fix

### Error 1: Consumer Offsets Topic
```
INVALID_REPLICATION_FACTOR (Unable to replicate the partition 3 time(s)
```

**Solution**: Set `KAFKA_OFFSETS_TOPIC_REPLICATION_FACTOR: 1`

### Error 2: Transaction State Log
```
org.apache.kafka.common.errors.InvalidReplicationFactorException
```

**Solution**: Set `KAFKA_TRANSACTION_STATE_LOG_REPLICATION_FACTOR: 1`

### Error 3: Schema Registry Timeout
```
SchemaRegistryTimeoutException: Timed out waiting for join group to complete
```

**Solution**: Fix consumer offsets topic replication (Error 1)

### Error 4: New Topics Fail
```
Topic replication factor cannot be greater than available brokers
```

**Solution**: Set `KAFKA_DEFAULT_REPLICATION_FACTOR: 1`

## Testing the Fix

```bash
# 1. Stop and clean
podman-compose down -v

# 2. Start with new config
podman-compose up -d

# 3. Watch logs (no replication errors)
podman logs kafka -f

# 4. Verify topics created
podman exec kafka kafka-topics \
  --bootstrap-server kafka:19092 \
  --list

# 5. Check Schema Registry (should start successfully)
podman logs schema-registry -f
```

## Summary

| Issue | Cause | Fix |
|-------|-------|-----|
| Replication error | Default RF=3, only 1 broker | Set RF=1 for all internal topics |
| Schema Registry timeout | Consumer offsets not created | Fix replication factor |
| New topics fail | Default RF too high | Set DEFAULT_REPLICATION_FACTOR=1 |

---

**This fix allows single-broker Kafka to work properly for development and testing.**
