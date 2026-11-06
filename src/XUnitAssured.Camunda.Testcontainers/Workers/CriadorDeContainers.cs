using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;

namespace XUnitAssured.Camunda.Testcontainers;

/// <summary>
/// Um container como o fixture o quer: o nome na rede, a imagem, as variáveis já resolvidas, as portas e a espera.
/// </summary>
/// <param name="Nome">O nome na rede.</param>
/// <param name="Imagem">A imagem.</param>
/// <param name="Variaveis">As variáveis de ambiente, sem marcador.</param>
/// <param name="Portas">As portas que o host alcança.</param>
/// <param name="Espera">Quando ele está pronto; nulo, assim que sobe.</param>
/// <param name="Basic">O usuário e a senha do pedido HTTP da espera, quando ele pede autenticação.</param>
internal sealed record DefinicaoDeContainer(
	string Nome,
	string Imagem,
	IReadOnlyDictionary<string, string> Variaveis,
	IReadOnlyList<int> Portas,
	CamundaContainerWait? Espera,
	(string Usuario, string Senha)? Basic);

/// <summary>A rede de uma execução, onde os containers se acham pelo nome.</summary>
internal interface IRedeDoTeste : IAsyncDisposable
{
	string Nome { get; }
}

/// <summary>Um container de pé.</summary>
internal interface IContainerDoTeste : IAsyncDisposable
{
	/// <summary>O host onde as portas mapeadas respondem.</summary>
	string Host { get; }

	/// <summary>A porta do host que leva à porta do container.</summary>
	int PortaNoHost(int portaInterna);
}

/// <summary>
/// Quem cria a rede e sobe os containers. O Docker de verdade é o <see cref="CriadorNoDocker"/>; os testes do pacote
/// usam um falso, sem Docker.
/// </summary>
internal interface ICriadorDeContainers
{
	Task<IRedeDoTeste> CriarRedeAsync(CancellationToken cancellationToken);

	/// <summary>Sobe o container na rede e espera ele ficar pronto.</summary>
	Task<IContainerDoTeste> SubirAsync(DefinicaoDeContainer definicao, IRedeDoTeste rede, CancellationToken cancellationToken);
}

/// <summary>O Docker de verdade, pelo Testcontainers.</summary>
internal sealed class CriadorNoDocker : ICriadorDeContainers
{
	public async Task<IRedeDoTeste> CriarRedeAsync(CancellationToken cancellationToken)
	{
		var rede = new NetworkBuilder().WithName($"xunitassured-{Guid.NewGuid():N}").Build();
		await rede.CreateAsync(cancellationToken).ConfigureAwait(false);
		return new Rede(rede);
	}

	public async Task<IContainerDoTeste> SubirAsync(DefinicaoDeContainer definicao, IRedeDoTeste rede, CancellationToken cancellationToken)
	{
		var builder = new ContainerBuilder(definicao.Imagem)
			.WithNetwork(((Rede)rede).Docker)
			.WithNetworkAliases(definicao.Nome)
			.WithEnvironment(definicao.Variaveis);
		foreach (var porta in definicao.Portas)
			builder = builder.WithPortBinding(porta, assignRandomHostPort: true);
		if (Espera(definicao) is { } espera)
			builder = builder.WithWaitStrategy(espera);

		var container = builder.Build();
		try
		{
			await container.StartAsync(cancellationToken).ConfigureAwait(false);
		}
		catch
		{
			await container.DisposeAsync().ConfigureAwait(false);
			throw;
		}

		return new Container(container);
	}

	// A espera da definição na forma do Testcontainers: o caminho HTTP, a linha do log ou a porta, nessa ordem.
	internal static IWaitForContainerOS? Espera(DefinicaoDeContainer definicao)
	{
		var espera = definicao.Espera;
		if (espera is null)
			return null;

		var aguardar = Wait.ForUnixContainer();
		if (espera.Path is { } caminho)
			return aguardar.UntilHttpRequestIsSucceeded(pedido =>
			{
				pedido = pedido.ForPort((ushort)(espera.Port ?? definicao.Portas[0])).ForPath(caminho);
				return definicao.Basic is var (usuario, senha) ? pedido.WithBasicAuthentication(usuario, senha) : pedido;
			});
		if (espera.Log is { } log)
			return aguardar.UntilMessageIsLogged(new Regex(Regex.Escape(log), RegexOptions.None, TimeSpan.FromSeconds(1)));
		return espera.Port is { } porta ? aguardar.UntilInternalTcpPortIsAvailable(porta) : null;
	}

	private sealed class Rede(INetwork docker) : IRedeDoTeste
	{
		public INetwork Docker { get; } = docker;

		public string Nome => Docker.Name;

		public async ValueTask DisposeAsync() => await Docker.DisposeAsync().ConfigureAwait(false);
	}

	private sealed class Container(IContainer docker) : IContainerDoTeste
	{
		public string Host => docker.Hostname;

		public int PortaNoHost(int portaInterna) => docker.GetMappedPublicPort(portaInterna);

		public async ValueTask DisposeAsync() => await docker.DisposeAsync().ConfigureAwait(false);
	}
}
