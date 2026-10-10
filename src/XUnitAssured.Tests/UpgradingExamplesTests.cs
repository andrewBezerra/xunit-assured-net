using System;
using System.Threading;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

using XUnitAssured.Core.Abstractions;
using XUnitAssured.Core.DSL;
using XUnitAssured.Core.Results;
using XUnitAssured.Http.Abstractions;
using XUnitAssured.Http.Extensions;
using XUnitAssured.Http.Results;
using XUnitAssured.Kafka.Abstractions;
using XUnitAssured.Kafka.Extensions;
using XUnitAssured.Playwright.Extensions;

namespace XUnitAssured.Tests;

[Trait("Category", "Core")]
[Trait("Component", "Documentation")]
/// <summary>
/// The "after" snippets of <c>UPGRADING.md</c>, compiled.
///
/// <para>
/// A migration guide whose code does not compile is worse than no guide: the reader is already
/// stuck, and following it costs them a second debugging session. Five obsolete guides were
/// deleted from this repository for exactly that, one of them telling people to install a package
/// that never existed.
/// </para>
///
/// <para>
/// So every corrected form the guide recommends lives here too. The ones that would need a
/// broker, a browser or a running API are skipped: they are here to be compiled, not run. If a
/// future change breaks one of these, the build says so, and the guide gets fixed with the code
/// instead of drifting away from it.
/// </para>
/// </summary>
public class UpgradingExamplesTests
{
	private sealed class Order
	{
		public int Id { get; set; }
	}

	private sealed class OrderCreated
	{
		public int OrderId { get; set; }

		public string Status { get; set; } = string.Empty;
	}

	// ──────────────────────────────────────────────
	// One await, at the end
	// ──────────────────────────────────────────────

	[Fact(Skip = "Guide example — requires a running API", DisplayName = "Upgrading: a chain ends with ExecuteAsync")]
	public async Task A_Chain_Ends_With_ExecuteAsync()
	{
		var assertions = await ScenarioDsl.Given()
			.ApiResource("https://api.example.com/orders")
			.Get()
			.ExecuteAsync();

		assertions.Then().AssertStatusCode(200);
	}

	// ──────────────────────────────────────────────
	// A chain kept in a variable has to carry its type
	// ──────────────────────────────────────────────

	[Fact(Skip = "Guide example — requires a running API", DisplayName = "Upgrading: a stored chain is typed by its package")]
	public void A_Stored_Chain_Is_Typed_By_Its_Package()
	{
		// `ITestScenario` here would not compile on Execute(): CS0411. `var` works too.
		IHttpScenario scenario = ScenarioDsl.Given()
			.ApiResource("https://api.example.com/orders")
			.Get();

		scenario.Execute().Then().AssertStatusCode(200);
	}

	[Fact(Skip = "Guide example — requires a broker", DisplayName = "Upgrading: a Kafka chain is typed by its package")]
	public async Task A_Kafka_Chain_Is_Typed_By_Its_Package()
	{
		IKafkaScenario scenario = ScenarioDsl.Given()
			.Topic("orders.created")
			.Consume()
			.ValidateMessage<OrderCreated>(message => message.Status.ShouldBe("Created"));

		await scenario.ExecuteAsync();
	}

	// ──────────────────────────────────────────────
	// A value an earlier step produced
	// ──────────────────────────────────────────────

	[Fact(Skip = "Guide example — requires a running API and a browser", DisplayName = "Upgrading: a deferred URL is a function")]
	public async Task A_Deferred_Url_Is_A_Function()
	{
		var orderId = 0;

		await ScenarioDsl.Given()
			.ApiResource("https://api.example.com/orders")
			.Post(new Order())
			.Validate((HttpStepResult response) => orderId = response.JsonPath<int>("$.id"))
			.And()
			.NavigateTo(() => $"/orders/{orderId}")
			.ExecuteAsync();
	}

	[Fact(Skip = "Guide example — requires a running API", DisplayName = "Upgrading: a deferred API resource is a function")]
	public async Task A_Deferred_Api_Resource_Is_A_Function()
	{
		var orderId = 0;

		var lido = await ScenarioDsl.Given()
			.ApiResource("https://api.example.com/orders")
			.Post(new Order())
			.Validate((HttpStepResult response) => orderId = response.JsonPath<int>("$.id"))
			.And()
			.ApiResource(() => $"https://api.example.com/orders/{orderId}")
			.Get()
			.ExecuteAsync();

		lido.Then().AssertStatusCode(200);
	}

	// NavigateTo e, desde a 6.2.0, ApiResource aceitam um valor adiado. Para os demais verbos,
	// a saída é honesta e simples: duas cadeias, cada uma com a sua execução. O guia diz isso
	// em vez de prometer uma sobrecarga que não existe.
	[Fact(Skip = "Guide example — requires a running API", DisplayName = "Upgrading: two chains when the verb takes no function")]
	public async Task Two_Chains_When_The_Verb_Takes_No_Function()
	{
		var orderId = 0;

		var criado = await ScenarioDsl.Given()
			.ApiResource("https://api.example.com/orders")
			.Post(new Order())
			.Validate((HttpStepResult response) => orderId = response.JsonPath<int>("$.id"))
			.ExecuteAsync();

		criado.Then().AssertStatusCode(201);

		var lido = await ScenarioDsl.Given()
			.ApiResource($"https://api.example.com/orders/{orderId}")
			.Get()
			.ExecuteAsync();

		lido.Then().AssertStatusCode(200);
	}

	// ──────────────────────────────────────────────
	// A custom step
	// ──────────────────────────────────────────────

	// Este roda de verdade: não precisa de nada de fora, e é a única parte do guia em que o leitor
	// escreve código que o framework chama, e não o contrário.
	[Fact(DisplayName = "Upgrading: a custom step accepts a CancellationToken")]
	public async Task A_Custom_Step_Accepts_A_CancellationToken()
	{
		var cenario = new TestScenario();
		var passo = new PassoProprio();

		cenario.SetCurrentStep(passo);
		cenario.AddValidation(resultado => resultado.Success.ShouldBeTrue());

		using var fonte = new CancellationTokenSource();
		await cenario.ExecutePendingAsync(fonte.Token);

		passo.TokenRecebido.ShouldBe(fonte.Token);
	}

	/// <summary>
	/// A forma que um passo próprio tem na 6.0.0. O que mudou na assinatura é o
	/// <see cref="CancellationToken"/>; o resto do contrato é o mesmo.
	/// </summary>
	private sealed class PassoProprio : ITestStep
	{
		public string? Name => null;

		public string StepType => "Proprio";

		public ITestStepResult? Result { get; private set; }

		public bool IsExecuted => Result != null;

		public bool IsValid { get; private set; }

		public CancellationToken TokenRecebido { get; private set; }

		public Task<ITestStepResult> ExecuteAsync(
			ITestContext context, CancellationToken cancellationToken = default)
		{
			TokenRecebido = cancellationToken;
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
