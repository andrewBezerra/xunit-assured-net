# Kafka Docker Configurations

Este diretório contém diferentes configurações do Kafka para testes locais.

**✨ Todas as configurações agora incluem:**
- ✅ **ACLs habilitadas** (StandardAuthorizer)
- ✅ **Super Users limitados** (admin, client, tool, ANONYMOUS)
- ✅ **Schema Registry** (porta 8081)
- ✅ **Kafka UI integrado** com Schema Registry
- ✅ **Replication Factor completo** (6 variáveis para single-broker)

📚 **Documentação Completa**:
- [**COMPLETE-GUIDE.md**](COMPLETE-GUIDE.md) - Guia completo de todas as configurações
- [CONFIGURATION-UPDATES.md](CONFIGURATION-UPDATES.md) - Resumo das atualizações
- [SINGLE-BROKER-REPLICATION-FIX.md](SINGLE-BROKER-REPLICATION-FIX.md) - Guia de replication factor
- [PODMAN-WSL-TROUBLESHOOTING.md](PODMAN-WSL-TROUBLESHOOTING.md) - Troubleshooting Podman + WSL

---

## 📋 Configurações Disponíveis

### 1. **KRaft-PLAINTEXT** (Mais Simples) ✅
**Recomendado para**: Desenvolvimento rápido e prototipagem

```json
{
  "bootstrapServers": "localhost:9092",
  "securityProtocol": "Plaintext"
}
```

**Características**:
- ✅ Sem autenticação
- ✅ Sem SSL/TLS
- ✅ Porta: 9092
- ✅ Setup em 30 segundos

📚 [Ver documentação completa](KRaft-PLAINTEXT/README.md)

---

### 2. **KRaft-SASL_PLAIN** (Autenticação Simples)
**Recomendado para**: Testar autenticação básica sem complexidade

```json
{
  "bootstrapServers": "localhost:9093",
  "securityProtocol": "SaslPlaintext",
  "authentication": {
    "type": "SaslPlain",
    "saslPlain": {
      "username": "admin",
      "password": "admin-secret",
      "useSsl": false
    }
  }
}
```

**Características**:
- ✅ SASL/PLAIN autenticação (username/password)
- ❌ Sem SSL/TLS (tráfego não criptografado)
- ✅ Porta: 9093
- ⚠️ Credenciais transmitidas em base64

📚 [Ver documentação completa](KRaft-SASL_PLAIN/README.md)

---

### 3. **KRaft-SASL_SCRAM** (Autenticação Forte)
**Recomendado para**: Testar SCRAM-SHA-256/512 sem SSL

```json
{
  "bootstrapServers": "localhost:9094",
  "securityProtocol": "SaslPlaintext",
  "authentication": {
    "type": "SaslScram256",
    "saslScram": {
      "username": "admin",
      "password": "admin-secret",
      "useSsl": false
    }
  }
}
```

**Características**:
- ✅ SASL/SCRAM-SHA-256 e SCRAM-SHA-512 autenticação
- ❌ Sem SSL/TLS (tráfego não criptografado)
- ✅ Porta: 9094
- ✅ Credenciais hasheadas (mais seguro que PLAIN)

📚 [Ver documentação completa](KRaft-SASL_SCRAM/README.md)

---

### 4. **KRaft-SASL_SCRAM-256** (Autenticação + ACLs)
**Recomendado para**: Simular ambiente de produção com autorização

```json
{
  "bootstrapServers": "localhost:39092",
  "securityProtocol": "SaslPlaintext",
  "authentication": {
    "type": "SaslScram256",
    "saslScram": {
      "username": "admin",
      "password": "admin-secret",
      "useSsl": false
    }
  }
}
```

**Características**:
- ✅ SASL/SCRAM-SHA-256 autenticação
- ✅ ACLs habilitadas (StandardAuthorizer)
- ✅ Múltiplos usuários pré-configurados
- ❌ Sem SSL/TLS (tráfego não criptografado)
- ✅ Porta: 39092
- ✅ Configuração production-like

📚 [Ver documentação completa](KRaft-SASL_SCRAM-256/README.md)

---

### 5. **KRaft-SASL_SCRAM-512** (Máxima Segurança de Autenticação)
**Recomendado para**: Máxima segurança de autenticação sem SSL

```json
{
  "bootstrapServers": "localhost:39092",
  "securityProtocol": "SaslPlaintext",
  "authentication": {
    "type": "SaslScram512",
    "saslScram": {
      "username": "admin",
      "password": "admin-secret",
      "useSsl": false
    }
  }
}
```

**Características**:
- ✅ SASL/SCRAM-SHA-512 autenticação (hash 512-bit)
- ✅ Mais seguro que SHA-256
- ✅ ACLs permissivas (desenvolvimento)
- ❌ Sem SSL/TLS (tráfego não criptografado)
- ⚠️ Porta: 39092 (compartilhada com SCRAM-256)
- ⚡ Levemente mais lento que SHA-256

