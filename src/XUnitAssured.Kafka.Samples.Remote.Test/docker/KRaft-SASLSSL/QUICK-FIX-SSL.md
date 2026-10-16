# SASL/SCRAM-SHA-512 + SSL Configuration - Quick Fix Guide

## Problem
The Kafka broker certificate is issued for hostname `kafka`, but the client connects via `localhost:39093`.  
Even with SSL verification disabled, Windows OpenSSL requires a trusted CA certificate.

## ✅ Complete Solution (Step-by-Step)

### Step 1: Add Hostname Alias

Run PowerShell **as Administrator**:

```powershell
Add-Content -Path "$env:SystemRoot\System32\drivers\etc\hosts" -Value "`n127.0.0.1 kafka" -Encoding ASCII
```

Verify:
```powershell
ping kafka
# Should respond from 127.0.0.1
```

### Step 1b: Generate the certificates

The certificates are not in the repository; each machine generates its own CA. In Git Bash (needs
`openssl` and a JDK's `keytool`), in this directory: `bash scripts/generate_ssl_certs.sh`. The CA can
only sign for `kafka`, `localhost` and `127.0.0.1`. Up to 6.30.3 the CA's private key was committed:
if you installed that CA, remove it first (`.\uninstall-ca-cert.ps1`).

### Step 2: Install CA Certificate

Run PowerShell **as Administrator** in the docker directory:

```powershell
cd C:\DEV\ProjetosPessoais\XUnitAssured.Net\src\XUnitAssured.Kafka.Samples.Local.Test\docker\KRaft-SASLSSL
.\install-ca-cert.ps1
```

This will:
- ✅ Check if running as Administrator
- ✅ Verify certificate file exists
- ✅ Install the CA certificate to Windows Root Certificate Store
- ✅ Verify the installation

**Alternative manual method**:
```powershell
certutil -addstore "Root" "$PWD\secrets\ca-cert.pem"
```

### Step 3: Verify Installation

```powershell
# Check certificate is installed
certutil -store "Root" | Select-String -Pattern "KafkaCA"

# Test Kafka connection
Test-NetConnection kafka -Port 39093
```

### Step 4: Run the Test

```powershell
cd ..\..\
dotnet test --filter "Auth05_SaslSsl_ShouldSucceed"
```

Expected result: **✅ PASSED**

---

## 🔄 Uninstall Certificate (Optional)

To remove the certificate after testing:

```powershell
cd C:\DEV\ProjetosPessoais\XUnitAssured.Net\src\XUnitAssured.Kafka.Samples.Local.Test\docker\KRaft-SASLSSL
.\uninstall-ca-cert.ps1
```

---

## ⚠️ Troubleshooting

### Issue: "Access Denied" when installing certificate
**Solution**: Make sure you're running PowerShell as **Administrator**

### Issue: Certificate not found
**Solution**: Generate certificates first:
```bash
cd scripts
./generate_ssl_certs.sh
```

### Issue: Test still fails after installation
**Solution**: Clear DNS cache and restart test:
```powershell
ipconfig /flushdns
dotnet test --filter "Auth05_SaslSsl_ShouldSucceed"
```

---

## 📊 Current Configuration Status

✅ **testsettings.json** - Using `kafka:39093`  
✅ **TestSettingsKafkaExtensions.cs** - Normalizes SASL Mechanism to ScramSha512  
✅ **KafkaClassFixture.cs** - Applies SSL CA Location and disables hostname verification  
✅ **Code compilation** - No errors  
✅ **Hostname resolution** - `kafka` resolves to `127.0.0.1`  
⏳ **Pending**: Install CA certificate (run `install-ca-cert.ps1`)

---

## 🎯 Quick Commands Summary

```powershell
# 1. Add hostname (as Admin)
Add-Content -Path "$env:SystemRoot\System32\drivers\etc\hosts" -Value "`n127.0.0.1 kafka"

# 2. Install certificate (as Admin)
cd C:\DEV\ProjetosPessoais\XUnitAssured.Net\src\XUnitAssured.Kafka.Samples.Local.Test\docker\KRaft-SASLSSL
.\install-ca-cert.ps1

# 3. Run test
cd ..\..\
dotnet test --filter "Auth05_SaslSsl_ShouldSucceed"
```

---

## Solution 3: Regenerate Certificates with localhost

### Modify certificate generation script
In `docker/KRaft-SASLSSL/scripts/generate_ssl_certs.sh`, add `DNS:localhost` and `IP:127.0.0.1` to the SAN:

```bash
# Add to the certificate generation
subjectAltName = DNS:kafka,DNS:localhost,IP:127.0.0.1
```

### Regenerate certificates
```bash
cd docker/KRaft-SASLSSL/scripts
./generate_ssl_certs.sh
```

### Restart Kafka
```bash
cd ../
docker-compose down
docker-compose up -d
```

---

## Current Configuration Status

✅ **testsettings.json** - Correct SASL/SCRAM-SHA-512 + SSL config  
✅ **TestSettingsKafkaExtensions.cs** - Normalizes SASL Mechanism to ScramSha512  
✅ **KafkaClassFixture.cs** - Applies SSL CA Location and disables hostname verification  
✅ **Code compilation** - No errors  

⚠️ **Pending**: Certificate trust issue (requires one of the solutions above)

---

## Test After Applying Solution

```powershell
dotnet test --filter "FullyQualifiedName~AuthenticationScramSha512SslTests.Auth05_SaslSsl_ShouldSucceed"
```

Expected result: **PASSED** ✅
