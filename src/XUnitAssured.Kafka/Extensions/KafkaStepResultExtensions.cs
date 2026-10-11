using System;
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
	[Obsolete("KafkaStepResult has its own JsonPath<T>(path), which a call written as result.JsonPath<T>(path) " +
		"already reaches. Call that one; this extension is removed in 7.0.")]
	public static T JsonPath<T>(this KafkaStepResult result, string path)
	{
		if (result == null)
			throw new ArgumentNullException(nameof(result));

		// O método da própria classe sempre vence a extensão, então esta só era alcançada como
		// KafkaStepResultExtensions.JsonPath(result, ...), com outra implementação e outras
		// mensagens. Agora as duas formas dão o mesmo resultado (#125).
		return result.JsonPath<T>(path);
	}
}
