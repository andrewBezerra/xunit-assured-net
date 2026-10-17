using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.UserSecrets;

namespace XUnitAssured.Core.Configuration;

/// <summary>
/// The .NET user secrets (<c>secrets.json</c>, from <c>dotnet user-secrets</c>) of the test
/// project, for <c>${SECRET:Key}</c> in a settings file (#212).
/// </summary>
/// <remarks>
/// The id is the <c>UserSecretsId</c> of the test project's <c>.csproj</c>, which the SDK compiles
/// into the assembly as a <see cref="UserSecretsIdAttribute"/>. A run can load more than one
/// assembly with it — a <c>WebApplicationFactory</c> loads the API project, which often has its
/// own — so a <c>"userSecretsId"</c> at the root of the file chooses, and two ids with no choice
/// fail rather than read another project's secrets.
/// </remarks>
internal static class SegredosDoUsuario
{
	private static readonly ConcurrentDictionary<string, IConfiguration> PorId = new(StringComparer.OrdinalIgnoreCase);

	private static readonly JsonDocumentOptions Leitura = new()
	{
		CommentHandling = JsonCommentHandling.Skip,
		AllowTrailingCommas = true
	};

	/// <summary>
	/// A reader of the secrets this file should use, or one that finds nothing when there is no
	/// <c>UserSecretsId</c> to read from.
	/// </summary>
	internal static Func<string, string?> Para(string json)
	{
		var id = EscolherId(IdDeclarado(json), IdsCarregados());
		if (id is null)
			return _ => null;

		var segredos = PorId.GetOrAdd(id, i => new ConfigurationBuilder().AddUserSecrets(i, reloadOnChange: false).Build());
		return chave => segredos[chave];
	}

	internal static string? EscolherId(string? declarado, IEnumerable<string> carregados)
	{
		if (!string.IsNullOrWhiteSpace(declarado))
			return declarado;

		var ids = carregados.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
		return ids.Count switch
		{
			0 => null,
			1 => ids[0],
			_ => throw new InvalidOperationException(
				$"More than one loaded assembly has a UserSecretsId ({string.Join(", ", ids)}), so it is not clear "
				+ "whose secrets.json ${SECRET:...} should read. Add \"userSecretsId\": \"<the test project's id>\" "
				+ "at the root of the settings file.")
		};
	}

	private static string? IdDeclarado(string json)
	{
		try
		{
			using var documento = JsonDocument.Parse(json, Leitura);
			return documento.RootElement.ValueKind == JsonValueKind.Object
				&& documento.RootElement.TryGetProperty("userSecretsId", out var id)
				&& id.ValueKind == JsonValueKind.String
					? id.GetString()
					: null;
		}
		catch (JsonException)
		{
			// The file is parsed again by its loader, which reports the error with the file's path.
			return null;
		}
	}

	private static IEnumerable<string> IdsCarregados() =>
		AppDomain.CurrentDomain.GetAssemblies()
			.Where(assembly => !assembly.IsDynamic)
			.Select(assembly => assembly.GetCustomAttribute<UserSecretsIdAttribute>()?.UserSecretsId)
			.Where(id => !string.IsNullOrWhiteSpace(id))
			.Select(id => id!);
}
