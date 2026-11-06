using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace XUnitAssured.Camunda.Testcontainers;

/// <summary>Os modos de autenticação do Camunda no container.</summary>
internal enum ModoDeAutenticacao
{
	None,
	Basic
}

/// <summary>
/// O plano da subida, feito antes de qualquer container: o modo, a ordem do <c>dependsOn</c> e as variáveis de cada
/// container, já resolvidas. Um erro de configuração aparece aqui, ao criar o fixture, e não no meio da subida.
/// </summary>
internal static partial class MontagemDosContainers
{
	internal const string Usuario = "xunit";

	/// <summary>O nome do Camunda na rede, reservado para ele.</summary>
	internal static string NomeDoCamunda => Enderecos.NomeNaRede;

	/// <summary>Os endereços do Camunda do container, só com TLS.</summary>
	internal static EnderecosDoCamunda Enderecos => EnderecosDoCamunda.Padrao;

	/// <summary>O modo pedido; sem nada, o Basic.</summary>
	internal static ModoDeAutenticacao Modo(string? pedido) => pedido?.Trim().ToLowerInvariant() switch
	{
		null or "" or "basic" => ModoDeAutenticacao.Basic,
		"none" => ModoDeAutenticacao.None,
		"oauth" => throw new NotSupportedException(
			"camunda.containers.authentication \"oauth\" comes in XUnitAssured.Camunda.Testcontainers 6.40.0, with a Keycloak the " +
			"fixture starts and configures. Use \"basic\" (the default) or \"none\" for now."),
		_ => throw new InvalidOperationException(
			$"camunda.containers.authentication \"{pedido}\" is not a mode: use \"none\" or \"basic\" (the default).")
	};

	/// <summary>
	/// Os containers na ordem em que sobem: cada um depois dos de que depende, e, entre os livres, na ordem escrita.
	/// </summary>
	/// <exception cref="InvalidOperationException">Um nome falta, repete, é o do Camunda, ou o <c>dependsOn</c> tem um ciclo.</exception>
	internal static IReadOnlyList<CamundaServiceContainer> Ordenar(IReadOnlyList<CamundaServiceContainer> servicos)
	{
		ArgumentNullException.ThrowIfNull(servicos);

		var porNome = new Dictionary<string, CamundaServiceContainer>(StringComparer.OrdinalIgnoreCase);
		for (var i = 0; i < servicos.Count; i++)
		{
			var servico = servicos[i];
			if (string.IsNullOrWhiteSpace(servico.Name))
				throw Configuracao($"The container #{i + 1} in camunda.containers.services has no name.");
			if (string.Equals(servico.Name, NomeDoCamunda, StringComparison.OrdinalIgnoreCase))
				throw Configuracao($"'{NomeDoCamunda}' is the Camunda's own name on the network: give the container another name.");
			if (string.IsNullOrWhiteSpace(servico.Image))
				throw Configuracao($"The container '{servico.Name}' has no image.");
			if (!porNome.TryAdd(servico.Name, servico))
				throw Configuracao($"The container name '{porNome[servico.Name].Name}' appears twice in camunda.containers.services.");
		}

		foreach (var servico in servicos)
			foreach (var dependencia in servico.DependsOn)
				if (!porNome.ContainsKey(dependencia) && !EhOCamunda(dependencia))
					throw Configuracao($"The container '{servico.Name}' depends on '{dependencia}', which is not in camunda.containers.services.");

		// Cada volta sobe o primeiro, na ordem escrita, cujas dependências já subiram; sem nenhum assim, há um ciclo.
		var ordem = new List<CamundaServiceContainer>();
		var prontos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		var restantes = servicos.ToList();
		while (restantes.Count > 0)
		{
			var proximo = restantes.FirstOrDefault(s => s.DependsOn.All(d => prontos.Contains(d) || EhOCamunda(d)));
			if (proximo is null)
				throw Configuracao($"camunda.containers.services has a dependsOn cycle: {string.Join(" → ", Ciclo(restantes[0], porNome, prontos))}.");
			restantes.Remove(proximo);
			prontos.Add(proximo.Name!);
			ordem.Add(proximo);
		}

		return ordem;
	}

	private static bool EhOCamunda(string nome) => string.Equals(nome, NomeDoCamunda, StringComparison.OrdinalIgnoreCase);

	// O ciclo por inteiro, seguindo as dependências de um container que não pôde subir até voltar a um já visto.
	private static IEnumerable<string> Ciclo(
		CamundaServiceContainer inicio, Dictionary<string, CamundaServiceContainer> porNome, HashSet<string> prontos)
	{
		var caminho = new List<string>();
		var atual = inicio;
		while (true)
		{
			var visto = caminho.FindIndex(n => string.Equals(n, atual.Name, StringComparison.OrdinalIgnoreCase));
			if (visto >= 0)
				return caminho.Skip(visto).Append(caminho[visto]);
			caminho.Add(atual.Name!);
			// Um container fora do ciclo pode depender de um dentro: segue a primeira dependência que também não subiu.
			atual = porNome[atual.DependsOn.First(d => !EhOCamunda(d) && !prontos.Contains(d))];
		}
	}

