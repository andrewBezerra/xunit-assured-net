using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Xunit;

using XUnitAssured.Camunda.Testing;
using XUnitAssured.Core.Configuration;

namespace XUnitAssured.Camunda.Testcontainers;

/// <summary>
/// O Camunda onde o teste roda, escolhido pelo <c>testsettings.json</c>: o Camunda e os workers do cliente no Docker, ou
/// um cluster de fora.
/// </summary>
/// <remarks>
/// <para>
/// O Camunda sobe sempre com TLS, no REST e no gRPC, com um certificado de uma autoridade de teste gerada a cada execução:
/// o cliente do teste e os workers confiam só nela (os workers pelas variáveis das tecnologias comuns, como
/// <c>SSL_CERT_FILE</c>, <c>NODE_EXTRA_CA_CERTS</c> e o truststore do Java).
/// </para>
/// <para>
/// Com <c>camunda.containers</c>, sobe uma rede, o <c>camunda/camunda</c> nela pelo nome <c>camunda</c>, e os containers
/// da lista, na ordem do <c>dependsOn</c>, cada um pronto antes do seguinte. Cada container recebe o endereço do Camunda na
/// rede (<c>CAMUNDA_REST_ADDRESS</c>, <c>ZEEBE_REST_ADDRESS</c>, <c>ZEEBE_GRPC_ADDRESS</c>) e a autenticação do modo
/// (<c>CAMUNDA_AUTH_STRATEGY</c>, e no Basic o <c>CAMUNDA_BASIC_AUTH_USERNAME</c> e o <c>CAMUNDA_BASIC_AUTH_PASSWORD</c>);
/// o cliente do próprio teste fala com o Camunda no mesmo modo e com a mesma credencial, numa senha nova a cada execução.
/// </para>
/// <para>
/// Sem a lista e com o <c>camunda.restAddress</c>, usa esse cluster, como o <see cref="RequiredCamundaClusterFixture"/>.
/// Sem os dois, falha ao ser criado, dizendo o que acrescentar: o teste não cai no localhost.
/// </para>
/// <para>
/// Precisa de um Docker que o Testcontainers alcance quando a lista existe. Para a suíte inteira, use-o como
/// <c>ICollectionFixture</c>: o Camunda leva uns 35 segundos para subir.
/// </para>
/// </remarks>
public class CamundaFixture : CamundaClusterFixture, IAsyncLifetime, IAsyncDisposable
{
	private readonly ICriadorDeContainers _criador;
	private readonly CamundaSettings _lida;
	private readonly PlanoDaSubida? _plano;
	private readonly List<IAsyncDisposable> _deDescartar = [];
	private string? _pastaDoTls;

	/// <summary>Lê a seção <c>camunda</c> do <c>testsettings.json</c> e decide onde o teste roda.</summary>
	/// <exception cref="InvalidOperationException">
	/// Não há lista nem endereço, a lista está mal escrita, ou o modo <c>oauth</c> não diz o client id do worker.
	/// </exception>
	public CamundaFixture()
		: this(TestSettingsSection.Read<CamundaSectionWithContainers>("camunda"), Environment.GetEnvironmentVariable, new CriadorNoDocker())
	{
	}

	internal CamundaFixture(
		CamundaSectionWithContainers? lida, Func<string, string?> ambiente, ICriadorDeContainers criador, Func<string>? gerarSenha = null)
		: base(lida?.Containers is null ? CamundaSettings.LoadRequired(lida, ambiente, ComContainers) : new CamundaSettings())
	{
		_criador = criador ?? throw new ArgumentNullException(nameof(criador));
		_lida = lida ?? new CamundaSettings();
		if (lida?.Containers is { } containers)
		{
			// A senha do Basic pode vir do teste; os segredos do OAuth são sempre novos, a cada execução.
			var senha = (gerarSenha ?? NovoSegredo)();
			_plano = PlanoDaSubida.Montar(containers, senha, NovoSegredo, ambiente);
		}
	}

	private static string NovoSegredo() => Guid.NewGuid().ToString("N");

	// A mensagem de quando não há cluster nenhum: este fixture também aceita a lista dos containers.
	private const string ComContainers =
		" Or declare the containers this fixture starts in Docker, in \"camunda\": { \"containers\": { \"services\": [ ... ] } }.";

	/// <summary>Se o teste roda nos containers que o fixture subiu, e não num cluster de fora.</summary>
	public bool UsesContainers => _plano is not null;

