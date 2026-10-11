using System;
using System.Threading;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

using XUnitAssured.Core.Abstractions;
using XUnitAssured.Core.DSL;
using XUnitAssured.Core.Extensions;
using XUnitAssured.Core.Results;

namespace XUnitAssured.Tests.CoreTests;

/// <summary>
/// O que o <c>AssertSuccess</c> mostra quando o passo falhou.
///
/// <para>
/// Um passo que lança vira um resultado com o tipo e a mensagem da exceção nas propriedades, e o
/// <c>AssertSuccess</c> as lista. É o que diz, sem depurar, por que o passo falhou. A rodada de
/// mutação (#141) mostrou que nenhum teste lia essa parte: ela podia sumir em silêncio.
/// </para>
/// </summary>
[Trait("Category", "Core")]
[Trait("Component", "Validation")]
public class DiagnosticoDoAssertSuccessTests
{
	[Fact(DisplayName = "AssertSuccess on a step that threw names the exception type and message")]
	public void AssertSuccess_Names_The_Exception()
	{
		var falha = TestStepResult.CreateFailure(new TimeoutException("o broker não respondeu"));

		var erro = Should.Throw<ShouldAssertException>(() => Builder(falha).AssertSuccess());

		erro.Message.ShouldContain("ExceptionType=System.TimeoutException");
		erro.Message.ShouldContain("ExceptionMessage=o broker não respondeu");
	}

	[Fact(DisplayName = "AssertSuccess with no properties shows only the errors")]
	public void AssertSuccess_Without_Properties_Shows_Only_Errors()
	{
		var falha = TestStepResult.CreateFailure(new[] { "a fila não existe" });

		var erro = Should.Throw<ShouldAssertException>(() => Builder(falha).AssertSuccess());

		erro.Message.ShouldContain("a fila não existe");
		erro.Message.ShouldNotContain("Properties:");
	}

	private static ValidationBuilder<TestStepResult> Builder(TestStepResult resultado)
	{
		var cenario = new TestScenario();
		cenario.SetCurrentStep(new PassoPronto(resultado));
		return new ValidationBuilder<TestStepResult>(cenario);
	}

	private sealed class PassoPronto(ITestStepResult resultado) : ITestStep
	{
		public string? Name => null;
		public string StepType => "Pronto";
		public ITestStepResult? Result { get; } = resultado;
		public bool IsExecuted => true;
		public bool IsValid => true;

		public Task<ITestStepResult> ExecuteAsync(ITestContext context, CancellationToken cancellationToken = default) =>
			Task.FromResult(Result!);

		public void Validate(Action<ITestStepResult> validation) => validation(Result!);
	}
}
