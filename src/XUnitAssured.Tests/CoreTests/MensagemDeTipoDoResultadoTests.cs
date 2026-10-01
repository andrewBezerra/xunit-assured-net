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

[Trait("Category", "Core")]
[Trait("Component", "Validation")]
/// <summary>
/// O que a pessoa lê quando a cadeia foi tipada para um pacote e o passo é de outro.
///
/// <para>
/// Acontece de verdade, e de forma pouco óbvia: <c>Validate(r =&gt; ...)</c> sem tipo no parâmetro
/// liga-se à sobrecarga de qualquer pacote referenciado que aceite um resultado genérico, e isso
/// tipa o resto da cadeia para aquele pacote. Uma cadeia de Kafka sai tipada como HTTP, e a falha
/// só aparece aqui, na hora de ler o resultado.
/// </para>
///
/// <para>
/// A mensagem antiga mandava escolher o <c>Execute&lt;T&gt;()</c> certo. Desde a 6.0.0 não é assim
/// que o construtor de asserções é escolhido, então ela mandava a pessoa para o lugar errado,
/// justamente quando ela já estava perdida. Estes testes verificam que a mensagem nomeia os dois
/// tipos e diz o que fazer.
/// </para>
/// </summary>
public class MensagemDeTipoDoResultadoTests
{
	[Fact(DisplayName = "A mismatched result type should name both types")]
	public void Mismatched_Result_Should_Name_Both_Types()
	{
		var erro = Erro();

		erro.Message.ShouldContain(nameof(ResultadoEsperado));
		erro.Message.ShouldContain(nameof(ResultadoProduzido));
	}

	// O que torna a mensagem acionável: sem isto, ela diz que algo está errado e não o que fazer.
	[Fact(DisplayName = "A mismatched result type should say how to fix it")]
	public void Mismatched_Result_Should_Say_How_To_Fix_It()
	{
		var erro = Erro();

		erro.Message.ShouldContain("Validate(");
		erro.Message.ShouldContain($"({nameof(ResultadoProduzido)} r)");
	}

	// A sugestão antiga não descreve mais o mecanismo, e precisa ficar fora.
	[Fact(DisplayName = "A mismatched result type should not point at Execute<T>()")]
	public void Mismatched_Result_Should_Not_Point_At_Generic_Execute()
	{
		Erro().Message.ShouldNotContain("Execute<T>()");
	}

	[Fact(DisplayName = "A step that has not run should say that instead")]
	public void A_Step_That_Has_Not_Run_Should_Say_That_Instead()
	{
		var cenario = new TestScenario();
		cenario.SetCurrentStep(new PassoDeMentira());

		var erro = Should.Throw<InvalidOperationException>(
			() => new ValidationBuilder<ResultadoEsperado>(cenario).AssertSuccess());

		erro.Message.ShouldContain("not been executed");
	}

	private static InvalidOperationException Erro()
	{
		var cenario = new TestScenario();
		var passo = new PassoDeMentira();
		cenario.SetCurrentStep(passo);
		passo.ExecuteAsync(cenario.Context).GetAwaiter().GetResult();

		return Should.Throw<InvalidOperationException>(
			() => new ValidationBuilder<ResultadoEsperado>(cenario).AssertSuccess());
	}

	private sealed class PassoDeMentira : ITestStep
	{
		public string? Name => null;

		public string StepType => "Mentira";

		public ITestStepResult? Result { get; private set; }

		public bool IsExecuted => Result != null;

		public bool IsValid => true;

		public Task<ITestStepResult> ExecuteAsync(
			ITestContext context, CancellationToken cancellationToken = default)
		{
			Result = new ResultadoProduzido();
			return Task.FromResult(Result);
		}

		public void Validate(Action<ITestStepResult> validation) => validation(Result!);
	}

	private sealed class ResultadoProduzido : TestStepResult
	{
	}

	private sealed class ResultadoEsperado : TestStepResult
	{
	}
}
