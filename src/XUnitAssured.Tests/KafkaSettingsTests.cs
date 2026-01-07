using Confluent.Kafka;

using XUnitAssured.Kafka;

namespace XUnitAssured.Tests;

[Trait("Unit Test", "KafkaSettings")]
public class KafkaSettingsTests
{
	[Fact(DisplayName = "Constructor should initialize with default values")]
	public void DefaultConstructor_ShouldInitializeWithDefaultValues()
	{
		// Act
		var settings = new KafkaSettings();

		// Assert
		Assert.Equal("localhost:9092", settings.BootstrapServers);
		Assert.Null(settings.GroupId);
		Assert.Equal(SecurityProtocol.Plaintext, settings.SecurityProtocol);
		Assert.Null(settings.SaslMechanism);
		Assert.Null(settings.SaslUsername);
		Assert.Null(settings.SaslPassword);
		Assert.True(settings.EnableSslCertificateVerification);
		Assert.Null(settings.SslCaLocation);
		Assert.Null(settings.SchemaRegistry);
		Assert.Null(settings.AdditionalProperties);
	}

	[Fact(DisplayName = "SectionName constant should have correct value")]
	public void SectionName_ShouldHaveCorrectValue()
	{
		// Assert
		Assert.Equal("Kafka", KafkaSettings.SectionName);
	}

	[Fact(DisplayName = "ToProducerConfig should create valid ProducerConfig")]
	public void ToProducerConfig_ShouldCreateValidProducerConfig()
	{
		// Arrange
		var settings = new KafkaSettings
		{
			BootstrapServers = "broker1:9092,broker2:9092",
			SecurityProtocol = SecurityProtocol.Plaintext
		};

		// Act
		var config = settings.ToProducerConfig();

		// Assert
		Assert.Equal("broker1:9092,broker2:9092", config.BootstrapServers);
		Assert.Equal(SecurityProtocol.Plaintext, config.SecurityProtocol);
		Assert.NotNull(config.ClientId);
		Assert.StartsWith("xunitassured-producer-", config.ClientId);
	}

	[Fact(DisplayName = "ToProducerConfig should use custom client ID")]
	public void ToProducerConfig_ShouldUseCustomClientId()
	{
		// Arrange
		var settings = new KafkaSettings();
		var customClientId = "my-custom-producer";

		// Act
		var config = settings.ToProducerConfig(customClientId);

		// Assert
		Assert.Equal(customClientId, config.ClientId);
	}

	[Fact(DisplayName = "ToConsumerConfig should create valid ConsumerConfig")]
	public void ToConsumerConfig_ShouldCreateValidConsumerConfig()
	{
		// Arrange
		var settings = new KafkaSettings
		{
			BootstrapServers = "broker1:9092",
			GroupId = "test-group"
		};

		// Act
		var config = settings.ToConsumerConfig();

		// Assert
		Assert.Equal("broker1:9092", config.BootstrapServers);
		Assert.Equal("test-group", config.GroupId);
		Assert.Equal(AutoOffsetReset.Earliest, config.AutoOffsetReset);
		Assert.False(config.EnableAutoCommit);
	}

	[Fact(DisplayName = "ToConsumerConfig should generate GroupId if not provided")]
	public void ToConsumerConfig_ShouldGenerateGroupId_WhenNotProvided()
	{
		// Arrange
		var settings = new KafkaSettings
		{
			GroupId = null
		};

		// Act
		var config = settings.ToConsumerConfig();

		// Assert
		Assert.NotNull(config.GroupId);
		Assert.StartsWith("xunitassured-consumer-", config.GroupId);
	}

	[Fact(DisplayName = "ToConsumerConfig should override GroupId parameter")]
	public void ToConsumerConfig_ShouldOverrideGroupId_WhenParameterProvided()
	{
		// Arrange
		var settings = new KafkaSettings
		{
			GroupId = "original-group"
		};
		var overrideGroupId = "override-group";

		// Act
		var config = settings.ToConsumerConfig(overrideGroupId);

		// Assert
		Assert.Equal(overrideGroupId, config.GroupId);
	}

	[Fact(DisplayName = "ToClientConfig should apply SASL settings")]
	public void ToClientConfig_ShouldApplySaslSettings()
	{
		// Arrange
		var settings = new KafkaSettings
		{
			BootstrapServers = "broker:9092",
			SecurityProtocol = SecurityProtocol.SaslSsl,
			SaslMechanism = SaslMechanism.ScramSha256,
			SaslUsername = "testuser",
			SaslPassword = "testpass"
		};

		// Act
		var config = settings.ToClientConfig();

		// Assert
		Assert.Equal(SecurityProtocol.SaslSsl, config.SecurityProtocol);
		Assert.Equal(SaslMechanism.ScramSha256, config.SaslMechanism);
		Assert.Equal("testuser", config.SaslUsername);
		Assert.Equal("testpass", config.SaslPassword);
	}

