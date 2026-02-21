#!/bin/bash

# Quick Start Script for Podman + WSL
# KRaft-SASL_SCRAM-512 Configuration

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

echo "🚀 Kafka SCRAM-SHA-512 - Podman Quick Start"
echo "=============================================="
echo ""

# Function to check if command exists
command_exists() {
    command -v "$1" >/dev/null 2>&1
}

# Check prerequisites
echo "📋 Checking prerequisites..."
if ! command_exists podman; then
    echo "❌ Podman is not installed. Install it with:"
    echo "   sudo apt update && sudo apt install podman"
    exit 1
fi

if ! command_exists podman-compose && ! command_exists docker-compose; then
    echo "⚠️  Warning: Neither podman-compose nor docker-compose found."
    echo "   Install podman-compose with:"
    echo "   pip3 install podman-compose"
    echo "   Or use: podman compose (native support)"
fi

echo "✅ Prerequisites OK"
echo ""

# Stop existing containers
echo "🛑 Stopping existing containers..."
if command_exists podman-compose; then
    podman-compose down 2>/dev/null || true
else
    podman compose down 2>/dev/null || true
fi

# Clean volumes (optional)
read -p "🧹 Clean volumes? This will delete all data (y/N): " -n 1 -r
echo
if [[ $REPLY =~ ^[Yy]$ ]]; then
    echo "   Cleaning volumes..."
    podman volume prune -f
fi

# Start containers
echo ""
echo "🚀 Starting containers..."
if command_exists podman-compose; then
    podman-compose up -d
else
    podman compose up -d
fi

# Wait for containers
echo ""
echo "⏳ Waiting for containers to start..."
sleep 5

# Check container status
echo ""
echo "📊 Container Status:"
podman ps --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}"

# Wait for Kafka to be healthy
echo ""
echo "⏳ Waiting for Kafka to be healthy (may take 30-40s)..."
timeout=60
elapsed=0
while ! podman exec kafka kafka-broker-api-versions --bootstrap-server kafka:19092 >/dev/null 2>&1; do
    if [ $elapsed -ge $timeout ]; then
        echo "❌ Timeout waiting for Kafka"
        echo "   Check logs: podman logs kafka"
        exit 1
    fi
    echo -n "."
    sleep 2
    elapsed=$((elapsed + 2))
done
echo " ✅ Kafka is healthy!"

# Check Schema Registry
echo ""
echo "🔍 Checking Schema Registry..."
sleep 5
if podman logs schema-registry 2>&1 | grep -q "Server started"; then
    echo "✅ Schema Registry is running"
else
    echo "⚠️  Schema Registry may not be fully started yet"
    echo "   Check logs: podman logs schema-registry"
fi

# Test DNS resolution
echo ""
echo "🧪 Testing DNS resolution..."
if podman exec kafka-ui nslookup schema-registry >/dev/null 2>&1; then
    echo "✅ DNS resolution: schema-registry → OK"
else
    echo "❌ DNS resolution: schema-registry → FAILED"
    echo "   Check network: podman network inspect kraft-sasl_scram-512_kafka-network"
fi

# Test Schema Registry API
echo ""
echo "🧪 Testing Schema Registry API..."
if curl -sf http://localhost:8081/subjects >/dev/null 2>&1; then
    echo "✅ Schema Registry API: http://localhost:8081 → OK"
else
    echo "⚠️  Schema Registry API not accessible yet (may take a few more seconds)"
fi

# Test Kafka UI
echo ""
echo "🧪 Testing Kafka UI..."
if curl -sf http://localhost:8080 >/dev/null 2>&1; then
    echo "✅ Kafka UI: http://localhost:8080 → OK"
else
    echo "⚠️  Kafka UI not accessible yet"
fi

# Summary
echo ""
echo "=============================================="
echo "✅ Startup Complete!"
echo "=============================================="
echo ""
echo "📍 Services:"
echo "   Kafka Broker:     localhost:39092 (external)"
echo "   Kafka Broker:     kafka:19092 (internal)"
echo "   Schema Registry:  http://localhost:8081"
echo "   Kafka UI:         http://localhost:8080"
echo ""
echo "🔐 Credentials:"
echo "   Username: admin"
echo "   Password: admin-secret"
echo "   Mechanism: SCRAM-SHA-512"
echo ""
echo "📋 Useful Commands:"
echo "   View logs:        podman logs kafka -f"
echo "   View logs:        podman logs schema-registry -f"
echo "   View logs:        podman logs kafka-ui -f"
echo "   All logs:         podman-compose logs -f"
echo "   Stop:             podman-compose down"
echo "   Restart:          podman-compose restart"
echo ""
echo "🌐 Access Kafka UI: http://localhost:8080"
echo ""

# Optional: Open browser (if running in WSL with Windows)
if grep -qi microsoft /proc/version 2>/dev/null; then
    read -p "🌐 Open Kafka UI in Windows browser? (y/N): " -n 1 -r
    echo
    if [[ $REPLY =~ ^[Yy]$ ]]; then
        # Get WSL IP
        WSL_IP=$(hostname -I | awk '{print $1}')
        echo "   Opening http://${WSL_IP}:8080 ..."
        cmd.exe /c start "http://${WSL_IP}:8080" 2>/dev/null || echo "   Please open manually: http://localhost:8080"
    fi
fi

echo "✨ Done!"
