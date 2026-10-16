using System.Text.Json.Serialization;

using XUnitAssured.Core.Abstractions;
using XUnitAssured.Core.Configuration;

namespace XUnitAssured.Camunda;

/// <summary>
/// O que o pacote precisa saber para alcançar um cluster Camunda 8.
///
/// <para>
/// Lido da seção <c>camunda</c> do <c>testsettings.json</c>, ao lado de <c>http</c>, <c>kafka</c> e
/// <c>rabbitmq</c>. O que ficar de fora aqui ainda pode vir das variáveis <c>CAMUNDA_*</c> que o SDK
/// oficial lê sozinho (<c>CAMUNDA_REST_ADDRESS</c>, <c>CAMUNDA_BASIC_AUTH_PASSWORD</c>...): a seção
/// ganha delas, e elas ganham dos padrões.
/// </para>
/// </summary>
public class CamundaSettings
{
	/// <summary>
	/// O endereço REST do cluster, sem o <c>/v2</c>, por exemplo <c>http://localhost:8080</c>.
	/// </summary>
	/// <remarks>Nulo deixa o SDK decidir: <c>CAMUNDA_REST_ADDRESS</c>, e senão o localhost.</remarks>
	[JsonPropertyName("restAddress")]
	public string? RestAddress { get; set; }

	/// <summary>Como o pacote se apresenta ao cluster.</summary>
	[JsonPropertyName("authentication")]
	public CamundaAuthenticationSettings Authentication { get; set; } = new();

	/// <summary>
	/// Quanto uma asserção espera o cluster chegar ao estado afirmado, em segundos. Padrão: 15.
	/// </summary>
	/// <remarks>
	/// As buscas do Camunda leem o armazenamento secundário, que fica atrás do motor: o estado
	/// aparece com atraso. O Camunda Process Test usa 10 s; aqui o padrão é um pouco maior porque
	/// o cluster é compartilhado e pode estar ocupado com os testes de outra pessoa.
	/// </remarks>
	[JsonPropertyName("assertionTimeoutSeconds")]
	public int AssertionTimeoutSeconds { get; set; } = 15;

	/// <summary>Intervalo entre duas perguntas ao cluster, em milissegundos. Padrão: 100.</summary>
	[JsonPropertyName("pollIntervalMilliseconds")]
	public int PollIntervalMilliseconds { get; set; } = 100;

	/// <summary>
	/// Por quanto tempo os mocks de uma instância continuam trabalhando, em segundos. Padrão: 120.
	/// </summary>
	/// <remarks>
	/// Os mocks param sozinhos quando a instância termina. Uma instância que não termina (o teste
	/// falhou, ou ela espera algo que nenhum mock resolve) é cancelada ao fim deste prazo, para
	/// não deixar no cluster compartilhado uma instância ativa que ninguém vai olhar.
	/// </remarks>
	[JsonPropertyName("mockLifetimeSeconds")]
	public int MockLifetimeSeconds { get; set; } = 120;

	/// <summary>O tenant das instâncias, quando o cluster tem multi-tenancy. Nulo: o padrão.</summary>
	[JsonPropertyName("tenantId")]
	public string? TenantId { get; set; }

	/// <summary>
	/// Carrega a seção <c>camunda</c> do <c>testsettings.json</c>, ou os padrões quando não há
	/// arquivo nem seção.
	/// </summary>
	/// <remarks>
	/// O arquivo é localizado como o resto da configuração: <c>TESTSETTINGS_PATH</c> quando
	/// definida, senão <c>testsettings.json</c> no diretório atual e nos pais, com os marcadores
	/// <c>${ENV:NOME}</c> substituídos. É o marcador que mantém a senha fora do arquivo.
	/// </remarks>
	public static CamundaSettings Load() =>
		TestSettingsSection.Read<CamundaSettings>("camunda") ?? new CamundaSettings();

	internal const string ChaveNoContexto = "_CamundaSettings";

	/// <summary>
	/// As do contexto, quando alguém as pôs lá (os testes do pacote, com prazos curtos), senão as
	/// do arquivo.
	/// </summary>
	internal static CamundaSettings De(ITestContext context) =>
		context.GetProperty<CamundaSettings>(ChaveNoContexto) ?? Load();
}

/// <summary>
/// A autenticação no cluster: nenhuma, Basic (usuário e senha) ou OAuth (client credentials).
/// </summary>
public class CamundaAuthenticationSettings
{
	/// <summary>
	/// <c>None</c>, <c>Basic</c> ou <c>OAuth</c>.
	/// </summary>
	/// <remarks>
	/// Nulo, e sem <c>CAMUNDA_AUTH_STRATEGY</c>: <c>Basic</c> quando há usuário (aqui ou em
	/// <c>CAMUNDA_BASIC_AUTH_USERNAME</c>), <c>OAuth</c> quando há client id. O SDK sozinho não faz
	/// isso para Basic, e sem a estratégia ele não manda credencial nenhuma.
	/// </remarks>
	[JsonPropertyName("type")]
	public string? Type { get; set; }

	/// <summary>Usuário, para <c>Basic</c>.</summary>
	[JsonPropertyName("username")]
	public string? Username { get; set; }

	/// <summary>Senha, para <c>Basic</c>. Use <c>${ENV:NOME}</c> em vez de escrevê-la no arquivo.</summary>
	[JsonPropertyName("password")]
	public string? Password { get; set; }

	/// <summary>Client id, para <c>OAuth</c>.</summary>
	[JsonPropertyName("clientId")]
	public string? ClientId { get; set; }

	/// <summary>Client secret, para <c>OAuth</c>. Use <c>${ENV:NOME}</c>.</summary>
	[JsonPropertyName("clientSecret")]
	public string? ClientSecret { get; set; }

	/// <summary>O endpoint de token, para <c>OAuth</c>.</summary>
	[JsonPropertyName("oauthUrl")]
	public string? OAuthUrl { get; set; }

	/// <summary>A audiência do token, para <c>OAuth</c>, quando o provedor exige.</summary>
	[JsonPropertyName("audience")]
	public string? Audience { get; set; }
}
