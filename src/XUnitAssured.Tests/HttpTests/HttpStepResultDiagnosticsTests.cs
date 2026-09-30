using System;
using System.Net.Http;

using Shouldly;
using Xunit;

using XUnitAssured.Http.Results;

namespace XUnitAssured.Tests.HttpTests;

[Trait("Category", "Http")]
[Trait("Component", "StepResult")]
/// <summary>
/// Tests that a transport-level HTTP failure keeps enough detail to be diagnosed.
/// A failed request used to report only <c>Exception.Message</c>, which for a socket
/// or DNS error says nothing about where in the test the call originated.
/// </summary>
public class HttpStepResultDiagnosticsTests
{
	private static Exception CapturedException()
	{
		try
		{
			throw new HttpRequestException("No such host is known.");
		}
		catch (HttpRequestException ex)
		{
			// Thrown and caught so the instance carries a real stack trace.
			return ex;
		}
	}

	[Fact(DisplayName = "CreateFailure should keep the exception message as the reported error")]
	public void CreateFailure_Should_Keep_Message_As_Error()
	{
		var result = HttpStepResult.CreateFailure(CapturedException());

		result.Success.ShouldBeFalse();
		result.Errors.ShouldHaveSingleItem();
		result.Errors[0].ShouldBe("No such host is known.");
	}

	[Fact(DisplayName = "CreateFailure should expose the exception type and stack trace")]
	public void CreateFailure_Should_Expose_Exception_Diagnostics()
	{
		var result = HttpStepResult.CreateFailure(CapturedException());

		result.GetProperty<string>("ExceptionType").ShouldBe(typeof(HttpRequestException).FullName);
		result.GetProperty<string>("ExceptionMessage").ShouldBe("No such host is known.");
		result.GetProperty<string>("ExceptionStackTrace").ShouldNotBeNullOrWhiteSpace();
		result.GetProperty<string>("ExceptionDetail").ShouldContain(nameof(HttpRequestException));
	}

	[Fact(DisplayName = "CreateFailure should report status code zero for a transport failure")]
	public void CreateFailure_Should_Report_Zero_Status_Code()
	{
		var result = HttpStepResult.CreateFailure(CapturedException());

		result.StatusCode.ShouldBe(0, "no response was received, so there is no status code");
		result.IsSuccessStatusCode.ShouldBeFalse();
	}

	[Fact(DisplayName = "CreateFailure should reject a null exception")]
	public void CreateFailure_Should_Reject_Null_Exception()
	{
		Should.Throw<ArgumentNullException>(() => HttpStepResult.CreateFailure((Exception)null!));
	}

	[Fact(DisplayName = "CreateHttpSuccess should mark 4xx and 5xx responses as unsuccessful")]
	public void CreateHttpSuccess_Should_Not_Mark_Error_Responses_As_Success()
	{
		HttpStepResult.CreateHttpSuccess(200).Success.ShouldBeTrue();
		HttpStepResult.CreateHttpSuccess(404).Success.ShouldBeFalse();
		HttpStepResult.CreateHttpSuccess(500).Success.ShouldBeFalse();
	}
}
