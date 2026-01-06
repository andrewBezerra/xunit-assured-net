using Xunit;

namespace XUnitAssured.Http.Samples.Test;

/// <summary>
/// Simple diagnostic test to verify the test server is working
/// </summary>
public class DiagnosticTests : IClassFixture<HttpSamplesFixture>
{
	private readonly HttpSamplesFixture _fixture;

	public DiagnosticTests(HttpSamplesFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task TestServerShouldStart()
	{
		// Arrange
		var client = _fixture.CreateClient();

		// Act
		var response = await client.GetAsync("/api/products");

		// Assert
		Assert.NotNull(response);
		var content = await response.Content.ReadAsStringAsync();
		
		// Log for debugging
		System.Console.WriteLine($"Status: {response.StatusCode}");
		System.Console.WriteLine($"Content: {content}");
		System.Console.WriteLine($"BaseUrl: {_fixture.BaseUrl}");
		
		Assert.True(response.IsSuccessStatusCode, 
			$"Expected success but got {response.StatusCode}. Content: {content}");
	}
}
