using System;
using System.IO;

using Confluent.Kafka;

using XUnitAssured.Core.Abstractions;
using XUnitAssured.Core.Configuration;
using XUnitAssured.Core.DSL;
using XUnitAssured.Kafka.Abstractions;
using XUnitAssured.Kafka.Configuration;
using XUnitAssured.Kafka.Steps;

namespace XUnitAssured.Tests.KafkaTests;

[Trait("Category", "Kafka")]
[Trait("Component", "Authentication")]
/// <summary>
/// Os verbos de autenticação do DSL (<c>WithSaslPlain</c>, <c>WithSsl</c>, ...) deixam no passo a
/// credencial que o usuário escreveu, e o resto do passo continua como estava.
///
/// <para>
/// <see cref="AutenticacaoNaConfiguracaoKafkaTests"/> cobre o que a credencial vira na
/// configuração do cliente; aqui é o degrau anterior: o verbo põe a credencial certa no passo?
/// </para>
/// </summary>
public class AutenticacaoNoDslKafkaTests
{
	public static TheoryData<string> Passos => new() { "consumo", "producao" };

	[Theory(DisplayName = "WithSaslPlain should set the SASL/PLAIN credential on the step")]
	[MemberData(nameof(Passos))]
	public void WithSaslPlain_Should_Set_Credential(string tipo)
	{
		var auth = Autenticacao(Montar(tipo).WithSaslPlain("ana", "segredo", useSsl: false));

		auth.Type.ShouldBe(KafkaAuthenticationType.SaslPlain);
		auth.SaslPlain!.Username.ShouldBe("ana");
		auth.SaslPlain.Password.ShouldBe("segredo");
		auth.SaslPlain.UseSsl.ShouldBeFalse();
	}

	[Theory(DisplayName = "WithSaslScram256 and WithSaslScram512 should pick the matching mechanism")]
	[MemberData(nameof(Passos))]
	public void WithSaslScram_Should_Pick_Mechanism(string tipo)
	{
		var sha256 = Autenticacao(Montar(tipo).WithSaslScram256("ana", "segredo"));
		sha256.Type.ShouldBe(KafkaAuthenticationType.SaslScram256);
		sha256.SaslScram!.Mechanism.ShouldBe(SaslMechanism.ScramSha256);
		sha256.SaslScram.Username.ShouldBe("ana");

		var sha512 = Autenticacao(Montar(tipo).WithSaslScram512("ana", "segredo", useSsl: false));
		sha512.Type.ShouldBe(KafkaAuthenticationType.SaslScram512);
		sha512.SaslScram!.Mechanism.ShouldBe(SaslMechanism.ScramSha512);
		sha512.SaslScram.UseSsl.ShouldBeFalse();
	}

	[Theory(DisplayName = "WithOAuthBearer should carry the token endpoint, client and scope")]
	[MemberData(nameof(Passos))]
	public void WithOAuthBearer_Should_Carry_Client(string tipo)
	{
		var auth = Autenticacao(Montar(tipo).WithOAuthBearer("https://idp/token", "cliente", "segredo", "kafka"));

		auth.Type.ShouldBe(KafkaAuthenticationType.OAuthBearer);
		auth.OAuthBearer!.TokenEndpointUrl.ShouldBe("https://idp/token");
		auth.OAuthBearer.ClientId.ShouldBe("cliente");
		auth.OAuthBearer.ClientSecret.ShouldBe("segredo");
		auth.OAuthBearer.Scope.ShouldBe("kafka");
	}

