using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

using XUnitAssured.Camunda.Extensions;
using XUnitAssured.Core.DSL;

namespace XUnitAssured.Tests.CamundaTests;

/// <summary>
/// <c>FollowInstance</c>: a instância que a API testada iniciou, e não o teste.
///
/// <para>
/// É o teste de ponta a ponta de quem integra uma API com o Camunda: o teste chama a API, e então
/// acompanha a instância que ela abriu, com os mesmos mocks e asserções de um <c>StartInstance</c>.
/// </para>
/// </summary>
[Trait("Category", "Camunda")]
[Trait("Component", "DSL")]
public class FollowInstanceTests
{
	[Fact(DisplayName = "FollowInstance by key puts the mocks to work on an instance someone else started")]
	public async Task Follow_By_Key()
	{
		var cluster = new ClusterFalso();
		var daApi = cluster.NovaInstancia("pedido");
		cluster.NovoJob(daApi, "cobrar", "cobrar");

		var execucao = await cluster.Cenario()
			.MockJobWorker("cobrar").ThenComplete(new { pago = true })
			.FollowInstance(daApi)
			.ExecuteAsync()
			.AssertInstanceCompleted()
			.AssertVariable("pago", true)
			.AssertJobMocked("cobrar");

		execucao.GetResult().ProcessInstanceKey.ShouldBe(daApi);
		execucao.GetResult().ProcessId.ShouldBe("pedido");
	}

	[Fact(DisplayName = "FollowInstance waits for an instance the API starts a moment after it answered")]
	public async Task Waits_For_The_Instance()
	{
		var cluster = new ClusterFalso();
		_ = Task.Run(async () =>
		{
			await Task.Delay(300);
			var tardia = cluster.NovaInstancia("pedido");
			cluster.Variavel(tardia, "pedidoId", 42);
			cluster.Estado(tardia, "COMPLETED");
		});

		var execucao = await cluster.Cenario(ClusterFalso.Rapido(prazoSegundos: 5))
			.FollowInstance("pedido", ("pedidoId", 42))
			.ExecuteAsync()
			.AssertInstanceCompleted();

		execucao.GetResult().Warnings.ShouldBeEmpty();
	}

	[Fact(DisplayName = "When more than one instance has the value, FollowInstance takes the newest and warns")]
	public async Task Newest_With_A_Warning()
	{
		var cluster = new ClusterFalso();
		var antiga = cluster.NovaInstancia("pedido");
		cluster.Variavel(antiga, "pedidoId", "P-1");
		var nova = cluster.NovaInstancia("pedido");
		cluster.Variavel(nova, "pedidoId", "P-1");

		var resultado = (await cluster.Cenario().FollowInstance("pedido", ("pedidoId", "P-1")).ExecuteAsync()).GetResult();

		resultado.ProcessInstanceKey.ShouldBe(nova);
		resultado.Warnings.Single().ShouldContain($"2 instances of 'pedido' have pedidoId = \"P-1\"; following the newest, {nova}");
	}

	[Fact(DisplayName = "An instance that never shows up fails the step, saying which one it waited for")]
	public async Task Missing_Instance_Fails()
	{
		var cluster = new ClusterFalso();

		var execucao = await cluster.Cenario().FollowInstance("pedido", ("pedidoId", 99)).ExecuteAsync();

		execucao.AssertFailure();
		execucao.GetResult().Errors.Single()
			.ShouldContain("No instance of 'pedido' with pedidoId = 99 showed up on the cluster within 1s");
	}

	[Fact(DisplayName = "FollowInstance needs a key, or a process id and a variable name")]
	public void Needs_Something_To_Follow()
	{
		Should.Throw<ArgumentException>(() => ScenarioDsl.Given().FollowInstance(" "));
		Should.Throw<ArgumentException>(() => ScenarioDsl.Given().FollowInstance("", ("pedidoId", 1)));
		Should.Throw<ArgumentException>(() => ScenarioDsl.Given().FollowInstance("pedido", ("", 1)));
	}
}
