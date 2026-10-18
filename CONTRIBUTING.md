# Contributing to XUnitAssured.Net

Contributions are welcome, issues and pull requests alike. By taking part you agree to the [Code of Conduct](CODE_OF_CONDUCT.md). Security issues go through the [security policy](SECURITY.md), not a public issue.

## Reporting a bug or asking for a feature

Open an [issue](https://github.com/andrewBezerra/xunit-assured-net/issues/new/choose) with the form that fits. For a bug, the most useful thing is a minimal test that reproduces it, with the package, its version and the target framework.

## Building and testing

You need the .NET 10 SDK, plus the .NET 8 and 9 runtimes to run the tests on every target framework the packages ship for.

```bash
dotnet build src/XUnitAssured.Net.sln
dotnet test src/XUnitAssured.Tests --filter "Requires!=Network&Requires!=Broker&Requires!=Browser&Requires!=Camunda&Requires!=Docker"
```

The filter skips the tests that need something outside the process, each marked with a `Requires` trait:

| Trait | Needs |
|-------|-------|
| `Network` | Internet access |
| `Broker` | A RabbitMQ broker on `127.0.0.1:5672` and a Kafka broker on `127.0.0.1:9092` |
| `Browser` | Chromium for Playwright: `pwsh src/XUnitAssured.Tests/bin/Debug/net10.0/playwright.ps1 install chromium` |
| `Camunda` | A Camunda 8 cluster on `localhost:8080` |
| `Docker` | Docker, for Testcontainers |

CI runs all of them on every pull request, so a contribution does not have to. The build-and-test job, which uses the filter above, is the one that has to pass.

To run the Kafka samples locally, the compose files under `src/XUnitAssured.Kafka.Samples.Remote.Test/docker` start a broker per authentication mode. On Windows, use `127.0.0.1` rather than `localhost` for Kafka and RabbitMQ: the README's [configuration section](README.md#configuration-testsettingsjson) explains why. The certificates and keys committed there are **test-only fixtures** for those local brokers, not credentials for anything real.

## Code

- **Warnings are errors** in every project (`TreatWarningsAsErrors`), analyzers included. Fix the warning rather than suppress it; if a suppression is really needed, say why next to it.
- **The public API is documented** with XML comments, which ship in the packages.
- **Bug fixes come with a test** that fails without the fix.
- **Adding a protocol package?** Read [Adding a protocol package](README.md#adding-a-protocol-package) first: the packages must compose in one scenario without their verbs colliding.

## Pull requests

**Merging to `main` publishes to NuGet**, so every pull request decides a version:

1. Raise `<Version>` in `src/Directory.Build.props`, following [semantic versioning](https://semver.org/).
2. Add `docs/releases/<version>.md` with what ships. The GitHub release is published with it as its body.
3. Add the same entry to the top of [CHANGELOG.md](CHANGELOG.md), marked `Current`.

The **Versão nova** check fails without the first two. On merge, the release workflow creates the tag and the GitHub release, then publishes to NuGet after a manual approval.

Keep a pull request to one change. Commit messages use a conventional prefix with the package as scope, such as `fix(kafka): …`, `test: …` or `docs: …`.
