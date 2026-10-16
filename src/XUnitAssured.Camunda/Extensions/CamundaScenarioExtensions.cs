using System;
using System.Linq;

using XUnitAssured.Camunda.Abstractions;
using XUnitAssured.Camunda.DSL;
using XUnitAssured.Camunda.Mocks;
using XUnitAssured.Camunda.Results;
using XUnitAssured.Camunda.Steps;
using XUnitAssured.Core.Abstractions;

namespace XUnitAssured.Camunda.Extensions;

/// <summary>
/// Os verbos de entrada, e os únicos que tomam o cenário não tipado.
/// </summary>
/// <remarks>
/// Pela convenção do README, um pacote de protocolo declara os verbos sobre o próprio cenário
/// tipado e só os de entrada sobre <see cref="ITestScenario"/>. Os daqui não colidem com os
/// dos outros pacotes, então uma cadeia pode começar no HTTP e seguir no Camunda.
/// </remarks>
public static class CamundaScenarioExtensions
{
	/// <inheritdoc cref="ICamundaScenario.Deploy" />
	public static ICamundaScenario Deploy(this ITestScenario scenario, params string[] resources) =>
		CamundaVerbos.Deploy(scenario ?? throw new ArgumentNullException(nameof(scenario)), resources);

	/// <inheritdoc cref="ICamundaScenario.MockJobWorker" />
	public static ICamundaJobMock MockJobWorker(this ITestScenario scenario, string jobType) =>
		CamundaVerbos.MockJobWorker(scenario ?? throw new ArgumentNullException(nameof(scenario)), jobType);

	/// <inheritdoc cref="ICamundaScenario.MockUserTask" />
	public static ICamundaUserTaskMock MockUserTask(this ITestScenario scenario, string elementId) =>
		CamundaVerbos.MockUserTask(scenario ?? throw new ArgumentNullException(nameof(scenario)), elementId);

	/// <inheritdoc cref="ICamundaScenario.SkipTimer" />
	public static ICamundaScenario SkipTimer(this ITestScenario scenario, string elementId) =>
		CamundaVerbos.SkipTimer(scenario ?? throw new ArgumentNullException(nameof(scenario)), elementId);

	/// <inheritdoc cref="ICamundaScenario.MockMessage" />
	public static ICamundaMessageMock MockMessage(this ITestScenario scenario, string messageName) =>
		CamundaVerbos.MockMessage(scenario ?? throw new ArgumentNullException(nameof(scenario)), messageName);

	/// <inheritdoc cref="ICamundaScenario.MockSignal" />
	public static ICamundaSignalMock MockSignal(this ITestScenario scenario, string signalName) =>
		CamundaVerbos.MockSignal(scenario ?? throw new ArgumentNullException(nameof(scenario)), signalName);

	/// <inheritdoc cref="ICamundaScenario.StartInstance" />
	public static ICamundaScenario StartInstance(
		this ITestScenario scenario, string processId, object? variables = null, int? version = null, string? versionTag = null) =>
		CamundaVerbos.StartInstance(scenario ?? throw new ArgumentNullException(nameof(scenario)), processId, variables, version, versionTag);
}

/// <summary>
/// O corpo dos verbos, compartilhado pelo invólucro e pelas extensões de entrada.
/// </summary>
internal static class CamundaVerbos
{
	internal static ICamundaScenario Deploy(ITestScenario scenario, string[] resources)
	{
		if (resources == null || resources.Length == 0)
			throw new ArgumentException("Name at least one BPMN file to deploy.", nameof(resources));
		if (resources.Any(string.IsNullOrWhiteSpace))
			throw new ArgumentException("A resource path is empty.", nameof(resources));

		scenario.SetCurrentStep(new CamundaDeployStep { Resources = resources.ToArray() });
		return CamundaScenario.De(scenario);
	}

	internal static ICamundaJobMock MockJobWorker(ITestScenario scenario, string jobType)
	{
		if (string.IsNullOrWhiteSpace(jobType))
			throw new ArgumentException("Job type is required: the type of the task's zeebe:taskDefinition.", nameof(jobType));

		return new MockEmDeclaracao(scenario, jobType);
	}

	internal static ICamundaUserTaskMock MockUserTask(ITestScenario scenario, string elementId)
	{
		if (string.IsNullOrWhiteSpace(elementId))
			throw new ArgumentException("Element id is required: the id of the bpmn:userTask.", nameof(elementId));

		return new UserTaskEmDeclaracao(scenario, elementId);
	}

	internal static ICamundaScenario SkipTimer(ITestScenario scenario, string elementId)
	{
		if (string.IsNullOrWhiteSpace(elementId))
			throw new ArgumentException("Element id is required: the id of the timer event in the BPMN.", nameof(elementId));

		MocksDeElemento.RegistrarTimer(scenario.Context, elementId);
		return CamundaScenario.De(scenario);
	}

	internal static ICamundaMessageMock MockMessage(ITestScenario scenario, string messageName)
	{
		if (string.IsNullOrWhiteSpace(messageName))
			throw new ArgumentException("Message name is required: the name of the bpmn:message.", nameof(messageName));

		return new EventoEmDeclaracao(scenario, messageName, mensagem: true);
	}

