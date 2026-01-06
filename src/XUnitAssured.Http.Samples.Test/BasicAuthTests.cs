using XUnitAssured.Extensions.Http;
using XUnitAssured.Http.Extensions;

using static XUnitAssured.Core.DSL.ScenarioDsl;

namespace XUnitAssured.Http.Samples.Test;

/// <summary>
/// Sample tests demonstrating Basic Authentication using XUnitAssured.Http.
/// Basic Auth sends credentials as base64-encoded "username:password" in the Authorization header.
/// </summary>
public class BasicAuthTests : IClassFixture<HttpSamplesFixture>
{
	private readonly HttpSamplesFixture _fixture;

	public BasicAuthTests(HttpSamplesFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public void Example01_BasicAuth_WithValidCredentials_ShouldReturnSuccess()
	{
		// Arrange
		var username = "admin";
		var password = "secret123";

		// Act & Assert
		Given()
			.WithHttpClient(_fixture.CreateClient())
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

	[Fact]
	public void Example02_BasicAuth_WithInvalidCredentials_ShouldReturn401()
	{
		// Arrange
		var username = "admin";
		var wrongPassword = "wrongpassword";

		// Act & Assert
		Given()
			.WithHttpClient(_fixture.CreateClient())
			.ApiResource("/api/auth/basic")
			.WithBasicAuth(username, wrongPassword)
			.Get()
		.When()
			.Execute()
		.Then()
			.AssertStatusCode(401);
	}

	[Fact]
	public void Example03_BasicAuth_WithInvalidUsername_ShouldReturn401()
	{
		// Arrange
		var wrongUsername = "invaliduser";
		var password = "secret123";

		// Act & Assert
		Given()
			.WithHttpClient(_fixture.CreateClient())
			.ApiResource("/api/auth/basic")
			.WithBasicAuth(wrongUsername, password)
			.Get()
		.When()
			.Execute()
		.Then()
			.AssertStatusCode(401);
	}

	[Fact]
	public void Example04_BasicAuth_WithoutCredentials_ShouldReturn401()
	{
		// Act & Assert - No authentication provided
		Given()
			.WithHttpClient(_fixture.CreateClient())
			.ApiResource("/api/auth/basic")
			.Get()
		.When()
			.Execute()
		.Then()
			.AssertStatusCode(401);
	}

	[Fact]
	public void Example05_BasicAuth_WithEmptyUsername_ShouldReturn401()
	{
		// Arrange
		var emptyUsername = "";
		var password = "secret123";

		// Act & Assert
		Given()
			.WithHttpClient(_fixture.CreateClient())
			.ApiResource("/api/auth/basic")
			.WithBasicAuth(emptyUsername, password)
			.Get()
		.When()
			.Execute()
		.Then()
			.AssertStatusCode(401);
	}

	[Fact]
	public void Example06_BasicAuth_WithEmptyPassword_ShouldReturn401()
	{
		// Arrange
		var username = "admin";
		var emptyPassword = "";

		// Act & Assert
		Given()
			.WithHttpClient(_fixture.CreateClient())
			.ApiResource("/api/auth/basic")
			.WithBasicAuth(username, emptyPassword)
			.Get()
		.When()
			.Execute()
		.Then()
			.AssertStatusCode(401);
	}

	[Fact]
	public void Example07_BasicAuth_CaseSensitiveUsername_ShouldReturn401()
	{
		// Arrange - Username is case-sensitive
		var username = "Admin"; // Wrong case
		var password = "secret123";

		// Act & Assert
		Given()
			.WithHttpClient(_fixture.CreateClient())
			.ApiResource("/api/auth/basic")
			.WithBasicAuth(username, password)
			.Get()
		.When()
			.Execute()
		.Then()
			.AssertStatusCode(401);
	}

	[Fact]
	public void Example08_BasicAuth_CaseSensitivePassword_ShouldReturn401()
	{
		// Arrange - Password is case-sensitive
		var username = "admin";
		var password = "Secret123"; // Wrong case

		// Act & Assert
		Given()
			.WithHttpClient(_fixture.CreateClient())
			.ApiResource("/api/auth/basic")
			.WithBasicAuth(username, password)
			.Get()
		.When()
			.Execute()
		.Then()
			.AssertStatusCode(401);
	}

	[Fact]
	public void Example09_BasicAuth_ValidCredentials_CheckResponseStructure()
	{
		// Arrange
		var username = "admin";
		var password = "secret123";

		// Act & Assert - Validate complete response structure
		Given()
			.WithHttpClient(_fixture.CreateClient())
			.ApiResource("/api/auth/basic")
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

	[Fact]
	public void Example10_BasicAuth_MultipleRequests_ShouldMaintainAuthentication()
	{
		// Arrange
		var username = "admin";
		var password = "secret123";

		// Act & Assert - First request
		Given()
			.WithHttpClient(_fixture.CreateClient())
			.ApiResource("/api/auth/basic")
			.WithBasicAuth(username, password)
			.Get()
		.When()
			.Execute()
		.Then()
			.AssertStatusCode(200)
			.AssertJsonPath<bool>("$.authenticated", value => value, "First request should be authenticated");

		// Act & Assert - Second request with same credentials
		Given()
			.WithHttpClient(_fixture.CreateClient())
			.ApiResource("/api/auth/basic")
			.WithBasicAuth(username, password)
			.Get()
		.When()
			.Execute()
		.Then()
			.AssertStatusCode(200)
			.AssertJsonPath<bool>("$.authenticated", value => value, "Second request should be authenticated");
	}
}
