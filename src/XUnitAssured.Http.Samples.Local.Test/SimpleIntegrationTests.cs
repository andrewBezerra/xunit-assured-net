using Shouldly;

using XUnitAssured.Http.Extensions;
using XUnitAssured.Http.Testing;

namespace XUnitAssured.Http.Samples.Local.Test;

[Trait("Category", "Integration")]
[Trait("Environment", "Local")]
/// <summary>
/// Diagnostic tests to debug XUnitAssured integration.
/// Demonstrates usage of XUnitAssured.Extensions for improved test readability.
/// </summary>
public class SimpleIntegrationTests : HttpTestBase<HttpSamplesFixture>, IClassFixture<HttpSamplesFixture>
{
	public SimpleIntegrationTests(HttpSamplesFixture fixture) : base(fixture)
	{
	}

	[Fact(DisplayName = "Direct HTTP Client should work and return successful response")]
	public async Task DirectHttpClient_ShouldWork()
	{
		// This should work - direct HttpClient call
		var client = Fixture.CreateClient();
		var response = await client.GetAsync("/api/products/1");


		response.IsSuccessStatusCode.ShouldBeTrue();
	}

	[Fact(DisplayName = "XUnitAssured with custom HTTP Client should work with relative URLs")]
	public void XUnitAssured_WithCustomHttpClient_ShouldWork()
	{
		// ✅ Using WithHttpClient to inject test server's client
		var client = Fixture.CreateClient();

		var scenario = Given()
			.WithHttpClient(client)                   // ← Inject test server's HttpClient
			.ApiResource("/api/products/1")           // ← Relative URL now works!
			.Get();

		scenario.ExecuteCurrentStepAsync().GetAwaiter().GetResult();

		var step = scenario.CurrentStep;
		var result = step?.Result as Results.HttpStepResult;

		result.ShouldNotBeNull();
		result.StatusCode.ShouldBe(200);

		var content = result.ResponseBody?.ToString();
		content.ShouldNotBeNullOrEmpty();
		content.ShouldContain("Laptop");
	}
}
