using System.Text.Json.Nodes;

namespace XUnitAssured.Jsonata;

/// <summary>
/// O resultado de uma expressão: um valor JSON, ou indefinido.
/// </summary>
/// <remarks>
/// Indefinido e <c>null</c> são coisas diferentes na JSONata: <c>body.cupom</c> num corpo sem
/// <c>cupom</c> é indefinido; num corpo com <c>"cupom": null</c>, é <c>null</c>. Um teste que lê um
/// campo que não veio precisa saber disso.
/// </remarks>
public sealed class JsonataResult
{
	private JsonataResult(bool isUndefined, JsonNode? value)
	{
		IsUndefined = isUndefined;
		Value = value;
	}

	/// <summary>O resultado indefinido: a expressão não achou nada.</summary>
	public static JsonataResult Undefined { get; } = new(true, null);

	/// <summary>Um resultado com valor; <see langword="null"/> é o JSON <c>null</c>.</summary>
	public static JsonataResult Of(JsonNode? value) => new(false, value);

	/// <summary>Se a expressão não achou nada.</summary>
	public bool IsUndefined { get; }

	/// <summary>O valor; <see langword="null"/> quando é o JSON <c>null</c> ou indefinido.</summary>
	public JsonNode? Value { get; }

	/// <summary>
	/// O valor, exigindo que exista: é o que uma variável de saída pede.
	/// </summary>
	/// <param name="what">O que o valor é, para a mensagem (o nome da variável).</param>
	/// <exception cref="JsonataEvaluationException">O resultado é indefinido.</exception>
	public JsonNode? Required(string what) => IsUndefined
		? throw new JsonataEvaluationException(null, $"the expression for '{what}' found no value")
		: Value;

	/// <summary>O valor como JSON, ou <c>undefined</c>.</summary>
	public override string ToString() => IsUndefined ? "undefined" : Value?.ToJsonString() ?? "null";
}
