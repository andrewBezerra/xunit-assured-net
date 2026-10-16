using System;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

using Flurl.Http;
using Flurl.Http.Testing;

using Shouldly;
using Xunit;

using XUnitAssured.Core.DSL;
using XUnitAssured.Core.Storage;
using XUnitAssured.Http.Configuration;
using XUnitAssured.Http.Handlers;
using XUnitAssured.Http.Results;
using XUnitAssured.Http.Steps;

namespace XUnitAssured.Tests.HttpTests;

[Trait("Category", "Http")]
[Trait("Component", "Authentication")]
/// <summary>
/// Autenticação por certificado (mTLS) com um certificado gerado no próprio teste.
///
/// <para>
/// Nenhum arquivo de certificado vive no repositório: o certificado é autoassinado, criado em
/// memória com <see cref="CertificateRequest"/>, e some com o processo. O pedido é falso (HttpTest
/// do Flurl, ou o servidor de mentira), então o que se verifica é de onde o handler tira o
/// certificado, o que ele diz quando não acha, e que o passo leva o pedido pelo cliente certo.
/// </para>
/// </summary>
public class CertificadoGeradoNoTesteTests
{
	private static X509Certificate2 CertificadoAutoassinado()
	{
		using var chave = RSA.Create(2048);
		var pedido = new CertificateRequest(
			$"CN=xunitassured-teste-{Guid.NewGuid():N}", chave, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

		return pedido.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddHours(1));
	}

	// Um caminho que não existe: o teste não cria arquivo nenhum.
	private static string CaminhoInexistente() =>
		Path.Combine(Path.GetTempPath(), $"nao-existe-{Guid.NewGuid():N}.pfx");

	[Fact(DisplayName = "The handler should refuse a missing configuration")]
	public void Handler_Should_Refuse_Missing_Configuration()
	{
		Should.Throw<ArgumentNullException>(() => new CertificateAuthHandler(null!));
	}

	[Fact(DisplayName = "The handler should say it is for certificates, and handle only a certificate config that has one")]
	public void Handler_Should_Handle_Only_Certificate_Configs()
	{
		var handler = new CertificateAuthHandler(new CertificateAuthConfig());

		handler.Type.ShouldBe(AuthenticationType.Certificate);
		handler.CanHandle(new HttpAuthConfig { Type = AuthenticationType.Certificate, Certificate = new CertificateAuthConfig() })
			.ShouldBeTrue();
		handler.CanHandle(new HttpAuthConfig { Type = AuthenticationType.Certificate }).ShouldBeFalse(
			"a certificate type with no certificate section has nothing to load");
		handler.CanHandle(new HttpAuthConfig { Type = AuthenticationType.Basic, Certificate = new CertificateAuthConfig() })
			.ShouldBeFalse();
		handler.CanHandle(null!).ShouldBeFalse();
	}

	// O certificado vai no handler do HttpClient, não no pedido: não há cabeçalho a pôr.
	[Fact(DisplayName = "Applying certificate auth to a request should add nothing to it")]
	public void Applying_Should_Add_Nothing_To_The_Request()
	{
		using var certificado = CertificadoAutoassinado();
		var handler = new CertificateAuthHandler(new CertificateAuthConfig { Certificate = certificado });
		var pedido = new FlurlRequest("http://api.teste/x");

		handler.ApplyAuthentication(pedido);

		pedido.Headers.Count.ShouldBe(0);
		pedido.Url.ToString().ShouldBe("http://api.teste/x");
	}

	[Fact(DisplayName = "A certificate instance should win over a path and a thumbprint")]
	public void Instance_Should_Win_Over_Path_And_Thumbprint()
	{
		using var certificado = CertificadoAutoassinado();
		var handler = new CertificateAuthHandler(new CertificateAuthConfig
		{
			Certificate = certificado,
			CertificatePath = CaminhoInexistente(),
			Thumbprint = "00"
		});

		handler.GetCertificate().ShouldBeSameAs(certificado);
	}

	[Fact(DisplayName = "A certificate path that does not exist should say which file was not found")]
	public void Missing_File_Should_Say_Which()
	{
		var caminho = CaminhoInexistente();
		var handler = new CertificateAuthHandler(new CertificateAuthConfig { CertificatePath = caminho, CertificatePassword = "x" });

		var erro = Should.Throw<InvalidOperationException>(() => handler.GetCertificate());

		erro.Message.ShouldStartWith("Failed to load certificate from file");
		erro.Message.ShouldContain(caminho);
	}

	[Fact(DisplayName = "A thumbprint that is not in the store should say which store was searched")]
	public void Thumbprint_Not_In_Store_Should_Say_Which_Store()
	{
		// Um certificado acabado de gerar: o seu thumbprint não está em loja nenhuma.
		using var certificado = CertificadoAutoassinado();
		var handler = new CertificateAuthHandler(new CertificateAuthConfig { Thumbprint = certificado.Thumbprint });

		var erro = Should.Throw<InvalidOperationException>(() => handler.GetCertificate());

		erro.Message.ShouldStartWith("Failed to load certificate from store");
	}

