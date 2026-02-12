using XUnitAssured.Http.Extensions;
using XUnitAssured.Http.Configuration;

namespace XUnitAssured.Http.Samples.Remote.Test;

[Trait("Authentication", "ApiKey")]
[Trait("Environment", "Remote")]
/// <summary>
/// Remote tests demonstrating API Key Authentication against a deployed API.
/// API Keys can be sent in HTTP headers or query parameters.
/// </summary>
/// <remarks>
/// Tests the same API Key authentication endpoints as local tests but against a remote server.
/// Requires remote API to be configured in testsettings.json.
/// </remarks>
public class ApiKeyAuthTests : HttpSamplesRemoteTestBase, IClassFixture<HttpSamplesRemoteFixture>
{
	public ApiKeyAuthTests(HttpSamplesRemoteFixture fixture) : base(fixture)
	{
	}

	#region Header-based API Key Tests

	[Fact(Skip = "Remote test - requires deployed API environment", DisplayName = "API Key in Header with valid key should return success")]
	public void Example01_ApiKeyHeader_WithValidKey_ShouldReturnSuccess()
	{
		// Arrange
		var validApiKey = "api-key-header-abc123xyz";

		// Act & Assert
		Given().ApiResource("/api/auth/apikey-header")
			.WithApiKey("X-API-Key", validApiKey, ApiKeyLocation.Header)
			.Get()
		.When()
			.Execute()
		.Then()
			.AssertStatusCode(200)
			.AssertJsonPath<string>("$.authType", value => value == "ApiKey-Header", "Auth type should be ApiKey-Header")
			.AssertJsonPath<bool>("$.authenticated", value => value, "Should be authenticated")
			.AssertJsonPath<string>("$.message", value => value?.Contains("successful") == true, "Should return success message");
	}

	[Fact(Skip = "Remote test - requires deployed API environment", DisplayName = "API Key in Header with invalid key should return 401 Unauthorized")]
	public void Example02_ApiKeyHeader_WithInvalidKey_ShouldReturn401()
	{
		// Arrange
		var invalidApiKey = "invalid-api-key";

		// Act & Assert
		Given().ApiResource("/api/auth/apikey-header")
			.WithApiKey("X-API-Key", invalidApiKey, ApiKeyLocation.Header)
			.Get()
		.When()
			.Execute()
		.Then()
			.AssertStatusCode(401);
		// Note: Server returns 401 without JSON body
	}

	[Fact(Skip = "Remote test - requires deployed API environment", DisplayName = "API Key in Header without key should return 401 Unauthorized")]
	public void Example03_ApiKeyHeader_WithoutKey_ShouldReturn401()
	{
		// Act & Assert - No API key provided
		Given().ApiResource("/api/auth/apikey-header")
			.Get()
		.When()
			.Execute()
		.Then()
			.AssertStatusCode(401);
		// Note: Server returns 401 without JSON body
	}

	[Fact(Skip = "Remote test - requires deployed API environment", DisplayName = "API Key in Header with empty key should return 401 Unauthorized")]
	public void Example04_ApiKeyHeader_WithEmptyKey_ShouldReturn401()
	{
		// Arrange
		var emptyApiKey = "";

		// Act & Assert
		Given().ApiResource("/api/auth/apikey-header")
			.WithApiKey("X-API-Key", emptyApiKey, ApiKeyLocation.Header)
			.Get()
		.When()
			.Execute()
		.Then()
			.AssertStatusCode(401);
	}

	[Fact(Skip = "Remote test - requires deployed API environment", DisplayName = "API Key in Header is case-sensitive and wrong case should return 401 Unauthorized")]
	public void Example05_ApiKeyHeader_KeyIsCaseSensitive_ShouldReturn401()
	{
		// Arrange - API key is case-sensitive
		var wrongCaseKey = "API-KEY-HEADER-ABC123XYZ"; // Wrong case

		// Act & Assert
		Given().ApiResource("/api/auth/apikey-header")
			.WithApiKey("X-API-Key", wrongCaseKey, ApiKeyLocation.Header)
			.Get()
		.When()
			.Execute()
		.Then()
			.AssertStatusCode(401);
		// Note: Server returns 401 without JSON body
	}

