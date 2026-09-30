using System;
using System.Net.Http;

using Microsoft.Playwright;

using XUnitAssured.Playwright.Extensions;

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
	public void Placing_An_Order_Is_Reflected_Across_Api_Topic_And_Ui()
	{
		// In a real test these come from fixtures; see the sample projects.
		var api = new HttpClient { BaseAddress = new Uri("https://localhost:5001") };
		IPage browser = null!;

		var orderId = 0;

		var scenario = Given();
		scenario.Context.SetProperty("_PlaywrightPage", browser);

		scenario
			// HTTP: create the order through the API
			.WithHttpClient(api)
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
