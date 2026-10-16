using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

using XUnitAssured.Camunda.Mocks;

namespace XUnitAssured.Tests.CamundaTests;

/// <summary>
/// O laço que resolve os jobs de uma instância, contra um cluster falso.
///
/// <para>
/// É aqui que mora a garantia do pacote num cluster compartilhado: o mock só toca nos jobs da
/// própria instância e das que ela chamou. A Fase 0 provou isso contra um Camunda de verdade; estes
/// testes travam o comportamento sem precisar de um.
/// </para>
/// </summary>
[Trait("Category", "Camunda")]
[Trait("Component", "Mocks")]
public class MocksDaInstanciaTests
{
	private static readonly Dictionary<string, JobMock> CompletarCobranca = new()
	{
		["cobrar"] = new JobMock("cobrar", AcaoDoMock.Completar, new { aprovado = true })
	};

	[Fact(DisplayName = "The mock resolves its own instance's job and leaves another test's job of the same type alone")]
	public async Task Resolves_Only_Its_Own_Instance()
	{
		var cluster = new ClusterFalso();
		var minha = cluster.NovaInstancia("pedido");
		var deOutroTeste = cluster.NovaInstancia("pedido");
		var meuJob = cluster.NovoJob(minha, "cobrar", "cobrar");
		var jobDoOutro = cluster.NovoJob(deOutroTeste, "cobrar", "cobrar");

		var mocks = Iniciar(cluster, minha, CompletarCobranca);
		await Ate(() => cluster.EstadoDe(minha) == "COMPLETED");
		await mocks.PararAsync();

		cluster.JobEstado(meuJob).ShouldBe("COMPLETED");
		cluster.JobEstado(jobDoOutro).ShouldBe("CREATED");
		mocks.Invocacoes("cobrar").ShouldBe(1);
	}

	[Fact(DisplayName = "The mock reaches the jobs of the instances its instance calls")]
	public async Task Reaches_Child_Instances()
	{
		var cluster = new ClusterFalso();
		var pai = cluster.NovaInstancia("pedido");
		var filha = cluster.NovaInstancia("envio", pai);
		var neta = cluster.NovaInstancia("etiqueta", filha);
		var jobDaNeta = cluster.NovoJob(neta, "cobrar", "cobrar");

		var mocks = Iniciar(cluster, pai, CompletarCobranca);
		await Ate(() => cluster.JobEstado(jobDaNeta) == "COMPLETED");
		await mocks.PararAsync();

		mocks.Instancias.ShouldBe(new[] { pai, filha, neta }, ignoreOrder: true);
	}

	[Fact(DisplayName = "A BPMN error mock throws the error instead of completing")]
	public async Task Throws_Bpmn_Error()
	{
		var cluster = new ClusterFalso();
		var instancia = cluster.NovaInstancia("pedido");
		var job = cluster.NovoJob(instancia, "cobrar", "cobrar");

		var mocks = Iniciar(cluster, instancia, new Dictionary<string, JobMock>
		{
			["cobrar"] = new JobMock("cobrar", AcaoDoMock.LancarErroBpmn, null, "PAGAMENTO_RECUSADO", "sem saldo")
		});
		await Ate(() => cluster.JobEstado(job) == "ERROR_THROWN");
		await mocks.PararAsync();

		cluster.Comandos.ShouldContain($"error {job} PAGAMENTO_RECUSADO");
	}

	[Fact(DisplayName = "A job leased by another worker becomes a problem that names the cause, instead of a silent wait")]
	public async Task Leased_Job_Is_A_Problem()
	{
		var cluster = new ClusterFalso();
		var instancia = cluster.NovaInstancia("pedido");
		cluster.NovoJob(instancia, "cobrar", "cobrar", comLeaseDeOutroWorker: true);

		var mocks = Iniciar(cluster, instancia, CompletarCobranca);
		await Ate(() => mocks.Problemas.Count > 0);
		await mocks.PararAsync();

		var problema = mocks.Problemas.ShouldHaveSingleItem();
		problema.ShouldContain("held by another worker with a lease");
		problema.ShouldContain("'cobrar'");
	}

	[Fact(DisplayName = "A job of a type with no mock is left for whoever owns it")]
	public async Task Unmocked_Type_Is_Left_Alone()
	{
		var cluster = new ClusterFalso();
		var instancia = cluster.NovaInstancia("pedido");
		var job = cluster.NovoJob(instancia, "despachar", "despachar");

		var mocks = Iniciar(cluster, instancia, CompletarCobranca);
		await Task.Delay(150);

		cluster.JobEstado(job).ShouldBe("CREATED");
		(await mocks.PendentesSemMockAsync(default)).ShouldHaveSingleItem().Type.ShouldBe("despachar");
		await mocks.PararAsync();
	}

	[Fact(DisplayName = "A network failure is retried on the next round, not turned into a test failure")]
	public async Task Network_Failure_Is_Retried()
	{
		var cluster = new ClusterFalso { BuscasDeJobQueFalham = 3 };
		var instancia = cluster.NovaInstancia("pedido");
		var job = cluster.NovoJob(instancia, "cobrar", "cobrar");

		var mocks = Iniciar(cluster, instancia, CompletarCobranca);
		await Ate(() => cluster.JobEstado(job) == "COMPLETED");
		await mocks.PararAsync();

		mocks.Problemas.ShouldBeEmpty();
	}

	[Fact(DisplayName = "The loop stops by itself once the instance has finished")]
	public async Task Stops_When_The_Instance_Finishes()
	{
		var cluster = new ClusterFalso();
		var instancia = cluster.NovaInstancia("pedido");
		cluster.Estado(instancia, "COMPLETED");

		var mocks = Iniciar(cluster, instancia, CompletarCobranca);
		await mocks.Rodando.WaitAsync(TimeSpan.FromSeconds(5));

		mocks.Encerrado.ShouldBeTrue();
		cluster.Canceladas.ShouldBeEmpty();
	}

	[Fact(DisplayName = "An instance still active when the mocks' lifetime ends is cancelled, so it is not left behind on a shared cluster")]
	public async Task Forgotten_Instance_Is_Cancelled()
	{
		var cluster = new ClusterFalso();
		var instancia = cluster.NovaInstancia("pedido");
		cluster.NovoJob(instancia, "despachar", "despachar");

		var mocks = Iniciar(cluster, instancia, CompletarCobranca, vidaSegundos: 1);
		await mocks.Rodando.WaitAsync(TimeSpan.FromSeconds(10));

		cluster.Canceladas.ShouldBe(new[] { instancia });
	}

	private static MocksDaInstancia Iniciar(
		ClusterFalso cluster, string instancia, Dictionary<string, JobMock> mocks, int vidaSegundos = 30)
	{
		var execucao = new MocksDaInstancia(cluster, instancia, mocks, ClusterFalso.Rapido(vidaDosMocksSegundos: vidaSegundos));
		execucao.Iniciar();
		return execucao;
	}

	private static async Task Ate(Func<bool> condicao)
	{
		var cronometro = Stopwatch.StartNew();
		while (!condicao())
		{
			if (cronometro.Elapsed > TimeSpan.FromSeconds(10))
				throw new TimeoutException("The condition was not met within 10 s.");

			await Task.Delay(10);
		}
	}
}
