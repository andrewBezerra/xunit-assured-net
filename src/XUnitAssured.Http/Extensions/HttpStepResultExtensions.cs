using System;
using System.Text.Json;
using XUnitAssured.Http.Results;

namespace XUnitAssured.Http.Extensions;

/// <summary>
/// Extension methods for HttpStepResult to support JSON Path extraction.
/// Provides a simple JSON path implementation for common scenarios.
/// </summary>
public static class HttpStepResultExtensions
{
	/// <summary>
	/// Extracts a value from the JSON response using a simplified JSON path.
	/// Supports: $.propertyName, $.property.nested, $.array[0]
	/// </summary>
	/// <typeparam name="T">The expected type of the value</typeparam>
	/// <param name="result">The HTTP step result containing the JSON response</param>
	/// <param name="path">JSON path expression (e.g., "$.id", "$.items[0].price")</param>
	/// <returns>The extracted value converted to type T</returns>
	/// <example>
	/// <code>
	/// var productId = result.JsonPath&lt;int&gt;("$.id");
	/// var productName = result.JsonPath&lt;string&gt;("$.name");
	/// var firstItemPrice = result.JsonPath&lt;decimal&gt;("$.items[0].price");
	/// </code>
	/// </example>
	/// <exception cref="InvalidOperationException">Thrown when response body is empty</exception>
	/// <exception cref="KeyNotFoundException">Thrown when the JSON path doesn't exist</exception>
	public static T JsonPath<T>(this HttpStepResult result, string path)
	{
		var responseBody = result.ResponseBody?.ToString() ?? string.Empty;
		
		if (string.IsNullOrWhiteSpace(responseBody))
			throw new InvalidOperationException("Response body is empty");

		using var document = JsonDocument.Parse(responseBody);
		var root = document.RootElement;

		// Remove leading $. if present
		if (path.StartsWith("$."))
			path = path.Substring(2);

		return JsonPathNavigator.Navigate<T>(root, path);
	}
}
