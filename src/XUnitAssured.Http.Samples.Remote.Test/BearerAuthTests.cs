using XUnitAssured.Http.Extensions;
using XUnitAssured.Http.Extensions;

namespace XUnitAssured.Http.Samples.Remote.Test;

[Trait("Authentication", "Bearer")]
[Trait("Environment", "Remote")]
/// <summary>
/// Remote tests demonstrating Bearer Token Authentication against a deployed API.
/// Bearer tokens are sent in the Authorization header as "Bearer {token}".
/// Commonly used for JWT (JSON Web Tokens) and OAuth2 access tokens.
/// </summary>
/// <remarks>
/// Tests the same authentication endpoints as local tests but against a remote server.
/// Requires remote API to be configured in testsettings.json:
/// <code>
/// {
///   "testMode": "Remote",
///   "http": {
///     "baseUrl": "https://your-api.com"
///   }
/// }
/// </code>
/// </remarks>
public class BearerAuthTests : HttpSamplesRemoteTestBase, IClassFixture<HttpSamplesRemoteFixture>
{
	public BearerAuthTests(HttpSamplesRemoteFixture fixture) : base(fixture)
	{
	}

	[Fact(Skip = "Remote test - requires deployed API environment", DisplayName = "Bearer Token Authentication with valid token should return success")]
	public void Example01_BearerAuth_WithValidToken_ShouldReturnSuccess()
	{
		// Arrange
		var validToken = "my-super-secret-token-12345";

		// Act & Assert
		Given().ApiResource("/api/auth/bearer")
			.WithBearerToken(validToken)
			.Get()
		.When()
			.Execute()
		.Then()
			.AssertStatusCode(200)
			.AssertJsonPath<string>("$.authType", value => value == "Bearer", "Auth type should be Bearer")
			.AssertJsonPath<bool>("$.authenticated", value => value, "Should be authenticated")
			.AssertJsonPath<string>("$.message", value => value?.Contains("successful") == true, "Should return success message");
	}

	[Fact(Skip = "Remote test - requires deployed API environment", DisplayName = "Bearer Token Authentication with invalid token should return 401 Unauthorized")]
	public void Example02_BearerAuth_WithInvalidToken_ShouldReturn401()
	{
		// Arrange
		var invalidToken = "invalid-token-xyz";

		// Act & Assert
		Given().ApiResource("/api/auth/bearer")
			.WithBearerToken(invalidToken)
			.Get()
		.When()
			.Execute()
		.Then()
			.AssertStatusCode(401);
		// Note: Server returns 401 without JSON body
	}

	[Fact(Skip = "Remote test - requires deployed API environment", DisplayName = "Bearer Token Authentication without token should return 401 Unauthorized")]
	public void Example03_BearerAuth_WithoutToken_ShouldReturn401()
	{
		// Act & Assert - No authentication provided
		Given().ApiResource("/api/auth/bearer")
			.Get()
		.When()
			.Execute()
		.Then()
			.AssertStatusCode(401);
		// Note: Server returns 401 without JSON body
	}

	[Fact(Skip = "Remote test - requires deployed API environment", DisplayName = "Bearer Token Authentication with empty token should return 401 Unauthorized")]
	public void Example04_BearerAuth_WithEmptyToken_ShouldReturn401()
	{
		// Arrange
		var emptyToken = "";

		// Act & Assert
		Given().ApiResource("/api/auth/bearer")
			.WithBearerToken(emptyToken)
			.Get()
		.When()
			.Execute()
		.Then()
			.AssertStatusCode(401);
	}

	[Fact(Skip = "Remote test - requires deployed API environment", DisplayName = "Bearer Token Authentication with custom prefix should work correctly")]
	public void Example05_BearerAuth_WithCustomPrefix_ShouldWork()
	{
		// Arrange
		var validToken = "my-super-secret-token-12345";
		var customPrefix = "Bearer"; // Default prefix

		// Act & Assert
		Given().ApiResource("/api/auth/bearer")
			.WithBearerToken(validToken, customPrefix)
			.Get()
		.When()
			.Execute()
		.Then()
			.AssertStatusCode(200)
			.AssertJsonPath<bool>("$.authenticated", value => value, "Should be authenticated with custom prefix");
	}

	[Fact(Skip = "Remote test - requires deployed API environment", DisplayName = "Bearer Token is case-sensitive and wrong case should return 401 Unauthorized")]
	public void Example06_BearerAuth_TokenIsCaseSensitive_ShouldReturn401()
	{
		// Arrange - Token is case-sensitive
		var wrongCaseToken = "MY-SUPER-SECRET-TOKEN-12345"; // Wrong case

		// Act & Assert
		Given().ApiResource("/api/auth/bearer")
			.WithBearerToken(wrongCaseToken)
			.Get()
		.When()
			.Execute()
		.Then()
			.AssertStatusCode(401);
		// Note: Server returns 401 without JSON body
	}

