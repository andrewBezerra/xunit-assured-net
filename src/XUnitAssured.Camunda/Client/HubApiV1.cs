using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace XUnitAssured.Camunda.Client;

/// <summary>
/// A API do Web Modeler (<c>/api/v1</c>), obsoleta no Camunda 8.10 e removida no 8.12, com os nomes de antes do 8.10: o
/// "project" dela é o workspace, e a "process application", o projeto. As chaves são as mesmas da v2.
/// </summary>
internal sealed class HubApiV1 : IHubApi
{
	private readonly ChamadasAoHub _hub;

	public HubApiV1(ChamadasAoHub hub) => _hub = hub;

	public CamundaHubApiVersion Versao => CamundaHubApiVersion.V1;

	public async Task<CamundaHubInfo> Info(CancellationToken ct)
	{
		var info = await _hub.Pedir(HttpMethod.Get, "api/v1/info", null, ct).ConfigureAwait(false);
		return new CamundaHubInfo(Versao, Hub.Texto(info?["authorizedOrganization"]),
			Hub.Booleano(info?["createPermission"]), Hub.Booleano(info?["readPermission"]),
			Hub.Booleano(info?["updatePermission"]), Hub.Booleano(info?["deletePermission"]));
	}
}
