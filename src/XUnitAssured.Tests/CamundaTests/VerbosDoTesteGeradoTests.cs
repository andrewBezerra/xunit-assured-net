using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

using XUnitAssured.Camunda;
using XUnitAssured.Camunda.Extensions;
using XUnitAssured.Core.DSL;

namespace XUnitAssured.Tests.CamundaTests;

/// <summary>
/// O worker que roda um passo: o mock que calcula a resposta a partir das variáveis do job, ou da
/// instância que espera a mensagem, em vez de devolver variáveis fixas. É onde um teste gerado roda
/// a chamada de verdade de um passo (o request HTTP, a mensagem no Kafka) e completa o job com o que
/// ela extraiu.
/// </summary>
[Trait("Category", "Camunda")]
[Trait("Component", "Mocks")]
public class MockQueCalculaTests
{
	private const string Pedido = "CamundaTests/bpmn/xa-pedido.bpmn";
	private const string Envio = "CamundaTests/bpmn/xa-envio.bpmn";

	[Fact(DisplayName = "ThenCompleteWith gets the job and the instance's variables, and completes the job with what it returns")]
	public async Task Job_Function_Completes_With_Its_Result()
	{
		var cluster = new ClusterFalso
		{
			AoCriarInstancia = (c, chave) =>
			{
				c.Variavel(chave, "pedidoId", 7);
				c.NovoJob(chave, "xa-cobrar", "cobrar");
			}
		};
		CamundaJobContext? visto = null;

		await cluster.Cenario()
			.Deploy(Pedido, Envio)
			.MockJobWorker("xa-cobrar").ThenCompleteWith(async job =>
			{
				await Task.Yield();
				visto = job;
				return new { cobrado = job.Variables["pedidoId"].GetInt32() * 10 };
			})
			.StartInstance("xa-pedido")
			.ExecuteAsync()
			.AssertInstanceCompleted()
			.AssertVariable("cobrado", 70)
			.AssertJobMocked("xa-cobrar");

		visto.ShouldNotBeNull();
		visto.JobType.ShouldBe("xa-cobrar");
		visto.ElementId.ShouldBe("cobrar");
		visto.JobKey.ShouldStartWith("job-");
		visto.ProcessInstanceKey.ShouldStartWith("pi-");
	}

	[Fact(DisplayName = "A function that returns null completes the job with no variables")]
	public async Task Job_Function_Returning_Null_Completes()
	{
		var cluster = new ClusterFalso { AoCriarInstancia = (c, chave) => c.NovoJob(chave, "xa-cobrar", "cobrar") };

		await cluster.Cenario()
			.Deploy(Pedido, Envio)
			.MockJobWorker("xa-cobrar").ThenCompleteWith(_ => Task.FromResult<object?>(null))
			.StartInstance("xa-pedido")
			.ExecuteAsync()
			.AssertInstanceCompleted()
			.AssertJobMocked("xa-cobrar");
	}

	[Fact(DisplayName = "A function that throws fails the job, and the test fails saying which job and why")]
	public async Task Job_Function_That_Throws_Fails_The_Test()
	{
		var cluster = new ClusterFalso { AoCriarInstancia = (c, chave) => c.NovoJob(chave, "xa-cobrar", "cobrar") };

		var execucao = await cluster.Cenario(ClusterFalso.Rapido(prazoSegundos: 10))
			.Deploy(Pedido, Envio)
			.MockJobWorker("xa-cobrar").ThenCompleteWith(_ => throw new InvalidOperationException("the API answered 500"))
			.StartInstance("xa-pedido")
			.ExecuteAsync();

		var falha = Should.Throw<ShouldAssertException>(() => execucao.AssertInstanceCompleted());
		falha.Message.ShouldContain("'xa-cobrar'");
		falha.Message.ShouldContain("the API answered 500");
		cluster.Comandos.ShouldContain(c => c.StartsWith("fail job-"));
		cluster.Comandos.ShouldNotContain(c => c.StartsWith("complete"));
	}

