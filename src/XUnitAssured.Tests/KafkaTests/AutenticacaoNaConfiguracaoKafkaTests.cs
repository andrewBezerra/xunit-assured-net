using System;
using System.IO;

using Confluent.Kafka;

using XUnitAssured.Core.Abstractions;
using XUnitAssured.Core.Configuration;
using XUnitAssured.Core.DSL;
using XUnitAssured.Core.Storage;
using XUnitAssured.Kafka;
using XUnitAssured.Kafka.Configuration;
using XUnitAssured.Kafka.Handlers;
using XUnitAssured.Kafka.Steps;
using XUnitAssured.Kafka.Testing;

namespace XUnitAssured.Tests.KafkaTests;

/// <summary>
/// A autenticação chega à configuração do produtor e do consumidor (#164), sem broker.
///
/// <para>
/// A primeira rodada de mutação do Kafka (#159) mostrou que apagar a aplicação da autenticação
/// num passo não derrubava teste nenhum, e que os handlers não tinham teste: os de autenticação
/// conferiam a <see cref="KafkaAuthConfig"/> que a DSL monta, não o que ela vira. E as cinco
/// cópias da escolha do handler tinham divergido; agora há uma, <c>AutenticacaoKafka</c>.
/// </para>
/// </summary>
[Trait("Category", "Kafka")]
[Trait("Component", "Authentication")]
public class AutenticacaoNaConfiguracaoKafkaTests
{
	public static TheoryData<string> Clientes => new() { "produtor", "consumidor" };

	[Theory(DisplayName = "SASL/PLAIN sets the protocol, the mechanism and the credentials, with or without SSL")]
	[MemberData(nameof(Clientes))]
	public void Sasl_Plain(string cliente)
	{
		var semSsl = Aplicado(cliente, a => a.UseSaslPlain("ana", "segredo", useSsl: false));
		semSsl.SecurityProtocol.ShouldBe(SecurityProtocol.SaslPlaintext);
		semSsl.SaslMechanism.ShouldBe(SaslMechanism.Plain);
		semSsl.SaslUsername.ShouldBe("ana");
		semSsl.SaslPassword.ShouldBe("segredo");

		Aplicado(cliente, a => a.UseSaslPlain("ana", "segredo")).SecurityProtocol.ShouldBe(SecurityProtocol.SaslSsl);
	}

	[Theory(DisplayName = "SASL/SCRAM sets SHA-256 or SHA-512 and the credentials")]
	[MemberData(nameof(Clientes))]
	public void Sasl_Scram(string cliente)
	{
		var sha256 = Aplicado(cliente, a => a.UseSaslScram256("ana", "segredo"));
		sha256.SecurityProtocol.ShouldBe(SecurityProtocol.SaslSsl);
		sha256.SaslMechanism.ShouldBe(SaslMechanism.ScramSha256);
		sha256.SaslUsername.ShouldBe("ana");
		sha256.SaslPassword.ShouldBe("segredo");

		var sha512 = Aplicado(cliente, a => a.UseSaslScram512("ana", "segredo", useSsl: false));
		sha512.SecurityProtocol.ShouldBe(SecurityProtocol.SaslPlaintext);
		sha512.SaslMechanism.ShouldBe(SaslMechanism.ScramSha512);
	}

	[Theory(DisplayName = "SSL sets the protocol, the CA and the verification; mutual TLS adds the client certificate")]
	[MemberData(nameof(Clientes))]
	public void Ssl_And_Mutual_Tls(string cliente)
	{
		var ssl = Aplicado(cliente, a => a.UseSsl("/certs/ca.pem", enableCertificateVerification: false));
		ssl.SecurityProtocol.ShouldBe(SecurityProtocol.Ssl);
		ssl.SslCaLocation.ShouldBe("/certs/ca.pem");
		ssl.EnableSslCertificateVerification.ShouldBe(false);
		ssl.SslCertificateLocation.ShouldBeNull();

		var mtls = Aplicado(cliente, a => a.UseMutualTls("/certs/cliente.pem", "/certs/cliente.key", "/certs/ca.pem", "senha-da-chave"));
		mtls.SecurityProtocol.ShouldBe(SecurityProtocol.Ssl);
		mtls.SslCertificateLocation.ShouldBe("/certs/cliente.pem");
		mtls.SslKeyLocation.ShouldBe("/certs/cliente.key");
		mtls.SslKeyPassword.ShouldBe("senha-da-chave");
		mtls.SslCaLocation.ShouldBe("/certs/ca.pem");
	}

