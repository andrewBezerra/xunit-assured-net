# XUnitAssured.Mcp

MCP (Model Context Protocol) server for AI-assisted test generation with the [XUnitAssured.Net](https://github.com/andrewBezerra/XUnitAssured.Net) framework. Integrates with GitHub Copilot Chat, VS Code, Visual Studio, and any MCP-compatible AI client via stdio transport.

## Installation

### Option A - Via dnx (recommended)

Requires [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) or later.

Add to your `.mcp.json` (repo root, `~/.mcp.json`, or `.vscode/mcp.json`):

```json
{
  "servers": {
    "xunitassured": {
      "type": "stdio",
      "command": "dnx",
      "args": ["XUnitAssured.Mcp@5.0.0", "--yes"]
    }
  }
}
```

Restart your IDE - the tools will be available immediately.

### Option B - Via dotnet tool

```bash
dotnet tool install --global XUnitAssured.Mcp
```

Then configure `.mcp.json`:

```json
{
  "servers": {
    "xunitassured": {
      "type": "stdio",
      "command": "xunitassured-mcp",
      "args": []
    }
  }
}
```

## Available Tools (10)

### Playwright (3 tools)

| Tool | Description |
|------|-------------|
| `translate_playwright_to_dsl` | Translates Playwright C# Inspector code to XUnitAssured fluent DSL |
| `translate_playwright_to_test` | Generates a complete Given/When/Then test from Playwright code |
| `list_xunitassured_dsl_methods` | Lists all Playwright DSL methods with Playwright equivalents |

### HTTP (3 tools)

| Tool | Description |
|------|-------------|
| `generate_http_test` | Scaffolds an HTTP test method (GET, POST, PUT, DELETE) |
| `generate_http_crud_tests` | Generates 5 CRUD test methods for a REST resource |
| `list_http_dsl_methods` | Lists all HTTP DSL methods (request, auth, assert) |

### Kafka (4 tools)

| Tool | Description |
|------|-------------|
| `generate_kafka_produce_test` | Scaffolds a Kafka produce test method |
| `generate_kafka_consume_test` | Scaffolds a Kafka consume test method |
| `generate_kafka_produce_consume_test` | Generates a round-trip produce/consume test |
| `list_kafka_dsl_methods` | Lists all Kafka DSL methods (produce, consume, auth, assert) |

## Usage Examples

In GitHub Copilot Chat (Agent mode):

> "Generate CRUD tests for /api/products with fields name:string, price:decimal"

> "Translate this Playwright code to XUnitAssured DSL"

> "Generate a Kafka produce-consume round-trip test for the orders topic"

> "List all HTTP DSL methods"

## Supported Clients

| Client | Support |
|--------|---------|
| GitHub Copilot Chat (VS / VS Code) | Agent mode |
| GitHub Copilot Coding Agent | via copilot-setup-steps.yml |
| Claude Desktop | via claude_desktop_config.json |
| Cursor | via MCP settings |
| Any MCP-compatible client | stdio transport |

## Links

- [GitHub Repository](https://github.com/andrewBezerra/XUnitAssured.Net)
- [Full Documentation](https://github.com/andrewBezerra/XUnitAssured.Net#readme)
- [Report Issues](https://github.com/andrewBezerra/XUnitAssured.Net/issues)

## License

Apache-2.0 - see [LICENSE.md](https://github.com/andrewBezerra/XUnitAssured.Net/blob/main/LICENSE.md)