	[Theory(DisplayName = "WithSsl and WithMutualTls should carry the certificates")]
	[MemberData(nameof(Passos))]
	public void WithSsl_And_MutualTls_Should_Carry_Certificates(string tipo)
	{
		var ssl = Autenticacao(Montar(tipo).WithSsl("/certs/ca.pem", enableCertificateVerification: false));
		ssl.Type.ShouldBe(KafkaAuthenticationType.Ssl);
		ssl.Ssl!.SslCaLocation.ShouldBe("/certs/ca.pem");
		ssl.Ssl.EnableSslCertificateVerification.ShouldBeFalse();

		var mtls = Autenticacao(Montar(tipo).WithMutualTls("/certs/c.pem", "/certs/c.key", "/certs/ca.pem", "senha"));
		mtls.Type.ShouldBe(KafkaAuthenticationType.MutualTls);
		mtls.Ssl!.SslCertificateLocation.ShouldBe("/certs/c.pem");
		mtls.Ssl.SslKeyLocation.ShouldBe("/certs/c.key");
		mtls.Ssl.SslKeyPassword.ShouldBe("senha");
	}

	[Theory(DisplayName = "WithNoKafkaAuth and WithKafkaAuth should set what they say")]
	[MemberData(nameof(Passos))]
	public void WithNoKafkaAuth_And_WithKafkaAuth(string tipo)
	{
		Autenticacao(Montar(tipo).WithNoKafkaAuth()).Type.ShouldBe(KafkaAuthenticationType.None);

		var custom = Autenticacao(Montar(tipo).WithKafkaAuth(a => a.UseSaslPlain("bia", "x")));
		custom.SaslPlain!.Username.ShouldBe("bia");
	}

	[Fact(DisplayName = "An authentication verb should keep the rest of a consume step")]
	public void Auth_Verb_Should_Keep_Consume_Step()
	{
		var passo = ScenarioDsl.Given().Topic("pedidos").Consume()
			.WithSchema(typeof(int))
			.WithTimeout(TimeSpan.FromSeconds(8))
			.WithGroupId("grupo")
			.WithBootstrapServers("broker:19092")
			.WithSaslPlain("ana", "segredo")
			.CurrentStep.ShouldBeOfType<KafkaConsumeStep>();

		passo.Topic.ShouldBe("pedidos");
		passo.SchemaType.ShouldBe(typeof(int));
		passo.Timeout.ShouldBe(TimeSpan.FromSeconds(8));
		passo.GroupId.ShouldBe("grupo");
		passo.BootstrapServers.ShouldBe("broker:19092");
	}

	[Fact(DisplayName = "An authentication verb should keep the rest of a produce step")]
	public void Auth_Verb_Should_Keep_Produce_Step()
	{
		var instante = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

		var passo = ScenarioDsl.Given().Topic("pedidos").Produce("chave", "valor")
			.WithPartition(2)
			.WithTimestamp(instante)
			.WithTimeout(TimeSpan.FromSeconds(8))
			.WithSaslScram256("ana", "segredo")
			.CurrentStep.ShouldBeOfType<KafkaProduceStep>();

		passo.Key.ShouldBe("chave");
		passo.Value.ShouldBe("valor");
		passo.Partition.ShouldBe(2);
		passo.Timestamp.ShouldBe(instante);
		passo.Timeout.ShouldBe(TimeSpan.FromSeconds(8));
	}

	// Mesma armadilha de PrecedenciaDeConfiguracaoTests: reconstruir o passo copiando a
	// PROPRIEDADE faz o padrão parecer informado, e o endereço e o grupo da fixture deixam de
	// valer. Quem autentica pelo DSL dentro de uma fixture passa a falar com localhost:9092.
	[Theory(DisplayName = "An authentication verb should not turn the default broker into an explicit one")]
	[MemberData(nameof(Passos))]
	public void Auth_Verb_Should_Not_Invent_Explicit_Broker(string tipo)
	{
		var cenario = ScenarioDsl.Given();
		cenario.Context.SetProperty("_KafkaBootstrapServers", "broker-da-fixture:9092");

		var passo = (tipo == "consumo" ? cenario.Topic("t").Consume() : cenario.Topic("t").Produce("v"))
			.WithSaslPlain("ana", "segredo").CurrentStep!;

		var endereco = passo is KafkaConsumeStep consumo
			? consumo.MontarConfig(cenario.Context).BootstrapServers
			: ((KafkaProduceStep)passo).MontarConfig(cenario.Context).BootstrapServers;

		endereco.ShouldBe("broker-da-fixture:9092");
	}