	// Até a #164, só a fixture fazia isto; os passos ignoravam a seção ssl de uma autenticação SASL.
	[Theory(DisplayName = "With SASL over SSL, the ssl section is applied too: the broker's CA and its verification")]
	[MemberData(nameof(Clientes))]
	public void Sasl_Over_Ssl_Applies_The_Ssl_Section(string cliente)
	{
		var config = Aplicado(cliente, a =>
		{
			a.UseSaslPlain("ana", "segredo");
			a.Ssl = new SslConfig { SslCaLocation = "/certs/ca.pem", EnableSslCertificateVerification = false };
		});

		config.SecurityProtocol.ShouldBe(SecurityProtocol.SaslSsl);
		config.SaslUsername.ShouldBe("ana");
		config.SslCaLocation.ShouldBe("/certs/ca.pem");
		config.EnableSslCertificateVerification.ShouldBe(false);
	}

	[Theory(DisplayName = "No authentication, or None, leaves the configuration as it was")]
	[MemberData(nameof(Clientes))]
	public void None_Changes_Nothing(string cliente)
	{
		Aplicado(cliente, a => a.UseNoAuth()).SecurityProtocol.ShouldBeNull();

		var config = Novo(cliente);
		AutenticacaoKafka.Aplicar(config, null);
		config.SecurityProtocol.ShouldBeNull();
		config.SaslUsername.ShouldBeNull();
	}

	// ---------- Os passos ----------

	public static TheoryData<string> Passos => new() { "produce", "batch-produce", "consume", "batch-consume" };

	[Theory(DisplayName = "Every step puts its own authentication in the client configuration")]
	[MemberData(nameof(Passos))]
	public void Step_Applies_Its_Own_Authentication(string passo)
	{
		var config = ConfigDoPasso(passo, Sasl("do-passo"), Contexto());

		config.SecurityProtocol.ShouldBe(SecurityProtocol.SaslPlaintext);
		config.SaslUsername.ShouldBe("do-passo");
	}

	// Os passos em lote iam direto ao arquivo de configuração e ignoravam a fixture.
	[Theory(DisplayName = "Every step uses the authentication the fixture put in the context, when it has none of its own")]
	[MemberData(nameof(Passos))]
	public void Step_Uses_The_Fixture_Authentication(string passo)
	{
		var contexto = Contexto();
		contexto.SetProperty("_KafkaAuthConfig", Sasl("da-fixture"));

		ConfigDoPasso(passo, null, contexto).SaslUsername.ShouldBe("da-fixture");
	}

	[Theory(DisplayName = "A step's own authentication wins over the fixture's")]
	[MemberData(nameof(Passos))]
	public void Step_Authentication_Wins_Over_The_Fixture(string passo)
	{
		var contexto = Contexto();
		contexto.SetProperty("_KafkaAuthConfig", Sasl("da-fixture"));

		ConfigDoPasso(passo, Sasl("do-passo"), contexto).SaslUsername.ShouldBe("do-passo");
	}

	[Theory(DisplayName = "Every step applies the authentication to a ProducerConfig or ConsumerConfig given to it")]
	[MemberData(nameof(Passos))]
	public void Step_Applies_The_Authentication_To_A_Given_Config(string passo)
	{
		var config = ConfigDoPasso(passo, Sasl("do-passo"), Contexto(), configDada: true);

		config.BootstrapServers.ShouldBe("dado:9092");
		config.SaslUsername.ShouldBe("do-passo");
	}

	// ---------- Apoio ----------

	private static ClientConfig Novo(string cliente) =>
		cliente == "produtor" ? new ProducerConfig() : new ConsumerConfig();

	private static ClientConfig Aplicado(string cliente, Action<KafkaAuthConfig> configurar)
	{
		var autenticacao = new KafkaAuthConfig();
		configurar(autenticacao);
		var config = Novo(cliente);
		AutenticacaoKafka.Aplicar(config, autenticacao);
		return config;
	}

	private static KafkaAuthConfig Sasl(string usuario)
	{
		var autenticacao = new KafkaAuthConfig();
		autenticacao.UseSaslPlain(usuario, "segredo", useSsl: false);
		return autenticacao;
	}

