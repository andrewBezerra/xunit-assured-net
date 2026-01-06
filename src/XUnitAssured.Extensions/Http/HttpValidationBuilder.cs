using System;
using Shouldly;
using XUnitAssured.Core.Abstractions;
using XUnitAssured.Extensions.Core;
using XUnitAssured.Http.Extensions;
using XUnitAssured.Http.Results;

namespace XUnitAssured.Extensions.Http;

/// <summary>
/// HTTP-specific validation builder that extends the generic ValidationBuilder.
/// Provides convenient methods for HTTP/REST API testing with fluent assertions.
/// </summary>
public class HttpValidationBuilder : ValidationBuilder<HttpStepResult>
{
	/// <summary>
	/// Initializes a new instance of the HttpValidationBuilder.
	/// </summary>
	/// <param name="scenario">The test scenario containing the executed HTTP step</param>
	public HttpValidationBuilder(ITestScenario scenario) : base(scenario)
	{
	}

	/// <summary>
	/// Marks the transition from "When" (action) to "Then" (assertions) in BDD-style tests.
	/// Returns HttpValidationBuilder to maintain fluent chain type.
	/// </summary>
	/// <returns>The same HTTP validation builder for method chaining</returns>
	public new HttpValidationBuilder Then()
	{
		base.Then();
		return this;
	}

	/// <summary>
	/// Asserts that the HTTP status code matches the expected value.
	/// </summary>
	/// <param name="expectedStatusCode">The expected HTTP status code (e.g., 200, 404, 500)</param>
	/// <returns>The same HTTP validation builder for method chaining</returns>
	/// <exception cref="ShouldAssertException">Thrown when status code doesn't match</exception>
	/// <example>
	/// <code>
	/// .Then()
	///     .AssertStatusCode(200)
	///     .AssertStatusCode(404);
	/// </code>
	/// </example>
	public HttpValidationBuilder AssertStatusCode(int expectedStatusCode)
	{
		Result.StatusCode.ShouldBe(expectedStatusCode,
			$"Expected HTTP status code {expectedStatusCode} but got {Result.StatusCode}");
		return this;
	}

	/// <summary>
	/// Validates that the JSON response matches the schema of the specified type T.
	/// Performs complete contract validation and detects breaking changes automatically.
	/// Delegates to ValidationBuilderExtensions.ValidateContract from XUnitAssured.Http.
	/// </summary>
	/// <typeparam name="T">The type to validate against (e.g., Product, User, ApiResponse)</typeparam>
	/// <returns>The same HTTP validation builder for method chaining</returns>
	/// <exception cref="InvalidOperationException">Thrown when response body is empty</exception>
	/// <exception cref="ShouldAssertException">Thrown when contract validation fails</exception>
	/// <example>
	/// <code>
	/// .Then()
	///     .ValidateContract&lt;Product&gt;()
	///     .AssertStatusCode(200);
	/// </code>
	/// </example>
	public HttpValidationBuilder ValidateContract<T>()
	{
		// Delegate to XUnitAssured.Http extension method
		return this.ValidateContract<HttpValidationBuilder, T>(Result);
	}

	/// <summary>
	/// Asserts a value extracted from the JSON response using a JSON path.
	/// Uses Shouldly for fluent assertions with clear error messages.
	/// Delegates to ValidationBuilderExtensions.AssertJsonPath from XUnitAssured.Http.
	/// </summary>
	/// <typeparam name="T">The expected type of the value (e.g., int, string, decimal)</typeparam>
	/// <param name="jsonPath">JSON path expression (e.g., "$.id", "$.items[0].price")</param>
	/// <param name="assertion">Action containing Shouldly assertions on the extracted value</param>
	/// <returns>The same HTTP validation builder for method chaining</returns>
	/// <example>
	/// <code>
	/// .Then()
	///     .AssertJsonPath&lt;int&gt;("$.id", id => id.ShouldBe(1))
	///     .AssertJsonPath&lt;string&gt;("$.name", name => name.ShouldNotBeNullOrEmpty())
	///     .AssertJsonPath&lt;decimal&gt;("$.price", price => 
	///     {
	///         price.ShouldBeGreaterThan(0);
	///         price.ShouldBeLessThan(10000);
	///     });
	/// </code>
	/// </example>
	public HttpValidationBuilder AssertJsonPath<T>(string jsonPath, Action<T> assertion)
	{
		// Delegate to XUnitAssured.Http extension method
		return this.AssertJsonPath<HttpValidationBuilder, T>(Result, jsonPath, assertion);
	}

	/// <summary>
	/// Legacy version of AssertJsonPath using Func&lt;T, bool&gt; for backward compatibility.
	/// Consider using the Action&lt;T&gt; overload with Shouldly assertions instead for clearer error messages.
	/// Delegates to ValidationBuilderExtensions.AssertJsonPath from XUnitAssured.Http.
	/// </summary>
	/// <typeparam name="T">The expected type of the value</typeparam>
	/// <param name="jsonPath">JSON path expression</param>
	/// <param name="predicate">Predicate function that returns true if validation passes</param>
	/// <param name="failureMessage">Custom failure message</param>
	/// <returns>The same HTTP validation builder for method chaining</returns>
	public HttpValidationBuilder AssertJsonPath<T>(string jsonPath, Func<T, bool> predicate, string? failureMessage = null)
	{
		// Delegate to XUnitAssured.Http extension method
		return this.AssertJsonPath<HttpValidationBuilder, T>(Result, jsonPath, predicate, failureMessage);
	}

	/// <summary>
	/// Extracts a value from the JSON response using a simplified JSON path.
	/// Supports: $.propertyName, $.property.nested, $.array[0]
	/// Delegates to HttpStepResultExtensions.JsonPath from XUnitAssured.Http.
	/// </summary>
	/// <typeparam name="T">The expected type of the value</typeparam>
	/// <param name="path">JSON path expression (e.g., "$.id", "$.items[0].price")</param>
	/// <returns>The extracted value converted to type T</returns>
	/// <exception cref="InvalidOperationException">Thrown when response body is empty</exception>
	/// <exception cref="System.Collections.Generic.KeyNotFoundException">Thrown when the JSON path doesn't exist</exception>
	/// <example>
	/// <code>
	/// var productId = builder.JsonPath&lt;int&gt;("$.id");
	/// var productName = builder.JsonPath&lt;string&gt;("$.name");
	/// var firstItemPrice = builder.JsonPath&lt;decimal&gt;("$.items[0].price");
	/// </code>
	/// </example>
	public T JsonPath<T>(string path)
	{
		// Delegate to XUnitAssured.Http extension method
		return Result.JsonPath<T>(path);
	}

	/// <summary>
	/// Gets the HTTP step result for additional custom assertions.
	/// Use this when you need direct access to the result beyond the fluent API.
	/// </summary>
	/// <returns>The HTTP step result</returns>
	public new HttpStepResult GetResult()
	{
		return Result;
	}
}
