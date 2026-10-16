using System;
using System.Threading;
using System.Threading.Tasks;

using XUnitAssured.Camunda.Abstractions;
using XUnitAssured.Camunda.Extensions;
using XUnitAssured.Camunda.Results;
using XUnitAssured.Core.Abstractions;
using XUnitAssured.Core.DSL;
using XUnitAssured.Core.Results;

namespace XUnitAssured.Camunda.DSL;

/// <summary>
/// Carrega o tipo de Camunda pela cadeia, delegando o resto ao cenário que envolve.
/// </summary>
/// <remarks>
/// O mesmo desenho do invólucro do RabbitMq: o contexto, o passo atual e a execução pertencem ao
/// cenário original; este tipo só acrescenta ser um <see cref="ICamundaScenario"/>.
/// </remarks>
internal sealed class CamundaScenario : ICamundaScenario, IFronteiraDePasso
{
	private readonly ITestScenario _cenario;

	bool IFronteiraDePasso.ComecaPassoNovo => FronteiraDePasso.ComecaPassoNovo(_cenario);

	private CamundaScenario(ITestScenario cenario) => _cenario = cenario;

	/// <summary>Envolve o cenário, ou devolve o invólucro que ele já é.</summary>
	internal static ICamundaScenario De(ITestScenario cenario) =>
		cenario as ICamundaScenario ?? new CamundaScenario(cenario);

	public ITestContext Context => _cenario.Context;

	public ITestStep? CurrentStep => _cenario.CurrentStep;

	public void SetCurrentStep(ITestStep step) => _cenario.SetCurrentStep(step);

	public void AddValidation(Action<ITestStepResult> validation) => _cenario.AddValidation(validation);

	public Task ExecutePendingAsync(CancellationToken cancellationToken = default) =>
		_cenario.ExecutePendingAsync(cancellationToken);

	public ICamundaScenario And()
	{
		_cenario.And();
		return this;
	}

	public ICamundaScenario On()
	{
		_cenario.On();
		return this;
	}

	public ICamundaScenario When()
	{
		_cenario.When();
		return this;
	}

	public ICamundaScenario Then()
	{
		_cenario.Then();
		return this;
	}

	public ICamundaScenario Deploy(params string[] resources) => CamundaVerbos.Deploy(_cenario, resources);

	public ICamundaJobMock MockJobWorker(string jobType) => CamundaVerbos.MockJobWorker(_cenario, jobType);

	public ICamundaUserTaskMock MockUserTask(string elementId) => CamundaVerbos.MockUserTask(_cenario, elementId);

	public ICamundaScenario SkipTimer(string elementId) => CamundaVerbos.SkipTimer(_cenario, elementId);

	public ICamundaMessageMock MockMessage(string messageName) => CamundaVerbos.MockMessage(_cenario, messageName);

	public ICamundaSignalMock MockSignal(string signalName) => CamundaVerbos.MockSignal(_cenario, signalName);

	public ICamundaChildProcessMock MockChildProcess(string callActivityId) => CamundaVerbos.MockChildProcess(_cenario, callActivityId);

	public ICamundaScenario StartInstance(string processId, object? variables = null, int? version = null, string? versionTag = null) =>
		CamundaVerbos.StartInstance(_cenario, processId, variables, version, versionTag);

	public ICamundaScenario Validate(Action<CamundaStepResult> validation) =>
		CamundaVerbos.Validate(_cenario, validation);

	public async Task<CamundaValidationBuilder> ExecuteAsync(CancellationToken cancellationToken = default)
	{
		await _cenario.ExecutePendingAsync(cancellationToken).ConfigureAwait(false);
		return new CamundaValidationBuilder(_cenario);
	}

	public CamundaValidationBuilder Execute() => ExecuteAsync().GetAwaiter().GetResult();

	ITestScenario ITestScenario.And() => And();

	ITestScenario ITestScenario.On() => On();

	ITestScenario ITestScenario.When() => When();

	ITestScenario ITestScenario.Then() => Then();
}
