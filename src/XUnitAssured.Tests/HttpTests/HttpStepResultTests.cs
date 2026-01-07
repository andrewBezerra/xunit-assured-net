using System.Net;
using Shouldly;
using Xunit;
using XUnitAssured.Core.Results;
using XUnitAssured.Http.Results;

namespace XUnitAssured.Tests.HttpTests;

[Trait("Category", "Http")]
[Trait("Component", "Result")]
/// <summary>
/// Unit tests for HttpStepResult class.
/// Validates HTTP-specific result functionality and helpers.
/// </summary>
public class HttpStepResultTests
{
	[Fact(DisplayName = "HttpStepResult should expose status code correctly")]
	public void HttpStepResult_Should_Expose_StatusCode()
	{
		// Arrange & Act
		var result = new HttpStepResult
		{
			Success = true,
			Data = new { Name = "Test" },
			Properties = new Dictionary<string, object?>
			{
				["StatusCode"] = 200
			}
		};

		// Assert
		result.StatusCode.ShouldBe(200);
		result.StatusCodeEnum.ShouldBe(HttpStatusCode.OK);
	}

	[Fact(DisplayName = "HttpStepResult should expose response body correctly")]
	public void HttpStepResult_Should_Expose_ResponseBody()
	{
		// Arrange
		var responseData = new { Id = 1, Name = "Test User" };

		// Act
		var result = new HttpStepResult
		{
			Success = true,
			Data = responseData,
			Properties = new Dictionary<string, object?>
			{
				["StatusCode"] = 201
			}
		};

		// Assert
		result.ResponseBody.ShouldBe(responseData);
		result.ResponseBody.ShouldNotBeNull();
		
		// Verify GetResponseBody returns the same object reference
		var dynamicResponse = result.GetResponseBody<object>();
		dynamicResponse.ShouldBe(responseData);
	}

	[Fact(DisplayName = "HttpStepResult should expose response headers correctly")]
	public void HttpStepResult_Should_Expose_Headers()
	{
		// Arrange
		var headers = new Dictionary<string, IEnumerable<string>>
		{
			["Content-Type"] = new[] { "application/json" },
			["X-Custom-Header"] = new[] { "custom-value" }
		};

		// Act
		var result = new HttpStepResult
		{
			Success = true,
			Properties = new Dictionary<string, object?>
			{
				["StatusCode"] = 200,
				["Headers"] = headers
			}
		};

		// Assert
		result.Headers.ShouldNotBeNull();
		result.Headers!.Count.ShouldBe(2);
		result.Headers["Content-Type"].ShouldContain("application/json");
		result.Headers["X-Custom-Header"].ShouldContain("custom-value");
	}

	[Fact(DisplayName = "HttpStepResult should expose ContentType and ReasonPhrase correctly")]
	public void HttpStepResult_Should_Expose_ContentType_And_ReasonPhrase()
	{
		// Arrange & Act
		var result = new HttpStepResult
		{
			Success = true,
			Properties = new Dictionary<string, object?>
			{
				["StatusCode"] = 200,
				["ContentType"] = "application/json",
				["ReasonPhrase"] = "OK"
			}
		};

		// Assert
		result.ContentType.ShouldBe("application/json");
		result.ReasonPhrase.ShouldBe("OK");
	}

	[Fact(DisplayName = "IsSuccessStatusCode should return true for 2xx status codes")]
	public void IsSuccessStatusCode_Should_Return_True_For_2xx()
	{
		// Arrange & Act
		var result200 = CreateResultWithStatusCode(200);
		var result201 = CreateResultWithStatusCode(201);
		var result204 = CreateResultWithStatusCode(204);

		// Assert
		result200.IsSuccessStatusCode.ShouldBeTrue();
		result201.IsSuccessStatusCode.ShouldBeTrue();
		result204.IsSuccessStatusCode.ShouldBeTrue();
	}

	[Fact(DisplayName = "IsSuccessStatusCode should return false for non-2xx status codes")]
	public void IsSuccessStatusCode_Should_Return_False_For_Non_2xx()
	{
		// Arrange & Act
		var result404 = CreateResultWithStatusCode(404);
		var result500 = CreateResultWithStatusCode(500);

		// Assert
		result404.IsSuccessStatusCode.ShouldBeFalse();
		result500.IsSuccessStatusCode.ShouldBeFalse();
	}

