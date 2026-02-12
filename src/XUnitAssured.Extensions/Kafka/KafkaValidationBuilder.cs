using System;
using Shouldly;
using XUnitAssured.Core.Abstractions;
using XUnitAssured.Extensions.Core;
using XUnitAssured.Kafka.Extensions;
using XUnitAssured.Kafka.Results;

namespace XUnitAssured.Extensions.Kafka;

/// <summary>
/// Kafka-specific validation builder that extends the generic ValidationBuilder.
/// Provides convenient methods for Kafka message testing with fluent assertions.
/// </summary>
public class KafkaValidationBuilder : ValidationBuilder<KafkaStepResult>
{
	/// <summary>
	/// Initializes a new instance of the KafkaValidationBuilder.
	/// </summary>
	/// <param name="scenario">The test scenario containing the executed Kafka step</param>
	public KafkaValidationBuilder(ITestScenario scenario) : base(scenario)
	{
	}

	/// <summary>
	/// Marks the transition from "When" (action) to "Then" (assertions) in BDD-style tests.
	/// Returns KafkaValidationBuilder to maintain fluent chain type.
	/// </summary>
	/// <returns>The same Kafka validation builder for method chaining</returns>
	public new KafkaValidationBuilder Then()
	{
		base.Then();
		return this;
	}

	/// <summary>
	/// Asserts that the Kafka message is from the expected topic.
	/// </summary>
	/// <param name="expectedTopic">The expected Kafka topic name</param>
	/// <returns>The same Kafka validation builder for method chaining</returns>
	/// <exception cref="ShouldAssertException">Thrown when topic doesn't match</exception>
	/// <example>
	/// <code>
	/// .Then()
	///     .AssertTopic("user-events")
	///     .AssertPartition(0);
	/// </code>
	/// </example>
	public KafkaValidationBuilder AssertTopic(string expectedTopic)
	{
		Result.Topic.ShouldBe(expectedTopic,
			$"Expected Kafka topic '{expectedTopic}' but got '{Result.Topic}'");
		return this;
	}

	/// <summary>
	/// Asserts that the message was produced/consumed from the expected partition.
	/// </summary>
	/// <param name="expectedPartition">The expected partition number</param>
	/// <returns>The same Kafka validation builder for method chaining</returns>
	/// <exception cref="ShouldAssertException">Thrown when partition doesn't match</exception>
	/// <example>
	/// <code>
	/// .Then()
	///     .AssertPartition(0)
	///     .AssertOffset(offset => offset > 0);
	/// </code>
	/// </example>
	public KafkaValidationBuilder AssertPartition(int expectedPartition)
	{
		Result.Partition.ShouldNotBeNull("Partition information is not available in the result");
		Result.Partition.ShouldBe(expectedPartition,
			$"Expected partition {expectedPartition} but got {Result.Partition}");
		return this;
	}

	/// <summary>
	/// Asserts that the message offset meets the specified condition.
	/// </summary>
	/// <param name="assertion">Action containing Shouldly assertions on the offset value</param>
	/// <returns>The same Kafka validation builder for method chaining</returns>
	/// <exception cref="ShouldAssertException">Thrown when offset assertion fails</exception>
	/// <example>
	/// <code>
	/// .Then()
	///     .AssertOffset(offset => offset.ShouldBeGreaterThan(0))
	///     .AssertOffset(offset => offset.ShouldBeLessThan(1000000));
	/// </code>
	/// </example>
	public KafkaValidationBuilder AssertOffset(Action<long> assertion)
	{
		if (assertion == null)
			throw new ArgumentNullException(nameof(assertion));

		Result.Offset.ShouldNotBeNull("Offset information is not available in the result");
		assertion(Result.Offset.Value);
		return this;
	}

	/// <summary>
	/// Asserts that the message has a specific header with the expected value.
	/// </summary>
	/// <param name="headerKey">The header key to check</param>
	/// <param name="assertion">Action containing Shouldly assertions on the header value</param>
	/// <returns>The same Kafka validation builder for method chaining</returns>
	/// <exception cref="ShouldAssertException">Thrown when header assertion fails</exception>
	/// <example>
	/// <code>
	/// .Then()
	///     .AssertHeader("correlation-id", value => value.ShouldNotBeNull())
	///     .AssertHeader("content-type", value => 
	///         System.Text.Encoding.UTF8.GetString(value).ShouldBe("application/json"));
	/// </code>
	/// </example>
	public KafkaValidationBuilder AssertHeader(string headerKey, Action<byte[]?> assertion)
	{
		if (string.IsNullOrWhiteSpace(headerKey))
			throw new ArgumentException("Header key cannot be null or empty", nameof(headerKey));
		if (assertion == null)
			throw new ArgumentNullException(nameof(assertion));

		var headerValue = Result.GetHeaderValue(headerKey);
		assertion(headerValue);
		return this;
	}

