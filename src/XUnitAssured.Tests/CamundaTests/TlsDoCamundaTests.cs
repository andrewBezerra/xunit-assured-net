using System;
using System.IO;
using System.Linq;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

using XUnitAssured.Camunda;
using XUnitAssured.Camunda.Client;
using XUnitAssured.Camunda.Testcontainers;

namespace XUnitAssured.Tests.CamundaTests;

/// <summary>
/// O Camunda do Testcontainers só com TLS, sem Docker: a autoridade de teste e o certificado do servidor, as variáveis do
/// Camunda e dos workers, os endereços em https, e o cliente do teste confiando só na autoridade.
/// </summary>
[Trait("Category", "Camunda")]
[Trait("Component", "Testcontainers")]
public class TlsDoCamundaTests
{
	private static readonly DateTimeOffset Agora = new(2026, 11, 6, 12, 0, 0, TimeSpan.Zero);

	// ---------- O certificado ----------

	[Fact(DisplayName = "The server certificate carries the network name and the host names, for server authentication")]
	public void Server_Certificate_Has_The_Names()
	{
		using var certificados = CertificadosDoTeste.Gerar(EnderecosDoCamunda.Padrao.NomesDoCertificado(["docker.empresa"]), Agora);

		var nomes = NomesAlternativos(certificados.Servidor);
		nomes.ShouldBe(["DNS:camunda", "DNS:localhost", "IP:127.0.0.1", "IP:::1", "DNS:docker.empresa"], ignoreOrder: true);
		certificados.Servidor.Extensions.OfType<X509EnhancedKeyUsageExtension>().Single()
			.EnhancedKeyUsages.Cast<Oid>().Select(o => o.Value).ShouldBe(["1.3.6.1.5.5.7.3.1"]);
		certificados.Servidor.HasPrivateKey.ShouldBeTrue();
	}

	[Fact(DisplayName = "The certificates are valid from five minutes before generation to a day after")]
	public void Certificates_Are_Valid_For_A_Day()
	{
		using var certificados = CertificadosDoTeste.Gerar(["camunda"], Agora);

		foreach (var certificado in new[] { certificados.Autoridade, certificados.Servidor })
		{
			new DateTimeOffset(certificado.NotBefore.ToUniversalTime()).ShouldBe(Agora.AddMinutes(-5));
			new DateTimeOffset(certificado.NotAfter.ToUniversalTime()).ShouldBe(Agora.AddDays(1));
		}
	}

	[Fact(DisplayName = "The server certificate's chain closes at the test authority, which is a CA and only a CA")]
	public void Server_Chain_Closes_At_The_Authority()
	{
		using var certificados = CertificadosDoTeste.Gerar(["camunda"], DateTimeOffset.UtcNow);

		ConfiancaNaAutoridade.Valida(certificados.Servidor, SslPolicyErrors.RemoteCertificateChainErrors, certificados.Autoridade).ShouldBeTrue();
		var restricoes = certificados.Autoridade.Extensions.OfType<X509BasicConstraintsExtension>().Single();
		restricoes.CertificateAuthority.ShouldBeTrue();
		certificados.Servidor.Issuer.ShouldBe(certificados.Autoridade.Subject);
	}

	[Fact(DisplayName = "Each run gets a new authority, and the server certificate of one does not chain to the other")]
	public void Each_Run_Has_Its_Own_Authority()
	{
		using var um = CertificadosDoTeste.Gerar(["camunda"], DateTimeOffset.UtcNow);
		using var outro = CertificadosDoTeste.Gerar(["camunda"], DateTimeOffset.UtcNow);

		um.Autoridade.Thumbprint.ShouldNotBe(outro.Autoridade.Thumbprint);
		um.SenhaDoTruststore.ShouldNotBe(outro.SenhaDoTruststore);
		ConfiancaNaAutoridade.Valida(um.Servidor, SslPolicyErrors.RemoteCertificateChainErrors, outro.Autoridade).ShouldBeFalse();
	}

