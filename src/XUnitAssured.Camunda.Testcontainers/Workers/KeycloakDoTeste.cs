using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Nodes;

namespace XUnitAssured.Camunda.Testcontainers;

/// <summary>
/// Os clients do realm de uma execução no modo <c>oauth</c>: o do Camunda (o da tela, que valida o token), o do worker do
/// cliente e o do próprio teste, cada um com o segredo dele, e o audience que o Camunda exige.
/// </summary>
/// <param name="ClientIdDoWorker">O client id que o cliente declarou para o worker.</param>
/// <param name="SegredoDoWorker">O segredo do worker: o declarado, ou um novo a cada execução.</param>
/// <param name="SegredoDoCamunda">O segredo do client do Camunda, novo a cada execução.</param>
/// <param name="SegredoDoTeste">O segredo do client do teste, novo a cada execução.</param>
/// <param name="Audience">O audience que o Camunda exige no token.</param>
internal sealed record CredenciaisOAuth(
	string ClientIdDoWorker, string SegredoDoWorker, string SegredoDoCamunda, string SegredoDoTeste, string Audience)
{
	/// <summary>O client do Camunda no realm.</summary>
	internal const string ClientIdDoCamunda = "camunda";

	/// <summary>O client do próprio teste no realm.</summary>
	internal const string ClientIdDoTeste = "xunitassured-test";

	/// <summary>O audience do Camunda self-managed, quando o cliente não diz outro.</summary>
	internal const string AudiencePadrao = "orchestration-api";

	/// <summary>
	/// As credenciais do realm: o client do worker que o cliente declarou, com o segredo dele ou um gerado, e os segredos
	/// novos do Camunda e do teste.
	/// </summary>
	/// <exception cref="InvalidOperationException">Sem o client id do worker, ou com o de um client reservado.</exception>
	internal static CredenciaisOAuth Montar(CamundaContainersOAuth? oauth, Func<string> gerarSegredo)
	{
		ArgumentNullException.ThrowIfNull(gerarSegredo);
		var clientId = oauth?.ClientId?.Trim();
		if (string.IsNullOrEmpty(clientId))
			throw new InvalidOperationException(
				"camunda.containers.authentication \"oauth\" needs the client id the worker uses, in camunda.containers.oauth.clientId: add " +
				"\"camunda\": { \"containers\": { \"oauth\": { \"clientId\": \"...\" } } } (the clientSecret is optional; without it, " +
				"the fixture generates one on every run).");
		if (string.Equals(clientId, ClientIdDoCamunda, StringComparison.OrdinalIgnoreCase)
			|| string.Equals(clientId, ClientIdDoTeste, StringComparison.OrdinalIgnoreCase))
			throw new InvalidOperationException(
				$"camunda.containers.oauth.clientId '{clientId}' is the name of a client the fixture creates " +
				$"('{ClientIdDoCamunda}' and '{ClientIdDoTeste}'): give the worker another client id.");

		var segredo = string.IsNullOrEmpty(oauth!.ClientSecret) ? gerarSegredo() : oauth.ClientSecret;
		var audience = string.IsNullOrWhiteSpace(oauth.Audience) ? AudiencePadrao : oauth.Audience.Trim();
		return new CredenciaisOAuth(clientId, segredo, gerarSegredo(), gerarSegredo(), audience);
	}
}

/// <summary>
/// O Keycloak do modo <c>oauth</c>, no molde do camunda-sm: a imagem <c>camunda/keycloak:quay-26.6.4</c>, o caminho
/// <c>/auth</c> e o realm <c>camunda-platform</c>, em https com o certificado de teste, pelo nome <c>keycloak</c> na rede.
/// </summary>
/// <remarks>
/// O realm é importado de um arquivo que o fixture gera e copia para o container (<c>start-dev --import-realm</c>), e não
/// montado pela API de administração: sobe pronto, sem um passo a mais depois da subida e sem a senha do administrador. O
/// <c>KC_HOSTNAME</c> fixa o emissor no nome da rede, para o token pedido pelo host (o do teste, na porta mapeada) e o pedido
/// pela rede (o dos workers) terem o mesmo <c>iss</c> que o Camunda valida.
/// </remarks>
internal static class KeycloakDoTeste
{
	internal const string Imagem = "camunda/keycloak:quay-26.6.4";
	internal const string NomeNaRede = "keycloak";
	internal const int Porta = 8443;
	internal const string CaminhoRelativo = "/auth";
	internal const string Realm = "camunda-platform";
	internal const string ArquivoDoRealm = "/opt/keycloak/data/import/camunda-platform-realm.json";

	private static string Esquema => EnderecosDoCamunda.Padrao.Esquema;

	/// <summary>O emissor dos tokens: o Keycloak como a rede o vê.</summary>
	internal static string EmissorNaRede => Emissor(NomeNaRede, Porta);

