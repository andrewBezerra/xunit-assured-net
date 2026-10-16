using System;
using System.Collections.Generic;

using XUnitAssured.Core.Abstractions;

namespace XUnitAssured.Camunda.Mocks;

/// <summary>A resposta combinada para uma user task, no lugar da pessoa.</summary>
internal sealed record UserTaskMock(string ElementId, object? Variables);

/// <summary>
/// Os mocks que a cadeia declarou por elemento do BPMN: user tasks e timers a pular.
/// </summary>
/// <remarks>
/// Ao contrário do job, que se mocka pelo tipo, estes se mockam pelo id do elemento: duas user
/// tasks do mesmo diagrama pedem respostas diferentes.
/// </remarks>
internal static class MocksDeElemento
{
	internal const string ChaveDasUserTasks = "_CamundaUserTaskMocks";
	internal const string ChaveDosTimers = "_CamundaTimersPulados";

	internal static void RegistrarUserTask(ITestContext context, UserTaskMock mock)
	{
		var mocks = UserTasks(context);
		if (mocks.ContainsKey(mock.ElementId))
			throw new InvalidOperationException(
				$"User task '{mock.ElementId}' is already mocked in this chain. Each user task gets one answer per chain; " +
				"write a second test for the other answer.");

		mocks[mock.ElementId] = mock;
	}

	internal static void RegistrarTimer(ITestContext context, string elementId)
	{
		if (!Timers(context).Add(elementId))
			throw new InvalidOperationException($"Timer '{elementId}' is already skipped in this chain.");
	}

	internal static Dictionary<string, UserTaskMock> UserTasks(ITestContext context) =>
		Obter(context, ChaveDasUserTasks, () => new Dictionary<string, UserTaskMock>(StringComparer.Ordinal));

	internal static HashSet<string> Timers(ITestContext context) =>
		Obter(context, ChaveDosTimers, () => new HashSet<string>(StringComparer.Ordinal));

	private static T Obter<T>(ITestContext context, string chave, Func<T> criar) where T : class
	{
		var valor = context.GetProperty<T>(chave);
		if (valor is null)
		{
			valor = criar();
			context.SetProperty(chave, valor);
		}

		return valor;
	}
}
