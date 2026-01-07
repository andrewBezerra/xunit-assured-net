using Confluent.Kafka;
using XUnitAssured.Core.Configuration;
using XUnitAssured.Kafka;
using XUnitAssured.Kafka.Configuration;

namespace XUnitAssured.Tests.KafkaTests;

[Collection("TestSettings")]
[Trait("Category", "Kafka")]
[Trait("Component", "ExtensionMethods")]
/// <summary>
/// Tests for TestSettingsKafkaExtensions extension methods.
/// Validates that Kafka settings can be accessed from TestSettings via extension methods.
/// </summary>
public class TestSettingsKafkaExtensionsTests
{
	public TestSettingsKafkaExtensionsTests()
	{
		// Clear caches before each test
		TestSettingsLoader.ClearCache();
		TestSettingsKafkaExtensions.ClearKafkaSettingsCache();
	}

	[Fact(DisplayName = "GetKafkaSettings should return null when no config file exists")]
	public void GetKafkaSettings_Should_Return_Null_When_No_Config()
	{
		// Arrange
		var settings = new TestSettings();
		Environment.SetEnvironmentVariable("TESTSETTINGS_PATH", "nonexistent.json");

		try
		{
			// Act
			var kafkaSettings = settings.GetKafkaSettings();

			// Assert
			kafkaSettings.ShouldBeNull();
		}
		finally
		{
			Environment.SetEnvironmentVariable("TESTSETTINGS_PATH", null);
		}
	}

	[Fact(DisplayName = "GetKafkaSettings should load from testsettings.json")]
	public void GetKafkaSettings_Should_Load_From_File()
	{
		// Arrange
		var uniqueId = Guid.NewGuid().ToString("N");
		var tempFile = Path.Combine(Path.GetTempPath(), $"test-kafka-ext-{uniqueId}.json");
		File.WriteAllText(tempFile, @"{
			""testMode"": ""Local"",
			""kafka"": {
				""bootstrapServers"": ""localhost:9092"",
				""groupId"": ""test-group"",
				""securityProtocol"": ""Plaintext""
			}
		}");
		Environment.SetEnvironmentVariable("TESTSETTINGS_PATH", tempFile);
		TestSettingsLoader.ClearCache();
		TestSettingsKafkaExtensions.ClearKafkaSettingsCache();

		try
		{
			// Act
			var settings = TestSettings.Load();
			var kafkaSettings = settings.GetKafkaSettings();

			// Assert
			kafkaSettings.ShouldNotBeNull();
			kafkaSettings.BootstrapServers.ShouldBe("localhost:9092");
			kafkaSettings.GroupId.ShouldBe("test-group");
		}
		finally
		{
			Environment.SetEnvironmentVariable("TESTSETTINGS_PATH", null);
			if (File.Exists(tempFile))
				File.Delete(tempFile);
		}
	}

	[Fact(DisplayName = "TryGetKafkaSettings should return true when settings available")]
	public void TryGetKafkaSettings_Should_Return_True_When_Available()
	{
		// Arrange
		var uniqueId = Guid.NewGuid().ToString("N");
		var tempFile = Path.Combine(Path.GetTempPath(), $"test-kafka-try-{uniqueId}.json");
		File.WriteAllText(tempFile, @"{
			""kafka"": {
				""bootstrapServers"": ""kafka:9092"",
				""groupId"": ""consumer-group""
			}
		}");
		Environment.SetEnvironmentVariable("TESTSETTINGS_PATH", tempFile);
		TestSettingsLoader.ClearCache();
		TestSettingsKafkaExtensions.ClearKafkaSettingsCache();

		try
		{
			// Act
			var settings = TestSettings.Load();
			var result = settings.TryGetKafkaSettings(out var kafkaSettings);

			// Assert
			result.ShouldBeTrue();
			kafkaSettings.ShouldNotBeNull();
			kafkaSettings.BootstrapServers.ShouldBe("kafka:9092");
		}
		finally
		{
			Environment.SetEnvironmentVariable("TESTSETTINGS_PATH", null);
			if (File.Exists(tempFile))
				File.Delete(tempFile);
		}
	}

	[Fact(DisplayName = "TryGetKafkaSettings should return false when no kafka section")]
	public void TryGetKafkaSettings_Should_Return_False_When_Not_Available()
	{
		// Arrange
		var uniqueId = Guid.NewGuid().ToString("N");
		var tempFile = Path.Combine(Path.GetTempPath(), $"test-no-kafka-{uniqueId}.json");
		File.WriteAllText(tempFile, @"{
			""testMode"": ""Local""
		}");
		Environment.SetEnvironmentVariable("TESTSETTINGS_PATH", tempFile);
		TestSettingsLoader.ClearCache();
		TestSettingsKafkaExtensions.ClearKafkaSettingsCache();

		try
		{
			// Act
			var settings = TestSettings.Load();
			var result = settings.TryGetKafkaSettings(out var kafkaSettings);

			// Assert
			result.ShouldBeFalse();
			kafkaSettings.ShouldBeNull();
		}
		finally
		{
			Environment.SetEnvironmentVariable("TESTSETTINGS_PATH", null);
			if (File.Exists(tempFile))
				File.Delete(tempFile);
		}
	}

