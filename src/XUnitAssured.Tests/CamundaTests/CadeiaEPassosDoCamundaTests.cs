using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

using XUnitAssured.Camunda.Abstractions;
using XUnitAssured.Camunda.DSL;
using XUnitAssured.Camunda.Extensions;
using XUnitAssured.Camunda.Gateway;
using XUnitAssured.Camunda.Results;
using XUnitAssured.Camunda.Steps;
using XUnitAssured.Core.Abstractions;
using XUnitAssured.Core.DSL;
using XUnitAssured.Core.Extensions;
using XUnitAssured.Core.Results;

namespace XUnitAssured.Tests.CamundaTests;

/// <summary>
/// A cadeia do Camunda como cadeia do Core (os conectivos, o passo atual, as verificações) e os passos
/// de iniciar e de seguir uma instância quando o cluster recusa ou some.
/// </summary>
[Trait("Category", "Camunda")]
[Trait("Component", "DSL")]
public class CadeiaEPassosDoCamundaTests
{
	// ---------- A cadeia ----------

	[Fact(DisplayName = "And() on a Camunda chain makes the next StartInstance a second instance, instead of replacing the first")]
	public async Task And_Opens_A_New_Step()
	{
		var cluster = Publicado();

		await cluster.Cenario().StartInstance("pedido").And().StartInstance("pedido").ExecuteAsync();

		cluster.Comandos.Count(c => c == "create def-pedido-v1").ShouldBe(2);
	}

	[Fact(DisplayName = "Deploy after another Camunda verb deploys the files, and the start uses the deployed version")]
	public async Task Deploy_Through_The_Chain()
	{
		var cluster = new ClusterFalso();

		await CamundaScenario.De(cluster.Cenario()).Deploy("xa-pedido").StartInstance("xa-pedido").ExecuteAsync();

		cluster.Comandos.ShouldBe(new[] { "deploy xa-pedido.bpmn", "create def-xa-pedido" });
	}

	[Fact(DisplayName = "On() on a Camunda chain also opens a new step")]
	public async Task On_Opens_A_New_Step()
	{
		var cluster = Publicado();

		await cluster.Cenario().StartInstance("pedido").On().StartInstance("pedido").ExecuteAsync();

		cluster.Comandos.Count(c => c == "create def-pedido-v1").ShouldBe(2);
	}

	[Fact(DisplayName = "Without And(), a second StartInstance rewrites the step being described, so only one instance starts")]
	public async Task Without_And_The_Step_Is_Rewritten()
	{
		var cluster = Publicado();

		await cluster.Cenario().StartInstance("pedido").When().StartInstance("pedido").Then().ExecuteAsync();

		cluster.Comandos.Count(c => c == "create def-pedido-v1").ShouldBe(1);
	}

	[Fact(DisplayName = "The connectives keep the chain a Camunda chain, also when called as a plain ITestScenario")]
	public void Connectives_Keep_The_Camunda_Chain()
	{
		var cadeia = CamundaScenario.De(new ClusterFalso().Cenario());
		ITestScenario comoCore = cadeia;

		cadeia.When().ShouldBeSameAs(cadeia);
		cadeia.Then().ShouldBeSameAs(cadeia);
		comoCore.And().ShouldBeSameAs(cadeia);
		comoCore.On().ShouldBeSameAs(cadeia);
		comoCore.When().ShouldBeSameAs(cadeia);
		comoCore.Then().ShouldBeSameAs(cadeia);
		CamundaScenario.De(cadeia).ShouldBeSameAs(cadeia);
	}

	[Fact(DisplayName = "After And(), the Camunda chain tells the Core a new step begins, as the plain chain would")]
	public void Step_Boundary_Is_Forwarded()
	{
		var cenario = new ClusterFalso().Cenario();
		var cadeia = CamundaScenario.De(cenario).StartInstance("pedido");

		FronteiraDePasso.ComecaPassoNovo(cadeia).ShouldBeFalse();
		cadeia.And();
		FronteiraDePasso.ComecaPassoNovo(cadeia).ShouldBeTrue();
		FronteiraDePasso.ComecaPassoNovo(cenario).ShouldBeTrue();
	}

