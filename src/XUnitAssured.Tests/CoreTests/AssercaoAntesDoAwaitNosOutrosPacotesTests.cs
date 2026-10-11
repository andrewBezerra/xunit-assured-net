using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

using XUnitAssured.Core.Abstractions;
using XUnitAssured.Core.DSL;
using XUnitAssured.Core.Results;
using XUnitAssured.Kafka.Extensions;
using XUnitAssured.Kafka.Results;
using XUnitAssured.Playwright.Extensions;
using XUnitAssured.Playwright.Results;
using XUnitAssured.RabbitMq.Extensions;
using XUnitAssured.RabbitMq.Results;

namespace XUnitAssured.Tests.CoreTests;

/// <summary>
/// As asserções antes do <c>await</c> no Kafka, no RabbitMQ e no Playwright.
///
/// <para>
/// O HTTP ganhou isso na 6.8.0, com as extensões escritas à mão. Nos outros pacotes elas são
/// geradas do próprio builder (XUnitAssured.SourceGenerators): o do Playwright tem 181 métodos, e
/// uma cópia à mão divergiria no primeiro método novo. Os testes de navegador do LiaisonHub
/// escreviam <c>var x = await ...ExecuteAsync(); x.Then().AssertFetchStatus(200);</c> 5 vezes em
/// 3 testes.
/// </para>
///
/// Sem broker nem navegador: o passo já vem com o resultado pronto.
/// </summary>
[Trait("Category", "Core")]
[Trait("Component", "Assertions")]
public class AssercaoAntesDoAwaitNosOutrosPacotesTests
{
	// ---------- Cada método do builder tem o seu sobre a Task ----------

	[Theory(DisplayName = "Every public method of the builder has its counterpart on the Task")]
	[InlineData(typeof(KafkaValidationBuilder), typeof(KafkaValidationTaskExtensions))]
	[InlineData(typeof(RabbitMqValidationBuilder), typeof(RabbitMqValidationTaskExtensions))]
	[InlineData(typeof(PlaywrightValidationBuilder), typeof(PlaywrightValidationTaskExtensions))]
	public void Every_Builder_Method_Has_A_Counterpart(Type builder, Type extensoes)
	{
		var geradas = extensoes
			.GetMethods(BindingFlags.Public | BindingFlags.Static)
			.Where(m => m.GetParameters().FirstOrDefault()?.ParameterType == typeof(Task<>).MakeGenericType(builder))
			.Select(m => Assinatura(m.Name, m.GetParameters().Skip(1)))
			.ToHashSet();

		var faltam = builder
			.GetMethods(BindingFlags.Public | BindingFlags.Instance)
			.Where(m => m.DeclaringType != typeof(object) && !m.IsSpecialName)
			.Where(m => !m.GetParameters().Any(p => p.IsOut || p.ParameterType.IsByRef))
			.Select(m => Assinatura(m.Name, m.GetParameters()))
			.Distinct()
			.Where(a => !geradas.Contains(a))
			.ToList();

		geradas.Count.ShouldBeGreaterThan(0);
		faltam.ShouldBeEmpty($"These {builder.Name} methods have no counterpart in {extensoes.Name}");
	}

	[Fact(DisplayName = "The inherited assertions keep the chain on the package's builder")]
	public void Inherited_Assertions_Keep_The_Chain()
	{
		var validate = typeof(KafkaValidationTaskExtensions).GetMethods()
			.Single(m => m.Name == "Validate" && m.GetParameters()[1].ParameterType == typeof(Action<KafkaStepResult>));

		validate.ReturnType.ShouldBe(typeof(Task<KafkaValidationBuilder>));
	}

	// ---------- Kafka ----------

	[Fact(DisplayName = "Kafka: assertions chain before the await, and the value ends it")]
	public async Task Kafka_Chains_Before_The_Await()
	{
		var mensagem = await Kafka("""{"id":7,"status":"open"}""")
			.AssertSuccess()
			.AssertJsonPath<string>("$.status", s => s == "open")
			.Validate(r => r.Success.ShouldBeTrue())
			.GetResult();

		mensagem.JsonPath<int>("$.id").ShouldBe(7);
	}