	[Fact(DisplayName = "ThenCorrelateWith gets the subscription and the instance's variables, and correlates with what it returns")]
	public async Task Message_Function_Correlates_With_Its_Result()
	{
		var cluster = Publicado(aoCriar: (c, chave) =>
		{
			c.Variavel(chave, "cpf", "123");
			c.NovaAssinatura(chave, "aguardar-pagamento", "pago", "P-1");
		});
		CamundaMessageContext? vista = null;

		await cluster.Cenario()
			.MockMessage("pago").ThenCorrelateWith(mensagem =>
			{
				vista = mensagem;
				return Task.FromResult<object?>(new { pagoPor = mensagem.Variables["cpf"].GetString() });
			})
			.StartInstance("pedido")
			.ExecuteAsync()
			.AssertInstanceCompleted()
			.AssertVariable("pagoPor", "123")
			.AssertMessageCorrelated("pago");

		vista.ShouldNotBeNull();
		vista.MessageName.ShouldBe("pago");
		vista.CorrelationKey.ShouldBe("P-1");
		vista.ElementId.ShouldBe("aguardar-pagamento");
		cluster.Comandos.ShouldContain("correlate pago P-1");
	}

	[Fact(DisplayName = "A message function that throws does not correlate, and the test fails saying why")]
	public async Task Message_Function_That_Throws_Fails_The_Test()
	{
		var cluster = Publicado(aoCriar: (c, chave) => c.NovaAssinatura(chave, "aguardar-pagamento", "pago", "P-1"));

		var execucao = await cluster.Cenario(ClusterFalso.Rapido(prazoSegundos: 10))
			.MockMessage("pago").ThenCorrelateWith(_ => throw new InvalidOperationException("no message on the topic"))
			.StartInstance("pedido")
			.ExecuteAsync();

		var falha = Should.Throw<ShouldAssertException>(() => execucao.AssertInstanceCompleted());
		falha.Message.ShouldContain("'pago'");
		falha.Message.ShouldContain("no message on the topic");
		cluster.Comandos.ShouldNotContain(c => c.StartsWith("correlate"));
	}

	[Fact(DisplayName = "The functions are required")]
	public void Functions_Are_Required()
	{
		Should.Throw<ArgumentNullException>(() => ScenarioDsl.Given().MockJobWorker("t").ThenCompleteWith(null!));
		Should.Throw<ArgumentNullException>(() => ScenarioDsl.Given().MockMessage("m").ThenCorrelateWith(null!));
	}

	private static ClusterFalso Publicado(Action<ClusterFalso, string> aoCriar)
	{
		var cluster = new ClusterFalso { AoCriarInstancia = aoCriar };
		var chave = cluster.Publicar("pedido", 1);
		cluster.DefinirBpmn(chave, ProcessoDoPedido);
		return cluster;
	}

	private const string ProcessoDoPedido = """
		<?xml version="1.0" encoding="UTF-8"?>
		<bpmn:definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL" id="d">
		  <bpmn:message id="Mensagem_Pago" name="pago" />
		  <bpmn:process id="pedido" isExecutable="true">
		    <bpmn:intermediateCatchEvent id="aguardar-pagamento"><bpmn:messageEventDefinition messageRef="Mensagem_Pago" /></bpmn:intermediateCatchEvent>
		  </bpmn:process>
		</bpmn:definitions>
		""";
}

/// <summary>
/// O cluster obrigatório: o teste que só faz sentido no Camunda falha quando não sabe qual cluster
/// usar, em vez de tentar o localhost.
/// </summary>
[Trait("Category", "Camunda")]
[Trait("Component", "Settings")]
public class ClusterObrigatorioTests
{
	private static readonly Func<string, string?> SemAmbiente = _ => null;