	[Fact(DisplayName = "A step set and a check added through the Camunda chain run when the chain executes")]
	public async Task Step_And_Check_Through_The_Chain()
	{
		var cluster = Publicado();
		var cadeia = CamundaScenario.De(cluster.Cenario());
		ITestStepResult? visto = null;

		cadeia.SetCurrentStep(new CamundaStartInstanceStep { ProcessId = "pedido" });
		cadeia.AddValidation(r => visto = r);
		await cadeia.ExecutePendingAsync();

		cadeia.CurrentStep.ShouldBeOfType<CamundaStartInstanceStep>().IsExecuted.ShouldBeTrue();
		visto.ShouldNotBeNull().Success.ShouldBeTrue();
		cluster.Comandos.ShouldContain("create def-pedido-v1");
	}

	[Fact(DisplayName = "SaveStep on a Camunda chain runs the pending start and keeps its result under the name")]
	public void Save_Step_Runs_The_Start()
	{
		var cluster = Publicado();
		var cadeia = cluster.Cenario().StartInstance("pedido");

		cadeia.SaveStep("inicio");

		cadeia.Context.Steps.TryGet("inicio").ShouldNotBeNull().IsExecuted.ShouldBeTrue();
		cluster.Comandos.ShouldContain("create def-pedido-v1");
	}

	[Fact(DisplayName = "Execute() runs the chain synchronously and Validate checks the start result")]
	public void Execute_Synchronously_With_Validate()
	{
		var cluster = Publicado(aoCriar: (c, chave) => c.NovoJob(chave, "cobrar", "cobrar"));
		string? chaveVista = null;

		cluster.Cenario()
			.MockJobWorker("cobrar").ThenComplete()
			.StartInstance("pedido")
			.Validate(r => chaveVista = r.ProcessInstanceKey)
			.Execute()
			.AssertInstanceCompleted();

		chaveVista.ShouldBe("pi-1");
	}

	[Fact(DisplayName = "FollowInstance by a variable, after a mock, follows the instance through the Camunda chain")]
	public async Task Follow_By_Variable_Through_The_Chain()
	{
		var cluster = new ClusterFalso();
		var daApi = cluster.NovaInstancia("pedido");
		cluster.Variavel(daApi, "pedidoId", 7);
		cluster.NovoJob(daApi, "cobrar", "cobrar");

		var execucao = await cluster.Cenario()
			.MockJobWorker("cobrar").ThenComplete()
			.FollowInstance("pedido", ("pedidoId", 7))
			.ExecuteAsync()
			.AssertInstanceCompleted();

		execucao.GetResult().ProcessInstanceKey.ShouldBe(daApi);
	}

	// ---------- Iniciar ----------

	[Fact(DisplayName = "A start the cluster refuses fails the step with Camunda's code")]
	public async Task Start_Refused()
	{
		var cluster = Publicado();
		cluster.Falha = op => op == nameof(ICamundaGateway.CreateInstanceAsync) ? new CamundaRecusouException("FORBIDDEN", "no permission") : null;
		var passo = new CamundaStartInstanceStep { ProcessId = "pedido" };

		var resultado = (CamundaStepResult)await passo.ExecuteAsync(cluster.Cenario().Context);

		resultado.Success.ShouldBeFalse();
		resultado.Errors.ShouldHaveSingleItem().ShouldBe("Camunda rejected starting 'pedido': FORBIDDEN: no permission");
		passo.IsValid.ShouldBeFalse();
	}

