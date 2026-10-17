using Flurl.Http.Testing;

using XUnitAssured.Http.Configuration;
using XUnitAssured.Http.Extensions;

namespace XUnitAssured.Tests.HttpTests;

[Trait("Category", "Http")]
[Trait("Component", "Authentication")]
/// <summary>
/// Tests for HTTP authentication functionality.
/// </summary>
public class AuthenticationTests
{
	// Executa o pedido sem rede (#169) e confere o cabeçalho que saiu: antes, o teste só montava
	// o cenário, contra o httpbin.org, e não conferia a autenticação.
	[Fact(DisplayName = "Should add Basic Authentication header successfully")]
	public async Task Should_Add_Basic_Auth_Header()
	{
		// Arrange
		using var http = new HttpTest();
		http.RespondWith("{}");

		// Act
		await Given()
			.ApiResource("http://api.teste/basic-auth")
			.WithBasicAuth("user", "pass")
			.Get()
			.ExecuteAsync();

		// Assert: "user:pass" em Base64
		http.ShouldHaveCalled("http://api.teste/basic-auth")
			.WithHeader("Authorization", "Basic dXNlcjpwYXNz")
			.Times(1);
	}

	[Fact(DisplayName = "Should add Bearer token header successfully")]
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

	[Fact(DisplayName = "Should support custom token prefix for Bearer authentication")]
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

	[Fact(DisplayName = "Should support authentication configuration via action")]
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

	[Fact(DisplayName = "Should allow no authentication override")]
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

	[Fact(DisplayName = "Should throw InvalidOperationException when not HTTP step")]
	public void Should_Throw_When_Not_Http_Step()
	{
		// Arrange
		var scenario = Given();

		// Act & Assert
		Should.Throw<InvalidOperationException>(() => 
			scenario.WithBasicAuth("user", "pass"));
	}
}

/// <summary>
/// Examples kept here so they compile against the real API. They are not run: each needs
/// infrastructure this suite does not start, as the comment on it says.
/// </summary>
public static class AuthenticationExamples
{
	// Compiled, not run: requires API with bearer token.
	public static void Integration_Should_Authenticate_With_Bearer_Token()
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
