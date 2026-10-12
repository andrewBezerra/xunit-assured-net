using System;
using System.IO;

using Confluent.Kafka;

using XUnitAssured.Core.Configuration;
using XUnitAssured.Core.DSL;
using XUnitAssured.Core.Storage;
using XUnitAssured.Kafka;
using XUnitAssured.Kafka.Configuration;
using XUnitAssured.Kafka.Handlers;
using XUnitAssured.Kafka.Steps;

namespace XUnitAssured.Tests.KafkaTests;

/// <summary>
/// SASL/OAUTHBEARER com OpenID Connect (#179): o cliente pede o token ao endpoint com as
/// credenciais do cliente OAuth, e o librdkafka o renova sozinho. Sem broker: o que se confere é
/// a configuração que chega ao produtor e ao consumidor.
/// </summary>
[Trait("Category", "Kafka")]
[Trait("Component", "Authentication")]
public class OAuthBearerKafkaTests
{
	private const string Endpoint = "http://localhost:8080/realms/kafka/protocol/openid-connect/token";

	public static TheoryData<string> Clientes => new() { "produtor", "consumidor" };

	[Theory(DisplayName = "OAUTHBEARER sets the mechanism, the OIDC method, the token endpoint and the client credentials")]
	[MemberData(nameof(Clientes))]
	public void OAuthBearer_Reaches_The_Client_Config(string cliente)
	{
		var config = Aplicado(cliente, a => a.UseOAuthBearer(Endpoint, "kafka-client", "segredo", "kafka", useSsl: false));

		config.SecurityProtocol.ShouldBe(SecurityProtocol.SaslPlaintext);
		config.SaslMechanism.ShouldBe(SaslMechanism.OAuthBearer);
		config.SaslOauthbearerMethod.ShouldBe(SaslOauthbearerMethod.Oidc);
		config.SaslOauthbearerTokenEndpointUrl.ShouldBe(Endpoint);
		config.SaslOauthbearerClientId.ShouldBe("kafka-client");
		config.SaslOauthbearerClientSecret.ShouldBe("segredo");
		config.SaslOauthbearerScope.ShouldBe("kafka");
	}

	[Theory(DisplayName = "OAUTHBEARER uses SASL_SSL by default, and asks for no scope when none is given")]
	[MemberData(nameof(Clientes))]
	public void OAuthBearer_Defaults(string cliente)
	{
		var config = Aplicado(cliente, a => a.UseOAuthBearer(Endpoint, "kafka-client", "segredo"));

		config.SecurityProtocol.ShouldBe(SecurityProtocol.SaslSsl);
		config.SaslOauthbearerScope.ShouldBeNull();
	}

	[Theory(DisplayName = "OAUTHBEARER without the token endpoint, the client id or the secret fails naming what is missing")]
	[InlineData("endpoint", "token endpoint URL")]
	[InlineData("id", "client id")]
	[InlineData("secret", "client secret")]
	public void OAuthBearer_Incomplete_Fails(string faltando, string mensagem)
	{
		var autenticacao = new KafkaAuthConfig();
		autenticacao.UseOAuthBearer(
			faltando == "endpoint" ? "" : Endpoint,
			faltando == "id" ? " " : "kafka-client",
			faltando == "secret" ? "" : "segredo");

		Should.Throw<InvalidOperationException>(() => AutenticacaoKafka.Aplicar(new ProducerConfig(), autenticacao))
			.Message.ShouldContain(mensagem);
	}

	[Fact(DisplayName = "WithOAuthBearer puts OAUTHBEARER in the step's client configuration")]
	public void WithOAuthBearer_Reaches_The_Step()
	{
		var cenario = Given()
			.Topic("pedidos")
			.Consume()
			.WithOAuthBearer(Endpoint, "kafka-client", "segredo", useSsl: false);

		var contexto = new TestContext(new StepStorage());
		contexto.SetProperty("_KafkaBootstrapServers", "broker:9092");
		var config = ((KafkaConsumeStep)cenario.CurrentStep!).MontarConfig(contexto);

		config.SecurityProtocol.ShouldBe(SecurityProtocol.SaslPlaintext);
		config.SaslMechanism.ShouldBe(SaslMechanism.OAuthBearer);
		config.SaslOauthbearerClientId.ShouldBe("kafka-client");
		config.SaslOauthbearerTokenEndpointUrl.ShouldBe(Endpoint);
	}

	[Fact(DisplayName = "UseNoAuth clears the OAUTHBEARER configuration")]
	public void UseNoAuth_Clears_OAuthBearer()
	{
		var autenticacao = new KafkaAuthConfig();
		autenticacao.UseOAuthBearer(Endpoint, "kafka-client", "segredo");

		autenticacao.UseNoAuth();

		autenticacao.Type.ShouldBe(KafkaAuthenticationType.None);
		autenticacao.OAuthBearer.ShouldBeNull();
	}

	private static ClientConfig Aplicado(string cliente, Action<KafkaAuthConfig> configurar)
	{
		var autenticacao = new KafkaAuthConfig();
		configurar(autenticacao);
		ClientConfig config = cliente == "produtor" ? new ProducerConfig() : new ConsumerConfig();
		AutenticacaoKafka.Aplicar(config, autenticacao);
		return config;
	}
}

/// <summary>
/// OAUTHBEARER escrito no <c>testsettings.json</c> chega ao cliente (#179).
/// </summary>
[Collection("TestSettings")]
[Trait("Category", "Kafka")]
[Trait("Component", "Authentication")]
public sealed class OAuthBearerNoArquivoKafkaTests : IDisposable
{
	private readonly string? _antes = Environment.GetEnvironmentVariable("TESTSETTINGS_PATH");
	private readonly string _pasta = Directory.CreateDirectory(
		Path.Combine(Path.GetTempPath(), "kafka-oauth-" + Guid.NewGuid().ToString("N"))).FullName;

	public void Dispose()
	{
		Environment.SetEnvironmentVariable("TESTSETTINGS_PATH", _antes);
		LimparCaches();
		Directory.Delete(_pasta, recursive: true);
	}

	[Fact(DisplayName = "An OAuthBearer authentication in testsettings.json reaches ToClientConfig and the steps")]
	public void OAuthBearer_From_The_Settings_File()
	{
		var arquivo = Path.Combine(_pasta, "testsettings.json");
		File.WriteAllText(arquivo, """
			{ "kafka": { "bootstrapServers": "localhost:9092",
			  "authentication": { "type": "OAuthBearer", "oAuthBearer": {
			    "tokenEndpointUrl": "http://localhost:8080/realms/kafka/protocol/openid-connect/token",
			    "clientId": "kafka-client", "clientSecret": "segredo", "useSsl": false } } } }
			""");
		Environment.SetEnvironmentVariable("TESTSETTINGS_PATH", arquivo);
		LimparCaches();

		foreach (ClientConfig config in new ClientConfig[]
		{
			KafkaSettings.Load().ToProducerConfig(),
			new KafkaProduceStep { Topic = "t" }.MontarConfig(new TestContext(new StepStorage()))
		})
		{
			config.SecurityProtocol.ShouldBe(SecurityProtocol.SaslPlaintext);
			config.SaslMechanism.ShouldBe(SaslMechanism.OAuthBearer);
			config.SaslOauthbearerClientId.ShouldBe("kafka-client");
			config.SaslOauthbearerClientSecret.ShouldBe("segredo");
		}
	}

	private static void LimparCaches()
	{
		TestSettingsLoader.ClearCache();
		TestSettingsKafkaExtensions.ClearKafkaSettingsCache();
	}
}