	private static ITestContext Contexto()
	{
		var contexto = new TestContext(new StepStorage());
		// O endereço vem do contexto, para o passo não ir ao arquivo de configuração atrás dele.
		contexto.SetProperty("_KafkaBootstrapServers", "fixture:9092");
		contexto.SetProperty("_KafkaGroupId", "grupo");
		return contexto;
	}

	private static ClientConfig ConfigDoPasso(string passo, KafkaAuthConfig? autenticacao, ITestContext contexto, bool configDada = false) => passo switch
	{
		"produce" => new KafkaProduceStep
		{
			Topic = "t", AuthConfig = autenticacao,
			ProducerConfig = configDada ? new ProducerConfig { BootstrapServers = "dado:9092" } : null
		}.MontarConfig(contexto),
		"batch-produce" => new KafkaBatchProduceStep
		{
			Topic = "t", AuthConfig = autenticacao,
			ProducerConfig = configDada ? new ProducerConfig { BootstrapServers = "dado:9092" } : null
		}.MontarConfig(contexto),
		"consume" => new KafkaConsumeStep
		{
			Topic = "t", AuthConfig = autenticacao,
			ConsumerConfig = configDada ? new ConsumerConfig { BootstrapServers = "dado:9092" } : null
		}.MontarConfig(contexto),
		"batch-consume" => new KafkaBatchConsumeStep
		{
			Topic = "t", AuthConfig = autenticacao,
			ConsumerConfig = configDada ? new ConsumerConfig { BootstrapServers = "dado:9092" } : null
		}.MontarConfig(contexto),
		_ => throw new ArgumentOutOfRangeException(nameof(passo))
	};
}

/// <summary>
/// A fixture aplica a autenticação do arquivo de configuração ao consumidor e ao produtor que
/// cria (#164). Lê <c>testsettings.json</c>, por isso é sequencial com os outros que mexem nele.
/// </summary>
[Collection("TestSettings")]
[Trait("Category", "Kafka")]
[Trait("Component", "Authentication")]
public sealed class AutenticacaoDaFixtureKafkaTests : IDisposable
{
	private readonly string? _antes = Environment.GetEnvironmentVariable("TESTSETTINGS_PATH");
	private readonly string _pasta = Directory.CreateDirectory(
		Path.Combine(Path.GetTempPath(), "kafka-auth-" + Guid.NewGuid().ToString("N"))).FullName;

	public AutenticacaoDaFixtureKafkaTests()
	{
		var arquivo = Path.Combine(_pasta, "testsettings.json");
		File.WriteAllText(arquivo, """
			{
			  "kafka": {
			    "bootstrapServers": "broker:9093",
			    "authentication": {
			      "type": "SaslSsl",
			      "saslPlain": { "username": "ana", "password": "segredo", "useSsl": true },
			      "ssl": { "sslCaLocation": "/certs/ca.pem", "enableSslCertificateVerification": false }
			    }
			  }
			}
			""");
		Environment.SetEnvironmentVariable("TESTSETTINGS_PATH", arquivo);
		LimparCaches();
	}

	public void Dispose()
	{
		Environment.SetEnvironmentVariable("TESTSETTINGS_PATH", _antes);
		LimparCaches();
		Directory.Delete(_pasta, recursive: true);
	}

	[Fact(DisplayName = "The fixture's consumer and shared producer carry the authentication of the settings file")]
	public void Fixture_Applies_The_Settings_Authentication()
	{
		using var fixture = new KafkaClassFixture();

		foreach (ClientConfig config in new ClientConfig[] { fixture.ConfiguracaoDoConsumidor(), fixture.ConfiguracaoDoProdutor() })
		{
			config.BootstrapServers.ShouldBe("broker:9093");
			config.SecurityProtocol.ShouldBe(SecurityProtocol.SaslSsl);
			config.SaslMechanism.ShouldBe(SaslMechanism.Plain);
			config.SaslUsername.ShouldBe("ana");
			config.SslCaLocation.ShouldBe("/certs/ca.pem");
			config.EnableSslCertificateVerification.ShouldBe(false);
		}
	}

	private static void LimparCaches()
	{
		TestSettingsLoader.ClearCache();
		TestSettingsKafkaExtensions.ClearKafkaSettingsCache();
	}
}