	[Fact(DisplayName = "Kafka: a failed assertion before the await surfaces at the await")]
	public async Task Kafka_Failure_Surfaces_At_The_Await()
	{
		var erro = await Falha(() => Kafka("""{"status":"open"}""").AssertJsonPath<string>("$.status", s => s == "closed", "status should be closed"));

		erro.Message.ShouldContain("status should be closed");
	}

	// ---------- RabbitMQ ----------

	[Fact(DisplayName = "RabbitMQ: assertions chain before the await")]
	public async Task RabbitMq_Chains_Before_The_Await()
	{
		await Pronto<RabbitMqValidationBuilder>(RabbitMqStepResult.CreateConsumeSuccess("pedidos", "pedido.criado", "ola", 0), c => new RabbitMqValidationBuilder(c))
			.AssertSuccess()
			.AssertMessage("ola")
			.AssertRoutingKey("pedido.criado");

		(await Falha(() => Pronto<RabbitMqValidationBuilder>(RabbitMqStepResult.CreateConsumeSuccess("pedidos", "pedido.criado", "ola", 0), c => new RabbitMqValidationBuilder(c))
				.AssertRoutingKey("pedido.cancelado")))
			.Message.ShouldContain("pedido.cancelado");
	}

	// ---------- Playwright ----------

	[Fact(DisplayName = "Playwright: assertions chain before the await")]
	public async Task Playwright_Chains_Before_The_Await()
	{
		var resultado = PlaywrightStepResult.CreateSuccess("https://app.test/painel", "Painel", () => "<h1>Painel</h1>");

		await Pronto<PlaywrightValidationBuilder>(resultado, c => new PlaywrightValidationBuilder(c))
			.AssertSuccess()
			.AssertUrlEndsWith("/painel");

		(await Falha(() => Pronto<PlaywrightValidationBuilder>(resultado, c => new PlaywrightValidationBuilder(c))
				.AssertUrlEndsWith("/entrar")))
			.Message.ShouldContain("/entrar");
	}

	// ---------- Apoio ----------

	private static Task<KafkaValidationBuilder> Kafka(string json) =>
		Pronto<KafkaValidationBuilder>(new KafkaStepResult
		{
			Success = true,
			Data = json,
			Properties = new Dictionary<string, object?>(),
			Metadata = new StepMetadata
			{
				StartedAt = DateTimeOffset.UtcNow,
				CompletedAt = DateTimeOffset.UtcNow,
				Status = StepStatus.Succeeded
			}
		}, c => new KafkaValidationBuilder(c));

	// O que ExecuteAsync() devolveria: a Task do builder, com o passo já executado. Assíncrona de
	// verdade, para a cadeia não depender de a Task já vir completa.
	private static async Task<TBuilder> Pronto<TBuilder>(ITestStepResult resultado, Func<ITestScenario, TBuilder> criar)
	{
		await Task.Yield();
		var cenario = ScenarioDsl.Given();
		cenario.SetCurrentStep(new PassoPronto(resultado));
		return criar(cenario);
	}

	// O Should.ThrowAsync do Shouldly deixa passar a própria ShouldAssertException em vez de
	// capturá-la, então a falha esperada é apanhada aqui.
	private static async Task<ShouldAssertException> Falha(Func<Task> cadeia)
	{
		try
		{
			await cadeia();
		}
		catch (ShouldAssertException ex)
		{
			return ex;
		}

		throw new InvalidOperationException("Expected the chain to fail, and it passed.");
	}

	private static string Assinatura(string nome, IEnumerable<ParameterInfo> parametros) =>
		$"{nome}({string.Join(", ", parametros.Select(p => Nome(p.ParameterType)))})";

	private static string Nome(Type tipo) =>
		tipo.IsGenericType ? $"{tipo.Name}[{string.Join(",", tipo.GetGenericArguments().Select(Nome))}]" : tipo.Name;

	private sealed class PassoPronto(ITestStepResult resultado) : ITestStep
	{
		public string? Name { get; set; }
		public string StepType => "Pronto";
		public ITestStepResult? Result { get; } = resultado;
		public bool IsExecuted => true;
		public bool IsValid { get; private set; }

		public Task<ITestStepResult> ExecuteAsync(ITestContext context, CancellationToken cancellationToken = default) =>
			Task.FromResult(Result!);

		public void Validate(Action<ITestStepResult> validation)
		{
			validation(Result!);
			IsValid = true;
		}
	}
}
