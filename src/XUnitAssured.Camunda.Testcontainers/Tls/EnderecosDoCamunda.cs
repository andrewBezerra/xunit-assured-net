using System;
using System.Collections.Generic;

namespace XUnitAssured.Camunda.Testcontainers;

/// <summary>
/// Onde o Camunda do container responde, num lugar só: o esquema, o nome na rede e as portas do REST e do gRPC. Os
/// endereços do fixture, dos workers e do teste saem daqui, e o esquema é sempre o com TLS.
/// </summary>
/// <param name="Esquema">O esquema dos endereços: <c>https</c>.</param>
/// <param name="NomeNaRede">O nome do Camunda na rede dos containers.</param>
/// <param name="PortaRest">A porta do REST dentro do container.</param>
/// <param name="PortaGrpc">A porta do gateway gRPC dentro do container.</param>
internal sealed record EnderecosDoCamunda(string Esquema, string NomeNaRede, int PortaRest, int PortaGrpc)
{
	/// <summary>O Camunda do fixture: só TLS, como <c>camunda</c> na rede, no 8080 e no 26500 da imagem.</summary>
	internal static EnderecosDoCamunda Padrao { get; } = new(Uri.UriSchemeHttps, "camunda", 8080, 26500);

	/// <summary>A pasta do container com o certificado, a chave e a autoridade.</summary>
	internal const string PastaDoTls = "/xunitassured/tls";

	/// <summary>O REST na rede dos containers.</summary>
	internal string RestNaRede => Endereco(NomeNaRede, PortaRest);

	/// <summary>O gateway gRPC na rede dos containers.</summary>
	internal string GrpcNaRede => Endereco(NomeNaRede, PortaGrpc);

	/// <summary>O REST como o teste o alcança: o host do Docker e a porta mapeada.</summary>
	internal string RestNoHost(string host, int porta) => Endereco(host, porta);

	/// <summary>
	/// Os nomes que o certificado do servidor leva: o da rede, e os do host, de onde o teste chega pela porta mapeada.
	/// </summary>
	internal IReadOnlyList<string> NomesDoCertificado(IEnumerable<string> outrosHosts)
	{
		var nomes = new List<string> { NomeNaRede, "localhost", "127.0.0.1", "::1" };
		nomes.AddRange(outrosHosts);
		return nomes;
	}

	/// <summary>
	/// Os hosts por onde o teste pode chegar ao Docker além do local: o do <c>TESTCONTAINERS_HOST_OVERRIDE</c> e o do
	/// <c>DOCKER_HOST</c>, quando é um endereço de rede.
	/// </summary>
	internal static IEnumerable<string> HostsDoDocker(Func<string, string?> ambiente)
	{
		ArgumentNullException.ThrowIfNull(ambiente);
		if (ambiente("TESTCONTAINERS_HOST_OVERRIDE") is { Length: > 0 } sobreposto)
			yield return sobreposto;
		if (Uri.TryCreate(ambiente("DOCKER_HOST"), UriKind.Absolute, out var docker) && docker.Scheme is "tcp" or "https" or "ssh"
			&& docker.Host.Length > 0)
			yield return docker.Host;
	}

	/// <summary>O caminho de um arquivo do TLS dentro do container.</summary>
	internal static string NoContainer(string arquivo) => $"{PastaDoTls}/{arquivo}";

	private string Endereco(string host, int porta) =>
		new UriBuilder(Esquema, host, porta).Uri.GetLeftPart(UriPartial.Authority);
}
