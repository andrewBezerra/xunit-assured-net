using System;
using System.Collections.Concurrent;
using System.Linq;
using NJsonSchema;
using Shouldly;
using XUnitAssured.Http.Results;

namespace XUnitAssured.Http.Extensions;

/// <summary>
/// Extension methods for ValidationBuilder to support contract validation and JSON path assertions.
/// These extensions integrate NJsonSchema for schema validation and Shouldly for fluent assertions.
/// </summary>
public static class ValidationBuilderExtensions
{
	// Cache de schemas para performance (evita regeneração)
	private static readonly ConcurrentDictionary<Type, JsonSchema> SchemaCache = new();

	/// <summary>
	/// Validates that the JSON response matches the schema of the specified type T.
	/// This performs complete contract validation and detects breaking changes automatically.
	/// Uses a cached schema for performance.
	/// </summary>
	/// <typeparam name="T">The type to validate against (e.g., Product, User)</typeparam>
	/// <param name="builder">The validation builder instance</param>
	/// <param name="result">The HTTP step result to validate</param>
	/// <returns>The same validation builder for method chaining</returns>
	/// <example>
	/// <code>
	/// .Then()
	///     .ValidateContract&lt;Product&gt;() // Validates entire JSON structure
	///     .AssertStatusCode(200);
	/// </code>
	/// </example>
	/// <exception cref="InvalidOperationException">Thrown when response body is empty</exception>
	public static TBuilder ValidateContract<TBuilder, T>(this TBuilder builder, HttpStepResult result)
		where TBuilder : class
	{
		var schema = GetOrAddSchema(typeof(T));
		var responseBody = result.ResponseBody?.ToString() ?? string.Empty;

		if (string.IsNullOrWhiteSpace(responseBody))
			throw new InvalidOperationException("Response body is empty - cannot validate contract");

		var errors = schema.Validate(responseBody);
		
		errors.ShouldBeEmpty(
			$"Contract validation failed for type {typeof(T).Name}:\n" +
			string.Join("\n", errors.Select(e => 
				$"  [{e.Kind}] {e.Path}: {e.Property} - {e.Schema?.Title ?? "validation error"}"
			)));

		return builder;
	}

	/// <summary>
	/// Asserts a value extracted from the JSON response using a JSON path.
	/// Uses Shouldly for fluent assertions with clear error messages.
	/// </summary>
	/// <typeparam name="TBuilder">The builder type for method chaining</typeparam>
	/// <typeparam name="T">The expected type of the value (e.g., int, string, decimal)</typeparam>
	/// <param name="builder">The validation builder instance</param>
	/// <param name="result">The HTTP step result containing the JSON</param>
	/// <param name="jsonPath">JSON path expression (e.g., "$.id", "$.items[0].price")</param>
	/// <param name="assertion">Action containing Shouldly assertions on the extracted value</param>
	/// <returns>The same validation builder for method chaining</returns>
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
	public static TBuilder AssertJsonPath<TBuilder, T>(
		this TBuilder builder, 
		HttpStepResult result, 
		string jsonPath, 
		Action<T> assertion)
		where TBuilder : class
	{
		var value = result.JsonPath<T>(jsonPath);
		assertion(value);
		return builder;
	}

	/// <summary>
	/// Legacy version of AssertJsonPath using Func&lt;T, bool&gt; for backward compatibility.
	/// Consider using the Action&lt;T&gt; overload with Shouldly assertions instead for clearer error messages.
	/// </summary>
	/// <typeparam name="TBuilder">The builder type for method chaining</typeparam>
	/// <typeparam name="T">The expected type of the value</typeparam>
	/// <param name="builder">The validation builder instance</param>
	/// <param name="result">The HTTP step result containing the JSON</param>
	/// <param name="jsonPath">JSON path expression</param>
	/// <param name="predicate">Predicate function that returns true if validation passes</param>
	/// <param name="failureMessage">Custom failure message</param>
	/// <returns>The same validation builder for method chaining</returns>
	public static TBuilder AssertJsonPath<TBuilder, T>(
		this TBuilder builder, 
		HttpStepResult result, 
		string jsonPath, 
		Func<T, bool> predicate, 
		string? failureMessage = null)
		where TBuilder : class
	{
		var value = result.JsonPath<T>(jsonPath);
		var predicateResult = predicate(value);
		predicateResult.ShouldBeTrue(failureMessage ?? $"JSON path assertion failed for path: {jsonPath}");
		return builder;
	}

	/// <summary>
	/// Gets or adds a cached JSON schema for the specified type.
	/// This improves performance by avoiding repeated schema generation.
	/// </summary>
	/// <param name="type">The type to generate schema for</param>
	/// <returns>The JSON schema for the type</returns>
	private static JsonSchema GetOrAddSchema(Type type)
	{
		return SchemaCache.GetOrAdd(type, t => JsonSchema.FromType(t));
	}
}
