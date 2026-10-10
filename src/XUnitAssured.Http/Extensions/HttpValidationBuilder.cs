using System;
using System.Collections.Generic;
using Shouldly;
using XUnitAssured.Core.Abstractions;
using XUnitAssured.Core.Extensions;
using XUnitAssured.Http.Results;

namespace XUnitAssured.Http.Extensions;

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
	/// Asserts that the values a path selects include <paramref name="expected"/>.
	/// </summary>
	/// <remarks>
	/// Pair it with <see cref="AssertJsonPathNotContains{T}"/>: a filter is only tested when the
	/// test also proves it left something out.
	/// </remarks>
	/// <typeparam name="T">The type of each value</typeparam>
	/// <param name="jsonPath">A path with [*], e.g. "$[*].id" or "$.items[*].id"</param>
	/// <param name="expected">The value that must be among them</param>
	/// <returns>The same HTTP validation builder for method chaining</returns>
	/// <example>
	/// <code>
	/// .Then()
	///     .AssertJsonPathContains("$[*].id", matchingId)
	///     .AssertJsonPathNotContains("$[*].id", otherCityId);
	/// </code>
	/// </example>
	public HttpValidationBuilder AssertJsonPathContains<T>(string jsonPath, T expected) =>
		this.AssertJsonPathContains<HttpValidationBuilder, T>(Result, jsonPath, expected);

	/// <summary>
	/// Asserts that the values a path selects do not include <paramref name="unexpected"/> —
	/// what a filter, a permission or an isolation rule must have left out.
	/// </summary>
	/// <typeparam name="T">The type of each value</typeparam>
	/// <param name="jsonPath">A path with [*], e.g. "$[*].id"</param>
	/// <param name="unexpected">The value that must not be among them</param>
	/// <returns>The same HTTP validation builder for method chaining</returns>
	public HttpValidationBuilder AssertJsonPathNotContains<T>(string jsonPath, T unexpected) =>
		this.AssertJsonPathNotContains<HttpValidationBuilder, T>(Result, jsonPath, unexpected);

	/// <summary>
	/// Asserts how many items there are at a path: the values a [*] selects
	/// ("$.items[*]"), or the length of the array the path points to ("$.items", "$").
	/// </summary>
	/// <param name="jsonPath">A path to an array, or a path with [*]</param>
	/// <param name="expectedCount">The expected number of items</param>
	/// <returns>The same HTTP validation builder for method chaining</returns>
	public HttpValidationBuilder AssertJsonPathCount(string jsonPath, int expectedCount) =>
		this.AssertJsonPathCount<HttpValidationBuilder>(Result, jsonPath, expectedCount);

	/// <summary>
	/// Asserts that every value a path selects satisfies <paramref name="predicate"/>.
	/// Fails when the path selects nothing, since an empty list satisfies any condition.
	/// </summary>
	/// <typeparam name="T">The type of each value</typeparam>
	/// <param name="jsonPath">A path with [*], e.g. "$.items[*].city"</param>
	/// <param name="predicate">The condition every value must satisfy</param>
	/// <param name="failureMessage">Custom failure message</param>
	/// <returns>The same HTTP validation builder for method chaining</returns>
	public HttpValidationBuilder AssertJsonPathAll<T>(string jsonPath, Func<T, bool> predicate, string? failureMessage = null) =>
		this.AssertJsonPathAll<HttpValidationBuilder, T>(Result, jsonPath, predicate, failureMessage);

	/// <summary>
	/// Extracts every value a path with [*] selects, e.g. the ids of a list.
	/// </summary>
	/// <typeparam name="T">The type of each value</typeparam>
	/// <param name="path">A path with [*], e.g. "$[*].id"</param>
	/// <returns>The selected values, in document order</returns>
	public IReadOnlyList<T> JsonPathAll<T>(string path) => Result.JsonPathAll<T>(path);

	/// <summary>
	/// Extracts a value from the JSON response using a simplified JSON path.
	/// Supports: $.propertyName, $.property.nested, $.array[0], a root array ($[0].id). For [*], use JsonPathAll.
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

	/// <summary>
	/// Captures the full HTTP step result into a local variable while continuing the fluent chain.
	/// Use this to store results for later comparison between different API calls.
	/// </summary>
	/// <param name="result">The variable that will receive the HTTP step result</param>
	/// <returns>The same HTTP validation builder for method chaining</returns>
	/// <example>
	/// <code>
	/// Given()
	///     .ApiResource("/api/products")
	///     .Post(newProduct)
	///     .When().Execute()
	///     .Then()
	///         .AssertStatusCode(201)
	///         .Extract(out var createResult);
	///
	/// // Later, use createResult to compare with another call
	/// var createdId = createResult.JsonPath&lt;int&gt;("$.id");
	/// </code>
	/// </example>
	public new HttpValidationBuilder Extract(out HttpStepResult result)
	{
		result = Result;
		return this;
	}

	/// <summary>
	/// Captures the HTTP step result via callback while continuing the fluent chain.
	/// Useful when you need to extract multiple values or perform complex capture logic.
	/// </summary>
	/// <param name="extractor">Action that receives the HTTP step result for extraction</param>
	/// <returns>The same HTTP validation builder for method chaining</returns>
	/// <example>
	/// <code>
	/// int statusCode = 0;
	/// Given()
	///     .ApiResource("/api/products/1")
	///     .Get()
	///     .When().Execute()
	///     .Then()
	///         .Extract(result => statusCode = result.StatusCode)
	///         .AssertStatusCode(200);
	/// </code>
	/// </example>
	public new HttpValidationBuilder Extract(Action<HttpStepResult> extractor)
	{
		if (extractor == null)
			throw new ArgumentNullException(nameof(extractor));

		extractor(Result);
		return this;
	}

	/// <summary>
	/// Extracts a value from the JSON response using a JSON path into a local variable,
	/// while continuing the fluent chain.
	/// Use this to capture specific response values for comparison between API calls.
	/// </summary>
	/// <typeparam name="T">The expected type of the value (e.g., int, string, decimal)</typeparam>
	/// <param name="jsonPath">JSON path expression (e.g., "$.id", "$.items[0].price")</param>
	/// <param name="value">The variable that will receive the extracted value</param>
	/// <returns>The same HTTP validation builder for method chaining</returns>
	/// <example>
	/// <code>
	/// // Create a product and capture its ID
	/// Given()
	///     .ApiResource("/api/products")
	///     .Post(newProduct)
	///     .When().Execute()
	///     .Then()
	///         .AssertStatusCode(201)
	///         .ExtractJsonPath&lt;int&gt;("$.id", out var createdId)
	///         .ExtractJsonPath&lt;string&gt;("$.name", out var createdName);
	///
	/// // Use captured values in subsequent calls
	/// Given()
	///     .ApiResource($"/api/products/{createdId}")
	///     .Get()
	///     .When().Execute()
	///     .Then()
	///         .AssertStatusCode(200)
	///         .AssertJsonPath&lt;string&gt;("$.name", name =&gt; name == createdName);
	/// </code>
	/// </example>
	public HttpValidationBuilder ExtractJsonPath<T>(string jsonPath, out T value)
	{
		value = Result.JsonPath<T>(jsonPath);
		return this;
	}
}
