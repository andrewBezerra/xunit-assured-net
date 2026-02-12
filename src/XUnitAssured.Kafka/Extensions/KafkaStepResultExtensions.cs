using System;
using System.Text.Json;
using XUnitAssured.Kafka.Results;

namespace XUnitAssured.Kafka.Extensions;

/// <summary>
/// Extension methods for KafkaStepResult to support JSON Path extraction.
/// Provides a simple JSON path implementation for validating JSON message payloads.
/// </summary>
public static class KafkaStepResultExtensions
{
	/// <summary>
	/// Extracts a value from the Kafka message JSON payload using a simplified JSON path.
	/// Supports: $.propertyName, $.property.nested, $.array[0]
	/// </summary>
	/// <typeparam name="T">The expected type of the value</typeparam>
	/// <param name="result">The Kafka step result containing the message</param>
	/// <param name="path">JSON path expression (e.g., "$.id", "$.items[0].price")</param>
	/// <returns>The extracted value converted to type T</returns>
	/// <example>
	/// <code>
	/// var userId = result.JsonPath&lt;int&gt;("$.userId");
	/// var userName = result.JsonPath&lt;string&gt;("$.user.name");
	/// var firstItemPrice = result.JsonPath&lt;decimal&gt;("$.items[0].price");
	/// </code>
	/// </example>
	/// <exception cref="InvalidOperationException">Thrown when message is null or not a string</exception>
	/// <exception cref="System.Collections.Generic.KeyNotFoundException">Thrown when the JSON path doesn't exist</exception>
	public static T JsonPath<T>(this KafkaStepResult result, string path)
	{
		if (result.Message == null)
			throw new InvalidOperationException("Message is null");

		// Get the message as a JSON string
		string messageJson;
		if (result.Message is string str)
		{
			messageJson = str;
		}
		else
		{
			// If the message is already a deserialized object, serialize it back to JSON
			messageJson = JsonSerializer.Serialize(result.Message);
		}

		if (string.IsNullOrWhiteSpace(messageJson))
			throw new InvalidOperationException("Message content is empty");

		using var document = JsonDocument.Parse(messageJson);
		var root = document.RootElement;

		// Remove leading $. if present
		if (path.StartsWith("$."))
			path = path.Substring(2);

		return JsonPathNavigator.Navigate<T>(root, path);
	}
}