	[Fact(Skip = "Remote test - requires deployed API environment", DisplayName = "API Key default location should be Header when not specified")]
	public void Example06_ApiKeyHeader_DefaultLocationIsHeader()
	{
		// Arrange
		var validApiKey = "api-key-header-abc123xyz";

		// Act & Assert - Default location is Header when not specified
		Given().ApiResource("/api/auth/apikey-header")
			.WithApiKey("X-API-Key", validApiKey) // No location specified = Header by default
			.Get()
		.When()
			.Execute()
		.Then()
			.AssertStatusCode(200)
			.AssertJsonPath<bool>("$.authenticated", value => value, "Should authenticate with default header location");
	}

	#endregion

	#region Query Parameter-based API Key Tests

	[Fact(Skip = "Remote test - requires deployed API environment", DisplayName = "API Key in Query Parameter with valid key should return success")]
	public void Example07_ApiKeyQuery_WithValidKey_ShouldReturnSuccess()
	{
		// Arrange
		var validApiKey = "api-key-query-xyz789abc";

		// Act & Assert
		Given().ApiResource("/api/auth/apikey-query")
			.WithApiKey("api_key", validApiKey, ApiKeyLocation.Query)
			.Get()
		.When()
			.Execute()
		.Then()
			.AssertStatusCode(200)
			.AssertJsonPath<string>("$.authType", value => value == "ApiKey-Query", "Auth type should be ApiKey-Query")
			.AssertJsonPath<bool>("$.authenticated", value => value, "Should be authenticated")
			.AssertJsonPath<string>("$.message", value => value?.Contains("successful") == true, "Should return success message");
	}

	[Fact(Skip = "Remote test - requires deployed API environment", DisplayName = "API Key in Query Parameter with invalid key should return 401 Unauthorized")]
	public void Example08_ApiKeyQuery_WithInvalidKey_ShouldReturn401()
	{
		// Arrange
		var invalidApiKey = "invalid-query-key";

		// Act & Assert
		Given().ApiResource("/api/auth/apikey-query")
			.WithApiKey("api_key", invalidApiKey, ApiKeyLocation.Query)
			.Get()
		.When()
			.Execute()
		.Then()
			.AssertStatusCode(401);
		// Note: Server returns 401 without JSON body
	}

	[Fact(Skip = "Remote test - requires deployed API environment", DisplayName = "API Key in Query Parameter without key should return 401 Unauthorized")]
	public void Example09_ApiKeyQuery_WithoutKey_ShouldReturn401()
	{
		// Act & Assert - No API key in query parameter
		Given().ApiResource("/api/auth/apikey-query")
			.Get()
		.When()
			.Execute()
		.Then()
			.AssertStatusCode(401);
		// Note: Server returns 401 without JSON body
	}

	[Fact(Skip = "Remote test - requires deployed API environment", DisplayName = "API Key in Query Parameter with empty key should return 401 Unauthorized")]
	public void Example10_ApiKeyQuery_WithEmptyKey_ShouldReturn401()
	{
		// Arrange
		var emptyApiKey = "";

		// Act & Assert
		Given().ApiResource("/api/auth/apikey-query")
			.WithApiKey("api_key", emptyApiKey, ApiKeyLocation.Query)
			.Get()
		.When()
			.Execute()
		.Then()
			.AssertStatusCode(401);
	}

	[Fact(Skip = "Remote test - requires deployed API environment", DisplayName = "API Key in Query Parameter is case-sensitive and wrong case should return 401 Unauthorized")]
	public void Example11_ApiKeyQuery_KeyIsCaseSensitive_ShouldReturn401()
	{
		// Arrange - API key is case-sensitive
		var wrongCaseKey = "API-KEY-QUERY-XYZ789ABC"; // Wrong case

		// Act & Assert
		Given().ApiResource("/api/auth/apikey-query")
			.WithApiKey("api_key", wrongCaseKey, ApiKeyLocation.Query)
			.Get()
		.When()
			.Execute()
		.Then()
			.AssertStatusCode(401);
		// Note: Server returns 401 without JSON body
	}

	#endregion

	#region Comparison and Mixed Scenarios

	[Fact(Skip = "Remote test - requires deployed API environment", DisplayName = "API Key in Header and Query Parameter are different endpoints")]
	public void Example12_ApiKey_HeaderAndQueryAreDifferent()
	{
		// Demonstrate that header and query API keys are different endpoints

		// Valid header key
		var headerKey = "api-key-header-abc123xyz";

		// Valid query key
		var queryKey = "api-key-query-xyz789abc";

		// Act & Assert - Header key works on header endpoint
		Given().ApiResource("/api/auth/apikey-header")
			.WithApiKey("X-API-Key", headerKey, ApiKeyLocation.Header)
			.Get()
		.When()
			.Execute()
		.Then()
			.AssertStatusCode(200)
			.AssertJsonPath<string>("$.authType", value => value == "ApiKey-Header", "Should authenticate with header key");

		// Act & Assert - Query key works on query endpoint
		Given().ApiResource("/api/auth/apikey-query")
			.WithApiKey("api_key", queryKey, ApiKeyLocation.Query)
			.Get()
		.When()
			.Execute()
		.Then()
			.AssertStatusCode(200)
			.AssertJsonPath<string>("$.authType", value => value == "ApiKey-Query", "Should authenticate with query key");
	}

