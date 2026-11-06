using System;
using System.IO;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace XUnitAssured.Camunda.Client;

/// <summary>
/// A confiança numa autoridade só: o certificado do servidor vale quando a cadeia dele chega a essa autoridade, e o nome
/// confere. As autoridades do sistema não entram, e nada é aceito por padrão.
/// </summary>
internal static class ConfiancaNaAutoridade
{
	/// <summary>A autoridade lida do arquivo PEM.</summary>
	internal static X509Certificate2 Ler(string caminhoPem) =>
		X509Certificate2.CreateFromPem(File.ReadAllText(caminhoPem ?? throw new ArgumentNullException(nameof(caminhoPem))));

	/// <summary>A validação do TLS que só aceita a cadeia até esta autoridade.</summary>
	internal static RemoteCertificateValidationCallback Callback(X509Certificate2 autoridade)
	{
		ArgumentNullException.ThrowIfNull(autoridade);
		return (_, certificado, _, erros) => certificado is not null && Valida(new X509Certificate2(certificado), erros, autoridade);
	}

	/// <summary>
	/// Se o certificado do servidor vale: sem certificado ou com o nome errado, não; senão, a cadeia tem de fechar na autoridade,
	/// e não numa autoridade do sistema.
	/// </summary>
	internal static bool Valida(X509Certificate2 servidor, SslPolicyErrors erros, X509Certificate2 autoridade)
	{
		ArgumentNullException.ThrowIfNull(servidor);
		ArgumentNullException.ThrowIfNull(autoridade);

		// O nome errado e a falta do certificado não se resolvem pela autoridade; só a cadeia, que a do sistema não reconhece.
		if ((erros & (SslPolicyErrors.RemoteCertificateNameMismatch | SslPolicyErrors.RemoteCertificateNotAvailable)) != 0)
			return false;

		using var cadeia = new X509Chain();
		cadeia.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
		cadeia.ChainPolicy.CustomTrustStore.Add(autoridade);
		// A autoridade de teste não publica revogação.
		cadeia.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
		if (!cadeia.Build(servidor))
			return false;

		var raiz = cadeia.ChainElements[^1].Certificate;
		return raiz.RawDataMemory.Span.SequenceEqual(autoridade.RawDataMemory.Span);
	}
}
