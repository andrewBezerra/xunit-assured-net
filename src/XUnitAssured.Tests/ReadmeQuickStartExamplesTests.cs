using System;
using System.Net.Http;
using System.Threading.Tasks;

using Microsoft.Playwright;

using XUnitAssured.Core.Abstractions;
using XUnitAssured.Core.Configuration;
using XUnitAssured.Http.Configuration;
using XUnitAssured.Http.Testing;
using XUnitAssured.Kafka.Testing;
using XUnitAssured.Playwright.Extensions;
using XUnitAssured.Playwright.Testing;
using XUnitAssured.RabbitMq.Extensions;

namespace XUnitAssured.Tests;

/// <summary>
/// The Quick Start snippets of the README, kept here so they are compiled against the
/// real DSL on every build, like the cross-boundary scenario in
/// <see cref="CrossScenarioExampleTests"/>. If a method they use is renamed or removed,
/// this file stops compiling and the README cannot drift from the API.
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
}
