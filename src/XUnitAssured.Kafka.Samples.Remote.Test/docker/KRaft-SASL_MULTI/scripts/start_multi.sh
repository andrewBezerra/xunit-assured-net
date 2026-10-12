#!/bin/bash
# Sobe o Kafka com SASL/PLAIN, SCRAM-SHA-256 e SCRAM-SHA-512 no mesmo listener (#179).
#
# Os usuários SCRAM são criados na formatação do storage, antes de o broker subir; os do PLAIN
# vêm do JAAS do docker-compose.yml. Também escreve /tmp/client.properties para o healthcheck.
set -e

. /etc/confluent/docker/bash-config

echo "===> Configuring Kafka..."
/etc/confluent/docker/configure

echo "===> Formatting Kafka storage with the SCRAM users..."
kafka-storage format \
  --add-scram 'SCRAM-SHA-256=[name=admin,password=admin-secret]' \
  --add-scram 'SCRAM-SHA-512=[name=admin,password=admin-secret]' \
  --add-scram 'SCRAM-SHA-256=[name=producer,password=producer-secret]' \
  --add-scram 'SCRAM-SHA-512=[name=producer,password=producer-secret]' \
  --add-scram 'SCRAM-SHA-256=[name=consumer,password=consumer-secret]' \
  --add-scram 'SCRAM-SHA-512=[name=consumer,password=consumer-secret]' \
  --ignore-formatted \
  -t "$CLUSTER_ID" \
  -c /etc/kafka/kafka.properties
echo "===> SCRAM users created: admin, producer, consumer (SCRAM-SHA-256 and SCRAM-SHA-512)"

cat > /tmp/client.properties <<PROPS
security.protocol=SASL_PLAINTEXT
sasl.mechanism=PLAIN
sasl.jaas.config=org.apache.kafka.common.security.plain.PlainLoginModule required username="admin" password="admin-secret";
PROPS

exec /etc/confluent/docker/run
