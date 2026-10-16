using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

using XUnitAssured.Camunda.Extensions;
using XUnitAssured.Camunda.Steps;
using XUnitAssured.Core.DSL;

namespace XUnitAssured.Tests.CamundaTests;

/// <summary>
/// <c>Deploy("pedido")</c>, que acha o arquivo do repositório pelo id do processo, e
/// <c>StartInstance</c> sobre o que já está no cluster, a última versão ou a pedida.
///
/// <para>
/// São os dois usos do pacote: o desenvolvedor testa o diagrama que mudou, e o QA testa o que foi
/// publicado na homologação, ponta a ponta ou mockando só o que não tem worker lá.
/// </para>
/// </summary>
[Trait("Category", "Camunda")]
[Trait("Component", "DSL")]
public class IdEVersaoDoCamundaTests
{
	// ---------- Deploy pelo id ----------

	[Fact(DisplayName = "Deploy by process id finds the BPMN file that declares it in the output folder")]
	public async Task Deploy_By_Id_Finds_The_File()
	{
		var cluster = new ClusterFalso { AoCriarInstancia = (c, chave) => c.NovoJob(chave, "xa-cobrar", "cobrar") };

		await cluster.Cenario()
			.Deploy("xa-pedido", "xa-envio")
			.MockJobWorker("xa-cobrar").ThenComplete()
			.StartInstance("xa-pedido")
			.ExecuteAsync()
			.AssertInstanceCompleted();

		cluster.Comandos.ShouldContain("deploy xa-pedido.bpmn,xa-envio.bpmn");
		cluster.Comandos.ShouldContain("create def-xa-pedido");
	}

	[Fact(DisplayName = "A file named twice, by path and by id, is deployed once: Camunda refuses a duplicate resource")]
	public async Task Same_File_Twice_Is_Deployed_Once()
	{
		var cluster = new ClusterFalso();

		await cluster.Cenario().Deploy("CamundaTests/bpmn/xa-envio.bpmn", "xa-envio", "xa-pedido").ExecuteAsync();

		cluster.Comandos.ShouldContain("deploy xa-envio.bpmn,xa-pedido.bpmn");
	}

	[Fact(DisplayName = "An unknown process id fails the deploy, listing the processes the output folder has")]
	public async Task Unknown_Id_Lists_What_Exists()
	{
		var cluster = new ClusterFalso();

		var execucao = await cluster.Cenario().Deploy("xa-inexistente").StartInstance("xa-inexistente").ExecuteAsync();

		var erro = execucao.GetResult().Errors.Single();
		erro.ShouldContain("because the deployment failed");
		erro.ShouldContain("declares process 'xa-inexistente'");
		erro.ShouldContain("'xa-pedido'");
		erro.ShouldContain("CopyToOutputDirectory");
		cluster.Comandos.ShouldBeEmpty();
	}

	[Theory(DisplayName = "A path has a folder separator or a deployable extension; anything else is a process id")]
	[InlineData("pedido.bpmn", true)]
	[InlineData("bpmn/pedido", true)]
	[InlineData(@"bpmn\pedido", true)]
	[InlineData("regras.DMN", true)]
	[InlineData("tela.form", true)]
	[InlineData("pedido", false)]
	[InlineData("pedido.v2", false)]
	public void Path_Or_Id(string argumento, bool caminho) =>
		ArquivosBpmn.EhCaminho(argumento).ShouldBe(caminho);

	[Fact(DisplayName = "A process id declared in two files is refused, naming both, instead of one being picked")]
	public void Id_In_Two_Files_Is_Refused()
	{
		var pasta = PastaTemporaria();
		try
		{
			File.WriteAllText(Path.Combine(pasta, "a.bpmn"), Bpmn("pedido"));
			Directory.CreateDirectory(Path.Combine(pasta, "velho"));
			File.WriteAllText(Path.Combine(pasta, "velho", "b.bpmn"), Bpmn("pedido"));
			File.WriteAllText(Path.Combine(pasta, "c.bpmn"), Bpmn("envio", "cobranca"));

			var erro = Should.Throw<InvalidOperationException>(() => ArquivosBpmn.Localizar("pedido", pasta));
			erro.Message.ShouldContain("a.bpmn");
			erro.Message.ShouldContain(Path.Combine("velho", "b.bpmn"));

			// Um arquivo com dois processos responde pelos dois.
			ArquivosBpmn.Localizar("cobranca", pasta).ShouldEndWith("c.bpmn");
		}
		finally
		{
			Directory.Delete(pasta, true);
		}
	}