	[Fact(DisplayName = "The files are the authority and the server chain in PEM, the key in PKCS#8, and the Java truststore")]
	public void Files_Are_Pem_And_Pkcs12()
	{
		using var certificados = CertificadosDoTeste.Gerar(["camunda"], DateTimeOffset.UtcNow);

		var arquivos = certificados.Arquivos();

		arquivos.Keys.ShouldBe(["ca.pem", "server.pem", "server.key", "truststore.p12"], ignoreOrder: true);
		Texto(arquivos["ca.pem"]).ShouldStartWith("-----BEGIN CERTIFICATE-----");
		var servidor = Texto(arquivos["server.pem"]);
		servidor.Split("-----BEGIN CERTIFICATE-----").Length.ShouldBe(3, "o certificado do servidor e a autoridade");
		Texto(arquivos["server.key"]).ShouldStartWith("-----BEGIN PRIVATE KEY-----");
		using var lido = X509Certificate2.CreateFromPem(servidor, Texto(arquivos["server.key"]));
		lido.Thumbprint.ShouldBe(certificados.Servidor.Thumbprint);
	}

	[Fact(DisplayName = "The Java truststore holds the authority, marked as trusted the way keytool marks it")]
	public void Java_Truststore_Trusts_The_Authority()
	{
		using var certificados = CertificadosDoTeste.Gerar(["camunda"], DateTimeOffset.UtcNow);

		var info = Pkcs12Info.Decode(certificados.Arquivos()["truststore.p12"], out _, skipCopy: true);
		info.VerifyMac(certificados.SenhaDoTruststore).ShouldBeTrue();
		var conteudo = info.AuthenticatedSafe.Single();
		conteudo.Decrypt(certificados.SenhaDoTruststore);
		var bolsa = conteudo.GetBags().OfType<Pkcs12CertBag>().Single();

		bolsa.GetCertificate().Thumbprint.ShouldBe(certificados.Autoridade.Thumbprint);
		bolsa.GetCertificate().HasPrivateKey.ShouldBeFalse();
		bolsa.Attributes.Cast<CryptographicAttributeObject>().Select(a => a.Oid.Value).ShouldContain("2.16.840.1.113894.746875.1.1");
	}

	[Fact(DisplayName = "The files are written to the host folder")]
	public void Files_Are_Written()
	{
		using var certificados = CertificadosDoTeste.Gerar(["camunda"], DateTimeOffset.UtcNow);
		var pasta = Directory.CreateTempSubdirectory("xunitassured-tls-teste-").FullName;
		try
		{
			certificados.Gravar(pasta);

			Directory.GetFiles(pasta).Select(Path.GetFileName).ShouldBe(["ca.pem", "server.key", "server.pem", "truststore.p12"], ignoreOrder: true);
		}
		finally
		{
			Directory.Delete(pasta, recursive: true);
		}
	}

	// ---------- A confiança do cliente ----------

	[Fact(DisplayName = "The client refuses a server certificate from another authority, a self-signed one, a wrong name and none")]
	public void Client_Trusts_Only_The_Authority()
	{
		using var certificados = CertificadosDoTeste.Gerar(["camunda"], DateTimeOffset.UtcNow);
		using var outra = CertificadosDoTeste.Gerar(["camunda"], DateTimeOffset.UtcNow);
		using var chave = RSA.Create(2048);
		using var autoassinado = new CertificateRequest("CN=camunda", chave, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
			.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddDays(1));
		var callback = ConfiancaNaAutoridade.Callback(certificados.Autoridade);

		callback(this, certificados.Servidor, null, SslPolicyErrors.RemoteCertificateChainErrors).ShouldBeTrue();
		callback(this, outra.Servidor, null, SslPolicyErrors.RemoteCertificateChainErrors).ShouldBeFalse();
		callback(this, autoassinado, null, SslPolicyErrors.RemoteCertificateChainErrors).ShouldBeFalse();
		callback(this, certificados.Servidor, null, SslPolicyErrors.RemoteCertificateNameMismatch).ShouldBeFalse();
		callback(this, null, null, SslPolicyErrors.RemoteCertificateNotAvailable).ShouldBeFalse();
	}