	[Fact(Skip = "Remote test - requires deployed API environment", DisplayName = "API Key should work across multiple requests with same key")]
	public void Example13_ApiKey_MultipleRequestsWithSameKey()
	{
		// Arrange
		var validHeaderKey = "api-key-header-abc123xyz";

		// Act & Assert - First request
		Given().ApiResource("/api/auth/apikey-header")
			.WithApiKey("X-API-Key", validHeaderKey, ApiKeyLocation.Header)
			.Get()
		.When()
			.Execute()
		.Then()
			.AssertStatusCode(200)
			.AssertJsonPath<bool>("$.authenticated", value => value, "First request should be authenticated");

		// Act & Assert - Second request with same key
		Given().ApiResource("/api/auth/apikey-header")
			.WithApiKey("X-API-Key", validHeaderKey, ApiKeyLocation.Header)
			.Get()
		.When()
			.Execute()
		.Then()
			.AssertStatusCode(200)
			.AssertJsonPath<bool>("$.authenticated", value => value, "Second request should be authenticated");
	}

	#endregion

	#region Automatic Authentication Tests

	// ============================================================================
	// AUTOMATIC AUTHENTICATION TESTS
	// ============================================================================
	// These tests demonstrate the automatic authentication feature where
	// API Key configured in testsettings.json is automatically applied
	// without needing to call .WithApiKey() explicitly.
	//
	// To use these tests:
	// 1. Configure ApiKey in testsettings.json:
	//    "authentication": {
	//      "type": "ApiKey",
	//      "apiKey": {
	//        "keyName": "X-API-Key",
	//        "keyValue": "api-key-header-abc123xyz",
	//        "location": "Header"
	//      }
	//    }
	// 2. Remove the Skip attribute to run the tests
	// ============================================================================

	[Fact(Skip = "Demo test - requires ApiKey configured in testsettings.json",
		  DisplayName = "DEMO: API Key (Header) applied automatically from testsettings.json")]
	public void Example14_ApiKeyHeader_Automatic_FromTestSettings()
	{
		// NO .WithApiKey() call needed!
		// API Key is automatically applied from testsettings.json
		Given()
			.ApiResource("/api/auth/apikey-header")
			.Get()
		.When()
			.Execute()
		.Then()
			.AssertStatusCode(200)
			.AssertJsonPath<string>("$.authType", value => value == "ApiKey-Header", "Auth type should be ApiKey-Header")
			.AssertJsonPath<bool>("$.authenticated", value => value, "Should be authenticated");
	}

	[Fact(Skip = "Demo test - requires ApiKey (Query) configured in testsettings.json",
		  DisplayName = "DEMO: API Key (Query) applied automatically from testsettings.json")]
	public void Example15_ApiKeyQuery_Automatic_FromTestSettings()
	{
		// Configure in testsettings.json:
		//   "apiKey": { "keyName": "api_key", "keyValue": "api-key-query-xyz789abc", "location": "Query" }

		Given()
			.ApiResource("/api/auth/apikey-query")
			.Get()
		.When()
			.Execute()
		.Then()
			.AssertStatusCode(200)
			.AssertJsonPath<string>("$.authType", value => value == "ApiKey-Query", "Auth type should be ApiKey-Query")
			.AssertJsonPath<bool>("$.authenticated", value => value, "Should be authenticated");
	}

	[Fact(Skip = "Demo test - requires ApiKey configured in testsettings.json",
		  DisplayName = "DEMO: Manual API Key overrides testsettings.json configuration")]
	public void Example16_ApiKey_ManualOverride_TestSettings()
	{
		// Even if testsettings.json has API Key configured,
		// calling .WithApiKey() explicitly will override it

		var differentKey = "different-invalid-key";

		Given()
			.ApiResource("/api/auth/apikey-header")
			.WithApiKey("X-API-Key", differentKey, ApiKeyLocation.Header)  // Manual override
			.Get()
		.When()
			.Execute()
		.Then()
			.AssertStatusCode(401);  // Should fail with invalid key
	}

	#endregion
}
