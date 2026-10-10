using System;
using System.Net.Http;
using System.Threading.Tasks;

using Microsoft.Playwright;

using XUnitAssured.Core.Abstractions;
using XUnitAssured.Core.Configuration;
using XUnitAssured.Http.Configuration;
using XUnitAssured.Http.Testing;
using XUnitAssured.Kafka.Extensions;
using XUnitAssured.Kafka.Testing;
using XUnitAssured.Playwright.Extensions;
using XUnitAssured.Playwright.Testing;
using XUnitAssured.RabbitMq.Extensions;

namespace XUnitAssured.Tests;

/// <summary>
/// The Quick Start snippets of the README, kept here so they are compiled against the
/// real DSL on every build, like the cross-boundary scenario in
/// <see cref="CrossScenarioExampleTests"/>. If a method they use is renamed or removed,
/// this file stops compiling and the README cannot drift from the API. Every C# block of the
/// Quick Start is here except the codegen one, which shows raw Playwright code beside its
/// translation rather than a test.
///
/// Skipped because each one needs a running API, broker or browser; the sample projects
/// exercise the same paths against real infrastructure.
/// </summary>
public static class ReadmeQuickStartExamples
{
	private sealed record Order(int Id, string Status);

	public class MyApiFixture : IHttpClientProvider, IHttpClientAuthProvider
	{
		private readonly HttpSettings _http = TestSettings.Load().GetHttpSettings()!;
		public HttpClient CreateClient() => new() { BaseAddress = new Uri(_http.BaseUrl!) };
		public HttpAuthConfig? GetAuthenticationConfig() => _http.Authentication;
	}

	[Trait("Category", "Examples")]
	public class MyApiTests : HttpTestBase<MyApiFixture>, IClassFixture<MyApiFixture>
	{
		public MyApiTests(MyApiFixture fixture) : base(fixture) { }

		[Fact(Skip = "README example - requires a running API")]
		public async Task Get_Users_Returns_Success()
		{
			var assertions = await Given()
				.ApiResource("/api/users")
				.Get()
				.ExecuteAsync();

			assertions
				.Then()
				.AssertStatusCode(200)
				.AssertJsonPath<string>("$.name", value => value == "John", "Name should be John");
		}

		[Fact(Skip = "README example - requires a running API")]
		public async Task Authentication_Examples()
		{
			// Bearer token
			var bearer = await Given().ApiResource("/api/secure")
				.WithBearerToken("my-jwt-token")
				.Get()
				.ExecuteAsync();
			bearer.Then().AssertStatusCode(200);

			// Basic auth
			var basic = await Given().ApiResource("/api/secure")
				.WithBasicAuth("username", "password")
				.Get()
				.ExecuteAsync();
			basic.Then().AssertStatusCode(200);

			// API key, in a header or the query string
			var apiKey = await Given().ApiResource("/api/secure")
				.WithApiKey("X-API-Key", "my-api-key", ApiKeyLocation.Header)
				.Get()
				.ExecuteAsync();
			apiKey.Then().AssertStatusCode(200);

			// OAuth2 client credentials
			var oauth = await Given().ApiResource("/api/secure")
				.WithOAuth2("https://auth.example.com/token", "client-id", "client-secret")
				.Get()
				.ExecuteAsync();
			oauth.Then().AssertStatusCode(200);
		}
	}

	[Trait("Category", "Examples")]
	public class MyUiTests : PlaywrightTestBase<PlaywrightTestFixture>, IClassFixture<PlaywrightTestFixture>
	{
		public MyUiTests(PlaywrightTestFixture fixture) : base(fixture) { }

		[Fact(Skip = "README example - requires a browser and a running web app")]
		public async Task Login_Should_Navigate_To_Dashboard()
		{
			var assertions = await Given()
				.NavigateTo("/login")
				.FillByLabel("Email", "user@test.com")
				.FillByLabel("Password", "secret")
				.ClickByRole(AriaRole.Button, "Sign in")
				.ExecuteAsync();

			assertions
				.Then()
				.AssertSuccess()
				.AssertUrl("/dashboard");
		}
	}

