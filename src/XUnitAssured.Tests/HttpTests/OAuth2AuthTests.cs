namespace XUnitAssured.Tests.HttpTests;

/// <summary>
/// Tests for OAuth 2.0 authentication.
/// </summary>
public class OAuth2AuthTests
{
	[Fact]
	public void Should_Configure_OAuth2_Client_Credentials()
	{
		// Arrange & Act
		var scenario = Given()
			.ApiResource("https://api.example.com/resource")
			.WithOAuth2("https://auth.example.com/token", "client-id", "client-secret")
			.Get();

		// Assert
		scenario.ShouldNotBeNull();
		scenario.CurrentStep.ShouldNotBeNull();
	}

	[Fact]
	public void Should_Configure_OAuth2_With_Scopes()
	{
		// Arrange & Act
		var scenario = Given()
			.ApiResource("https://api.example.com/resource")
			.WithOAuth2("https://auth.example.com/token", "client-id", "secret", "api.read", "api.write")
			.Get();

		// Assert
		scenario.ShouldNotBeNull();
	}

	[Fact]
	public void Should_Configure_OAuth2_Password_Grant()
	{
		// Arrange & Act
		var scenario = Given()
			.ApiResource("https://api.example.com/resource")
			.WithOAuth2Config(config =>
			{
				config.TokenUrl = "https://auth.example.com/token";
				config.ClientId = "client-id";
				config.ClientSecret = "secret";
				config.GrantType = Http.Configuration.OAuth2GrantType.Password;
				config.Username = "user";
				config.Password = "pass";
			})
			.Get();

		// Assert
		scenario.ShouldNotBeNull();
	}

	[Fact]
	public void Should_Support_OAuth2_Via_AuthConfig()
	{
		// Arrange & Act
		var scenario = Given()
			.ApiResource("https://api.example.com/resource")
			.WithAuthConfig(config =>
			{
				config.UseOAuth2("https://auth.example.com/token", "id", "secret");
			})
			.Get();

		// Assert
		scenario.ShouldNotBeNull();
	}

	[Fact]
	public void OAuth2Config_Should_Have_Defaults()
	{
		// Arrange & Act
		var config = new Http.Configuration.OAuth2Config();

		// Assert
		config.GrantType.ShouldBe(Http.Configuration.OAuth2GrantType.ClientCredentials);
	}

	[Fact]
	public void Should_Throw_When_Not_Http_Step()
	{
		// Arrange
		var scenario = Given();

		// Act & Assert
		Should.Throw<System.InvalidOperationException>(() =>
			scenario.WithOAuth2("url", "id", "secret"));
	}

	[Fact(Skip = "Integration test - requires real OAuth2 server")]
	public void Integration_Should_Authenticate_With_OAuth2()
	{
		var tokenUrl = System.Environment.GetEnvironmentVariable("OAUTH_TOKEN_URL");
		var clientId = System.Environment.GetEnvironmentVariable("OAUTH_CLIENT_ID");
		var clientSecret = System.Environment.GetEnvironmentVariable("OAUTH_CLIENT_SECRET");

		if (!string.IsNullOrEmpty(tokenUrl) && !string.IsNullOrEmpty(clientId))
		{
			Given()
				.ApiResource("https://api.example.com/protected")
				.WithOAuth2(tokenUrl, clientId, clientSecret!)
				.Get()
				.Validate(response =>
				{
					response.StatusCode.ShouldBe(200);
				});
		}
	}
}
