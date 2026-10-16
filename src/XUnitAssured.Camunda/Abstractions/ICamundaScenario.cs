using System;
using System.Threading;
using System.Threading.Tasks;

using XUnitAssured.Camunda.Extensions;
using XUnitAssured.Camunda.Results;
using XUnitAssured.Core.Abstractions;

namespace XUnitAssured.Camunda.Abstractions;

/// <summary>
/// Uma cadeia que está descrevendo um processo no Camunda.
/// </summary>
/// <remarks>
/// Os verbos moram no tipo, e não como extensão de <see cref="ITestScenario"/>, pela convenção do
/// README: só os verbos de entrada (<c>Deploy</c>, <c>MockJobWorker</c>, <c>StartInstance</c>)
/// tomam o cenário não tipado, e eles não colidem com <c>ApiResource</c>, <c>Topic</c> nem
/// <c>Queue</c>, então uma cadeia pode chamar a API, iniciar o processo e consumir o evento.
/// </remarks>
public interface ICamundaScenario : ITestScenario
{
	/// <inheritdoc cref="ITestScenario.And" />
	new ICamundaScenario And();

	/// <inheritdoc cref="ITestScenario.On" />
	new ICamundaScenario On();

	/// <inheritdoc cref="ITestScenario.When" />
	new ICamundaScenario When();

	/// <inheritdoc cref="ITestScenario.Then" />
	new ICamundaScenario Then();

	/// <summary>
	/// Implanta os arquivos num deploy só. Ponha o processo e os que ele chama juntos.
	/// </summary>
	/// <param name="resources">Caminhos dos arquivos, relativos à pasta de saída do teste.</param>
	ICamundaScenario Deploy(params string[] resources);

	/// <summary>
	/// Responde no lugar do worker de <paramref name="jobType"/>, só nas instâncias desta cadeia.
	/// </summary>
	/// <remarks>
	/// Vale para a instância e para as que ela chamar (call activities). Diga o que o mock
	/// responde com <c>ThenComplete</c> ou <c>ThenThrowBpmnError</c>.
	/// </remarks>
	/// <param name="jobType">O <c>type</c> da task, como no <c>zeebe:taskDefinition</c> do BPMN.</param>
	ICamundaJobMock MockJobWorker(string jobType);

	/// <summary>
	/// Inicia uma instância do processo que esta cadeia implantou.
	/// </summary>
	/// <param name="processId">O id do processo no BPMN.</param>
	/// <param name="variables">As variáveis iniciais: objeto anônimo, classe ou dicionário.</param>
	ICamundaScenario StartInstance(string processId, object? variables = null);

	/// <summary>Registra uma verificação sobre o resultado do passo.</summary>
	ICamundaScenario Validate(Action<CamundaStepResult> validation);

	/// <summary>Roda a cadeia e devolve as asserções.</summary>
	Task<CamundaValidationBuilder> ExecuteAsync(CancellationToken cancellationToken = default);

	/// <summary>
	/// Roda a cadeia e espera por ela.
	/// </summary>
	/// <remarks>
	/// Invólucro fino sobre <see cref="ExecuteAsync"/>, para uma suíte escrita na forma síncrona.
	/// </remarks>
	CamundaValidationBuilder Execute();
}

/// <summary>
/// Um mock declarado que ainda não disse o que responde.
/// </summary>
/// <remarks>
/// Não é um <see cref="ICamundaScenario"/> de propósito: a cadeia só continua depois de um
/// <c>Then...</c>, então esquecer a resposta é erro de compilação, e não um mock que faz algo
/// que ninguém escolheu.
/// </remarks>
public interface ICamundaJobMock
{
	/// <summary>Completa o job, com estas variáveis, como o worker faria.</summary>
	/// <param name="variables">O que o worker devolveria: objeto anônimo, classe ou dicionário. Nulo: nada.</param>
	ICamundaScenario ThenComplete(object? variables = null);

	/// <summary>
	/// Lança um erro BPMN no job, como um worker que recusa o trabalho por regra de negócio.
	/// </summary>
	/// <remarks>Uma boundary event de erro com este código pega; sem uma, a instância abre um incident.</remarks>
	/// <param name="errorCode">O código, como no <c>errorCode</c> do <c>bpmn:error</c>.</param>
	/// <param name="errorMessage">A mensagem, para o Operate.</param>
	/// <param name="variables">Variáveis enviadas junto com o erro.</param>
	ICamundaScenario ThenThrowBpmnError(string errorCode, string? errorMessage = null, object? variables = null);
}
