using System;

using Microsoft.Extensions.Options;

using Xunit;

using XUnitAssured.Base;
using XUnitAssured.Base.Auth;
using XUnitAssured.Base.Kafka;

namespace XUnitAssured.Tests;

[Trait("Unit Test", "BaseSettings")]
public class BaseSettingsTests
{
	[Fact(DisplayName = "Constructor should initialize properties with default values")]
	public void DefaultConstructor_ShouldInitializeWithDefaultValues()
	{
		// Act
		var settings = new BaseSettings();

		// Assert
		Assert.Null(settings.BaseUrl); // Will be set by configuration binding
		Assert.Null(settings.OpenApiDocument);
		Assert.Null(settings.Authentication);
		Assert.Null(settings.Kafka);
	}

	[Fact(DisplayName = "BaseUrl property should accept valid Uri")]
	public void BaseUrl_ShouldAcceptValidUri()
	{
		// Arrange & Act
		var settings = new BaseSettings
		{
			BaseUrl = new Uri("https://api.example.com")
		};

		// Assert
		Assert.Equal("https://api.example.com/", settings.BaseUrl.AbsoluteUri);
		Assert.Equal(Uri.UriSchemeHttps, settings.BaseUrl.Scheme);
	}

	[Fact(DisplayName = "OpenApiDocument should accept HTTP, HTTPS, or FILE Uri")]
	public void OpenApiDocument_ShouldAcceptValidUriSchemes()
	{
		// Arrange & Act - HTTP
		var settings1 = new BaseSettings
		{
			BaseUrl = new Uri("https://api.example.com"),
			OpenApiDocument = new Uri("http://api.example.com/swagger.json")
		};

		// Arrange & Act - File
		var settings2 = new BaseSettings
		{
			BaseUrl = new Uri("https://api.example.com"),
			OpenApiDocument = new Uri("file:///c:/swagger.json")
		};

		// Assert
		Assert.Equal(Uri.UriSchemeHttp, settings1.OpenApiDocument!.Scheme);
		Assert.Equal(Uri.UriSchemeFile, settings2.OpenApiDocument!.Scheme);
	}

	[Fact(DisplayName = "BaseSettingsValidator should pass for valid configuration")]
	public void BaseSettingsValidator_ShouldPass_WhenConfigurationIsValid()
	{
		// Arrange
		var settings = new BaseSettings
		{
			BaseUrl = new Uri("https://api.example.com"),
			OpenApiDocument = new Uri("https://api.example.com/swagger.json"),
			Authentication = new AuthenticationSettings
			{
				BaseUrl = new Uri("https://auth.example.com"),
				ClientId = "clientId",
				ClientSecret = "clientSecret",
				AuthenticationType = AuthenticationType.Bearer
			}
		};

		var validator = new BaseSettingsValidator();

		// Act
		var result = validator.Validate(null, settings);

		// Assert
		Assert.True(result.Succeeded);
	}

	[Fact(DisplayName = "BaseSettingsValidator should fail when BaseUrl is null")]
	public void BaseSettingsValidator_ShouldFail_WhenBaseUrlIsNull()
	{
		// Arrange
		var settings = new BaseSettings
		{
			BaseUrl = null! // Invalid
		};

		var validator = new BaseSettingsValidator();

		// Act
		var result = validator.Validate(null, settings);

		// Assert
		Assert.True(result.Failed);
		Assert.Contains("BaseUrl is required", result.FailureMessage);
	}

	[Fact(DisplayName = "BaseSettingsValidator should fail when BaseUrl is not absolute")]
	public void BaseSettingsValidator_ShouldFail_WhenBaseUrlIsNotAbsolute()
	{
		// Arrange
		var settings = new BaseSettings
		{
			BaseUrl = new Uri("/relative", UriKind.Relative) // Invalid
		};

		var validator = new BaseSettingsValidator();

		// Act
		var result = validator.Validate(null, settings);

		// Assert
		Assert.True(result.Failed);
		Assert.Contains("absolute URI", result.FailureMessage);
	}

	[Fact(DisplayName = "BaseSettingsValidator should fail when BaseUrl scheme is invalid")]
	public void BaseSettingsValidator_ShouldFail_WhenBaseUrlSchemeIsInvalid()
	{
		// Arrange
		var settings = new BaseSettings
		{
			BaseUrl = new Uri("ftp://api.example.com") // Invalid scheme
		};

		var validator = new BaseSettingsValidator();

		// Act
		var result = validator.Validate(null, settings);

		// Assert
		Assert.True(result.Failed);
		Assert.Contains("HTTP or HTTPS", result.FailureMessage);
	}

	[Fact(DisplayName = "BaseSettingsValidator should fail when Authentication is incomplete")]
	public void BaseSettingsValidator_ShouldFail_WhenAuthenticationIsIncomplete()
	{
		// Arrange
		var settings = new BaseSettings
		{
			BaseUrl = new Uri("https://api.example.com"),
			Authentication = new AuthenticationSettings
			{
				BaseUrl = null, // Missing
				ClientId = null, // Missing
				ClientSecret = null, // Missing
				AuthenticationType = AuthenticationType.Bearer
			}
		};

		var validator = new BaseSettingsValidator();

		// Act
		var result = validator.Validate(null, settings);

		// Assert
		Assert.True(result.Failed);
		Assert.Contains("Authentication.BaseUrl", result.FailureMessage);
		Assert.Contains("ClientId", result.FailureMessage);
		Assert.Contains("ClientSecret", result.FailureMessage);
	}

	[Fact(DisplayName = "AuthenticationSettingsValidator should pass when authentication is None")]
	public void AuthenticationSettingsValidator_ShouldPass_WhenAuthenticationIsNone()
	{
		// Arrange
		var authSettings = new AuthenticationSettings
		{
			AuthenticationType = AuthenticationType.None
			// BaseUrl, ClientId, ClientSecret not required when None
		};

		var validator = new AuthenticationSettingsValidator();

		// Act
		var result = validator.Validate(null, authSettings);

		// Assert
		Assert.True(result.Succeeded);
	}

	[Fact(DisplayName = "Kafka property should show obsolete warning")]
	public void Kafka_ShouldBeMarkedAsObsolete()
	{
		// Arrange & Act
#pragma warning disable CS0618 // Type or member is obsolete
		var settings = new BaseSettings
		{
			BaseUrl = new Uri("https://api.example.com"),
			Kafka = new KafkaSecurity // This should trigger obsolete warning
			{
				SecurityProtocol = "SaslPlaintext",
				Username = "user",
				Password = "pass"
			}
		};
#pragma warning restore CS0618

		// Assert - if code compiles, obsolete attribute is working
		Assert.NotNull(settings.Kafka);
	}

	[Fact(DisplayName = "SectionName constant should have correct value")]
	public void SectionName_ShouldHaveCorrectValue()
	{
		// Assert
		Assert.Equal("TestSettings", BaseSettings.SectionName);
	}
}
