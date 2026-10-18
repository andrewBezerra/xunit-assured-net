using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

using Jsonata.Net.Native;
using Jsonata.Net.Native.Eval;

using J = Jsonata.Net.Native.Json;

namespace XUnitAssured.Jsonata;

/// <summary>
/// As funções que este pacote liga em toda avaliação: as nossas, e as de data da biblioteca com o
/// formato conferido.
/// </summary>
/// <remarks>
/// Um nome ligado no ambiente da avaliação vence a função embutida de mesmo nome: é assim que
/// <c>$fromMillis</c> passa a conferir o formato antes de chamar a original.
/// </remarks>
internal static class JsonataFunctions
{
	// O formato padrão da biblioteca para $now e $fromMillis (ISO 8601 em UTC, com milissegundos).
	private const string FormatoUtc = @"yyyy-MM-dd\THH:mm:ss.fffK";

	/// <summary>Os nomes, como a expressão os chama sem o <c>$</c>, e os métodos.</summary>
	public static IReadOnlyList<(string Nome, MethodInfo Metodo)> All { get; } =
	[
		("jwt", Metodo(nameof(Jwt))),
		("fromMillis", Metodo(nameof(FromMillis))),
		("toMillis", Metodo(nameof(ToMillis))),
		("now", Metodo(nameof(Now))),
	];

	private static MethodInfo Metodo(string nome) =>
		typeof(JsonataFunctions).GetMethod(nome, BindingFlags.Public | BindingFlags.Static)
			?? throw new InvalidOperationException($"JsonataFunctions.{nome} is missing.");

	/// <summary>
	/// <c>$jwt(token)</c>: as claims do payload de um JWT, sem validar a assinatura. É para ler o que o
	/// token diz (<c>$jwt(body.access_token).sub</c>), não para confiar nele.
	/// </summary>
	public static J.JToken Jwt([PropagateUndefined] string token)
	{
		var partes = token.Split('.');
		if (partes.Length < 2 || partes[1].Length == 0)
			throw new JsonataException(JsonataErrorCode.D3110, "$jwt() expects a JWT: header.payload.signature");

		// O payload é base64url sem o preenchimento: volta ao base64 comum antes de decodificar.
		var base64 = partes[1].Replace('-', '+').Replace('_', '/');
		base64 = base64.PadRight(base64.Length + ((4 - (base64.Length % 4)) % 4), '=');
		string json;
		try
		{
			json = Encoding.UTF8.GetString(Convert.FromBase64String(base64));
		}
		catch (FormatException)
		{
			throw new JsonataException(JsonataErrorCode.D3110, "$jwt() could not decode the payload: it is not base64url");
		}

		try
		{
			return J.JToken.Parse(json);
		}
		catch (Exception ex) when (ex is not OutOfMemoryException)
		{
			throw new JsonataException(JsonataErrorCode.D3110, "$jwt() decoded the payload, but it is not JSON");
		}
	}

	/// <summary><c>$fromMillis</c> com o formato conferido.</summary>
	public static string FromMillis([PropagateUndefined] long number, [OptionalArgument(null)] string? picture, [OptionalArgument(null)] string? timezone)
	{
		ConferirFormato("fromMillis", picture);
		return BuiltinFunctions.fromMillis(number, picture ?? FormatoUtc, timezone);
	}

	/// <summary><c>$toMillis</c> com o formato conferido.</summary>
	public static long ToMillis([PropagateUndefined] string timestamp, [OptionalArgument(null)] string? picture)
	{
		ConferirFormato("toMillis", picture);
		return BuiltinFunctions.toMillis(timestamp, picture);
	}

	/// <summary><c>$now</c> com o formato conferido.</summary>
	public static string Now(JsThisArgument jsThis, [OptionalArgument(null)] string? picture, [OptionalArgument(null)] string? timezone)
	{
		ConferirFormato("now", picture);
		return BuiltinFunctions.now(jsThis, picture ?? FormatoUtc, timezone);
	}

	// O formato da documentação da JSONata marca cada parte entre colchetes ([Y0001]-[M01]-[D01]); o
	// do .NET não usa colchetes. Com ele, a biblioteca devolveria "[Y0001]-[1001]-[D01]", sem erro.
	private static void ConferirFormato(string funcao, string? picture)
	{
		if (picture is not null && picture.Contains('['))
			throw new JsonataException(JsonataErrorCode.D3110,
				$"${funcao}() got the picture '{picture}', which is the XPath format of the JSONata docs; this engine takes .NET date formats, such as 'yyyy-MM-dd' or 'dd/MM/yyyy HH:mm'");
	}
}