	[Fact(DisplayName = "With caCertificatePath, the client's connections validate the server by that authority alone")]
	public void Client_Transport_Uses_The_Authority()
	{
		using var certificados = CertificadosDoTeste.Gerar(["camunda"], DateTimeOffset.UtcNow);
		var pasta = Directory.CreateTempSubdirectory("xunitassured-tls-teste-").FullName;
		try
		{
			certificados.Gravar(pasta);
			var comAutoridade = new CamundaSettings { CaCertificatePath = Path.Combine(pasta, "ca.pem") };

			using var transporte = CamundaApiClient.Transporte(comAutoridade, _ => null);
			using var semAutoridade = CamundaApiClient.Transporte(new CamundaSettings(), _ => null);
			using var peloAmbiente = CamundaApiClient.Transporte(
				new CamundaSettings(), nome => nome == "CAMUNDA_MTLS_CA_PATH" ? comAutoridade.CaCertificatePath : null);

			var callback = transporte.SslOptions.RemoteCertificateValidationCallback.ShouldNotBeNull();
			callback(this, certificados.Servidor, null, SslPolicyErrors.RemoteCertificateChainErrors).ShouldBeTrue();
			using var outra = CertificadosDoTeste.Gerar(["camunda"], DateTimeOffset.UtcNow);
			callback(this, outra.Servidor, null, SslPolicyErrors.RemoteCertificateChainErrors).ShouldBeFalse();
			semAutoridade.SslOptions.RemoteCertificateValidationCallback.ShouldBeNull();
			peloAmbiente.SslOptions.RemoteCertificateValidationCallback.ShouldNotBeNull();
			CamundaApiClient.Key(comAutoridade).ShouldNotBe(CamundaApiClient.Key(new CamundaSettings()));
		}
		finally
		{
			Directory.Delete(pasta, recursive: true);
		}
	}

	// ---------- O Camunda e os workers ----------

	[Fact(DisplayName = "The Camunda turns TLS on for REST and gRPC, with the certificate and key copied into the container")]
	public async Task Camunda_Gets_Tls()
	{
		var criador = new WorkersNoContainerTests.CriadorFalso();
		await using var fixture = Fixture(criador);

		await fixture.InitializeAsync();

		var camunda = criador.Definicao("camunda");
		camunda.Variaveis["SERVER_SSL_ENABLED"].ShouldBe("true");
		camunda.Variaveis["SERVER_SSL_CERTIFICATE"].ShouldBe("/xunitassured/tls/server.pem");
		camunda.Variaveis["SERVER_SSL_CERTIFICATE_PRIVATE_KEY"].ShouldBe("/xunitassured/tls/server.key");
		camunda.Variaveis["CAMUNDA_API_GRPC_SSL_ENABLED"].ShouldBe("true");
		camunda.Variaveis["CAMUNDA_API_GRPC_SSL_CERTIFICATE"].ShouldBe("/xunitassured/tls/server.pem");
		camunda.Variaveis["CAMUNDA_API_GRPC_SSL_CERTIFICATEPRIVATEKEY"].ShouldBe("/xunitassured/tls/server.key");
		camunda.Arquivos.Keys.ShouldBe(
			["/xunitassured/tls/ca.pem", "/xunitassured/tls/server.pem", "/xunitassured/tls/server.key", "/xunitassured/tls/truststore.p12"],
			ignoreOrder: true);
		camunda.Autoridade.ShouldNotBeNull("a espera até ficar pronto é em https, confiando na autoridade");
	}

	[Fact(DisplayName = "A worker gets the authority through the common variables, and never the server key")]
	public async Task Worker_Trusts_The_Authority()
	{
		var criador = new WorkersNoContainerTests.CriadorFalso();
		await using var fixture = Fixture(criador);

		await fixture.InitializeAsync();

		var worker = criador.Definicao("worker");
		foreach (var variavel in new[]
			{ "SSL_CERT_FILE", "NODE_EXTRA_CA_CERTS", "REQUESTS_CA_BUNDLE", "CAMUNDA_MTLS_CA_PATH", "ZEEBE_CA_CERTIFICATE_PATH", "CAMUNDA_CLIENT_CACERTIFICATEPATH" })
			worker.Variaveis[variavel].ShouldBe("/xunitassured/tls/ca.pem", variavel);
		worker.Variaveis["JAVA_TOOL_OPTIONS"].ShouldContain("-Djavax.net.ssl.trustStore=/xunitassured/tls/truststore.p12");
		worker.Variaveis["JAVA_TOOL_OPTIONS"].ShouldContain("-Djavax.net.ssl.trustStoreType=PKCS12");
		worker.Variaveis["JAVA_TOOL_OPTIONS"].ShouldContain("-Djavax.net.ssl.trustStorePassword=");
		worker.Arquivos.Keys.ShouldBe(["/xunitassured/tls/ca.pem", "/xunitassured/tls/truststore.p12"], ignoreOrder: true);
		worker.Autoridade.ShouldBeNull();
	}

