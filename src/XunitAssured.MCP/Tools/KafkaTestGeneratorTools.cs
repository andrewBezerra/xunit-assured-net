using System.ComponentModel;
using System.Text;

using ModelContextProtocol.Server;

namespace XUnitAssured.Mcp.Tools;

/// <summary>
/// MCP tools for generating XUnitAssured.Kafka integration tests.
/// Provides tools to scaffold Kafka produce/consume test methods,
/// list the Kafka DSL API surface, and generate assertion chains.
/// </summary>
[McpServerToolType]
public static class KafkaTestGeneratorTools
{
	[McpServerTool(Name = "generate_kafka_produce_test"),
	 Description("Generates a complete XUnitAssured.Kafka test method that produces a message to a Kafka topic. " +
	             "Produces a ready-to-paste [Fact] method with Given/When/Then structure.")]
	public static string GenerateKafkaProduceTest(
		[Description("Kafka topic name, e.g. 'orders', 'user-events'")] string topic,
		[Description("Message key (optional). Leave empty for null key.")] string? key = null,
		[Description("Comma-separated fields for the message body, e.g. 'id:int, name:string, price:decimal, active:bool'")] string? fields = null,
		[Description("Authentication: none, sasl-plain, sasl-scram, ssl. Default: none")] string auth = "none",
		[Description("Test display name. Auto-generated if empty.")] string? displayName = null)
	{
		topic = (topic ?? "my-topic").Trim();
		auth = (auth ?? "none").Trim().ToLowerInvariant();
		displayName ??= $"Should produce message to {topic} topic";

		var sb = new StringBuilder();

		sb.AppendLine($"\t[Fact(DisplayName = \"{displayName}\")]");
		sb.AppendLine($"\tpublic void Should_Produce_To_{SanitizeName(topic)}()");
		sb.AppendLine("\t{");

		// Arrange - message body
		sb.AppendLine("\t\t// Arrange");
		if (fields != null)
		{
			sb.AppendLine("\t\tvar message = new");
			sb.AppendLine("\t\t{");
			foreach (var pair in ParseFieldPairs(fields))
			{
				sb.AppendLine($"\t\t\t{pair.Key} = {pair.Value},");
			}
			sb.AppendLine("\t\t};");
		}
		else
		{
			sb.AppendLine("\t\tvar message = new { id = 1, name = \"Test Message\", timestamp = DateTime.UtcNow };");
		}
		sb.AppendLine();

		// Act & Assert
		sb.AppendLine("\t\t// Act & Assert");
		sb.AppendLine("\t\tGiven()");
		sb.AppendLine($"\t\t\t.Topic(\"{topic}\")");

		if (!string.IsNullOrWhiteSpace(key))
			sb.AppendLine($"\t\t\t.Produce(\"{key}\", message)");
		else
			sb.AppendLine("\t\t\t.Produce(message)");

		// Auth
		AppendAuth(sb, auth);

		sb.AppendLine("\t\t.When()");
		sb.AppendLine("\t\t\t.Execute()");
		sb.AppendLine("\t\t.Then()");
		sb.AppendLine("\t\t\t.AssertSuccess();");
		sb.AppendLine("\t}");

		return sb.ToString();
	}

