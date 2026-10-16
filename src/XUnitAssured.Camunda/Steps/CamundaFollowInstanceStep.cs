using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using XUnitAssured.Camunda.Gateway;
using XUnitAssured.Camunda.Mocks;
using XUnitAssured.Camunda.Results;
using XUnitAssured.Core.Abstractions;
using XUnitAssured.Core.Results;

namespace XUnitAssured.Camunda.Steps;

/// <summary>
/// Acompanha uma instância que outro sistema iniciou, como a API testada, e põe os mocks para
/// trabalhar nela.
/// </summary>
/// <remarks>
/// <para>
/// Pela chave, quando quem iniciou a devolve; ou pelo processo e uma variável, quando não devolve
/// (o <c>orderId</c> que o teste mandou à API). A busca fica atrás do motor, e a instância pode nascer
/// um instante depois da resposta da API: o passo pergunta até <c>assertionTimeoutSeconds</c>.
/// </para>
/// <para>
/// Pela variável, se mais de uma instância tiver o valor, o passo segue a mais nova e diz isso em
/// <c>Warnings</c>: use um valor só do teste.
/// </para>
/// <para>
/// A instância seguida é tratada como a de um <c>StartInstance</c>: se ainda estiver ativa quando o
/// processo de teste terminar, é cancelada.
/// </para>
/// </remarks>
public class CamundaFollowInstanceStep : ITestStep
{
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

	/// <summary>A chave da instância, quando quem a iniciou a devolve.</summary>
	public string? ProcessInstanceKey { get; init; }

	/// <summary>O id do processo, para achar a instância pela variável.</summary>
	public string? ProcessId { get; init; }

	/// <summary>O nome da variável que identifica a instância.</summary>
	public string? VariableName { get; init; }

	/// <summary>O valor dela, comparado como JSON.</summary>
	public object? VariableValue { get; init; }

	/// <inheritdoc />
	public async Task<ITestStepResult> ExecuteAsync(ITestContext context, CancellationToken cancellationToken = default)
	{
		var cronometro = Stopwatch.StartNew();
		var procurada = Descrever();
		var diagnostico = new Dictionary<string, object?> { ["Following"] = procurada };

		try
		{
			var settings = CamundaSettings.De(context);
			var gateway = Gateways.De(context, settings);
			var prazo = TimeSpan.FromSeconds(Math.Max(1, settings.AssertionTimeoutSeconds));
			var intervalo = TimeSpan.FromMilliseconds(Math.Max(10, settings.PollIntervalMilliseconds));
			var avisos = new List<string>();

			InstanciaVista? instancia = null;
			while (true)
			{
				if (ProcessInstanceKey is not null)
				{
					instancia = await gateway.GetInstanceAsync(ProcessInstanceKey, cancellationToken).ConfigureAwait(false);
				}
				else
				{
					var achadas = await gateway.SearchInstancesByVariableAsync(
						ProcessId!, VariableName!, JsonSerializer.Serialize(VariableValue), cancellationToken).ConfigureAwait(false);
					instancia = achadas.FirstOrDefault();

					if (achadas.Count > 1)
						avisos.Add(
							$"{achadas.Count} instances of '{ProcessId}' have {VariableName} = {JsonSerializer.Serialize(VariableValue)}; " +
							$"following the newest, {instancia!.ProcessInstanceKey}. Use a value unique to the test.");
				}

				if (instancia is not null || cronometro.Elapsed >= prazo)
					break;

				await Task.Delay(intervalo, cancellationToken).ConfigureAwait(false);
			}

			if (instancia is null)
			{
				Result = CamundaStepResult.CreateFailure(
					$"No {procurada} showed up on the cluster within {prazo.TotalSeconds:0.#}s. Check that the system under test " +
					"started it, and on this cluster.",
					null, diagnostico, cronometro.Elapsed);
				IsValid = false;
				return Result;
			}

			context.SetProperty(CamundaStartInstanceStep.ChaveDaInstancia, instancia.ProcessInstanceKey);

			var mocks = new MocksDaInstancia(
				gateway, instancia.ProcessInstanceKey, MocksDaCadeia.Todos(context), settings,
				MocksDeElemento.UserTasks(context), MocksDeElemento.Timers(context), instancia.ProcessDefinitionKey,
				MocksDeElemento.Mensagens(context), MocksDeElemento.Sinais(context), MocksDeElemento.Filhos(context),
				filhosPorProcesso: MocksDeElemento.FilhosPorProcesso(context));
			context.SetProperty(MocksDaInstancia.ChaveNoContexto, mocks);
			mocks.Iniciar();

			Result = CamundaStepResult.CreateStartSuccess(
				instancia.ProcessId, instancia.ProcessInstanceKey, cronometro.Elapsed,
				instancia.ProcessDefinitionKey, instancia.Version, instancia.VersionTag,
				CamundaDeployStep.Avisos(context).Concat(avisos).ToArray());
			IsValid = true;
			return Result;
		}
		catch (Exception ex)
		{
			var erro = ex is CamundaRecusouException recusa
				? $"Camunda rejected looking for the {procurada}: {recusa.Codigo}: {recusa.Message}"
				: $"Could not follow the {procurada}: {ex.Message}";

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

	private string Descrever() => ProcessInstanceKey is not null
		? $"process instance {ProcessInstanceKey}"
		: $"instance of '{ProcessId}' with {VariableName} = {JsonSerializer.Serialize(VariableValue)}";
}
