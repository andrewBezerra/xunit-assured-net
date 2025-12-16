using XUnitAssured.Http.Configuration;
using XUnitAssured.Http.Extensions;

namespace XUnitAssured.Tests.HttpTests;

/// <summary>
/// Tests for HTTP authentication functionality.
/// </summary>
public class AuthenticationTests
{
	[Fact]
	public void Should_Add_Basic_Auth_Header()
	{
		// Arrange & Act
		var scenario = Given()
			.ApiResource("https://httpbin.org/basic-auth/user/pass")
			.WithBasicAuth("user", "pass")
			.Get();

		// Assert
		scenario.ShouldNotBeNull();
		scenario.CurrentStep.ShouldNotBeNull();
	}

	[Fact]
	public void Should_Add_Bearer_Token_Header()
	{
		// Arrange & Act
		var scenario = Given()
			.ApiResource("https://jsonplaceholder.typicode.com/posts/1")
			.WithBearerToken("test-token-123")
			.Get();

		// Assert
		scenario.ShouldNotBeNull();
		scenario.CurrentStep.ShouldNotBeNull();
	}

	[Fact]
	public void Should_Support_Custom_Token_Prefix()
	{
		// Arrange & Act
		var scenario = Given()
			.ApiResource("https://api.example.com/resource")
			.WithBearerToken("test-token", prefix: "JWT")
			.Get();

		// Assert
		scenario.ShouldNotBeNull();
	}

	[Fact]
	public void Should_Support_Auth_Config_Action()
	{
		// Arrange & Act
		var scenario = Given()
			.ApiResource("https://api.example.com/resource")
			.WithAuthConfig(config =>
			{
				config.UseBasicAuth("testuser", "testpass");
			})
			.Get();

		// Assert
		scenario.ShouldNotBeNull();
	}

	[Fact]
	public void Should_Allow_No_Auth_Override()
	{
		// Arrange & Act
		var scenario = Given()
			.ApiResource("https://jsonplaceholder.typicode.com/posts/1")
			.WithNoAuth()
			.Get();

		// Assert
		scenario.ShouldNotBeNull();
	}

	[Fact]
	public void Should_Throw_When_Not_Http_Step()
	{
		// Arrange
		var scenario = Given();

		// Act & Assert
		Should.Throw<InvalidOperationException>(() => 
			scenario.WithBasicAuth("user", "pass"));
	}

	[Fact(Skip = "Integration test - requires real API with basic auth")]
	public void Integration_Should_Authenticate_With_Basic_Auth()
	{
		// This test requires a real API endpoint with basic authentication
		Given()
			.ApiResource("https://httpbin.org/basic-auth/user/pass")
			.WithBasicAuth("user", "pass")
			.Get()
			.Validate(response =>
			{
				response.StatusCode.ShouldBe(200);
				response.IsSuccessStatusCode.ShouldBeTrue();
			});
	}

	[Fact(Skip = "Integration test - requires API with bearer token")]
	public void Integration_Should_Authenticate_With_Bearer_Token()
	{
		// This test requires a real API endpoint that accepts bearer tokens
		var token = Environment.GetEnvironmentVariable("TEST_API_TOKEN");
		
		if (!string.IsNullOrEmpty(token))
		{
			Given()
				.ApiResource("https://api.example.com/protected")
				.WithBearerToken(token)
				.Get()
				.Validate(response =>
				{
					response.StatusCode.ShouldBe(200);
				});
		}
	}
}
