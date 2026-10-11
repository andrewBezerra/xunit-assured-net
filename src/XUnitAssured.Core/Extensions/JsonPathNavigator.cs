using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
	/// Supports: propertyName, property.nested, array[0], a root array ($[0], $[0].id), and a
	/// filter that finds one item of a list by a field ($.members[?(@.id=='42')].name).
	/// </summary>
	/// <typeparam name="T">The expected type of the value</typeparam>
	/// <param name="element">The JSON element to navigate</param>
	/// <param name="path">The path to navigate, with or without the leading $ / $.</param>
	/// <returns>The value at the specified path, converted to type T</returns>
	/// <exception cref="InvalidOperationException">
	/// The path has [*], which selects many values, or a filter that does not match exactly one item.
	/// </exception>
	public static T Navigate<T>(JsonElement element, string path)
	{
		var alvo = element;

		foreach (var segmento in Segmentos(path))
		{
			if (segmento.Curinga)
				throw new InvalidOperationException(
					$"The path '{path}' has [*], which selects many values. Read them with JsonPathAll, " +
					"or assert them with AssertJsonPathContains, AssertJsonPathCount or AssertJsonPathAll.");

			alvo = segmento.Filtro is { } filtro
				? UnicoItem(alvo, filtro, path)
				: Avancar(alvo, segmento);
		}

		return Deserialize<T>(alvo);
	}

	/// <summary>
	/// Navigates a path that may contain [*] or a filter and returns every value it selects, in
	/// document order. Without them it returns the single value at the path.
	/// </summary>
	/// <typeparam name="T">The expected type of each value</typeparam>
	/// <param name="element">The JSON element to navigate</param>
	/// <param name="path">The path, e.g. $[*].id, $.items[*].sku or $.items[?(@.active==true)].sku</param>
	/// <returns>The selected values, converted to type T</returns>
	public static IReadOnlyList<T> NavigateAll<T>(JsonElement element, string path)
	{
		var atuais = new List<JsonElement> { element };

		foreach (var segmento in Segmentos(path))
		{
			atuais = segmento.Curinga ? atuais.SelectMany(e => ItensDe(e, "[*]", path)).ToList()
				: segmento.Filtro is { } filtro ? atuais.SelectMany(e => ItensDe(e, filtro.Texto, path).Where(filtro.Aceita)).ToList()
				: atuais.Select(e => Avancar(e, segmento)).ToList();
		}

		return atuais.Select(Deserialize<T>).ToList();
	}

	/// <summary>
	/// Procura o caminho sem lançar quando ele falta, para quem precisa distinguir um campo
	/// presente com null de um campo ausente. Para no primeiro trecho que não existe.
	/// </summary>
	/// <param name="element">O elemento onde a procura começa</param>
	/// <param name="path">
	/// O caminho, sem [*]. Um filtro ali acha no máximo um item; quando não acha nenhum, o caminho
	/// para nele, como numa propriedade que falta.
	/// </param>
	/// <returns>
	/// O valor, quando achou; senão até onde chegou e o que havia lá, e se o que faltou foi só o
	/// último trecho.
	/// </returns>
	/// <exception cref="InvalidOperationException">O caminho tem [*], ou um filtro que achou mais de um item.</exception>
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
			var existe = segmento.Propriedade != null ? alvo.ValueKind == JsonValueKind.Object && alvo.TryGetProperty(segmento.Propriedade, out proximo)
				: segmento.Filtro is { } filtro ? TentarFiltro(alvo, filtro, path, out proximo)
				: TentarIndice(alvo, segmento.Indice!.Value, out proximo);

			if (!existe)
				return new Procura(false, alvo.Clone(), alcancado, i == segmentos.Count - 1);

			alvo = proximo;
			alcancado += segmento.Propriedade != null ? $".{segmento.Propriedade}"
				: segmento.Filtro != null ? segmento.Filtro.Texto
				: $"[{segmento.Indice}]";
		}

		return new Procura(true, alvo.Clone(), alcancado, false);
	}

	/// <summary>
	/// O que <see cref="Procurar"/> encontrou. Quando não achou, <see cref="Valor"/> é o que havia
	/// em <see cref="Alcancado"/>, o último trecho que existia.
	/// </summary>
	internal readonly record struct Procura(bool Achou, JsonElement Valor, string Alcancado, bool SoFaltouOUltimo);

	/// <summary>
	/// Se a contagem de um caminho é de quantos valores ele seleciona, e não do tamanho da lista
	/// para onde aponta: quando tem [*], ou termina num filtro. Um filtro no meio só acha o item, e
	/// $.drafts[?(@.week=='x')].members conta os membros daquele rascunho.
	/// </summary>
	public static bool ContaASelecao(string path)
	{
		var segmentos = Segmentos(path).ToList();
		return segmentos.Any(s => s.Curinga) || segmentos.Count > 0 && segmentos[^1].Filtro != null;
	}

	private static bool TentarIndice(JsonElement elemento, int indice, out JsonElement item)
	{
		item = default;
		if (elemento.ValueKind != JsonValueKind.Array || indice < 0 || indice >= elemento.GetArrayLength())
			return false;

		item = elemento[indice];
		return true;
	}

	private static bool TentarFiltro(JsonElement elemento, Filtro filtro, string path, out JsonElement item)
	{
		item = default;
		if (elemento.ValueKind != JsonValueKind.Array)
			return false;

		var achados = elemento.EnumerateArray().Where(filtro.Aceita).Take(2).ToList();
		if (achados.Count == 0)
			return false;

		// Mais de um: UnicoItem lança dizendo quantos achou entre quantos havia.
		item = achados.Count == 1 ? achados[0] : UnicoItem(elemento, filtro, path);
		return true;
	}

	/// <summary>
	/// O item que o filtro acha, onde só se lê um valor. Nenhum ou mais de um é erro, dizendo
	/// quantos achou entre quantos havia.
	/// </summary>
	private static JsonElement UnicoItem(JsonElement elemento, Filtro filtro, string path)
	{
		var itens = ItensDe(elemento, filtro.Texto, path).ToList();
		var achados = itens.Where(filtro.Aceita).ToList();

		return achados.Count switch
		{
			1 => achados[0],
			0 => throw new InvalidOperationException(
				$"The filter {filtro.Texto} in '{path}' matched none of the {itens.Count} item(s) there."),
			_ => throw new InvalidOperationException(
				$"The filter {filtro.Texto} in '{path}' matched {achados.Count} of the {itens.Count} item(s), " +
				"and only one value can be read here. Narrow the filter, or read every match with JsonPathAll.")
		};
	}

	/// <summary>Um passo do caminho: uma propriedade, um índice, o curinga [*], ou um filtro.</summary>
	private readonly record struct Segmento(string? Propriedade, int? Indice, bool Curinga, Filtro? Filtro = null);

	private static JsonElement Avancar(JsonElement elemento, Segmento segmento) =>
		segmento.Propriedade != null
			? elemento.GetProperty(segmento.Propriedade)
			: elemento[segmento.Indice!.Value];

	private static IEnumerable<JsonElement> ItensDe(JsonElement elemento, string segmento, string path)
	{
		if (elemento.ValueKind != JsonValueKind.Array)
			throw new InvalidOperationException(
				$"{segmento} in '{path}' expects an array, but the value there is {elemento.ValueKind}.");

		return elemento.EnumerateArray();
	}

	/// <summary>
	/// Lê o caminho em segmentos. Aceita com ou sem o prefixo ($, $.) e um índice em qualquer
	/// posição -- inclusive logo na raiz, que é a forma de um endpoint que devolve uma lista.
	/// Até a 6.1 o índice só era reconhecido depois de um nome de propriedade, e $[0] falhava.
	/// O ']' que fecha um filtro é procurado fora das aspas: [?(@.nome=='a]b')] é um filtro só.
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
				var fim = FimDoColchete(resto, i);
				if (fim < 0)
					throw new FormatException($"The path '{path}' opens '[' without closing it.");

				var conteudo = resto.Substring(i + 1, fim - i - 1).Trim();
				if (conteudo == "*")
					yield return new Segmento(null, null, true);
				else if (int.TryParse(conteudo, out var indice))
					yield return new Segmento(null, indice, false);
				else if (conteudo.StartsWith("?", StringComparison.Ordinal))
					yield return new Segmento(null, null, false, Filtro.Ler(conteudo, path!));
				else
					throw new FormatException($"'[{conteudo}]' in '{path}' is neither an index, [*] nor a filter [?(...)].");

				i = fim + 1;
				continue;
			}

			var inicio = i;
			while (i < resto.Length && resto[i] != '.' && resto[i] != '[')
				i++;

			yield return new Segmento(resto.Substring(inicio, i - inicio), null, false);
		}
	}

	private static int FimDoColchete(string texto, int abre)
	{
		char? aspas = null;
		for (var i = abre + 1; i < texto.Length; i++)
		{
			var c = texto[i];
			if (aspas != null)
			{
				if (c == '\\')
					i++;
				else if (c == aspas)
					aspas = null;
			}
			else if (c == '\'' || c == '"')
				aspas = c;
			else if (c == ']')
				return i;
		}

		return -1;
	}

	/// <summary>
	/// Um filtro [?(@.campo=='valor')]: só igualdade (== e !=) contra um texto, número, true,
	/// false ou null. @ sozinho compara o próprio item, para uma lista de textos.
	/// </summary>
	/// <remarks>
	/// Como na RFC 9535, != é o contrário de ==: um item sem o campo entra em != e não em ==.
	/// Texto e número não se misturam: '1' não é igual a 1.
	/// </remarks>
	private sealed record Filtro(string Texto, IReadOnlyList<string> Campo, bool Igual, JsonElement Literal)
	{
		public bool Aceita(JsonElement item)
		{
			var valor = item;
			foreach (var nome in Campo)
			{
				if (valor.ValueKind != JsonValueKind.Object || !valor.TryGetProperty(nome, out valor))
					return !Igual;
			}

			return Iguais(valor, Literal) == Igual;
		}

		private static bool Iguais(JsonElement valor, JsonElement literal)
		{
			if (valor.ValueKind != literal.ValueKind)
				return false;

			return valor.ValueKind switch
			{
				JsonValueKind.String => valor.GetString() == literal.GetString(),
				JsonValueKind.Number => valor.TryGetDecimal(out var a) && literal.TryGetDecimal(out var b)
					? a == b
					: valor.GetDouble().Equals(literal.GetDouble()),
				JsonValueKind.True or JsonValueKind.False or JsonValueKind.Null => true,
				_ => false
			};
		}

		public static Filtro Ler(string conteudo, string path)
		{
			var texto = $"[{conteudo}]";
			var expressao = conteudo.Substring(1).Trim();
			if (expressao.StartsWith("(", StringComparison.Ordinal) && expressao.EndsWith(")", StringComparison.Ordinal))
				expressao = expressao.Substring(1, expressao.Length - 2).Trim();

			var (posicao, igual) = Operador(expressao);
			var lado = posicao >= 0 ? expressao.Substring(0, posicao).Trim() : string.Empty;

			if (posicao < 0 || !(lado == "@" || lado.StartsWith("@.", StringComparison.Ordinal)))
				throw new FormatException(
					$"'{texto}' in '{path}' is not a filter this library reads. It compares one field of " +
					"each item with == or !=, as in [?(@.status=='open')] or [?(@.count!=0)].");

			var campo = lado == "@" ? Array.Empty<string>() : lado.Substring(2).Split('.');
			if (campo.Any(string.IsNullOrWhiteSpace))
				throw new FormatException($"'{texto}' in '{path}' has an empty field name in '{lado}'.");

			var literal = LerLiteral(expressao.Substring(posicao + 2).Trim(), texto, path);
			return new Filtro(texto, campo, igual, literal);
		}

		// O primeiro == ou != fora das aspas: o valor comparado pode conter os dois.
		private static (int Posicao, bool Igual) Operador(string expressao)
		{
			char? aspas = null;
			for (var i = 0; i < expressao.Length - 1; i++)
			{
				var c = expressao[i];
				if (aspas != null)
				{
					if (c == '\\')
						i++;
					else if (c == aspas)
						aspas = null;
				}
				else if (c == '\'' || c == '"')
					aspas = c;
				else if ((c == '=' || c == '!') && expressao[i + 1] == '=')
					return (i, c == '=');
			}

			return (-1, false);
		}

		private static JsonElement LerLiteral(string valor, string filtro, string path)
		{
			if (valor.Length >= 2 && (valor[0] == '\'' || valor[0] == '"') && valor[^1] == valor[0])
			{
				var texto = new StringBuilder();
				for (var i = 1; i < valor.Length - 1; i++)
				{
					if (valor[i] == '\\' && i + 1 < valor.Length - 1)
						i++;
					texto.Append(valor[i]);
				}

				return JsonSerializer.SerializeToElement(texto.ToString());
			}

			try
			{
				using var documento = JsonDocument.Parse(valor);
				if (documento.RootElement.ValueKind is not (JsonValueKind.Object or JsonValueKind.Array))
					return documento.RootElement.Clone();
			}
			catch (JsonException)
			{
			}

			throw new FormatException(
				$"'{valor}' in the filter {filtro} of '{path}' is not a value. Use a text in quotes " +
				"('open'), a number, true, false or null.");
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
