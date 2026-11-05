using System.Collections.Generic;
using System.Text.Json;
using System.Threading;

namespace XUnitAssured.Camunda;

/// <summary>
/// O job que um mock de <c>ThenCompleteWith</c> vai completar: quem ele é e as variáveis da instância.
/// </summary>
/// <remarks>
/// As variáveis são as do escopo do processo, como a busca do Camunda as mostra quando o job aparece:
/// a busca lê o armazenamento secundário, e um valor muito grande pode vir truncado por ela.
/// </remarks>
public sealed class CamundaJobContext
{
	internal CamundaJobContext(
		string jobKey, string jobType, string elementId, string processInstanceKey,
		IReadOnlyDictionary<string, JsonElement> variables, CancellationToken cancellationToken)
	{
		JobKey = jobKey;
		JobType = jobType;
		ElementId = elementId;
		ProcessInstanceKey = processInstanceKey;
		Variables = variables;
		CancellationToken = cancellationToken;
	}

	/// <summary>A chave do job.</summary>
	public string JobKey { get; }

	/// <summary>O tipo do job: o <c>type</c> do <c>zeebe:taskDefinition</c>.</summary>
	public string JobType { get; }

	/// <summary>O id, no BPMN, do elemento que criou o job.</summary>
	public string ElementId { get; }

	/// <summary>A instância do job: a iniciada pelo teste, ou uma que ela chamou.</summary>
	public string ProcessInstanceKey { get; }

	/// <summary>As variáveis da instância, pelo nome, cada uma como o JSON que o Camunda guarda.</summary>
	public IReadOnlyDictionary<string, JsonElement> Variables { get; }

	/// <summary>Cancelado quando os mocks da instância param.</summary>
	public CancellationToken CancellationToken { get; }
}

/// <summary>
/// A mensagem que um mock de <c>ThenCorrelateWith</c> vai correlacionar: a assinatura que espera por ela
/// e as variáveis da instância que espera.
/// </summary>
/// <remarks>As variáveis são as do escopo do processo, como em <see cref="CamundaJobContext"/>.</remarks>
public sealed class CamundaMessageContext
{
	internal CamundaMessageContext(
		string messageName, string correlationKey, string elementId, string processInstanceKey,
		IReadOnlyDictionary<string, JsonElement> variables, CancellationToken cancellationToken)
	{
		MessageName = messageName;
		CorrelationKey = correlationKey;
		ElementId = elementId;
		ProcessInstanceKey = processInstanceKey;
		Variables = variables;
		CancellationToken = cancellationToken;
	}

	/// <summary>O nome da mensagem, como no <c>bpmn:message</c>.</summary>
	public string MessageName { get; }

	/// <summary>A chave de correlação que a assinatura traz.</summary>
	public string CorrelationKey { get; }

	/// <summary>O id, no BPMN, do elemento que espera a mensagem.</summary>
	public string ElementId { get; }

	/// <summary>A instância que espera a mensagem.</summary>
	public string ProcessInstanceKey { get; }

	/// <summary>As variáveis da instância, pelo nome, cada uma como o JSON que o Camunda guarda.</summary>
	public IReadOnlyDictionary<string, JsonElement> Variables { get; }

	/// <summary>Cancelado quando os mocks da instância param.</summary>
	public CancellationToken CancellationToken { get; }
}