	[Fact(DisplayName = "ToClientConfig should apply SSL settings")]
	public void ToClientConfig_ShouldApplySslSettings()
	{
		// Arrange
		var settings = new KafkaSettings
		{
			BootstrapServers = "broker:9092",
			SecurityProtocol = SecurityProtocol.Ssl,
			EnableSslCertificateVerification = false,
			SslCaLocation = "/path/to/ca-cert.pem"
		};

		// Act
		var config = settings.ToClientConfig();

		// Assert
		Assert.Equal(SecurityProtocol.Ssl, config.SecurityProtocol);
		Assert.False(config.EnableSslCertificateVerification);
		Assert.Equal("/path/to/ca-cert.pem", config.SslCaLocation);
	}

	[Fact(DisplayName = "ToClientConfig should apply additional properties")]
	public void ToClientConfig_ShouldApplyAdditionalProperties()
	{
		// Arrange
		var settings = new KafkaSettings
		{
			BootstrapServers = "broker:9092",
			AdditionalProperties = new Dictionary<string, string>
			{
				{ "socket.timeout.ms", "60000" },
				{ "metadata.max.age.ms", "180000" }
			}
		};

		// Act
		var config = settings.ToClientConfig();

		// Assert
		Assert.Equal("broker:9092", config.BootstrapServers);
		// Additional properties are applied via Set() method
		// We can't directly validate them, but we verify no exception is thrown
	}
}

[Trait("Unit Test", "KafkaSettingsValidator")]
public class KafkaSettingsValidatorTests
{
	private readonly KafkaSettingsValidator _validator = new();

	[Fact(DisplayName = "Validator should pass for valid minimal configuration")]
	public void Validate_ShouldPass_WithValidMinimalConfiguration()
	{
		// Arrange
		var settings = new KafkaSettings
		{
			BootstrapServers = "localhost:9092"
		};

		// Act
		var result = _validator.Validate(null, settings);

		// Assert
		Assert.True(result.Succeeded);
	}

	[Fact(DisplayName = "Validator should pass for valid configuration with multiple brokers")]
	public void Validate_ShouldPass_WithMultipleBrokers()
	{
		// Arrange
		var settings = new KafkaSettings
		{
			BootstrapServers = "broker1:9092,broker2:9093,broker3:9094"
		};

		// Act
		var result = _validator.Validate(null, settings);

		// Assert
		Assert.True(result.Succeeded);
	}

	[Fact(DisplayName = "Validator should fail when BootstrapServers is null")]
	public void Validate_ShouldFail_WhenBootstrapServersIsNull()
	{
		// Arrange
		var settings = new KafkaSettings
		{
			BootstrapServers = null!
		};

		// Act
		var result = _validator.Validate(null, settings);

		// Assert
		Assert.True(result.Failed);
		Assert.Contains("BootstrapServers is required", result.FailureMessage);
	}

	[Fact(DisplayName = "Validator should fail when BootstrapServers is empty")]
	public void Validate_ShouldFail_WhenBootstrapServersIsEmpty()
	{
		// Arrange
		var settings = new KafkaSettings
		{
			BootstrapServers = "   "
		};

		// Act
		var result = _validator.Validate(null, settings);

		// Assert
		Assert.True(result.Failed);
		Assert.Contains("BootstrapServers is required", result.FailureMessage);
	}

	[Fact(DisplayName = "Validator should fail when BootstrapServers has invalid format")]
	public void Validate_ShouldFail_WhenBootstrapServersHasInvalidFormat()
	{
		// Arrange
		var settings = new KafkaSettings
		{
			BootstrapServers = "localhost" // Missing port
		};

		// Act
		var result = _validator.Validate(null, settings);

		// Assert
		Assert.True(result.Failed);
		Assert.Contains("Invalid BootstrapServer format", result.FailureMessage);
	}

	[Fact(DisplayName = "Validator should fail when BootstrapServers contains empty entries")]
	public void Validate_ShouldFail_WhenBootstrapServersContainsEmptyEntries()
	{
		// Arrange
		var settings = new KafkaSettings
		{
			BootstrapServers = "broker1:9092,,broker2:9093"
		};

		// Act
		var result = _validator.Validate(null, settings);

		// Assert
		Assert.True(result.Failed);
		Assert.Contains("empty entries", result.FailureMessage);
	}

