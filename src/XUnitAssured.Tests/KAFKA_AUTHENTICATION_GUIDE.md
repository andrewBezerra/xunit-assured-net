# 🔐 XUnitAssured Kafka Authentication Guide

## Overview

XUnitAssured.Kafka supports **6 authentication types** covering **90% of real-world Kafka scenarios**:

- ✅ **None** (Plaintext) - Local development
- ✅ **SASL/PLAIN** - Confluent Cloud, Azure Event Hubs (60% coverage)
- ✅ **SASL/SCRAM-SHA-256** - AWS MSK (15% coverage)
- ✅ **SASL/SCRAM-SHA-512** - Self-hosted secure (5% coverage)
- ✅ **SSL/TLS** - Enterprise on-premise (7% coverage)
- ✅ **Mutual TLS (mTLS)** - High security (3% coverage)

---

## Quick Start

### 1. SASL/PLAIN (Confluent Cloud, Azure Event Hubs)

```csharp
// From settings (recommended - DRY)
Given()
    .Topic("my-topic")
    .WithSaslPlain()  // Loads from kafkasettings.json
    .Consume();

// Explicit credentials
Given()
    .Topic("my-topic")
    .WithSaslPlain("username", "password")
    .Consume();
```

### 2. SASL/SCRAM-SHA-256 (AWS MSK)

```csharp
// From settings
Given()
    .Topic("my-topic")
    .WithSaslScram()  // Auto-detects SHA-256 or SHA-512
    .Consume();

// Explicit SHA-256
Given()
    .Topic("my-topic")
    .WithSaslScram256("username", "password")
    .Consume();

// Explicit SHA-512
Given()
    .Topic("my-topic")
    .WithSaslScram512("username", "password")
    .Consume();
```

### 3. SSL/TLS (Enterprise)

```csharp
// From settings
Given()
    .Topic("my-topic")
    .WithSsl()
    .Consume();

// Explicit CA certificate
Given()
    .Topic("my-topic")
    .WithSsl("/path/to/ca-cert.pem")
    .Consume();
```

### 4. Mutual TLS (High Security)

```csharp
Given()
    .Topic("my-topic")
    .WithMutualTls(
        certificateLocation: "/path/to/client-cert.pem",
        keyLocation: "/path/to/client-key.pem",
        caLocation: "/path/to/ca-cert.pem"
    )
    .Consume();
```

---

## Configuration File (`kafkasettings.json`)

Place this file in the root of your test project:

```json
{
  "kafka": {
    "bootstrapServers": "localhost:9092",
    "groupId": "test-consumer-group",
    "authentication": {
      "type": "None"
    }
  },
  "environments": {
    "confluent": {
      "bootstrapServers": "pkc-xxxxx.us-east-1.aws.confluent.cloud:9092",
      "groupId": "confluent-group",
      "authentication": {
        "type": "SaslSsl",
        "saslPlain": {
          "username": "${ENV:CONFLUENT_KAFKA_KEY}",
          "password": "${ENV:CONFLUENT_KAFKA_SECRET}",
          "useSsl": true
        }
      }
    },
    "aws-msk": {
      "bootstrapServers": "b-1.mycluster.kafka.us-east-1.amazonaws.com:9096",
      "groupId": "msk-group",
      "authentication": {
        "type": "SaslScram256",
        "saslScram": {
          "username": "${ENV:MSK_USERNAME}",
          "password": "${ENV:MSK_PASSWORD}",
          "mechanism": "SCRAM-SHA-256",
          "useSsl": true
        }
      }
    },
    "enterprise-ssl": {
      "bootstrapServers": "kafka-ssl.company.com:9093",
      "groupId": "enterprise-group",
      "authentication": {
        "type": "Ssl",
        "ssl": {
          "sslCaLocation": "${ENV:KAFKA_CA_CERT_PATH}",
          "enableSslCertificateVerification": true
        }
      }
    },
    "enterprise-mtls": {
      "bootstrapServers": "kafka-mtls.company.com:9093",
      "groupId": "mtls-group",
      "authentication": {
        "type": "MutualTls",
        "ssl": {
          "sslCertificateLocation": "${ENV:KAFKA_CLIENT_CERT_PATH}",
          "sslKeyLocation": "${ENV:KAFKA_CLIENT_KEY_PATH}",
          "sslCaLocation": "${ENV:KAFKA_CA_CERT_PATH}",
          "sslKeyPassword": "${ENV:KAFKA_KEY_PASSWORD}",
          "enableSslCertificateVerification": true
        }
      }
    }
  }
}
```

