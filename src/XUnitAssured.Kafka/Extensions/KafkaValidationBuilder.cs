using System;
using System.Text;
using Confluent.Kafka;
using Shouldly;
using XUnitAssured.Core.Abstractions;
using XUnitAssured.Core.Extensions;
using XUnitAssured.Kafka.Results;

namespace XUnitAssured.Kafka.Extensions;

/// <summary>
/// Kafka-specific validation builder that extends the generic ValidationBuilder.
/// Provides convenient methods for Kafka testing with fluent assertions.
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
	public new KafkaValidationBuilder Then()
	{
		base.Then();
		return this;
	}

	/// <summary>
	/// Asserts that the Kafka operation was successful.
	/// </summary>
	public new KafkaValidationBuilder AssertSuccess()
	{
		base.AssertSuccess();
		return this;
	}

	/// <summary>
	/// Asserts that the Kafka operation failed.
	/// </summary>
	public new KafkaValidationBuilder AssertFailure()
	{
		base.AssertFailure();
		return this;
	}

	/// <summary>
	/// Asserts the topic name matches the expected value.
	/// </summary>
	public KafkaValidationBuilder AssertTopic(string expectedTopic)
	{
		Result.Topic.ShouldBe(expectedTopic,
			$"Expected topic '{expectedTopic}' but got '{Result.Topic}'");
		return this;
	}

	/// <summary>
	/// Asserts the message key using a custom assertion.
	/// </summary>
	public KafkaValidationBuilder AssertKey(Action<object?> assertion)
	{
		if (assertion == null) throw new ArgumentNullException(nameof(assertion));
		assertion(Result.Key);
		return this;
	}

	/// <summary>
	/// Asserts the message value using a custom assertion.
	/// </summary>
	public KafkaValidationBuilder AssertMessage(Action<object?> assertion)
	{
		if (assertion == null) throw new ArgumentNullException(nameof(assertion));
		assertion(Result.Message);
		return this;
	}

	/// <summary>
	/// Asserts the message value as a typed object.
	/// </summary>
	public KafkaValidationBuilder AssertMessage<T>(Action<T?> assertion)
	{
		if (assertion == null) throw new ArgumentNullException(nameof(assertion));
		var message = Result.GetMessage<T>();
		assertion(message);
		return this;
	}

	/// <summary>
	/// Asserts a value extracted from the JSON message using a JSON path.
	/// Uses Shouldly for fluent assertions with clear error messages.
	/// </summary>
	/// <typeparam name="T">The expected type of the value (e.g., int, string, decimal)</typeparam>
	/// <param name="jsonPath">JSON path expression (e.g., "$.id", "$.name")</param>
	/// <param name="assertion">Action containing Shouldly assertions on the extracted value</param>
	/// <returns>The same Kafka validation builder for method chaining</returns>
	/// <example>
	/// <code>
	/// .Then()
	///     .AssertJsonPath&lt;int&gt;("$.id", id => id.ShouldBe(1))
	///     .AssertJsonPath&lt;string&gt;("$.name", name => name.ShouldNotBeNullOrEmpty());
	/// </code>
	/// </example>
	public KafkaValidationBuilder AssertJsonPath<T>(string jsonPath, Action<T> assertion)
	{
		if (assertion == null) throw new ArgumentNullException(nameof(assertion));
		var value = Result.JsonPath<T>(jsonPath);
		assertion(value);
		return this;
	}

	/// <summary>
	/// Asserts a value extracted from the JSON message using a JSON path with a predicate.
	/// Consider using the Action overload with Shouldly assertions for clearer error messages.
	/// </summary>
	/// <typeparam name="T">The expected type of the value</typeparam>
	/// <param name="jsonPath">JSON path expression (e.g., "$.id", "$.name")</param>
	/// <param name="predicate">Predicate function that returns true if validation passes</param>
	/// <param name="failureMessage">Custom failure message</param>
	/// <returns>The same Kafka validation builder for method chaining</returns>
	/// <example>
	/// <code>
	/// .Then()
	///     .AssertJsonPath&lt;int&gt;("$.id", value =&gt; value == 1, "ID should be 1")
	///     .AssertJsonPath&lt;string&gt;("$.name", value =&gt; !string.IsNullOrEmpty(value), "Name required");
	/// </code>
	/// </example>
	public KafkaValidationBuilder AssertJsonPath<T>(string jsonPath, Func<T, bool> predicate, string? failureMessage = null)
	{
		if (predicate == null) throw new ArgumentNullException(nameof(predicate));
		var value = Result.JsonPath<T>(jsonPath);
		var predicateResult = predicate(value);
		predicateResult.ShouldBeTrue(failureMessage ?? $"JSON path assertion failed for path: {jsonPath}");
		return this;
	}

	/// <summary>
	/// Asserts that a specific header exists with the expected string value.
	/// </summary>
	public KafkaValidationBuilder AssertHeader(string key, string expectedValue)
	{
		var headerBytes = Result.GetHeaderValue(key);
		headerBytes.ShouldNotBeNull($"Header '{key}' not found");
		var actualValue = Encoding.UTF8.GetString(headerBytes);
		actualValue.ShouldBe(expectedValue,
			$"Expected header '{key}' to be '{expectedValue}' but got '{actualValue}'");
		return this;
	}

	/// <summary>
	/// Asserts that a specific header exists using a custom assertion on its byte value.
	/// </summary>
	public KafkaValidationBuilder AssertHeader(string key, Action<byte[]> assertion)
	{
		if (assertion == null) throw new ArgumentNullException(nameof(assertion));
		var headerBytes = Result.GetHeaderValue(key);
		headerBytes.ShouldNotBeNull($"Header '{key}' not found");
		assertion(headerBytes);
		return this;
	}

	/// <summary>
	/// Asserts the partition number.
	/// </summary>
	public KafkaValidationBuilder AssertPartition(int expectedPartition)
	{
		Result.Partition.ShouldBe(expectedPartition,
			$"Expected partition {expectedPartition} but got {Result.Partition}");
		return this;
	}

	/// <summary>
	/// Asserts the batch size matches the expected count.
	/// Works for both batch produce and batch consume results.
	/// </summary>
	public KafkaValidationBuilder AssertBatchCount(int expectedCount)
	{
		var batchSize = Result.GetProperty<int>("BatchSize");
		batchSize.ShouldBe(expectedCount,
			$"Expected batch size {expectedCount} but got {batchSize}");
		return this;
	}

	/// <summary>
	/// Gets the Kafka step result for additional custom assertions.
	/// </summary>
	public new KafkaStepResult GetResult()
	{
		return Result;
	}
}
