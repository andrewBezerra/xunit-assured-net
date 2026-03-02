using XUnitAssured.Playwright.Configuration;

namespace XUnitAssured.Tests.PlaywrightTests;

[Trait("Category", "Playwright")]
[Trait("Component", "Configuration")]
/// <summary>
/// Tests for PlaywrightSettings, PlaywrightSettingsLoader, and BrowserType.
/// </summary>
public class PlaywrightSettingsTests
{
	[Fact(DisplayName = "Should create PlaywrightSettings with default values")]
	public void Should_Create_Default_Settings()
	{
		// Act
		var settings = new PlaywrightSettings();

		// Assert
		settings.ShouldNotBeNull();
		settings.BaseUrl.ShouldBeNull();
		settings.Browser.ShouldBe(BrowserType.Chromium);
		settings.Headless.ShouldBeTrue();
		settings.SlowMo.ShouldBe(0);
		settings.DefaultTimeout.ShouldBe(30000);
		settings.NavigationTimeout.ShouldBe(30000);
		settings.ViewportWidth.ShouldBe(1280);
		settings.ViewportHeight.ShouldBe(720);
		settings.ScreenshotPath.ShouldBe("screenshots");
		settings.RecordTrace.ShouldBeFalse();
		settings.TracePath.ShouldBe("traces");
		settings.IgnoreHttpsErrors.ShouldBeFalse();
		settings.ExtraHttpHeaders.ShouldBeNull();
		settings.TestIdAttribute.ShouldBe("data-testid");
	}

	[Fact(DisplayName = "Should merge PlaywrightSettings with overrides taking precedence")]
	public void Should_Merge_Settings()
	{
		// Arrange
		var baseSettings = new PlaywrightSettings
		{
			BaseUrl = "https://base.example.com",
			Browser = BrowserType.Chromium,
			Headless = true,
			DefaultTimeout = 30000,
			ViewportWidth = 1280,
			ViewportHeight = 720,
			ExtraHttpHeaders = new Dictionary<string, string>
			{
				["X-Base-Header"] = "base-value"
			}
		};

		var overrideSettings = new PlaywrightSettings
		{
			Browser = BrowserType.Firefox,
			DefaultTimeout = 60000,
			SlowMo = 100,
			ViewportWidth = 1920,
			ViewportHeight = 1080
		};

		// Act
		var merged = baseSettings.Merge(overrideSettings);

		// Assert
		merged.BaseUrl.ShouldBe("https://base.example.com"); // From base (override is null)
		merged.Browser.ShouldBe(BrowserType.Firefox); // Overridden
		merged.DefaultTimeout.ShouldBe(60000); // Overridden
		merged.SlowMo.ShouldBe(100); // Overridden
		merged.ViewportWidth.ShouldBe(1920); // Overridden
		merged.ViewportHeight.ShouldBe(1080); // Overridden
		merged.ExtraHttpHeaders.ShouldNotBeNull(); // From base
		merged.ExtraHttpHeaders!["X-Base-Header"].ShouldBe("base-value");
	}

	[Fact(DisplayName = "Should preserve base URL when override URL is null")]
	public void Should_Merge_BaseUrl_From_Base_When_Override_Is_Null()
	{
		// Arrange
		var baseSettings = new PlaywrightSettings { BaseUrl = "https://myapp.com" };
		var overrideSettings = new PlaywrightSettings { BaseUrl = null };

		// Act
		var merged = baseSettings.Merge(overrideSettings);

		// Assert
		merged.BaseUrl.ShouldBe("https://myapp.com");
	}

	[Fact(DisplayName = "Should use override URL when provided")]
	public void Should_Merge_BaseUrl_From_Override_When_Provided()
	{
		// Arrange
		var baseSettings = new PlaywrightSettings { BaseUrl = "https://base.com" };
		var overrideSettings = new PlaywrightSettings { BaseUrl = "https://override.com" };

		// Act
		var merged = baseSettings.Merge(overrideSettings);

		// Assert
		merged.BaseUrl.ShouldBe("https://override.com");
	}

	[Fact(DisplayName = "Should return same settings when merging with null")]
	public void Should_Return_Same_Settings_When_Merging_With_Null()
	{
		// Arrange
		var settings = new PlaywrightSettings
		{
			BaseUrl = "https://myapp.com",
			Browser = BrowserType.WebKit
		};

		// Act
		var merged = settings.Merge(null!);

		// Assert
		merged.ShouldBe(settings);
	}

	[Fact(DisplayName = "Should override ExtraHttpHeaders from override settings")]
	public void Should_Merge_ExtraHttpHeaders_From_Override()
	{
		// Arrange
		var baseSettings = new PlaywrightSettings
		{
			ExtraHttpHeaders = new Dictionary<string, string> { ["X-Base"] = "base" }
		};
		var overrideSettings = new PlaywrightSettings
		{
			ExtraHttpHeaders = new Dictionary<string, string> { ["X-Override"] = "override" }
		};

		// Act
		var merged = baseSettings.Merge(overrideSettings);

		// Assert
		merged.ExtraHttpHeaders.ShouldNotBeNull();
		merged.ExtraHttpHeaders!.ContainsKey("X-Override").ShouldBeTrue();
		merged.ExtraHttpHeaders.ContainsKey("X-Base").ShouldBeFalse();
	}

	[Fact(DisplayName = "Should load PlaywrightSettings returning defaults when no file exists")]
	public void Should_Load_Settings_Returns_Defaults()
	{
		// Arrange
		PlaywrightSettingsLoader.ClearCache();

		// Act
		var settings = PlaywrightSettings.Load();

		// Assert
		settings.ShouldNotBeNull();
		settings.Browser.ShouldBe(BrowserType.Chromium);
		settings.Headless.ShouldBeTrue();
		settings.DefaultTimeout.ShouldBe(30000);
	}

	[Fact(DisplayName = "Should clear PlaywrightSettings cache successfully")]
	public void Should_Clear_Settings_Cache()
	{
		// Arrange
		PlaywrightSettingsLoader.Load();

		// Act
		PlaywrightSettingsLoader.ClearCache();

		// Assert - no exception thrown, fresh load works
		var settings = PlaywrightSettingsLoader.Load();
		settings.ShouldNotBeNull();
	}

	[Fact(DisplayName = "Should have correct BrowserType enum values")]
	public void Should_Have_Correct_BrowserType_Values()
	{
		// Assert
		((int)BrowserType.Chromium).ShouldBe(0);
		((int)BrowserType.Firefox).ShouldBe(1);
		((int)BrowserType.WebKit).ShouldBe(2);
	}

	[Fact(DisplayName = "Should merge TestIdAttribute when override has custom value")]
	public void Should_Merge_TestIdAttribute()
	{
		// Arrange
		var baseSettings = new PlaywrightSettings { TestIdAttribute = "data-testid" };
		var overrideSettings = new PlaywrightSettings { TestIdAttribute = "data-cy" };

		// Act
		var merged = baseSettings.Merge(overrideSettings);

		// Assert
		merged.TestIdAttribute.ShouldBe("data-cy");
	}
}