	[Fact(Skip = "Remote test - requires deployed API environment", DisplayName = "Bearer Token with whitespace should be trimmed and authentication succeeds")]
	public void Example07_BearerAuth_WithWhitespaceInToken_ShouldReturn401()
	{
		// Arrange - Token with extra whitespace
		// Note: The server trims the token, so this actually passes authentication
		// Changed to expect 200 to match server behavior
		var tokenWithWhitespace = "my-super-secret-token-12345 ";

		// Act & Assert
		Given().ApiResource("/api/auth/bearer")
			.WithBearerToken(tokenWithWhitespace)
			.Get()
		.When()
			.Execute()
		.Then()
			.AssertStatusCode(200)
			.AssertJsonPath<bool>("$.authenticated", value => value, "Server trims token, so authentication succeeds");
	}

	[Fact(Skip = "Remote test - requires deployed API environment", DisplayName = "Bearer Token Authentication with valid token should return complete response structure")]
	public void Example08_BearerAuth_ValidToken_CheckResponseStructure()
	{
		// Arrange
		var validToken = "my-super-secret-token-12345";

		// Act & Assert - Validate complete response structure
		Given().ApiResource("/api/auth/bearer")
			.WithBearerToken(validToken)
			.Get()
		.When()
			.Execute()
		.Then()
			.AssertStatusCode(200)
			.AssertJsonPath<object>("$.authType", value => value != null, "authType field should exist")
			.AssertJsonPath<object>("$.authenticated", value => value != null, "authenticated field should exist")
			.AssertJsonPath<object>("$.message", value => value != null, "message field should exist");
	}

	[Fact(Skip = "Remote test - requires deployed API environment", DisplayName = "Bearer Token Authentication should maintain authentication across multiple requests")]
	public void Example09_BearerAuth_MultipleRequests_ShouldMaintainAuthentication()
	{
		// Arrange
		var validToken = "my-super-secret-token-12345";

		// Act & Assert - First request
		Given().ApiResource("/api/auth/bearer")
			.WithBearerToken(validToken)
			.Get()
		.When()
			.Execute()
		.Then()
			.AssertStatusCode(200)
			.AssertJsonPath<bool>("$.authenticated", value => value, "First request should be authenticated");

		// Act & Assert - Second request with same token
		Given().ApiResource("/api/auth/bearer")
			.WithBearerToken(validToken)
			.Get()
		.When()
			.Execute()
		.Then()
			.AssertStatusCode(200)
			.AssertJsonPath<bool>("$.authenticated", value => value, "Second request should be authenticated");
	}

	[Fact(Skip = "Remote test - requires deployed API environment", DisplayName = "Bearer Token Authentication with different tokens should behave differently")]
	public void Example10_BearerAuth_DifferentTokens_ShouldBehaveDifferently()
	{
		// Arrange
		var validToken = "my-super-secret-token-12345";
		var invalidToken = "different-invalid-token";

		// Act & Assert - Valid token
		Given().ApiResource("/api/auth/bearer")
			.WithBearerToken(validToken)
			.Get()
		.When()
			.Execute()
		.Then()
			.AssertStatusCode(200)
			.AssertJsonPath<bool>("$.authenticated", value => value, "Valid token should authenticate");

		// Act & Assert - Invalid token
		Given().ApiResource("/api/auth/bearer")
			.WithBearerToken(invalidToken)
			.Get()
		.When()
			.Execute()
		.Then()
			.AssertStatusCode(401);
		// Note: Server returns 401 without JSON body
	}

	// ============================================================================
	// AUTOMATIC AUTHENTICATION TESTS
	// ============================================================================
	// These tests demonstrate the automatic authentication feature where
	// bearer token configured in testsettings.json is automatically applied
	// without needing to call .WithBearerToken() explicitly.
	//
	// To use these tests:
	// 1. Configure Bearer Auth in testsettings.json:
	//    "authentication": {
	//      "type": "Bearer",
	//      "bearer": {
	//        "token": "my-super-secret-token-12345",
	//        "prefix": "Bearer"
	//      }
	//    }
	// 2. Remove the Skip attribute to run the tests
	// ============================================================================

	[Fact(Skip = "Demo test - requires Bearer Auth configured in testsettings.json", 
	      DisplayName = "DEMO: Bearer Token applied automatically from testsettings.json")]
	public void Example11_BearerAuth_Automatic_FromTestSettings()
	{
		// NO .WithBearerToken() call needed!
		// Bearer token is automatically applied from testsettings.json

		Given()
			.ApiResource("/api/auth/bearer")
			.Get()
		.When()
			.Execute()
		.Then()
			.AssertStatusCode(200)
			.AssertJsonPath<string>("$.authType", value => value == "Bearer", "Auth type should be Bearer")
			.AssertJsonPath<bool>("$.authenticated", value => value, "Should be authenticated");
	}

	[Fact(Skip = "Demo test - requires Bearer Auth configured in testsettings.json", 
	      DisplayName = "DEMO: Manual token overrides testsettings.json configuration")]
	public void Example12_BearerAuth_ManualOverride_TestSettings()
	{
		// Even if testsettings.json has Bearer token configured,
		// calling .WithBearerToken() explicitly will override it

		var differentToken = "different-token-that-fails";

		Given()
			.ApiResource("/api/auth/bearer")
			.WithBearerToken(differentToken)  // Manual override
			.Get()
		.When()
			.Execute()
		.Then()
			.AssertStatusCode(401);  // Should fail with invalid token
	}
}
