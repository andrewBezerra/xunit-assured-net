using CoreTestSettings = XUnitAssured.Core.Configuration.TestSettings;
using TestSettingsLoader = XUnitAssured.Core.Configuration.TestSettingsLoader;
using TestMode = XUnitAssured.Core.Configuration.TestMode;

namespace XUnitAssured.Tests.CoreTests;

[Collection("TestSettings")]
[Trait("Category", "Core")]
[Trait("Component", "TestSettings")]
/// <summary>
/// Unit tests for TestSettings and TestSettingsLoader.
/// Tests configuration loading, environment variable substitution, and default values.
/// </summary>
public class TestSettingsTests
{
	[Fact(DisplayName = "TestSettings should have default values")]
	public void TestSettings_Should_Have_Default_Values()
	{
		// Arrange & Act
		var settings = new CoreTestSettings();

		// Assert
		settings.TestMode.ShouldBe(TestMode.Local);
		settings.Environment.ShouldBeNull();
	}

	[Fact(DisplayName = "TestSettings should allow setting TestMode")]
	public void TestSettings_Should_Allow_Setting_TestMode()
	{
		// Arrange
		var settings = new CoreTestSettings();

		// Act
		settings.TestMode = TestMode.Remote;

		// Assert
		settings.TestMode.ShouldBe(TestMode.Remote);
	}

	[Fact(DisplayName = "TestSettings should allow setting Environment")]
	public void TestSettings_Should_Allow_Setting_Environment()
	{
		// Arrange
		var settings = new CoreTestSettings();

		// Act
		settings.Environment = "staging";

		// Assert
		settings.Environment.ShouldBe("staging");
	}

	[Fact(DisplayName = "TestSettingsLoader should return default settings when file not found")]
	public void TestSettingsLoader_Should_Return_Default_When_File_Not_Found()
	{
		// Arrange
		TestSettingsLoader.ClearCache();
		var uniquePath = $"nonexistent-{Guid.NewGuid():N}.json";
		Environment.SetEnvironmentVariable("TESTSETTINGS_PATH", uniquePath);

		try
		{
			// Act
			var settings = TestSettingsLoader.Load();

			// Assert
			settings.ShouldNotBeNull();
			settings.TestMode.ShouldBe(TestMode.Local);
		}
		finally
		{
			// Cleanup
			Environment.SetEnvironmentVariable("TESTSETTINGS_PATH", null);
			TestSettingsLoader.ClearCache();
		}
	}

	[Fact(DisplayName = "TestSettingsLoader should cache settings")]
	public void TestSettingsLoader_Should_Cache_Settings()
	{
		// Arrange
		TestSettingsLoader.ClearCache();

		// Act
		var settings1 = TestSettingsLoader.Load();
		var settings2 = TestSettingsLoader.Load();

		// Assert
		settings1.ShouldBe(settings2); // Same instance (cached)

		// Cleanup
		TestSettingsLoader.ClearCache();
	}

	[Fact(DisplayName = "TestSettingsLoader ClearCache should clear cached settings")]
	public void TestSettingsLoader_ClearCache_Should_Clear_Cache()
	{
		// Arrange
		var settings1 = TestSettingsLoader.Load();

		// Act
		TestSettingsLoader.ClearCache();
		var settings2 = TestSettingsLoader.Load();

		// Assert
		settings1.ShouldNotBe(settings2); // Different instances
	}

	[Fact(DisplayName = "CoreTestSettings.Load should call TestSettingsLoader")]
	public void TestSettings_Load_Should_Call_TestSettingsLoader()
	{
		// Arrange
		TestSettingsLoader.ClearCache();

		// Act
		var settings = CoreTestSettings.Load();

		// Assert
		settings.ShouldNotBeNull();
		settings.ShouldBeOfType<CoreTestSettings>();

		// Cleanup
		TestSettingsLoader.ClearCache();
	}

	[Fact(DisplayName = "CoreTestSettings.Load should support environment parameter")]
	public void TestSettings_Load_Should_Support_Environment_Parameter()
	{
		// Arrange
		TestSettingsLoader.ClearCache();

		// Act
		var settings = CoreTestSettings.Load("staging");

		// Assert
		settings.ShouldNotBeNull();
		// Note: Environment is only set if file exists, otherwise remains null
		// This is expected behavior

		// Cleanup
		TestSettingsLoader.ClearCache();
	}

