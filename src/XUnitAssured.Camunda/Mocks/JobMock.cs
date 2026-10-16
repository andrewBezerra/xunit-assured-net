using System;
using System.Collections.Generic;

using XUnitAssured.Core.Abstractions;

namespace XUnitAssured.Camunda.Mocks;

/// <summary>O que o mock faz com o job.</summary>
internal enum AcaoDoMock
{
	Completar,
	LancarErroBpmn,
	Falhar
}

/// <summary>
/// A resposta combinada para um tipo de job, no lugar do worker de verdade.
/// </summary>
internal sealed record JobMock(
	string Type,
	AcaoDoMock Acao,
	object? Variables,
	string? ErrorCode = null,
	string? ErrorMessage = null)
{
	public string Descrever() => Acao switch
	{
		AcaoDoMock.LancarErroBpmn => $"throw BPMN error '{ErrorCode}'",
		AcaoDoMock.Falhar => "fail",
		_ => "complete"
	};
}

/// <summary>
/// Os mocks que a cadeia declarou, por tipo de job.
/// </summary>
/// <remarks>
/// Mocks são configuração da cadeia, e não passos: valem para todas as instâncias que ela
/// iniciar, declarados antes ou depois do <c>StartInstance</c>, porque a cadeia inteira é descrita
/// antes de qualquer passo rodar.
/// </remarks>
internal static class MocksDaCadeia
{
	internal const string ChaveNoContexto = "_CamundaMocks";

	internal static void Registrar(ITestContext context, JobMock mock)
	{
		var mocks = Todos(context);

		if (mocks.ContainsKey(mock.Type))
			throw new InvalidOperationException(
				$"Job type '{mock.Type}' is already mocked in this chain. Each job type gets one answer per chain; " +
				"write a second test for the other answer.");

		mocks[mock.Type] = mock;
	}

	internal static Dictionary<string, JobMock> Todos(ITestContext context)
	{
		var mocks = context.GetProperty<Dictionary<string, JobMock>>(ChaveNoContexto);
		if (mocks is null)
		{
			mocks = new Dictionary<string, JobMock>(StringComparer.Ordinal);
			context.SetProperty(ChaveNoContexto, mocks);
		}

		return mocks;
	}
}
