using System;
using System.Collections.Generic;
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

	/// <summary>Lê a seção <c>camunda</c> do <c>testsettings.json</c> e decide onde o teste roda.</summary>
	/// <exception cref="InvalidOperationException">Não há lista nem endereço, ou a lista está mal escrita.</exception>
	/// <exception cref="NotSupportedException">O modo <c>oauth</c>, que vem na 6.40.0.</exception>
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
			_plano = PlanoDaSubida.Montar(containers, (gerarSenha ?? (() => Guid.NewGuid().ToString("N")))());
	}

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
			var rede = await _criador.CriarRedeAsync(CancellationToken.None).ConfigureAwait(false);
			_deDescartar.Add(rede);
			var camunda = await _criador.SubirAsync(_plano.Camunda, rede, CancellationToken.None).ConfigureAwait(false);
			_deDescartar.Add(camunda);
			foreach (var servico in _plano.Servicos)
				_deDescartar.Add(await _criador.SubirAsync(servico, rede, CancellationToken.None).ConfigureAwait(false));

			Settings = new CamundaSettings
			{
				RestAddress = $"http://{camunda.Host}:{camunda.PortaNoHost(MontagemDosContainers.PortaRest)}",
				Authentication = _plano.Modo == ModoDeAutenticacao.Basic
					? new CamundaAuthenticationSettings { Type = "Basic", Username = MontagemDosContainers.Usuario, Password = _plano.Senha }
					: new CamundaAuthenticationSettings { Type = "None" },
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
	public virtual Task DisposeAsync() => DescartarAsync();

	async ValueTask IAsyncDisposable.DisposeAsync()
	{
		await DisposeAsync().ConfigureAwait(false);
		GC.SuppressFinalize(this);
	}

	// Do último que subiu ao primeiro, e a rede por último; descartado, não descarta de novo.
	private async Task DescartarAsync()
	{
		for (var i = _deDescartar.Count - 1; i >= 0; i--)
			await _deDescartar[i].DisposeAsync().ConfigureAwait(false);
		_deDescartar.Clear();
	}

	/// <summary>O que sobe, já resolvido: o Camunda e a lista na ordem, com as variáveis de cada um.</summary>
	private sealed record PlanoDaSubida(ModoDeAutenticacao Modo, string Senha, DefinicaoDeContainer Camunda, IReadOnlyList<DefinicaoDeContainer> Servicos)
	{
		internal static PlanoDaSubida Montar(CamundaContainersSettings containers, string senha)
		{
			var modo = MontagemDosContainers.Modo(containers.Authentication);
			var ordem = MontagemDosContainers.Ordenar(containers.Services);
			var nomes = ordem.Select(s => s.Name!).ToList();
			var basic = modo == ModoDeAutenticacao.Basic ? (MontagemDosContainers.Usuario, senha) : ((string, string)?)null;

			// Pronto é quando a REST API responde, autenticada no Basic, e não quando o processo sobe.
			var camunda = new DefinicaoDeContainer(
				MontagemDosContainers.NomeDoCamunda, CamundaContainerFixture.ImagemPadrao, MontagemDosContainers.VariaveisDoCamunda(modo, senha),
				[MontagemDosContainers.PortaRest, MontagemDosContainers.PortaGrpc],
				new CamundaContainerWait { Port = MontagemDosContainers.PortaRest, Path = "/v2/topology" }, basic);
			var servicos = ordem.Select(s => new DefinicaoDeContainer(
				s.Name!, s.Image!, MontagemDosContainers.VariaveisDoServico(s, modo, senha, nomes), s.Ports, s.Wait, null)).ToList();
			return new PlanoDaSubida(modo, senha, camunda, servicos);
		}
	}
}
