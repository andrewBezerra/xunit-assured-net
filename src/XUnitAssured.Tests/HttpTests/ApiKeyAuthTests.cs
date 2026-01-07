using XUnitAssured.Http.Configuration;
using XUnitAssured.Http.Extensions;

namespace XUnitAssured.Tests.HttpTests;

[Trait("Category", "Http")]
[Trait("Authentication", "ApiKey")]
/// <summary>
/// Tests for API Key authentication.
/// </summary>
public class ApiKeyAuthTests
{
	[Fact(DisplayName = "Should add API key in HTTP header successfully")]
	public void Should_Add_ApiKey_In_Header()
	{
		// Arrange & Act
		var scenario = Given()
			.ApiResource("https://api.example.com/resource")
			.WithApiKey("X-API-Key", "test-key-123")
			.Get();

		// Assert
		scenario.ShouldNotBeNull();
		scenario.CurrentStep.ShouldNotBeNull();
	}

	[Fact(DisplayName = "Should add API key in query parameter successfully")]
	public void Should_Add_ApiKey_In_Query()
	{
		// Arrange & Act
		var scenario = Given()
			.ApiResource("https://api.example.com/resource")
			.WithApiKey("api_key", "test-key-123", ApiKeyLocation.Query)
			.Get();

		// Assert
		scenario.ShouldNotBeNull();
	}

	[Fact(DisplayName = "Should support custom API key name")]
	public void Should_Support_Custom_Key_Name()
	{
		// Arrange & Act
		var scenario = Given()
			.ApiResource("https://api.example.com/resource")
			.WithApiKey("X-Custom-Key", "custom-value")
			.Get();

		// Assert
		scenario.ShouldNotBeNull();
	}

	[Fact(DisplayName = "Should support API key configuration via auth config")]
	public void Should_Support_ApiKey_Via_Config()
	{
		// Arrange & Act
		var scenario = Given()
			.ApiResource("https://api.example.com/resource")
			.WithAuthConfig(config =>
			{
				config.UseApiKey("X-API-Key", "test-key", ApiKeyLocation.Header);
			})
			.Get();

		// Assert
		scenario.ShouldNotBeNull();
	}

	[Fact(DisplayName = "ApiKeyAuthConfig should have default values")]
	public void ApiKeyAuthConfig_Should_Have_Defaults()
	{
		// Arrange & Act
		var config = new ApiKeyAuthConfig();

		// Assert
		config.KeyName.ShouldBe("X-API-Key");
		config.Location.ShouldBe(ApiKeyLocation.Header);
	}

	[Fact(Skip = "Integration test - requires real API with API key", DisplayName = "Integration test should authenticate with API key successfully")]
	public void Integration_Should_Authenticate_With_ApiKey()
	{
		var apiKey = Environment.GetEnvironmentVariable("TEST_API_KEY");

		if (!string.IsNullOrEmpty(apiKey))
		{
			Given()
				.ApiResource("https://api.example.com/protected")
				.WithApiKey("X-API-Key", apiKey)
				.Get()
				.Validate(response =>
				{
					response.StatusCode.ShouldBe(200);
				});
		}
	}
}
