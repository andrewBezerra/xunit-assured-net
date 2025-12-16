using System.Collections.Generic;
using XUnitAssured.Http.Configuration;
using XUnitAssured.Http.Extensions;

namespace XUnitAssured.Tests.HttpTests;

/// <summary>
/// Tests for Custom Header authentication.
/// </summary>
public class CustomHeaderAuthTests
{
	[Fact]
	public void Should_Add_Single_Custom_Header()
	{
		// Arrange & Act
		var scenario = Given()
			.ApiResource("https://api.example.com/resource")
			.WithCustomHeader("X-Auth-Token", "abc123")
			.Get();

		// Assert
		scenario.ShouldNotBeNull();
		scenario.CurrentStep.ShouldNotBeNull();
	}

	[Fact]
	public void Should_Add_Multiple_Custom_Headers()
	{
		// Arrange
		var headers = new Dictionary<string, string>
		{
			["X-Auth-Token"] = "token123",
			["X-Session-ID"] = "session456",
			["X-Request-ID"] = "req789"
		};

		// Act
		var scenario = Given()
			.ApiResource("https://api.example.com/resource")
			.WithCustomHeaders(headers)
			.Get();

		// Assert
		scenario.ShouldNotBeNull();
	}

	[Fact]
	public void Should_Support_CustomHeader_Via_Config()
	{
		// Arrange & Act
		var scenario = Given()
			.ApiResource("https://api.example.com/resource")
			.WithAuthConfig(config =>
			{
				config.UseCustomHeader("X-Custom", "value");
			})
			.Get();

		// Assert
		scenario.ShouldNotBeNull();
	}

	[Fact]
	public void CustomHeaderAuthConfig_Should_Add_Headers()
	{
		// Arrange
		var config = new CustomHeaderAuthConfig();

		// Act
		config.AddHeader("X-Test", "value1");
		config.AddHeader("X-Another", "value2");

		// Assert
		config.Headers.Count.ShouldBe(2);
		config.Headers["X-Test"].ShouldBe("value1");
		config.Headers["X-Another"].ShouldBe("value2");
	}

	[Fact]
	public void Should_Throw_When_No_Headers_Provided()
	{
		// Arrange
		var scenario = Given().ApiResource("https://api.example.com/resource");

		// Act & Assert
		Should.Throw<ArgumentException>(() => 
			scenario.WithCustomHeaders(new Dictionary<string, string>()));
	}

	[Fact]
	public void Should_Throw_When_Not_Http_Step()
	{
		// Arrange
		var scenario = Given();

		// Act & Assert
		Should.Throw<InvalidOperationException>(() =>
			scenario.WithCustomHeader("X-Auth", "value"));
	}

	[Fact(Skip = "Integration test - requires real API")]
	public void Integration_Should_Authenticate_With_Custom_Header()
	{
		var authToken = Environment.GetEnvironmentVariable("TEST_AUTH_TOKEN");

		if (!string.IsNullOrEmpty(authToken))
		{
			Given()
				.ApiResource("https://api.example.com/protected")
				.WithCustomHeader("X-Auth-Token", authToken)
				.Get()
				.Validate(response =>
				{
					response.StatusCode.ShouldBe(200);
				});
		}
	}
}
