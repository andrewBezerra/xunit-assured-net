using XUnitAssured.Core.Configuration;
using XUnitAssured.Http.Configuration;

namespace XUnitAssured.Tests.HttpTests;

[Collection("TestSettings")]
[Trait("Category", "Http")]
[Trait("Component", "ExtensionMethods")]
/// <summary>
/// Tests for TestSettingsHttpExtensions extension methods.
/// Validates that HTTP settings can be accessed from TestSettings via extension methods.
/// </summary>
public class TestSettingsHttpExtensionsTests
{
	public TestSettingsHttpExtensionsTests()
	{
		// Clear caches before each test
		TestSettingsLoader.ClearCache();
		TestSettingsHttpExtensions.ClearHttpSettingsCache();
	}

	[Fact(DisplayName = "GetHttpSettings should return null when no config file exists")]
	public void GetHttpSettings_Should_Return_Null_When_No_Config()
	{
		// Arrange
		var settings = new TestSettings();
		Environment.SetEnvironmentVariable("TESTSETTINGS_PATH", "nonexistent.json");

		try
		{
			// Act
			var httpSettings = settings.GetHttpSettings();

			// Assert
			httpSettings.ShouldBeNull();
		}
		finally
		{
			Environment.SetEnvironmentVariable("TESTSETTINGS_PATH", null);
		}
	}

	[Fact(DisplayName = "GetHttpSettings should load from testsettings.json")]
	public void GetHttpSettings_Should_Load_From_File()
	{
		// Arrange
		var uniqueId = Guid.NewGuid().ToString("N");
		var tempFile = Path.Combine(Path.GetTempPath(), $"test-http-ext-{uniqueId}.json");
		File.WriteAllText(tempFile, @"{
			""testMode"": ""Local"",
			""http"": {
				""baseUrl"": ""https://test-api.com"",
				""timeout"": 60
			}
		}");
		Environment.SetEnvironmentVariable("TESTSETTINGS_PATH", tempFile);
		TestSettingsLoader.ClearCache();
		TestSettingsHttpExtensions.ClearHttpSettingsCache();

		try
		{
			// Act
			var settings = TestSettings.Load();
			var httpSettings = settings.GetHttpSettings();

			// Assert
			httpSettings.ShouldNotBeNull();
			httpSettings.BaseUrl.ShouldBe("https://test-api.com");
			httpSettings.Timeout.ShouldBe(60);
		}
		finally
		{
			Environment.SetEnvironmentVariable("TESTSETTINGS_PATH", null);
			if (File.Exists(tempFile))
				File.Delete(tempFile);
		}
	}

	[Fact(DisplayName = "TryGetHttpSettings should return true when settings available")]
	public void TryGetHttpSettings_Should_Return_True_When_Available()
	{
		// Arrange
		var uniqueId = Guid.NewGuid().ToString("N");
		var tempFile = Path.Combine(Path.GetTempPath(), $"test-http-try-{uniqueId}.json");
		File.WriteAllText(tempFile, @"{
			""http"": {
				""baseUrl"": ""https://api.com"",
				""timeout"": 30
			}
		}");
		Environment.SetEnvironmentVariable("TESTSETTINGS_PATH", tempFile);
		TestSettingsLoader.ClearCache();
		TestSettingsHttpExtensions.ClearHttpSettingsCache();

		try
		{
			// Act
			var settings = TestSettings.Load();
			var result = settings.TryGetHttpSettings(out var httpSettings);

			// Assert
			result.ShouldBeTrue();
			httpSettings.ShouldNotBeNull();
			httpSettings.BaseUrl.ShouldBe("https://api.com");
		}
		finally
		{
			Environment.SetEnvironmentVariable("TESTSETTINGS_PATH", null);
			if (File.Exists(tempFile))
				File.Delete(tempFile);
		}
	}

	[Fact(DisplayName = "TryGetHttpSettings should return false when no http section")]
	public void TryGetHttpSettings_Should_Return_False_When_Not_Available()
	{
		// Arrange
		var uniqueId = Guid.NewGuid().ToString("N");
		var tempFile = Path.Combine(Path.GetTempPath(), $"test-no-http-{uniqueId}.json");
		File.WriteAllText(tempFile, @"{
			""testMode"": ""Local""
		}");
		Environment.SetEnvironmentVariable("TESTSETTINGS_PATH", tempFile);
		TestSettingsLoader.ClearCache();
		TestSettingsHttpExtensions.ClearHttpSettingsCache();

		try
		{
			// Act
			var settings = TestSettings.Load();
			var result = settings.TryGetHttpSettings(out var httpSettings);

			// Assert
			result.ShouldBeFalse();
			httpSettings.ShouldBeNull();
		}
		finally
		{
			Environment.SetEnvironmentVariable("TESTSETTINGS_PATH", null);
			if (File.Exists(tempFile))
				File.Delete(tempFile);
		}
	}

	[Fact(DisplayName = "GetHttpSettings should cache results")]
	public void GetHttpSettings_Should_Cache_Results()
	{
		// Arrange
		var uniqueId = Guid.NewGuid().ToString("N");
		var tempFile = Path.Combine(Path.GetTempPath(), $"test-http-cache-{uniqueId}.json");
		File.WriteAllText(tempFile, @"{
			""http"": {
				""baseUrl"": ""https://cached-api.com""
			}
		}");
		Environment.SetEnvironmentVariable("TESTSETTINGS_PATH", tempFile);
		TestSettingsLoader.ClearCache();
		TestSettingsHttpExtensions.ClearHttpSettingsCache();

		try
		{
			// Act
			var settings = TestSettings.Load();
			var http1 = settings.GetHttpSettings();
			var http2 = settings.GetHttpSettings();

			// Assert
			http1.ShouldBe(http2); // Same instance (cached)
		}
		finally
		{
			Environment.SetEnvironmentVariable("TESTSETTINGS_PATH", null);
			if (File.Exists(tempFile))
				File.Delete(tempFile);
		}
	}

	[Fact(DisplayName = "GetHttpSettings should support environment variables")]
	public void GetHttpSettings_Should_Support_Environment_Variables()
	{
		// Arrange
		var uniqueId = Guid.NewGuid().ToString("N");
		var envVarName = $"TEST_HTTP_URL_{uniqueId}";
		Environment.SetEnvironmentVariable(envVarName, "https://env-api.com");
		
		var tempFile = Path.Combine(Path.GetTempPath(), $"test-http-envvar-{uniqueId}.json");
		var jsonContent = "{\n\t\"http\": {\n\t\t\"baseUrl\": \"${ENV:" + envVarName + "}\"\n\t}\n}";
		File.WriteAllText(tempFile, jsonContent);
		Environment.SetEnvironmentVariable("TESTSETTINGS_PATH", tempFile);
		TestSettingsLoader.ClearCache();
		TestSettingsHttpExtensions.ClearHttpSettingsCache();

		try
		{
			// Act
			var settings = TestSettings.Load();
			var httpSettings = settings.GetHttpSettings();

			// Assert
			httpSettings.ShouldNotBeNull();
			httpSettings.BaseUrl.ShouldBe("https://env-api.com");
		}
		finally
		{
			Environment.SetEnvironmentVariable("TESTSETTINGS_PATH", null);
			Environment.SetEnvironmentVariable(envVarName, null);
			if (File.Exists(tempFile))
				File.Delete(tempFile);
		}
	}
}