	/// <summary>
	/// As variáveis do Camunda no container, no modo pedido: o TLS no REST e no gateway gRPC, com o certificado do servidor
	/// que o fixture copia para o container; o armazenamento em H2 na memória com a busca gravada na hora; e o usuário do
	/// Basic, ou a API aberta.
	/// </summary>
	/// <remarks>
	/// O REST é o servidor web do Spring Boot (<c>server.ssl.*</c>, com o certificado e a chave em PEM); o gRPC, o
	/// <c>camunda.api.grpc.ssl.*</c> da referência de propriedades do Camunda 8.10.
	/// </remarks>
	internal static IReadOnlyDictionary<string, string> VariaveisDoCamunda(ModoDeAutenticacao modo, string senha)
	{
		var certificado = EnderecosDoCamunda.NoContainer(CertificadosDoTeste.ArquivoDoServidor);
		var chave = EnderecosDoCamunda.NoContainer(CertificadosDoTeste.ArquivoDaChave);
		var variaveis = new Dictionary<string, string>(StringComparer.Ordinal)
		{
			["SERVER_SSL_ENABLED"] = "true",
			["SERVER_SSL_CERTIFICATE"] = certificado,
			["SERVER_SSL_CERTIFICATE_PRIVATE_KEY"] = chave,
			["CAMUNDA_API_GRPC_SSL_ENABLED"] = "true",
			["CAMUNDA_API_GRPC_SSL_CERTIFICATE"] = certificado,
			["CAMUNDA_API_GRPC_SSL_CERTIFICATEPRIVATEKEY"] = chave,
			["CAMUNDA_DATA_SECONDARYSTORAGE_TYPE"] = "rdbms",
			["CAMUNDA_DATA_SECONDARYSTORAGE_RDBMS_URL"] = "jdbc:h2:mem:xunit;DB_CLOSE_DELAY=-1;MODE=PostgreSQL",
			["CAMUNDA_DATA_SECONDARYSTORAGE_RDBMS_USERNAME"] = "sa",
			["CAMUNDA_DATA_SECONDARYSTORAGE_RDBMS_FLUSHINTERVAL"] = "PT0S",
			// Sem isto, o usuário criado aqui não tem permissão nenhuma, e o primeiro deploy volta FORBIDDEN.
			["CAMUNDA_SECURITY_AUTHORIZATIONS_ENABLED"] = "false"
		};
		if (modo == ModoDeAutenticacao.None)
		{
			variaveis["CAMUNDA_SECURITY_AUTHENTICATION_UNPROTECTEDAPI"] = "true";
			return variaveis;
		}

		variaveis["CAMUNDA_SECURITY_AUTHENTICATION_UNPROTECTEDAPI"] = "false";
		variaveis["CAMUNDA_SECURITY_INITIALIZATION_USERS_0_USERNAME"] = Usuario;
		variaveis["CAMUNDA_SECURITY_INITIALIZATION_USERS_0_PASSWORD"] = senha;
		variaveis["CAMUNDA_SECURITY_INITIALIZATION_USERS_0_NAME"] = Usuario;
		variaveis["CAMUNDA_SECURITY_INITIALIZATION_USERS_0_EMAIL"] = "xunit@localhost";
		return variaveis;
	}

