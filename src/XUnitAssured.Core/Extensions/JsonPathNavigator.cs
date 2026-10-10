using System;
using System.Collections.Generic;
using System.Linq;
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
	/// Supports: propertyName, property.nested, array[0], a root array ($[0], $[0].id).
	/// </summary>
	/// <typeparam name="T">The expected type of the value</typeparam>
	/// <param name="element">The JSON element to navigate</param>
	/// <param name="path">The path to navigate, with or without the leading $ / $.</param>
	/// <returns>The value at the specified path, converted to type T</returns>
	/// <exception cref="InvalidOperationException">The path has [*], which selects many values.</exception>
	public static T Navigate<T>(JsonElement element, string path)
	{
		var alvo = element;

		foreach (var segmento in Segmentos(path))
		{
			if (segmento.Curinga)
				throw new InvalidOperationException(
					$"The path '{path}' has [*], which selects many values. Read them with JsonPathAll, " +
					"or assert them with AssertJsonPathContains, AssertJsonPathCount or AssertJsonPathAll.");

			alvo = Avancar(alvo, segmento);
		}

		return Deserialize<T>(alvo);
	}

	/// <summary>
	/// Navigates a path that may contain [*] and returns every value it selects, in document
	/// order. Without [*] it returns the single value at the path.
	/// </summary>
	/// <typeparam name="T">The expected type of each value</typeparam>
	/// <param name="element">The JSON element to navigate</param>
	/// <param name="path">The path, e.g. $[*].id or $.items[*].sku</param>
	/// <returns>The selected values, converted to type T</returns>
	public static IReadOnlyList<T> NavigateAll<T>(JsonElement element, string path)
	{
		var atuais = new List<JsonElement> { element };

		foreach (var segmento in Segmentos(path))
		{
			atuais = segmento.Curinga
				? atuais.SelectMany(e => ItensDe(e, path)).ToList()
				: atuais.Select(e => Avancar(e, segmento)).ToList();
		}

		return atuais.Select(Deserialize<T>).ToList();
	}

	/// <summary>
	/// Procura o caminho sem lançar quando ele falta, para quem precisa distinguir um campo
	/// presente com null de um campo ausente. Para no primeiro trecho que não existe.
	/// </summary>
	/// <param name="element">O elemento onde a procura começa</param>
	/// <param name="path">O caminho, sem [*]</param>
	/// <returns>
	/// O valor, quando achou; senão até onde chegou e o que havia lá, e se o que faltou foi só o
	/// último trecho.
	/// </returns>
	/// <exception cref="InvalidOperationException">O caminho tem [*], que seleciona vários valores.</exception>
	public static Procura Procurar(JsonElement element, string path)
	{
		var segmentos = Segmentos(path).ToList();
		var alvo = element;
		var alcancado = "$";

		for (var i = 0; i < segmentos.Count; i++)
		{
			var segmento = segmentos[i];
			if (segmento.Curinga)
				throw new InvalidOperationException(
					$"The path '{path}' has [*], which selects many values. Point it at a single value.");

			JsonElement proximo = default;
			var existe = segmento.Propriedade != null
				? alvo.ValueKind == JsonValueKind.Object && alvo.TryGetProperty(segmento.Propriedade, out proximo)
				: TentarIndice(alvo, segmento.Indice!.Value, out proximo);

			if (!existe)
				return new Procura(false, alvo.Clone(), alcancado, i == segmentos.Count - 1);

			alvo = proximo;
			alcancado += segmento.Propriedade != null ? $".{segmento.Propriedade}" : $"[{segmento.Indice}]";
		}

		return new Procura(true, alvo.Clone(), alcancado, false);
	}

	/// <summary>
	/// O que <see cref="Procurar"/> encontrou. Quando não achou, <see cref="Valor"/> é o que havia
	/// em <see cref="Alcancado"/>, o último trecho que existia.
	/// </summary>
	internal readonly record struct Procura(bool Achou, JsonElement Valor, string Alcancado, bool SoFaltouOUltimo);

	private static bool TentarIndice(JsonElement elemento, int indice, out JsonElement item)
	{
		item = default;
		if (elemento.ValueKind != JsonValueKind.Array || indice < 0 || indice >= elemento.GetArrayLength())
			return false;

		item = elemento[indice];
		return true;
	}

	/// <summary>Um passo do caminho: uma propriedade, um índice, ou o curinga [*].</summary>
	private readonly record struct Segmento(string? Propriedade, int? Indice, bool Curinga);

	private static JsonElement Avancar(JsonElement elemento, Segmento segmento) =>
		segmento.Propriedade != null
			? elemento.GetProperty(segmento.Propriedade)
			: elemento[segmento.Indice!.Value];

	private static IEnumerable<JsonElement> ItensDe(JsonElement elemento, string path)
	{
		if (elemento.ValueKind != JsonValueKind.Array)
			throw new InvalidOperationException(
				$"[*] in '{path}' expects an array, but the value there is {elemento.ValueKind}.");

		return elemento.EnumerateArray();
	}

	/// <summary>
	/// Lê o caminho em segmentos. Aceita com ou sem o prefixo ($, $.) e um índice em qualquer
	/// posição -- inclusive logo na raiz, que é a forma de um endpoint que devolve uma lista.
	/// Até a 6.1 o índice só era reconhecido depois de um nome de propriedade, e $[0] falhava.
	/// </summary>
	private static IEnumerable<Segmento> Segmentos(string path)
	{
		var resto = path?.Trim() ?? string.Empty;
		if (resto.StartsWith("$", StringComparison.Ordinal))
			resto = resto.Substring(1);

		var i = 0;
		while (i < resto.Length)
		{
			if (resto[i] == '.')
			{
				i++;
				continue;
			}

			if (resto[i] == '[')
			{
				var fim = resto.IndexOf(']', i);
				if (fim < 0)
					throw new FormatException($"The path '{path}' opens '[' without closing it.");

				var conteudo = resto.Substring(i + 1, fim - i - 1).Trim();
				if (conteudo == "*")
					yield return new Segmento(null, null, true);
				else if (int.TryParse(conteudo, out var indice))
					yield return new Segmento(null, indice, false);
				else
					throw new FormatException($"'[{conteudo}]' in '{path}' is neither an index nor [*].");

				i = fim + 1;
				continue;
			}

			var inicio = i;
			while (i < resto.Length && resto[i] != '.' && resto[i] != '[')
				i++;

			yield return new Segmento(resto.Substring(inicio, i - inicio), null, false);
		}
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
		// Clone: quem chama descarta o JsonDocument logo depois de navegar, e um JsonElement
		// preso a ele quebraria no primeiro uso. Medido: $[2] como JsonElement lançava
		// ObjectDisposedException ao ler uma propriedade.
		if (typeof(T) == typeof(JsonElement))
			return (T)(object)element.Clone();

		// For complex types, deserialize
		return JsonSerializer.Deserialize<T>(element.GetRawText())!;
	}
}
