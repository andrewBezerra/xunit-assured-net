using XUnitAssured.Extensions.Http;
using XUnitAssured.Http.Extensions;

using static XUnitAssured.Core.DSL.ScenarioDsl;

namespace XUnitAssured.Http.Samples.Test;

/// <summary>
/// Sample tests demonstrating Custom Header Authentication using XUnitAssured.Http.
/// Custom headers allow for flexible authentication schemes beyond standard patterns.
/// </summary>
public class CustomHeaderAuthTests : IClassFixture<HttpSamplesFixture>
{
	private readonly HttpSamplesFixture _fixture;

	public CustomHeaderAuthTests(HttpSamplesFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public void Example01_CustomHeader_WithValidHeaders_ShouldReturnSuccess()
	{
		// Arrange
		var authToken = "custom-token-12345";
		var sessionId = "session-abc-xyz";

		// Act & Assert - Using WithCustomHeaders for multiple headers
		Given()
			.WithHttpClient(_fixture.CreateClient())
			.ApiResource($"/api/auth/custom-header")
			.WithCustomHeaders(new Dictionary<string, string>
			{
				["X-Custom-Auth-Token"] = authToken,
				["X-Custom-Session-Id"] = sessionId
			})
			.Get()
		.When()
			.Execute()
		.Then()
			.AssertStatusCode(200)
			.AssertJsonPath<string>("$.authType", value => value == "CustomHeader", "Auth type should be CustomHeader")
			.AssertJsonPath<bool>("$.authenticated", value => value, "Should be authenticated")
			.AssertJsonPath<string>("$.message", value => value?.Contains("successful") == true, "Should return success message")
			.AssertJsonPath<string>("$.customData.SessionId", value => value == sessionId, $"Should return session ID: {sessionId}");
	}

	[Fact]
	public void Example02_CustomHeader_WithSingleHeader_UsingWithCustomHeader()
	{
		// Arrange
		var authToken = "custom-token-12345";

		// Act & Assert - Using WithCustomHeader for single header
		// Note: This endpoint requires TWO headers, so this should fail
		Given()
			.WithHttpClient(_fixture.CreateClient())
			.ApiResource($"/api/auth/custom-header")
			.WithCustomHeader("X-Custom-Auth-Token", authToken)
			.Get()
		.When()
			.Execute()
		.Then()
			.AssertStatusCode(401);
	}

	[Fact]
	public void Example03_CustomHeader_WithInvalidAuthToken_ShouldReturn401()
	{
		// Arrange
		var invalidAuthToken = "invalid-token";
		var validSessionId = "session-abc-xyz";

		// Act & Assert
		Given()
			.WithHttpClient(_fixture.CreateClient())
			.ApiResource($"/api/auth/custom-header")
			.WithCustomHeaders(new Dictionary<string, string>
			{
				["X-Custom-Auth-Token"] = invalidAuthToken,
				["X-Custom-Session-Id"] = validSessionId
			})
			.Get()
		.When()
			.Execute()
		.Then()
			.AssertStatusCode(401);
	}

	[Fact]
	public void Example04_CustomHeader_WithInvalidSessionId_ShouldReturn401()
	{
		// Arrange
		var validAuthToken = "custom-token-12345";
		var invalidSessionId = "invalid-session";

		// Act & Assert
		Given()
			.WithHttpClient(_fixture.CreateClient())
			.ApiResource($"/api/auth/custom-header")
			.WithCustomHeaders(new Dictionary<string, string>
			{
				["X-Custom-Auth-Token"] = validAuthToken,
				["X-Custom-Session-Id"] = invalidSessionId
			})
			.Get()
		.When()
			.Execute()
		.Then()
			.AssertStatusCode(401);
	}

	[Fact]
	public void Example05_CustomHeader_MissingAuthToken_ShouldReturn401()
	{
		// Arrange
		var validSessionId = "session-abc-xyz";

		// Act & Assert - Missing X-Custom-Auth-Token header
		Given()
			.WithHttpClient(_fixture.CreateClient())
			.ApiResource($"/api/auth/custom-header")
			.WithHeader("X-Custom-Session-Id", validSessionId) // Using WithHeader instead
			.Get()
		.When()
			.Execute()
		.Then()
			.AssertStatusCode(401);
	}

	[Fact]
	public void Example06_CustomHeader_MissingSessionId_ShouldReturn401()
	{
		// Arrange
		var validAuthToken = "custom-token-12345";

		// Act & Assert - Missing X-Custom-Session-Id header
		Given()
			.WithHttpClient(_fixture.CreateClient())
			.ApiResource($"/api/auth/custom-header")
			.WithHeader("X-Custom-Auth-Token", validAuthToken) // Using WithHeader instead
			.Get()
		.When()
			.Execute()
		.Then()
			.AssertStatusCode(401);
	}

	[Fact]
	public void Example07_CustomHeader_WithoutAnyHeaders_ShouldReturn401()
	{
		// Act & Assert - No custom headers provided
		Given()
			.WithHttpClient(_fixture.CreateClient())
			.ApiResource($"/api/auth/custom-header")
			.Get()
		.When()
			.Execute()
		.Then()
			.AssertStatusCode(401);
	}

	[Fact]
	public void Example08_CustomHeader_WithEmptyAuthToken_ShouldReturn401()
	{
		// Arrange
		var emptyAuthToken = "";
		var validSessionId = "session-abc-xyz";

		// Act & Assert
		Given()
			.WithHttpClient(_fixture.CreateClient())
			.ApiResource($"/api/auth/custom-header")
			.WithCustomHeaders(new Dictionary<string, string>
			{
				["X-Custom-Auth-Token"] = emptyAuthToken,
				["X-Custom-Session-Id"] = validSessionId
			})
			.Get()
		.When()
			.Execute()
		.Then()
			.AssertStatusCode(401);
	}

	[Fact]
	public void Example09_CustomHeader_WithEmptySessionId_ShouldReturn401()
	{
		// Arrange
		var validAuthToken = "custom-token-12345";
		var emptySessionId = "";

		// Act & Assert
		Given()
			.WithHttpClient(_fixture.CreateClient())
			.ApiResource($"/api/auth/custom-header")
			.WithCustomHeaders(new Dictionary<string, string>
			{
				["X-Custom-Auth-Token"] = validAuthToken,
				["X-Custom-Session-Id"] = emptySessionId
			})
			.Get()
		.When()
			.Execute()
		.Then()
			.AssertStatusCode(401);
	}

	[Fact]
	public void Example10_CustomHeader_CaseSensitiveValues_ShouldReturn401()
	{
		// Arrange - Header values are case-sensitive
		var wrongCaseAuthToken = "CUSTOM-TOKEN-12345"; // Wrong case
		var wrongCaseSessionId = "SESSION-ABC-XYZ"; // Wrong case

		// Act & Assert
		Given()
			.WithHttpClient(_fixture.CreateClient())
			.ApiResource($"/api/auth/custom-header")
			.WithCustomHeaders(new Dictionary<string, string>
			{
				["X-Custom-Auth-Token"] = wrongCaseAuthToken,
				["X-Custom-Session-Id"] = wrongCaseSessionId
			})
			.Get()
		.When()
			.Execute()
		.Then()
			.AssertStatusCode(401);
	}

	[Fact]
	public void Example11_CustomHeader_ValidHeaders_CheckResponseStructure()
	{
		// Arrange
		var authToken = "custom-token-12345";
		var sessionId = "session-abc-xyz";

		// Act & Assert - Validate complete response structure
		Given()
			.WithHttpClient(_fixture.CreateClient())
			.ApiResource($"/api/auth/custom-header")
			.WithCustomHeaders(new Dictionary<string, string>
			{
				["X-Custom-Auth-Token"] = authToken,
				["X-Custom-Session-Id"] = sessionId
			})
			.Get()
		.When()
			.Execute()
		.Then()
			.AssertStatusCode(200)
			.AssertJsonPath<object>("$.authType", value => value != null, "authType field should exist")
			.AssertJsonPath<object>("$.authenticated", value => value != null, "authenticated field should exist")
			.AssertJsonPath<object>("$.message", value => value != null, "message field should exist")
			.AssertJsonPath<object>("$.customData", value => value != null, "customData field should exist")
			.AssertJsonPath<object>("$.customData.SessionId", value => value != null, "customData.SessionId should exist");
	}

	[Fact]
	public void Example12_CustomHeader_MultipleRequests_ShouldMaintainAuthentication()
	{
		// Arrange
		var authToken = "custom-token-12345";
		var sessionId = "session-abc-xyz";

		var headers = new Dictionary<string, string>
		{
			["X-Custom-Auth-Token"] = authToken,
			["X-Custom-Session-Id"] = sessionId
		};

		// Act & Assert - First request
		Given()
			.WithHttpClient(_fixture.CreateClient())
			.ApiResource($"/api/auth/custom-header")
			.WithCustomHeaders(headers)
			.Get()
		.When()
			.Execute()
		.Then()
			.AssertStatusCode(200)
			.AssertJsonPath<bool>("$.authenticated", value => value, "First request should be authenticated");

		// Act & Assert - Second request with same headers
		Given()
			.WithHttpClient(_fixture.CreateClient())
			.ApiResource($"/api/auth/custom-header")
			.WithCustomHeaders(headers)
			.Get()
		.When()
			.Execute()
		.Then()
			.AssertStatusCode(200)
			.AssertJsonPath<bool>("$.authenticated", value => value, "Second request should be authenticated");
	}
}
