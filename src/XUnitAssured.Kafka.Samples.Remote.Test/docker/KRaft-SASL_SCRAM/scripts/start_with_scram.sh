#!/bin/bash
# Sobe o Kafka com SASL/SCRAM (SHA-256 e SHA-512) sem ACLs (#177).
#
# Os usuários SCRAM são criados na formatação do storage, antes de o broker subir, como no
# exemplo KRaft-SASL_SCRAM-512: criá-los depois, com kafka-configs, exige um cliente que já se
# autentique no próprio listener SCRAM. Também escreve /tmp/client.properties, que o healthcheck
# do docker-compose.yml usa para falar com o broker.
set -e

. /etc/confluent/docker/bash-config

echo "===> Configuring Kafka..."
/etc/confluent/docker/configure

echo "===> Formatting Kafka storage with the SCRAM users..."
kafka-storage format \
  --add-scram 'SCRAM-SHA-256=[name=admin,password=admin-secret]' \
  --add-scram 'SCRAM-SHA-512=[name=admin,password=admin-secret]' \
  --add-scram 'SCRAM-SHA-256=[name=user1,password=user1-password]' \
  --add-scram 'SCRAM-SHA-512=[name=user1,password=user1-password]' \
  --ignore-formatted \
  -t "$CLUSTER_ID" \
  -c /etc/kafka/kafka.properties
echo "===> SCRAM users created: admin, user1 (SCRAM-SHA-256 and SCRAM-SHA-512)"

cat > /tmp/client.properties <<PROPS
security.protocol=SASL_PLAINTEXT
sasl.mechanism=SCRAM-SHA-256
sasl.jaas.config=org.apache.kafka.common.security.scram.ScramLoginModule required username="admin" password="admin-secret";
PROPS

exec /etc/confluent/docker/run
