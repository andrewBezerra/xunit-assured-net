# XUnitAssured.Jsonata

JSONata expressions for XUnitAssured: select, transform and assert on the JSON a step returns.

```csharp
var resposta = await Given(api).ApiResource("/pedidos").Post(pedido).ExecuteAsync();
var saida = resposta.GetResult().ToJsonObject();   // { status, headers, body }

JsonataExpression.AssertTrue("status = 201 and $exists(body.id)", saida);
var total = JsonataExpression.Evaluate("$sum(body.itens.(preco * qtd))", saida).Required("total");
var caros = JsonataExpression.Evaluate("body.itens[preco > 100].sku", saida).Value;
```

Variables are passed by name and read as `$name`:

```csharp
JsonataExpression.AssertTrue("body.cliente.id = $clienteId", saida,
    new Dictionary<string, JsonNode?> { ["clienteId"] = clienteId });
```

`ToJsonObject()` exists on the HTTP, Kafka and RabbitMQ results, with `body` in all three (the JSON of
the body when it is JSON, its text otherwise).

## What this package adds to JSONata

- **A time limit** (5 s by default, `JsonataOptions.Timeout`) and a **recursion limit** (250): a
  runaway expression fails the test instead of hanging it or crashing the process.
- **`$jwt(token)`**: the claims of a JWT payload, without checking the signature.
- **Date formats are .NET formats** (`yyyy-MM-dd`), as in Jsonata.Net.Native. A picture in the XPath
  format of the JSONata docs (`[Y0001]-[M01]-[D01]`) in `$fromMillis`, `$toMillis` or `$now` fails with
  an error that says so, instead of returning wrong text. Number formats (`$formatNumber`,
  `$formatInteger`, `$parseInteger`) are also .NET formats.
- A variable cannot take the name of a function (`sum`, `count`, ...): it would hide it.

Built on [Jsonata.Net.Native](https://github.com/mikhail-barg/jsonata.net.native).
