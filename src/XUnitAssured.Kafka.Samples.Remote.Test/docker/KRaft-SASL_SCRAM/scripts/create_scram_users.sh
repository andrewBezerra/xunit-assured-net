#!/bin/bash
set -e

echo "=========================================="
echo "Creating SCRAM Users"
echo "=========================================="

# Wait a bit more to ensure Kafka is fully ready
sleep 5

# Create SCRAM-SHA-256 users
echo "Creating SCRAM-SHA-256 user: admin"
kafka-configs --bootstrap-server kafka:9092 \
  --alter \
  --add-config 'SCRAM-SHA-256=[password=admin-secret]' \
  --entity-type users \
  --entity-name admin

echo "Creating SCRAM-SHA-256 user: user1"
kafka-configs --bootstrap-server kafka:9092 \
  --alter \
  --add-config 'SCRAM-SHA-256=[password=user1-password]' \
  --entity-type users \
  --entity-name user1

# Create SCRAM-SHA-512 users
echo "Creating SCRAM-SHA-512 user: admin"
kafka-configs --bootstrap-server kafka:9092 \
  --alter \
  --add-config 'SCRAM-SHA-512=[password=admin-secret]' \
  --entity-type users \
  --entity-name admin

echo "Creating SCRAM-SHA-512 user: user1"
kafka-configs --bootstrap-server kafka:9092 \
  --alter \
  --add-config 'SCRAM-SHA-512=[password=user1-password]' \
  --entity-type users \
  --entity-name user1

echo "=========================================="
echo "SCRAM Users Created Successfully!"
echo "=========================================="
echo "Available users:"
echo "  - admin (SCRAM-SHA-256 and SCRAM-SHA-512)"
echo "  - user1 (SCRAM-SHA-256 and SCRAM-SHA-512)"
echo "=========================================="

# List all SCRAM configurations
echo "Verifying SCRAM configurations:"
kafka-configs --bootstrap-server kafka:9092 \
  --describe \
  --entity-type users || true
