using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using XUnitAssured.Core.Abstractions;
using XUnitAssured.Core.Results;
using XUnitAssured.Core.Storage;

namespace XUnitAssured.Core.DSL;

/// <summary>
/// Implementation of ITestScenario that manages the fluent test DSL.
///
/// <para>
/// <b>A chain describes; executing carries it out.</b> Writing
/// <c>.ApiResource(...).Post(...)</c> records a step and nothing more; the request goes out
/// when the chain is executed. The alternative — running each step where it was written —
/// means the chain does I/O at every <c>And()</c>, and an honestly asynchronous version of
/// that needs an <c>await</c> at each of those points. There is no way to write
/// <c>await x.And()</c> in the middle of a fluent chain and still have it read like one.
/// </para>
///
/// <para>
/// The cost is that a value produced by one step is not in hand while the chain is being
/// written. A check written as a lambda is fine — it closes over the variable and runs later,
/// after the value is there. A value interpolated into a string is not: that string is built
/// as the chain is written. The overloads taking <c>Func&lt;string&gt;</c> exist for exactly
/// that case.
/// </para>
/// </summary>
public class TestScenario : ITestScenario
{
	/// <summary>
	/// The steps in the order they were written, each with the checks registered for it.
	/// </summary>
	private readonly List<PassoPlanejado> _passos = new();

	/// <inheritdoc />
	public ITestContext Context { get; }

	/// <inheritdoc />
	public ITestStep? CurrentStep => _passos.Count > 0 ? _passos[^1].Step : null;

	/// <summary>
	/// Creates a new TestScenario with a fresh context.
	/// </summary>
	public TestScenario()
	{
		var stepStorage = new StepStorage();
		Context = new TestContext(stepStorage);
	}

	/// <summary>
	/// Creates a new TestScenario with the specified context.
	/// </summary>
	public TestScenario(ITestContext context)
	{
		Context = context ?? throw new ArgumentNullException(nameof(context));
	}

	/// <inheritdoc />
	public ITestScenario And() => this;

	/// <inheritdoc />
	public ITestScenario On() => this;

	/// <inheritdoc />
	public ITestScenario When() => this;

	/// <inheritdoc />
	public ITestScenario Then() => this;

	/// <inheritdoc />
	public void SetCurrentStep(ITestStep step)
	{
		if (step == null) throw new ArgumentNullException(nameof(step));

		_passos.Add(new PassoPlanejado(step));
	}

	/// <inheritdoc />
	public void AddValidation(Action<ITestStepResult> validation)
	{
		if (validation == null) throw new ArgumentNullException(nameof(validation));

		if (_passos.Count == 0)
			throw new InvalidOperationException(
				"No step to validate. Describe a step before validating it.");

		var passo = _passos[^1];

		// A step that already ran has its result in hand, so there is nothing to wait for.
		// This is what keeps a check written after execution working the way it reads.
		if (passo.Step.Result != null)
		{
			passo.Step.Validate(validation);
			return;
		}

		passo.Validacoes.Add(validation);
	}

	/// <inheritdoc />
	public async Task ExecutePendingAsync(CancellationToken cancellationToken = default)
	{
		foreach (var passo in _passos)
		{
			if (!passo.Step.IsExecuted)
				await passo.Step.ExecuteAsync(Context, cancellationToken).ConfigureAwait(false);

			// Checks run right after their own step, not at the end: a scenario that fails
			// should say which step failed, and a later step may well depend on an earlier
			// one being what it should be.
			foreach (var validacao in passo.Validacoes)
				passo.Step.Validate(validacao);

			passo.Validacoes.Clear();
		}
	}

	private sealed class PassoPlanejado
	{
		public PassoPlanejado(ITestStep step) => Step = step;

		public ITestStep Step { get; }

		public List<Action<ITestStepResult>> Validacoes { get; } = new();
	}
}