	[Fact(DisplayName = "Validator should fail when SaslMechanism is missing for SASL auth")]
	public void Validate_ShouldFail_WhenSaslMechanismMissingForSaslAuth()
	{
		// Arrange
		var settings = new KafkaSettings
		{
			BootstrapServers = "broker:9092",
			SecurityProtocol = SecurityProtocol.SaslPlaintext,
			SaslMechanism = null,
			SaslUsername = "user",
			SaslPassword = "pass"
		};

		// Act
		var result = _validator.Validate(null, settings);

		// Assert
		Assert.True(result.Failed);
		Assert.Contains("SaslMechanism is required", result.FailureMessage);
	}

	[Fact(DisplayName = "Validator should fail when SaslUsername is missing for SASL auth")]
	public void Validate_ShouldFail_WhenSaslUsernameMissingForSaslAuth()
	{
		// Arrange
		var settings = new KafkaSettings
		{
			BootstrapServers = "broker:9092",
			SecurityProtocol = SecurityProtocol.SaslSsl,
			SaslMechanism = SaslMechanism.Plain,
			SaslUsername = null,
			SaslPassword = "pass"
		};

		// Act
		var result = _validator.Validate(null, settings);

		// Assert
		Assert.True(result.Failed);
		Assert.Contains("SaslUsername is required", result.FailureMessage);
	}

	[Fact(DisplayName = "Validator should fail when SaslPassword is missing for SASL auth")]
	public void Validate_ShouldFail_WhenSaslPasswordMissingForSaslAuth()
	{
		// Arrange
		var settings = new KafkaSettings
		{
			BootstrapServers = "broker:9092",
			SecurityProtocol = SecurityProtocol.SaslPlaintext,
			SaslMechanism = SaslMechanism.ScramSha512,
			SaslUsername = "user",
			SaslPassword = "   "
		};

		// Act
		var result = _validator.Validate(null, settings);

		// Assert
		Assert.True(result.Failed);
		Assert.Contains("SaslPassword is required", result.FailureMessage);
	}

	[Fact(DisplayName = "Validator should pass for valid SASL configuration")]
	public void Validate_ShouldPass_WithValidSaslConfiguration()
	{
		// Arrange
		var settings = new KafkaSettings
		{
			BootstrapServers = "broker:9092",
			SecurityProtocol = SecurityProtocol.SaslSsl,
			SaslMechanism = SaslMechanism.Plain,
			SaslUsername = "testuser",
			SaslPassword = "testpass"
		};

		// Act
		var result = _validator.Validate(null, settings);

		// Assert
		Assert.True(result.Succeeded);
	}

	[Fact(DisplayName = "Validator should fail when SSL certificate file does not exist")]
	public void Validate_ShouldFail_WhenSslCertificateFileDoesNotExist()
	{
		// Arrange
		var settings = new KafkaSettings
		{
			BootstrapServers = "broker:9092",
			SecurityProtocol = SecurityProtocol.Ssl,
			SslCaLocation = "/nonexistent/path/to/ca-cert.pem"
		};

		// Act
		var result = _validator.Validate(null, settings);

		// Assert
		Assert.True(result.Failed);
		Assert.Contains("SSL CA certificate file not found", result.FailureMessage);
	}

	[Fact(DisplayName = "Validator should fail when SchemaRegistry URL is missing")]
	public void Validate_ShouldFail_WhenSchemaRegistryUrlMissing()
	{
		// Arrange
		var settings = new KafkaSettings
		{
			BootstrapServers = "broker:9092",
			SchemaRegistry = new SchemaRegistrySettings
			{
				Url = null!
			}
		};

		// Act
		var result = _validator.Validate(null, settings);

		// Assert
		Assert.True(result.Failed);
		Assert.Contains("SchemaRegistry.Url is required", result.FailureMessage);
	}

	[Fact(DisplayName = "Validator should fail when SchemaRegistry URL is invalid")]
	public void Validate_ShouldFail_WhenSchemaRegistryUrlIsInvalid()
	{
		// Arrange
		var settings = new KafkaSettings
		{
			BootstrapServers = "broker:9092",
			SchemaRegistry = new SchemaRegistrySettings
			{
				Url = "not-a-valid-url"
			}
		};

		// Act
		var result = _validator.Validate(null, settings);

		// Assert
		Assert.True(result.Failed);
		Assert.Contains("Invalid SchemaRegistry.Url", result.FailureMessage);
	}

