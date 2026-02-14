#!/bin/bash
# ============================================================================
# CREATE SCRAM USERS IN KAFKA VIA ZOOKEEPER
# ============================================================================
# This script is executed by the kafka-init container after the broker starts.
# It creates SCRAM-SHA-256 and SCRAM-SHA-512 users.
# ============================================================================

set -e

KAFKA_BOOTSTRAP="kafka:9092"
ZOOKEEPER="zookeeper:2181"

echo "=== Waiting for Kafka broker to be ready ==="

# Wait for Kafka to be ready
cub kafka-ready -b "$KAFKA_BOOTSTRAP" 1 60

echo "=== Creating SCRAM users ==="

# Create SCRAM-SHA-256 user
kafka-configs --zookeeper "$ZOOKEEPER" --alter \
  --add-config 'SCRAM-SHA-256=[password=scrampass],SCRAM-SHA-512=[password=scrampass]' \
  --entity-type users --entity-name scramuser

echo "Created user: scramuser (SCRAM-SHA-256 + SCRAM-SHA-512)"

# Create admin SCRAM user (for inter-broker if needed)
kafka-configs --zookeeper "$ZOOKEEPER" --alter \
  --add-config 'SCRAM-SHA-256=[password=admin-secret],SCRAM-SHA-512=[password=admin-secret]' \
  --entity-type users --entity-name admin

echo "Created user: admin (SCRAM-SHA-256 + SCRAM-SHA-512)"

echo ""
echo "=== SCRAM users created successfully ==="
