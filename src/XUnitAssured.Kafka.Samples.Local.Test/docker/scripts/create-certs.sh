#!/bin/bash
# ============================================================================
# CERTIFICATE GENERATION SCRIPT FOR KAFKA SSL/mTLS TESTING
# ============================================================================
# Generates: CA cert, broker keystore/truststore, client cert/key
# Output: docker/config/certs/
# ============================================================================

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CERTS_DIR="$SCRIPT_DIR/../config/certs"
PASSWORD="changeit"
VALIDITY_DAYS=365

# Broker settings
BROKER_CN="kafka"
BROKER_SAN="DNS:kafka,DNS:localhost,IP:127.0.0.1"

# Client settings
CLIENT_CN="kafka-test-client"

echo "=== Generating Kafka SSL/mTLS certificates ==="
echo "Output directory: $CERTS_DIR"

# Clean and create directory
rm -rf "$CERTS_DIR"
mkdir -p "$CERTS_DIR"

# -------------------------------------------------------
# 1. Generate CA (Certificate Authority)
# -------------------------------------------------------
echo "[1/5] Generating CA certificate..."

openssl req -new -x509 -keyout "$CERTS_DIR/ca-key.pem" -out "$CERTS_DIR/ca-cert.pem" \
  -days $VALIDITY_DAYS -nodes \
  -subj "/CN=XUnitAssured-Test-CA/O=XUnitAssured/C=BR"

# -------------------------------------------------------
# 2. Generate Broker Keystore (JKS for Kafka)
# -------------------------------------------------------
echo "[2/5] Generating broker keystore..."

# Create broker keystore with private key
keytool -genkey -noprompt \
  -alias broker \
  -dname "CN=$BROKER_CN,O=XUnitAssured,C=BR" \
  -ext "SAN=$BROKER_SAN" \
  -keystore "$CERTS_DIR/kafka.broker.keystore.jks" \
  -keyalg RSA -keysize 2048 \
  -storepass "$PASSWORD" -keypass "$PASSWORD" \
  -validity $VALIDITY_DAYS

# Create CSR
keytool -certreq -alias broker \
  -keystore "$CERTS_DIR/kafka.broker.keystore.jks" \
  -file "$CERTS_DIR/broker-csr.pem" \
  -storepass "$PASSWORD" \
  -ext "SAN=$BROKER_SAN"

# Sign broker cert with CA
openssl x509 -req -CA "$CERTS_DIR/ca-cert.pem" -CAkey "$CERTS_DIR/ca-key.pem" \
  -in "$CERTS_DIR/broker-csr.pem" -out "$CERTS_DIR/broker-cert-signed.pem" \
  -days $VALIDITY_DAYS -CAcreateserial \
  -extfile <(printf "subjectAltName=$BROKER_SAN")

# Import CA cert into broker keystore
keytool -import -noprompt -alias ca \
  -file "$CERTS_DIR/ca-cert.pem" \
  -keystore "$CERTS_DIR/kafka.broker.keystore.jks" \
  -storepass "$PASSWORD"

# Import signed broker cert into broker keystore
keytool -import -noprompt -alias broker \
  -file "$CERTS_DIR/broker-cert-signed.pem" \
  -keystore "$CERTS_DIR/kafka.broker.keystore.jks" \
  -storepass "$PASSWORD"

# -------------------------------------------------------
# 3. Generate Broker Truststore
# -------------------------------------------------------
echo "[3/5] Generating broker truststore..."

keytool -import -noprompt -alias ca \
  -file "$CERTS_DIR/ca-cert.pem" \
  -keystore "$CERTS_DIR/kafka.broker.truststore.jks" \
  -storepass "$PASSWORD"

# -------------------------------------------------------
# 4. Generate Client Certificate (PEM format for .NET)
# -------------------------------------------------------
echo "[4/5] Generating client certificate..."

openssl req -new -newkey rsa:2048 -nodes \
  -keyout "$CERTS_DIR/client-key.pem" -out "$CERTS_DIR/client-csr.pem" \
  -subj "/CN=$CLIENT_CN/O=XUnitAssured/C=BR"

openssl x509 -req -CA "$CERTS_DIR/ca-cert.pem" -CAkey "$CERTS_DIR/ca-key.pem" \
  -in "$CERTS_DIR/client-csr.pem" -out "$CERTS_DIR/client-cert.pem" \
  -days $VALIDITY_DAYS -CAcreateserial

# -------------------------------------------------------
# 5. Create credentials file
# -------------------------------------------------------
echo "[5/5] Creating credentials file..."

cat > "$CERTS_DIR/broker_creds" <<EOF
$PASSWORD
EOF

# Clean up CSR files
rm -f "$CERTS_DIR/broker-csr.pem" "$CERTS_DIR/client-csr.pem" "$CERTS_DIR/ca-cert.srl"

echo ""
echo "=== Certificates generated successfully ==="
echo "CA cert:           $CERTS_DIR/ca-cert.pem"
echo "Broker keystore:   $CERTS_DIR/kafka.broker.keystore.jks"
echo "Broker truststore: $CERTS_DIR/kafka.broker.truststore.jks"
echo "Client cert:       $CERTS_DIR/client-cert.pem"
echo "Client key:        $CERTS_DIR/client-key.pem"
echo "Password:          $PASSWORD"
