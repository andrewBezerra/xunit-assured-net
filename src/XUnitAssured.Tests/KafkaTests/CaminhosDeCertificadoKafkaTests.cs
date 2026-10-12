using System;
using System.IO;

using XUnitAssured.Core.Configuration;
using XUnitAssured.Core.DSL;
using XUnitAssured.Core.Storage;
using XUnitAssured.Kafka;
using XUnitAssured.Kafka.Configuration;
using XUnitAssured.Kafka.Steps;

namespace XUnitAssured.Tests.KafkaTests;

/// <summary>
/// Um caminho de certificado relativo é lido a partir da pasta do <c>testsettings.json</c>, e
/// não da pasta em que o runner começou (#175).
///
/// <para>
/// O exemplo SASL/SSL usava um caminho absoluto de uma máquina, que além disso apontava para um
/// projeto renomeado: não conectava. Um caminho relativo ia ao librdkafka como escrito, e valia
/// a partir do diretório de trabalho, que muda de runner para runner.
/// </para>
/// </summary>
[Collection("TestSettings")]
[Trait("Category", "Kafka")]
[Trait("Component", "Configuration")]
public sealed class CaminhosDeCertificadoKafkaTests : IDisposable
{
	private readonly string? _antes = Environment.GetEnvironmentVariable("TESTSETTINGS_PATH");
	private readonly string _pasta = Directory.CreateDirectory(
		Path.Combine(Path.GetTempPath(), "kafka-caminhos-" + Guid.NewGuid().ToString("N"))).FullName;

	public CaminhosDeCertificadoKafkaTests()
	{
		foreach (var arquivo in new[] { "ca.pem", "cliente.pem", "cliente.key", "registro-ca.pem" })
		{
			Directory.CreateDirectory(Path.Combine(_pasta, "certs"));
			File.WriteAllText(Path.Combine(_pasta, "certs", arquivo), "");
		}
	}

	public void Dispose()
	{
		Environment.SetEnvironmentVariable("TESTSETTINGS_PATH", _antes);
		LimparCaches();
		Directory.Delete(_pasta, recursive: true);
	}

	[Fact(DisplayName = "Relative certificate paths are read from the settings file's folder: top level, authentication.ssl and schemaRegistry")]
	public void Relative_Paths_Are_Read_From_The_Settings_Folder()
	{
		Usar("""
			"sslCaLocation": "certs/ca.pem",
			"schemaRegistry": { "url": "http://registro:8081", "sslCaLocation": "certs/registro-ca.pem" },
			"authentication": { "type": "MutualTls", "ssl": {
			  "sslCaLocation": "certs/ca.pem", "sslCertificateLocation": "certs/cliente.pem", "sslKeyLocation": "certs/cliente.key" } }
			""");

		var settings = KafkaSettings.Load();

		settings.SslCaLocation.ShouldBe(Path.Combine(_pasta, "certs", "ca.pem"));
		settings.SchemaRegistry!.SslCaLocation.ShouldBe(Path.Combine(_pasta, "certs", "registro-ca.pem"));
		settings.Authentication!.Ssl!.SslCaLocation.ShouldBe(Path.Combine(_pasta, "certs", "ca.pem"));
		settings.Authentication.Ssl.SslCertificateLocation.ShouldBe(Path.Combine(_pasta, "certs", "cliente.pem"));
		settings.Authentication.Ssl.SslKeyLocation.ShouldBe(Path.Combine(_pasta, "certs", "cliente.key"));
	}

	// Sem arquivo ao lado da configuração, o caminho fica como estava: quem contava com o
	// diretório de trabalho continua funcionando.
	[Fact(DisplayName = "A relative path with no file next to the settings, and an absolute path, are kept as written")]
	public void Paths_Without_A_File_There_Are_Kept()
	{
		var absoluto = Path.Combine(_pasta, "certs", "cliente.pem");
		Usar($$"""
			"sslCaLocation": "nao-existe/ca.pem",
			"authentication": { "type": "Ssl", "ssl": { "sslCaLocation": {{System.Text.Json.JsonSerializer.Serialize(absoluto)}} } }
			""");

		var settings = KafkaSettings.Load();

		settings.SslCaLocation.ShouldBe("nao-existe/ca.pem");
		settings.Authentication!.Ssl!.SslCaLocation.ShouldBe(absoluto);
	}

	[Fact(DisplayName = "The resolved path is what reaches the client configuration of a step")]
	public void The_Resolved_Path_Reaches_The_Step()
	{
		Usar(""" "securityProtocol": "SaslSsl", "sslCaLocation": "certs/ca.pem" """);

		var config = new KafkaConsumeStep { Topic = "t" }.MontarConfig(new TestContext(new StepStorage()));

		config.SslCaLocation.ShouldBe(Path.Combine(_pasta, "certs", "ca.pem"));
	}

	// ---------- Apoio ----------

	private void Usar(string seccao)
	{
		var arquivo = Path.Combine(_pasta, "testsettings.json");
		File.WriteAllText(arquivo, $$"""{ "kafka": { "bootstrapServers": "broker:9093", {{seccao}} } }""");
		Environment.SetEnvironmentVariable("TESTSETTINGS_PATH", arquivo);
		LimparCaches();
	}

	private static void LimparCaches()
	{
		TestSettingsLoader.ClearCache();
		TestSettingsKafkaExtensions.ClearKafkaSettingsCache();
	}
}
