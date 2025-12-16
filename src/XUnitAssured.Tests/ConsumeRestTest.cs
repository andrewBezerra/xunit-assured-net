namespace XUnitAssured.Tests;

/// <summary>
/// Integration tests demonstrating the complete XUnitAssured DSL.
/// These tests show HTTP + Kafka integration scenarios.
/// </summary>
public class ConsumeRestTest
{
	[Fact(Skip = "Integration test - requires real API")]
	public void HttpPost_Should_Work_With_Fluent_DSL()
	{
		// Simple HTTP test using jsonplaceholder (public API)
		Given()
			.ApiResource("https://jsonplaceholder.typicode.com/posts")
			.Post(new
			{
				title = "Test Post",
				body = "Test Body",
				userId = 1
			})
			.SaveStep("First")
			.Validate(response =>
			{
				response.StatusCode.ShouldBe(201);
				response.IsSuccessStatusCode.ShouldBeTrue();
			});
	}

	[Fact(Skip = "Integration test - requires real API and Kafka")]
	public void GetApiRetornaValidaTopicoECSTSucesso()
	{
		// Complete test: HTTP POST + Kafka message validation
		// This demonstrates the full power of the DSL
		Given()
			// Step 1: HTTP POST to create student
			.ApiResource("http://minhaapi.com.br/api/v1/estudante")
			.Post(new EstudanteRequestBody
			{
				Nome = "Carlos Bezerra",
				DataNascimento = "1985-03-02",
				NomeMae = "Terezinha Bezerra"
			})
			.SaveStep("First")
			.Validate(response =>
			{
				response.StatusCode.ShouldBe(201);
				response.IsSuccessStatusCode.ShouldBeTrue();
			})
			// Step 2: Validate Kafka message was published
			.And()
			.On()
			.Topic("Estudante_ECST")
			.Consume()
			.WithSchema(typeof(EstudanteECST))
			.SaveStep("KafkaMessage")
			.Validate(message =>
			{
				message.ShouldNotBeNull();
				message.Topic.ShouldBe("Estudante_ECST");

				// Validate message content
				var kafkaData = message.GetMessage<EstudanteECST>();
				kafkaData.ShouldNotBeNull();
				kafkaData.Nome.ShouldBe("Carlos Bezerra");
			});
	}

	[Fact]
	public void Complete_DSL_Syntax_Documentation()
	{
		// ============================================
		// COMPLETE DSL SYNTAX REFERENCE
		// ============================================
		// This test documents all available DSL methods
		// Skip = true by default (for documentation)

		// HTTP DSL Methods:
		// ------------------
		// Given()                              - Start scenario
		// .ApiResource(url)                    - Set endpoint
		// .Get()                               - HTTP GET
		// .Post(body)                          - HTTP POST with body
		// .Put(body)                           - HTTP PUT with body
		// .Delete()                            - HTTP DELETE
		// .Patch(body)                         - HTTP PATCH with body
		// .WithHeader(name, value)             - Add header
		// .WithQueryParam(name, value)         - Add query param
		// .WithTimeout(seconds)                - Set timeout
		// .SaveStep(name)                      - Save step for later reference
		// .Validate(response => ...)           - Validate HTTP response
		//
		// Kafka DSL Methods:
		// ------------------
		// .And()                               - Chain to next step
		// .On()                                - Continue chain
		// .Topic(name)                         - Set Kafka topic
		// .Consume()                           - Consume message
		// .WithSchema(type)                    - Set expected schema
		// .WithTimeout(timespan)               - Set consume timeout
		// .WithBootstrapServers(servers)       - Set Kafka servers
		// .WithGroupId(groupId)                - Set consumer group
		// .SaveStep(name)                      - Save Kafka step
		// .Validate(message => ...)            - Validate Kafka message
		// .ValidateMessage<T>(msg => ...)      - Validate typed message

		// Example combining everything:
		if (false) // Documentation only
		{
			Given()
				.ApiResource("https://api.example.com/resource")
				.WithHeader("Authorization", "Bearer token")
				.WithQueryParam("filter", "active")
				.Post(new { data = "value" })
				.WithTimeout(30)
				.SaveStep("HttpRequest")
				.Validate(response =>
				{
					response.StatusCode.ShouldBe(201);
					response.ContentType.ShouldContain("json");
				})
				.And()
				.On()
				.Topic("my-topic")
				.WithBootstrapServers("localhost:9092")
				.WithGroupId("test-group")
				.Consume()
				.WithSchema(typeof(MyMessage))
				.SaveStep("KafkaMsg")
				.ValidateMessage<MyMessage>(msg =>
				{
					msg.Id.ShouldNotBeEmpty();
				});
		}
	}

	// Test DTOs
	private class MyMessage
	{
		public string? Id { get; set; }
		public string? Data { get; set; }
	}
}
