using System;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

using XUnitAssured.Camunda.Extensions;
using XUnitAssured.Core.DSL;

namespace XUnitAssured.Tests.CamundaTests;

/// <summary>
/// <c>FollowInstanceByInstanceVariable</c>: acompanha a instância de outro processo pelo valor que uma
/// variável da instância do teste só ganha enquanto ela roda.
///
/// <para>
/// O caso da portabilidade: o processo do teste recebe o NU da NUCLEA no meio do caminho, e a retenção do
/// banco de origem é a instância que tem esse mesmo NU. O valor não existe quando a cadeia é montada, então
/// o passo lê a variável da instância do teste, espera ela aparecer, e só então procura a outra.
/// </para>
/// </summary>
[Trait("Category", "Camunda")]
[Trait("Component", "DSL")]
public class FollowInstanceByInstanceVariableTests
{
	[Fact(DisplayName = "FollowInstanceByInstanceVariable follows the instance that has the value the test instance's variable holds")]
	public async Task Follows_By_The_Value_Of_The_Test_Instance()
	{
		var cluster = new ClusterFalso();
		var doTeste = cluster.NovaInstancia("portabilidade");
		cluster.Variavel(doTeste, "nu", "2099010100000001");
		var outra = cluster.NovaInstancia("retencao");
		cluster.Variavel(outra, "nu", "2099010100000009");
		var retencao = cluster.NovaInstancia("retencao");
		cluster.Variavel(retencao, "nu", "2099010100000001");
		cluster.Estado(retencao, "COMPLETED");

		var execucao = await cluster.Cenario()
			.FollowInstanceByInstanceVariable("retencao", "nu", doTeste, "nu")
			.ExecuteAsync()
			.AssertInstanceCompleted();

		execucao.GetResult().ProcessInstanceKey.ShouldBe(retencao);
		execucao.GetResult().ProcessId.ShouldBe("retencao");
	}

	[Fact(DisplayName = "FollowInstanceByInstanceVariable waits for the variable to show up on the test instance")]
	public async Task Waits_For_The_Variable()
	{
		var cluster = new ClusterFalso();
		var doTeste = cluster.NovaInstancia("portabilidade");
		var retencao = cluster.NovaInstancia("retencao");
		cluster.Variavel(retencao, "nu", 42);
		cluster.Estado(retencao, "COMPLETED");
		_ = Task.Run(async () =>
		{
			await Task.Delay(300);
			cluster.Variavel(doTeste, "nu", 42);
		});

		var execucao = await cluster.Cenario(ClusterFalso.Rapido(prazoSegundos: 5))
			.FollowInstanceByInstanceVariable("retencao", "nu", doTeste, "nu")
			.ExecuteAsync()
			.AssertInstanceCompleted();

		execucao.GetResult().ProcessInstanceKey.ShouldBe(retencao);
	}

	[Fact(DisplayName = "FollowInstanceByInstanceVariable waits for the followed instance once the value is known")]
	public async Task Waits_For_The_Followed_Instance()
	{
		var cluster = new ClusterFalso();
		var doTeste = cluster.NovaInstancia("portabilidade");
		cluster.Variavel(doTeste, "nu", "N-7");
		_ = Task.Run(async () =>
		{
			await Task.Delay(300);
			var tardia = cluster.NovaInstancia("retencao");
			cluster.Variavel(tardia, "nu", "N-7");
			cluster.Estado(tardia, "COMPLETED");
		});

		var execucao = await cluster.Cenario(ClusterFalso.Rapido(prazoSegundos: 5))
			.FollowInstanceByInstanceVariable("retencao", "nu", doTeste, "nu")
			.ExecuteAsync()
			.AssertInstanceCompleted();

		execucao.GetResult().ProcessId.ShouldBe("retencao");
	}

	[Fact(DisplayName = "A variable that never shows up on the test instance fails the step, saying which one")]
	public async Task Missing_Variable_Fails()
	{
		var cluster = new ClusterFalso();
		var doTeste = cluster.NovaInstancia("portabilidade");

		var execucao = await cluster.Cenario()
			.FollowInstanceByInstanceVariable("retencao", "nu", doTeste, "nu")
			.ExecuteAsync();

		execucao.AssertFailure();
		execucao.GetResult().Errors.ShouldHaveSingleItem()
			.ShouldContain($"The variable 'nu' never showed up on process instance {doTeste} within 1s");
	}

	[Fact(DisplayName = "A value with no instance to follow fails the step, saying the value it looked for")]
	public async Task Missing_Followed_Instance_Fails()
	{
		var cluster = new ClusterFalso();
		var doTeste = cluster.NovaInstancia("portabilidade");
		cluster.Variavel(doTeste, "nu", "N-8");

		var execucao = await cluster.Cenario()
			.FollowInstanceByInstanceVariable("retencao", "nu", doTeste, "nu")
			.ExecuteAsync();

		execucao.AssertFailure();
		execucao.GetResult().Errors.ShouldHaveSingleItem()
			.ShouldContain("No instance of 'retencao' with nu = \"N-8\" showed up on the cluster within 1s");
	}

	[Fact(DisplayName = "FollowInstanceByInstanceVariable needs the process, the variable, the test instance and its variable")]
	public void Needs_Everything()
	{
		Should.Throw<ArgumentException>(() => ScenarioDsl.Given().FollowInstanceByInstanceVariable(" ", "nu", "1", "nu"));
		Should.Throw<ArgumentException>(() => ScenarioDsl.Given().FollowInstanceByInstanceVariable("retencao", "", "1", "nu"));
		Should.Throw<ArgumentException>(() => ScenarioDsl.Given().FollowInstanceByInstanceVariable("retencao", "nu", " ", "nu"));
		Should.Throw<ArgumentException>(() => ScenarioDsl.Given().FollowInstanceByInstanceVariable("retencao", "nu", "1", ""));
	}
}
