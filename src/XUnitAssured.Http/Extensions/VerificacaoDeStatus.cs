using System;
using System.Linq;

using Shouldly;

using XUnitAssured.Core.Results;
using XUnitAssured.Http.Results;

namespace XUnitAssured.Http.Extensions;

/// <summary>
/// A verificação de status, a mesma no fim da cadeia (<c>AssertStatusCode</c>) e num passo do
/// meio (<c>ExpectStatusCode</c>): os dois dizem os códigos aceitos, o que veio e o corpo.
/// </summary>
internal static class VerificacaoDeStatus
{
	/// <param name="resultado">O resultado do passo, de uma requisição ou de várias ao mesmo tempo</param>
	/// <param name="aceitos">Os códigos aceitos</param>
	/// <param name="dizerOPedido">
	/// Começa a mensagem pela requisição. Num passo do meio do cenário, é o que diz qual passo falhou.
	/// </param>
	public static void Verificar(ITestStepResult resultado, int[] aceitos, bool dizerOPedido = false)
	{
		var esperado = DescreverCodigos(aceitos);

		if (resultado is ConcurrentHttpStepResult concorrente)
		{
			// Cada resposta que fugiu do esperado diz por quê; as que acertaram só ocupariam espaço.
			var fora = concorrente.Responses
				.Select((r, i) => (Resposta: r, Posicao: i + 1))
				.Where(x => !aceitos.Contains(x.Resposta.StatusCode))
				.Select(x => $"Request {x.Posicao} ({x.Resposta.StatusCode}): {x.Resposta.Explicacao()}");

			concorrente.StatusCodes.ShouldAllBe(codigo => aceitos.Contains(codigo),
				Prefixo(concorrente.Responses.FirstOrDefault(), dizerOPedido) +
				$"Expected every one of the {concorrente.Responses.Count} concurrent requests to get " +
				$"{esperado}, but they got: {string.Join(", ", concorrente.StatusCodes)}" +
				Environment.NewLine + string.Join(Environment.NewLine, fora));
			return;
		}

		if (resultado is not HttpStepResult http)
			throw new InvalidOperationException($"Expected HttpStepResult but got {resultado?.GetType().Name ?? "null"}");

		aceitos.Contains(http.StatusCode).ShouldBeTrue(
			Prefixo(http, dizerOPedido) +
			$"Expected HTTP status code {esperado} but got {http.StatusCode}. {http.Explicacao()}");
	}

	private static string Prefixo(HttpStepResult? resultado, bool dizerOPedido)
	{
		if (!dizerOPedido || resultado?.Request is not { } pedido)
			return string.Empty;

		// O caminho basta para dizer qual passo foi; o host é o mesmo em todos.
		var caminho = pedido.Url is { IsAbsoluteUri: true } url ? url.PathAndQuery : pedido.Url?.ToString();
		return $"After {pedido.Method} {caminho}: ";
	}

	// 404 / 403 or 404 / 400, 403 or 404
	private static string DescreverCodigos(int[] codigos) =>
		codigos.Length == 1
			? codigos[0].ToString()
			: $"{string.Join(", ", codigos.Take(codigos.Length - 1))} or {codigos[codigos.Length - 1]}";
}
