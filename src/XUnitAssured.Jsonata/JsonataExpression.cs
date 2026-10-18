using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

using Jsonata.Net.Native;
using Jsonata.Net.Native.Eval;

using J = Jsonata.Net.Native.Json;

namespace XUnitAssured.Jsonata;

/// <summary>
/// Avalia expressões JSONata sobre o JSON que um passo devolveu, com as variáveis do teste.
/// </summary>
/// <remarks>
/// <para>
/// É a camada que o teste escrito à mão e o QualityAssured usam igual: o mesmo limite de tempo, o
/// mesmo limite de recursão, as mesmas funções. Duas leituras da mesma expressão não podem dar
/// resultados diferentes por depender de quem a avaliou.
/// </para>
/// <para>
/// As funções de data aceitam o formato do .NET (<c>yyyy-MM-dd</c>), não o da documentação da
/// JSONata (<c>[Y0001]-[M01]-[D01]</c>): este é recusado com um erro que diz isso, em vez de virar
/// texto errado. Os formatos de número (<c>$formatNumber</c>, <c>$formatInteger</c>,
/// <c>$parseInteger</c>) também são os do .NET; ali os dois formatos se parecem e não há como recusar
/// um sem recusar o outro.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var total = JsonataExpression.Evaluate("$sum(body.itens.(preco * qtd))", resposta.ToJsonObject());
/// JsonataExpression.AssertTrue("status = 201 and body.cliente.id = $clienteId", resposta.ToJsonObject(),
///     new Dictionary&lt;string, JsonNode?&gt; { ["clienteId"] = clienteId });
/// </code>
/// </example>
public static class JsonataExpression
{
	/// <summary>
	/// A profundidade máxima de chamadas. A padrão da biblioteca; acima dela, uma recursão infinita
	/// estoura a pilha da thread antes de ser barrada, e um estouro de pilha derruba o processo.
	/// </summary>
	public const int MaxDepth = 250;

	private static readonly IReadOnlyList<(string Nome, MethodInfo Metodo)> NossasFuncoes = JsonataFunctions.All;

	private static readonly HashSet<string> NomesDeFuncao = new(
		typeof(BuiltinFunctions).GetMethods(BindingFlags.Public | BindingFlags.Static).Select(m => m.Name)
			.Concat(NossasFuncoes.Select(f => f.Nome)),
		StringComparer.Ordinal);

	/// <summary>
	/// Se o nome é o de uma função da JSONata ou deste pacote. Uma variável com esse nome esconderia a
	/// função: com uma variável <c>sum</c>, <c>$sum(...)</c> deixaria de somar.
	/// </summary>
	public static bool IsFunctionName(string name) => NomesDeFuncao.Contains(name);

	/// <summary>
	/// O erro de sintaxe da expressão, com a posição, ou <see langword="null"/> quando ela é válida.
	/// </summary>
	public static string? Validate(string expression)
	{
		ArgumentNullException.ThrowIfNull(expression);
		try
		{
			_ = new JsonataQuery(expression);
			return null;
		}
		catch (JsonataException ex)
		{
			return ex.Message;
		}
	}