	[Fact(DisplayName = "A declared JAVA_TOOL_OPTIONS keeps its options after the truststore's, and the TLS placeholders resolve")]
	public async Task Java_Options_And_Tls_Placeholders()
	{
		var criador = new WorkersNoContainerTests.CriadorFalso();
		var worker = new CamundaServiceContainer { Name = "worker", Image = "exemplo/worker:1" };
		worker.Environment["JAVA_TOOL_OPTIONS"] = "-Xmx256m";
		worker.Environment["App__Ca"] = "${camunda.caCertificatePath}";
		worker.Environment["App__Store"] = "${camunda.trustStorePath}";
		worker.Environment["App__StorePassword"] = "${camunda.trustStorePassword}";
		await using var fixture = Fixture(criador, worker);

		await fixture.InitializeAsync();

		var variaveis = criador.Definicao("worker").Variaveis;
		variaveis["JAVA_TOOL_OPTIONS"].ShouldStartWith("-Djavax.net.ssl.trustStore=");
		variaveis["JAVA_TOOL_OPTIONS"].ShouldEndWith(" -Xmx256m");
		variaveis["App__Ca"].ShouldBe("/xunitassured/tls/ca.pem");
		variaveis["App__Store"].ShouldBe("/xunitassured/tls/truststore.p12");
		variaveis["JAVA_TOOL_OPTIONS"].ShouldContain($"-Djavax.net.ssl.trustStorePassword={variaveis["App__StorePassword"]}");
	}

	[Fact(DisplayName = "Every address is https: on the network, for gRPC and on the host, and no variable carries http")]
	public async Task Every_Address_Is_Https()
	{
		var criador = new WorkersNoContainerTests.CriadorFalso();
		await using var fixture = Fixture(criador);

		await fixture.InitializeAsync();

		EnderecosDoCamunda.Padrao.RestNaRede.ShouldBe("https://camunda:8080");
		EnderecosDoCamunda.Padrao.GrpcNaRede.ShouldBe("https://camunda:26500");
		fixture.Settings.RestAddress.ShouldStartWith("https://");
		criador.Subidos.SelectMany(d => d.Variaveis.Values).ShouldNotContain(v => v.Contains("http:", StringComparison.Ordinal));
	}

	[Fact(DisplayName = "The test client trusts the authority file in a host folder that disposing the fixture deletes")]
	public async Task Host_Folder_Lives_With_The_Fixture()
	{
		var criador = new WorkersNoContainerTests.CriadorFalso();
		var fixture = Fixture(criador);
		await fixture.InitializeAsync();

		var autoridade = fixture.Settings.CaCertificatePath.ShouldNotBeNull();
		File.Exists(autoridade).ShouldBeTrue();
		var pasta = fixture.PastaDoTls.ShouldNotBeNull();
		Path.GetDirectoryName(autoridade).ShouldBe(pasta);

		await fixture.DisposeAsync();

		Directory.Exists(pasta).ShouldBeFalse();
	}

	[Theory(DisplayName = "The certificate also names the Docker host the test reaches the container through")]
	[InlineData("TESTCONTAINERS_HOST_OVERRIDE", "docker.empresa", "docker.empresa")]
	[InlineData("DOCKER_HOST", "tcp://10.0.0.5:2375", "10.0.0.5")]
	[InlineData("DOCKER_HOST", "npipe:////./pipe/docker_engine", null)]
	public void Docker_Host_Is_Named(string variavel, string valor, string? esperado)
	{
		var hosts = EnderecosDoCamunda.HostsDoDocker(nome => nome == variavel ? valor : null).ToList();

		if (esperado is null)
			hosts.ShouldBeEmpty();
		else
			hosts.ShouldBe([esperado]);
	}

	// ---------- Apoio ----------

	private static CamundaFixture Fixture(WorkersNoContainerTests.CriadorFalso criador, CamundaServiceContainer? worker = null) =>
		new(new CamundaSectionWithContainers
		{
			Containers = new CamundaContainersSettings { Services = [worker ?? new CamundaServiceContainer { Name = "worker", Image = "exemplo/worker:1" }] }
		}, _ => null, criador);

	private static string Texto(byte[] conteudo) => Encoding.ASCII.GetString(conteudo);

	private static string[] NomesAlternativos(X509Certificate2 certificado)
	{
		var extensao = certificado.Extensions.OfType<X509SubjectAlternativeNameExtension>().Single();
		return [.. extensao.EnumerateDnsNames().Select(n => $"DNS:{n}"), .. extensao.EnumerateIPAddresses().Select(ip => $"IP:{ip}")];
	}
}