	[Fact(DisplayName = "Authentication verbs should reject a null scenario")]
	public void Auth_Verbs_Should_Reject_Null_Scenario()
	{
		ITestScenario nulo = null!;
		var verbos = new (string Nome, Action Aplicar)[]
		{
			("WithSaslPlain()", () => nulo.WithSaslPlain()),
			("WithSaslPlain(u, p)", () => nulo.WithSaslPlain("u", "p")),
			("WithKafkaAuth", () => nulo.WithKafkaAuth(_ => { })),
			("WithSaslScram()", () => nulo.WithSaslScram()),
			("WithSaslScram256", () => nulo.WithSaslScram256("u", "p")),
			("WithSaslScram512", () => nulo.WithSaslScram512("u", "p")),
			("WithOAuthBearer", () => nulo.WithOAuthBearer("https://idp", "c", "s")),
			("WithSsl()", () => nulo.WithSsl()),
			("WithSsl(ca)", () => nulo.WithSsl("/ca.pem")),
			("WithMutualTls", () => nulo.WithMutualTls("c", "k")),
			("WithNoKafkaAuth", () => nulo.WithNoKafkaAuth()),
		};

		foreach (var (nome, aplicar) in verbos)
			Should.Throw<ArgumentNullException>(aplicar, nome);
	}

	[Fact(DisplayName = "WithKafkaAuth should reject a null configuration action")]
	public void WithKafkaAuth_Should_Reject_Null_Action()
	{
		Should.Throw<ArgumentNullException>(() => ScenarioDsl.Given().Topic("t").Consume().WithKafkaAuth(null!));
	}

	[Fact(DisplayName = "Authentication verbs should refuse a scenario with no single Kafka step")]
	public void Auth_Verbs_Should_Refuse_Without_Kafka_Step()
	{
		var verbos = new (string Nome, Action<ITestScenario> Aplicar)[]
		{
			("WithSaslPlain()", c => c.WithSaslPlain()),
			("WithSaslPlain(u, p)", c => c.WithSaslPlain("u", "p")),
			("WithSaslScram()", c => c.WithSaslScram()),
			("WithSsl()", c => c.WithSsl()),
			("WithNoKafkaAuth", c => c.WithNoKafkaAuth()),
		};

		foreach (var (nome, aplicar) in verbos)
		{
			var erro = Should.Throw<InvalidOperationException>(() => aplicar(ScenarioDsl.Given()), nome);
			erro.Message.ShouldContain("Current step is not a Kafka step", customMessage: nome);
		}
	}

	private static ITestScenario Montar(string tipo) =>
		tipo == "consumo"
			? ScenarioDsl.Given().Topic("pedidos").Consume()
			: ScenarioDsl.Given().Topic("pedidos").Produce("valor");

	private static KafkaAuthConfig Autenticacao(IKafkaScenario cenario)
	{
		cenario.ShouldBeAssignableTo<IKafkaScenario>();

		var auth = cenario.CurrentStep switch
		{
			KafkaConsumeStep c => c.AuthConfig,
			KafkaProduceStep p => p.AuthConfig,
			_ => null
		};

		auth.ShouldNotBeNull();
		return auth!;
	}
}

[Collection("TestSettings")]
[Trait("Category", "Kafka")]
[Trait("Component", "Authentication")]
/// <summary>
/// As variantes sem argumento (<c>WithSaslPlain()</c>, <c>WithSaslScram()</c>, <c>WithSsl()</c>)
/// leem a credencial da seção kafka do testsettings.json. Compartilham a variável de ambiente e o
/// cache de configuração, daí a coleção sequencial.
/// </summary>
public sealed class AutenticacaoDoArquivoNoDslKafkaTests : IDisposable
{
	private readonly string? _caminhoAnterior = Environment.GetEnvironmentVariable("TESTSETTINGS_PATH");
	private readonly string _pasta = Path.Combine(Path.GetTempPath(), $"xa-kafka-dsl-auth-{Guid.NewGuid():N}");

