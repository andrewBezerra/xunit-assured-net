using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

using Flurl.Http.Testing;

using Shouldly;
using Xunit;

using XUnitAssured.Core.Abstractions;
using XUnitAssured.Http.Configuration;
using XUnitAssured.Http.Steps;

namespace XUnitAssured.Tests.HttpTests;

[Trait("Category", "Http")]
[Trait("Component", "Authentication")]
/// <summary>
/// O que os verbos de autenticação dizem quando chamados sem cenário ou antes do
/// <c>ApiResource</c>, e o que alguns deles gravam no passo.
///
/// <para>
/// Todo verbo de autenticação reconstrói o passo HTTP corrente, então chamá-lo antes de haver um
/// é erro de quem escreve o teste. A mensagem precisa dizer o que fazer, e é ela que se verifica,
/// verbo por verbo: uma guarda que some num verbo só passaria despercebida num teste genérico.
/// </para>
/// </summary>
public class GuardasDosVerbosDeAutenticacaoTests
{
	public static IEnumerable<object[]> Verbos() => new[]
	{
		Verbo("WithBasicAuth", s => s.WithBasicAuth("u", "p")),
		Verbo("WithBearerToken", s => s.WithBearerToken("tk")),
		Verbo("WithAuthConfig", s => s.WithAuthConfig(c => c.UseBasicAuth("u", "p"))),
		Verbo("WithApiKey", s => s.WithApiKey("X-API-Key", "k")),
		Verbo("WithApiKey(location)", s => s.WithApiKey("k", "v", ApiKeyLocation.Query)),
		Verbo("WithOAuth2", s => s.WithOAuth2("http://auth.teste/token", "c", "s")),
		Verbo("WithOAuth2Config", s => s.WithOAuth2Config(_ => { })),
		Verbo("WithCustomHeader", s => s.WithCustomHeader("X-H", "v")),
		Verbo("WithCustomHeaders", s => s.WithCustomHeaders(new Dictionary<string, string> { ["X-H"] = "v" })),
		Verbo("WithCertificate()", s => s.WithCertificate()),
		Verbo("WithCertificate(path)", s => s.WithCertificate("c.pfx")),
		Verbo("WithCertificateFromStore", s => s.WithCertificateFromStore("00")),
		Verbo("WithNoAuth", s => s.WithNoAuth())
	};

	private static object[] Verbo(string nome, Action<ITestScenario> chamar) => new object[] { nome, chamar };

	[Theory(DisplayName = "Every auth verb should refuse a null scenario")]
	[MemberData(nameof(Verbos))]
	public void Auth_Verb_Should_Refuse_A_Null_Scenario(string verbo, Action<ITestScenario> chamar)
	{
		Should.Throw<ArgumentNullException>(() => chamar(null!), verbo)
			.ParamName.ShouldBe("scenario");
	}

	[Theory(DisplayName = "Every auth verb before ApiResource should say to call ApiResource first")]
	[MemberData(nameof(Verbos))]
	public void Auth_Verb_Before_ApiResource_Should_Say_So(string verbo, Action<ITestScenario> chamar)
	{
		Should.Throw<InvalidOperationException>(() => chamar(Given()), verbo)
			.Message.ShouldContain("Call ApiResource() first");
	}

	[Fact(DisplayName = "Configuration-action verbs should refuse a missing action")]
	public void Configuration_Verbs_Should_Refuse_A_Missing_Action()
	{
		Should.Throw<ArgumentNullException>(() => Given().ApiResource("http://a").WithAuthConfig(null!))
			.ParamName.ShouldBe("configure");
		Should.Throw<ArgumentNullException>(() => Given().ApiResource("http://a").WithOAuth2Config(null!))
			.ParamName.ShouldBe("configure");
	}

	[Fact(DisplayName = "WithCustomHeaders should refuse no headers at all")]
	public void WithCustomHeaders_Should_Refuse_No_Headers()
	{
		Should.Throw<ArgumentException>(() => Given().ApiResource("http://a").WithCustomHeaders(null!))
			.Message.ShouldContain("At least one header");
		Should.Throw<ArgumentException>(() => Given().ApiResource("http://a").WithCustomHeaders(new Dictionary<string, string>()))
			.Message.ShouldContain("At least one header");
	}

	[Fact(DisplayName = "WithCertificate(path) and WithCertificateFromStore should record where the certificate comes from")]
	public void Certificate_Verbs_Should_Record_The_Source()
	{
		var doArquivo = Given().ApiResource("http://a").WithCertificate("cliente.pfx", "senha")
			.CurrentStep.ShouldBeOfType<HttpRequestStep>().AuthConfig!;
		var daLoja = Given().ApiResource("http://a")
			.WithCertificateFromStore("ABCDEF", StoreLocation.LocalMachine, StoreName.Root)
			.CurrentStep.ShouldBeOfType<HttpRequestStep>().AuthConfig!;

		doArquivo.Type.ShouldBe(AuthenticationType.Certificate);
		doArquivo.Certificate!.CertificatePath.ShouldBe("cliente.pfx");
		doArquivo.Certificate.CertificatePassword.ShouldBe("senha");
		daLoja.Type.ShouldBe(AuthenticationType.Certificate);
		daLoja.Certificate!.Thumbprint.ShouldBe("ABCDEF");
		daLoja.Certificate.StoreLocation.ShouldBe(StoreLocation.LocalMachine);
		daLoja.Certificate.StoreName.ShouldBe(StoreName.Root);
	}

	[Fact(DisplayName = "WithBearerToken with a custom prefix should send that prefix")]
	public async Task Bearer_With_Prefix_Should_Send_The_Prefix()
	{
		using var http = new HttpTest();
		http.RespondWith("{}");

		await Given().ApiResource("http://api.teste/r").WithBearerToken("tk-9", prefix: "JWT").Get().ExecuteAsync();

		http.ShouldHaveCalled("http://api.teste/r").WithHeader("Authorization", "JWT tk-9").Times(1);
	}

	[Fact(DisplayName = "WithCustomHeaders should send every header given")]
	public async Task Custom_Headers_Should_All_Be_Sent()
	{
		using var http = new HttpTest();
		http.RespondWith("{}");

		await Given()
			.ApiResource("http://api.teste/r")
			.WithCustomHeaders(new Dictionary<string, string> { ["X-Token"] = "abc", ["X-Sessao"] = "xyz" })
			.Get()
			.ExecuteAsync();

		http.ShouldHaveCalled("http://api.teste/r").WithHeader("X-Token", "abc").WithHeader("X-Sessao", "xyz").Times(1);
	}
}