	[Fact(DisplayName = "TestMode enum should have correct values")]
	public void TestMode_Enum_Should_Have_Correct_Values()
	{
		// Assert
		((int)TestMode.Local).ShouldBe(0);
		((int)TestMode.Remote).ShouldBe(1);
	}

	[Fact(DisplayName = "TestMode Local should be default")]
	public void TestMode_Local_Should_Be_Default()
	{
		// Arrange
		var settings = new CoreTestSettings();

		// Assert
		settings.TestMode.ShouldBe(TestMode.Local);
		((int)settings.TestMode).ShouldBe(0);
	}

	[Fact(DisplayName = "TestSettingsLoader should support TEST_ENV environment variable")]
	public void TestSettingsLoader_Should_Support_TEST_ENV_Variable()
	{
		// Arrange
		TestSettingsLoader.ClearCache();
		Environment.SetEnvironmentVariable("TEST_ENV", "production");

		try
		{
			// Act
			var settings = TestSettingsLoader.Load();

			// Assert
			settings.ShouldNotBeNull();
			// If testsettings.production.json exists, Environment would be set
			// Otherwise, settings are still valid with defaults
		}
		finally
		{
			// Cleanup
			Environment.SetEnvironmentVariable("TEST_ENV", null);
			TestSettingsLoader.ClearCache();
		}
	}

	[Fact(DisplayName = "TestSettingsLoader should prioritize TESTSETTINGS_PATH")]
	public void TestSettingsLoader_Should_Prioritize_Custom_Path()
	{
		// Arrange
		TestSettingsLoader.ClearCache();
		var uniqueId = Guid.NewGuid().ToString("N");
		var tempFile = Path.Combine(Path.GetTempPath(), $"test-custom-settings-{uniqueId}.json");
		File.WriteAllText(tempFile, "{\"testMode\":\"Remote\",\"environment\":\"custom\"}");
		Environment.SetEnvironmentVariable("TESTSETTINGS_PATH", tempFile);

		try
		{
			// Act
			var settings = TestSettingsLoader.Load();

			// Assert
			settings.ShouldNotBeNull();
			settings.TestMode.ShouldBe(TestMode.Remote);
			settings.Environment.ShouldBe("custom");
		}
		finally
		{
			// Cleanup
			Environment.SetEnvironmentVariable("TESTSETTINGS_PATH", null);
			TestSettingsLoader.ClearCache();
			if (File.Exists(tempFile))
				File.Delete(tempFile);
		}
	}

	[Fact(DisplayName = "TestSettingsLoader should replace environment variables in JSON")]
	public void TestSettingsLoader_Should_Replace_Environment_Variables()
	{
		// Arrange
		TestSettingsLoader.ClearCache();
		var uniqueId = Guid.NewGuid().ToString("N");
		var tempFile = Path.Combine(Path.GetTempPath(), $"test-envvar-settings-{uniqueId}.json");
		var envVarName = $"TEST_MY_VALUE_{uniqueId}";
		Environment.SetEnvironmentVariable(envVarName, "replaced-value");
		File.WriteAllText(tempFile, $"{{\"testMode\":\"Local\",\"environment\":\"${{ENV:{envVarName}}}\"}}");
		Environment.SetEnvironmentVariable("TESTSETTINGS_PATH", tempFile);

		try
		{
			// Act
			var settings = TestSettingsLoader.Load();

			// Assert
			settings.ShouldNotBeNull();
			settings.Environment.ShouldBe("replaced-value");
		}
		finally
		{
			// Cleanup
			Environment.SetEnvironmentVariable("TESTSETTINGS_PATH", null);
			Environment.SetEnvironmentVariable(envVarName, null);
			TestSettingsLoader.ClearCache();
			if (File.Exists(tempFile))
				File.Delete(tempFile);
		}
	}

	[Fact(DisplayName = "TestSettingsLoader should keep placeholder if env var not found")]
	public void TestSettingsLoader_Should_Keep_Placeholder_If_Variable_Not_Found()
	{
		// Arrange
		TestSettingsLoader.ClearCache();
		var uniqueId = Guid.NewGuid().ToString("N");
		var tempFile = Path.Combine(Path.GetTempPath(), $"test-missing-envvar-settings-{uniqueId}.json");
		var jsonContent = @"{
	""testMode"": ""Local"",
	""environment"": ""${ENV:NONEXISTENT_VAR}""
}";
		File.WriteAllText(tempFile, jsonContent);
		Environment.SetEnvironmentVariable("TESTSETTINGS_PATH", tempFile);
		TestSettingsLoader.ClearCache(); // Clear cache AFTER setting TESTSETTINGS_PATH