	public AutenticacaoDoArquivoNoDslKafkaTests() => Directory.CreateDirectory(_pasta);

	[Fact(DisplayName = "WithSaslPlain() should read the credential from testsettings.json")]
	public void WithSaslPlain_Should_Read_Settings()
	{
		UsarArquivo("""
			{ "kafka": { "authentication": { "type": "SaslPlain",
			  "saslPlain": { "username": "do-arquivo", "password": "s", "useSsl": false } } } }
			""");

		var passo = ScenarioDsl.Given().Topic("t").Consume().WithSaslPlain().CurrentStep
			.ShouldBeOfType<KafkaConsumeStep>();

		passo.AuthConfig!.Type.ShouldBe(KafkaAuthenticationType.SaslPlain);
		passo.AuthConfig.SaslPlain!.Username.ShouldBe("do-arquivo");
	}

	[Fact(DisplayName = "WithSaslScram() should read the credential from testsettings.json")]
	public void WithSaslScram_Should_Read_Settings()
	{
		UsarArquivo("""
			{ "kafka": { "authentication": { "type": "SaslScram512",
			  "saslScram": { "username": "do-arquivo", "password": "s", "mechanism": "ScramSha512" } } } }
			""");

		var passo = ScenarioDsl.Given().Topic("t").Produce("v").WithSaslScram().CurrentStep
			.ShouldBeOfType<KafkaProduceStep>();

		passo.AuthConfig!.Type.ShouldBe(KafkaAuthenticationType.SaslScram512);
		passo.AuthConfig.SaslScram!.Username.ShouldBe("do-arquivo");
		passo.AuthConfig.SaslScram.Mechanism.ShouldBe(SaslMechanism.ScramSha512);
	}

	[Fact(DisplayName = "WithSsl() should read the certificates from testsettings.json")]
	public void WithSsl_Should_Read_Settings()
	{
		UsarArquivo("""
			{ "kafka": { "authentication": { "type": "Ssl",
			  "ssl": { "sslCaLocation": "/do-arquivo/ca.pem" } } } }
			""");

		var passo = ScenarioDsl.Given().Topic("t").Consume().WithSsl().CurrentStep
			.ShouldBeOfType<KafkaConsumeStep>();

		passo.AuthConfig!.Type.ShouldBe(KafkaAuthenticationType.Ssl);
		passo.AuthConfig.Ssl!.SslCaLocation.ShouldBe("/do-arquivo/ca.pem");
	}

	[Fact(DisplayName = "Settings-based authentication verbs should explain what is missing from the file")]
	public void Settings_Verbs_Should_Explain_Missing_Section()
	{
		UsarArquivo("""{ "kafka": { "bootstrapServers": "b:9092" } }""");

		Should.Throw<InvalidOperationException>(() => ScenarioDsl.Given().Topic("t").Consume().WithSaslPlain())
			.Message.ShouldContain("authentication.saslPlain");
		Should.Throw<InvalidOperationException>(() => ScenarioDsl.Given().Topic("t").Consume().WithSaslScram())
			.Message.ShouldContain("authentication.saslScram");
		Should.Throw<InvalidOperationException>(() => ScenarioDsl.Given().Topic("t").Consume().WithSsl())
			.Message.ShouldContain("authentication.ssl");
	}

	private void UsarArquivo(string json)
	{
		var caminho = Path.Combine(_pasta, "testsettings.json");
		File.WriteAllText(caminho, json);
		Environment.SetEnvironmentVariable("TESTSETTINGS_PATH", caminho);
		LimparCaches();
	}

	private static void LimparCaches()
	{
		TestSettingsLoader.ClearCache();
		TestSettingsKafkaExtensions.ClearKafkaSettingsCache();
	}

	public void Dispose()
	{
		Environment.SetEnvironmentVariable("TESTSETTINGS_PATH", _caminhoAnterior);
		LimparCaches();
		try { Directory.Delete(_pasta, recursive: true); } catch (IOException) { }
	}
}
