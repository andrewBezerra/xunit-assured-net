using System.Collections.Concurrent;
using System.Text.Json;

using XUnitAssured.Core.Abstractions;

namespace XUnitAssured.Camunda.Gateway;

/// <summary>
/// De onde um passo tira o cluster.
/// </summary>
/// <remarks>
/// <para>
/// Primeiro o contexto, onde um teste do próprio pacote põe um cluster falso (ou, mais
/// adiante, uma fixture). Senão, um gateway compartilhado montado do <c>testsettings.json</c>.
/// </para>
/// <para>
/// O compartilhado vive enquanto o processo de teste viver, e não é descartado: os mocks de uma
/// instância continuam trabalhando depois que a cadeia que os criou terminou, e descartar o
/// cliente no fim da cadeia os mataria no meio. Um por configuração, para dois arquivos de
/// ambiente no mesmo processo não dividirem cliente.
/// </para>
/// </remarks>
internal static class Gateways
{
	internal const string ChaveNoContexto = "_CamundaGateway";

	private static readonly ConcurrentDictionary<string, ICamundaGateway> Compartilhados = new();

	internal static ICamundaGateway De(ITestContext context, CamundaSettings settings)
	{
		var doContexto = context.GetProperty<ICamundaGateway>(ChaveNoContexto);
		if (doContexto is not null)
			return doContexto;

		var chave = JsonSerializer.Serialize(settings);
		return Compartilhados.GetOrAdd(chave, _ => new SdkCamundaGateway(settings));
	}
}
