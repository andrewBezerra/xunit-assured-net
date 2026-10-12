# KRaft-OAUTH_OIDC

Kafka (KRaft) with SASL/OAUTHBEARER, validating tokens issued by a Keycloak.

```bash
podman-compose up -d      # or: docker compose up -d
```

| | |
|---|---|
| Kafka | `localhost:9092` (SASL_PLAINTEXT, OAUTHBEARER) |
| Keycloak | http://localhost:8080 (admin / admin) |
| kafka-ui | http://localhost:8090 |
| OAuth clients | `kafka-client` / `kafka-client-secret` (tests), `kafka-broker` / `kafka-broker-secret` (the broker itself) |

`keycloak-config` creates the realm `kafka`, the clients and some users (`scripts/configure_keycloak.sh`).
The broker waits for the realm's signing keys before it starts (`scripts/format_storage.sh`): started
earlier, it would reject every token for an hour.

`testsettings.json` connects as `kafka-client`. The Kafka client asks Keycloak for a token and renews
it by itself:

```json
"authentication": {
  "type": "OAuthBearer",
  "oAuthBearer": {
    "tokenEndpointUrl": "http://localhost:8080/realms/kafka/protocol/openid-connect/token",
    "clientId": "kafka-client",
    "clientSecret": "kafka-client-secret",
    "useSsl": false
  }
}
```

**What the broker needs, if you adapt this to your own setup:**
- `sasl.oauthbearer.jwks.endpoint.url` (and `sasl.oauthbearer.token.endpoint.url` for its own login)
  as broker settings. The `oauth.*` JAAS options seen in some guides belong to Strimzi; Apache Kafka's
  `OAuthBearerValidatorCallbackHandler` ignores them.
- `sasl.oauthbearer.expected.audience`: Keycloak's tokens carry `"aud": "account"`, and Kafka rejects
  any `aud` when no audience is expected.
- No issuer is checked: a token asked for from the host has `http://localhost:8080/...` as issuer, and
  one asked for inside the network has `http://keycloak:8080/...`.
- Listener names without `_`: the image turns each `_` of a `KAFKA_LISTENER_NAME_*` variable into `.`.
