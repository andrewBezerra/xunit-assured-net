using System;
using System.Linq;

using Shouldly;
using Xunit;

using XUnitAssured.Camunda;
using XUnitAssured.Camunda.Abstractions;
using XUnitAssured.Camunda.Extensions;
using XUnitAssured.Camunda.Gateway;
using XUnitAssured.Camunda.Mocks;
using XUnitAssured.Camunda.Steps;
using XUnitAssured.Core.DSL;

namespace XUnitAssured.Tests.CamundaTests;

/// <summary>
/// O que os verbos descrevem, sem cluster: cada um monta o passo ou o mock certo, e recusa cedo o
/// que não faz sentido.
/// </summary>
[Trait("Category", "Camunda")]
[Trait("Component", "DSL")]
public class VerbosDoCamundaTests
{
	[Fact(DisplayName = "Deploy describes one deploy step with every file, so parent and child go together")]
	public void Deploy_Describes_One_Step_With_Every_File()
	{
		var cenario = ScenarioDsl.Given().Deploy("a.bpmn", "b.bpmn");

		var passo = cenario.CurrentStep.ShouldBeOfType<CamundaDeployStep>();
		passo.Resources.ShouldBe(new[] { "a.bpmn", "b.bpmn" });
		cenario.ShouldBeAssignableTo<ICamundaScenario>();
	}

	[Fact(DisplayName = "Deploy with no file, or an empty one, is refused when the chain is written")]
	public void Deploy_Without_Files_Is_Refused()
	{
		Should.Throw<ArgumentException>(() => ScenarioDsl.Given().Deploy());
		Should.Throw<ArgumentException>(() => ScenarioDsl.Given().Deploy("a.bpmn", " "));
	}

	[Fact(DisplayName = "StartInstance after Deploy is a second step, not a replacement")]
	public void StartInstance_After_Deploy_Is_A_New_Step()
	{
		var cenario = ScenarioDsl.Given()
			.Deploy("a.bpmn")
			.StartInstance("pedido", new { id = 1 });

		var passo = cenario.CurrentStep.ShouldBeOfType<CamundaStartInstanceStep>();
		passo.ProcessId.ShouldBe("pedido");
		passo.Variables.ShouldNotBeNull();
	}

	[Fact(DisplayName = "StartInstance needs a process id")]
	public void StartInstance_Needs_A_Process_Id() =>
		Should.Throw<ArgumentException>(() => ScenarioDsl.Given().StartInstance(""));

	[Fact(DisplayName = "A mock is registered only when it says what it answers")]
	public void Mock_Registers_On_Its_Answer()
	{
		var inicio = ScenarioDsl.Given();
		var declarado = inicio.MockJobWorker("cobrar");

		MocksDaCadeia.Todos(inicio.Context).ShouldBeEmpty();

		declarado.ThenComplete(new { aprovado = true });

		var mock = MocksDaCadeia.Todos(inicio.Context)["cobrar"];
		mock.Acao.ShouldBe(AcaoDoMock.Completar);
		mock.Variables.ShouldNotBeNull();
	}

	[Fact(DisplayName = "A BPMN error mock keeps its code and message")]
	public void Bpmn_Error_Mock_Keeps_Code_And_Message()
	{
		var cenario = ScenarioDsl.Given().MockJobWorker("cobrar").ThenThrowBpmnError("RECUSADO", "sem saldo");

		var mock = MocksDaCadeia.Todos(cenario.Context)["cobrar"];
		mock.Acao.ShouldBe(AcaoDoMock.LancarErroBpmn);
		mock.ErrorCode.ShouldBe("RECUSADO");
		mock.ErrorMessage.ShouldBe("sem saldo");
	}

	[Fact(DisplayName = "A BPMN error mock needs an error code")]
	public void Bpmn_Error_Mock_Needs_A_Code() =>
		Should.Throw<ArgumentException>(() => ScenarioDsl.Given().MockJobWorker("cobrar").ThenThrowBpmnError(""));

	[Fact(DisplayName = "Mocking the same job type twice in one chain is refused, instead of one answer winning in silence")]
	public void Same_Type_Twice_Is_Refused()
	{
		var cenario = ScenarioDsl.Given().MockJobWorker("cobrar").ThenComplete();

		Should.Throw<InvalidOperationException>(() => cenario.MockJobWorker("cobrar").ThenThrowBpmnError("X"))
			.Message.ShouldContain("already mocked");
	}

	[Fact(DisplayName = "A mock needs a job type")]
	public void Mock_Needs_A_Type() =>
		Should.Throw<ArgumentException>(() => ScenarioDsl.Given().MockJobWorker(" "));

	[Fact(DisplayName = "Mocks declared after StartInstance still count: the whole chain is described before anything runs")]
	public void Mocks_After_Start_Count()
	{
		var cenario = ScenarioDsl.Given()
			.Deploy("a.bpmn")
			.StartInstance("pedido")
			.MockJobWorker("cobrar").ThenComplete();

		MocksDaCadeia.Todos(cenario.Context).Keys.ShouldBe(new[] { "cobrar" });
		cenario.CurrentStep.ShouldBeOfType<CamundaStartInstanceStep>();
	}
}

