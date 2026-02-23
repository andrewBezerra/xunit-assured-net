# Guia Rápido: Testando Conexão Kafka

## ✅ Passo 1: Verificar se Kafka está rodando

```bash
docker ps | grep kafka
```

**Esperado:**
```
kafka           Running
zookeeper       Running
schema-registry Running
kafka-ui        Running
```

## ✅ Passo 2: Testar porta 29092

```bash
# Windows PowerShell
Test-NetConnection -ComputerName localhost -Port 29092

# Ou com netstat
netstat -an | findstr "29092"
```

**Esperado:**
```
TCP    0.0.0.0:29092          0.0.0.0:0              LISTENING
```

## ✅ Passo 3: Listar tópicos via console

```bash
docker exec -it kafka kafka-topics --list --bootstrap-server localhost:9092
```

**Esperado:**
- Lista de tópicos ou mensagem vazia (OK se não houver tópicos ainda)

## ✅ Passo 4: Testar produção de mensagem

```bash
# Produzir mensagem de teste
echo "test message" | docker exec -i kafka kafka-console-producer \
  --bootstrap-server localhost:9092 \
  --topic test-connection

# Consumir mensagem
docker exec -it kafka kafka-console-consumer \
  --bootstrap-server localhost:9092 \
  --topic test-connection \
  --from-beginning \
  --max-messages 1 \
  --timeout-ms 5000
```

**Esperado:**
```
test message
Processed a total of 1 messages
```

## ✅ Passo 5: Verificar Kafka UI

1. Abra: http://localhost:8083
2. Selecione cluster: **local**
3. Vá em **Brokers**
4. Verifique status: **CONNECTED**

## ✅ Passo 6: Executar testes

```bash
cd XUnitAssured.Kafka.Samples.Local.Test
dotnet test --logger "console;verbosity=detailed"
```

## 🔧 Troubleshooting

### Kafka não está rodando?
```bash
docker-compose up -d
```

### Porta 29092 não está aberta?
Verifique docker-compose.yml:
```yaml
ports:
  - "29092:29092"  # ← Esta linha deve existir
```

### Kafka UI não abre?
```bash
docker logs kafka-ui
```

### Testes falham com timeout?
1. Verifique se Kafka está respondendo (passos acima)
2. Aumente timeout no testsettings.json
3. Verifique logs do Kafka:
   ```bash
   docker logs kafka
   ```

## 📊 Monitoramento

### Verificar consumer groups
```bash
docker exec -it kafka kafka-consumer-groups \
  --bootstrap-server localhost:9092 \
  --list
```

### Ver detalhes do grupo de teste
```bash
docker exec -it kafka kafka-consumer-groups \
  --bootstrap-server localhost:9092 \
  --group xunit-test-consumer-group \
  --describe
```

### Limpar tópico de teste
```bash
docker exec -it kafka kafka-topics --delete \
  --bootstrap-server localhost:9092 \
  --topic xunit-test-topic
```

## ✅ Checklist Final

- [ ] Docker está rodando
- [ ] Container `kafka` está UP
- [ ] Porta 29092 está aberta (LISTENING)
- [ ] Kafka UI está acessível
- [ ] Consegue produzir e consumir mensagem via console
- [ ] testsettings.json aponta para `localhost:29092`

Se todos os itens acima estiverem OK, os testes devem funcionar! 🎉