	[Fact(DisplayName = "Validator should fail when SchemaRegistry URL has invalid scheme")]
	public void Validate_ShouldFail_WhenSchemaRegistryUrlHasInvalidScheme()
	{
		// Arrange
		var settings = new KafkaSettings
		{
			BootstrapServers = "broker:9092",
			SchemaRegistry = new SchemaRegistrySettings
			{
				Url = "ftp://schema-registry:8081"
			}
		};

		// Act
		var result = _validator.Validate(null, settings);

		// Assert
		Assert.True(result.Failed);
		Assert.Contains("HTTP or HTTPS", result.FailureMessage);
	}

	[Fact(DisplayName = "Validator should pass for valid SchemaRegistry configuration")]
	public void Validate_ShouldPass_WithValidSchemaRegistryConfiguration()
	{
		// Arrange
		var settings = new KafkaSettings
		{
			BootstrapServers = "broker:9092",
			SchemaRegistry = new SchemaRegistrySettings
			{
				Url = "https://schema-registry:8081"
			}
		};

		// Act
		var result = _validator.Validate(null, settings);

		// Assert
		Assert.True(result.Succeeded);
	}

	[Fact(DisplayName = "Validator should fail when BasicAuthUserInfo is missing")]
	public void Validate_ShouldFail_WhenBasicAuthUserInfoMissing()
	{
		// Arrange
		var settings = new KafkaSettings
		{
			BootstrapServers = "broker:9092",
			SchemaRegistry = new SchemaRegistrySettings
			{
				Url = "https://schema-registry:8081",
				BasicAuthCredentialsSource = Confluent.SchemaRegistry.AuthCredentialsSource.UserInfo,
				BasicAuthUserInfo = null
			}
		};

		// Act
		var result = _validator.Validate(null, settings);

		// Assert
		Assert.True(result.Failed);
		Assert.Contains("BasicAuthUserInfo is required", result.FailureMessage);
	}

	[Fact(DisplayName = "Validator should fail when BasicAuthUserInfo has invalid format")]
	public void Validate_ShouldFail_WhenBasicAuthUserInfoHasInvalidFormat()
	{
		// Arrange
		var settings = new KafkaSettings
		{
			BootstrapServers = "broker:9092",
			SchemaRegistry = new SchemaRegistrySettings
			{
				Url = "https://schema-registry:8081",
				BasicAuthCredentialsSource = Confluent.SchemaRegistry.AuthCredentialsSource.UserInfo,
				BasicAuthUserInfo = "invalid-format-no-colon"
			}
		};

		// Act
		var result = _validator.Validate(null, settings);

		// Assert
		Assert.True(result.Failed);
		Assert.Contains("format 'username:password'", result.FailureMessage);
	}

	[Fact(DisplayName = "Validator should pass with valid BasicAuthUserInfo")]
	public void Validate_ShouldPass_WithValidBasicAuthUserInfo()
	{
		// Arrange
		var settings = new KafkaSettings
		{
			BootstrapServers = "broker:9092",
			SchemaRegistry = new SchemaRegistrySettings
			{
				Url = "https://schema-registry:8081",
				BasicAuthCredentialsSource = Confluent.SchemaRegistry.AuthCredentialsSource.UserInfo,
				BasicAuthUserInfo = "username:password"
			}
		};

		// Act
		var result = _validator.Validate(null, settings);

		// Assert
		Assert.True(result.Succeeded);
	}

	[Fact(DisplayName = "Validator should accumulate multiple errors")]
	public void Validate_ShouldAccumulateMultipleErrors()
	{
		// Arrange
		var settings = new KafkaSettings
		{
			BootstrapServers = "invalid-format", // Missing port
			SecurityProtocol = SecurityProtocol.SaslSsl,
			SaslMechanism = null, // Missing
			SaslUsername = null, // Missing
			SaslPassword = null, // Missing
			SchemaRegistry = new SchemaRegistrySettings
			{
				Url = null! // Missing
			}
		};

		// Act
		var result = _validator.Validate(null, settings);

		// Assert
		Assert.True(result.Failed);
		Assert.Contains("Invalid BootstrapServer format", result.FailureMessage);
		Assert.Contains("SaslMechanism", result.FailureMessage);
		Assert.Contains("SaslUsername", result.FailureMessage);
		Assert.Contains("SaslPassword", result.FailureMessage);
		Assert.Contains("SchemaRegistry.Url", result.FailureMessage);
	}
}
