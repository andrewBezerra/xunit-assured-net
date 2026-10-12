using System;
using System.IO;

using XUnitAssured.Core.Configuration;
using XUnitAssured.Http.Configuration;
using XUnitAssured.Kafka;
using XUnitAssured.Kafka.Configuration;
using XUnitAssured.RabbitMq;

namespace XUnitAssured.Tests.CoreTests;

/// <summary>
/// <c>testsettings.{ambiente}.json</c> vale para as seções dos pacotes, não só para o Core (#154).
///
/// <para>
/// Antes, com <c>TEST_ENV=staging</c>, o arquivo de staging valia só para testMode e environment:
/// http, kafka, rabbitmq e playwright vinham do arquivo base, e o de staging era ignorado sem aviso.
/// Agora o arquivo do ambiente é posto sobre o base e sobrepõe só o que escreve.
/// </para>
/// </summary>
[Collection("TestSettings")]
[Trait("Category", "Core")]
[Trait("Component", "Configuration")]
public sealed class ArquivoDoAmbienteTests : IDisposable
{
	private readonly string _pasta = Directory.CreateDirectory(
		Path.Combine(Path.GetTempPath(), "ambiente-" + Guid.NewGuid().ToString("N"))).FullName;

	public ArquivoDoAmbienteTests()
	{
		Escrever("testsettings.json", """
			{
			  "testMode": "Local",
			  "http": {
			    "baseUrl": "https://base.teste",
			    "timeout": 60,
			    "authentication": { "type": "Bearer", "bearer": { "token": "tk-base" } }
			  },
			  "kafka": { "bootstrapServers": "base:9092", "groupId": "grupo-base" },
			  "rabbitmq": { "connectionUri": "amqp://base/" }
			}
			""");
		Escrever("testsettings.staging.json", """
			{
			  "testMode": "Remote",
			  "Http": { "baseUrl": "https://staging.teste" },
			  "kafka": { "bootstrapServers": "staging:9092" },
			  "rabbitmq": { "connectionUri": "amqp://staging/" }
			}
			""");

		Environment.SetEnvironmentVariable("TESTSETTINGS_PATH", Path.Combine(_pasta, "testsettings.json"));
		Environment.SetEnvironmentVariable("TEST_ENV", null);
		LimparCaches();
	}

	public void Dispose()
	{
		Environment.SetEnvironmentVariable("TESTSETTINGS_PATH", null);
		Environment.SetEnvironmentVariable("TEST_ENV", null);
		LimparCaches();
		Directory.Delete(_pasta, recursive: true);
	}

	[Fact(DisplayName = "The environment's file overrides what it writes in every section, and keeps what it leaves out")]
	public void Environment_File_Overrides_Only_What_It_Writes()
	{
		var settings = TestSettings.Load("staging");

		settings.TestMode.ShouldBe(TestMode.Remote);
		settings.Environment.ShouldBe("staging");

		// "Http" no arquivo do ambiente e "http" no base são a mesma seção
		var http = settings.GetHttpSettings()!;
		http.BaseUrl.ShouldBe("https://staging.teste");
		http.Timeout.ShouldBe(60);
		http.Authentication!.Bearer!.Token.ShouldBe("tk-base");

		var kafka = settings.GetKafkaSettings()!;
		kafka.BootstrapServers.ShouldBe("staging:9092");
		kafka.GroupId.ShouldBe("grupo-base");
	}

	[Fact(DisplayName = "TEST_ENV reaches the sections read without a TestSettings, as RabbitMQ and the steps read them")]
	public void Test_Env_Reaches_Every_Section()
	{
		Environment.SetEnvironmentVariable("TEST_ENV", "staging");

		RabbitMqSettings.Load().ConnectionUri.ShouldBe("amqp://staging/");
		KafkaSettings.Load().BootstrapServers.ShouldBe("staging:9092");
		new TestSettings().GetHttpSettings()!.BaseUrl.ShouldBe("https://staging.teste");
	}

	// O cache é por arquivo: sem o ambiente na chave, quem leu primeiro decidiria para os dois.
	[Fact(DisplayName = "Settings with and without an environment do not share a cached section")]
	public void Cache_Tells_Environments_Apart()
	{
		TestSettings.Load().GetHttpSettings()!.BaseUrl.ShouldBe("https://base.teste");
		TestSettings.Load("staging").GetHttpSettings()!.BaseUrl.ShouldBe("https://staging.teste");
		TestSettings.Load().GetKafkaSettings()!.BootstrapServers.ShouldBe("base:9092");
	}

	// Antes, sem TESTSETTINGS_PATH, um ambiente sem arquivo dava os padrões, nem o base. Com ele,
	// como aqui, já dava o base: este teste guarda o comportamento, não a mudança.
	[Fact(DisplayName = "An environment with no file of its own reads the base file")]
	public void Environment_Without_File_Reads_The_Base()
	{
		var settings = TestSettings.Load("producao");

		settings.TestMode.ShouldBe(TestMode.Local);
		settings.Environment.ShouldBe("producao");
		settings.GetHttpSettings()!.BaseUrl.ShouldBe("https://base.teste");
	}

	[Fact(DisplayName = "A malformed environment file fails TestSettings.Load naming it, and leaves the sections unread")]
	public void Malformed_Environment_File()
	{
		Escrever("testsettings.quebrado.json", "{ \"http\": ");

		Should.Throw<InvalidOperationException>(() => TestSettings.Load("quebrado"))
			.Message.ShouldContain("testsettings.quebrado.json");

		Environment.SetEnvironmentVariable("TEST_ENV", "quebrado");
		new TestSettings().GetHttpSettings().ShouldBeNull();
	}

	// ---------- Apoio ----------

	private void Escrever(string nome, string json) => File.WriteAllText(Path.Combine(_pasta, nome), json);

	private static void LimparCaches()
	{
		TestSettingsLoader.ClearCache();
		TestSettingsHttpExtensions.ClearHttpSettingsCache();
		TestSettingsKafkaExtensions.ClearKafkaSettingsCache();
	}
}
