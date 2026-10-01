using System;
using System.Text.Json;

namespace XUnitAssured.Core.Extensions;

/// <summary>
/// Percorre um caminho JSON sobre um <see cref="JsonElement"/> e devolve o valor no tipo pedido.
///
/// <para>
/// Mora no Core porque mais de um pacote de protocolo precisa dele. Antes existiam três cópias:
/// uma no Http, uma no Kafka idêntica a menos do namespace, e uma terceira no próprio Kafka com a
/// classe renomeada. Uma correção de interpretação de caminho tinha de ser aplicada três vezes, e
/// nada avisava se uma fosse esquecida.
/// </para>
///
/// <para>
/// Continua <c>internal</c>: é detalhe de implementação, e os pacotes de protocolo o alcançam por
/// <c>InternalsVisibleTo</c> em vez de virar superfície pública.
/// </para>
/// </summary>
internal static class JsonPathNavigator
{
	/// <summary>
	/// Navigates through a JSON element using a simplified JSON path.
	/// Supports: propertyName, property.nested, array[0]
	/// </summary>
	/// <typeparam name="T">The expected type of the value</typeparam>
	/// <param name="element">The JSON element to navigate</param>
	/// <param name="path">The path to navigate (without leading $.)</param>
	/// <returns>The value at the specified path, converted to type T</returns>
	public static T Navigate<T>(JsonElement element, string path)
	{
		if (string.IsNullOrEmpty(path))
		{
			// We've reached the target
			return Deserialize<T>(element);
		}

		// Handle array index: propertyName[0]
		var arrayIndexStart = path.IndexOf('[');
		if (arrayIndexStart > 0)
		{
			var propertyName = path.Substring(0, arrayIndexStart);
			var arrayIndexEnd = path.IndexOf(']', arrayIndexStart);
			var indexStr = path.Substring(arrayIndexStart + 1, arrayIndexEnd - arrayIndexStart - 1);
			var index = int.Parse(indexStr);

			var arrayElement = element.GetProperty(propertyName);
			var itemElement = arrayElement[index];

			var remainingPath = arrayIndexEnd + 1 < path.Length && path[arrayIndexEnd + 1] == '.'
				? path.Substring(arrayIndexEnd + 2)
				: string.Empty;

			return Navigate<T>(itemElement, remainingPath);
		}

		// Handle nested property: property.nested
		var dotIndex = path.IndexOf('.');
		if (dotIndex > 0)
		{
			var propertyName = path.Substring(0, dotIndex);
			var remainingPath = path.Substring(dotIndex + 1);
			var nextElement = element.GetProperty(propertyName);
			return Navigate<T>(nextElement, remainingPath);
		}

		// Simple property
		var targetElement = element.GetProperty(path);
		return Deserialize<T>(targetElement);
	}

	/// <summary>
	/// Converte o elemento no tipo pedido, tratando os tipos comuns direto por desempenho.
	/// </summary>
	private static T Deserialize<T>(JsonElement element)
	{
		// Handle common types directly for performance
		if (typeof(T) == typeof(string))
			return (T)(object)element.GetString()!;
		if (typeof(T) == typeof(int))
			return (T)(object)element.GetInt32();
		if (typeof(T) == typeof(long))
			return (T)(object)element.GetInt64();
		if (typeof(T) == typeof(bool))
			return (T)(object)element.GetBoolean();
		if (typeof(T) == typeof(decimal))
			return (T)(object)element.GetDecimal();
		if (typeof(T) == typeof(double))
			return (T)(object)element.GetDouble();
		if (typeof(T) == typeof(JsonElement))
			return (T)(object)element;

		// For complex types, deserialize
		return JsonSerializer.Deserialize<T>(element.GetRawText())!;
	}
}
