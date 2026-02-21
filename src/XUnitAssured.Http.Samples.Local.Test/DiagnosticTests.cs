using XUnitAssured.Http.Testing;

namespace XUnitAssured.Http.Samples.Local.Test;

[Trait("Category", "Diagnostics")]
[Trait("Environment", "Local")]

/// <summary>
/// Simple diagnostic test to verify the test server is working
/// </summary>
public class DiagnosticTests : HttpTestBase<HttpSamplesFixture>, IClassFixture<HttpSamplesFixture>
{
	public DiagnosticTests(HttpSamplesFixture fixture) : base(fixture)
	{
	}

	[Fact(DisplayName = "Test server should start and respond to HTTP requests successfully")]
	public async Task TestServerShouldStart()
	{
		// Arrange
		var client = Fixture.CreateClient();

		// Act
		var response = await client.GetAsync("/api/products");

		// Assert
		Assert.NotNull(response);
		var content = await response.Content.ReadAsStringAsync();

		// Log for debugging
		Console.WriteLine($"Status: {response.StatusCode}");
		Console.WriteLine($"Content: {content}");
		Console.WriteLine($"BaseUrl: {Fixture.BaseUrl}");

		Assert.True(response.IsSuccessStatusCode,
			$"Expected success but got {response.StatusCode}. Content: {content}");
	}
}
