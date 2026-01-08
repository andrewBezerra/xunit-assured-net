using Shouldly;
using XUnitAssured.Http.Extensions;

namespace XUnitAssured.Http.Samples.Remote.Test;

[Trait("Category", "Integration")]
[Trait("Environment", "Remote")]
/// <summary>
/// Remote integration tests demonstrating XUnitAssured usage against a deployed API.
/// Tests the same endpoints as local tests but against a remote server configured in testsettings.json.
/// Demonstrates usage of XUnitAssured.Extensions for improved test readability.
/// </summary>
/// <remarks>
/// These tests require a remote API to be deployed and accessible.
/// Configure the remote API URL in testsettings.json:
/// <code>
/// {
///   "testMode": "Remote",
///   "http": {
///     "baseUrl": "https://your-deployed-api.com"
///   }
/// }
/// </code>
/// </remarks>
public class SimpleIntegrationTests : HttpSamplesRemoteTestBase, IClassFixture<HttpSamplesRemoteFixture>
{
	public SimpleIntegrationTests(HttpSamplesRemoteFixture fixture) : base(fixture)
	{
	}

	[Fact(Skip = "Remote test - requires deployed API environment", DisplayName = "Direct HTTP Client should work and return successful response from remote API")]
	public async Task DirectHttpClient_ShouldWork()
	{
		// This should work - direct HttpClient call to remote API
		var client = Fixture.CreateClient();
		var response = await client.GetAsync("/api/products/1");

		response.IsSuccessStatusCode.ShouldBeTrue();
	}

	[Fact(Skip = "Remote test - requires deployed API environment", DisplayName = "XUnitAssured with custom HTTP Client should work with relative URLs on remote API")]
	public void XUnitAssured_WithCustomHttpClient_ShouldWork()
	{
		// ✅ Using WithHttpClient to inject remote API's client
		var client = Fixture.CreateClient();

		var scenario = Given()
			.WithHttpClient(client)                   // ← Inject remote API's HttpClient
			.ApiResource("/api/products/1")           // ← Relative URL works with BaseAddress
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