	[Trait("Category", "Examples")]
	public class MyKafkaTests : KafkaTestBase<KafkaClassFixture>, IClassFixture<KafkaClassFixture>
	{
		public MyKafkaTests(KafkaClassFixture fixture) : base(fixture) { }

		[Fact(Skip = "README example - requires a Kafka broker")]
		public async Task Produce_And_Consume_Message()
		{
			var topic = GenerateUniqueTopic("my-test");

			await Given()
				.Topic(topic)
				.Produce("Hello, Kafka!")
				.ExecuteAsync();

			var assertions = await Given()
				.Topic(topic)
				.Consume()
				.WithGroupId($"test-{Guid.NewGuid():N}")
				.ExecuteAsync();

			assertions
				.Then()
				.AssertSuccess()
				.AssertMessage<string>(msg => msg.ShouldBe("Hello, Kafka!"));
		}

		[Fact(Skip = "README example - requires a Kafka broker")]
		public async Task Batch_Operations()
		{
			var topic = GenerateUniqueTopic("my-batch");
			var messages = new[] { "one", "two", "three", "four", "five" };

			var produced = await Given()
				.Topic(topic)
				.ProduceBatch(messages)
				.ExecuteAsync();
			produced.Then().AssertSuccess().AssertBatchCount(5);

			var consumed = await Given()
				.Topic(topic)
				.ConsumeBatch(5)
				.WithGroupId($"test-{Guid.NewGuid():N}")
				.ExecuteAsync();
			consumed.Then().AssertSuccess().AssertBatchCount(5);
		}

		[Fact(Skip = "README example - requires Kafka brokers with authentication")]
		public async Task Authentication_Examples()
		{
			// SASL/PLAIN
			var saslPlain = await Given().Topic("my-topic")
				.Produce("message")
				.WithBootstrapServers("localhost:29093")
				.WithAuth(auth => auth.UseSaslPlain("user", "password", useSsl: false))
				.ExecuteAsync();
			saslPlain.Then().AssertSuccess();

			// SSL (one-way)
			var ssl = await Given().Topic("my-topic")
				.Produce("message")
				.WithBootstrapServers("localhost:29096")
				.WithAuth(auth => auth.UseSsl("certs/ca-cert.pem"))
				.ExecuteAsync();
			ssl.Then().AssertSuccess();

			// Mutual TLS
			var mtls = await Given().Topic("my-topic")
				.Produce("message")
				.WithBootstrapServers("localhost:29097")
				.WithAuth(auth => auth.UseMutualTls("client-cert.pem", "client-key.pem", "ca-cert.pem"))
				.ExecuteAsync();
			mtls.Then().AssertSuccess();
		}
	}

	[Trait("Category", "Examples")]
	public class MyRabbitMqTests
	{
		[Fact(Skip = "README example - requires a RabbitMQ broker")]
		public async Task Publish_And_Consume_Message()
		{
			await Given()
				.Queue("orders")
				.Publish(new { id = 42, status = "Created" })
				.ExecuteAsync();

			var assertions = await Given()
				.Queue("orders")
				.Consume()
				.WithTimeout(TimeSpan.FromSeconds(5))
				.ExecuteAsync();

			assertions
				.Then()
				.AssertSuccess()
				.AssertMessage<Order>(order => order.Status.ShouldBe("Created"));
		}
	}

#if NET10_0_OR_GREATER
	// O BrowserAppFixture só existe no net10. O Program aqui é o da SampleWebApi; no README, o
	// da API de quem lê.
	public sealed class MyApp : XUnitAssured.Playwright.AspNetCore.BrowserAppFixture<Program>
	{
		protected override string? AppDirectory => "../../../../my-app/dist";
	}
#endif
}
