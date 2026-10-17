# Security Policy

## Supported versions

Security fixes go into the latest release, and only into it. Each fix ships as a new version of the affected packages.

| Version | Supported |
|---------|-----------|
| Latest 6.x | Yes |
| Older 6.x, 5.x and earlier | No: upgrade to the latest ([UPGRADING.md](UPGRADING.md) covers 5.x) |

## Reporting a vulnerability

**Please do not open a public issue for a vulnerability.** Report it privately through GitHub instead:

1. Open the repository's [Security tab](https://github.com/andrewBezerra/xunit-assured-net/security).
2. Choose **Report a vulnerability**.

Include the affected package and version, what an attacker can do, and the steps or a test that reproduces it. You will get a reply on the report itself. Once a fix is published, the advisory is made public, with credit to you unless you prefer otherwise.

## What is not a vulnerability

The certificates, keys and passwords committed under `src/XUnitAssured.Kafka.Samples.Remote.Test/docker`, including its `testsettings.json` files, are **test-only fixtures** for local brokers started by the compose files. They protect nothing real.
