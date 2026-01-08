using XUnitAssured.Extensions.Http;
using XUnitAssured.Http.Extensions;
using XUnitAssured.Http.Samples.Test;
using XUnitAssured.Http.Testing;

namespace XUnitAssured.Http.Samples.Local.Test;

[Trait("Authentication", "CustomHeader")]
[Trait("Environment", "Local")]

/// <summary>
/// Sample tests demonstrating Custom Header Authentication using XUnitAssured.Http.
/// Custom headers allow for flexible authentication schemes beyond standard patterns.
/// </summary>
public class CustomHeaderAuthTests : HttpTestBase<HttpSamplesFixture>, IClassFixture<HttpSamplesFixture>
{
	public CustomHeaderAuthTests(HttpSamplesFixture fixture) : base(fixture)
	{
	}

	[Fact(DisplayName = "Custom Header Authentication with valid headers should return success")]
	public void Example01_CustomHeader_WithValidHeaders_ShouldReturnSuccess()
	{
		// Arrange
		var authToken = "custom-token-12345";
		var sessionId = "session-abc-xyz";

		// Act & Assert - Using WithCustomHeaders for multiple headers
		Given().ApiResource($"/api/auth/custom-header")
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

	[Fact(DisplayName = "Custom Header Authentication with single header using WithCustomHeader should fail when two headers required")]
	public void Example02_CustomHeader_WithSingleHeader_UsingWithCustomHeader()
	{
		// Arrange
		var authToken = "custom-token-12345";

		// Act & Assert - Using WithCustomHeader for single header
		// Note: This endpoint requires TWO headers, so this should fail
		Given().ApiResource($"/api/auth/custom-header")
			.WithCustomHeader("X-Custom-Auth-Token", authToken)
			.Get()
		.When()
			.Execute()
		.Then()
			.AssertStatusCode(401);
	}

	[Fact(DisplayName = "Custom Header Authentication with invalid auth token should return 401 Unauthorized")]
	public void Example03_CustomHeader_WithInvalidAuthToken_ShouldReturn401()
	{
		// Arrange
		var invalidAuthToken = "invalid-token";
		var validSessionId = "session-abc-xyz";

		// Act & Assert
		Given().ApiResource($"/api/auth/custom-header")
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

	[Fact(DisplayName = "Custom Header Authentication with invalid session ID should return 401 Unauthorized")]
	public void Example04_CustomHeader_WithInvalidSessionId_ShouldReturn401()
	{
		// Arrange
		var validAuthToken = "custom-token-12345";
		var invalidSessionId = "invalid-session";

		// Act & Assert
		Given().ApiResource($"/api/auth/custom-header")
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

	[Fact(DisplayName = "Custom Header Authentication with missing auth token should return 401 Unauthorized")]
	public void Example05_CustomHeader_MissingAuthToken_ShouldReturn401()
	{
		// Arrange
		var validSessionId = "session-abc-xyz";

		// Act & Assert - Missing X-Custom-Auth-Token header
		Given().ApiResource($"/api/auth/custom-header")
			.WithHeader("X-Custom-Session-Id", validSessionId) // Using WithHeader instead
			.Get()
		.When()
			.Execute()
		.Then()
			.AssertStatusCode(401);
	}

	[Fact(DisplayName = "Custom Header Authentication with missing session ID should return 401 Unauthorized")]
	public void Example06_CustomHeader_MissingSessionId_ShouldReturn401()
	{
		// Arrange
		var validAuthToken = "custom-token-12345";

		// Act & Assert - Missing X-Custom-Session-Id header
		Given().ApiResource($"/api/auth/custom-header")
			.WithHeader("X-Custom-Auth-Token", validAuthToken) // Using WithHeader instead
			.Get()
		.When()
			.Execute()
		.Then()
			.AssertStatusCode(401);
	}

	[Fact(DisplayName = "Custom Header Authentication without any headers should return 401 Unauthorized")]
	public void Example07_CustomHeader_WithoutAnyHeaders_ShouldReturn401()
	{
		// Act & Assert - No custom headers provided
		Given().ApiResource($"/api/auth/custom-header")
			.Get()
		.When()
			.Execute()
		.Then()
			.AssertStatusCode(401);
	}

	[Fact(DisplayName = "Custom Header Authentication with empty auth token should return 401 Unauthorized")]
	public void Example08_CustomHeader_WithEmptyAuthToken_ShouldReturn401()
	{
		// Arrange
		var emptyAuthToken = "";
		var validSessionId = "session-abc-xyz";

		// Act & Assert
		Given().ApiResource($"/api/auth/custom-header")
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

	[Fact(DisplayName = "Custom Header Authentication with empty session ID should return 401 Unauthorized")]
	public void Example09_CustomHeader_WithEmptySessionId_ShouldReturn401()
	{
		// Arrange
		var validAuthToken = "custom-token-12345";
		var emptySessionId = "";

		// Act & Assert
		Given().ApiResource($"/api/auth/custom-header")
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

	[Fact(DisplayName = "Custom Header Authentication values are case-sensitive and wrong case should return 401 Unauthorized")]
	public void Example10_CustomHeader_CaseSensitiveValues_ShouldReturn401()
	{
		// Arrange - Header values are case-sensitive
		var wrongCaseAuthToken = "CUSTOM-TOKEN-12345"; // Wrong case
		var wrongCaseSessionId = "SESSION-ABC-XYZ"; // Wrong case

		// Act & Assert
		Given().ApiResource($"/api/auth/custom-header")
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

	[Fact(DisplayName = "Custom Header Authentication with valid headers should return complete response structure")]
	public void Example11_CustomHeader_ValidHeaders_CheckResponseStructure()
	{
		// Arrange
		var authToken = "custom-token-12345";
		var sessionId = "session-abc-xyz";

		// Act & Assert - Validate complete response structure
		Given().ApiResource($"/api/auth/custom-header")
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

	[Fact(DisplayName = "Custom Header Authentication should maintain authentication across multiple requests")]
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
		Given().ApiResource($"/api/auth/custom-header")
			.WithCustomHeaders(headers)
			.Get()
		.When()
			.Execute()
		.Then()
			.AssertStatusCode(200)
			.AssertJsonPath<bool>("$.authenticated", value => value, "First request should be authenticated");

		// Act & Assert - Second request with same headers
		Given().ApiResource($"/api/auth/custom-header")
			.WithCustomHeaders(headers)
			.Get()
		.When()
			.Execute()
		.Then()
			.AssertStatusCode(200)
			.AssertJsonPath<bool>("$.authenticated", value => value, "Second request should be authenticated");
	}
}
