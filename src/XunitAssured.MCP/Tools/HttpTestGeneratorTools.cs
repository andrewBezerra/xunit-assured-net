using System.ComponentModel;
using System.Text;

using ModelContextProtocol.Server;

namespace XUnitAssured.Mcp.Tools;

/// <summary>
/// MCP tools for generating XUnitAssured.Http REST API tests.
/// Provides tools to scaffold complete test methods, list the HTTP DSL API surface,
/// and generate assertion chains from API endpoint descriptions.
/// </summary>
[McpServerToolType]
public static class HttpTestGeneratorTools
{
	[McpServerTool(Name = "generate_http_test"),
	 Description("Generates a complete XUnitAssured.Http test method for a REST API endpoint. " +
	             "Produces a ready-to-paste [Fact] method with Given/When/Then structure. " +
	             "Example: method=GET, endpoint=/api/products/1, expectedStatus=200")]
	public static string GenerateHttpTest(
		[Description("HTTP method: GET, POST, PUT, PATCH, or DELETE")] string method,
		[Description("API endpoint path, e.g. /api/products or /api/products/1")] string endpoint,
		[Description("Expected HTTP status code, e.g. 200, 201, 404. Default: 200")] int expectedStatus = 200,
		[Description("JSON body for POST/PUT/PATCH as a C# anonymous object description, e.g. 'name=Laptop, price=999.99'. Leave empty for GET/DELETE.")] string? body = null,
		[Description("Test display name. Auto-generated if empty.")] string? displayName = null,
		[Description("Comma-separated list of JSON path assertions, e.g. '$.id>0, $.name=NotEmpty, $.price>0'. Leave empty for no assertions.")] string? assertions = null,
		[Description("Authentication type: none, basic, bearer, apikey. Default: none")] string auth = "none")
	{
		method = (method ?? "GET").Trim().ToUpperInvariant();
		endpoint = (endpoint ?? "/api/resource").Trim();
		auth = (auth ?? "none").Trim().ToLowerInvariant();

		var methodName = BuildMethodName(method, endpoint, expectedStatus);
		displayName ??= BuildDisplayName(method, endpoint, expectedStatus);

		var sb = new StringBuilder();

		sb.AppendLine($"\t[Fact(DisplayName = \"{displayName}\")]");
		sb.AppendLine($"\tpublic void {methodName}()");
		sb.AppendLine("\t{");

		// Body variable for POST/PUT/PATCH
		if (body != null && method is "POST" or "PUT" or "PATCH")
		{
			sb.AppendLine("\t\t// Arrange");
			sb.AppendLine("\t\tvar requestBody = new");
			sb.AppendLine("\t\t{");
			foreach (var pair in ParseBodyPairs(body))
			{
				sb.AppendLine($"\t\t\t{pair.Key} = {pair.Value},");
			}
			sb.AppendLine("\t\t};");
			sb.AppendLine();
		}

		// Given
		sb.AppendLine("\t\t// Act & Assert");
		sb.AppendLine("\t\tGiven()");

		// Auth
		switch (auth)
		{
			case "basic":
				sb.AppendLine("\t\t\t.ApiResource(\"" + endpoint + "\")");
				sb.AppendLine("\t\t\t.WithBasicAuth(\"username\", \"password\")");
				break;
			case "bearer":
				sb.AppendLine("\t\t\t.ApiResource(\"" + endpoint + "\")");
				sb.AppendLine("\t\t\t.WithBearerToken(\"your-token-here\")");
				break;
			case "apikey":
				sb.AppendLine("\t\t\t.ApiResource(\"" + endpoint + "\")");
				sb.AppendLine("\t\t\t.WithApiKey(\"X-API-Key\", \"your-api-key\")");
				break;
			default:
				sb.AppendLine("\t\t\t.ApiResource(\"" + endpoint + "\")");
				break;
		}

		// HTTP method
		switch (method)
		{
			case "GET":
				sb.AppendLine("\t\t\t.Get()");
				break;
			case "POST":
				sb.AppendLine(body != null ? "\t\t\t.Post(requestBody)" : "\t\t\t.Post()");
				break;
			case "PUT":
				sb.AppendLine("\t\t\t.Put(requestBody)");
				break;
			case "PATCH":
				sb.AppendLine("\t\t\t.Patch(requestBody)");
				break;
			case "DELETE":
				sb.AppendLine("\t\t\t.Delete()");
				break;
		}

		// When/Then
		sb.AppendLine("\t\t.When()");
		sb.AppendLine("\t\t\t.Execute()");
		sb.AppendLine("\t\t.Then()");
		sb.Append($"\t\t\t.AssertStatusCode({expectedStatus})");

		// Assertions
		if (!string.IsNullOrWhiteSpace(assertions))
		{
			foreach (var assertion in ParseAssertions(assertions))
			{
				sb.AppendLine();
				sb.Append($"\t\t\t{assertion}");
			}
		}

		sb.AppendLine(";");
		sb.AppendLine("\t}");

		return sb.ToString();
	}