	/// <summary>
	/// Avalia a expressão sobre a entrada, com as variáveis disponíveis como <c>$nome</c>.
	/// </summary>
	/// <param name="expression">A expressão JSONata.</param>
	/// <param name="input">O documento que a expressão lê; <see langword="null"/> é o JSON <c>null</c>.</param>
	/// <param name="variables">As variáveis do teste, pelo nome sem o <c>$</c>.</param>
	/// <param name="options">O limite de tempo; sem ele, <see cref="JsonataOptions.Default"/>.</param>
	/// <exception cref="JsonataEvaluationException">
	/// A expressão não é válida, falhou ao ser avaliada, ou passou do limite de tempo.
	/// </exception>
	/// <exception cref="ArgumentException">Uma variável tem o nome de uma função.</exception>
	public static JsonataResult Evaluate(
		string expression,
		JsonNode? input,
		IReadOnlyDictionary<string, JsonNode?>? variables = null,
		JsonataOptions? options = null)
	{
		ArgumentNullException.ThrowIfNull(expression);
		options ??= JsonataOptions.Default;

		JsonataQuery consulta;
		try
		{
			consulta = new JsonataQuery(expression);
		}
		catch (JsonataException ex)
		{
			throw new JsonataEvaluationException(expression, $"the expression is not valid JSONata: {ex.Message}", ex);
		}

		var ambiente = Ambiente(variables);
		var dados = ParaJsonata(input);

		// A biblioteca não tem cancelamento: a avaliação roda noutra thread, e quem espera desiste no
		// prazo. O .NET não aborta threads; a que estourou segue até terminar, e o resultado dela
		// é descartado.
		var avaliacao = Task.Run(() => consulta.Eval(dados, ambiente));
		bool terminou;
		try
		{
			terminou = avaliacao.Wait(options.Timeout);
		}
		catch (AggregateException ex) when (ex.InnerException is JsonataException erro)
		{
			throw new JsonataEvaluationException(expression, erro.Message, erro);
		}
		// A biblioteca também deixa escapar exceções do .NET (um cast que ela não previu): a falha é da
		// avaliação, e diz o que houve, em vez de "One or more errors occurred".
		catch (AggregateException ex) when (ex.InnerException is { } erro and not OutOfMemoryException)
		{
			throw new JsonataEvaluationException(expression, $"the evaluation failed: {erro.GetType().Name}: {erro.Message}", erro);
		}

		if (!terminou)
			throw new JsonataEvaluationException(expression,
				string.Create(System.Globalization.CultureInfo.InvariantCulture,
					$"the expression took longer than {options.Timeout.TotalSeconds:0.###} s; check for a very large range or a recursion that does not end"));

		var resultado = avaliacao.Result;
		return resultado.Type == J.JTokenType.Undefined
			? JsonataResult.Undefined
			: JsonataResult.Of(JsonNode.Parse(resultado.ToFlatString()));
	}

	/// <summary>
	/// Afirma que a expressão dá <see langword="true"/>. Indefinido, <see langword="false"/> ou qualquer
	/// outro valor falham, com o valor obtido na mensagem.
	/// </summary>
	/// <exception cref="JsonataAssertionException">A expressão não deu <see langword="true"/>.</exception>
	/// <exception cref="JsonataEvaluationException">A expressão não pôde ser avaliada.</exception>
	public static void AssertTrue(
		string expression,
		JsonNode? input,
		IReadOnlyDictionary<string, JsonNode?>? variables = null,
		JsonataOptions? options = null)
	{
		var resultado = Evaluate(expression, input, variables, options);
		if (resultado.Value is JsonValue valor && valor.GetValueKind() == JsonValueKind.True)
			return;

		throw new JsonataAssertionException(expression, resultado);
	}

	/// <summary>
	/// Como um valor entra num texto: o conteúdo de uma string, sem aspas; vazio para nulo ou
	/// indefinido; o JSON de qualquer outro valor (<c>42</c>, <c>true</c>, <c>["a","b"]</c>).
	/// </summary>
	public static string Text(JsonNode? value) => value switch
	{
		null => "",
		JsonValue v when v.GetValueKind() == JsonValueKind.String => v.GetValue<string>(),
		_ => value.ToJsonString(),
	};

	private static EvaluationEnvironment Ambiente(IReadOnlyDictionary<string, JsonNode?>? variaveis)
	{
		var ambiente = new EvaluationEnvironment { MaxDepth = MaxDepth };
		foreach (var (nome, metodo) in NossasFuncoes)
			ambiente.BindFunction(nome, metodo);

		foreach (var (nome, valor) in variaveis ?? new Dictionary<string, JsonNode?>())
		{
			if (IsFunctionName(nome))
				throw new ArgumentException($"The variable '{nome}' has the name of a JSONata function, and would hide ${nome}.", "variables");
			ambiente.BindValue(nome, ParaJsonata(valor));
		}

		return ambiente;
	}

	private static J.JToken ParaJsonata(JsonNode? valor) => J.JToken.Parse(valor?.ToJsonString() ?? "null");
}
