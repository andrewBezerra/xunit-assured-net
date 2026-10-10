using XUnitAssured.Core.Abstractions;

namespace XUnitAssured.Core.DSL;

/// <summary>
/// Diz se a cadeia pediu um passo novo -- o And() ou o On() que ainda não foi atendido.
///
/// <para>
/// Interna de propósito: é o contrato entre o TestScenario e os pacotes de protocolo, e não uma
/// API para quem escreve teste. Os invólucros tipados de cada pacote (HttpScenario,
/// BrowserScenario...) repassam a pergunta ao cenário que embrulham.
/// </para>
///
/// <para>
/// Existe porque um verbo que acrescenta ação ao passo atual, como os do Playwright, precisa
/// saber se deve continuar o passo ou abrir outro. Sem isto o And() não separava nada numa
/// cadeia só de navegador: as ações caíam todas no mesmo passo, e um Validate no meio só rodava
/// no fim, depois das ações que deveriam vir depois dele.
/// </para>
/// </summary>
internal interface IFronteiraDePasso
{
	bool ComecaPassoNovo { get; }
}

internal static class FronteiraDePasso
{
	/// <summary>Se o próximo verbo deve abrir um passo novo em vez de continuar o atual.</summary>
	public static bool ComecaPassoNovo(ITestScenario cenario) =>
		cenario is IFronteiraDePasso fronteira && fronteira.ComecaPassoNovo;
}
