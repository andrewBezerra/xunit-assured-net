using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

using XUnitAssured.Core.Abstractions;
using XUnitAssured.Core.DSL;
using XUnitAssured.Core.Results;

namespace XUnitAssured.Tests.CoreTests;

[Trait("Category", "Core")]
[Trait("Component", "DSL")]
/// <summary>
/// Um verbo que reconstrói o passo substitui o passo, e não acrescenta outro.
///
/// <para>
/// Os passos são <c>init</c>-only, então todo verbo que muda um valor reconstrói o passo inteiro e
/// chama <c>SetCurrentStep</c> com o novo. Quando a 6.0.0 fez a cadeia só descrever,
/// <c>SetCurrentStep</c> passou a acrescentar à lista de passos planejados — e com isso cada verbo
/// depois da ação passou a criar uma execução extra.
/// </para>
///
/// <para>
/// O efeito é pior do que lentidão. Numa cadeia de consumo, o primeiro passo retira a mensagem e o
/// segundo não acha nada: as asserções leem o último resultado, que é o vazio. A cadeia que a
/// documentação ensina, ação e depois configuração, passou a falhar por construção.
/// </para>
///
/// <para>
/// Um passo novo começa onde a gramática do DSL diz que começa: em <c>And()</c> ou <c>On()</c>.
/// </para>
/// </summary>
public class ReconstrucaoDePassoTests
{
	[Fact(DisplayName = "Rebuilding the current step should not add an execution")]
	public async Task Rebuilding_Should_Not_Add_An_Execution()
	{
		var cenario = new TestScenario();
		var primeiro = new PassoContado();

		cenario.SetCurrentStep(primeiro);
		var segundo = new PassoContado();
		cenario.SetCurrentStep(segundo);

		await cenario.ExecutePendingAsync();

		primeiro.Execucoes.ShouldBe(0, "the first step was replaced, not queued");
		segundo.Execucoes.ShouldBe(1);
	}

	[Fact(DisplayName = "And should start a new step")]
	public async Task And_Should_Start_A_New_Step()
	{
		var cenario = new TestScenario();
		var primeiro = new PassoContado();
		var segundo = new PassoContado();

		cenario.SetCurrentStep(primeiro);
		cenario.And();
		cenario.SetCurrentStep(segundo);

		await cenario.ExecutePendingAsync();

		primeiro.Execucoes.ShouldBe(1);
		segundo.Execucoes.ShouldBe(1);
	}

	[Fact(DisplayName = "On should start a new step too")]
	public async Task On_Should_Start_A_New_Step_Too()
	{
		var cenario = new TestScenario();
		var primeiro = new PassoContado();
		var segundo = new PassoContado();

		cenario.SetCurrentStep(primeiro);
		cenario.On();
		cenario.SetCurrentStep(segundo);

		await cenario.ExecutePendingAsync();

		primeiro.Execucoes.ShouldBe(1);
		segundo.Execucoes.ShouldBe(1);
	}

	// A verificação registrada antes do verbo tem de sobreviver à reconstrução, senão o conserto
	// troca execução dupla por asserção perdida, que é pior.
	[Fact(DisplayName = "A check registered before the rebuild should run on the new step")]
	public async Task A_Check_Registered_Before_The_Rebuild_Should_Survive()
	{
		var cenario = new TestScenario();
		cenario.SetCurrentStep(new PassoContado());

		var rodou = false;
		cenario.AddValidation(_ => rodou = true);

		cenario.SetCurrentStep(new PassoContado());

		await cenario.ExecutePendingAsync();

		rodou.ShouldBeTrue("the check was registered for the step being described, whichever object it is");
	}

	// When e Then são marcadores dentro de um passo, e não separadores: o idioma
	// `.Get().When().Execute()` tem um passo só.
	[Fact(DisplayName = "When and Then should not start a new step")]
	public async Task When_And_Then_Should_Not_Start_A_New_Step()
	{
		var cenario = new TestScenario();
		var primeiro = new PassoContado();
		var segundo = new PassoContado();

		cenario.SetCurrentStep(primeiro);
		cenario.When();
		cenario.Then();
		cenario.SetCurrentStep(segundo);

		await cenario.ExecutePendingAsync();

		primeiro.Execucoes.ShouldBe(0);
		segundo.Execucoes.ShouldBe(1);
	}

	private sealed class PassoContado : ITestStep
	{
		public string? Name => null;

		public string StepType => "Contado";

		public ITestStepResult? Result { get; private set; }

		public bool IsExecuted => Result != null;

		public bool IsValid { get; private set; }

		public int Execucoes { get; private set; }

		public Task<ITestStepResult> ExecuteAsync(
			ITestContext context, CancellationToken cancellationToken = default)
		{
			Execucoes++;
			Result = TestStepResult.CreateSuccess(data: "pronto");
			return Task.FromResult(Result);
		}

		public void Validate(Action<ITestStepResult> validation)
		{
			if (Result == null)
				throw new InvalidOperationException("Step must be executed before validation.");

			validation(Result);
			IsValid = true;
		}
	}
}
