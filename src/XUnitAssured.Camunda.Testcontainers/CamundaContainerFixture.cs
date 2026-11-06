using System;
using System.IO;
using System.Threading.Tasks;

using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

using Xunit;

using XUnitAssured.Camunda.Testing;

namespace XUnitAssured.Camunda.Testcontainers;

/// <summary>
/// Sobe um Camunda 8.10 no Docker, para os testes de uma classe ou de uma coleção.
/// </summary>
/// <remarks>
/// <para>
/// O mesmo Camunda que o job de ida e volta do CI usa: <c>camunda/camunda</c> sozinho, sem
/// Elasticsearch nem Connectors, com o armazenamento secundário em H2 na memória e a busca gravada
/// na hora (<c>FLUSHINTERVAL=PT0S</c>), como o Camunda Process Test. Leva uns 35 segundos para ficar
/// pronto; por isso, para a suíte inteira, use-o como <c>ICollectionFixture</c>.
/// </para>
/// <para>
/// A API exige autenticação Basic, com um usuário <c>xunit</c> e uma senha nova a cada execução: o
/// fixture as entrega às cadeias, e ninguém precisa conhecê-las. Os prazos (asserções, mocks) vêm do
/// <c>testsettings.json</c>, como sempre.
/// </para>
/// <para>
/// Só com TLS, no REST e no gRPC: o fixture gera, a cada execução, uma autoridade de teste e o certificado do servidor, e
/// as cadeias confiam só nessa autoridade (<see cref="CamundaSettings.CaCertificatePath"/>).
/// </para>
/// <para>
/// Precisa de um Docker que o Testcontainers alcance (o do runner <c>ubuntu-latest</c> do GitHub
/// serve). Sem Docker, o fixture falha ao iniciar, e com ele toda a classe.
/// </para>
/// </remarks>
public class CamundaContainerFixture : CamundaClusterFixture, IAsyncLifetime
{
	/// <summary>A imagem padrão: a versão do Camunda com que o pacote foi verificado.</summary>
	public const string ImagemPadrao = "camunda/camunda:8.10.4";

	private readonly string _senha = Guid.NewGuid().ToString("N");
	private IContainer? _container;
	private CertificadosDoTeste? _certificados;
	private string? _pastaDoTls;

	/// <summary>A imagem do Camunda. Troque para testar outra versão 8.10.x.</summary>
	protected virtual string Image => ImagemPadrao;

	/// <summary>O container, depois de iniciado.</summary>
	public IContainer Container =>
		_container ?? throw new InvalidOperationException("The Camunda container has not started yet.");

	/// <summary>O endereço da REST API no host, sem <c>/v2</c>. O Operate fica em <c>{RestAddress}/operate</c>.</summary>
	public string RestAddress => Settings.RestAddress
		?? throw new InvalidOperationException("The Camunda container has not started yet.");

	/// <summary>
	/// Acrescenta configurações ao container antes de ele subir: variáveis de ambiente, rede, nome.
	/// </summary>
	/// <param name="builder">O container como o fixture o montou.</param>
	/// <returns>O container com o que foi acrescentado.</returns>
	protected virtual ContainerBuilder Configure(ContainerBuilder builder) => builder;

	/// <inheritdoc />
	public virtual async Task InitializeAsync()
	{
		var enderecos = MontagemDosContainers.Enderecos;
		_certificados = CertificadosDoTeste.Gerar(
			enderecos.NomesDoCertificado(EnderecosDoCamunda.HostsDoDocker(Environment.GetEnvironmentVariable)), DateTimeOffset.UtcNow);
		_pastaDoTls = Directory.CreateTempSubdirectory("xunitassured-tls-").FullName;
		_certificados.Gravar(_pastaDoTls);

		var builder = new ContainerBuilder(Image)
			.WithPortBinding(enderecos.PortaRest, assignRandomHostPort: true)
			.WithPortBinding(enderecos.PortaGrpc, assignRandomHostPort: true)
			// O mesmo Camunda do CamundaFixture: o TLS, o H2 na memória e o usuário do Basic.
			.WithEnvironment(MontagemDosContainers.VariaveisDoCamunda(ModoDeAutenticacao.Basic, _senha))
			// Pronto é quando a REST API responde em https, autenticada, e não quando o processo sobe.
			.WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(pedido => pedido
				.ForPort((ushort)enderecos.PortaRest)
				.ForPath("/v2/topology")
				.UsingTls()
				.UsingHttpMessageHandler(CriadorNoDocker.ConfiandoNa(_certificados.Autoridade))
				.WithBasicAuthentication(MontagemDosContainers.Usuario, _senha)));
		foreach (var (nome, conteudo) in _certificados.Arquivos())
			builder = builder.WithResourceMapping(conteudo, EnderecosDoCamunda.NoContainer(nome));

		_container = Configure(builder).Build();
		await _container.StartAsync().ConfigureAwait(false);

		var arquivo = CamundaSettings.Load();
		Settings = new CamundaSettings
		{
			RestAddress = enderecos.RestNoHost(_container.Hostname, _container.GetMappedPublicPort(enderecos.PortaRest)),
			CaCertificatePath = Path.Combine(_pastaDoTls, CertificadosDoTeste.ArquivoDaAutoridade),
			Authentication = new CamundaAuthenticationSettings { Type = "Basic", Username = MontagemDosContainers.Usuario, Password = _senha },
			AssertionTimeoutSeconds = arquivo.AssertionTimeoutSeconds,
			PollIntervalMilliseconds = arquivo.PollIntervalMilliseconds,
			MockLifetimeSeconds = arquivo.MockLifetimeSeconds
		};
	}

	/// <inheritdoc />
	public virtual async Task DisposeAsync()
	{
		if (_container is not null)
			await _container.DisposeAsync().ConfigureAwait(false);
		if (_pastaDoTls is not null && Directory.Exists(_pastaDoTls))
			Directory.Delete(_pastaDoTls, recursive: true);
		_certificados?.Dispose();
	}
}