	/// <inheritdoc />
	public virtual async Task InitializeAsync()
	{
		if (_plano is null)
			return;

		try
		{
			// A autoridade fica no host para o cliente do teste; a pasta some no descarte.
			_pastaDoTls = Directory.CreateTempSubdirectory("xunitassured-tls-").FullName;
			_plano.Certificados.Gravar(_pastaDoTls);

			var rede = await _criador.CriarRedeAsync(CancellationToken.None).ConfigureAwait(false);
			_deDescartar.Add(rede);
			// No OAuth, o Keycloak antes do Camunda: o Camunda lê o emissor dele ao subir.
			IContainerDoTeste? keycloak = null;
			if (_plano.Keycloak is { } definicaoDoKeycloak)
			{
				keycloak = await _criador.SubirAsync(definicaoDoKeycloak, rede, CancellationToken.None).ConfigureAwait(false);
				_deDescartar.Add(keycloak);
			}
			var camunda = await _criador.SubirAsync(_plano.Camunda, rede, CancellationToken.None).ConfigureAwait(false);
			_deDescartar.Add(camunda);
			foreach (var servico in _plano.Servicos)
				_deDescartar.Add(await _criador.SubirAsync(servico, rede, CancellationToken.None).ConfigureAwait(false));

			Settings = new CamundaSettings
			{
				RestAddress = MontagemDosContainers.Enderecos.RestNoHost(
					camunda.Host, camunda.PortaNoHost(MontagemDosContainers.Enderecos.PortaRest)),
				CaCertificatePath = Path.Combine(_pastaDoTls, CertificadosDoTeste.ArquivoDaAutoridade),
				Authentication = _plano.Modo switch
				{
					ModoDeAutenticacao.Basic => new CamundaAuthenticationSettings
					{
						Type = "Basic", Username = MontagemDosContainers.Usuario, Password = _plano.Senha
					},
					// O cliente do teste pede o token no mesmo Keycloak, pela porta mapeada no host, com o client dele.
					ModoDeAutenticacao.OAuth => new CamundaAuthenticationSettings
					{
						Type = "OAuth",
						ClientId = CredenciaisOAuth.ClientIdDoTeste,
						ClientSecret = _plano.OAuth!.SegredoDoTeste,
						OAuthUrl = KeycloakDoTeste.TokenNoHost(keycloak!.Host, keycloak.PortaNoHost(KeycloakDoTeste.Porta)),
						Audience = _plano.OAuth.Audience
					},
					_ => new CamundaAuthenticationSettings { Type = "None" }
				},
				AssertionTimeoutSeconds = _lida.AssertionTimeoutSeconds,
				PollIntervalMilliseconds = _lida.PollIntervalMilliseconds,
				MockLifetimeSeconds = _lida.MockLifetimeSeconds,
				Coverage = _lida.Coverage
			};
		}
		catch
		{
			await DescartarAsync().ConfigureAwait(false);
			throw;
		}
	}

	/// <inheritdoc />
	public virtual async Task DisposeAsync()
	{
		await DescartarAsync().ConfigureAwait(false);
		_plano?.Certificados.Dispose();
	}

	async ValueTask IAsyncDisposable.DisposeAsync()
	{
		await DisposeAsync().ConfigureAwait(false);
		GC.SuppressFinalize(this);
	}

	// Do último que subiu ao primeiro, a rede por último, e a pasta do TLS; descartado, não descarta de novo.
	private async Task DescartarAsync()
	{
		for (var i = _deDescartar.Count - 1; i >= 0; i--)
			await _deDescartar[i].DisposeAsync().ConfigureAwait(false);
		_deDescartar.Clear();
		if (_pastaDoTls is not null && Directory.Exists(_pastaDoTls))
			Directory.Delete(_pastaDoTls, recursive: true);
		_pastaDoTls = null;
	}

	/// <summary>A pasta do host com a autoridade e os certificados desta execução, enquanto os containers estão de pé.</summary>
	internal string? PastaDoTls => _pastaDoTls;

