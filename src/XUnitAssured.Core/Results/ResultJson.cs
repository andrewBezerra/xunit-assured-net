using System.Text.Json;
using System.Text.Json.Nodes;

namespace XUnitAssured.Core.Results;

/// <summary>
/// Como o resultado de um passo vira JSON: o que os <c>ToJsonObject()</c> dos protocolos dividem,
/// para o corpo de uma resposta HTTP e o de uma mensagem virarem o mesmo tipo de valor.
/// </summary>
public static class ResultJson
{
	/// <summary>
	/// O corpo como valor JSON: o JSON dele quando ele é JSON, o texto quando não é, e
	/// <see langword="null"/> quando não há corpo.
	/// </summary>
	public static JsonNode? Body(string? text)
	{
		if (string.IsNullOrEmpty(text))
			return null;

		try
		{
			return JsonNode.Parse(text);
		}
		catch (JsonException)
		{
			return JsonValue.Create(text);
		}
	}
}
