using System.Net;
using Shouldly;
using Xunit;
using XUnitAssured.Core.Storage;
using XUnitAssured.Http.Results;
using XUnitAssured.Http.Steps;

namespace XUnitAssured.Tests.HttpTests;

/// <summary>
/// Integration tests for HttpRequestStep.
/// Uses real HTTP requests to public APIs for testing.
/// </summary>
public class HttpRequestStepTests
{
	[Fact]
	public async Task HttpRequestStep_Should_Execute_Get_Request()
	{
		// Arrange
		var step = new HttpRequestStep
		{
			Url = "https://jsonplaceholder.typicode.com/posts/1",
			Method = HttpMethod.Get
		};

		var context = new MockTestContext();

		// Act
		var result = await step.ExecuteAsync(context);

		// Assert
		result.ShouldNotBeNull();
		result.Success.ShouldBeTrue();
		result.ShouldBeOfType<HttpStepResult>();

		var httpResult = (HttpStepResult)result;
		httpResult.StatusCode.ShouldBe(200);
		httpResult.IsSuccessStatusCode.ShouldBeTrue();
		httpResult.ResponseBody.ShouldNotBeNull();
	}

	[Fact]
	public async Task HttpRequestStep_Should_Execute_Post_Request()
	{
		// Arrange
		var requestBody = new { title = "Test Post", body = "Test content", userId = 1 };
		var step = new HttpRequestStep
		{
			Url = "https://jsonplaceholder.typicode.com/posts",
			Method = HttpMethod.Post,
			Body = requestBody
		};

		var context = new MockTestContext();

		// Act
		var result = await step.ExecuteAsync(context);

		// Assert
		result.ShouldNotBeNull();
		result.Success.ShouldBeTrue();

		var httpResult = (HttpStepResult)result;
		httpResult.StatusCode.ShouldBe(201);
		httpResult.IsSuccessStatusCode.ShouldBeTrue();
	}

	[Fact]
	public async Task HttpRequestStep_Should_Handle_404_Error()
	{
		// Arrange
		var step = new HttpRequestStep
		{
			Url = "https://jsonplaceholder.typicode.com/posts/999999",
			Method = HttpMethod.Get
		};

		var context = new MockTestContext();

		// Act
		var result = await step.ExecuteAsync(context);

		// Assert
		result.ShouldNotBeNull();
		result.Success.ShouldBeFalse(); // 404 is not success

		var httpResult = (HttpStepResult)result;
		httpResult.StatusCode.ShouldBe(404);
		httpResult.IsClientError.ShouldBeTrue();
	}

	[Fact]
	public async Task HttpRequestStep_Should_Add_Custom_Headers()
	{
		// Arrange
		var step = new HttpRequestStep
		{
			Url = "https://httpbin.org/headers",
			Method = HttpMethod.Get,
			Headers = new Dictionary<string, string>
			{
				["X-Custom-Header"] = "TestValue"
			}
		};

		var context = new MockTestContext();

		// Act
		var result = await step.ExecuteAsync(context);

		// Assert
		result.ShouldNotBeNull();
		result.Success.ShouldBeTrue();

		var httpResult = (HttpStepResult)result;
		httpResult.StatusCode.ShouldBe(200);
	}

	[Fact]
	public async Task HttpRequestStep_Should_Add_Query_Parameters()
	{
		// Arrange
		var step = new HttpRequestStep
		{
			Url = "https://jsonplaceholder.typicode.com/posts",
			Method = HttpMethod.Get,
			QueryParams = new Dictionary<string, object?>
			{
				["userId"] = 1
			}
		};

		var context = new MockTestContext();

		// Act
		var result = await step.ExecuteAsync(context);

		// Assert
		result.ShouldNotBeNull();
		result.Success.ShouldBeTrue();

		var httpResult = (HttpStepResult)result;
		httpResult.StatusCode.ShouldBe(200);
		httpResult.ResponseBody.ShouldNotBeNull();
	}

	[Fact]
	public async Task HttpRequestStep_Should_Set_IsExecuted_After_Execution()
	{
		// Arrange
		var step = new HttpRequestStep
		{
			Url = "https://jsonplaceholder.typicode.com/posts/1",
			Method = HttpMethod.Get
		};

		var context = new MockTestContext();

		// Act
		step.IsExecuted.ShouldBeFalse();
		await step.ExecuteAsync(context);

		// Assert
		step.IsExecuted.ShouldBeTrue();
		step.Result.ShouldNotBeNull();
	}

	[Fact]
	public async Task HttpRequestStep_Should_Validate_Successfully()
	{
		// Arrange
		var step = new HttpRequestStep
		{
			Url = "https://jsonplaceholder.typicode.com/posts/1",
			Method = HttpMethod.Get
		};

		var context = new MockTestContext();
		await step.ExecuteAsync(context);

		// Act
		step.Validate(result =>
		{
			var httpResult = (HttpStepResult)result;
			httpResult.StatusCode.ShouldBe(200);
			httpResult.IsSuccessStatusCode.ShouldBeTrue();
		});

		// Assert
		step.IsValid.ShouldBeTrue();
	}

	[Fact]
	public async Task HttpRequestStep_Should_Throw_When_Validating_Before_Execution()
	{
		// Arrange
		var step = new HttpRequestStep
		{
			Url = "https://jsonplaceholder.typicode.com/posts/1",
			Method = HttpMethod.Get
		};

		// Act & Assert
		Should.Throw<InvalidOperationException>(() => step.Validate(result => { }));
	}

	[Fact]
	public void HttpRequestStep_Should_Have_Correct_StepType()
	{
		// Arrange & Act
		var step = new HttpRequestStep
		{
			Url = "https://example.com"
		};

		// Assert
		step.StepType.ShouldBe("Http");
	}

	[Fact]
	public async Task HttpRequestStep_Should_Handle_Timeout()
	{
		// Arrange
		var step = new HttpRequestStep
		{
			Url = "https://httpbin.org/delay/10", // Delays 10 seconds
			Method = HttpMethod.Get,
			TimeoutSeconds = 1 // 1 second timeout
		};

		var context = new MockTestContext();

		// Act
		var result = await step.ExecuteAsync(context);

		// Assert
		result.ShouldNotBeNull();
		result.Success.ShouldBeFalse();
		result.Errors.ShouldNotBeEmpty();
	}

	// Mock test context for testing
	private class MockTestContext : Core.Abstractions.ITestContext
	{
		public Core.Abstractions.IStepStorage Steps { get; } = new StepStorage();
		public IDictionary<string, object?> Properties { get; } = new Dictionary<string, object?>();

		public T? GetProperty<T>(string key)
		{
			if (Properties.TryGetValue(key, out var value) && value is T typedValue)
				return typedValue;
			return default;
		}

		public void SetProperty<T>(string key, T? value)
		{
			Properties[key] = value;
		}
	}
}
