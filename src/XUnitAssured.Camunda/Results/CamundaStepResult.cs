using System;
using System.Collections.Generic;

using XUnitAssured.Core.Results;

namespace XUnitAssured.Camunda.Results;

/// <summary>
/// O que um passo de Camunda deixa para ser verificado.
///
/// <para>
/// As propriedades existem sempre, ainda que vazias, pela mesma decisão de contrato dos pacotes
/// Kafka e RabbitMq: um teste afirma sobre o comportamento, e não sobre a presença de uma chave.
/// </para>
/// </summary>
public class CamundaStepResult : TestStepResult
{
	/// <summary>
	/// A instância que o passo iniciou, ou nulo num passo que não inicia instância.
	/// </summary>
	/// <remarks>É a chave que o Operate mostra: cole em <c>/operate/processes/{chave}</c>.</remarks>
	public string? ProcessInstanceKey => GetProperty<string>("ProcessInstanceKey");

	/// <summary>O processo da instância iniciada, ou nulo num passo que não inicia instância.</summary>
	public string? ProcessId => GetProperty<string>("ProcessId");

	/// <summary>A chave da versão da definição de onde a instância nasceu.</summary>
	public string? DefinitionKey => GetProperty<string>("DefinitionKey");

	/// <summary>
	/// A versão do processo que a instância roda, como o cluster a numerou. Nulo num passo que não
	/// inicia instância.
	/// </summary>
	/// <remarks>
	/// Sem versão pedida e sem <c>Deploy</c> na cadeia, é a última que estava no cluster: diz qual
	/// diagrama o teste exercitou de fato.
	/// </remarks>
	public int? Version => GetProperty<int?>("Version");

	/// <summary>O rótulo de versão (<c>zeebe:versionTag</c>) dessa versão, quando o cluster o conhece.</summary>
	public string? VersionTag => GetProperty<string>("VersionTag");

	/// <summary>Os processos que o deploy registrou. Vazio num passo que não implanta.</summary>
	public IReadOnlyList<DeployedProcess> DeployedProcesses =>
		GetProperty<IReadOnlyList<DeployedProcess>>("DeployedProcesses") ?? Array.Empty<DeployedProcess>();

	/// <summary>Resultado de um deploy que o cluster aceitou.</summary>
	public static CamundaStepResult CreateDeploySuccess(
		IReadOnlyList<DeployedProcess> processes, TimeSpan? elapsed = null) =>
		new()
		{
			Success = true,
			Errors = Array.Empty<string>(),
			Data = processes,
			DataType = typeof(IReadOnlyList<DeployedProcess>),
			Metadata = Metadados(StepStatus.Succeeded, elapsed),
			Properties = Propriedades(null, null, processes)
		};

	/// <summary>Resultado de uma instância que o cluster criou.</summary>
	public static CamundaStepResult CreateStartSuccess(
		string processId,
		string processInstanceKey,
		TimeSpan? elapsed = null,
		string? definitionKey = null,
		int? version = null,
		string? versionTag = null)
	{
		var propriedades = Propriedades(processId, processInstanceKey, Array.Empty<DeployedProcess>());
		propriedades["DefinitionKey"] = definitionKey;
		propriedades["Version"] = version;
		propriedades["VersionTag"] = versionTag;

		return new()
		{
			Success = true,
			Errors = Array.Empty<string>(),
			Data = processInstanceKey,
			DataType = typeof(string),
			Metadata = Metadados(StepStatus.Succeeded, elapsed),
			Properties = propriedades
		};
	}

	/// <summary>
	/// Resultado de uma falha, com o diagnóstico que o passo juntou.
	/// </summary>
	public static CamundaStepResult CreateFailure(
		string error,
		Exception? exception = null,
		IDictionary<string, object?>? diagnostics = null,
		TimeSpan? elapsed = null)
	{
		var propriedades = Propriedades(null, null, Array.Empty<DeployedProcess>());
		propriedades["ExceptionType"] = exception?.GetType().FullName;
		propriedades["ExceptionDetail"] = exception?.ToString();

		if (diagnostics != null)
		{
			foreach (var par in diagnostics)
				propriedades[par.Key] = par.Value;
		}

		return new CamundaStepResult
		{
			Success = false,
			Errors = new[] { error },
			DataType = typeof(string),
			Metadata = Metadados(StepStatus.Failed, elapsed),
			Properties = propriedades
		};
	}

	private static Dictionary<string, object?> Propriedades(
		string? processId, string? processInstanceKey, IReadOnlyList<DeployedProcess> processes) =>
		new()
		{
			["ProcessId"] = processId,
			["ProcessInstanceKey"] = processInstanceKey,
			["DeployedProcesses"] = processes
		};

	private static StepMetadata Metadados(StepStatus status, TimeSpan? elapsed) => new()
	{
		StartedAt = DateTimeOffset.UtcNow - (elapsed ?? TimeSpan.Zero),
		CompletedAt = DateTimeOffset.UtcNow,
		Status = status
	};
}

/// <summary>Um processo que um deploy registrou no cluster.</summary>
/// <param name="ProcessId">O id do processo no BPMN, por exemplo <c>pedido</c>.</param>
/// <param name="DefinitionKey">A chave desta versão da definição, que identifica exatamente o que foi implantado.</param>
/// <param name="Version">A versão que o cluster deu. Um BPMN idêntico ao já implantado não ganha versão nova.</param>
/// <param name="ResourceName">O arquivo de onde o processo veio.</param>
public sealed record DeployedProcess(string ProcessId, string DefinitionKey, int Version, string ResourceName);
