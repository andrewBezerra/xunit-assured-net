using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using XUnitAssured.Camunda.Gateway;
using XUnitAssured.Camunda.Mocks;
using XUnitAssured.Camunda.Results;
using XUnitAssured.Core.Abstractions;
using XUnitAssured.Core.Results;

namespace XUnitAssured.Camunda.Steps;

/// <summary>
/// Inicia uma instância do processo e põe os mocks para trabalhar nela.
/// </summary>
/// <remarks>
/// <para>
/// <b>Qual versão.</b> Numa cadeia com <c>Deploy</c>, a que a cadeia implantou, pela chave da
/// definição, e não pela última do <c>processId</c>: num cluster compartilhado, a última pode ser
/// a que outra pessoa acabou de implantar. Por isso, nessa cadeia, iniciar um processo que ela não
/// implantou é erro.
/// </para>
/// <para>
/// Numa cadeia sem <c>Deploy</c>, ou com <see cref="Version"/> ou <see cref="VersionTag"/>, a que
/// já está no cluster: é o teste do QA sobre o que foi publicado na homologação. Sem versão pedida,
/// a última. O resultado diz qual versão rodou, porque "a última" muda quando alguém publica.
/// </para>
/// </remarks>
public class CamundaStartInstanceStep : ITestStep
{
	internal const string ChaveDaInstancia = "_CamundaProcessInstanceKey";

	/// <inheritdoc />
	public string? Name { get; internal set; }

	/// <inheritdoc />
	public string StepType => "Camunda";

	/// <inheritdoc />
	public ITestStepResult? Result { get; private set; }

	/// <inheritdoc />
	public bool IsExecuted => Result != null;

	/// <inheritdoc />
	public bool IsValid { get; private set; }

	/// <summary>O id do processo no BPMN.</summary>
	public string ProcessId { get; init; } = string.Empty;

	/// <summary>As variáveis com que a instância nasce: um objeto anônimo, uma classe ou um dicionário.</summary>
	public object? Variables { get; init; }

	/// <summary>A versão já implantada no cluster, como ele a numerou. Nulo: a última.</summary>
	public int? Version { get; init; }

	/// <summary>
	/// O rótulo de versão (<c>zeebe:versionTag</c>) já implantado no cluster. Se mais de uma versão
	/// tem o rótulo, a mais nova delas.
	/// </summary>
	public string? VersionTag { get; init; }

	/// <inheritdoc />
	public async Task<ITestStepResult> ExecuteAsync(ITestContext context, CancellationToken cancellationToken = default)
	{
		var cronometro = Stopwatch.StartNew();
		var diagnostico = new Dictionary<string, object?> { ["ProcessId"] = ProcessId };

		try
		{
			var settings = CamundaSettings.De(context);
			var gateway = Gateways.De(context, settings);

			var (definicao, erro) = await Escolher(context, gateway, settings, cancellationToken).ConfigureAwait(false);
			if (definicao is null)
			{
				Result = CamundaStepResult.CreateFailure(erro!, null, diagnostico, cronometro.Elapsed);
				IsValid = false;
				return Result;
			}

			diagnostico["DefinitionKey"] = definicao.DefinitionKey;
			diagnostico["Version"] = definicao.Version;

			var chave = await gateway.CreateInstanceAsync(definicao.DefinitionKey, Variables, settings.TenantId, cancellationToken)
				.ConfigureAwait(false);

			context.SetProperty(ChaveDaInstancia, chave);

			var mocks = new MocksDaInstancia(
				gateway, chave, MocksDaCadeia.Todos(context), settings,
				MocksDeElemento.UserTasks(context), MocksDeElemento.Timers(context), definicao.DefinitionKey,
				MocksDeElemento.Mensagens(context), MocksDeElemento.Sinais(context), MocksDeElemento.Filhos(context),
				filhosPorProcesso: MocksDeElemento.FilhosPorProcesso(context));
			context.SetProperty(MocksDaInstancia.ChaveNoContexto, mocks);
			mocks.Iniciar();

			Result = CamundaStepResult.CreateStartSuccess(
				ProcessId, chave, cronometro.Elapsed, definicao.DefinitionKey, definicao.Version, definicao.VersionTag,
				CamundaDeployStep.Avisos(context).ToArray());
			IsValid = true;
			return Result;
		}
		catch (Exception ex)
		{
			var erro = ex is CamundaRecusouException recusa
				? $"Camunda rejected starting '{ProcessId}': {recusa.Codigo}: {recusa.Message}"
				: $"Could not start '{ProcessId}': {ex.Message}";

			Result = CamundaStepResult.CreateFailure(erro, ex, diagnostico, cronometro.Elapsed);
			IsValid = false;
			return Result;
		}
	}