	/// <summary>
	/// As variáveis de um container da lista: as do SDK, com o endereço do Camunda na rede e a autenticação do modo; as da
	/// confiança na autoridade de teste, para as tecnologias comuns; e as que ele declarou, com os marcadores resolvidos. As
	/// declaradas ganham, menos o <c>JAVA_TOOL_OPTIONS</c>, que junta o truststore ao que foi declarado.
	/// </summary>
	/// <exception cref="InvalidOperationException">Um marcador que não existe, ou a credencial pedida no modo sem credencial.</exception>
	internal static IReadOnlyDictionary<string, string> VariaveisDoServico(
		CamundaServiceContainer servico, ModoDeAutenticacao modo, string senha, string senhaDoTruststore, IReadOnlyCollection<string> nomes)
	{
		var autoridade = EnderecosDoCamunda.NoContainer(CertificadosDoTeste.ArquivoDaAutoridade);
		var truststore = EnderecosDoCamunda.NoContainer(CertificadosDoTeste.ArquivoDoTruststore);
		var java = $"-Djavax.net.ssl.trustStore={truststore} -Djavax.net.ssl.trustStorePassword={senhaDoTruststore} -Djavax.net.ssl.trustStoreType=PKCS12";
		var variaveis = new Dictionary<string, string>(StringComparer.Ordinal)
		{
			["CAMUNDA_REST_ADDRESS"] = Enderecos.RestNaRede,
			["ZEEBE_REST_ADDRESS"] = Enderecos.RestNaRede,
			["ZEEBE_GRPC_ADDRESS"] = Enderecos.GrpcNaRede,
			["CAMUNDA_AUTH_STRATEGY"] = modo == ModoDeAutenticacao.Basic ? "BASIC" : "NONE",
			// A autoridade de teste, pelo nome que cada família lê: o OpenSSL (Go, .NET no Linux, curl), o Node, o Python, o
			// SDK .NET do Camunda, o cliente Java do Camunda e o Spring Zeebe; e o truststore do Java.
			["SSL_CERT_FILE"] = autoridade,
			["NODE_EXTRA_CA_CERTS"] = autoridade,
			["REQUESTS_CA_BUNDLE"] = autoridade,
			["CAMUNDA_MTLS_CA_PATH"] = autoridade,
			["ZEEBE_CA_CERTIFICATE_PATH"] = autoridade,
			["CAMUNDA_CLIENT_CACERTIFICATEPATH"] = autoridade,
			["JAVA_TOOL_OPTIONS"] = java
		};
		if (modo == ModoDeAutenticacao.Basic)
		{
			variaveis["CAMUNDA_BASIC_AUTH_USERNAME"] = Usuario;
			variaveis["CAMUNDA_BASIC_AUTH_PASSWORD"] = senha;
		}

		foreach (var (chave, valor) in servico.Environment)
		{
			var resolvido = Marcador().Replace(
				valor ?? "", m => Resolver(m.Value, m.Groups[1].Value, servico.Name!, modo, senha, senhaDoTruststore, nomes));
			variaveis[chave] = chave == "JAVA_TOOL_OPTIONS" ? $"{java} {resolvido}" : resolvido;
		}
		return variaveis;
	}

	// O valor de um marcador: os do Camunda, e o host e a porta de outro container da lista.
	private static string Resolver(
		string marcador, string chave, string servico, ModoDeAutenticacao modo, string senha, string senhaDoTruststore,
		IReadOnlyCollection<string> nomes)
	{
		switch (chave)
		{
			case "camunda.restAddress":
				return Enderecos.RestNaRede;
			case "camunda.grpcAddress":
				return Enderecos.GrpcNaRede;
			case "camunda.caCertificatePath":
				return EnderecosDoCamunda.NoContainer(CertificadosDoTeste.ArquivoDaAutoridade);
			case "camunda.trustStorePath":
				return EnderecosDoCamunda.NoContainer(CertificadosDoTeste.ArquivoDoTruststore);
			case "camunda.trustStorePassword":
				return senhaDoTruststore;
			case "camunda.username" or "camunda.password" when modo == ModoDeAutenticacao.None:
				throw Configuracao(
					$"The container '{servico}' uses {marcador}, but camunda.containers.authentication is \"none\": there is no credential.");
			case "camunda.username":
				return Usuario;
			case "camunda.password":
				return senha;
			case "camunda.tokenUrl" or "camunda.clientId" or "camunda.clientSecret" or "camunda.audience":
				throw new NotSupportedException(
					$"The container '{servico}' uses {marcador}, an OAuth value: OAuth in the containers comes in 6.40.0.");
		}

		var ponto = chave.IndexOf('.', StringComparison.Ordinal);
		var nome = ponto > 0 ? chave[..ponto] : "";
		var parte = ponto > 0 ? chave[(ponto + 1)..] : "";
		if (nomes.Contains(nome, StringComparer.OrdinalIgnoreCase))
		{
			if (parte == "host")
				return nome;
			if (parte.StartsWith("port:", StringComparison.Ordinal)
				&& int.TryParse(parte["port:".Length..], NumberStyles.None, CultureInfo.InvariantCulture, out var porta))
				return porta.ToString(CultureInfo.InvariantCulture);
		}

		throw Configuracao(
			$"The container '{servico}' uses {marcador}, which is not a placeholder: use ${{camunda.restAddress}}, " +
			"${camunda.grpcAddress}, ${camunda.username}, ${camunda.password}, ${camunda.caCertificatePath}, " +
			"${camunda.trustStorePath}, ${camunda.trustStorePassword}, ${<name>.host} or ${<name>.port:<port>}, with the name of " +
			"a container in the list.");
	}

	[GeneratedRegex(@"\$\{([^}]+)\}", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
	private static partial Regex Marcador();

	private static InvalidOperationException Configuracao(string mensagem) => new(mensagem);
}