	[Fact(DisplayName = "A BPMN file that is not valid XML is named when the id is not found")]
	public void Unreadable_File_Is_Named()
	{
		var pasta = PastaTemporaria();
		try
		{
			File.WriteAllText(Path.Combine(pasta, "quebrado.bpmn"), "<definitions><process id=\"pedido\">");

			Should.Throw<FileNotFoundException>(() => ArquivosBpmn.Localizar("pedido", pasta))
				.Message.ShouldContain("could not be read as XML: quebrado.bpmn");
		}
		finally
		{
			Directory.Delete(pasta, true);
		}
	}

	// ---------- StartInstance sobre o que está no cluster ----------

	[Fact(DisplayName = "Without Deploy, StartInstance starts the latest version on the cluster and says which one ran")]
	public async Task Without_Deploy_Starts_The_Latest()
	{
		var cluster = new ClusterFalso();
		cluster.Publicar("xa-pedido", 1);
		cluster.Publicar("xa-pedido", 2, "1.4.0");
		cluster.Publicar("xa-pedido", 3, "1.5.0");

		var resultado = (await cluster.Cenario().StartInstance("xa-pedido").ExecuteAsync()).GetResult();

		resultado.Success.ShouldBeTrue();
		resultado.Version.ShouldBe(3);
		resultado.VersionTag.ShouldBe("1.5.0");
		resultado.DefinitionKey.ShouldBe("def-xa-pedido-v3");
		cluster.Comandos.ShouldContain("create def-xa-pedido-v3");
	}

	[Fact(DisplayName = "A version number starts exactly that version")]
	public async Task Version_Number()
	{
		var cluster = new ClusterFalso();
		cluster.Publicar("xa-pedido", 1);
		cluster.Publicar("xa-pedido", 2);

		var resultado = (await cluster.Cenario().StartInstance("xa-pedido", version: 1).ExecuteAsync()).GetResult();

		resultado.Version.ShouldBe(1);
		cluster.Comandos.ShouldContain("create def-xa-pedido-v1");
	}

	[Fact(DisplayName = "A version tag starts the newest version that carries it")]
	public async Task Version_Tag()
	{
		var cluster = new ClusterFalso();
		cluster.Publicar("xa-pedido", 4, "1.4.0");
		cluster.Publicar("xa-pedido", 5, "1.4.0");
		cluster.Publicar("xa-pedido", 6, "1.5.0");

		var resultado = (await cluster.Cenario().StartInstance("xa-pedido", versionTag: "1.4.0").ExecuteAsync()).GetResult();

		resultado.Version.ShouldBe(5);
		resultado.VersionTag.ShouldBe("1.4.0");
	}

	[Fact(DisplayName = "A version the cluster does not have fails, listing the versions it has, newest first")]
	public async Task Missing_Version_Lists_Versions()
	{
		var cluster = new ClusterFalso();
		cluster.Publicar("xa-pedido", 1);
		cluster.Publicar("xa-pedido", 2, "1.4.0");

		var execucao = await cluster.Cenario().StartInstance("xa-pedido", versionTag: "9.9.9").ExecuteAsync();

		execucao.AssertFailure();
		var erro = execucao.GetResult().Errors.Single();
		erro.ShouldContain("version tag '9.9.9' of process 'xa-pedido'");
		erro.ShouldContain("is not deployed on http://camunda.falso:8080");
		erro.ShouldContain("Versions there, newest first: 2 (tag '1.4.0'), 1.");
		cluster.Comandos.ShouldNotContain(c => c.StartsWith("create"));
	}

	[Fact(DisplayName = "A process the cluster never saw fails, suggesting to deploy it")]
	public async Task Unknown_Process_Suggests_Deploy()
	{
		var cluster = new ClusterFalso();

		var erro = (await cluster.Cenario().StartInstance("xa-pedido").ExecuteAsync()).GetResult().Errors.Single();

		erro.ShouldContain("Could not start process 'xa-pedido': it is not deployed on");
		erro.ShouldContain("call Deploy(...) in this chain");
	}

	[Fact(DisplayName = "An explicit version wins over the chain's deploy: the test asked for it by name")]
	public async Task Explicit_Version_Wins_Over_Deploy()
	{
		var cluster = new ClusterFalso();
		cluster.Publicar("xa-pedido", 7);

		var resultado = (await cluster.Cenario()
			.Deploy("xa-pedido", "xa-envio")
			.StartInstance("xa-pedido", version: 7)
			.ExecuteAsync()).GetResult();

		resultado.Version.ShouldBe(7);
		cluster.Comandos.ShouldContain("create def-xa-pedido-v7");
	}