### Environment Variables

```sh
# Windows (PowerShell)
$env:CONFLUENT_KAFKA_KEY = "your-api-key"
$env:CONFLUENT_KAFKA_SECRET = "your-api-secret"
$env:MSK_USERNAME = "your-msk-username"
$env:MSK_PASSWORD = "your-msk-password"
$env:KAFKA_CA_CERT_PATH = "C:\certs\ca-cert.pem"

# Linux/Mac
export CONFLUENT_KAFKA_KEY="your-api-key"
export CONFLUENT_KAFKA_SECRET="your-api-secret"
export MSK_USERNAME="your-msk-username"
export MSK_PASSWORD="your-msk-password"
export KAFKA_CA_CERT_PATH="/certs/ca-cert.pem"
```

---

## Authentication Types Comparison

| Type | Use Case | Platforms | Security | Complexity |
|------|----------|-----------|----------|------------|
| **None** | Local dev | Docker, localhost | ⚠️ Low | ⭐ Easy |
| **SASL/PLAIN** | Cloud managed | Confluent, Azure | ⭐⭐ Medium | ⭐⭐ Easy |
| **SASL/SCRAM-256** | AWS MSK | AWS MSK, self-hosted | ⭐⭐⭐ High | ⭐⭐ Medium |
| **SASL/SCRAM-512** | Self-hosted secure | On-premise | ⭐⭐⭐⭐ Very High | ⭐⭐ Medium |
| **SSL/TLS** | Enterprise | On-premise | ⭐⭐⭐⭐ Very High | ⭐⭐⭐ Complex |
| **Mutual TLS** | High security | Banking, Finance | ⭐⭐⭐⭐⭐ Maximum | ⭐⭐⭐⭐ Complex |

---

## Platform-Specific Examples

### Confluent Cloud

```csharp
// kafkasettings.json
{
  "kafka": {
    "bootstrapServers": "pkc-xxxxx.confluent.cloud:9092",
    "authentication": {
      "type": "SaslSsl",
      "saslPlain": {
        "username": "${ENV:CONFLUENT_KAFKA_KEY}",
        "password": "${ENV:CONFLUENT_KAFKA_SECRET}",
        "useSsl": true
      }
    }
  }
}

// Test code
Given()
    .Topic("orders")
    .WithSaslPlain()  // Uses settings
    .Consume()
    .Validate(result => result.Success.ShouldBeTrue());
```

### AWS MSK

```csharp
// kafkasettings.json
{
  "kafka": {
    "bootstrapServers": "b-1.mycluster.kafka.us-east-1.amazonaws.com:9096",
    "authentication": {
      "type": "SaslScram256",
      "saslScram": {
        "username": "${ENV:MSK_USERNAME}",
        "password": "${ENV:MSK_PASSWORD}",
        "mechanism": "SCRAM-SHA-256",
        "useSsl": true
      }
    }
  }
}

// Test code
Given()
    .Topic("transactions")
    .WithSaslScram()  // Uses settings
    .Consume();
```

### Azure Event Hubs (Kafka API)

```csharp
Given()
    .Topic("my-event-hub")
    .WithSaslPlain("$ConnectionString", "Endpoint=sb://...")
    .Consume();
```

### Enterprise On-Premise (SSL)

```csharp
Given()
    .Topic("customer-data")
    .WithSsl("/etc/kafka/certs/ca-cert.pem")
    .Consume();
```

### High Security (Mutual TLS)

