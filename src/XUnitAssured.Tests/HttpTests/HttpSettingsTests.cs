using XUnitAssured.Http.Configuration;

namespace XUnitAssured.Tests.HttpTests;

[Trait("Category", "Http")]
[Trait("Component", "Configuration")]
/// <summary>
/// Tests for HttpSettings and HttpSettingsLoader.
/// </summary>
public class HttpSettingsTests
{
	[Fact(DisplayName = "Should create HttpSettings with default values")]
	public void Should_Create_Default_Settings()
	{
		// Act
		var settings = new HttpSettings();

		// Assert
		settings.ShouldNotBeNull();
		settings.Timeout.ShouldBe(30);
		settings.Authentication.ShouldNotBeNull();
		settings.Authentication.Type.ShouldBe(AuthenticationType.None);
	}

	[Fact(DisplayName = "Should merge HttpSettings correctly")]
	public void Should_Merge_Settings()
	{
		// Arrange
		var baseSettings = new HttpSettings
		{
			BaseUrl = "https://api.example.com",
			Timeout = 30,
			DefaultHeaders = new Dictionary<string, string>
			{
				["User-Agent"] = "Test/1.0"
			}
		};

		var overrideSettings = new HttpSettings
		{
			Timeout = 60,
			DefaultHeaders = new Dictionary<string, string>
			{
				["Authorization"] = "Bearer token"
			}
		};

		// Act
		var merged = baseSettings.Merge(overrideSettings);

		// Assert
		merged.BaseUrl.ShouldBe("https://api.example.com"); // From base
		merged.Timeout.ShouldBe(60); // Overridden
		merged.DefaultHeaders.ShouldNotBeNull();
		merged.DefaultHeaders.Count.ShouldBe(2); // Merged
	}

	[Fact(DisplayName = "Should load HttpSettings from configuration file")]
	public void Should_Load_Settings_From_File()
	{
		// Act
		var settings = HttpSettings.Load();

		// Assert
		settings.ShouldNotBeNull();
		// Will use default settings if httpsettings.json doesn't exist
	}

	[Fact(DisplayName = "Should clear HttpSettings cache successfully")]
	public void Should_Clear_Settings_Cache()
	{
		// Arrange
		HttpSettingsLoader.Load();

		// Act
		HttpSettingsLoader.ClearCache();

		// Assert - no exception thrown
		var settings = HttpSettingsLoader.Load();
		settings.ShouldNotBeNull();
	}

	[Fact(DisplayName = "Should support Basic Authentication configuration")]
	public void Should_Support_Basic_Auth_Config()
	{
		// Arrange
		var config = new HttpAuthConfig();

		// Act
		config.UseBasicAuth("testuser", "testpass");

		// Assert
		config.Type.ShouldBe(AuthenticationType.Basic);
		config.Basic.ShouldNotBeNull();
		config.Basic.Username.ShouldBe("testuser");
		config.Basic.Password.ShouldBe("testpass");
	}

	[Fact(DisplayName = "Should support Bearer token configuration")]
	public void Should_Support_Bearer_Token_Config()
	{
		// Arrange
		var config = new HttpAuthConfig();

		// Act
		config.UseBearerToken("test-token-123");

		// Assert
		config.Type.ShouldBe(AuthenticationType.Bearer);
		config.Bearer.ShouldNotBeNull();
		config.Bearer.Token.ShouldBe("test-token-123");
		config.Bearer.Prefix.ShouldBe("Bearer");
	}
}
