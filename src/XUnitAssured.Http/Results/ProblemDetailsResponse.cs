using System;
using System.Collections.Generic;
using System.Text.Json;

namespace XUnitAssured.Http.Results;

/// <summary>
/// An RFC 7807 / RFC 9457 error body: the standard fields, plus every other member as an
/// extension — which is where APIs put a machine-readable error code.
/// </summary>
public sealed record ProblemDetailsResponse
{
	/// <summary>The <c>type</c> member, or null when absent.</summary>
	public string? Type { get; init; }

	/// <summary>The <c>title</c> member, or null when absent.</summary>
	public string? Title { get; init; }

	/// <summary>The <c>status</c> member, or null when absent.</summary>
	public int? Status { get; init; }

	/// <summary>The <c>detail</c> member, or null when absent.</summary>
	public string? Detail { get; init; }

	/// <summary>The <c>instance</c> member, or null when absent.</summary>
	public string? Instance { get; init; }

	/// <summary>
	/// Every other member of the body, by name. ASP.NET Core writes extensions at the top level,
	/// so <c>code</c> or <c>errors</c> appear here.
	/// </summary>
	public IReadOnlyDictionary<string, JsonElement> Extensions { get; init; } =
		new Dictionary<string, JsonElement>();

	/// <summary>
	/// Reads an extension as the requested type, e.g. <c>p.Extension&lt;string&gt;("code")</c>.
	/// </summary>
	/// <typeparam name="T">The type to read the extension as</typeparam>
	/// <param name="name">The extension name, case-insensitive</param>
	/// <returns>The value, or default when the extension is absent</returns>
	public T? Extension<T>(string name)
	{
		foreach (var par in Extensions)
		{
			if (string.Equals(par.Key, name, StringComparison.OrdinalIgnoreCase))
				return par.Value.Deserialize<T>();
		}

		return default;
	}

	/// <summary>Reads a JSON body as Problem Details.</summary>
	/// <param name="json">The response body</param>
	/// <returns>The problem details</returns>
	/// <exception cref="InvalidOperationException">The body is empty or not a JSON object.</exception>
	public static ProblemDetailsResponse Parse(string json)
	{
		if (string.IsNullOrWhiteSpace(json))
			throw new InvalidOperationException("Response body is empty, so it has no problem details.");

		using var documento = JsonDocument.Parse(json);
		var raiz = documento.RootElement;

		if (raiz.ValueKind != JsonValueKind.Object)
			throw new InvalidOperationException(
				$"Problem details are a JSON object, but the body is {raiz.ValueKind}.");

		string? texto = null;
		int? status = null;
		string? tipo = null, titulo = null, detalhe = null, instancia = null;
		var extensoes = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);

		foreach (var membro in raiz.EnumerateObject())
		{
			texto = membro.Value.ValueKind == JsonValueKind.String ? membro.Value.GetString() : null;

			switch (membro.Name.ToLowerInvariant())
			{
				case "type": tipo = texto; break;
				case "title": titulo = texto; break;
				case "detail": detalhe = texto; break;
				case "instance": instancia = texto; break;
				case "status":
					status = membro.Value.ValueKind == JsonValueKind.Number ? membro.Value.GetInt32() : null;
					break;
				// ASP.NET Core aninha as extensões em "extensions" quando serializa por outro
				// caminho; as duas formas viram a mesma coisa aqui.
				case "extensions" when membro.Value.ValueKind == JsonValueKind.Object:
					foreach (var aninhada in membro.Value.EnumerateObject())
						extensoes[aninhada.Name] = aninhada.Value.Clone();
					break;
				default:
					extensoes[membro.Name] = membro.Value.Clone();
					break;
			}
		}

		return new ProblemDetailsResponse
		{
			Type = tipo,
			Title = titulo,
			Status = status,
			Detail = detalhe,
			Instance = instancia,
			Extensions = extensoes
		};
	}
}
