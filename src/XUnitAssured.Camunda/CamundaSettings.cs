using System;
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
	/// falhou, ou ela espera algo que nenhum mock resolve) é cancelada ao fim deste prazo ou quando o
	/// processo de teste termina, o que vier primeiro, para não deixar no cluster compartilhado uma
	/// instância ativa que ninguém vai olhar.
	/// </remarks>
	[JsonPropertyName("mockLifetimeSeconds")]
	public int MockLifetimeSeconds { get; set; } = 120;

	/// <summary>
	/// O relatório de cobertura dos diagramas: o que as instâncias dos testes percorreram e o que nunca rodou.
	/// </summary>
	[JsonPropertyName("coverage")]
	public CamundaCoverageSettings Coverage { get; set; } = new();

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

	/// <summary>
	/// Carrega a seção <c>camunda</c> como <see cref="Load"/>, mas exige o endereço do cluster: sem a seção
	/// com o <c>restAddress</c>, e sem a variável <c>CAMUNDA_REST_ADDRESS</c>, lança em vez de deixar o
	/// cliente usar o localhost.
	/// </summary>
	/// <remarks>
	/// Para o teste que só faz sentido num cluster de verdade, como o que um gerador escreve para rodar o
	/// processo no Camunda do cliente: ele falha dizendo o que falta, e não roda contra um Camunda que
	/// alguém deixou de pé na máquina. A cadeia sem fixture continua com o <see cref="Load"/>.
	/// </remarks>
	/// <exception cref="InvalidOperationException">Não há endereço do cluster.</exception>
	public static CamundaSettings LoadRequired() =>
		LoadRequired(TestSettingsSection.Read<CamundaSettings>("camunda"), Environment.GetEnvironmentVariable);

	internal static CamundaSettings LoadRequired(CamundaSettings? lida, Func<string, string?> ambiente, string? outroCaminho = null)
	{
		ArgumentNullException.ThrowIfNull(ambiente);

		// Os mesmos lugares de onde o cliente tira o endereço: a seção, e senão as variáveis do SDK.
		var endereco = lida?.RestAddress ?? ambiente("CAMUNDA_REST_ADDRESS") ?? ambiente("ZEEBE_REST_ADDRESS");
		if (string.IsNullOrWhiteSpace(endereco))
			throw new InvalidOperationException(
				"This test runs on Camunda, but no cluster is configured: testsettings.json has no \"camunda\" section " +
				"with a \"restAddress\", and CAMUNDA_REST_ADDRESS is not set. Add the cluster's address, for example " +
				"{ \"camunda\": { \"restAddress\": \"http://localhost:8080\" } }, with its authentication: the test does not " +
				"fall back to localhost on its own." + outroCaminho);

		return lida ?? new CamundaSettings();
	}

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

	/// <summary>O scope do token, para <c>OAuth</c>, quando o provedor exige (o <c>CAMUNDA_OAUTH_SCOPE</c>).</summary>
	[JsonPropertyName("scope")]
	public string? Scope { get; set; }
}

/// <summary>
/// O relatório de cobertura que o pacote grava quando o processo de teste termina.
/// </summary>
public class CamundaCoverageSettings
{
	/// <summary>Se o relatório é gravado. Padrão: sim, como no Camunda Process Test.</summary>
	[JsonPropertyName("enabled")]
	public bool Enabled { get; set; } = true;

	/// <summary>
	/// Onde gravar <c>index.html</c> e <c>coverage.json</c>. Relativo à pasta de saída do teste.
	/// Padrão: <c>camunda-coverage</c>.
	/// </summary>
	[JsonPropertyName("directory")]
	public string Directory { get; set; } = "camunda-coverage";
}
