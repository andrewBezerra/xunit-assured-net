using System;
using System.Globalization;

namespace XUnitAssured.Core.Results;

/// <summary>
/// Um prazo em segundos, como as mensagens dos pacotes o escrevem: "1.5", "30".
/// </summary>
/// <remarks>
/// As mensagens são em inglês, então o número também: com a cultura da máquina, o mesmo prazo
/// saía "1,5s" em pt-BR e "1.5s" no CI, e o Kafka escrevia todos os dígitos (#166).
/// </remarks>
internal static class Segundos
{
	internal static string De(TimeSpan prazo) => prazo.TotalSeconds.ToString("0.##", CultureInfo.InvariantCulture);
}