	[Fact(DisplayName = "A start lost to the network fails the step with the error's message")]
	public async Task Start_Network_Failure()
	{
		var cluster = Publicado();
		cluster.Falha = op => op == nameof(ICamundaGateway.CreateInstanceAsync) ? new HttpRequestException("connection refused") : null;
		var passo = new CamundaStartInstanceStep { ProcessId = "pedido" };

		var resultado = await passo.ExecuteAsync(cluster.Cenario().Context);

		resultado.Errors.ShouldHaveSingleItem().ShouldBe("Could not start 'pedido': connection refused");
	}

	[Fact(DisplayName = "The start step is a Camunda step, validated only after it ran")]
	public async Task Start_Step_Validate()
	{
		var cluster = Publicado(aoCriar: (c, chave) => c.Estado(chave, "COMPLETED"));
		var passo = new CamundaStartInstanceStep { ProcessId = "pedido" };

		passo.StepType.ShouldBe("Camunda");
		passo.Name.ShouldBeNull();
		Should.Throw<InvalidOperationException>(() => passo.Validate(_ => { }))
			.Message.ShouldContain("Execute the chain before validating");

		await passo.ExecuteAsync(cluster.Cenario().Context);
		ITestStepResult? visto = null;
		passo.Validate(r => visto = r);

		visto.ShouldBeSameAs(passo.Result);
		passo.IsValid.ShouldBeTrue();
	}

	// ---------- Seguir ----------

	[Fact(DisplayName = "Following by key, when the cluster refuses the search, fails the step with Camunda's code")]
	public async Task Follow_Refused()
	{
		var cluster = new ClusterFalso
		{
			Falha = op => op == nameof(ICamundaGateway.GetInstanceAsync) ? new CamundaRecusouException("UNAUTHORIZED", "bad credentials") : null
		};
		var passo = new CamundaFollowInstanceStep { ProcessInstanceKey = "pi-9" };

		var resultado = await passo.ExecuteAsync(cluster.Cenario().Context);

		resultado.Success.ShouldBeFalse();
		resultado.Errors.ShouldHaveSingleItem().ShouldBe(
			"Camunda rejected looking for the process instance pi-9: UNAUTHORIZED: bad credentials");
		passo.IsValid.ShouldBeFalse();
	}

	[Fact(DisplayName = "Following by variable, when the network fails, fails the step saying what it looked for")]
	public async Task Follow_Network_Failure()
	{
		var cluster = new ClusterFalso
		{
			Falha = op => op == nameof(ICamundaGateway.SearchInstancesByVariableAsync) ? new HttpRequestException("connection refused") : null
		};
		var passo = new CamundaFollowInstanceStep { ProcessId = "pedido", VariableName = "pedidoId", VariableValue = 7 };

		var resultado = await passo.ExecuteAsync(cluster.Cenario().Context);

		resultado.Errors.ShouldHaveSingleItem().ShouldBe("Could not follow the instance of 'pedido' with pedidoId = 7: connection refused");
	}

	[Fact(DisplayName = "The follow step is a Camunda step, validated only after it ran")]
	public async Task Follow_Step_Validate()
	{
		var cluster = new ClusterFalso();
		var instancia = cluster.NovaInstancia("pedido");
		cluster.Estado(instancia, "COMPLETED");
		var passo = new CamundaFollowInstanceStep { ProcessInstanceKey = instancia };

		passo.StepType.ShouldBe("Camunda");
		passo.Name.ShouldBeNull();
		Should.Throw<InvalidOperationException>(() => passo.Validate(_ => { }));

		await passo.ExecuteAsync(cluster.Cenario().Context);
		ITestStepResult? visto = null;
		passo.Validate(r => visto = r);

		visto.ShouldBeSameAs(passo.Result);
		passo.IsValid.ShouldBeTrue();
	}

	// ---------- Apoio ----------

	private static ClusterFalso Publicado(Action<ClusterFalso, string>? aoCriar = null)
	{
		var cluster = new ClusterFalso { AoCriarInstancia = aoCriar ?? ((c, chave) => c.Estado(chave, "COMPLETED")) };
		cluster.Publicar("pedido", 1);
		return cluster;
	}
}
