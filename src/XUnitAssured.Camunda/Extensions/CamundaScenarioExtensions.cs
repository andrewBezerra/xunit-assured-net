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
/// tipado e só os de entrada sobre <see cref="ITestScenario"/>. Os três daqui não colidem com os
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
