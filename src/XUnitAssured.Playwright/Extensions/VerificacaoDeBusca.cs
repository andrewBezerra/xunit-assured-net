using System.Linq;

using Shouldly;

using XUnitAssured.Playwright.Results;

namespace XUnitAssured.Playwright.Extensions;

/// <summary>
/// A verificação do status de uma busca feita de dentro da página, a mesma no fim da cadeia
/// (<c>AssertFetchStatus</c>) e num passo do meio (<c>ExpectFetchStatus</c>).
/// </summary>
internal static class VerificacaoDeBusca
{
	public static void Verificar(PageFetch busca, int[] aceitos)
	{
		var esperado = aceitos.Length == 1
			? aceitos[0].ToString()
			: $"{string.Join(", ", aceitos.Take(aceitos.Length - 1))} or {aceitos[aceitos.Length - 1]}";

		aceitos.Contains(busca.Status).ShouldBeTrue(
			$"Expected {busca.Method} {busca.Url} from the page to answer {esperado}, but it answered {busca.Status}. Body: {busca.Body}");
	}
}
