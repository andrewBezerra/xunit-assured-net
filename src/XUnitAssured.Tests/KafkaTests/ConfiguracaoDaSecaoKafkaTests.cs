using System;
using System.Collections.Generic;
using System.IO;

using Confluent.Kafka;

using XUnitAssured.Core.Configuration;
using XUnitAssured.Core.DSL;
using XUnitAssured.Core.Storage;
using XUnitAssured.Kafka;
using XUnitAssured.Kafka.Configuration;
using XUnitAssured.Kafka.Steps;
using XUnitAssured.Kafka.Testing;

namespace XUnitAssured.Tests.KafkaTests;

/// <summary>
/// A seção <c>kafka</c> vira a mesma configuração de cliente por qualquer caminho — passo,
/// <see cref="KafkaClassFixture"/> e <see cref="KafkaSettings.ToClientConfig"/> (#172).
///
/// <para>
/// Cada caminho lia uma parte dela. Contra um broker SASL de verdade, credenciais no topo da
/// seção conectavam pelo <c>ToClientConfig</c> e falhavam num passo ("1/1 brokers are down"), e
/// as de <c>authentication</c> o contrário. Agora os dois valem em todos, e
/// <c>authentication</c> vence quando diz alguma coisa. A verificação do hostname do broker só é
/// desligada quando a configuração pede.
/// </para>
/// </summary>
[Collection("TestSettings")]
[Trait("Category", "Kafka")]
[Trait("Component", "Configuration")]
public sealed class ConfiguracaoDaSecaoKafkaTests : IDisposable
{
	private readonly string? _antes = Environment.GetEnvironmentVariable("TESTSETTINGS_PATH");
	private readonly string _pasta = Directory.CreateDirectory(
		Path.Combine(Path.GetTempPath(), "kafka-secao-" + Guid.NewGuid().ToString("N"))).FullName;

	public void Dispose()
	{
		Environment.SetEnvironmentVariable("TESTSETTINGS_PATH", _antes);
		LimparCaches();
		Directory.Delete(_pasta, recursive: true);
	}

	public static TheoryData<string> Caminhos => new() { "ToProducerConfig", "ToConsumerConfig", "passo de produção", "passo de produção em lote", "passo de consumo", "passo de consumo em lote", "produtor da fixture", "consumidor da fixture" };

	[Theory(DisplayName = "SASL credentials at the top of the kafka section reach every path")]
	[MemberData(nameof(Caminhos))]
	public void Top_Level_Sasl_Reaches_Every_Path(string caminho)
	{
		Usar("""
			"securityProtocol": "SaslPlaintext", "saslMechanism": "Plain",
			"saslUsername": "do-topo", "saslPassword": "segredo"
			""");

		var config = Config(caminho);

		config.SecurityProtocol.ShouldBe(SecurityProtocol.SaslPlaintext);
		config.SaslMechanism.ShouldBe(SaslMechanism.Plain);
		config.SaslUsername.ShouldBe("do-topo");
		config.SaslPassword.ShouldBe("segredo");
	}

	[Theory(DisplayName = "The authentication section reaches every path, ToClientConfig included")]
	[MemberData(nameof(Caminhos))]
	public void Authentication_Reaches_Every_Path(string caminho)
	{
		Usar(""" "authentication": { "type": "SaslPlain", "saslPlain": { "username": "da-secao", "password": "segredo", "useSsl": false } } """);

		var config = Config(caminho);

		config.SecurityProtocol.ShouldBe(SecurityProtocol.SaslPlaintext);
		config.SaslUsername.ShouldBe("da-secao");
	}

	[Theory(DisplayName = "When both set the credentials, authentication wins on every path")]
	[MemberData(nameof(Caminhos))]
	public void Authentication_Wins_Over_The_Top_Level(string caminho)
	{
		Usar("""
			"securityProtocol": "SaslPlaintext", "saslMechanism": "Plain", "saslUsername": "do-topo", "saslPassword": "x",
			"authentication": { "type": "SaslScram512", "saslScram": { "username": "da-secao", "password": "y", "useSsl": true } }
			""");

		var config = Config(caminho);

		config.SecurityProtocol.ShouldBe(SecurityProtocol.SaslSsl);
		config.SaslMechanism.ShouldBe(SaslMechanism.ScramSha512);
		config.SaslUsername.ShouldBe("da-secao");
		config.SaslPassword.ShouldBe("y");
	}