	[Fact(DisplayName = "LoadRequired fails when the file has no camunda section and no CAMUNDA_REST_ADDRESS is set, saying what to add")]
	public void Missing_Section_Fails()
	{
		var falha = Should.Throw<InvalidOperationException>(() => CamundaSettings.LoadRequired(null, SemAmbiente));

		falha.Message.ShouldContain("testsettings.json");
		falha.Message.ShouldContain("\"camunda\"");
		falha.Message.ShouldContain("restAddress");
		falha.Message.ShouldContain("CAMUNDA_REST_ADDRESS");
	}

	[Fact(DisplayName = "LoadRequired fails when the camunda section has no restAddress")]
	public void Section_Without_Address_Fails()
	{
		var semEndereco = new CamundaSettings { TenantId = "t" };

		Should.Throw<InvalidOperationException>(() => CamundaSettings.LoadRequired(semEndereco, SemAmbiente))
			.Message.ShouldContain("restAddress");
	}

	[Fact(DisplayName = "LoadRequired returns the section when it has the address")]
	public void Section_With_Address_Is_Returned()
	{
		var lida = new CamundaSettings { RestAddress = "http://camunda:8080" };

		CamundaSettings.LoadRequired(lida, SemAmbiente).ShouldBeSameAs(lida);
	}

	[Fact(DisplayName = "LoadRequired accepts the address from CAMUNDA_REST_ADDRESS, as the client reads it")]
	public void Address_From_The_Environment_Is_Enough()
	{
		var ambiente = new Dictionary<string, string> { ["CAMUNDA_REST_ADDRESS"] = "http://camunda:8080" };

		var settings = CamundaSettings.LoadRequired(null, n => ambiente.GetValueOrDefault(n));

		settings.RestAddress.ShouldBeNull();
	}
}

/// <summary>
/// O caminho esperado de um teste gerado: quantas vezes um elemento rodou e a que fim a instância
/// chegou.
/// </summary>
[Trait("Category", "Camunda")]
[Trait("Component", "Assertions")]
public class CaminhoDaInstanciaTests
{
	private const string Pedido = "CamundaTests/bpmn/xa-pedido.bpmn";
	private const string Envio = "CamundaTests/bpmn/xa-envio.bpmn";

	[Fact(DisplayName = "AssertElementCompleted counts the element's completed instances")]
	public async Task Counts_Completions()
	{
		var cluster = new ClusterFalso
		{
			AoCriarInstancia = (c, chave) =>
			{
				c.MaisUmElemento(chave, "consultar", "SERVICE_TASK", "COMPLETED");
				c.MaisUmElemento(chave, "consultar", "SERVICE_TASK", "COMPLETED");
				c.Estado(chave, "COMPLETED");
			}
		};

		await cluster.Cenario()
			.Deploy(Pedido, Envio)
			.StartInstance("xa-pedido")
			.ExecuteAsync()
			.AssertElementCompleted("consultar", 2)
			.AssertElementCompleted("nunca", 0);
	}

