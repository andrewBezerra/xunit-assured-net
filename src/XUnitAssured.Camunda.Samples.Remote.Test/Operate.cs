using XUnitAssured.Camunda;
using XUnitAssured.Camunda.Extensions;

using Xunit.Abstractions;

namespace XUnitAssured.Camunda.Samples.Remote.Test;

/// <summary>
/// Escreve na saída do teste onde abrir a instância no Operate, para conferir com os próprios olhos
/// o caminho que ela fez no diagrama.
/// </summary>
internal static class Operate
{
	/// <summary>A mesma coisa, no meio da cadeia, antes do <c>await</c>.</summary>
	public static async Task<CamundaValidationBuilder> Mostrar(
		this Task<CamundaValidationBuilder> execucao, ITestOutputHelper saida, string oQueVer) =>
		(await execucao.ConfigureAwait(false)).Mostrar(saida, oQueVer);

	public static CamundaValidationBuilder Mostrar(this CamundaValidationBuilder execucao, ITestOutputHelper saida, string oQueVer)
	{
		var resultado = execucao.GetResult();
		var endereco = CamundaSettings.Load().RestAddress?.TrimEnd('/');

		var versao = resultado.Version is { } v ? $" versão {v}" : "";
		var rotulo = resultado.VersionTag is { } t ? $", tag {t}" : "";
		saida.WriteLine($"{resultado.ProcessId}{versao}{rotulo}: instância {resultado.ProcessInstanceKey}");
		saida.WriteLine($"  Operate: {endereco}/operate/processes/{resultado.ProcessInstanceKey}");
		saida.WriteLine($"  O que ver: {oQueVer}");

		return execucao;
	}
}