	// "None" é o valor do exemplo do README: não diz nada, então o topo vale.
	[Theory(DisplayName = "An authentication of type None leaves the top-level credentials in place")]
	[MemberData(nameof(Caminhos))]
	public void Authentication_None_Keeps_The_Top_Level(string caminho)
	{
		Usar("""
			"securityProtocol": "SaslPlaintext", "saslMechanism": "Plain", "saslUsername": "do-topo", "saslPassword": "x",
			"authentication": { "type": "None" }
			""");

		Config(caminho).SaslUsername.ShouldBe("do-topo");
	}

	[Theory(DisplayName = "The top-level CA, certificate verification and additionalProperties reach every path")]
	[MemberData(nameof(Caminhos))]
	public void Top_Level_Ssl_And_Additional_Properties_Reach_Every_Path(string caminho)
	{
		Usar("""
			"securityProtocol": "Ssl", "sslCaLocation": "/certs/ca.pem", "enableSslCertificateVerification": false,
			"additionalProperties": { "socket.keepalive.enable": "true" }
			""");

		var config = Config(caminho);

		config.SecurityProtocol.ShouldBe(SecurityProtocol.Ssl);
		config.SslCaLocation.ShouldBe("/certs/ca.pem");
		config.EnableSslCertificateVerification.ShouldBe(false);
		config.Get("socket.keepalive.enable").ShouldBe("true");
	}

	// Até a 6.16, a fixture desligava a verificação sozinha com SSL; os passos não.
	[Theory(DisplayName = "Hostname verification stays on with SSL, unless the settings turn it off")]
	[MemberData(nameof(Caminhos))]
	public void Hostname_Verification_Is_Off_Only_When_Asked(string caminho)
	{
		Usar(""" "securityProtocol": "SaslSsl", "sslCaLocation": "/certs/ca.pem" """);
		Config(caminho).SslEndpointIdentificationAlgorithm.ShouldBeNull();

		Usar(""" "securityProtocol": "SaslSsl", "sslCaLocation": "/certs/ca.pem", "sslEndpointIdentificationAlgorithm": "None" """);
		Config(caminho).SslEndpointIdentificationAlgorithm.ShouldBe(SslEndpointIdentificationAlgorithm.None);
	}

	[Fact(DisplayName = "A step's own authentication wins over the top-level credentials")]
	public void Step_Authentication_Wins_Over_The_Top_Level()
	{
		Usar(""" "securityProtocol": "SaslPlaintext", "saslMechanism": "Plain", "saslUsername": "do-topo", "saslPassword": "x" """);
		var doPasso = new KafkaAuthConfig();
		doPasso.UseSaslScram256("do-passo", "y", useSsl: false);

		var config = new KafkaProduceStep { Topic = "t", AuthConfig = doPasso }.MontarConfig(Contexto());

		config.SaslMechanism.ShouldBe(SaslMechanism.ScramSha256);
		config.SaslUsername.ShouldBe("do-passo");
	}

	// ---------- Apoio ----------

	private void Usar(string seccao)
	{
		var arquivo = Path.Combine(_pasta, $"testsettings-{Guid.NewGuid():N}.json");
		File.WriteAllText(arquivo, $$"""{ "kafka": { "bootstrapServers": "broker:9092", {{seccao}} } }""");
		Environment.SetEnvironmentVariable("TESTSETTINGS_PATH", arquivo);
		LimparCaches();
	}

	private static ClientConfig Config(string caminho)
	{
		switch (caminho)
		{
			case "ToProducerConfig": return KafkaSettings.Load().ToProducerConfig();
			case "ToConsumerConfig": return KafkaSettings.Load().ToConsumerConfig();
			case "passo de produção": return new KafkaProduceStep { Topic = "t" }.MontarConfig(Contexto());
			case "passo de consumo": return new KafkaConsumeStep { Topic = "t" }.MontarConfig(Contexto());
			case "passo de produção em lote": return new KafkaBatchProduceStep { Topic = "t" }.MontarConfig(Contexto());
			case "passo de consumo em lote": return new KafkaBatchConsumeStep { Topic = "t" }.MontarConfig(Contexto());
		}

		using var fixture = new KafkaClassFixture();
		return caminho == "produtor da fixture" ? fixture.ConfiguracaoDoProdutor() : fixture.ConfiguracaoDoConsumidor();
	}

	private static TestContext Contexto() => new(new StepStorage());

	private static void LimparCaches()
	{
		TestSettingsLoader.ClearCache();
		TestSettingsKafkaExtensions.ClearKafkaSettingsCache();
	}
}
