using System;
using System.IO;

using Confluent.Kafka;

using XUnitAssured.Core.Configuration;
using XUnitAssured.Kafka;
using XUnitAssured.Kafka.Configuration;
using XUnitAssured.Tests.CoreTests;

namespace XUnitAssured.Tests.KafkaTests;

[Collection("TestSettings")]
[Trait("Category", "Kafka")]
[Trait("Component", "Configuration")]
/// <summary>
/// <see cref="KafkaSettings.Load"/> used to be a stub that returned defaults whatever
/// the configuration said, so every step run without a fixture ignored
/// <c>testsettings.json</c> and the <c>...FromSettings</c> DSL methods always threw.
/// These tests point <c>TESTSETTINGS_PATH</c> at a temporary file and assert that
/// what the file says is what the step sees. They share global state (an environment
/// variable and two caches), hence the sequential collection.
/// </summary>
public sealed class KafkaSettingsLoadTests : IDisposable
{
	private readonly string? _previousPath = Environment.GetEnvironmentVariable("TESTSETTINGS_PATH");
	private readonly string _tempDirectory = Path.Combine(Path.GetTempPath(), $"xa-kafka-settings-{Guid.NewGuid():N}");

	public KafkaSettingsLoadTests()
	{
		Directory.CreateDirectory(_tempDirectory);
	}

	private void UseSettingsFile(string json)
	{
		var path = Path.Combine(_tempDirectory, "testsettings.json");
		File.WriteAllText(path, json);
		Environment.SetEnvironmentVariable("TESTSETTINGS_PATH", path);
		ClearCaches();
	}

	private static void ClearCaches()
	{
		TestSettingsLoader.ClearCache();
		TestSettingsKafkaExtensions.ClearKafkaSettingsCache();
	}

	[Fact(DisplayName = "Load should read the kafka section of testsettings.json")]
	public void Load_Should_Read_Kafka_Section()
	{
		UseSettingsFile("""
			{
			  "testMode": "Local",
			  "kafka": {
			    "bootstrapServers": "broker.test:9095",
			    "groupId": "settings-group",
			    "securityProtocol": "SaslPlaintext",
			    "authentication": {
			      "type": "SaslPlain",
			      "saslPlain": { "username": "alice", "password": "secret", "useSsl": false }
			    }
			  }
			}
			""");

		var settings = KafkaSettings.Load();

		settings.BootstrapServers.ShouldBe("broker.test:9095");
		settings.GroupId.ShouldBe("settings-group");
		settings.SecurityProtocol.ShouldBe(SecurityProtocol.SaslPlaintext);
		settings.Authentication.ShouldNotBeNull();
		settings.Authentication!.Type.ShouldBe(KafkaAuthenticationType.SaslPlain);
		settings.Authentication.SaslPlain.ShouldNotBeNull();
		settings.Authentication.SaslPlain!.Username.ShouldBe("alice");
	}

	[Fact(DisplayName = "Load should substitute ${ENV:...} placeholders")]
	public void Load_Should_Substitute_Environment_Placeholders()
	{
		Environment.SetEnvironmentVariable("XA_TEST_BROKER", "from-env:9096");
		try
		{
			UseSettingsFile("""
				{ "kafka": { "bootstrapServers": "${ENV:XA_TEST_BROKER}" } }
				""");

			KafkaSettings.Load().BootstrapServers.ShouldBe("from-env:9096");
		}
		finally
		{
			Environment.SetEnvironmentVariable("XA_TEST_BROKER", null);
		}
	}

	[Fact(DisplayName = "Load should return defaults when the file has no kafka section")]
	public void Load_Should_Return_Defaults_Without_Kafka_Section()
	{
		UseSettingsFile("""
			{ "testMode": "Local", "http": { "baseUrl": "https://api.test" } }
			""");

		var settings = KafkaSettings.Load();

		settings.ShouldNotBeNull("a missing section must not become a null reference for callers");
		settings.BootstrapServers.ShouldBe("localhost:9092");
		settings.Authentication.ShouldBeNull();
	}

	[Fact(DisplayName = "Load should return defaults when no settings file exists")]
	public void Load_Should_Return_Defaults_Without_File()
	{
		Environment.SetEnvironmentVariable("TESTSETTINGS_PATH", Path.Combine(_tempDirectory, "does-not-exist.json"));
		ClearCaches();

		var settings = KafkaSettings.Load();

		settings.ShouldNotBeNull();
		settings.BootstrapServers.ShouldBe("localhost:9092");
	}

	public void Dispose()
	{
		Environment.SetEnvironmentVariable("TESTSETTINGS_PATH", _previousPath);
		ClearCaches();
		try { Directory.Delete(_tempDirectory, recursive: true); } catch (IOException) { }
	}
}