/// <summary>
/// A seção <c>camunda</c> traduzida para o SDK: só entra o que foi preenchido, para o resto vir
/// das variáveis <c>CAMUNDA_*</c>.
/// </summary>
[Trait("Category", "Camunda")]
[Trait("Component", "Configuration")]
public class ConfiguracaoDoCamundaTests
{
	[Fact(DisplayName = "Basic authentication becomes the SDK's basic keys")]
	public void Basic_Becomes_Sdk_Keys()
	{
		var config = SdkCamundaGateway.Configuracao(new CamundaSettings
		{
			RestAddress = "http://camunda:8080",
			Authentication = new CamundaAuthenticationSettings { Type = "basic", Username = "xunit", Password = "segredo" }
		});

		config["CAMUNDA_REST_ADDRESS"].ShouldBe("http://camunda:8080");
		config["CAMUNDA_AUTH_STRATEGY"].ShouldBe("BASIC");
		config["CAMUNDA_BASIC_AUTH_USERNAME"].ShouldBe("xunit");
		config["CAMUNDA_BASIC_AUTH_PASSWORD"].ShouldBe("segredo");
	}

	[Fact(DisplayName = "With no type, a username means Basic: without the strategy the SDK sends no credentials at all")]
	public void Username_Means_Basic()
	{
		if (Environment.GetEnvironmentVariable("CAMUNDA_AUTH_STRATEGY") is not null)
			return; // a variável decide, e é o SDK que a lê

		SdkCamundaGateway.Configuracao(new CamundaSettings
		{
			Authentication = new CamundaAuthenticationSettings { Username = "xunit", Password = "segredo" }
		})["CAMUNDA_AUTH_STRATEGY"].ShouldBe("BASIC");
	}

	[Fact(DisplayName = "What is not filled is left out, so the CAMUNDA_* environment variables still apply")]
	public void Empty_Is_Left_Out()
	{
		var config = SdkCamundaGateway.Configuracao(new CamundaSettings());

		// Só a estratégia pode vir, deduzida das variáveis CAMUNDA_* do próprio ambiente.
		config.Keys.Where(k => k != "CAMUNDA_AUTH_STRATEGY").ShouldBeEmpty();
	}

	[Fact(DisplayName = "OAuth becomes the SDK's client credentials keys")]
	public void OAuth_Becomes_Sdk_Keys()
	{
		var config = SdkCamundaGateway.Configuracao(new CamundaSettings
		{
			Authentication = new CamundaAuthenticationSettings
			{
				Type = "OAuth", ClientId = "testes", ClientSecret = "s", OAuthUrl = "https://idp/token", Audience = "orchestration"
			}
		});

		config["CAMUNDA_AUTH_STRATEGY"].ShouldBe("OAUTH");
		config["CAMUNDA_CLIENT_ID"].ShouldBe("testes");
		config["CAMUNDA_OAUTH_URL"].ShouldBe("https://idp/token");
		config["CAMUNDA_TOKEN_AUDIENCE"].ShouldBe("orchestration");
	}

	[Fact(DisplayName = "An unknown authentication type is refused with the valid ones")]
	public void Unknown_Type_Is_Refused() =>
		Should.Throw<InvalidOperationException>(() => SdkCamundaGateway.Configuracao(new CamundaSettings
		{
			Authentication = new CamundaAuthenticationSettings { Type = "kerberos" }
		})).Message.ShouldContain("None, Basic or OAuth");

	[Fact(DisplayName = "Defaults: 15 s per assertion, 100 ms between checks, mocks alive for 120 s")]
	public void Defaults()
	{
		var padrao = new CamundaSettings();

		padrao.AssertionTimeoutSeconds.ShouldBe(15);
		padrao.PollIntervalMilliseconds.ShouldBe(100);
		padrao.MockLifetimeSeconds.ShouldBe(120);
		padrao.Authentication.ShouldNotBeNull();
	}

	[Fact(DisplayName = "Settings in the context win over the file")]
	public void Context_Settings_Win()
	{
		var cenario = ScenarioDsl.Given();
		var proprias = new CamundaSettings { AssertionTimeoutSeconds = 3 };
		cenario.Context.SetProperty(CamundaSettings.ChaveNoContexto, proprias);

		CamundaSettings.De(cenario.Context).ShouldBeSameAs(proprias);
	}

	[Fact(DisplayName = "The context's cluster wins over the shared one")]
	public void Context_Gateway_Wins()
	{
		var falso = new ClusterFalso();
		var cenario = falso.Cenario();

		Gateways.De(cenario.Context, new CamundaSettings()).ShouldBeSameAs(falso);
	}

	[Fact(DisplayName = "Steps of the package are Camunda steps")]
	public void Step_Types() =>
		new[] { new CamundaDeployStep().StepType, new CamundaStartInstanceStep().StepType }
			.Distinct().ShouldBe(new[] { "Camunda" });
}