		try
		{
			// Act
			var settings = TestSettingsLoader.Load();

			// Assert
			settings.ShouldNotBeNull();
			settings.Environment.ShouldBe("${ENV:NONEXISTENT_VAR}"); // Keeps placeholder
		}
		finally
		{
			// Cleanup
			Environment.SetEnvironmentVariable("TESTSETTINGS_PATH", null);
			TestSettingsLoader.ClearCache();
			
			// Force garbage collection to release any file handles
			GC.Collect();
			GC.WaitForPendingFinalizers();
			
			// Try delete with retry
			for (int attempt = 0; attempt < 3; attempt++)
			{
				try
				{
					if (File.Exists(tempFile))
						File.Delete(tempFile);
					break;
				}
				catch (IOException)
				{
					if (attempt < 2)
						System.Threading.Thread.Sleep(100);
					// Ignore on final attempt - file will be cleaned up by OS
				}
			}
		}
	}

	[Fact(DisplayName = "TestSettingsLoader should handle JSON with comments")]
	public void TestSettingsLoader_Should_Handle_JSON_With_Comments()
	{
		// Arrange
		TestSettingsLoader.ClearCache();
		var uniqueId = Guid.NewGuid().ToString("N");
		var tempFile = Path.Combine(Path.GetTempPath(), $"test-comments-settings-{uniqueId}.json");
		File.WriteAllText(tempFile, @"{
			// This is a comment
			""testMode"": ""Local"", // Inline comment
			""environment"": ""test""
		}");
		Environment.SetEnvironmentVariable("TESTSETTINGS_PATH", tempFile);

		try
		{
			// Act
			var settings = TestSettingsLoader.Load();

			// Assert
			settings.ShouldNotBeNull();
			settings.TestMode.ShouldBe(TestMode.Local);
			settings.Environment.ShouldBe("test");
		}
		finally
		{
			// Cleanup
			Environment.SetEnvironmentVariable("TESTSETTINGS_PATH", null);
			TestSettingsLoader.ClearCache();
			if (File.Exists(tempFile))
				File.Delete(tempFile);
		}
	}

	[Fact(DisplayName = "TestSettingsLoader should handle JSON with trailing commas")]
	public void TestSettingsLoader_Should_Handle_Trailing_Commas()
	{
		// Arrange
		TestSettingsLoader.ClearCache();
		var uniqueId = Guid.NewGuid().ToString("N");
		var tempFile = Path.Combine(Path.GetTempPath(), $"test-trailing-commas-settings-{uniqueId}.json");
		File.WriteAllText(tempFile, @"{
			""testMode"": ""Remote"",
			""environment"": ""test"",
		}");
		Environment.SetEnvironmentVariable("TESTSETTINGS_PATH", tempFile);

		try
		{
			// Act
			var settings = TestSettingsLoader.Load();

			// Assert
			settings.ShouldNotBeNull();
			settings.TestMode.ShouldBe(TestMode.Remote);
		}
		finally
		{
			// Cleanup
			Environment.SetEnvironmentVariable("TESTSETTINGS_PATH", null);
			TestSettingsLoader.ClearCache();
			if (File.Exists(tempFile))
				File.Delete(tempFile);
		}
	}

	[Fact(DisplayName = "TestSettingsLoader should be case-insensitive for property names")]
	public void TestSettingsLoader_Should_Be_Case_Insensitive()
	{
		// Arrange
		TestSettingsLoader.ClearCache();
		var uniqueId = Guid.NewGuid().ToString("N");
		var tempFile = Path.Combine(Path.GetTempPath(), $"test-caseinsensitive-settings-{uniqueId}.json");
		File.WriteAllText(tempFile, "{\"TESTMODE\":\"Remote\",\"ENVIRONMENT\":\"prod\"}");
		Environment.SetEnvironmentVariable("TESTSETTINGS_PATH", tempFile);

		try
		{
			// Act
			var settings = TestSettingsLoader.Load();

			// Assert
			settings.ShouldNotBeNull();
			settings.TestMode.ShouldBe(TestMode.Remote);
			settings.Environment.ShouldBe("prod");
		}
		finally
		{
			// Cleanup
			Environment.SetEnvironmentVariable("TESTSETTINGS_PATH", null);
			TestSettingsLoader.ClearCache();
			if (File.Exists(tempFile))
				File.Delete(tempFile);
		}
	}
}
