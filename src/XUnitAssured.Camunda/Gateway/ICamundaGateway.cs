using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using XUnitAssured.Camunda.Results;

namespace XUnitAssured.Camunda.Gateway;

/// <summary>
/// Tudo o que o pacote pede ao cluster, nos termos do pacote.
/// </summary>
/// <remarks>
/// <para>
/// O SDK oficial só aparece na implementação. Duas razões: a linha estável dele começou em
/// 2026-10-14 e pode precisar ser trocada por um cliente REST próprio sem mexer no DSL; e os
/// testes do pacote substituem o cluster por um falso que responde estas perguntas.
/// </para>
/// <para>
/// As chaves são texto. O Camunda as expõe como inteiros de 64 bits serializados em texto, e o
/// SDK como tipos próprios; texto é o que um teste lê, imprime e cola no Operate.
/// </para>
/// </remarks>
internal interface ICamundaGateway
{
	Task<IReadOnlyList<DeployedProcess>> DeployAsync(
		IReadOnlyList<RecursoBpmn> recursos, CancellationToken ct);

	/// <summary>
	/// As versões do processo que já estão no cluster. Sem versão nem rótulo e com
	/// <paramref name="onlyLatest"/>, só a última; sem nada, todas.
	/// </summary>
	Task<IReadOnlyList<DefinicaoVista>> SearchProcessDefinitionsAsync(
		string processId, int? version, string? versionTag, bool onlyLatest, string? tenantId, CancellationToken ct);

	Task<string> CreateInstanceAsync(string definitionKey, object? variables, string? tenantId, CancellationToken ct);

	Task<IReadOnlyList<JobPendente>> SearchCreatedJobsAsync(
		IReadOnlyCollection<string> processInstanceKeys, IReadOnlyCollection<string>? types, CancellationToken ct);

	Task CompleteJobAsync(string jobKey, object? variables, CancellationToken ct);

	Task ThrowBpmnErrorAsync(string jobKey, string errorCode, string? errorMessage, object? variables, CancellationToken ct);

	/// <summary>Falha o job sem tentativas restantes: o Camunda abre um incident nele.</summary>
	Task FailJobAsync(string jobKey, string errorMessage, CancellationToken ct);

	Task<IReadOnlyList<InstanciaVista>> SearchChildInstancesAsync(
		IReadOnlyCollection<string> parentKeys, CancellationToken ct);

	Task<InstanciaVista?> GetInstanceAsync(string processInstanceKey, CancellationToken ct);

	Task<IReadOnlyList<ElementoVisto>> SearchElementsAsync(string processInstanceKey, CancellationToken ct);

	Task<IReadOnlyList<VariavelVista>> SearchVariablesAsync(string processInstanceKey, string? name, CancellationToken ct);

	Task<IReadOnlyList<IncidenteVisto>> SearchActiveIncidentsAsync(string processInstanceKey, CancellationToken ct);

	Task CancelInstanceAsync(string processInstanceKey, CancellationToken ct);

	/// <summary>
	/// O que os elementos da árvore esperam: user task, mensagem, sinal, job. Timer de borda e timer
	/// depois de gateway de evento não aparecem aqui (medido na 8.10.4); esses saem do BPMN.
	/// </summary>
	Task<IReadOnlyList<EsperaVista>> SearchWaitStatesAsync(string rootProcessInstanceKey, CancellationToken ct);

	Task CompleteUserTaskAsync(string userTaskKey, object? variables, CancellationToken ct);

	/// <summary>
	/// Encerra elementos e ativa outros, numa modificação só. O <c>ancestorElementInstanceKey</c> é o
	/// escopo onde ativar, quando o elemento está dentro de um subprocesso.
	/// </summary>
	Task ModifyInstanceAsync(
		string processInstanceKey,
		IReadOnlyList<string> terminateElementInstanceKeys,
		IReadOnlyList<string> activateElementIds,
		string? ancestorElementInstanceKey,
		object? variables,
		CancellationToken ct);

	/// <summary>
	/// As assinaturas de mensagem ainda abertas. Com <paramref name="processInstanceKeys"/>, só as dessas
	/// instâncias; sem, as do cluster inteiro, para ver quem mais espera a mesma mensagem e chave.
	/// </summary>
	/// <remarks>Ao contrário da busca de esperas, esta mostra a borda de mensagem (medido na 8.10.4).</remarks>
	Task<IReadOnlyList<AssinaturaVista>> SearchOpenMessageSubscriptionsAsync(
		IReadOnlyCollection<string>? processInstanceKeys,
		IReadOnlyCollection<string> messageNames,
		string? correlationKey,
		CancellationToken ct);

	/// <summary>Correlaciona a mensagem e devolve a instância que a recebeu.</summary>
	Task<string> CorrelateMessageAsync(
		string messageName, string correlationKey, object? variables, string? tenantId, CancellationToken ct);

	/// <summary>O XML da versão implantada.</summary>
	Task<string> GetProcessDefinitionXmlAsync(string definitionKey, CancellationToken ct);
}

/// <summary>Um arquivo para implantar: o nome que o cluster vai mostrar e o conteúdo.</summary>
internal sealed record RecursoBpmn(string Nome, byte[] Conteudo);

/// <summary>Uma versão de um processo, como a busca de definições a mostra.</summary>
internal sealed record DefinicaoVista(string ProcessId, string DefinitionKey, int Version, string? VersionTag);

/// <summary>Um job que espera ser resolvido.</summary>
internal sealed record JobPendente(string JobKey, string Type, string ElementId, string ProcessInstanceKey);

/// <summary>Uma instância como a busca a mostra.</summary>
internal sealed record InstanciaVista(
	string ProcessInstanceKey, string ProcessId, string State, bool HasIncident, string? ParentProcessInstanceKey,
	string? ProcessDefinitionKey = null, string? ParentElementInstanceKey = null);

/// <summary>Um elemento de uma instância, como a busca o mostra.</summary>
internal sealed record ElementoVisto(
	string ElementId, string ElementName, string Type, string State, string ElementInstanceKey = "");

/// <summary>
/// Um ponto de espera de um elemento. <see cref="Tipo"/> é o que o Camunda responde: <c>USER_TASK</c>,
/// <c>MESSAGE</c>, <c>SIGNAL</c>, <c>JOB</c>, <c>TIMER</c>...
/// </summary>
internal sealed record EsperaVista(
	string ProcessInstanceKey,
	string ElementInstanceKey,
	string ElementId,
	string Tipo,
	string? UserTaskKey = null,
	string? MessageName = null,
	string? CorrelationKey = null,
	string? SignalName = null);

/// <summary>Uma assinatura de mensagem: um elemento esperando a mensagem com esta chave.</summary>
internal sealed record AssinaturaVista(
	string ProcessInstanceKey, string ElementId, string ElementInstanceKey, string MessageName, string CorrelationKey);

/// <summary>Uma variável, com o valor em JSON como o Camunda guarda.</summary>
internal sealed record VariavelVista(string Name, string ValueJson, string ScopeKey);

/// <summary>Um incident aberto.</summary>
internal sealed record IncidenteVisto(string ElementId, string ErrorType, string ErrorMessage);

/// <summary>
/// O cluster recusou um comando. <see cref="Codigo"/> é o que ele respondeu, por exemplo
/// <c>INVALID_STATE</c> ou <c>NOT_FOUND</c>.
/// </summary>
internal sealed class CamundaRecusouException : Exception
{
	public CamundaRecusouException(string codigo, string mensagem, Exception? interna = null)
		: base(mensagem, interna) => Codigo = codigo;

	public string Codigo { get; }
}