	/// <inheritdoc />
	public void Validate(Action<ITestStepResult> validation)
	{
		if (Result == null)
			throw new InvalidOperationException("Step has not been executed. Execute the chain before validating.");

		validation(Result);
		IsValid = true;
	}

	private async Task<(DefinicaoVista? Definicao, string? Erro)> Escolher(
		ITestContext context, ICamundaGateway gateway, CamundaSettings settings, CancellationToken ct)
	{
		// Um deploy que falhou não pode virar, em silêncio, o teste da versão que já estava lá.
		var falhaDoDeploy = context.GetProperty<string>(CamundaDeployStep.ChaveDaFalha);
		if (falhaDoDeploy is not null)
			return (null, $"Process '{ProcessId}' was not started because the deployment failed. {falhaDoDeploy}");

		var pediuVersao = Version is not null || VersionTag is not null;
		var definicoes = CamundaDeployStep.Definicoes(context);

		if (!pediuVersao && definicoes.Count > 0)
		{
			if (definicoes.TryGetValue(ProcessId, out var implantado))
				return (new DefinicaoVista(implantado.ProcessId, implantado.DefinitionKey, implantado.Version, null), null);

			return (null,
				$"Process '{ProcessId}' was not deployed in this chain (it deployed {string.Join(", ", definicoes.Keys.Select(k => $"'{k}'"))}). " +
				"Add it to Deploy(...): a chain that deploys starts only what it deployed, so that another version on a shared " +
				"cluster never answers for the test. To start the version already on the cluster, use a chain without Deploy.");
		}

		var achadas = await gateway.SearchProcessDefinitionsAsync(ProcessId, Version, VersionTag, onlyLatest: true, settings.TenantId, ct)
			.ConfigureAwait(false);

		var escolhida = achadas.OrderByDescending(d => d.Version).FirstOrDefault();
		if (escolhida is not null)
			return (escolhida, null);

		return (null, await NaoEncontrada(gateway, settings, ct).ConfigureAwait(false));
	}

	private async Task<string> NaoEncontrada(ICamundaGateway gateway, CamundaSettings settings, CancellationToken ct)
	{
		var pedida = Version is not null ? $"version {Version} of process '{ProcessId}'"
			: VersionTag is not null ? $"version tag '{VersionTag}' of process '{ProcessId}'"
			: $"process '{ProcessId}'";

		var endereco = settings.RestAddress ?? Environment.GetEnvironmentVariable("CAMUNDA_REST_ADDRESS") ?? "the cluster";
		var mensagem = $"Could not start {pedida}: it is not deployed on {endereco}.";

		var todas = await gateway.SearchProcessDefinitionsAsync(ProcessId, null, null, onlyLatest: false, settings.TenantId, ct)
			.ConfigureAwait(false);

		if (todas.Count > 0)
		{
			var recentes = todas.OrderByDescending(d => d.Version).Take(10)
				.Select(d => d.VersionTag is null ? $"{d.Version}" : $"{d.Version} (tag '{d.VersionTag}')");
			mensagem += $" Versions there, newest first: {string.Join(", ", recentes)}.";
		}
		else
		{
			mensagem += " Deploy it first, or call Deploy(...) in this chain to test the BPMN of the repository.";
		}

		return mensagem + " A process deployed a moment ago may take a few seconds to show in the search.";
	}
}
