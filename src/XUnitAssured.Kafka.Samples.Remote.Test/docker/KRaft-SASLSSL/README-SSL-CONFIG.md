# SASL/SSL Configuration Guide

## Overview

This guide explains how the SASL/SCRAM-SHA-512 with SSL authentication works in the XUnitAssured.Kafka tests.

## Current Configuration

### Docker Compose
The Kafka broker is configured with:
- **Security Protocol**: SASL_SSL
- **SASL Mechanism**: SCRAM-SHA-512
- **SSL Certificates**: Java Keystores (JKS format)
  - Location: `./secrets/kafka.keystore.jks` and `./secrets/kafka.truststore.jks`
  - Password: `kafka-secret`
- **Client Authentication**: Not required (`none`)
- **Endpoint Verification**: Disabled

### Test Settings (`testsettings.json`)

```json
{
  "kafka": {
    "bootstrapServers": "localhost:39093",
    "securityProtocol": "SaslSsl",
    "authentication": {
      "type": "SaslScram512",
      "saslScram": {
        "username": "admin",
        "password": "admin-secret",
        "useSsl": true
      }
    },
    "ssl": {
      "enableSslCertificateVerification": false
    }
  }
}
```

## Important Notes

### 1. Confluent.Kafka .NET Client & JKS Files

**The Confluent.Kafka .NET client does NOT support Java Keystore (JKS) files directly.**

It expects certificates in **PEM format**:
- CA Certificate: `.crt` or `.pem` file
- Client Certificate: `.crt` or `.pem` file (for mutual TLS)
- Client Key: `.key` or `.pem` file (for mutual TLS)

### 2. Why the Tests Work Without PEM Files

The tests work because:
1. **SSL Certificate Verification is Disabled** (`enableSslCertificateVerification: false`)
2. **The connection is still encrypted** (SSL/TLS handshake occurs)
3. **SASL authentication validates identity** (username/password)

This is **acceptable for local development/testing** but **NOT recommended for production**.

### 3. Converting JKS to PEM (Optional for Production)

If you need full SSL verification, convert the JKS files to PEM:

#### Extract CA Certificate from Truststore

```bash
# Navigate to secrets directory
cd docker/KRaft-SASLSSL/secrets

# Convert truststore to PKCS12
keytool -importkeystore \
  -srckeystore kafka.truststore.jks \
  -srcstoretype JKS \
  -srcstorepass kafka-secret \
  -destkeystore kafka.truststore.p12 \
  -deststoretype PKCS12 \
  -deststorepass kafka-secret

# Extract CA certificate
openssl pkcs12 -in kafka.truststore.p12 \
  -nokeys -out ca-cert.pem \
  -passin pass:kafka-secret
```

#### Update `testsettings.json` to Use CA Certificate

```json
{
  "kafka": {
    "ssl": {
      "sslCaLocation": "docker/KRaft-SASLSSL/secrets/ca-cert.pem",
      "enableSslCertificateVerification": true
    }
  }
}
```

## Architecture

### Authentication Flow

1. **Client connects** to `localhost:39093`
2. **SSL/TLS handshake** (encryption established)
3. **SASL/SCRAM-SHA-512 authentication**:
   - Client sends username: `admin`
   - Server validates password: `admin-secret`
4. **Connection established** if authentication succeeds

### Configuration Layers

The XUnitAssured.Kafka library applies configurations in this order:

1. **SASL Handler** (`SaslScramHandler`):
   - Sets `SecurityProtocol` (SaslSsl or SaslPlaintext)
   - Sets `SaslMechanism` (ScramSha512)
   - Sets `SaslUsername` and `SaslPassword`

2. **SSL Configuration** (if `authentication.ssl` is present):
   - Sets `SslCaLocation` (if provided)
   - Sets `EnableSslCertificateVerification`
   - Sets client certificates (for mutual TLS)

## Troubleshooting

### Error: "Broker: Not authorized"
- Check username/password in `testsettings.json`
- Verify SCRAM credentials in Kafka broker

### Error: "SSL handshake failed"
- Enable logging in Kafka client
- Check if broker is listening on correct port
- Verify `enableSslCertificateVerification` is `false` for self-signed certs

### Error: "Connection refused"
- Ensure Docker Compose is running: `docker-compose up -d`
- Check port mapping: `docker ps | grep kafka`
- Verify firewall allows port 39093

## References

- [Confluent Kafka Security Documentation](https://docs.confluent.io/platform/current/security/index.html)
- [SASL/SCRAM Authentication](https://docs.confluent.io/platform/current/kafka/authentication_sasl/authentication_sasl_scram.html)
- [SSL Encryption](https://docs.confluent.io/platform/current/kafka/encryption.html)