	[Fact(DisplayName = "AssertElementCompleted gives up at once when the element completed more times than expected")]
	public async Task More_Completions_Fail_At_Once()
	{
		var cluster = new ClusterFalso
		{
			AoCriarInstancia = (c, chave) =>
			{
				for (var i = 0; i < 3; i++)
					c.MaisUmElemento(chave, "consultar", "SERVICE_TASK", "COMPLETED");
			}
		};

		var execucao = await cluster.Cenario(ClusterFalso.Rapido(prazoSegundos: 10))
			.Deploy(Pedido, Envio)
			.StartInstance("xa-pedido")
			.ExecuteAsync();

		var cronometro = System.Diagnostics.Stopwatch.StartNew();
		var falha = Should.Throw<ShouldAssertException>(() => execucao.AssertElementCompleted("consultar", 2));
		cronometro.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(5));
		falha.Message.ShouldContain("to complete 'consultar' 2 time(s)");
		falha.Message.ShouldContain("it completed 3 time(s)");
	}

	[Fact(DisplayName = "AssertElementCompleted with fewer completions gives up when the instance has finished")]
	public async Task Fewer_Completions_Fail_When_The_Instance_Finished()
	{
		var cluster = new ClusterFalso
		{
			AoCriarInstancia = (c, chave) =>
			{
				c.MaisUmElemento(chave, "consultar", "SERVICE_TASK", "COMPLETED");
				c.Estado(chave, "COMPLETED");
			}
		};

		var execucao = await cluster.Cenario(ClusterFalso.Rapido(prazoSegundos: 10))
			.Deploy(Pedido, Envio)
			.StartInstance("xa-pedido")
			.ExecuteAsync();

		var falha = Should.Throw<ShouldAssertException>(() => execucao.AssertElementCompleted("consultar", 2));
		falha.Message.ShouldContain("it completed 1 time(s), and the instance is COMPLETED");
	}

	[Fact(DisplayName = "AssertEndedAt passes when the instance completed through that end event")]
	public async Task Ended_At_The_End_Event()
	{
		var cluster = new ClusterFalso
		{
			AoCriarInstancia = (c, chave) =>
			{
				c.MaisUmElemento(chave, "fim-concluida", "END_EVENT", "COMPLETED");
				c.Estado(chave, "COMPLETED");
			}
		};

		await cluster.Cenario()
			.Deploy(Pedido, Envio)
			.StartInstance("xa-pedido")
			.ExecuteAsync()
			.AssertEndedAt("fim-concluida");
	}

	[Fact(DisplayName = "AssertEndedAt fails at once, naming the end the instance reached instead")]
	public async Task Ended_Elsewhere_Fails_Naming_The_End()
	{
		var cluster = new ClusterFalso
		{
			AoCriarInstancia = (c, chave) =>
			{
				c.MaisUmElemento(chave, "fim-retido", "END_EVENT", "COMPLETED");
				c.Estado(chave, "COMPLETED");
			}
		};

		var execucao = await cluster.Cenario(ClusterFalso.Rapido(prazoSegundos: 10))
			.Deploy(Pedido, Envio)
			.StartInstance("xa-pedido")
			.ExecuteAsync();

		var falha = Should.Throw<ShouldAssertException>(() => execucao.AssertEndedAt("fim-concluida"));
		falha.Message.ShouldContain("to end at 'fim-concluida'");
		falha.Message.ShouldContain("it ended at 'fim-retido'");
	}

	[Fact(DisplayName = "AssertEndedAt fails at once when the instance was terminated")]
	public async Task Terminated_Fails()
	{
		var cluster = new ClusterFalso { AoCriarInstancia = (c, chave) => c.Estado(chave, "TERMINATED") };

		var execucao = await cluster.Cenario(ClusterFalso.Rapido(prazoSegundos: 10))
			.Deploy(Pedido, Envio)
			.StartInstance("xa-pedido")
			.ExecuteAsync();

		Should.Throw<ShouldAssertException>(() => execucao.AssertEndedAt("fim-concluida"))
			.Message.ShouldContain("it was terminated");
	}

	[Fact(DisplayName = "The element and end ids are required, and the count cannot be negative")]
	public async Task Arguments_Are_Checked()
	{
		var cluster = new ClusterFalso { AoCriarInstancia = (c, chave) => c.Estado(chave, "COMPLETED") };
		var execucao = await cluster.Cenario()
			.Deploy(Pedido, Envio)
			.StartInstance("xa-pedido")
			.ExecuteAsync();

		Should.Throw<ArgumentException>(() => execucao.AssertElementCompleted(" ", 1));
		Should.Throw<ArgumentOutOfRangeException>(() => execucao.AssertElementCompleted("a", -1));
		Should.Throw<ArgumentException>(() => execucao.AssertEndedAt(""));
	}
}
