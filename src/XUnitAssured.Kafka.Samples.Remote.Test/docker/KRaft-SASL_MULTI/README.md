# KRaft-SASL_MULTI

One Kafka broker (KRaft, no ZooKeeper) that takes SASL/PLAIN, SCRAM-SHA-256 and SCRAM-SHA-512 on the
same listener, to test several mechanisms side by side. No SSL, no ACLs.

```bash
podman-compose up -d      # or: docker compose up -d
```

| | |
|---|---|
| Kafka | `localhost:9096` (SASL_PLAINTEXT) |
| kafka-ui | http://localhost:8080 |
| Users (every mechanism) | `admin` / `admin-secret`, `producer` / `producer-secret`, `consumer` / `consumer-secret` |

The PLAIN users are listed in `docker-compose.yml`; the SCRAM users are created when the storage is
formatted, by `scripts/start_multi.sh`, so they exist as soon as Kafka is up.

`testsettings.json` connects with PLAIN. For SCRAM, change `authentication`:

```json
"authentication": { "type": "SaslScram256", "saslScram": { "username": "admin", "password": "admin-secret", "useSsl": false } }
```
