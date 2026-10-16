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
	/// Implanta os arquivos do repositório num deploy só. Ponha o processo e os que ele chama juntos.
	/// </summary>
	/// <remarks>
	/// Cada argumento é um caminho (<c>bpmn/pedido.bpmn</c>) ou o id de um processo (<c>pedido</c>).
	/// Com o id, o arquivo é o <c>.bpmn</c> da pasta de saída do teste que declara esse processo.
	/// Implantar o mesmo conteúdo de novo não cria versão.
	/// </remarks>
	/// <param name="resources">Caminhos relativos à pasta de saída do teste, ou ids de processo.</param>
	ICamundaScenario Deploy(params string[] resources);

	/// <summary>
	/// Responde no lugar do worker de <paramref name="jobType"/>, só nas instâncias desta cadeia.
	/// </summary>
	/// <remarks>
	/// Vale para a instância e para as que ela chamar (call activities). Diga o que o mock
	/// responde com <c>ThenComplete</c> ou <c>ThenThrowBpmnError</c>. Num ambiente com workers de
	/// verdade, como a homologação, mocke só os tipos que não têm worker lá: o Camunda entrega o job
	/// a quem pedir primeiro.
	/// </remarks>
	/// <param name="jobType">O <c>type</c> da task, como no <c>zeebe:taskDefinition</c> do BPMN.</param>
	ICamundaJobMock MockJobWorker(string jobType);

	/// <summary>
	/// Responde no lugar da pessoa a user task <paramref name="elementId"/>, só nas instâncias desta cadeia.
	/// </summary>
	/// <remarks>
	/// Vale toda vez que a instância, ou uma que ela chamar, chegar nessa task. Só para user tasks do
	/// Camunda (<c>zeebe:userTask</c>); as antigas, feitas de job, se mockam pelo tipo com
	/// <c>MockJobWorker("io.camunda.zeebe:userTask")</c>.
	/// </remarks>
	/// <param name="elementId">O id da user task no BPMN.</param>
	ICamundaUserTaskMock MockUserTask(string elementId);

	/// <summary>
	/// Pula o timer <paramref name="elementId"/> quando a instância chegar nele, só nas instâncias desta cadeia.
	/// </summary>
	/// <remarks>
	/// <para>
	/// O relógio do cluster é de todos, então o pacote não o adianta: modifica a instância, encerrando a
	/// espera e ativando o que vem depois do timer. Vale para o timer intermediário, o timer depois de
	/// um gateway de evento e o timer de borda (a borda que interrompe encerra a atividade; a que não
	/// interrompe a deixa ativa).
	/// </para>
	/// <para>
	/// Um timer que desemboca num gateway de junção é recusado: ativado por modificação, o gateway
	/// dispara sem esperar os outros ramos. Nesse caso, ponha a duração numa variável e use <c>PT1S</c>.
	/// </para>
	/// </remarks>
	/// <param name="elementId">O id do evento de timer no BPMN (o da borda, para um timer de borda).</param>
	ICamundaScenario SkipTimer(string elementId);

	/// <summary>
	/// Inicia uma instância do processo.
	/// </summary>
	/// <remarks>
	/// Numa cadeia com <c>Deploy</c>, a versão que a cadeia implantou. Sem <c>Deploy</c>, ou com
	/// <paramref name="version"/> ou <paramref name="versionTag"/>, a que já está no cluster (sem
	/// versão pedida, a última). O resultado diz qual versão rodou.
	/// </remarks>
	/// <param name="processId">O id do processo no BPMN.</param>
	/// <param name="variables">As variáveis iniciais: objeto anônimo, classe ou dicionário.</param>
	/// <param name="version">A versão já implantada, como o cluster a numerou (1, 2, 3...).</param>
	/// <param name="versionTag">O rótulo de versão já implantado (<c>zeebe:versionTag</c>), por exemplo <c>1.4.0</c>.</param>
	ICamundaScenario StartInstance(string processId, object? variables = null, int? version = null, string? versionTag = null);

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
/// Uma user task mockada que ainda não disse o que responde.
/// </summary>
public interface ICamundaUserTaskMock
{
	/// <summary>Completa a task, com estas variáveis, como a pessoa faria no Tasklist.</summary>
	/// <param name="variables">O que o formulário devolveria: objeto anônimo, classe ou dicionário. Nulo: nada.</param>
	ICamundaScenario ThenComplete(object? variables = null);
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