	internal static ICamundaSignalMock MockSignal(ITestScenario scenario, string signalName)
	{
		if (string.IsNullOrWhiteSpace(signalName))
			throw new ArgumentException("Signal name is required: the name of the bpmn:signal.", nameof(signalName));

		return new EventoEmDeclaracao(scenario, signalName, mensagem: false);
	}

	internal static ICamundaScenario StartInstance(
		ITestScenario scenario, string processId, object? variables, int? version, string? versionTag)
	{
		if (string.IsNullOrWhiteSpace(processId))
			throw new ArgumentException("Process id is required: the id of the bpmn:process.", nameof(processId));
		if (version is <= 0)
			throw new ArgumentOutOfRangeException(nameof(version), version, "Camunda numbers versions from 1.");
		if (versionTag is not null && string.IsNullOrWhiteSpace(versionTag))
			throw new ArgumentException("Version tag is empty. Leave it null to start the latest version.", nameof(versionTag));
		if (version is not null && versionTag is not null)
			throw new ArgumentException("Pass version or versionTag, not both: each one already names a single version.", nameof(versionTag));

		scenario.SetCurrentStep(new CamundaStartInstanceStep
		{
			ProcessId = processId,
			Variables = variables,
			Version = version,
			VersionTag = versionTag
		});
		return CamundaScenario.De(scenario);
	}

	internal static ICamundaScenario Validate(ITestScenario scenario, Action<CamundaStepResult> validation)
	{
		if (validation == null) throw new ArgumentNullException(nameof(validation));

		scenario.AddValidation(resultado =>
		{
			if (resultado is not CamundaStepResult camunda)
				throw new InvalidOperationException(
					$"Expected a CamundaStepResult but got {resultado.GetType().Name}.");

			validation(camunda);
		});

		return CamundaScenario.De(scenario);
	}

	private sealed class EventoEmDeclaracao : ICamundaMessageMock, ICamundaSignalMock
	{
		private readonly ITestScenario _cenario;
		private readonly string _nome;
		private readonly bool _mensagem;

		public EventoEmDeclaracao(ITestScenario cenario, string nome, bool mensagem)
		{
			_cenario = cenario;
			_nome = nome;
			_mensagem = mensagem;
		}

		public ICamundaScenario ThenCorrelate(object? variables = null)
		{
			MocksDeElemento.RegistrarMensagem(_cenario.Context, new EventoMock(_nome, variables));
			return CamundaScenario.De(_cenario);
		}

		public ICamundaScenario ThenTrigger(object? variables = null)
		{
			MocksDeElemento.RegistrarSinal(_cenario.Context, new EventoMock(_nome, variables));
			return CamundaScenario.De(_cenario);
		}

		public override string ToString() => _mensagem ? $"message '{_nome}'" : $"signal '{_nome}'";
	}

	private sealed class UserTaskEmDeclaracao : ICamundaUserTaskMock
	{
		private readonly ITestScenario _cenario;
		private readonly string _elemento;

		public UserTaskEmDeclaracao(ITestScenario cenario, string elemento)
		{
			_cenario = cenario;
			_elemento = elemento;
		}

		public ICamundaScenario ThenComplete(object? variables = null)
		{
			MocksDeElemento.RegistrarUserTask(_cenario.Context, new UserTaskMock(_elemento, variables));
			return CamundaScenario.De(_cenario);
		}
	}

	private sealed class MockEmDeclaracao : ICamundaJobMock
	{
		private readonly ITestScenario _cenario;
		private readonly string _tipo;

		public MockEmDeclaracao(ITestScenario cenario, string tipo)
		{
			_cenario = cenario;
			_tipo = tipo;
		}

		public ICamundaScenario ThenComplete(object? variables = null)
		{
			MocksDaCadeia.Registrar(_cenario.Context, new JobMock(_tipo, AcaoDoMock.Completar, variables));
			return CamundaScenario.De(_cenario);
		}

		public ICamundaScenario ThenFail(string errorMessage)
		{
			if (string.IsNullOrWhiteSpace(errorMessage))
				throw new ArgumentException("Error message is required: it is what the incident shows in Operate.", nameof(errorMessage));

			MocksDaCadeia.Registrar(_cenario.Context, new JobMock(_tipo, AcaoDoMock.Falhar, null, ErrorMessage: errorMessage));
			return CamundaScenario.De(_cenario);
		}

		public ICamundaScenario ThenThrowBpmnError(string errorCode, string? errorMessage = null, object? variables = null)
		{
			if (string.IsNullOrWhiteSpace(errorCode))
				throw new ArgumentException("Error code is required: the errorCode of the bpmn:error.", nameof(errorCode));

			MocksDaCadeia.Registrar(
				_cenario.Context,
				new JobMock(_tipo, AcaoDoMock.LancarErroBpmn, variables, errorCode, errorMessage));
			return CamundaScenario.De(_cenario);
		}
	}
}
