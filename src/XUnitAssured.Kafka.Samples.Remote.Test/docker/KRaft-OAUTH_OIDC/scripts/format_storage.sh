#!/bin/bash
set -e

file_path="/tmp/clusterID/clusterID"
interval=5

echo "===> Waiting for cluster ID..."
while [ ! -e "$file_path" ] || [ ! -s "$file_path" ]; do
  echo "Waiting..."
  sleep $interval
done

echo "===> Cluster ID found: $(cat $file_path)"

# The broker fetches the realm's signing keys (JWKS) when it starts, and again only an hour later.
# Started before keycloak-config has created the realm, it would reject every token for an hour,
# closing the connection with nothing in its log. podman-compose does not wait for depends_on
# conditions, so wait here, whatever the engine (#179).
jwks_path="/realms/kafka/protocol/openid-connect/certs"
echo "===> Waiting for the Keycloak realm's keys at http://keycloak:8080${jwks_path}..."
until (exec 3<>/dev/tcp/keycloak/8080 &&
       printf 'GET %s HTTP/1.0\r\nHost: keycloak\r\n\r\n' "$jwks_path" >&3 &&
       head -1 <&3 | grep -q ' 200 ') 2>/dev/null; do
  sleep $interval
done
echo "===> Keycloak realm ready"

# Load Confluent configuration
. /etc/confluent/docker/bash-config

echo "===> Configuring Kafka..."
/etc/confluent/docker/configure

# Format storage
echo "===> Formatting Kafka storage..."
kafka-storage format \
  --ignore-formatted \
  -t $(cat "$file_path") \
  -c /etc/kafka/kafka.properties

echo "===> Kafka storage formatted successfully!"
