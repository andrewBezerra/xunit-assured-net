# 🚀 Quick Start - SASL/SCRAM-SHA-512 + SSL

Este guia mostra como configurar e executar testes com SASL/SCRAM-SHA-512 + SSL.

## ⚡ Configuração Rápida (2 minutos)

### 1️⃣ Adicionar hostname `kafka` (Apenas uma vez)

**Execute PowerShell como Administrador**:

```powershell
Add-Content -Path "$env:SystemRoot\System32\drivers\etc\hosts" -Value "`n127.0.0.1 kafka" -Encoding ASCII
```

✅ **Verificar**: Execute `ping kafka` - deve responder de `127.0.0.1`

---

### 2️⃣ Instalar Certificado CA (Apenas uma vez)

**Execute PowerShell como Administrador** neste diretório:

```powershell
cd C:\DEV\ProjetosPessoais\XUnitAssured.Net\src\XUnitAssured.Kafka.Samples.Local.Test\docker\KRaft-SASLSSL
.\install-ca-cert.ps1
```

O script irá:
- ✅ Verificar permissões de administrador
- ✅ Instalar o certificado CA no Windows
- ✅ Verificar a instalação

✅ **Verificar**: Execute `certutil -store "Root" | Select-String "KafkaCA"`

---

### 3️⃣ Executar Testes

```powershell
cd C:\DEV\ProjetosPessoais\XUnitAssured.Net\src\XUnitAssured.Kafka.Samples.Local.Test
dotnet test --filter "Auth05_SaslSsl_ShouldSucceed"
```

**Resultado esperado**: ✅ **Test Passed**

---

## 📋 Pré-requisitos

- [x] Kafka rodando no Docker/Podman (porta 39093)
- [x] Certificados gerados em `./secrets/`
- [x] PowerShell com permissões de Administrador

---

## 🔧 Comandos Úteis

### Verificar Kafka está rodando
```bash
# No WSL/Linux
podman ps | grep kafka
curl -v telnet://kafka:39093
```

### Verificar configuração
```powershell
# Hostname resolve para 127.0.0.1?
ping kafka

# Porta 39093 acessível?
Test-NetConnection kafka -Port 39093

# Certificado instalado?
certutil -store "Root" | Select-String "KafkaCA"
```

### Desinstalar certificado (após testes)
```powershell
.\uninstall-ca-cert.ps1
```

---

## ❓ Problemas Comuns

### "Access Denied" ao instalar certificado
➡️ Execute PowerShell como **Administrador**

### "Certificate not found"
➡️ Gere os certificados primeiro:
```bash
cd scripts
./generate_ssl_certs.sh
```

### Teste ainda falha
➡️ Limpe o cache DNS:
```powershell
ipconfig /flushdns
```

➡️ Verifique os logs do Kafka:
```bash
podman logs kafka
```

---

## 📚 Documentação Completa

- [QUICK-FIX-SSL.md](QUICK-FIX-SSL.md) - Guia detalhado de troubleshooting
- [README-SSL-CONFIG.md](README-SSL-CONFIG.md) - Documentação técnica completa

---

## ✅ Checklist de Configuração

- [ ] Hostname `kafka` adicionado ao hosts
- [ ] Certificado CA instalado no Windows
- [ ] Kafka rodando na porta 39093
- [ ] Teste `ping kafka` responde de 127.0.0.1
- [ ] Teste `Test-NetConnection kafka -Port 39093` bem-sucedido

---

**Pronto para testar!** 🎉
