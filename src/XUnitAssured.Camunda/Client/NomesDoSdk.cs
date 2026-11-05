using System;
using System.Linq;
using System.Reflection;
using System.Text.Json.Serialization;

namespace XUnitAssured.Camunda.Client;

/// <summary>
/// Os nomes que o Camunda e o Operate usam (<c>JOB_NO_RETRIES</c>, <c>SERVICE_TASK</c>) para os enums do SDK, nos dois
/// sentidos.
/// </summary>
/// <remarks>
/// O SDK declara o nome num <see cref="JsonPropertyNameAttribute"/> em cada membro, que o <c>JsonStringEnumConverter</c> não
/// lê: o nome do membro sai sem o sublinhado (<c>JOBNORETRIES</c>).
/// </remarks>
internal static class NomesDoSdk
{
	/// <summary>O nome do Camunda para o valor do enum.</summary>
	public static string Nome<T>(T valor) where T : struct, Enum
	{
		var membro = valor.ToString();
		return NomeDe(typeof(T).GetField(membro, BindingFlags.Public | BindingFlags.Static)) ?? membro;
	}

	/// <summary>O valor do enum que o Camunda chama assim, pelo nome dele ou pelo do membro, sem diferenciar maiúsculas.</summary>
	/// <exception cref="ArgumentException">Nenhum membro tem esse nome.</exception>
	public static T Valor<T>(string nome) where T : struct, Enum
	{
		var campo = typeof(T).GetFields(BindingFlags.Public | BindingFlags.Static).FirstOrDefault(c =>
			string.Equals(NomeDe(c), nome, StringComparison.OrdinalIgnoreCase) || string.Equals(c.Name, nome, StringComparison.OrdinalIgnoreCase));
		return campo is null
			? throw new ArgumentException($"'{nome}' is not a {typeof(T).Name} of the Camunda.", nameof(nome))
			: (T)campo.GetValue(null)!;
	}

	private static string? NomeDe(FieldInfo? campo) =>
		campo?.GetCustomAttributes(typeof(JsonPropertyNameAttribute), false).OfType<JsonPropertyNameAttribute>().FirstOrDefault()?.Name is { Length: > 0 } nome
			? nome
			: null;
}
