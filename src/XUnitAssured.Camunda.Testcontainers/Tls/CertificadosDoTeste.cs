using System;
using System.Collections.Generic;
using System.Formats.Asn1;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace XUnitAssured.Camunda.Testcontainers;

/// <summary>
/// A autoridade de teste e o certificado do servidor de uma execução, gerados pelo .NET, sem ferramenta de fora: o Camunda
/// usa o do servidor, e o teste e os workers confiam só na autoridade.
/// </summary>
/// <remarks>
/// Valem do momento da geração (com cinco minutos de folga para o relógio do container) até um dia depois. Os arquivos ficam
/// numa pasta do container (<see cref="EnderecosDoCamunda.PastaDoTls"/>) e numa pasta temporária do host, apagada no fim.
/// </remarks>
internal sealed class CertificadosDoTeste : IDisposable
{
	internal const string ArquivoDaAutoridade = "ca.pem";
	internal const string ArquivoDoServidor = "server.pem";
	internal const string ArquivoDaChave = "server.key";
	internal const string ArquivoDoTruststore = "truststore.p12";

	// O atributo que o keytool põe no certificado confiável de um PKCS12: sem ele, o Java não lê o certificado como confiável.
	private const string OidDoConfiavelNoJava = "2.16.840.1.113894.746875.1.1";
	private const string OidDeQualquerUso = "2.5.29.37.0";
	private const string OidDoServidor = "1.3.6.1.5.5.7.3.1";
	private const string OidDoNomeAmigavel = "1.2.840.113549.1.9.20";

	private CertificadosDoTeste(X509Certificate2 autoridade, X509Certificate2 servidor, string senhaDoTruststore)
	{
		Autoridade = autoridade;
		Servidor = servidor;
		SenhaDoTruststore = senhaDoTruststore;
	}

	/// <summary>A autoridade, com a chave dela.</summary>
	internal X509Certificate2 Autoridade { get; }

	/// <summary>O certificado do servidor, assinado pela autoridade, com a chave dele.</summary>
	internal X509Certificate2 Servidor { get; }

	/// <summary>A senha do truststore do Java, nova a cada execução.</summary>
	internal string SenhaDoTruststore { get; }

	/// <summary>
	/// Gera a autoridade e o certificado do servidor com os nomes dados (DNS ou IP), válido de <paramref name="agora"/> a um
	/// dia depois.
	/// </summary>
	internal static CertificadosDoTeste Gerar(IEnumerable<string> nomes, DateTimeOffset agora)
	{
		ArgumentNullException.ThrowIfNull(nomes);

		var inicio = agora.AddMinutes(-5);
		var fim = agora.AddDays(1);

		using var chaveDaAutoridade = RSA.Create(2048);
		var pedidoDaAutoridade = new CertificateRequest(
			$"CN=XUnitAssured test CA {Guid.NewGuid():N}", chaveDaAutoridade, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
		pedidoDaAutoridade.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, true, 0, true));
		pedidoDaAutoridade.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
		pedidoDaAutoridade.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(pedidoDaAutoridade.PublicKey, false));
		var autoridade = pedidoDaAutoridade.CreateSelfSigned(inicio, fim);

		using var chaveDoServidor = RSA.Create(2048);
		var pedidoDoServidor = new CertificateRequest("CN=camunda", chaveDoServidor, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
		pedidoDoServidor.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
		pedidoDoServidor.CertificateExtensions.Add(
			new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, true));
		pedidoDoServidor.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([new Oid(OidDoServidor)], false));
		pedidoDoServidor.CertificateExtensions.Add(X509AuthorityKeyIdentifierExtension.CreateFromCertificate(autoridade, true, false));
		var alternativos = new SubjectAlternativeNameBuilder();
		foreach (var nome in nomes.Distinct(StringComparer.OrdinalIgnoreCase))
		{
			if (IPAddress.TryParse(nome, out var ip))
				alternativos.AddIpAddress(ip);
			else
				alternativos.AddDnsName(nome);
		}
		pedidoDoServidor.CertificateExtensions.Add(alternativos.Build());

		var serie = RandomNumberGenerator.GetBytes(16);
		serie[0] &= 0x7F;
		using var assinado = pedidoDoServidor.Create(autoridade, inicio, fim, serie);
		var servidor = assinado.CopyWithPrivateKey(chaveDoServidor);

		return new CertificadosDoTeste(autoridade, servidor, Convert.ToHexString(RandomNumberGenerator.GetBytes(16)));
	}

	/// <summary>
	/// Os arquivos, pelo nome: a autoridade e o servidor em PEM (o do servidor com a cadeia até a autoridade), a chave do
	/// servidor em PKCS#8, e o truststore PKCS12 do Java com a autoridade como confiável.
	/// </summary>
	internal IReadOnlyDictionary<string, byte[]> Arquivos() => new Dictionary<string, byte[]>(StringComparer.Ordinal)
	{
		[ArquivoDaAutoridade] = Encoding.ASCII.GetBytes(Autoridade.ExportCertificatePem() + "\n"),
		[ArquivoDoServidor] = Encoding.ASCII.GetBytes(Servidor.ExportCertificatePem() + "\n" + Autoridade.ExportCertificatePem() + "\n"),
		[ArquivoDaChave] = Encoding.ASCII.GetBytes(Servidor.GetRSAPrivateKey()!.ExportPkcs8PrivateKeyPem() + "\n"),
		[ArquivoDoTruststore] = TruststoreDoJava()
	};

	/// <summary>Grava os arquivos na pasta, que tem de existir.</summary>
	internal void Gravar(string pasta)
	{
		foreach (var (nome, conteudo) in Arquivos())
			File.WriteAllBytes(Path.Combine(pasta, nome), conteudo);
	}

	// O PKCS12 que o Java lê como truststore: a autoridade sem chave, com o atributo de confiável e um nome.
	private byte[] TruststoreDoJava()
	{
		var conteudo = new Pkcs12SafeContents();
		var bolsa = conteudo.AddCertificate(Autoridade);
		bolsa.Attributes.Add(new Pkcs9AttributeObject(new Oid(OidDoConfiavelNoJava), Oid(OidDeQualquerUso)));
		bolsa.Attributes.Add(new Pkcs9AttributeObject(new Oid(OidDoNomeAmigavel), NomeBmp("xunitassured-ca")));

		var construtor = new Pkcs12Builder();
		construtor.AddSafeContentsEncrypted(
			conteudo, SenhaDoTruststore, new PbeParameters(PbeEncryptionAlgorithm.Aes256Cbc, HashAlgorithmName.SHA256, 2048));
		construtor.SealWithMac(SenhaDoTruststore, HashAlgorithmName.SHA256, 2048);
		return construtor.Encode();
	}

	private static byte[] Oid(string valor)
	{
		var escritor = new AsnWriter(AsnEncodingRules.DER);
		escritor.WriteObjectIdentifier(valor);
		return escritor.Encode();
	}

	private static byte[] NomeBmp(string valor)
	{
		var escritor = new AsnWriter(AsnEncodingRules.DER);
		escritor.WriteCharacterString(UniversalTagNumber.BMPString, valor);
		return escritor.Encode();
	}

	public void Dispose()
	{
		Autoridade.Dispose();
		Servidor.Dispose();
	}
}
