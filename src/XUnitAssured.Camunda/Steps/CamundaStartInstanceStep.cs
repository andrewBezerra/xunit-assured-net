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
/// Inicia uma instância do processo implantado nesta cadeia e põe os mocks para trabalhar nela.
/// </summary>
/// <remarks>
/// A instância é criada pela chave da definição que o <c>Deploy</c> da cadeia registrou, e não
/// pela última versão do <c>processId</c>: num cluster compartilhado, a última versão pode ser a
/// que outra pessoa acabou de implantar. Por isso iniciar um processo que a cadeia não implantou
/// é erro, e não um atalho.
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

	/// <inheritdoc />
	public async Task<ITestStepResult> ExecuteAsync(ITestContext context, CancellationToken cancellationToken = default)
	{
		var cronometro = Stopwatch.StartNew();
		var diagnostico = new Dictionary<string, object?> { ["ProcessId"] = ProcessId };

		try
		{
			var definicoes = CamundaDeployStep.Definicoes(context);
			if (!definicoes.TryGetValue(ProcessId, out var definicao))
			{
				var falhaDoDeploy = context.GetProperty<string>(CamundaDeployStep.ChaveDaFalha);
				var erro = falhaDoDeploy is not null
					? $"Process '{ProcessId}' was not started because the deployment failed. {falhaDoDeploy}"
					: $"Process '{ProcessId}' was not deployed in this chain" +
					  (definicoes.Count > 0 ? $" (it deployed {string.Join(", ", definicoes.Keys.Select(k => $"'{k}'"))})" : "") +
					  ". Call Deploy(...) with its BPMN first: the instance is started from the definition this chain " +
					  "deployed, so that another version on a shared cluster never answers for the test.";

				Result = CamundaStepResult.CreateFailure(erro, null, diagnostico, cronometro.Elapsed);
				IsValid = false;
				return Result;
			}

			diagnostico["ProcessDefinitionKey"] = definicao.DefinitionKey;

			var settings = CamundaSettings.De(context);
			var gateway = Gateways.De(context, settings);
			var chave = await gateway.CreateInstanceAsync(definicao.DefinitionKey, Variables, settings.TenantId, cancellationToken)
				.ConfigureAwait(false);

			context.SetProperty(ChaveDaInstancia, chave);

			var mocks = new MocksDaInstancia(gateway, chave, MocksDaCadeia.Todos(context), settings);
			context.SetProperty(MocksDaInstancia.ChaveNoContexto, mocks);
			mocks.Iniciar();

			Result = CamundaStepResult.CreateStartSuccess(ProcessId, chave, cronometro.Elapsed);
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
}