	[Fact(DisplayName = "A configuration with no certificate source should say the three options")]
	public void No_Source_Should_Say_The_Options()
	{
		var handler = new CertificateAuthHandler(new CertificateAuthConfig());

		Should.Throw<InvalidOperationException>(() => handler.GetCertificate())
			.Message.ShouldContain("Certificate instance, CertificatePath, or Thumbprint");
	}

	[Fact(DisplayName = "WithCertificateInstance should record the certificate on the step")]
	public void WithCertificateInstance_Should_Record_The_Certificate()
	{
		using var certificado = CertificadoAutoassinado();

		var passo = Given()
			.ApiResource("http://api.teste/seguro")
			.Get()
			.WithCertificateInstance(certificado)
			.CurrentStep.ShouldBeOfType<HttpRequestStep>();

		passo.AuthConfig.ShouldNotBeNull();
		passo.AuthConfig!.Type.ShouldBe(AuthenticationType.Certificate);
		passo.AuthConfig.Certificate!.Certificate.ShouldBeSameAs(certificado);
		passo.Url.ShouldBe("http://api.teste/seguro", "the auth verb must keep what earlier verbs configured");
	}

	[Fact(DisplayName = "WithCertificateInstance should refuse a missing certificate or a chain with no HTTP step")]
	public void WithCertificateInstance_Should_Refuse_Bad_Input()
	{
		using var certificado = CertificadoAutoassinado();

		Should.Throw<ArgumentNullException>(() => Given().ApiResource("http://api.teste").WithCertificateInstance(null!));
		Should.Throw<ArgumentNullException>(() => ((XUnitAssured.Core.Abstractions.ITestScenario)null!).WithCertificateInstance(certificado));
		Should.Throw<InvalidOperationException>(() => Given().WithCertificateInstance(certificado))
			.Message.ShouldContain("Call ApiResource() first");
	}

	[Fact(DisplayName = "A step with a certificate should send the request through the certificate's client")]
	public async Task Step_With_Certificate_Should_Send_The_Request()
	{
		using var certificado = CertificadoAutoassinado();
		using var http = new HttpTest();
		http.RespondWith("{\"ok\":true}", 200);

		await Given()
			.ApiResource("http://api.teste/seguro")
			.WithCertificateInstance(certificado)
			.WithHeader("X-Correlacao", "abc")
			.WithQueryParam("pagina", 2)
			.Get()
			.ExecuteAsync()
			.AssertStatusCode(200)
			.AssertJsonPath<bool>("$.ok", v => v.ShouldBeTrue());

		http.ShouldHaveCalled("http://api.teste/seguro?pagina=2")
			.WithHeader("X-Correlacao", "abc")
			.WithoutHeader("Authorization")
			.Times(1);
	}

	[Fact(DisplayName = "A step whose certificate is not in the store should fail and say so, not send anything")]
	public async Task Step_With_Missing_Store_Certificate_Should_Fail()
	{
		using var http = new HttpTest();
		http.RespondWith("{}");

		var resultado = await Given()
			.ApiResource("http://api.teste/seguro")
			.WithCertificateFromStore("0000000000000000000000000000000000000000")
			.Get()
			.ExecuteAsync()
			.GetResult();

		resultado.Success.ShouldBeFalse();
		resultado.Errors.ShouldContain(e => e.Contains("Failed to load certificate from store"));
		http.ShouldNotHaveMadeACall();
	}

	// Com HttpClient próprio, o certificado é do handler desse cliente, e é de quem o passou.
	[Fact(DisplayName = "With a custom client, certificate auth should leave the request as it was")]
	public async Task Custom_Client_With_Certificate_Should_Add_Nothing()
	{
		using var certificado = CertificadoAutoassinado();
		var servidor = ServidorDeMentira.Sempre(ServidorDeMentira.Responder(HttpStatusCode.OK, "{}"));
		using var cliente = new System.Net.Http.HttpClient(servidor) { BaseAddress = new Uri("http://servidor.teste") };

		var passo = new HttpRequestStep
		{
			Url = "/seguro",
			CustomHttpClient = cliente,
			AuthConfig = new HttpAuthConfig
			{
				Type = AuthenticationType.Certificate,
				Certificate = new CertificateAuthConfig { Certificate = certificado }
			}
		};

		var resultado = (HttpStepResult)await passo.ExecuteAsync(new TestContext(new StepStorage()));

		resultado.Success.ShouldBeTrue(string.Join("; ", resultado.Errors));
		var recebido = servidor.Recebidos.ShouldHaveSingleItem();
		recebido.Caminho.ShouldBe("/seguro");
		recebido.Cabecalhos.ContainsKey("Authorization").ShouldBeFalse("the certificate is the client's, not a header");
	}
}
