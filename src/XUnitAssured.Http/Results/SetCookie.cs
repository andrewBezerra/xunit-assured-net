using System;
using System.Globalization;

namespace XUnitAssured.Http.Results;

/// <summary>
/// A cookie as a response set it: the <c>Set-Cookie</c> header, read into its attributes.
/// </summary>
/// <remarks>
/// The attributes are what a session test is about — <c>HttpOnly</c> keeps the cookie away from
/// page scripts, <c>Path</c> limits which routes receive it, and an expiry in the past is how a
/// server tells the client to forget it.
/// </remarks>
public sealed record SetCookie
{
	/// <summary>The cookie name.</summary>
	public string Name { get; init; } = string.Empty;

	/// <summary>The cookie value, as sent.</summary>
	public string Value { get; init; } = string.Empty;

	/// <summary>The <c>Path</c> attribute, or null when absent.</summary>
	public string? Path { get; init; }

	/// <summary>The <c>Domain</c> attribute, or null when absent.</summary>
	public string? Domain { get; init; }

	/// <summary>The <c>Expires</c> attribute, or null when absent or unreadable.</summary>
	public DateTimeOffset? Expires { get; init; }

	/// <summary>The <c>Max-Age</c> attribute in seconds, or null when absent.</summary>
	public int? MaxAge { get; init; }

	/// <summary>Whether the <c>HttpOnly</c> attribute is present.</summary>
	public bool HttpOnly { get; init; }

	/// <summary>Whether the <c>Secure</c> attribute is present.</summary>
	public bool Secure { get; init; }

	/// <summary>The <c>SameSite</c> attribute (Strict, Lax, None), or null when absent.</summary>
	public string? SameSite { get; init; }

	/// <summary>
	/// Whether this <c>Set-Cookie</c> tells the client to delete the cookie: a <c>Max-Age</c> of
	/// zero or less, or an <c>Expires</c> in the past.
	/// </summary>
	public bool IsExpired =>
		MaxAge is <= 0 || (MaxAge is null && Expires is { } quando && quando <= DateTimeOffset.UtcNow);

	/// <summary>
	/// Reads one <c>Set-Cookie</c> header value. Attribute names are case-insensitive, as the
	/// specification says; unknown attributes are ignored.
	/// </summary>
	/// <param name="header">The header value, e.g. "sid=abc; Path=/; HttpOnly"</param>
	/// <returns>The cookie, or null when the value has no name=value pair</returns>
	public static SetCookie? Parse(string header)
	{
		if (string.IsNullOrWhiteSpace(header))
			return null;

		var partes = header.Split(';');
		var par = partes[0];
		var igual = par.IndexOf('=');
		if (igual <= 0)
			return null;

		var cookie = new SetCookie
		{
			Name = par.Substring(0, igual).Trim(),
			Value = par.Substring(igual + 1).Trim()
		};

		for (var i = 1; i < partes.Length; i++)
		{
			var atributo = partes[i].Trim();
			if (atributo.Length == 0)
				continue;

			var sinal = atributo.IndexOf('=');
			var nome = (sinal < 0 ? atributo : atributo.Substring(0, sinal)).Trim();
			var valor = sinal < 0 ? null : atributo.Substring(sinal + 1).Trim();

			cookie = nome.ToLowerInvariant() switch
			{
				"path" => cookie with { Path = valor },
				"domain" => cookie with { Domain = valor },
				"expires" => cookie with { Expires = LerData(valor) },
				"max-age" => cookie with { MaxAge = int.TryParse(valor, NumberStyles.Integer, CultureInfo.InvariantCulture, out var segundos) ? segundos : null },
				"httponly" => cookie with { HttpOnly = true },
				"secure" => cookie with { Secure = true },
				"samesite" => cookie with { SameSite = valor },
				_ => cookie
			};
		}

		return cookie;
	}

	private static DateTimeOffset? LerData(string? valor) =>
		DateTimeOffset.TryParse(valor, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var data)
			? data
			: null;
}