📚 [Ver documentação completa](KRaft-SASL_SCRAM-512/README.md)

---

### 6. **KRaft-SASLSSL** (Máxima Segurança)
**Recomendado para**: Simular ambiente de produção

```json
{
  "bootstrapServers": "kafka:39093",
  "securityProtocol": "SaslSsl",
  "authentication": {
    "type": "SaslScram512",
    "saslScram": {
      "username": "admin",
      "password": "admin-secret"
    }
  }
}
```

**Características**:
- ✅ SASL/SCRAM-SHA-512 autenticação
- ✅ SSL/TLS encryption
- ✅ Porta: 39093
- ⚠️ Requer certificados e hostname `kafka`

📚 [Ver documentação completa](KRaft-SASLSSL/README.md)

---

### 7. **Zookeeper** (Legado)
**Recomendado para**: Compatibilidade com sistemas antigos

```json
{
  "bootstrapServers": "localhost:29092",
  "securityProtocol": "Plaintext"
}
```

**Características**:
- ✅ Configuração clássica com Zookeeper
- ✅ Schema Registry incluído
- ✅ Porta: 29092
- ⚠️ Zookeeper será descontinuado (preferir KRaft)

---

## 🚀 Como Usar

### Passo 1: Escolher Configuração

Acesse o diretório da configuração desejada:

```bash
# Para desenvolvimento simples (recomendado)
cd KRaft-PLAINTEXT

# Para testes de segurança
cd KRaft-SASLSSL

# Para compatibilidade com Zookeeper
cd Zookeeper
```

### Passo 2: Iniciar Kafka

```bash
docker-compose up -d
```

Ou com Podman (WSL):
```bash
podman-compose up -d
```

### Passo 3: Configurar testsettings.json

Copie a configuração correspondente para o arquivo raiz:

**Para KRaft-PLAINTEXT**:
```json
{
  "kafka": {
    "bootstrapServers": "localhost:9092",
    "securityProtocol": "Plaintext",
    "authentication": { "type": "None" }
  }
}
```

**Para KRaft-SASLSSL**:
```json
{
  "kafka": {
    "bootstrapServers": "kafka:39093",
    "securityProtocol": "SaslSsl",
    "sslCaLocation": "C:/DEV/.../ca-cert.pem",
    "enableSslCertificateVerification": true,
    "authentication": {
      "type": "SaslScram512",
      "saslScram": {
        "username": "admin",
        "password": "admin-secret",
        "useSsl": true
      }
    }
  }
}
```

### Passo 4: Executar Testes

```powershell
cd C:\DEV\ProjetosPessoais\XUnitAssured.Net\src\XUnitAssured.Kafka.Samples.Local.Test
dotnet test
```

---

## 📊 Comparação Rápida

| Configuração | Porta | Auth | ACLs | SSL | Zookeeper | KRaft | Complexidade | Uso Recomendado |
|--------------|-------|------|------|-----|-----------|-------|--------------|-----------------|
| **KRaft-PLAINTEXT** | 9092 | ❌ | ❌ | ❌ | ❌ | ✅ | ⭐ Simples | Desenvolvimento rápido |
| **KRaft-SASL_PLAIN** | 9093 | ✅ PLAIN | ❌ | ❌ | ❌ | ✅ | ⭐⭐ Média | Autenticação básica |
| **KRaft-SASL_SCRAM** | 9094 | ✅ SCRAM-256/512 | ❌ | ❌ | ❌ | ✅ | ⭐⭐ Média | Autenticação forte |
| **KRaft-SASL_SCRAM-256** | 39092 | ✅ SCRAM-256 | ✅ | ❌ | ❌ | ✅ | ⭐⭐⭐ Complexa | Auth + Authorization |
| **KRaft-SASL_SCRAM-512** | 39092⚠️ | ✅ SCRAM-512 | ✅ | ❌ | ❌ | ✅ | ⭐⭐⭐ Complexa | Máxima auth security |
| **KRaft-SASLSSL** | 39093 | ✅ SCRAM-512 | ❌ | ✅ | ❌ | ✅ | ⭐⭐⭐ Complexa | Simular produção |
| **Zookeeper** | 29092 | ❌ | ❌ | ❌ | ✅ | ❌ | ⭐⭐ Média | Compatibilidade legado |

⚠️ **SCRAM-256 e SCRAM-512 compartilham a porta 39092** - só uma pode rodar por vez!

---

## 🎯 Quando Usar Cada Uma?

### Use **KRaft-PLAINTEXT** quando:
- 🚀 Desenvolvimento rápido
- 🧪 Testes de integração básicos
- 📚 Aprendendo Kafka
- 🔧 Prototipagem