	[Fact(DisplayName = "A failed deploy is never replaced by the version already on the cluster")]
	public async Task Failed_Deploy_Does_Not_Fall_Back()
	{
		var cluster = new ClusterFalso();
		cluster.Publicar("xa-pedido", 3);

		var execucao = await cluster.Cenario().Deploy("nao-existe.bpmn").StartInstance("xa-pedido", version: 3).ExecuteAsync();

		execucao.GetResult().Errors.Single().ShouldContain("because the deployment failed");
		cluster.Comandos.ShouldNotContain(c => c.StartsWith("create"));
	}

	[Fact(DisplayName = "A failed assertion names the version that ran")]
	public async Task Failure_Names_The_Version()
	{
		var cluster = new ClusterFalso();
		cluster.Publicar("xa-pedido", 3, "1.5.0");

		var execucao = await cluster.Cenario().StartInstance("xa-pedido").ExecuteAsync();

		Should.Throw<ShouldAssertException>(() => execucao.AssertInstanceCompleted())
			.Message.ShouldContain("(xa-pedido version 3, tag '1.5.0') to complete");
	}

	[Fact(DisplayName = "Version and tag together, a version below 1 or an empty tag are refused when the chain is written")]
	public void Bad_Versions_Are_Refused()
	{
		Should.Throw<ArgumentException>(() => ScenarioDsl.Given().StartInstance("p", version: 1, versionTag: "1.0"));
		Should.Throw<ArgumentOutOfRangeException>(() => ScenarioDsl.Given().StartInstance("p", version: 0));
		Should.Throw<ArgumentException>(() => ScenarioDsl.Given().StartInstance("p", versionTag: " "));
	}

	// ---------- O QA na homologação ----------

	[Fact(DisplayName = "End to end with no mock: the real workers move the instance and the assertions wait for them")]
	public async Task End_To_End_Without_Mocks()
	{
		var cluster = new ClusterFalso { AoCriarInstancia = (c, chave) => TrabalhadorDeVerdade(c, c.NovoJob(chave, "xa-cobrar", "cobrar")) };
		cluster.Publicar("xa-pedido", 2);

		await cluster.Cenario(ClusterFalso.Rapido(prazoSegundos: 5))
			.StartInstance("xa-pedido", new { pedidoId = 1 })
			.ExecuteAsync()
			.AssertInstanceCompleted()
			.AssertCompletedElements("cobrar");
	}

	[Fact(DisplayName = "Mocking only the type with no real worker: the mock answers it and the real worker the rest")]
	public async Task Mock_Only_What_Has_No_Worker()
	{
		var cluster = new ClusterFalso
		{
			AoCriarInstancia = (c, chave) =>
			{
				c.NovoJob(chave, "xa-sistema-externo", "consultar");
				TrabalhadorDeVerdade(c, c.NovoJob(chave, "xa-cobrar", "cobrar"));
			}
		};
		cluster.Publicar("xa-pedido", 2);

		await cluster.Cenario(ClusterFalso.Rapido(prazoSegundos: 5))
			.MockJobWorker("xa-sistema-externo").ThenComplete(new { score = 700 })
			.StartInstance("xa-pedido")
			.ExecuteAsync()
			.AssertInstanceCompleted()
			.AssertJobMocked("xa-sistema-externo")
			.AssertVariable("score", 700);

		cluster.Comandos.Count(c => c.StartsWith("complete")).ShouldBe(2);
	}

	// ---------- Apoio ----------

	// Um worker de verdade, fora do teste: completa o job um pouco depois, sem passar pelo mock.
	private static void TrabalhadorDeVerdade(ClusterFalso cluster, string jobKey) =>
		_ = Task.Run(async () =>
		{
			await Task.Delay(50);
			await cluster.CompleteJobAsync(jobKey, null, CancellationToken.None);
		});

	private static string PastaTemporaria()
	{
		var pasta = Path.Combine(Path.GetTempPath(), "xa-bpmn-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(pasta);
		return pasta;
	}

	private static string Bpmn(params string[] processos) =>
		"<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
		"<bpmn:definitions xmlns:bpmn=\"http://www.omg.org/spec/BPMN/20100524/MODEL\" id=\"d\">" +
		string.Concat(processos.Select(p => $"<bpmn:process id=\"{p}\" isExecutable=\"true\" />")) +
		"</bpmn:definitions>";
}
