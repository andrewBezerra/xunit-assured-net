using System;
using System.Threading.Tasks;
using XUnitAssured.Core.Abstractions;
using XUnitAssured.Core.Storage;

namespace XUnitAssured.Core.DSL;

/// <summary>
/// Implementation of ITestScenario that manages the fluent test DSL.
/// </summary>
public class TestScenario : ITestScenario
{
	/// <inheritdoc />
	public ITestContext Context { get; }

	/// <inheritdoc />
	public ITestStep? CurrentStep { get; private set; }

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
	public ITestScenario And()
	{
		// Execute current step before chaining
		ExecuteCurrentStepAsync().GetAwaiter().GetResult();
		return this;
	}

	/// <inheritdoc />
	public ITestScenario On()
	{
		// Execute current step before chaining
		ExecuteCurrentStepAsync().GetAwaiter().GetResult();
		return this;
	}

	/// <inheritdoc />
	public ITestScenario When()
	{
		// This is a pass-through for readability in BDD-style tests
		return this;
	}

	/// <inheritdoc />
	public ITestScenario Then()
	{
		// This is a pass-through for readability in BDD-style tests
		return this;
	}

	/// <inheritdoc />
	public void SetCurrentStep(ITestStep step)
	{
		CurrentStep = step ?? throw new ArgumentNullException(nameof(step));
	}

	/// <inheritdoc />
	public async Task ExecuteCurrentStepAsync()
	{
		if (CurrentStep == null)
			return;

		if (!CurrentStep.IsExecuted)
		{
			await CurrentStep.ExecuteAsync(Context);
		}
	}
}