	[McpServerTool(Name = "generate_kafka_consume_test"),
	 Description("Generates a complete XUnitAssured.Kafka test method that consumes a message from a Kafka topic. " +
	             "Produces a ready-to-paste [Fact] method with Given/When/Then structure and message assertions.")]
	public static string GenerateKafkaConsumeTest(
		[Description("Kafka topic name, e.g. 'orders', 'user-events'")] string topic,
		[Description("Consumer group ID. Default: 'test-group'")] string groupId = "test-group",
		[Description("Comma-separated JSON path assertions, e.g. '$.id>0, $.name=NotEmpty'. Leave empty for basic assertions.")] string? assertions = null,
		[Description("Authentication: none, sasl-plain, sasl-scram, ssl. Default: none")] string auth = "none",
		[Description("Test display name. Auto-generated if empty.")] string? displayName = null)
	{
		topic = (topic ?? "my-topic").Trim();
		groupId = (groupId ?? "test-group").Trim();
		auth = (auth ?? "none").Trim().ToLowerInvariant();
		displayName ??= $"Should consume message from {topic} topic";

		var sb = new StringBuilder();

		sb.AppendLine($"\t[Fact(DisplayName = \"{displayName}\")]");
		sb.AppendLine($"\tpublic void Should_Consume_From_{SanitizeName(topic)}()");
		sb.AppendLine("\t{");
		sb.AppendLine("\t\t// Act & Assert");
		sb.AppendLine("\t\tGiven()");
		sb.AppendLine($"\t\t\t.Topic(\"{topic}\")");
		sb.AppendLine("\t\t\t.Consume()");
		sb.AppendLine($"\t\t\t.WithGroupId(\"{groupId}\")");

		// Auth
		AppendAuth(sb, auth);

		sb.AppendLine("\t\t.When()");
		sb.AppendLine("\t\t\t.Execute()");
		sb.AppendLine("\t\t.Then()");
		sb.Append("\t\t\t.AssertSuccess()");

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

	[McpServerTool(Name = "generate_kafka_produce_consume_test"),
	 Description("Generates a complete produce-then-consume round-trip test for a Kafka topic. " +
	             "First produces a message, then consumes it and validates the content. " +
	             "Useful for end-to-end Kafka integration testing.")]
	public static string GenerateKafkaRoundTripTest(
		[Description("Kafka topic name")] string topic,
		[Description("Comma-separated fields for the message, e.g. 'id:int, name:string, amount:decimal'")] string? fields = null,
		[Description("Consumer group ID. Default: 'test-group'")] string groupId = "test-group",
		[Description("Authentication: none, sasl-plain, sasl-scram, ssl. Default: none")] string auth = "none",
		[Description("Test display name. Auto-generated if empty.")] string? displayName = null)
	{
		topic = (topic ?? "my-topic").Trim();
		groupId = (groupId ?? "test-group").Trim();
		auth = (auth ?? "none").Trim().ToLowerInvariant();
		displayName ??= $"Should produce and consume message on {topic} topic";

		var sb = new StringBuilder();

		sb.AppendLine($"\t[Fact(DisplayName = \"{displayName}\")]");
		sb.AppendLine($"\tpublic void Should_RoundTrip_{SanitizeName(topic)}()");
		sb.AppendLine("\t{");

		// Arrange
		sb.AppendLine("\t\t// Arrange");
		if (fields != null)
		{
			sb.AppendLine("\t\tvar message = new");
			sb.AppendLine("\t\t{");
			foreach (var pair in ParseFieldPairs(fields))
			{
				sb.AppendLine($"\t\t\t{pair.Key} = {pair.Value},");
			}
			sb.AppendLine("\t\t};");
		}
		else
		{
			sb.AppendLine("\t\tvar message = new { id = 1, name = \"RoundTrip Test\", timestamp = DateTime.UtcNow };");
		}
		sb.AppendLine();

		// Produce
		sb.AppendLine("\t\t// Act — Produce");
		sb.AppendLine("\t\tGiven()");
		sb.AppendLine($"\t\t\t.Topic(\"{topic}\")");
		sb.AppendLine("\t\t\t.Produce(message)");
		AppendAuth(sb, auth);
		sb.AppendLine("\t\t.When()");
		sb.AppendLine("\t\t\t.Execute()");
		sb.AppendLine("\t\t.Then()");
		sb.AppendLine("\t\t\t.AssertSuccess();");
		sb.AppendLine();

		// Consume
		sb.AppendLine("\t\t// Assert — Consume");
		sb.AppendLine("\t\tGiven()");
		sb.AppendLine($"\t\t\t.Topic(\"{topic}\")");
		sb.AppendLine("\t\t\t.Consume()");
		sb.AppendLine($"\t\t\t.WithGroupId(\"{groupId}\")");
		AppendAuth(sb, auth);
		sb.AppendLine("\t\t.When()");
		sb.AppendLine("\t\t\t.Execute()");
		sb.AppendLine("\t\t.Then()");
		sb.AppendLine("\t\t\t.AssertSuccess()");
		sb.AppendLine("\t\t\t.AssertTopic(\"" + topic + "\");");
		sb.AppendLine("\t}");

		return sb.ToString();
	}

	[McpServerTool(Name = "list_kafka_dsl_methods", ReadOnly = true),
	 Description("Lists all available XUnitAssured.Kafka DSL methods for Kafka integration testing. " +
	             "Use this as a reference when writing Kafka produce/consume tests.")]
	public static string ListKafkaDslMethods(
		[Description("Optional filter: 'produce', 'consume', 'auth', 'assert', 'config', 'all'. Default is 'all'.")] string filter = "all")
	{
		filter = (filter ?? "all").Trim().ToLowerInvariant();

		var sections = new List<string>();

		if (filter is "all" or "produce")
		{
			sections.Add("""
			== Produce ==
			  Given()
			    .Topic("my-topic")
			    .Produce(message)                               Produce with null key
			    .Produce("key", message)                        Produce with key
			    .ProduceBatch(messages)                          Produce multiple messages
			    .ProduceBatch(keyedMessages)                     Produce multiple keyed messages
			    .WithKey("key")                                  Set message key
			    .WithHeaders(headers)                            Set Kafka headers
			    .WithHeader("name", bytes)                       Add single header
			    .WithPartition(0)                                Target specific partition
			    .WithTimestamp(DateTime.UtcNow)                  Set custom timestamp
			    .WithJsonOptions(options)                        Custom JSON serialization
			""");
		}

		if (filter is "all" or "consume")
		{
			sections.Add("""
			== Consume ==
			  Given()
			    .Topic("my-topic")
			    .Consume()                                       Consume single message
			    .ConsumeBatch(5)                                  Consume N messages
			    .WithGroupId("my-group")                          Set consumer group ID
			    .WithSchema(typeof(MyMessage))                    Set expected schema type
			    .WithConsumerConfig(config)                       Custom ConsumerConfig
			""");
		}

		if (filter is "all" or "config")
		{
			sections.Add("""
			== Configuration (shared) ==
			    .WithBootstrapServers("broker:9092")              Set broker address
			    .WithTimeout(TimeSpan.FromSeconds(30))            Set operation timeout
			    .WithProducerConfig(config)                       Custom ProducerConfig
			""");
		}

		if (filter is "all" or "auth")
		{
			sections.Add("""
			== Authentication ==
			    .WithSaslPlain()                                  SASL/PLAIN from kafkasettings.json
			    .WithSaslPlain("user", "pass")                    SASL/PLAIN explicit
			    .WithSaslScram()                                  SASL/SCRAM from settings
			    .WithKafkaAuth(c => c.UseSaslPlain(...))          Custom auth config
			""");
		}

		if (filter is "all" or "assert")
		{
			sections.Add("""
			== Execution & Assertions ==
			  .When().Execute()
			  .Then()
			    .AssertSuccess()                                   Assert operation succeeded
			    .AssertFailure()                                   Assert operation failed
			    .AssertTopic("my-topic")                            Assert topic name
			    .AssertKey(key => key.ShouldBe("123"))              Assert message key
			    .AssertMessage(msg => msg.ShouldNotBeNull())        Assert raw message
			    .AssertMessage<Order>(o => o.Id.ShouldBe(1))        Assert typed message
			    .AssertJsonPath<int>("$.id", id => id.ShouldBe(1))  Assert JSON value
			    .AssertHeader("correlation-id", "abc")              Assert header value
			    .AssertPartition(0)                                 Assert partition
			    .AssertBatchCount(5)                                Assert batch size
			""");
		}

		if (filter is "all")
		{
			sections.Add("""
			== Complete Produce Example ==
			  Given()
			      .Topic("orders")
			      .Produce("order-123", new { id = 1, total = 99.99m })
			  .When()
			      .Execute()
			  .Then()
			      .AssertSuccess();

			== Complete Consume Example ==
			  Given()
			      .Topic("orders")
			      .Consume()
			      .WithGroupId("test-group")
			  .When()
			      .Execute()
			  .Then()
			      .AssertSuccess()
			      .AssertJsonPath<int>("$.id", id => id.ShouldBe(1));
			""");
		}

		return sections.Count > 0
			? string.Join("\n", sections)
			: $"Unknown filter '{filter}'. Use: produce, consume, auth, assert, config, or all.";
	}

	// ──────────────────────────────────────────────
	// Internal helpers
	// ──────────────────────────────────────────────

	private static string SanitizeName(string name)
	{
		return string.Concat(name.Split('-', '.', ' ')
			.Select(p => p.Length > 0 ? char.ToUpper(p[0]) + p[1..] : ""));
	}

	private static void AppendAuth(StringBuilder sb, string auth)
	{
		switch (auth)
		{
			case "sasl-plain":
				sb.AppendLine("\t\t\t.WithSaslPlain(\"username\", \"password\")");
				break;
			case "sasl-scram":
				sb.AppendLine("\t\t\t.WithSaslScram()");
				break;
			case "ssl":
				sb.AppendLine("\t\t\t// .WithSsl() — configure SSL in kafkasettings.json");
				break;
		}
	}

	private static List<KeyValuePair<string, string>> ParseFieldPairs(string fields)
	{
		var pairs = new List<KeyValuePair<string, string>>();
		foreach (var field in fields.Split(',', StringSplitOptions.RemoveEmptyEntries))
		{
			var kv = field.Trim().Split(':', 2);
			var name = kv[0].Trim();
			var type = kv.Length > 1 ? kv[1].Trim().ToLowerInvariant() : "string";

			var defaultValue = type switch
			{
				"int" or "integer" or "long" => "1",
				"decimal" => "99.99m",
				"double" or "float" => "99.99",
				"bool" or "boolean" => "true",
				"datetime" => "DateTime.UtcNow",
				"guid" => "Guid.NewGuid()",
				_ => $"\"Test {name}\""
			};

			pairs.Add(new(name, defaultValue));
		}
		return pairs;
	}

	private static List<string> ParseAssertions(string assertions)
	{
		var result = new List<string>();
		foreach (var part in assertions.Split(',', StringSplitOptions.RemoveEmptyEntries))
		{
			var assertion = part.Trim();

			if (assertion.Contains('>'))
			{
				var kv = assertion.Split('>', 2);
				var path = NormalizePath(kv[0].Trim());
				var value = kv[1].Trim();
				result.Add($".AssertJsonPath<int>(\"{path}\", v => v > {value}, \"{path} should be > {value}\")");
			}
			else if (assertion.Contains("=NotEmpty"))
			{
				var path = NormalizePath(assertion.Replace("=NotEmpty", "").Trim());
				result.Add($".AssertJsonPath<string>(\"{path}\", v => !string.IsNullOrEmpty(v), \"{path} should not be empty\")");
			}
			else if (assertion.Contains('='))
			{
				var kv = assertion.Split('=', 2);
				var path = NormalizePath(kv[0].Trim());
				var value = kv[1].Trim();

				if (int.TryParse(value, out var intVal))
					result.Add($".AssertJsonPath<int>(\"{path}\", v => v == {intVal}, \"{path} should be {intVal}\")");
				else
					result.Add($".AssertJsonPath<string>(\"{path}\", v => v == \"{value}\", \"{path} should be '{value}'\")");
			}
		}
		return result;
	}

	private static string NormalizePath(string path)
	{
		return path.StartsWith("$") ? path : "$." + path;
	}
}
