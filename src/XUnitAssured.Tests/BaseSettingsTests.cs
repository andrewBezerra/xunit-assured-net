using System;
using Xunit;
using XUnitAssured.Base;
using XUnitAssured.Base.Auth;
using XUnitAssured.Base.Kafka;

namespace XUnitAssured.Tests;

[Trait("Unit Test", "BaseSettings")]
public class BaseSettingsTests
{
	[Fact(DisplayName = "Ctor padrão deve inicializar propriedades com valores default esperados")] 
	public void DefaultConstructor_ShouldInitializeExpectedDefaults()
	{
		// Act
		var settings = new BaseSettings();

		// Assert
		Assert.Equal(string.Empty, settings.BaseUrl);
		Assert.Equal(string.Empty, settings.OpenApiDocument);
		Assert.NotNull(settings.Authentication);
		Assert.Null(settings.Kafka);
		Assert.Equal(AuthenticationType.None, settings.Authentication!.AuthenticationType);
	}

	[Fact(DisplayName = "Ctor parametrizado deve atribuir valores informados")] 
	public void ParameterizedConstructor_ShouldAssignProvidedValues()
	{
		// Arrange
		var auth = new AuthenticationSettings("https://auth", "client", "secret", AuthenticationType.Bearer);
		var kafka = new KafkaSecurity { SecurityProtocol = "SaslPlaintext", SaslMechanisms = "Plain", Username = "user", Password = "pwd" };

		// Act
		var settings = new BaseSettings("https://api", "openapi.json", auth, kafka);

		// Assert
		Assert.Equal("https://api", settings.BaseUrl);
		Assert.Equal("openapi.json", settings.OpenApiDocument);
		Assert.Same(auth, settings.Authentication);
		Assert.Same(kafka, settings.Kafka);
	}

	[Fact(DisplayName = "Validate não deve lançar exceção quando configuração é válida")] 
	public void Validate_ShouldNotThrow_WhenConfigurationIsValid()
	{
		// Arrange
		var settings = new BaseSettings
		{
			BaseUrl = "https://api.example.com",
			OpenApiDocument = "openapi.json",
			Authentication = new AuthenticationSettings("https://auth.example.com", "clientId", "clientSecret", AuthenticationType.Bearer),
			Kafka = new KafkaSecurity { SecurityProtocol = "SaslPlaintext", SaslMechanisms = "Plain", Username = "user", Password = "pass" }
		};

		// Act & Assert
		var ex = Record.Exception(() => settings.Validate());
		Assert.Null(ex);
	}

	[Fact(DisplayName = "Validate deve lançar exceção quando BaseUrl inválida")] 
	public void Validate_ShouldThrow_WhenBaseUrlIsInvalid()
	{
		// Arrange
		var settings = new BaseSettings { BaseUrl = "htp:/invalida" }; // dispara regra de URL inválida

		// Act
		var ex = Assert.Throws<InvalidTestSettingsException>(() => settings.Validate());

		// Assert
		Assert.Contains("A URL deve ser válida", ex.Message, StringComparison.OrdinalIgnoreCase);
	}

	[Fact(DisplayName = "Validate deve lançar exceção quando Authentication Bearer incompleta")] 
	public void Validate_ShouldThrow_WhenAuthenticationBearerIncomplete()
	{
		// Arrange - AuthenticationType Bearer com campos faltando
		var settings = new BaseSettings
		{
			BaseUrl = "https://api.example.com", // válido para não interferir
			Authentication = new AuthenticationSettings(null, null, null, AuthenticationType.Bearer)
		};

		// Act
		var ex = Assert.Throws<InvalidTestSettingsException>(() => settings.Validate());

		// Assert
		Assert.Contains("BaseUrl não pode ser vazio", ex.Message);
		Assert.Contains("ClientId não pode ser vazio", ex.Message);
		Assert.Contains("ClientSecret não pode ser vazio", ex.Message);
	}

	[Fact(DisplayName = "Validate deve lançar exceção quando Kafka SaslPlaintext incompleto")] 
	public void Validate_ShouldThrow_WhenKafkaSaslPlaintextIncomplete()
	{
		// Arrange
		var settings = new BaseSettings
		{
			BaseUrl = "https://api.example.com",
			Kafka = new KafkaSecurity { SecurityProtocol = "SaslPlaintext", SaslMechanisms = string.Empty, Username = string.Empty, Password = null }
		};

		// Act
		var ex = Assert.Throws<InvalidTestSettingsException>(() => settings.Validate());

		// Assert
		Assert.Contains("SaslMechanisms não pode ser vazio", ex.Message);
		Assert.Contains("Username não pode ser vazio", ex.Message);
		Assert.Contains("Password não pode ser nulo", ex.Message);
	}
}
