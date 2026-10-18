using System;

namespace XUnitAssured.Jsonata;

/// <summary>
/// A expressão não pôde ser avaliada: sintaxe, erro na avaliação, limite de tempo ou valor ausente.
/// </summary>
public sealed class JsonataEvaluationException : Exception
{
	/// <summary>Cria o erro com a expressão e o motivo.</summary>
	public JsonataEvaluationException(string? expression, string reason, Exception? inner = null)
		: base(expression is null ? reason : $"{reason}. Expression: {expression}", inner)
	{
		Expression = expression;
		Reason = reason;
	}

	/// <summary>A expressão que falhou, quando se sabe qual.</summary>
	public string? Expression { get; }

	/// <summary>O motivo, sem a expressão.</summary>
	public string Reason { get; }
}

/// <summary>
/// Uma asserção por expressão não deu <see langword="true"/>.
/// </summary>
public sealed class JsonataAssertionException : Exception
{
	/// <summary>Cria a falha com a expressão e o que ela deu.</summary>
	public JsonataAssertionException(string expression, JsonataResult actual)
		: base($"expected {expression} to be true, but it was {actual}")
	{
		Expression = expression;
		Actual = actual;
	}

	/// <summary>A expressão da asserção.</summary>
	public string Expression { get; }

	/// <summary>O que ela deu.</summary>
	public JsonataResult Actual { get; }
}
