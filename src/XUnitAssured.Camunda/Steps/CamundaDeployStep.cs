using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using XUnitAssured.Camunda.Gateway;
using XUnitAssured.Camunda.Results;
using XUnitAssured.Core.Abstractions;
using XUnitAssured.Core.Results;

namespace XUnitAssured.Camunda.Steps;

/// <summary>
/// Implanta arquivos BPMN (e DMN, formulários) num deploy só.
/// </summary>
/// <remarks>
/// <para>
/// <b>Um deploy só, de propósito.</b> Uma <i>call activity</i> com <c>bindingType="deployment"</c>
/// chama o filho implantado junto com o pai, e o Camunda recusa o deploy de um pai sem o filho
/// ao lado. É também o que mantém o teste longe da versão de outra pessoa num cluster
/// compartilhado. Implantar o mesmo conteúdo de novo não cria versão: o cluster devolve a que já
/// existe, então implantar em todo teste custa uma ida e volta e nada mais.
/// </para>
/// <para>
/// Os processos implantados ficam no contexto, e é por eles que o <c>StartInstance</c> inicia a
/// instância: pela chave desta definição, e não pela última versão do <c>processId</c>.
/// </para>
/// </remarks>
public class CamundaDeployStep : ITestStep
{
	internal const string ChaveDasDefinicoes = "_CamundaDefinicoes";
	internal const string ChaveDaFalha = "_CamundaFalhaDoDeploy";

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

	/// <summary>
	/// Os arquivos. Um caminho relativo é procurado na pasta de saída do teste e depois na pasta atual.
	/// </summary>
	public IReadOnlyList<string> Resources { get; init; } = Array.Empty<string>();

	/// <inheritdoc />
	public async Task<ITestStepResult> ExecuteAsync(ITestContext context, CancellationToken cancellationToken = default)
	{
		var cronometro = Stopwatch.StartNew();
		var diagnostico = new Dictionary<string, object?> { ["Resources"] = string.Join(", ", Resources) };

		try
		{
			var recursos = Resources.Select(Ler).ToList();
			var gateway = Gateways.De(context, CamundaSettings.De(context));

			var processos = await gateway.DeployAsync(recursos, cancellationToken).ConfigureAwait(false);

			var definicoes = Definicoes(context);
			foreach (var processo in processos)
				definicoes[processo.ProcessId] = processo;

			Result = CamundaStepResult.CreateDeploySuccess(processos, cronometro.Elapsed);
			IsValid = true;
			return Result;
		}
		catch (Exception ex)
		{
			var erro = ex is CamundaRecusouException recusa
				? $"Camunda rejected the deployment of {string.Join(", ", Resources.Select(Path.GetFileName))}: {recusa.Codigo}: {recusa.Message}"
				: $"Could not deploy {string.Join(", ", Resources.Select(Path.GetFileName))}: {ex.Message}";

			context.SetProperty(ChaveDaFalha, erro);
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

	internal static Dictionary<string, DeployedProcess> Definicoes(ITestContext context)
	{
		var definicoes = context.GetProperty<Dictionary<string, DeployedProcess>>(ChaveDasDefinicoes);
		if (definicoes is null)
		{
			definicoes = new Dictionary<string, DeployedProcess>(StringComparer.Ordinal);
			context.SetProperty(ChaveDasDefinicoes, definicoes);
		}

		return definicoes;
	}

	internal static string Localizar(string caminho)
	{
		if (Path.IsPathRooted(caminho))
			return caminho;

		var naSaida = Path.Combine(AppContext.BaseDirectory, caminho);
		if (File.Exists(naSaida))
			return naSaida;

		return Path.GetFullPath(caminho);
	}

	private static RecursoBpmn Ler(string caminho)
	{
		var arquivo = Localizar(caminho);
		if (!File.Exists(arquivo))
			throw new FileNotFoundException(
				$"BPMN file '{caminho}' not found in the test output folder ({AppContext.BaseDirectory}) " +
				"or the current folder. Copy it to the output with <None Include=\"...\" CopyToOutputDirectory=\"PreserveNewest\" />.",
				caminho);

		return new RecursoBpmn(Path.GetFileName(arquivo), File.ReadAllBytes(arquivo));
	}
}
