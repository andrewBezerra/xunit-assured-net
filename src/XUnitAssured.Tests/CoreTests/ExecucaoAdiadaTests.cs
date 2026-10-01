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
/// A chain describes; executing carries it out.
///
/// <para>
/// Steps used to run where they were written — <c>And()</c> and <c>Validate()</c> each ran
/// whatever was pending. That is why the DSL could not be made asynchronous without an
/// <c>await</c> at every link, which is the shape that makes a fluent chain impossible to
/// write.
/// </para>
///
/// <para>
/// Deferring has a risk worth naming, and these tests exist for it: a check that is registered
/// and never run would turn a failing assertion into a passing test. Nothing is quieter than
/// an assertion that was never reached.
/// </para>
/// </summary>
public class ExecucaoAdiadaTests
{
	[Fact(DisplayName = "Describing a chain should not run anything")]
	public async Task Describing_Should_Not_Run_Anything()
	{
		var cenario = new TestScenario();
		var passo = new PassoDeMentira();

		cenario.SetCurrentStep(passo);
		cenario.And();

		passo.Execucoes.ShouldBe(0);

		await cenario.ExecutePendingAsync();

		passo.Execucoes.ShouldBe(1);
	}

	// O ponto de risco desta mudança: uma verificação registrada e nunca executada
	// transformaria uma asserção falha num teste verde.
	[Fact(DisplayName = "A registered check should run when the chain is executed")]
	public async Task Registered_Check_Should_Run()
	{
		var cenario = new TestScenario();
		cenario.SetCurrentStep(new PassoDeMentira());

		var rodou = false;
		cenario.AddValidation(_ => rodou = true);

		rodou.ShouldBeFalse("nothing should run while the chain is only being described");

		await cenario.ExecutePendingAsync();

		rodou.ShouldBeTrue();
	}

	[Fact(DisplayName = "A failing check should fail the chain, not be swallowed")]
	public async Task Failing_Check_Should_Fail_The_Chain()
	{
		var cenario = new TestScenario();
		cenario.SetCurrentStep(new PassoDeMentira());
		cenario.AddValidation(_ => throw new InvalidOperationException("esta verificação falhou"));

		var erro = await Should.ThrowAsync<InvalidOperationException>(
			() => cenario.ExecutePendingAsync());

		erro.Message.ShouldBe("esta verificação falhou");
	}

	// É o que torna um cenário que atravessa fronteiras possível numa cadeia só: o valor que
	// o primeiro passo produz não existe enquanto a cadeia é escrita, e existe quando a
	// verificação do segundo roda.
	[Fact(DisplayName = "A value produced by one step should reach a later step's check")]
	public async Task Value_From_One_Step_Should_Reach_A_Later_Check()
	{
		var cenario = new TestScenario();
		var capturado = 0;

		var primeiro = new PassoDeMentira { Identificador = 42 };
		cenario.SetCurrentStep(primeiro);
		cenario.AddValidation(resultado => capturado = ((ResultadoDeMentira)resultado).Identificador);

		cenario.And();
		cenario.SetCurrentStep(new PassoDeMentira());
		cenario.AddValidation(_ => capturado.ShouldBe(42, "the first step ran before this check"));

		await cenario.ExecutePendingAsync();

		capturado.ShouldBe(42);
	}

	[Fact(DisplayName = "Steps should run in the order they were written")]
	public async Task Steps_Should_Run_In_Order()
	{
		var cenario = new TestScenario();
		var ordem = new List<string>();

		cenario.SetCurrentStep(new PassoDeMentira { AoExecutar = () => ordem.Add("primeiro") });
		cenario.And();
		cenario.SetCurrentStep(new PassoDeMentira { AoExecutar = () => ordem.Add("segundo") });

		await cenario.ExecutePendingAsync();

		ordem.ShouldBe(new[] { "primeiro", "segundo" });
	}

	// Uma verificação escrita depois da execução não tem o que esperar, e roda na hora —
	// é o que mantém as asserções pós-Execute funcionando como se lê.
	[Fact(DisplayName = "A check registered after execution should run immediately")]
	public async Task Check_After_Execution_Should_Run_Immediately()
	{
		var cenario = new TestScenario();
		cenario.SetCurrentStep(new PassoDeMentira());
		await cenario.ExecutePendingAsync();

		var rodou = false;
		cenario.AddValidation(_ => rodou = true);

		rodou.ShouldBeTrue();
	}

	[Fact(DisplayName = "Executing twice should not run a step twice")]
	public async Task Executing_Twice_Should_Not_Rerun()
	{
		var cenario = new TestScenario();
		var passo = new PassoDeMentira();
		cenario.SetCurrentStep(passo);

		await cenario.ExecutePendingAsync();
		await cenario.ExecutePendingAsync();

		passo.Execucoes.ShouldBe(1);
	}

	[Fact(DisplayName = "Cancellation should reach the step")]
	public async Task Cancellation_Should_Reach_The_Step()
	{
		var cenario = new TestScenario();
		var passo = new PassoDeMentira();
		cenario.SetCurrentStep(passo);

		using var fonte = new CancellationTokenSource();
		await cenario.ExecutePendingAsync(fonte.Token);

		passo.TokenRecebido.ShouldBe(fonte.Token);
	}

	[Fact(DisplayName = "Validating with no step should say so")]
	public void Validating_With_No_Step_Should_Say_So()
	{
		Should.Throw<InvalidOperationException>(() => new TestScenario().AddValidation(_ => { }));
	}

	private sealed class PassoDeMentira : ITestStep
	{
		public string? Name => null;

		public string StepType => "Mentira";

		public ITestStepResult? Result { get; private set; }

		public bool IsExecuted => Result != null;

		public bool IsValid { get; private set; }

		public int Execucoes { get; private set; }

		public int Identificador { get; init; }

		public Action? AoExecutar { get; init; }

		public CancellationToken TokenRecebido { get; private set; }

		public Task<ITestStepResult> ExecuteAsync(ITestContext context, CancellationToken cancellationToken = default)
		{
			Execucoes++;
			TokenRecebido = cancellationToken;
			AoExecutar?.Invoke();
			Result = new ResultadoDeMentira { Identificador = Identificador };

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

	private sealed class ResultadoDeMentira : ITestStepResult
	{
		public StepMetadata Metadata { get; } = new();

		public bool Success => true;

		public IReadOnlyList<string> Errors => Array.Empty<string>();

		public object? Data => Identificador;

		public Type? DataType => typeof(int);

		public IReadOnlyDictionary<string, object?> Properties { get; } =
			new Dictionary<string, object?>();

		public T? GetProperty<T>(string name) => default;

		public T? GetData<T>() => Data is T valor ? valor : default;

		public int Identificador { get; init; }
	}
}