	/// <summary>O endereço do token na rede, o dos workers.</summary>
	internal static string TokenNaRede => EmissorNaRede + "/protocol/openid-connect/token";

	/// <summary>As chaves que o Camunda usa para validar o token.</summary>
	internal static string ChavesNaRede => EmissorNaRede + "/protocol/openid-connect/certs";

	/// <summary>O login da tela do Camunda, que o OIDC dele pede.</summary>
	internal static string AutorizacaoNaRede => EmissorNaRede + "/protocol/openid-connect/auth";

	/// <summary>O endereço do token como o teste o alcança: o host do Docker e a porta mapeada.</summary>
	internal static string TokenNoHost(string host, int porta) => Emissor(host, porta) + "/protocol/openid-connect/token";

	private static string Emissor(string host, int porta) =>
		new UriBuilder(Esquema, host, porta).Uri.GetLeftPart(UriPartial.Authority) + $"{CaminhoRelativo}/realms/{Realm}";

	/// <summary>As variáveis do Keycloak: o https com o certificado de teste, o caminho e o emissor fixo.</summary>
	internal static IReadOnlyDictionary<string, string> Variaveis() => new Dictionary<string, string>(StringComparer.Ordinal)
	{
		["KC_HTTPS_CERTIFICATE_FILE"] = EnderecosDoCamunda.NoContainer(CertificadosDoTeste.ArquivoDoServidor),
		["KC_HTTPS_CERTIFICATE_KEY_FILE"] = EnderecosDoCamunda.NoContainer(CertificadosDoTeste.ArquivoDaChave),
		["KC_HTTPS_PORT"] = Porta.ToString(System.Globalization.CultureInfo.InvariantCulture),
		["KC_HTTP_ENABLED"] = "false",
		["KC_HTTP_RELATIVE_PATH"] = CaminhoRelativo,
		["KC_HOSTNAME"] = new UriBuilder(Esquema, NomeNaRede, Porta).Uri.GetLeftPart(UriPartial.Authority) + CaminhoRelativo,
		["KC_HOSTNAME_STRICT"] = "false"
	};

	/// <summary>O comando: o modo de desenvolvimento, que importa o realm da pasta de importação.</summary>
	internal static IReadOnlyList<string> Comando { get; } = ["start-dev", "--import-realm"];

	/// <summary>
	/// O realm em JSON: os três clients confidenciais; o do worker e o do teste com o client credentials (a conta de
	/// serviço), o audience do Camunda no token e o <c>client_id</c> que o Camunda lê para saber quem chama.
	/// </summary>
	internal static byte[] RealmJson(CredenciaisOAuth credenciais)
	{
		ArgumentNullException.ThrowIfNull(credenciais);
		var realm = new JsonObject
		{
			["realm"] = Realm,
			["enabled"] = true,
			["sslRequired"] = "external",
			["clients"] = new JsonArray(
				new JsonObject
				{
					["clientId"] = CredenciaisOAuth.ClientIdDoCamunda,
					["secret"] = credenciais.SegredoDoCamunda,
					["publicClient"] = false,
					["serviceAccountsEnabled"] = false,
					["standardFlowEnabled"] = true,
					["redirectUris"] = new JsonArray("*"),
					["protocol"] = "openid-connect"
				},
				ClientDeServico(credenciais.ClientIdDoWorker, credenciais.SegredoDoWorker, credenciais.Audience),
				ClientDeServico(CredenciaisOAuth.ClientIdDoTeste, credenciais.SegredoDoTeste, credenciais.Audience))
		};
		return Encoding.UTF8.GetBytes(realm.ToJsonString());
	}

	private static JsonObject ClientDeServico(string clientId, string segredo, string audience) => new()
	{
		["clientId"] = clientId,
		["secret"] = segredo,
		["publicClient"] = false,
		["serviceAccountsEnabled"] = true,
		["standardFlowEnabled"] = false,
		["directAccessGrantsEnabled"] = false,
		["protocol"] = "openid-connect",
		["protocolMappers"] = new JsonArray(
			new JsonObject
			{
				["name"] = "audience",
				["protocol"] = "openid-connect",
				["protocolMapper"] = "oidc-audience-mapper",
				["config"] = new JsonObject
				{
					["included.custom.audience"] = audience,
					["access.token.claim"] = "true",
					["id.token.claim"] = "false",
					["introspection.token.claim"] = "true"
				}
			},
			new JsonObject
			{
				["name"] = "client_id",
				["protocol"] = "openid-connect",
				["protocolMapper"] = "oidc-hardcoded-claim-mapper",
				["config"] = new JsonObject
				{
					["claim.name"] = "client_id",
					["claim.value"] = clientId,
					["jsonType.label"] = "String",
					["access.token.claim"] = "true",
					["id.token.claim"] = "false",
					["introspection.token.claim"] = "true"
				}
			})
	};
}
