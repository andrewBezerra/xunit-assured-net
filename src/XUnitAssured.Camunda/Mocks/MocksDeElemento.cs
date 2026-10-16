using System;
using System.Collections.Generic;

using XUnitAssured.Core.Abstractions;

namespace XUnitAssured.Camunda.Mocks;

/// <summary>A resposta combinada para uma user task, no lugar da pessoa.</summary>
internal sealed record UserTaskMock(string ElementId, object? Variables);

/// <summary>As variáveis que vão com a mensagem ou o sinal, quando o teste responde por quem os enviaria.</summary>
internal sealed record EventoMock(string Nome, object? Variables);

/// <summary>
/// A resposta combinada para o processo filho que uma call activity chama: terminar com estas
/// variáveis, ou com o erro BPMN deste código.
/// </summary>
internal sealed record FilhoMock(string CallActivityId, object? Variables, string? ErrorCode = null);

/// <summary>
/// Os mocks que a cadeia declarou fora dos jobs: user tasks e timers, pelo id do elemento; mensagens e
/// sinais, pelo nome; processos filhos, pelo id da call activity que os chama.
/// </summary>
/// <remarks>
/// Ao contrário do job, que se mocka pelo tipo, estes se mockam pelo id do elemento: duas user
/// tasks do mesmo diagrama pedem respostas diferentes.
/// </remarks>
internal static class MocksDeElemento
{
	internal const string ChaveDasUserTasks = "_CamundaUserTaskMocks";
	internal const string ChaveDosTimers = "_CamundaTimersPulados";
	internal const string ChaveDasMensagens = "_CamundaMensagens";
	internal const string ChaveDosSinais = "_CamundaSinais";
	internal const string ChaveDosFilhos = "_CamundaFilhos";

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

	internal static void RegistrarMensagem(ITestContext context, EventoMock mock) =>
		Registrar(Mensagens(context), mock, $"Message '{mock.Nome}' is already mocked in this chain.");

	internal static void RegistrarSinal(ITestContext context, EventoMock mock) =>
		Registrar(Sinais(context), mock, $"Signal '{mock.Nome}' is already mocked in this chain.");

	internal static void RegistrarFilho(ITestContext context, FilhoMock mock)
	{
		var mocks = Filhos(context);
		if (mocks.ContainsKey(mock.CallActivityId))
			throw new InvalidOperationException(
				$"The child process of call activity '{mock.CallActivityId}' is already mocked in this chain. Each call activity " +
				"gets one answer per chain; write a second test for the other answer.");

		mocks[mock.CallActivityId] = mock;
	}

	internal static Dictionary<string, FilhoMock> Filhos(ITestContext context) =>
		Obter(context, ChaveDosFilhos, () => new Dictionary<string, FilhoMock>(StringComparer.Ordinal));

	internal static Dictionary<string, EventoMock> Mensagens(ITestContext context) =>
		Obter(context, ChaveDasMensagens, () => new Dictionary<string, EventoMock>(StringComparer.Ordinal));

	internal static Dictionary<string, EventoMock> Sinais(ITestContext context) =>
		Obter(context, ChaveDosSinais, () => new Dictionary<string, EventoMock>(StringComparer.Ordinal));

	private static void Registrar(Dictionary<string, EventoMock> mocks, EventoMock mock, string jaExiste)
	{
		if (mocks.ContainsKey(mock.Nome))
			throw new InvalidOperationException(jaExiste + " Each one gets one answer per chain; write a second test for the other answer.");

		mocks[mock.Nome] = mock;
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
