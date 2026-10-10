using XUnitAssured.Core.Extensions;
using System;
using System.Collections.Generic;
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
	/// Supports: $.propertyName, $.property.nested, $.array[0], a root array ($[0].id). For [*], use JsonPathAll.
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
	/// <exception cref="System.Collections.Generic.KeyNotFoundException">Thrown when the JSON path doesn't exist</exception>
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

	/// <summary>
	/// Extracts every value a path selects, for a path with [*] — e.g. the ids of a list.
	/// </summary>
	/// <typeparam name="T">The expected type of each value</typeparam>
	/// <param name="result">The HTTP step result containing the JSON response</param>
	/// <param name="path">JSON path with [*], e.g. "$[*].id" or "$.items[*].sku"</param>
	/// <returns>The selected values, in document order</returns>
	/// <example>
	/// <code>
	/// var ids = result.JsonPathAll&lt;string&gt;("$[*].id");          // a root array
	/// var skus = result.JsonPathAll&lt;string&gt;("$.items[*].sku");  // an array in an object
	/// </code>
	/// </example>
	/// <exception cref="InvalidOperationException">Thrown when response body is empty</exception>
	public static IReadOnlyList<T> JsonPathAll<T>(this HttpStepResult result, string path)
	{
		var responseBody = result.ResponseBody?.ToString() ?? string.Empty;

		if (string.IsNullOrWhiteSpace(responseBody))
			throw new InvalidOperationException("Response body is empty");

		using var document = JsonDocument.Parse(responseBody);
		return JsonPathNavigator.NavigateAll<T>(document.RootElement, path);
	}

	/// <summary>
	/// How many items there are at a path: the values a [*] selects, or the length of the
	/// array the path points to.
	/// </summary>
	internal static int JsonPathCount(this HttpStepResult result, string path)
	{
		if (path.Contains("[*]"))
			return result.JsonPathAll<JsonElement>(path).Count;

		var responseBody = result.ResponseBody?.ToString() ?? string.Empty;
		if (string.IsNullOrWhiteSpace(responseBody))
			throw new InvalidOperationException("Response body is empty");

		using var document = JsonDocument.Parse(responseBody);
		var alvo = JsonPathNavigator.Navigate<JsonElement>(document.RootElement, path);

		if (alvo.ValueKind != JsonValueKind.Array)
			throw new InvalidOperationException(
				$"The value at '{path}' is {alvo.ValueKind}, not an array, so it has no count. " +
				"Point the path at a list, or use [*] to count the values it selects.");

		return alvo.GetArrayLength();
	}
}
