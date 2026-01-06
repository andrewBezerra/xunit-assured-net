using Shouldly;

using XUnitAssured.Http.Extensions;

using static XUnitAssured.Core.DSL.ScenarioDsl;

namespace XUnitAssured.Http.Samples.Test;

/// <summary>
/// Diagnostic tests to debug XUnitAssured integration.
/// Demonstrates usage of XUnitAssured.Extensions for improved test readability.
/// </summary>
public class SimpleIntegrationTests : IClassFixture<HttpSamplesFixture>
{
	private readonly HttpSamplesFixture _fixture;

	public SimpleIntegrationTests(HttpSamplesFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task DirectHttpClient_ShouldWork()
	{
		// This should work - direct HttpClient call
		var client = _fixture.CreateClient();
		var response = await client.GetAsync("/api/products/1");


		response.IsSuccessStatusCode.ShouldBeTrue();
	}

	[Fact]
	public void XUnitAssured_WithCustomHttpClient_ShouldWork()
	{
		// ✅ Using WithHttpClient to inject test server's client
		var client = _fixture.CreateClient();

		var scenario = Given()
			.WithHttpClient(client)                   // ← Inject test server's HttpClient
			.ApiResource("/api/products/1")           // ← Relative URL now works!
			.Get();

		scenario.ExecuteCurrentStepAsync().GetAwaiter().GetResult();

		var step = scenario.CurrentStep;
		var result = step?.Result as XUnitAssured.Http.Results.HttpStepResult;

		result.ShouldNotBeNull();
		result.StatusCode.ShouldBe(200);

		var content = result.ResponseBody?.ToString();
		content.ShouldNotBeNullOrEmpty();
		content.ShouldContain("Laptop");
	}
}
