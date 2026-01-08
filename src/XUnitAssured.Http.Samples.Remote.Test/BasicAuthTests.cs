using XUnitAssured.Http.Extensions;
using XUnitAssured.Http.Extensions;

namespace XUnitAssured.Http.Samples.Remote.Test;

[Trait("Authentication", "Basic")]
[Trait("Environment", "Remote")]
/// <summary>
/// Remote tests demonstrating Basic Authentication against a deployed API.
/// Basic Auth sends credentials as base64-encoded "username:password" in the Authorization header.
/// </summary>
/// <remarks>
/// Tests the same authentication endpoints as local tests but against a remote server.
/// Requires remote API to be configured in testsettings.json.
/// </remarks>
public class BasicAuthTests : HttpSamplesRemoteTestBase, IClassFixture<HttpSamplesRemoteFixture>
{
	public BasicAuthTests(HttpSamplesRemoteFixture fixture) : base(fixture)
	{
	}

	[Fact(Skip = "Remote test - requires deployed API environment", DisplayName = "Basic Authentication with valid credentials should return success")]
	public void Example01_BasicAuth_WithValidCredentials_ShouldReturnSuccess()
	{
		// Arrange
		var username = "admin";
		var password = "secret123";

		// Act & Assert
		Given()
			.ApiResource("/api/auth/basic")
			.WithBasicAuth(username, password)
			.Get()
		.When()
			.Execute()
		.Then()
			.AssertStatusCode(200)
			.AssertJsonPath<string>("$.authType", value => value == "Basic", "Auth type should be Basic")
			.AssertJsonPath<bool>("$.authenticated", value => value, "Should be authenticated")
			.AssertJsonPath<string>("$.username", value => value == username, $"Username should be '{username}'")
			.AssertJsonPath<string>("$.message", value => value?.Contains("successful") == true, "Should return success message");
	}

	[Fact(Skip = "Remote test - requires deployed API environment", DisplayName = "Basic Authentication with invalid credentials should return 401 Unauthorized")]
	public void Example02_BasicAuth_WithInvalidCredentials_ShouldReturn401()
	{
		// Arrange
		var username = "admin";
		var wrongPassword = "wrongpassword";

		// Act & Assert
		Given().ApiResource("/api/auth/basic")
			.WithBasicAuth(username, wrongPassword)
			.Get()
		.When()
			.Execute()
		.Then()
			.AssertStatusCode(401);
	}

	[Fact(Skip = "Remote test - requires deployed API environment", DisplayName = "Basic Authentication with invalid username should return 401 Unauthorized")]
	public void Example03_BasicAuth_WithInvalidUsername_ShouldReturn401()
	{
		// Arrange
		var wrongUsername = "invaliduser";
		var password = "secret123";

		// Act & Assert
		Given().ApiResource("/api/auth/basic")
			.WithBasicAuth(wrongUsername, password)
			.Get()
		.When()
			.Execute()
		.Then()
			.AssertStatusCode(401);
	}

	[Fact(Skip = "Remote test - requires deployed API environment", DisplayName = "Basic Authentication without credentials should return 401 Unauthorized")]
	public void Example04_BasicAuth_WithoutCredentials_ShouldReturn401()
	{
		// Act & Assert - No authentication provided
		Given().ApiResource("/api/auth/basic")
			.Get()
		.When()
			.Execute()
		.Then()
			.AssertStatusCode(401);
	}

	[Fact(Skip = "Remote test - requires deployed API environment", DisplayName = "Basic Authentication with empty username should return 401 Unauthorized")]
	public void Example05_BasicAuth_WithEmptyUsername_ShouldReturn401()
	{
		// Arrange
		var emptyUsername = "";
		var password = "secret123";

		// Act & Assert
		Given().ApiResource("/api/auth/basic")
			.WithBasicAuth(emptyUsername, password)
			.Get()
		.When()
			.Execute()
		.Then()
			.AssertStatusCode(401);
	}

	[Fact(Skip = "Remote test - requires deployed API environment", DisplayName = "Basic Authentication with empty password should return 401 Unauthorized")]
	public void Example06_BasicAuth_WithEmptyPassword_ShouldReturn401()
	{
		// Arrange
		var username = "admin";
		var emptyPassword = "";

		// Act & Assert
		Given().ApiResource("/api/auth/basic")
			.WithBasicAuth(username, emptyPassword)
			.Get()
		.When()
			.Execute()
		.Then()
			.AssertStatusCode(401);
	}

	[Fact(Skip = "Remote test - requires deployed API environment", DisplayName = "Basic Authentication with case-sensitive username should return 401 Unauthorized")]
	public void Example07_BasicAuth_CaseSensitiveUsername_ShouldReturn401()
	{
		// Arrange - Username is case-sensitive
		var username = "Admin"; // Wrong case
		var password = "secret123";

		// Act & Assert
		Given().ApiResource("/api/auth/basic")
			.WithBasicAuth(username, password)
			.Get()
		.When()
			.Execute()
		.Then()
			.AssertStatusCode(401);
	}

	[Fact(Skip = "Remote test - requires deployed API environment", DisplayName = "Basic Authentication with case-sensitive password should return 401 Unauthorized")]
	public void Example08_BasicAuth_CaseSensitivePassword_ShouldReturn401()
	{
		// Arrange - Password is case-sensitive
		var username = "admin";
		var password = "Secret123"; // Wrong case

		// Act & Assert
		Given().ApiResource("/api/auth/basic")
			.WithBasicAuth(username, password)
			.Get()
		.When()
			.Execute()
		.Then()
			.AssertStatusCode(401);
	}

	[Fact(Skip = "Remote test - requires deployed API environment", DisplayName = "Basic Authentication with valid credentials should return complete response structure")]
	public void Example09_BasicAuth_ValidCredentials_CheckResponseStructure()
	{
		// Arrange
		var username = "admin";
		var password = "secret123";

		// Act & Assert - Validate complete response structure
		Given().ApiResource("/api/auth/basic")
			.WithBasicAuth(username, password)
			.Get()
		.When()
			.Execute()
		.Then()
			.AssertStatusCode(200)
			.AssertJsonPath<object>("$.authType", value => value != null, "authType field should exist")
			.AssertJsonPath<object>("$.authenticated", value => value != null, "authenticated field should exist")
			.AssertJsonPath<object>("$.username", value => value != null, "username field should exist")
			.AssertJsonPath<object>("$.message", value => value != null, "message field should exist");
	}

	[Fact(Skip = "Remote test - requires deployed API environment", DisplayName = "Basic Authentication should maintain authentication across multiple requests")]
	public void Example10_BasicAuth_MultipleRequests_ShouldMaintainAuthentication()
	{
		// Arrange
		var username = "admin";
		var password = "secret123";

		// Act & Assert - First request
		Given().ApiResource("/api/auth/basic")
			.WithBasicAuth(username, password)
			.Get()
		.When()
			.Execute()
		.Then()
			.AssertStatusCode(200)
			.AssertJsonPath<bool>("$.authenticated", value => value, "First request should be authenticated");

		// Act & Assert - Second request with same credentials
		Given().ApiResource("/api/auth/basic")
			.WithBasicAuth(username, password)
			.Get()
		.When()
			.Execute()
		.Then()
			.AssertStatusCode(200)
			.AssertJsonPath<bool>("$.authenticated", value => value, "Second request should be authenticated");
	}
}
