using System.Threading.Tasks;

using XUnitAssured.Kafka.Testing;
using XUnitAssured.Playwright.Extensions;
using XUnitAssured.Playwright.Testing;

namespace XUnitAssured.Tests;

[Trait("Category", "Examples")]
/// <summary>
/// The scenario shown at the top of the README, kept here so it is compiled against
/// the real DSL on every build. If a method it uses is renamed or removed, this file
/// stops compiling and the README cannot silently drift into fiction.
///
/// It is skipped because it needs a running API, a Kafka broker and a browser. The
/// sample projects exercise each leg against real infrastructure.
/// </summary>
public class CrossScenarioExampleTests
{
	/// <summary>The message the API is expected to publish after creating an order.</summary>
	private sealed record OrderCreated(int OrderId, string Status);

	[Fact(Skip = "README example - requires the API, a Kafka broker and a browser", DisplayName = "Placing an order should be reflected in the API, the topic and the UI")]
	public async Task Placing_An_Order_Is_Reflected_Across_Api_Topic_And_Ui()
	{
		// In a real test these are xUnit class fixtures; see the sample projects.
		var api = new ApiFixture();
		var kafka = new KafkaClassFixture();
		var ui = new PlaywrightTestFixture();

		var orderId = 0;
		await using var browser = await ui.OpenPageAsync();

		var scenario = Given(api, kafka, browser);

		scenario
			// HTTP: create the order through the API
			.ApiResource("/api/orders")
			.Post(new { customerId = 42, total = 99.90m })
			.Validate(response =>
			{
				response.StatusCode.ShouldBe(201);
				orderId = response.JsonPath<int>("$.id");
			})

			// Kafka: the API must have published the event
			.And().On()
			.Topic("orders.created")
			.Consume()
			.ValidateMessage<OrderCreated>(message =>
			{
				message.OrderId.ShouldBe(orderId);
				message.Status.ShouldBe("Created");
			})

			// Browser: and the order must be visible to the user
			.And()
			.NavigateTo($"/orders/{orderId}");

		// With Http, Kafka and Playwright all referenced, `Execute()` is ambiguous —
		// each package defines its own. Naming the package resolves it; a single
		// entry point is on the roadmap.
		PlaywrightBddExtensions.Execute(scenario)
			.Then()
			.AssertUrlContains($"/orders/{orderId}")
			.AssertTextContainsByTestId("order-status", "Created");
	}
}
