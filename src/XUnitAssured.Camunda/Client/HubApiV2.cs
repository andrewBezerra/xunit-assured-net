using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace XUnitAssured.Camunda.Client;

/// <summary>A Camunda Hub API (<c>/api/v2</c>), do Camunda 8.10 em diante: os nomes da tela, e os erros da RFC 9457.</summary>
internal sealed class HubApiV2 : IHubApi
{
	private readonly ChamadasAoHub _hub;

	public HubApiV2(ChamadasAoHub hub) => _hub = hub;

	public CamundaHubApiVersion Versao => CamundaHubApiVersion.V2;

	// A v2 não diz as permissões do client.
	public async Task<CamundaHubInfo> Info(CancellationToken ct)
	{
		var info = await _hub.Pedir(HttpMethod.Get, "api/v2/info", null, ct).ConfigureAwait(false);
		return new CamundaHubInfo(Versao, Hub.Texto(info?["authorizedOrganization"]), null, null, null, null);
	}
}