	[McpServerTool(Name = "generate_http_crud_tests"),
	 Description("Generates a complete set of CRUD test methods (GET all, GET by id, POST create, PUT update, DELETE) " +
	             "for a REST API resource. Produces a full test class body with 5-6 test methods.")]
	public static string GenerateCrudTests(
		[Description("Base API path, e.g. /api/products")] string basePath,
		[Description("Resource name (singular), e.g. Product, User, Order")] string resourceName,
		[Description("Comma-separated fields for POST/PUT body, e.g. 'name:string, price:decimal, description:string'")] string? fields = null)
	{
		basePath = (basePath ?? "/api/resources").Trim().TrimEnd('/');
		resourceName = (resourceName ?? "Resource").Trim();
		var lowerName = resourceName.ToLowerInvariant();

		var sb = new StringBuilder();

		// GET all
		sb.AppendLine(GenerateHttpTest("GET", basePath, 200,
			displayName: $"GET all {lowerName}s should return 200 OK"));
		sb.AppendLine();

		// GET by ID
		sb.AppendLine(GenerateHttpTest("GET", $"{basePath}/1", 200,
			assertions: "$.id>0, $.name=NotEmpty",
			displayName: $"GET {lowerName} by ID should return {lowerName} details"));
		sb.AppendLine();

		// GET not found
		sb.AppendLine(GenerateHttpTest("GET", $"{basePath}/9999", 404,
			displayName: $"GET non-existent {lowerName} should return 404"));
		sb.AppendLine();

		// POST create
		var postBody = fields != null ? BuildBodyFromFields(fields) : $"name=New {resourceName}, description=Test {lowerName}";
		sb.AppendLine(GenerateHttpTest("POST", basePath, 201,
			body: postBody,
			assertions: "$.id>0",
			displayName: $"POST create {lowerName} should return 201 Created"));
		sb.AppendLine();

		// PUT update
		var putBody = fields != null ? BuildBodyFromFields(fields) : $"name=Updated {resourceName}, description=Updated {lowerName}";
		sb.AppendLine(GenerateHttpTest("PUT", $"{basePath}/1", 200,
			body: putBody,
			displayName: $"PUT update {lowerName} should return 200 OK"));
		sb.AppendLine();

		// DELETE
		sb.AppendLine(GenerateHttpTest("DELETE", $"{basePath}/1", 204,
			displayName: $"DELETE {lowerName} should return 204 No Content"));

		return sb.ToString();
	}

	[McpServerTool(Name = "list_http_dsl_methods", ReadOnly = true),
	 Description("Lists all available XUnitAssured.Http DSL methods for REST API testing. " +
	             "Use this as a reference when writing HTTP integration tests.")]
	public static string ListHttpDslMethods(
		[Description("Optional filter: 'request', 'auth', 'assert', 'all'. Default is 'all'.")] string filter = "all")
	{
		filter = (filter ?? "all").Trim().ToLowerInvariant();

		var sections = new List<string>();

		if (filter is "all" or "request")
		{
			sections.Add("""
			== Request Setup ==
			  Given()                                           Start a test scenario
			  Given(fixture)                                    Start with IHttpClientProvider fixture
			    .ApiResource("/api/endpoint")                   Set the target URL
			    .WithHttpClient(client)                         Use a custom HttpClient
			    .WithHeader("X-Custom", "value")                Add request header
			    .WithQueryParam("page", 1)                      Add query parameter
			    .WithTimeout(30)                                Set timeout in seconds

			== HTTP Methods ==
			    .Get()                                          HTTP GET
			    .Post(body)                                     HTTP POST with JSON body
			    .Post()                                         HTTP POST without body
			    .PostFormData(dict)                              POST form-urlencoded data
			    .Put(body)                                      HTTP PUT with JSON body
			    .Patch(body)                                    HTTP PATCH with JSON body
			    .Delete()                                       HTTP DELETE
			""");
		}

		if (filter is "all" or "auth")
		{
			sections.Add("""
			== Authentication ==
			    .WithBasicAuth("user", "pass")                  Basic authentication
			    .WithBearerToken("jwt-token")                   Bearer token auth
			    .WithApiKey("X-API-Key", "key-value")           API key in header
			    .WithApiKeyInQuery("api_key", "key-value")      API key in query string
			    .WithOAuth2ClientCredentials(config)             OAuth2 client credentials
			    .WithCertificate(cert)                           Client certificate auth
			    .WithCustomHeaderAuth("Header", "value")        Custom header auth
			""");
		}

		if (filter is "all" or "assert")
		{
			sections.Add("""
			== Execution & Assertions ==
			  .When().Execute()                                 Execute the request
			  .Then()
			    .AssertStatusCode(200)                           Assert HTTP status code
			    .AssertSuccess()                                 Assert IsValid = true
			    .ValidateContract<Product>()                     Validate JSON schema
			    .AssertJsonPath<int>("$.id", id => id.ShouldBe(1))          Assert JSON value
			    .AssertJsonPath<string>("$.name", n => n.ShouldNotBeEmpty()) Assert JSON string
			    .Extract(out var result)                         Capture result for later use
			    .Extract(r => myVar = r.StatusCode)             Capture via callback
			    .JsonPath<int>("$.id")                           Extract value from JSON
			""");
		}

		if (filter is "all")
		{
			sections.Add("""
			== Complete Example ==
			  Given()
			      .ApiResource("/api/products/1")
			      .Get()
			  .When()
			      .Execute()
			  .Then()
			      .AssertStatusCode(200)
			      .AssertJsonPath<string>("$.name", name => name.ShouldNotBeEmpty())
			      .ValidateContract<Product>();
			""");
		}

		return sections.Count > 0
			? string.Join("\n", sections)
			: $"Unknown filter '{filter}'. Use: request, auth, assert, or all.";
	}