### Use **KRaft-SASL_PLAIN** quando:
- 🔐 Testando lógica de autenticação
- 📝 Validando credenciais
- 🧪 Desenvolvimento de features de segurança
- ⚡ Quer autenticação sem complexidade de SSL

### Use **KRaft-SASL_SCRAM** quando:
- 🔐 Testando SCRAM-SHA-256 ou SCRAM-SHA-512
- 📝 Validando autenticação forte (hashed)
- 🧪 Desenvolvimento de features de segurança
- ⚡ Quer autenticação melhor que PLAIN sem SSL

### Use **KRaft-SASL_SCRAM-256** quando:
- 🔐 Testando SCRAM com ACLs habilitadas
- 📝 Validando autorização (quem pode acessar o quê)
- 🧪 Simulando ambiente production-like
- ⚡ Quer autenticação + autorização sem SSL
- 👥 Testando múltiplos service accounts

### Use **KRaft-SASL_SCRAM-512** quando:
- 🔒 Precisar de **máxima segurança de autenticação**
- 📝 Compliance exigir SHA-512
- 🧪 Testar diferenças entre SHA-256 e SHA-512
- ⚡ Performance não for crítica
- 🎯 Ambiente mais permissivo (ACLs não restritivas)

### Use **KRaft-SASLSSL** quando:
- 🔒 Testando autenticação + criptografia
- 🛡️ Simulando ambiente de produção
- 📝 Documentando segurança completa
- ✅ Validando certificados SSL/TLS

### Use **Zookeeper** quando:
- 🔄 Migrando de versão antiga
- 🧩 Testando compatibilidade
- 📦 Usando Schema Registry (versão antiga)

---

## 🛠️ Comandos Úteis

### Verificar Status
```bash
# Ver containers rodando
docker ps

# Ver logs do Kafka
docker logs kafka -f

# Verificar saúde do broker
docker exec kafka kafka-broker-api-versions --bootstrap-server kafka:19092
```

### Gerenciar Tópicos
```bash
# Listar tópicos
docker exec kafka kafka-topics --bootstrap-server kafka:19092 --list

# Criar tópico
docker exec kafka kafka-topics --bootstrap-server kafka:19092 \
  --create --topic test --partitions 3 --replication-factor 1

# Deletar tópico
docker exec kafka kafka-topics --bootstrap-server kafka:19092 \
  --delete --topic test
```

### Limpar Tudo
```bash
# Parar e remover containers
docker-compose down -v

# Remover volumes órfãos
docker volume prune
```

---

## 🌐 UIs Disponíveis

Todas as configurações incluem **Kafka UI**:

- **URL**: http://localhost:8080
- **Features**: 
  - 📊 Visualizar tópicos e mensagens
  - 👥 Monitorar consumer groups
  - 📈 Métricas do cluster
  - 🔍 Buscar mensagens

**Para KRaft-SASLSSL**: A UI é acessível em http://localhost:8080 e está pré-configurada com as credenciais.

---

## ⚙️ Configuração Atual

**Arquivo**: `XUnitAssured.Kafka.Samples.Local.Test\testsettings.json`

Para trocar de configuração:
1. Parar o Kafka atual: `docker-compose down`
2. Iniciar nova configuração: `cd <config-dir> && docker-compose up -d`
3. Atualizar `testsettings.json` com as configurações corretas
4. Executar testes: `dotnet test`

---

## 📚 Documentação Individual

- [KRaft-PLAINTEXT README](KRaft-PLAINTEXT/README.md) - Sem autenticação, sem SSL
- [KRaft-SASL_PLAIN README](KRaft-SASL_PLAIN/README.md) - Autenticação SASL/PLAIN sem SSL
- [KRaft-SASL_SCRAM README](KRaft-SASL_SCRAM/README.md) - Autenticação SASL/SCRAM sem SSL
- [KRaft-SASL_SCRAM-256 README](KRaft-SASL_SCRAM-256/README.md) - SASL/SCRAM-SHA-256 com ACLs
- [KRaft-SASL_SCRAM-512 README](KRaft-SASL_SCRAM-512/README.md) - SASL/SCRAM-SHA-512 máxima segurança
- [KRaft-SASLSSL README](KRaft-SASLSSL/README.md) - SASL/SCRAM-SHA-512 com SSL
- [KRaft-SASLSSL Quick Fix Guide](KRaft-SASLSSL/QUICK-FIX-SSL.md) - Resolver problemas SSL

---

## ✅ Checklist de Setup

- [ ] Docker/Podman instalado e rodando
- [ ] Configuração escolhida (recomendado: KRaft-PLAINTEXT)
- [ ] `docker-compose up -d` executado
- [ ] Kafka healthy (verificar com `docker ps`)
- [ ] `testsettings.json` configurado corretamente
- [ ] Testes passando

---

**Dica**: Comece com **KRaft-PLAINTEXT** para familiarizar-se, depois avance para **KRaft-SASLSSL** quando precisar testar segurança.