	[Fact(DisplayName = "IsRedirect should return true for 3xx status codes")]
	public void IsRedirect_Should_Return_True_For_3xx()
	{
		// Arrange & Act
		var result301 = CreateResultWithStatusCode(301);
		var result302 = CreateResultWithStatusCode(302);
		var result304 = CreateResultWithStatusCode(304);

		// Assert
		result301.IsRedirect.ShouldBeTrue();
		result302.IsRedirect.ShouldBeTrue();
		result304.IsRedirect.ShouldBeTrue();
	}

	[Fact(DisplayName = "IsClientError should return true for 4xx status codes")]
	public void IsClientError_Should_Return_True_For_4xx()
	{
		// Arrange & Act
		var result400 = CreateResultWithStatusCode(400);
		var result404 = CreateResultWithStatusCode(404);
		var result403 = CreateResultWithStatusCode(403);

		// Assert
		result400.IsClientError.ShouldBeTrue();
		result404.IsClientError.ShouldBeTrue();
		result403.IsClientError.ShouldBeTrue();
	}

	[Fact(DisplayName = "IsServerError should return true for 5xx status codes")]
	public void IsServerError_Should_Return_True_For_5xx()
	{
		// Arrange & Act
		var result500 = CreateResultWithStatusCode(500);
		var result502 = CreateResultWithStatusCode(502);
		var result503 = CreateResultWithStatusCode(503);

		// Assert
		result500.IsServerError.ShouldBeTrue();
		result502.IsServerError.ShouldBeTrue();
		result503.IsServerError.ShouldBeTrue();
	}

	[Fact(DisplayName = "CreateHttpSuccess should create complete HTTP result with all properties")]
	public void CreateHttpSuccess_Should_Create_Complete_Result()
	{
		// Arrange
		var responseBody = new { Id = 1, Name = "Test" };
		var headers = new Dictionary<string, IEnumerable<string>>
		{
			["Content-Type"] = new[] { "application/json" }
		};

		// Act
		var result = HttpStepResult.CreateHttpSuccess(
			statusCode: 201,
			responseBody: responseBody,
			headers: headers,
			contentType: "application/json",
			reasonPhrase: "Created"
		);

		// Assert
		result.Success.ShouldBeTrue();
		result.StatusCode.ShouldBe(201);
		result.ResponseBody.ShouldBe(responseBody);
		result.ContentType.ShouldBe("application/json");
		result.ReasonPhrase.ShouldBe("Created");
		result.Headers.ShouldNotBeNull();
		result.Metadata.Status.ShouldBe(StepStatus.Succeeded);
	}

	[Fact(DisplayName = "CreateHttpSuccess should set Success to false for non-2xx status codes")]
	public void CreateHttpSuccess_Should_Set_Success_False_For_Non_2xx()
	{
		// Act
		var result = HttpStepResult.CreateHttpSuccess(
			statusCode: 404,
			responseBody: new { Error = "Not Found" }
		);

		// Assert
		result.Success.ShouldBeFalse();
		result.StatusCode.ShouldBe(404);
		result.IsClientError.ShouldBeTrue();
	}

	[Fact(DisplayName = "CreateFailure should create failed result with exception message")]
	public void CreateFailure_Should_Create_Failed_Result()
	{
		// Arrange
		var exception = new HttpRequestException("Network error");

		// Act
		var result = HttpStepResult.CreateFailure(exception);

		// Assert
		result.Success.ShouldBeFalse();
		result.StatusCode.ShouldBe(0); // No response received
		result.Metadata.Status.ShouldBe(StepStatus.Failed);
		result.Errors.Count.ShouldBe(1);
		result.Errors[0].ShouldBe("Network error");
	}

	// Helper method
	private HttpStepResult CreateResultWithStatusCode(int statusCode)
	{
		return new HttpStepResult
		{
			Success = statusCode >= 200 && statusCode <= 299,
			Properties = new Dictionary<string, object?>
			{
				["StatusCode"] = statusCode
			}
		};
	}
}
