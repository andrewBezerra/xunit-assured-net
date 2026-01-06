using XUnitAssured.Extensions.Http;
using XUnitAssured.Http.Configuration;
using XUnitAssured.Http.Extensions;

using static XUnitAssured.Core.DSL.ScenarioDsl;

namespace XUnitAssured.Http.Samples.Test;

/// <summary>
/// Sample tests demonstrating OAuth2 Authentication using XUnitAssured.Http.
/// Showcases the Client Credentials flow with automatic token management.
/// </summary>
public class OAuth2AuthTests : IClassFixture<HttpSamplesFixture>
{
	private readonly HttpSamplesFixture _fixture;

	public OAuth2AuthTests(HttpSamplesFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public void Example01_OAuth2_TokenEndpoint_WithValidCredentials_ShouldReturnToken()
	{
		// Arrange
		var clientId = "test-client-id";
		var clientSecret = "test-client-secret";

		// Create form data
		var formData = new Dictionary<string, string>
		{
			["grant_type"] = "client_credentials",
			["client_id"] = clientId,
			["client_secret"] = clientSecret
		};

		// Act & Assert - Request OAuth2 token using form data
		Given()
			.WithHttpClient(_fixture.CreateClient())
			.ApiResource($"/api/auth/oauth2/token")
			.PostFormData(formData)
		.When()
			.Execute()
		.Then()
			.AssertStatusCode(200)
			.AssertJsonPath<string>("$.access_token", value => !string.IsNullOrEmpty(value), "Should return access token")
			.AssertJsonPath<string>("$.token_type", value => value == "Bearer", "Token type should be Bearer")
			.AssertJsonPath<int>("$.expires_in", value => value > 0, "Should have expiration time")
			.AssertJsonPath<string>("$.access_token", value => value?.StartsWith("oauth2-access-token-") == true, "Access token should have correct prefix");
	}

	[Fact]
	public void Example02_OAuth2_TokenEndpoint_WithInvalidClientId_ShouldReturn401()
	{
		// Arrange
		var invalidClientId = "invalid-client";
		var clientSecret = "test-client-secret";

		// Create form data
		var formData = new Dictionary<string, string>
		{
			["grant_type"] = "client_credentials",
			["client_id"] = invalidClientId,
			["client_secret"] = clientSecret
		};

		// Act & Assert
		Given()
			.WithHttpClient(_fixture.CreateClient())
			.ApiResource("/api/auth/oauth2/token")
			.PostFormData(formData)
		.When()
			.Execute()
		.Then()
			.AssertStatusCode(401);
	}

	[Fact]
	public void Example03_OAuth2_TokenEndpoint_WithInvalidClientSecret_ShouldReturn401()
	{
		// Arrange
		var clientId = "test-client-id";
		var invalidSecret = "wrong-secret";

		// Create form data
		var formData = new Dictionary<string, string>
		{
			["grant_type"] = "client_credentials",
			["client_id"] = clientId,
			["client_secret"] = invalidSecret
		};

		// Act & Assert
		Given()
			.WithHttpClient(_fixture.CreateClient())
			.ApiResource("/api/auth/oauth2/token")
			.PostFormData(formData)
		.When()
			.Execute()
		.Then()
			.AssertStatusCode(401);
	}

	[Fact]
	public void Example04_OAuth2_TokenEndpoint_WithUnsupportedGrantType_ShouldReturn400()
	{
		// Arrange
		var clientId = "test-client-id";
		var clientSecret = "test-client-secret";

		// Create form data
		var formData = new Dictionary<string, string>
		{
			["grant_type"] = "password", // Unsupported grant type
			["client_id"] = clientId,
			["client_secret"] = clientSecret
		};

		// Act & Assert
		Given()
			.WithHttpClient(_fixture.CreateClient())
			.ApiResource("/api/auth/oauth2/token")
			.PostFormData(formData)
		.When()
			.Execute()
		.Then()
			.AssertStatusCode(400)
			.AssertJsonPath<string>("$.error", value => value == "unsupported_grant_type", "Error should be unsupported_grant_type");
	}

	[Fact]
	public void Example05_OAuth2_CompleteFlow_GetTokenAndAccessProtectedEndpoint()
	{
		// Step 1: Request OAuth2 token
		var clientId = "test-client-id";
		var clientSecret = "test-client-secret";

		// Create form data
		var formData = new Dictionary<string, string>
		{
			["grant_type"] = "client_credentials",
			["client_id"] = clientId,
			["client_secret"] = clientSecret
		};

		var tokenResponse = Given()
			.WithHttpClient(_fixture.CreateClient())
			.ApiResource("/api/auth/oauth2/token")
			.PostFormData(formData)
		.When()
			.Execute()
		.Then()
			.AssertStatusCode(200)
			.GetResult();

		// Extract access token
		var accessToken = tokenResponse.JsonPath<string>("$.access_token");

		// Step 2: Use the token to access protected endpoint
		Given()
			.WithHttpClient(_fixture.CreateClient())
			.ApiResource($"/api/auth/oauth2/protected")
			.WithBearerToken(accessToken)
			.Get()
		.When()
			.Execute()
		.Then()
			.AssertStatusCode(200)
			.AssertJsonPath<string>("$.authType", value => value == "OAuth2", "Auth type should be OAuth2")
			.AssertJsonPath<bool>("$.authenticated", value => value, "Should be authenticated")
			.AssertJsonPath<string>("$.message", value => value?.Contains("successful") == true, "Should return success message");
	}

	[Fact]
	public void Example06_OAuth2_ProtectedEndpoint_WithoutToken_ShouldReturn401()
	{
		// Act & Assert - No token provided
		Given()
			.WithHttpClient(_fixture.CreateClient())
			.ApiResource($"/api/auth/oauth2/protected")
			.Get()
		.When()
			.Execute()
		.Then()
			.AssertStatusCode(401);
	}

	[Fact]
	public void Example07_OAuth2_ProtectedEndpoint_WithInvalidToken_ShouldReturn401()
	{
		// Arrange
		var invalidToken = "invalid-oauth2-token";

		// Act & Assert
		Given()
			.WithHttpClient(_fixture.CreateClient())
			.ApiResource($"/api/auth/oauth2/protected")
			.WithBearerToken(invalidToken)
			.Get()
		.When()
			.Execute()
		.Then()
			.AssertStatusCode(401);
	}

	[Fact(Skip = "WithOAuth2 extension requires OAuth2Handler implementation fix for form-urlencoded")]
	public void Example08_OAuth2_UsingXUnitAssuredOAuth2Extension()
	{
		// Arrange
		var tokenUrl = $"{_fixture.BaseUrl}/api/auth/oauth2/token";
		var clientId = "test-client-id";
		var clientSecret = "test-client-secret";

		// Act & Assert - Using XUnitAssured's OAuth2 extension
		// Note: This demonstrates the WithOAuth2 extension which handles token retrieval automatically
		Given()
			.WithHttpClient(_fixture.CreateClient())
			.ApiResource($"/api/auth/oauth2/protected")
			.WithOAuth2(tokenUrl, clientId, clientSecret)
			.Get()
		.When()
			.Execute()
		.Then()
			.AssertStatusCode(200)
			.AssertJsonPath<string>("$.authType", value => value == "OAuth2", "Auth type should be OAuth2")
			.AssertJsonPath<bool>("$.authenticated", value => value, "Should be authenticated with OAuth2 extension");
	}

	[Fact]
	public void Example09_OAuth2_WithScopes_ShouldIncludeScopeInToken()
	{
		// Arrange
		var clientId = "test-client-id";
		var clientSecret = "test-client-secret";
		var scopes = "read write admin";

		// Create form data
		var formData = new Dictionary<string, string>
		{
			["grant_type"] = "client_credentials",
			["client_id"] = clientId,
			["client_secret"] = clientSecret,
			["scope"] = scopes
		};

		// Act & Assert
		Given()
			.WithHttpClient(_fixture.CreateClient())
			.ApiResource($"/api/auth/oauth2/token")
			.PostFormData(formData)
		.When()
			.Execute()
		.Then()
			.AssertStatusCode(200)
			.AssertJsonPath<string>("$.access_token", value => !string.IsNullOrEmpty(value), "Should return access token")
			.AssertJsonPath<string>("$.scope", value => value == scopes, $"Should return requested scopes: {scopes}");
	}

	[Fact(Skip = "WithOAuth2Config extension requires OAuth2Handler implementation fix for form-urlencoded")]
	public void Example10_OAuth2_UsingCustomConfiguration()
	{
		// Arrange
		var tokenUrl = $"{_fixture.BaseUrl}/api/auth/oauth2/token";

		// Act & Assert - Using WithOAuth2Config for advanced configuration
		Given()
			.WithHttpClient(_fixture.CreateClient())
			.ApiResource($"/api/auth/oauth2/protected")
			.WithOAuth2Config(config =>
			{
				config.TokenUrl = tokenUrl;
				config.ClientId = "test-client-id";
				config.ClientSecret = "test-client-secret";
				config.GrantType = OAuth2GrantType.ClientCredentials;
				config.Scopes = ["read", "write"];
			})
			.Get()
		.When()
			.Execute()
		.Then()
			.AssertStatusCode(200)
			.AssertJsonPath<bool>("$.authenticated", value => value, "Should be authenticated with custom OAuth2 config");
	}

	[Fact(Skip = "WithOAuth2 extension requires OAuth2Handler implementation fix for form-urlencoded")]
	public void Example11_OAuth2_MultipleRequests_ShouldReuseToken()
	{
		// Arrange
		var tokenUrl = $"{_fixture.BaseUrl}/api/auth/oauth2/token";
		var clientId = "test-client-id";
		var clientSecret = "test-client-secret";

		// Act & Assert - First request (will get token)
		Given()
			.WithHttpClient(_fixture.CreateClient())
			.ApiResource($"/api/auth/oauth2/protected")
			.WithOAuth2(tokenUrl, clientId, clientSecret)
			.Get()
		.When()
			.Execute()
		.Then()
			.AssertStatusCode(200)
			.AssertJsonPath<bool>("$.authenticated", value => value, "First request should be authenticated");

		// Act & Assert - Second request (should reuse cached token)
		Given()
			.WithHttpClient(_fixture.CreateClient())
			.ApiResource($"/api/auth/oauth2/protected")
			.WithOAuth2(tokenUrl, clientId, clientSecret)
			.Get()
		.When()
			.Execute()
		.Then()
			.AssertStatusCode(200)
			.AssertJsonPath<bool>("$.authenticated", value => value, "Second request should be authenticated with cached token");
	}

	[Fact]
	public void Example12_OAuth2_TokenResponse_ValidateStructure()
	{
		// Arrange
		var clientId = "test-client-id";
		var clientSecret = "test-client-secret";

		// Create form data
		var formData = new Dictionary<string, string>
		{
			["grant_type"] = "client_credentials",
			["client_id"] = clientId,
			["client_secret"] = clientSecret
		};

		// Act & Assert - Validate complete OAuth2 token response structure
		Given()
			.WithHttpClient(_fixture.CreateClient())
			.ApiResource($"/api/auth/oauth2/token")
			.PostFormData(formData)
		.When()
			.Execute()
		.Then()
			.AssertStatusCode(200)
			.AssertJsonPath<string>("$.access_token", value => !string.IsNullOrEmpty(value), "access_token field should exist")
			.AssertJsonPath<string>("$.token_type", value => !string.IsNullOrEmpty(value), "token_type field should exist")
			.AssertJsonPath<int>("$.expires_in", value => value > 0, "expires_in field should exist")
			.AssertJsonPath<string>("$.scope", value => !string.IsNullOrEmpty(value), "scope field should exist");
	}
}
