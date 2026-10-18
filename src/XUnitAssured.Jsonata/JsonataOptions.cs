using System;

namespace XUnitAssured.Jsonata;

/// <summary>
/// O que limita uma avaliação.
/// </summary>
public sealed class JsonataOptions
{
	/// <summary>Cinco segundos: muito acima do que uma expressão de teste leva, abaixo de travar o teste.</summary>
	public static JsonataOptions Default { get; } = new();

	/// <summary>
	/// Quanto uma avaliação pode levar. <c>[1..10000000].($ * 2)</c> leva uns 20 s e 1,7 GB; sem o limite,
	/// o teste ficaria parado ali.
	/// </summary>
	public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(5);
}