	/// <summary>
	/// Asserts a value extracted from the JSON message payload using a JSON path.
	/// Uses Shouldly for fluent assertions with clear error messages.
	/// </summary>
	/// <typeparam name="T">The expected type of the value (e.g., int, string, decimal)</typeparam>
	/// <param name="jsonPath">JSON path expression (e.g., "$.id", "$.items[0].price")</param>
	/// <param name="assertion">Action containing Shouldly assertions on the extracted value</param>
	/// <returns>The same Kafka validation builder for method chaining</returns>
	/// <exception cref="InvalidOperationException">Thrown when message is null or empty</exception>
	/// <exception cref="System.Collections.Generic.KeyNotFoundException">Thrown when the JSON path doesn't exist</exception>
	/// <example>
	/// <code>
	/// .Then()
	///     .AssertJsonPath&lt;int&gt;("$.userId", id => id.ShouldBeGreaterThan(0))
	///     .AssertJsonPath&lt;string&gt;("$.user.name", name => name.ShouldNotBeNullOrEmpty())
	///     .AssertJsonPath&lt;decimal&gt;("$.amount", amount => 
	///     {
	///         amount.ShouldBeGreaterThan(0);
	///         amount.ShouldBeLessThan(10000);
	///     });
	/// </code>
	/// </example>
	public KafkaValidationBuilder AssertJsonPath<T>(string jsonPath, Action<T> assertion)
	{
		if (string.IsNullOrWhiteSpace(jsonPath))
			throw new ArgumentException("JSON path cannot be null or empty", nameof(jsonPath));
		if (assertion == null)
			throw new ArgumentNullException(nameof(assertion));

		var value = Result.JsonPath<T>(jsonPath);
		assertion(value);
		return this;
	}

	/// <summary>
	/// Alternative version of AssertJsonPath using Func&lt;T, bool&gt; predicate.
	/// Consider using the Action&lt;T&gt; overload with Shouldly assertions for clearer error messages.
	/// This overload mirrors the HTTP API pattern for consistency.
	/// </summary>
	/// <typeparam name="T">The expected type of the value</typeparam>
	/// <param name="jsonPath">JSON path expression</param>
	/// <param name="predicate">Predicate function that returns true if validation passes</param>
	/// <param name="failureMessage">Custom failure message</param>
	/// <returns>The same Kafka validation builder for method chaining</returns>
	/// <exception cref="InvalidOperationException">Thrown when message is null or empty</exception>
	/// <exception cref="System.Collections.Generic.KeyNotFoundException">Thrown when the JSON path doesn't exist</exception>
	/// <example>
	/// <code>
	/// .Then()
	///     .AssertJsonPath&lt;int&gt;("$.userId", id => id > 0, "User ID must be positive")
	///     .AssertJsonPath&lt;string&gt;("$.status", status => status == "active", "Status must be active");
	/// </code>
	/// </example>
	public KafkaValidationBuilder AssertJsonPath<T>(string jsonPath, Func<T, bool> predicate, string? failureMessage = null)
	{
		if (string.IsNullOrWhiteSpace(jsonPath))
			throw new ArgumentException("JSON path cannot be null or empty", nameof(jsonPath));
		if (predicate == null)
			throw new ArgumentNullException(nameof(predicate));

		var value = Result.JsonPath<T>(jsonPath);
		var predicateResult = predicate(value);
		predicateResult.ShouldBeTrue(failureMessage ?? $"JSON path assertion failed for path: {jsonPath}");
		return this;
	}

	/// <summary>
	/// Extracts a value from the JSON message payload using a simplified JSON path.
	/// Supports: $.propertyName, $.property.nested, $.array[0]
	/// </summary>
	/// <typeparam name="T">The expected type of the value</typeparam>
	/// <param name="path">JSON path expression (e.g., "$.id", "$.items[0].price")</param>
	/// <returns>The extracted value converted to type T</returns>
	/// <exception cref="InvalidOperationException">Thrown when message is null or empty</exception>
	/// <exception cref="System.Collections.Generic.KeyNotFoundException">Thrown when the JSON path doesn't exist</exception>
	/// <example>
	/// <code>
	/// var userId = builder.JsonPath&lt;int&gt;("$.userId");
	/// var userName = builder.JsonPath&lt;string&gt;("$.user.name");
	/// var firstItemPrice = builder.JsonPath&lt;decimal&gt;("$.items[0].price");
	/// </code>
	/// </example>
	public T JsonPath<T>(string path)
	{
		return Result.JsonPath<T>(path);
	}

	/// <summary>
	/// Gets the Kafka step result for additional custom assertions.
	/// Use this when you need direct access to the result beyond the fluent API.
	/// </summary>
	/// <returns>The Kafka step result</returns>
	public new KafkaStepResult GetResult()
	{
		return Result;
	}
}