```csharp
Given()
    .Topic("sensitive-data")
    .WithMutualTls(
        certificateLocation: "/etc/kafka/certs/client-cert.pem",
        keyLocation: "/etc/kafka/certs/client-key.pem",
        caLocation: "/etc/kafka/certs/ca-cert.pem",
        keyPassword: "secure-password"
    )
    .Consume();
```

---

## Advanced Usage

### Custom Configuration

```csharp
Given()
    .Topic("my-topic")
    .WithKafkaAuth(config =>
    {
        config.UseSaslPlain("user", "pass");
        // or
        config.UseSaslScram256("user", "pass");
        // or
        config.UseSsl("/path/to/ca-cert.pem");
    })
    .Consume();
```

### Override Settings

```csharp
// Most tests use settings
Given().Topic("topic1").WithSaslPlain().Consume();

// Specific test needs different auth
Given()
    .Topic("topic2")
    .WithSaslPlain("different-user", "different-pass")
    .Consume();
```

### Disable Authentication

```csharp
// Override settings authentication
Given()
    .Topic("local-topic")
    .WithNoKafkaAuth()
    .Consume();
```

---

## Troubleshooting

### Authentication Not Applied

1. Check if `kafkasettings.json` exists
2. Verify JSON syntax is valid
3. Check environment variables are set
4. Verify bootstrap servers are correct

### SSL Certificate Errors

```csharp
// Disable verification for development (NOT for production!)
Given()
    .Topic("test-topic")
    .WithSsl("/path/to/ca-cert.pem", enableCertificateVerification: false)
    .Consume();
```

### SASL Authentication Failed

- Verify username and password are correct
- Check if mechanism matches broker (PLAIN vs SCRAM-SHA-256 vs SCRAM-SHA-512)
- Ensure SSL is enabled when required (`useSsl: true`)

### Connection Timeout

- Verify bootstrap servers address and port
- Check network connectivity
- Verify firewall rules allow Kafka ports (9092, 9093, 9096, etc.)

---

## Best Practices

### ✅ DO:
- Store sensitive credentials in environment variables
- Use `kafkasettings.json` for base configuration
- Override authentication per test when needed
- Use SSL/TLS in production
- Enable certificate verification in production
- Use SCRAM over PLAIN when possible

### ❌ DON'T:
- Commit passwords in `kafkasettings.json`
- Hardcode credentials in test code
- Disable SSL certificate verification in production
- Use PLAIN without SSL/TLS
- Share authentication across incompatible tests

---

## Examples Repository

See complete examples in:
- `XUnitAssured.Tests/KafkaTests/SaslPlainAuthTests.cs`
- `XUnitAssured.Tests/KafkaTests/SaslScramAuthTests.cs`
- `XUnitAssured.Tests/KafkaTests/SslAuthTests.cs`

---

## Extension Methods Reference

| Method | Parameters | Description |
|--------|-----------|-------------|
| `.WithSaslPlain()` | none | Load SASL/PLAIN from settings |
| `.WithSaslPlain(user, pass)` | username, password, useSsl? | Explicit SASL/PLAIN |
| `.WithSaslScram()` | none | Load SASL/SCRAM from settings |
| `.WithSaslScram256(user, pass)` | username, password, useSsl? | Explicit SCRAM-SHA-256 |
| `.WithSaslScram512(user, pass)` | username, password, useSsl? | Explicit SCRAM-SHA-512 |
| `.WithSsl()` | none | Load SSL from settings |
| `.WithSsl(caLocation)` | caLocation, enableVerification? | Explicit SSL/TLS |
| `.WithMutualTls(cert, key)` | certificateLocation, keyLocation, caLocation?, keyPassword? | Mutual TLS |
| `.WithKafkaAuth(config)` | Action<KafkaAuthConfig> | Custom configuration |
| `.WithNoKafkaAuth()` | none | Disable authentication |

---

**Need help?** Open an issue on GitHub: https://github.com/andrewBezerra/XUnitAssured.Net