	[Fact(DisplayName = "GetKafkaSettings should cache results")]
	public void GetKafkaSettings_Should_Cache_Results()
	{
		// Arrange
		var uniqueId = Guid.NewGuid().ToString("N");
		var tempFile = Path.Combine(Path.GetTempPath(), $"test-kafka-cache-{uniqueId}.json");
		File.WriteAllText(tempFile, @"{
			""kafka"": {
				""bootstrapServers"": ""cached-kafka:9092""
			}
		}");
		Environment.SetEnvironmentVariable("TESTSETTINGS_PATH", tempFile);
		TestSettingsLoader.ClearCache();
		TestSettingsKafkaExtensions.ClearKafkaSettingsCache();

		try
		{
			// Act
			var settings = TestSettings.Load();
			var kafka1 = settings.GetKafkaSettings();
			var kafka2 = settings.GetKafkaSettings();

			// Assert
			kafka1.ShouldBe(kafka2); // Same instance (cached)
		}
		finally
		{
			Environment.SetEnvironmentVariable("TESTSETTINGS_PATH", null);
			if (File.Exists(tempFile))
				File.Delete(tempFile);
		}
	}

	[Fact(DisplayName = "GetKafkaSettings should support environment variables")]
	public void GetKafkaSettings_Should_Support_Environment_Variables()
	{
		// Arrange
		var uniqueId = Guid.NewGuid().ToString("N");
		var envVarName = $"TEST_KAFKA_SERVERS_{uniqueId}";
		Environment.SetEnvironmentVariable(envVarName, "prod-kafka:9092");
		
		var tempFile = Path.Combine(Path.GetTempPath(), $"test-kafka-envvar-{uniqueId}.json");
		var jsonContent = "{\n\t\"kafka\": {\n\t\t\"bootstrapServers\": \"${ENV:" + envVarName + "}\"\n\t}\n}";
		File.WriteAllText(tempFile, jsonContent);
		Environment.SetEnvironmentVariable("TESTSETTINGS_PATH", tempFile);
		TestSettingsLoader.ClearCache();
		TestSettingsKafkaExtensions.ClearKafkaSettingsCache();

		try
		{
			// Act
			var settings = TestSettings.Load();
			var kafkaSettings = settings.GetKafkaSettings();

			// Assert
			kafkaSettings.ShouldNotBeNull();
			kafkaSettings.BootstrapServers.ShouldBe("prod-kafka:9092");
		}
		finally
		{
			Environment.SetEnvironmentVariable("TESTSETTINGS_PATH", null);
			Environment.SetEnvironmentVariable(envVarName, null);
			if (File.Exists(tempFile))
				File.Delete(tempFile);
		}
	}

	[Fact(DisplayName = "GetKafkaSettings should work with ToProducerConfig")]
	public void GetKafkaSettings_Should_Work_With_ToProducerConfig()
	{
		// Arrange
		var uniqueId = Guid.NewGuid().ToString("N");
		var tempFile = Path.Combine(Path.GetTempPath(), $"test-kafka-producer-{uniqueId}.json");
		File.WriteAllText(tempFile, @"{
			""kafka"": {
				""bootstrapServers"": ""kafka:9092"",
				""securityProtocol"": ""Plaintext""
			}
		}");
		Environment.SetEnvironmentVariable("TESTSETTINGS_PATH", tempFile);
		TestSettingsLoader.ClearCache();
		TestSettingsKafkaExtensions.ClearKafkaSettingsCache();

		try
		{
			// Act
			var settings = TestSettings.Load();
			var kafkaSettings = settings.GetKafkaSettings();
			var producerConfig = kafkaSettings?.ToProducerConfig("test-producer");

			// Assert
			producerConfig.ShouldNotBeNull();
			producerConfig.BootstrapServers.ShouldBe("kafka:9092");
			producerConfig.ClientId.ShouldBe("test-producer");
		}
		finally
		{
			Environment.SetEnvironmentVariable("TESTSETTINGS_PATH", null);
			if (File.Exists(tempFile))
				File.Delete(tempFile);
		}
	}

	[Fact(DisplayName = "GetKafkaSettings should work with ToConsumerConfig")]
	public void GetKafkaSettings_Should_Work_With_ToConsumerConfig()
	{
		// Arrange
		var uniqueId = Guid.NewGuid().ToString("N");
		var tempFile = Path.Combine(Path.GetTempPath(), $"test-kafka-consumer-{uniqueId}.json");
		File.WriteAllText(tempFile, @"{
			""kafka"": {
				""bootstrapServers"": ""kafka:9092"",
				""groupId"": ""test-group""
			}
		}");
		Environment.SetEnvironmentVariable("TESTSETTINGS_PATH", tempFile);
		TestSettingsLoader.ClearCache();
		TestSettingsKafkaExtensions.ClearKafkaSettingsCache();

		try
		{
			// Act
			var settings = TestSettings.Load();
			var kafkaSettings = settings.GetKafkaSettings();
			var consumerConfig = kafkaSettings?.ToConsumerConfig();

			// Assert
			consumerConfig.ShouldNotBeNull();
			consumerConfig.BootstrapServers.ShouldBe("kafka:9092");
			consumerConfig.GroupId.ShouldBe("test-group");
			consumerConfig.AutoOffsetReset.ShouldBe(AutoOffsetReset.Earliest);
		}
		finally
		{
			Environment.SetEnvironmentVariable("TESTSETTINGS_PATH", null);
			if (File.Exists(tempFile))
				File.Delete(tempFile);
		}
	}
}