	// ──────────────────────────────────────────────
	// Internal helpers
	// ──────────────────────────────────────────────

	private static string BuildMethodName(string method, string endpoint, int status)
	{
		// /api/products/1 → Products_1, /api/users → Users
		var parts = endpoint.Split('/', StringSplitOptions.RemoveEmptyEntries);
		var resource = parts.Length >= 2 ? parts[^1] : "Resource";
		if (int.TryParse(resource, out _) && parts.Length >= 3)
			resource = parts[^2] + "ById";

		resource = char.ToUpper(resource[0]) + resource[1..];

		return $"{method}_{resource}_ShouldReturn{status}";
	}

	private static string BuildDisplayName(string method, string endpoint, int status)
	{
		var statusText = status switch
		{
			200 => "200 OK",
			201 => "201 Created",
			204 => "204 No Content",
			400 => "400 Bad Request",
			401 => "401 Unauthorized",
			403 => "403 Forbidden",
			404 => "404 Not Found",
			500 => "500 Internal Server Error",
			_ => status.ToString()
		};
		return $"{method} {endpoint} should return {statusText}";
	}

	private static List<KeyValuePair<string, string>> ParseBodyPairs(string body)
	{
		var pairs = new List<KeyValuePair<string, string>>();
		foreach (var part in body.Split(',', StringSplitOptions.RemoveEmptyEntries))
		{
			var kv = part.Trim().Split('=', 2);
			if (kv.Length == 2)
			{
				var key = kv[0].Trim();
				var value = kv[1].Trim();

				// Detect type
				if (decimal.TryParse(value, System.Globalization.NumberStyles.Any,
					System.Globalization.CultureInfo.InvariantCulture, out var dec))
				{
					pairs.Add(new(key, value.Contains('.') ? $"{value}m" : value));
				}
				else if (bool.TryParse(value, out var b))
				{
					pairs.Add(new(key, b.ToString().ToLowerInvariant()));
				}
				else
				{
					pairs.Add(new(key, $"\"{value}\""));
				}
			}
		}
		return pairs;
	}

	private static List<string> ParseAssertions(string assertions)
	{
		var result = new List<string>();
		foreach (var part in assertions.Split(',', StringSplitOptions.RemoveEmptyEntries))
		{
			var assertion = part.Trim();

			if (assertion.Contains(">"))
			{
				var kv = assertion.Split('>', 2);
				var path = kv[0].Trim();
				var value = kv[1].Trim();
				if (!path.StartsWith("$")) path = "$." + path;
				result.Add($".AssertJsonPath<int>(\"{path}\", v => v > {value}, \"{path} should be > {value}\")");
			}
			else if (assertion.Contains("=NotEmpty"))
			{
				var path = assertion.Replace("=NotEmpty", "").Trim();
				if (!path.StartsWith("$")) path = "$." + path;
				result.Add($".AssertJsonPath<string>(\"{path}\", v => !string.IsNullOrEmpty(v), \"{path} should not be empty\")");
			}
			else if (assertion.Contains("="))
			{
				var kv = assertion.Split('=', 2);
				var path = kv[0].Trim();
				var value = kv[1].Trim();
				if (!path.StartsWith("$")) path = "$." + path;

				if (int.TryParse(value, out var intVal))
					result.Add($".AssertJsonPath<int>(\"{path}\", v => v == {intVal}, \"{path} should be {intVal}\")");
				else
					result.Add($".AssertJsonPath<string>(\"{path}\", v => v == \"{value}\", \"{path} should be '{value}'\")");
			}
		}
		return result;
	}

	private static string BuildBodyFromFields(string fields)
	{
		var parts = new List<string>();
		foreach (var field in fields.Split(',', StringSplitOptions.RemoveEmptyEntries))
		{
			var kv = field.Trim().Split(':', 2);
			var name = kv[0].Trim();
			var type = kv.Length > 1 ? kv[1].Trim().ToLowerInvariant() : "string";

			var defaultValue = type switch
			{
				"int" or "integer" => "1",
				"decimal" or "double" or "float" => "9.99",
				"bool" or "boolean" => "true",
				_ => $"Test {name}"
			};

			parts.Add($"{name}={defaultValue}");
		}
		return string.Join(", ", parts);
	}
}
