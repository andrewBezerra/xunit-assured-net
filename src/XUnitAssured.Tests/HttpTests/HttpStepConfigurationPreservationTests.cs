using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;

using Shouldly;
using Xunit;

using XUnitAssured.Core.Abstractions;
using XUnitAssured.Core.DSL;
using XUnitAssured.Http.Configuration;
using XUnitAssured.Http.Extensions;
using XUnitAssured.Http.Steps;

namespace XUnitAssured.Tests.HttpTests;

[Trait("Category", "Http")]
[Trait("Component", "RequestStep")]
/// <summary>
/// Regression tests guarding the immutable-rebuild pattern used by the HTTP DSL.
/// Every fluent method that changes one value rebuilds the whole step, and any value
/// left out of that rebuild is silently dropped — which historically cost
/// <c>WithTimeout()</c> both its authentication and its custom HttpClient.
/// </summary>
public class HttpStepConfigurationPreservationTests
{
	/// <summary>
	/// Values a fully-configured step carries, used as the baseline for every
	/// preservation assertion below.
	/// </summary>
	private static (ITestScenario Scenario, HttpClient Client) BuildFullyConfiguredScenario()
	{
		var client = new HttpClient { BaseAddress = new Uri("https://example.test") };

		var scenario = new TestScenario();
		scenario.WithHttpClient(client)
			.ApiResource("/api/products")
			.WithBearerToken("token-abc")
			.WithHeader("X-Correlation-Id", "corr-1")
			.WithQueryParam("page", 2);

		return (scenario, client);
	}

	private static HttpRequestStep CurrentStep(ITestScenario scenario) =>
		scenario.CurrentStep.ShouldBeOfType<HttpRequestStep>();

	[Fact(DisplayName = "WithTimeout should preserve authentication configuration")]
	public void WithTimeout_Should_Preserve_AuthConfig()
	{
		// Arrange
		var (scenario, _) = BuildFullyConfiguredScenario();
		CurrentStep(scenario).AuthConfig.ShouldNotBeNull();

		// Act
		scenario.WithTimeout(90);

		// Assert
		var step = CurrentStep(scenario);
		step.TimeoutSeconds.ShouldBe(90);
		step.AuthConfig.ShouldNotBeNull("WithTimeout must not discard authentication");
		step.AuthConfig!.Type.ShouldBe(AuthenticationType.Bearer);
		step.AuthConfig.Bearer!.Token.ShouldBe("token-abc");
	}

	[Fact(DisplayName = "WithTimeout should preserve the custom HttpClient")]
	public void WithTimeout_Should_Preserve_CustomHttpClient()
	{
		// Arrange
		var (scenario, client) = BuildFullyConfiguredScenario();

		// Act
		scenario.WithTimeout(90);

		// Assert
		CurrentStep(scenario).CustomHttpClient.ShouldBeSameAs(client,
			"WithTimeout must not discard the HttpClient supplied for integration tests");
	}

	[Fact(DisplayName = "WithTimeout should preserve headers and query parameters")]
	public void WithTimeout_Should_Preserve_Headers_And_QueryParams()
	{
		// Arrange
		var (scenario, _) = BuildFullyConfiguredScenario();

		// Act
		scenario.WithTimeout(90);

		// Assert
		var step = CurrentStep(scenario);
		step.Url.ShouldBe("/api/products");
		step.Headers["X-Correlation-Id"].ShouldBe("corr-1");
		step.QueryParams.ContainsKey("page").ShouldBeTrue();
	}

	[Fact(DisplayName = "Setting the HTTP method should preserve authentication and HttpClient")]
	public void Method_Change_Should_Preserve_Auth_And_Client()
	{
		// Arrange
		var (scenario, client) = BuildFullyConfiguredScenario();

		// Act
		scenario.Post(new { Name = "widget" });

		// Assert
		var step = CurrentStep(scenario);
		step.Method.ShouldBe(HttpMethod.Post);
		step.Body.ShouldNotBeNull();
		step.AuthConfig.ShouldNotBeNull();
		step.CustomHttpClient.ShouldBeSameAs(client);
	}

	[Fact(DisplayName = "PostFormData should preserve authentication and HttpClient")]
	public void PostFormData_Should_Preserve_Auth_And_Client()
	{
		// Arrange
		var (scenario, client) = BuildFullyConfiguredScenario();

		// Act
		scenario.PostFormData(new Dictionary<string, string> { ["grant_type"] = "client_credentials" });

		// Assert
		var step = CurrentStep(scenario);
		step.Method.ShouldBe(HttpMethod.Post);
		step.Body.ShouldBeAssignableTo<HttpContent>();
		step.AuthConfig.ShouldNotBeNull();
		step.CustomHttpClient.ShouldBeSameAs(client);
	}

	[Fact(DisplayName = "Applying authentication after a timeout should preserve the timeout")]
	public void Auth_After_Timeout_Should_Preserve_Timeout()
	{
		// Arrange
		var scenario = new TestScenario();
		scenario.ApiResource("/api/products").WithTimeout(75);

		// Act
		scenario.WithBasicAuth("user", "secret");

		// Assert
		var step = CurrentStep(scenario);
		step.TimeoutSeconds.ShouldBe(75, "authentication must not reset a configured timeout");
		step.AuthConfig!.Type.ShouldBe(AuthenticationType.Basic);
	}

	/// <summary>
	/// The strongest guard: rather than listing today's properties, this walks them
	/// reflectively. A property added to <see cref="HttpRequestStep"/> in the future
	/// that the copy constructor forgets will fail here without anyone remembering
	/// to extend this test.
	/// </summary>
	[Fact(DisplayName = "Copy constructor should carry over every configurable property")]
	public void CopyConstructor_Should_Carry_Every_Configurable_Property()
	{
		// Arrange — a step where no property holds its default value, so a dropped
		// property shows up as a difference rather than coinciding with the default.
		var original = new HttpRequestStep
		{
			Url = "https://example.test/api/things",
			Method = HttpMethod.Patch,
			Body = new { Name = "value" },
			Headers = new Dictionary<string, string> { ["X-Test"] = "1" },
			QueryParams = new Dictionary<string, object?> { ["q"] = "term" },
			TimeoutSeconds = 123,
			AuthConfig = new HttpAuthConfig(),
			CustomHttpClient = new HttpClient()
		};

		// Act
		var copy = new HttpRequestStep(original);

		// Assert
		var settableProperties = typeof(HttpRequestStep)
			.GetProperties(BindingFlags.Public | BindingFlags.Instance)
			.Where(p => p.SetMethod != null)
			// Result/IsValid are execution state, not configuration.
			.Where(p => p.Name is not (nameof(HttpRequestStep.Result) or nameof(HttpRequestStep.IsValid)))
			.ToList();

		settableProperties.ShouldNotBeEmpty();

		foreach (var property in settableProperties)
		{
			property.GetValue(copy).ShouldBe(
				property.GetValue(original),
				$"'{property.Name}' was not carried over by the HttpRequestStep copy constructor");
		}
	}

	[Fact(DisplayName = "Copy constructor should reject a null source")]
	public void CopyConstructor_Should_Reject_Null_Source()
	{
		Should.Throw<ArgumentNullException>(() => new HttpRequestStep(null!));
	}
}