	/// <summary>
	/// O que sobe, já resolvido: os certificados, o Camunda e a lista na ordem, com as variáveis e os arquivos de cada um.
	/// </summary>
	private sealed record PlanoDaSubida(
		ModoDeAutenticacao Modo, string Senha, CredenciaisOAuth? OAuth, CertificadosDoTeste Certificados, DefinicaoDeContainer? Keycloak,
		DefinicaoDeContainer Camunda, IReadOnlyList<DefinicaoDeContainer> Servicos)
	{
		internal static PlanoDaSubida Montar(
			CamundaContainersSettings containers, string senha, Func<string> gerarSegredo, Func<string, string?> ambiente)
		{
			var modo = MontagemDosContainers.Modo(containers.Authentication);
			var oauth = modo == ModoDeAutenticacao.OAuth ? CredenciaisOAuth.Montar(containers.OAuth, gerarSegredo) : null;
			var ordem = MontagemDosContainers.Ordenar(containers.Services, comKeycloak: oauth is not null);
			var nomes = ordem.Select(s => s.Name!).ToList();
			var basic = modo == ModoDeAutenticacao.Basic ? (MontagemDosContainers.Usuario, senha) : ((string, string)?)null;
			var enderecos = MontagemDosContainers.Enderecos;
			// O Keycloak responde no mesmo certificado do servidor: o nome dele entra no certificado quando ele sobe.
			var outrosNomes = EnderecosDoCamunda.HostsDoDocker(ambiente).ToList();
			if (oauth is not null)
				outrosNomes.Add(KeycloakDoTeste.NomeNaRede);
			var certificados = CertificadosDoTeste.Gerar(enderecos.NomesDoCertificado(outrosNomes), DateTimeOffset.UtcNow);

			// Pronto é quando a REST API responde em https, autenticada no Basic, e não quando o processo sobe. No OAuth, a
			// espera não tem token: pronto é o /v2/topology respondendo 401, a API de pé e protegida.
			var camunda = new DefinicaoDeContainer(
				MontagemDosContainers.NomeDoCamunda, CamundaContainerFixture.ImagemPadrao,
				MontagemDosContainers.VariaveisDoCamunda(modo, senha, oauth, certificados.SenhaDoTruststore),
				[enderecos.PortaRest, enderecos.PortaGrpc],
				new CamundaContainerWait { Port = enderecos.PortaRest, Path = "/v2/topology" }, basic,
				ArquivosDoTls(certificados, doCamunda: true), certificados.Autoridade,
				StatusDaEspera: oauth is null ? null : System.Net.HttpStatusCode.Unauthorized);
			var keycloak = oauth is null ? null : DefinicaoDoKeycloak(oauth, certificados);
			var servicos = ordem.Select(s => new DefinicaoDeContainer(
				s.Name!, s.Image!, MontagemDosContainers.VariaveisDoServico(s, modo, senha, certificados.SenhaDoTruststore, nomes, oauth),
				s.Ports, s.Wait, null, ArquivosDoTls(certificados, doCamunda: false), null)).ToList();
			return new PlanoDaSubida(modo, senha, oauth, certificados, keycloak, camunda, servicos);
		}

		// O Keycloak em https com o certificado do servidor, o realm importado do arquivo gerado, e pronto quando o realm
		// publica a configuração do OpenID.
		private static DefinicaoDeContainer DefinicaoDoKeycloak(CredenciaisOAuth oauth, CertificadosDoTeste certificados)
		{
			var arquivos = new Dictionary<string, byte[]>(ArquivosDoTls(certificados, doCamunda: true), StringComparer.Ordinal)
			{
				[KeycloakDoTeste.ArquivoDoRealm] = KeycloakDoTeste.RealmJson(oauth)
			};
			return new DefinicaoDeContainer(
				KeycloakDoTeste.NomeNaRede, KeycloakDoTeste.Imagem, KeycloakDoTeste.Variaveis(), [KeycloakDoTeste.Porta],
				new CamundaContainerWait
				{
					Port = KeycloakDoTeste.Porta,
					Path = $"{KeycloakDoTeste.CaminhoRelativo}/realms/{KeycloakDoTeste.Realm}/.well-known/openid-configuration"
				},
				null, arquivos, certificados.Autoridade, KeycloakDoTeste.Comando);
		}

		// O Camunda recebe o certificado e a chave dele; os outros, só a autoridade e o truststore.
		internal static IReadOnlyDictionary<string, byte[]> ArquivosDoTls(CertificadosDoTeste certificados, bool doCamunda) =>
			certificados.Arquivos()
				.Where(a => doCamunda || a.Key is CertificadosDoTeste.ArquivoDaAutoridade or CertificadosDoTeste.ArquivoDoTruststore)
				.ToDictionary(a => EnderecosDoCamunda.NoContainer(a.Key), a => a.Value, StringComparer.Ordinal);
	}
}
