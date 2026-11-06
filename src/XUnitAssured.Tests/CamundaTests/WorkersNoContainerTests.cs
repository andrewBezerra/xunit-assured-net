using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

using XUnitAssured.Camunda;
using XUnitAssured.Camunda.Testcontainers;

namespace XUnitAssured.Tests.CamundaTests;

/// <summary>
/// Os workers do cliente no Testcontainers, sem Docker: um criador de containers falso guarda o que o fixture pediu (a rede,
/// o Camunda e cada container da lista, na ordem), e o teste confere a ordem, as variáveis e a escolha do lugar.
/// </summary>
[Trait("Category", "Camunda")]
[Trait("Component", "Testcontainers")]
public class WorkersNoContainerTests
{
	// ---------- A lista lida do testsettings.json ----------

	[Fact(DisplayName = "The containers section is read from testsettings.json: name, image, environment, ports, wait and dependsOn")]
	public void Section_Is_Read_From_The_Settings_File()
	{
		var secao = Secao("""
			{
			  "containers": {
			    "authentication": "basic",
			    "services": [
			      { "name": "postgres", "image": "postgres:16", "environment": { "POSTGRES_PASSWORD": "teste" },
			        "ports": [5432], "wait": { "port": 5432 } },
			      { "name": "worker", "image": "banco/worker:1", "dependsOn": ["postgres"],
			        "environment": { "Camunda__Url": "${camunda.restAddress}" }, "wait": { "log": "worker pronto" } }
			    ]
			  }
			}
			""");

		var containers = secao.Containers.ShouldNotBeNull();
		containers.Authentication.ShouldBe("basic");
		containers.Services.Select(s => s.Name).ShouldBe(["postgres", "worker"]);
		var postgres = containers.Services[0];
		postgres.Image.ShouldBe("postgres:16");
		postgres.Environment["POSTGRES_PASSWORD"].ShouldBe("teste");
		postgres.Ports.ShouldBe([5432]);
		postgres.Wait.ShouldNotBeNull().Port.ShouldBe(5432);
		var worker = containers.Services[1];
		worker.DependsOn.ShouldBe(["postgres"]);
		worker.Wait.ShouldNotBeNull().Log.ShouldBe("worker pronto");
	}

	// ---------- A ordem do dependsOn ----------

	[Fact(DisplayName = "The containers start in dependsOn order, after the Camunda, keeping the declared order otherwise")]
	public async Task Containers_Start_In_DependsOn_Order()
	{
		var criador = new CriadorFalso();
		await using var fixture = Fixture(criador,
			Servico("worker", dependeDe: ["api", "kafka"]),
			Servico("api", dependeDe: ["postgres"]),
			Servico("kafka"),
			Servico("postgres"));

		await fixture.InitializeAsync();

		criador.Subidos.Select(d => d.Nome).ShouldBe(["camunda", "kafka", "postgres", "api", "worker"]);
		criador.Redes.ShouldBe(1);
		criador.Subidos.ShouldAllBe(d => d.Rede == criador.NomeDaRede);
	}

	[Fact(DisplayName = "A cycle in dependsOn is a configuration error that names the cycle")]
	public void Cycle_In_DependsOn_Is_An_Error()
	{
		var erro = Should.Throw<InvalidOperationException>(() => Fixture(new CriadorFalso(),
			Servico("a", dependeDe: ["b"]),
			Servico("b", dependeDe: ["c"]),
			Servico("c", dependeDe: ["a"])));

		erro.Message.ShouldContain("cycle");
		erro.Message.ShouldContain("a → b → c → a");
	}

	[Theory(DisplayName = "A malformed list is a configuration error that says what is wrong")]
	[InlineData("dependencia", "'worker' depends on 'fila'")]
	[InlineData("repetido", "'api' appears twice")]
	[InlineData("reservado", "'camunda' is the Camunda's own name")]
	[InlineData("sem-imagem", "'api' has no image")]
	[InlineData("sem-nome", "has no name")]
	public void Malformed_List_Is_An_Error(string caso, string mensagem)
	{
		CamundaServiceContainer[] lista = caso switch
		{
			"dependencia" => [Servico("worker", dependeDe: ["fila"])],
			"repetido" => [Servico("api"), Servico("API")],
			"reservado" => [Servico("camunda")],
			"sem-imagem" => [new CamundaServiceContainer { Name = "api" }],
			_ => [new CamundaServiceContainer { Image = "x:1" }]
		};

		var erro = Should.Throw<InvalidOperationException>(() => Fixture(new CriadorFalso(), lista));

		erro.Message.ShouldContain(mensagem);
	}

	// ---------- As variáveis e os placeholders ----------

	[Fact(DisplayName = "Every container gets the Camunda's address on the network, as the SDK variables")]
	public async Task Workers_Get_The_Camunda_Address()
	{
		var criador = new CriadorFalso();
		await using var fixture = Fixture(criador, Servico("worker"));

		await fixture.InitializeAsync();

		var worker = criador.Definicao("worker");
		worker.Variaveis["CAMUNDA_REST_ADDRESS"].ShouldBe("https://camunda:8080");
		worker.Variaveis["ZEEBE_REST_ADDRESS"].ShouldBe("https://camunda:8080");
		worker.Variaveis["ZEEBE_GRPC_ADDRESS"].ShouldBe("https://camunda:26500");
	}

	[Fact(DisplayName = "Placeholders in a container's environment take the Camunda's and the other containers' values")]
	public async Task Placeholders_Are_Resolved()
	{
		var criador = new CriadorFalso();
		var worker = Servico("worker", dependeDe: ["postgres"]);
		worker.Environment["Camunda__Url"] = "${camunda.restAddress}";
		worker.Environment["Camunda__Grpc"] = "${camunda.grpcAddress}";
		worker.Environment["Camunda__User"] = "${camunda.username}";
		worker.Environment["Camunda__Password"] = "${camunda.password}";
		worker.Environment["ConnectionStrings__Banco"] = "Host=${postgres.host};Port=${postgres.port:5432}";
		await using var fixture = Fixture(criador, gerarSenha: () => "senha-1", Servico("postgres"), worker);

		await fixture.InitializeAsync();

		var variaveis = criador.Definicao("worker").Variaveis;
		variaveis["Camunda__Url"].ShouldBe("https://camunda:8080");
		variaveis["Camunda__Grpc"].ShouldBe("https://camunda:26500");
		variaveis["Camunda__User"].ShouldBe("xunit");
		variaveis["Camunda__Password"].ShouldBe("senha-1");
		variaveis["ConnectionStrings__Banco"].ShouldBe("Host=postgres;Port=5432");
	}

	[Theory(DisplayName = "An unknown placeholder is a configuration error that names it and the container")]
	[InlineData("${camunda.endereco}", "${camunda.endereco}")]
	[InlineData("${fila.host}", "${fila.host}")]
	[InlineData("${postgres.port:abc}", "${postgres.port:abc}")]
	public void Unknown_Placeholder_Is_An_Error(string valor, string citado)
	{
		var worker = Servico("worker");
		worker.Environment["X"] = valor;

		var erro = Should.Throw<InvalidOperationException>(() => Fixture(new CriadorFalso(), Servico("postgres"), worker));

		erro.Message.ShouldContain(citado);
		erro.Message.ShouldContain("'worker'");
	}

	// ---------- A escolha do lugar ----------

	[Fact(DisplayName = "With the containers list, the chains talk to the Camunda container on its mapped port")]
	public async Task With_The_List_The_Client_Uses_The_Container()
	{
		var criador = new CriadorFalso();
		await using var fixture = Fixture(criador, gerarSenha: () => "senha-1", Servico("worker"));

		await fixture.InitializeAsync();

		fixture.Settings.RestAddress.ShouldBe($"https://{CriadorFalso.Host}:{CriadorFalso.PortaNoHost(8080)}");
		fixture.Settings.AssertionTimeoutSeconds.ShouldBe(7);
	}

	[Fact(DisplayName = "Without the list but with a restAddress, the fixture uses that cluster and starts nothing")]
	public async Task Without_The_List_The_Fixture_Uses_The_Cluster()
	{
		var criador = new CriadorFalso();
		var lida = new CamundaSectionWithContainers { RestAddress = "http://cluster:8080" };
		await using var fixture = new CamundaFixture(lida, _ => null, criador, () => "x");

		await fixture.InitializeAsync();

		fixture.Settings.RestAddress.ShouldBe("http://cluster:8080");
		criador.Subidos.ShouldBeEmpty();
		criador.Redes.ShouldBe(0);
	}

	[Fact(DisplayName = "Without the list and without an address, the fixture fails with the message that says what to add")]
	public void Without_Anything_The_Fixture_Fails()
	{
		var erro = Should.Throw<InvalidOperationException>(() => new CamundaFixture(null, _ => null, new CriadorFalso(), () => "x"));

		erro.Message.ShouldContain("no cluster is configured");
		erro.Message.ShouldContain("containers");
	}

	[Fact(DisplayName = "A container that fails to start stops the ones already started and the network")]
	public async Task Failed_Start_Cleans_Up()
	{
		var criador = new CriadorFalso { FalharEm = "worker" };
		await using var fixture = Fixture(criador, Servico("postgres"), Servico("worker", dependeDe: ["postgres"]));

		await Should.ThrowAsync<InvalidOperationException>(fixture.InitializeAsync);

		criador.Descartados.ShouldBe(["postgres", "camunda", "rede"]);
	}

	[Fact(DisplayName = "Disposing the fixture stops the containers in reverse order, then the network")]
	public async Task Dispose_Stops_In_Reverse_Order()
	{
		var criador = new CriadorFalso();
		var fixture = Fixture(criador, Servico("postgres"), Servico("worker", dependeDe: ["postgres"]));
		await fixture.InitializeAsync();

		await fixture.DisposeAsync();

		criador.Descartados.ShouldBe(["worker", "postgres", "camunda", "rede"]);
	}

	// ---------- Sem autenticação (5c) ----------

	[Fact(DisplayName = "Authentication none: the Camunda opens its API, the worker gets NONE, and the client sends no credentials")]
	public async Task None_Opens_The_Api()
	{
		var criador = new CriadorFalso();
		await using var fixture = Fixture(criador, autenticacao: "none", servicos: [Servico("worker")]);

		await fixture.InitializeAsync();

		var camunda = criador.Definicao("camunda");
		camunda.Variaveis["CAMUNDA_SECURITY_AUTHENTICATION_UNPROTECTEDAPI"].ShouldBe("true");
		camunda.Variaveis.Keys.ShouldNotContain("CAMUNDA_SECURITY_INITIALIZATION_USERS_0_USERNAME");
		camunda.Basic.ShouldBeNull();
		var worker = criador.Definicao("worker");
		worker.Variaveis["CAMUNDA_AUTH_STRATEGY"].ShouldBe("NONE");
		worker.Variaveis.Keys.ShouldNotContain("CAMUNDA_BASIC_AUTH_USERNAME");
		fixture.Settings.Authentication.Type.ShouldBe("None");
		fixture.Settings.Authentication.Username.ShouldBeNull();
	}

	[Fact(DisplayName = "Authentication none: a credential placeholder is an error, since there is no credential")]
	public void None_Has_No_Credential_Placeholders()
	{
		var worker = Servico("worker");
		worker.Environment["Camunda__Password"] = "${camunda.password}";

		var erro = Should.Throw<InvalidOperationException>(() => Fixture(new CriadorFalso(), autenticacao: "none", servicos: [worker]));

		erro.Message.ShouldContain("${camunda.password}");
		erro.Message.ShouldContain("none");
	}

	// ---------- O Basic, o padrão (5d) ----------

	[Fact(DisplayName = "Without authentication, the mode is Basic: the worker and the client get the same user and password")]
	public async Task Basic_Is_The_Default()
	{
		var criador = new CriadorFalso();
		await using var fixture = Fixture(criador, gerarSenha: () => "senha-1", autenticacao: null, servicos: [Servico("worker")]);

		await fixture.InitializeAsync();

		var camunda = criador.Definicao("camunda");
		camunda.Variaveis["CAMUNDA_SECURITY_AUTHENTICATION_UNPROTECTEDAPI"].ShouldBe("false");
		camunda.Variaveis["CAMUNDA_SECURITY_INITIALIZATION_USERS_0_USERNAME"].ShouldBe("xunit");
		camunda.Variaveis["CAMUNDA_SECURITY_INITIALIZATION_USERS_0_PASSWORD"].ShouldBe("senha-1");
		camunda.Basic.ShouldBe(("xunit", "senha-1"));
		var worker = criador.Definicao("worker");
		worker.Variaveis["CAMUNDA_AUTH_STRATEGY"].ShouldBe("BASIC");
		worker.Variaveis["CAMUNDA_BASIC_AUTH_USERNAME"].ShouldBe("xunit");
		worker.Variaveis["CAMUNDA_BASIC_AUTH_PASSWORD"].ShouldBe("senha-1");
		fixture.Settings.Authentication.Type.ShouldBe("Basic");
		fixture.Settings.Authentication.Username.ShouldBe("xunit");
		fixture.Settings.Authentication.Password.ShouldBe("senha-1");
	}

	[Fact(DisplayName = "Basic: the password changes on every run")]
	public async Task Basic_Password_Changes_Every_Run()
	{
		var primeiro = new CriadorFalso();
		var segundo = new CriadorFalso();
		await using var um = new CamundaFixture(Lida(null, Servico("worker")), _ => null, primeiro);
		await using var dois = new CamundaFixture(Lida(null, Servico("worker")), _ => null, segundo);

		await um.InitializeAsync();
		await dois.InitializeAsync();

		var senhaUm = primeiro.Definicao("worker").Variaveis["CAMUNDA_BASIC_AUTH_PASSWORD"];
		senhaUm.ShouldNotBeNullOrWhiteSpace();
		senhaUm.ShouldNotBe(segundo.Definicao("worker").Variaveis["CAMUNDA_BASIC_AUTH_PASSWORD"]);
	}

	[Fact(DisplayName = "Authentication oauth is not here yet: a clear error says it comes in 6.40.0")]
	public void OAuth_Comes_In_A_Later_Version()
	{
		var erro = Should.Throw<NotSupportedException>(() => Fixture(new CriadorFalso(), autenticacao: "oauth", servicos: [Servico("worker")]));

		erro.Message.ShouldContain("6.40.0");
	}

	[Fact(DisplayName = "An unknown authentication mode is an error that lists the modes")]
	public void Unknown_Mode_Is_An_Error()
	{
		var erro = Should.Throw<InvalidOperationException>(() => Fixture(new CriadorFalso(), autenticacao: "kerberos", servicos: [Servico("worker")]));

		erro.Message.ShouldContain("kerberos");
		erro.Message.ShouldContain("none");
		erro.Message.ShouldContain("basic");
	}

	[Fact(DisplayName = "An OAuth placeholder is not here yet: a clear error says OAuth in the containers comes in 6.40.0")]
	public void OAuth_Placeholder_Comes_In_A_Later_Version()
	{
		var worker = Servico("worker");
		worker.Environment["Camunda__ClientId"] = "${camunda.clientId}";

		var erro = Should.Throw<NotSupportedException>(() => Fixture(new CriadorFalso(), worker));

		erro.Message.ShouldContain("${camunda.clientId}");
		erro.Message.ShouldContain("6.40.0");
	}

	[Fact(DisplayName = "UsesContainers says whether the test runs on the fixture's containers or on a cluster")]
	public void UsesContainers_Tells_Where_The_Test_Runs()
	{
		Fixture(new CriadorFalso(), Servico("worker")).UsesContainers.ShouldBeTrue();
		new CamundaFixture(new CamundaSectionWithContainers { RestAddress = "http://cluster:8080" }, _ => null, new CriadorFalso())
			.UsesContainers.ShouldBeFalse();
	}

	[Fact(DisplayName = "Disposing through IAsyncDisposable stops the containers too, and a second dispose does nothing")]
	public async Task IAsyncDisposable_Stops_The_Containers_Once()
	{
		var criador = new CriadorFalso();
		var fixture = Fixture(criador, Servico("worker"));
		await fixture.InitializeAsync();

		await ((IAsyncDisposable)fixture).DisposeAsync();
		await fixture.DisposeAsync();

		criador.Descartados.ShouldBe(["worker", "camunda", "rede"]);
	}

	// ---------- A espera no Docker ----------

	[Fact(DisplayName = "In Docker, a path waits for the HTTP request, with Basic when the definition asks for it")]
	public void Docker_Wait_For_A_Path()
	{
		var comBasic = Definicao(new CamundaContainerWait { Port = 8080, Path = "/v2/topology" }, ("xunit", "senha"));
		var semBasic = Definicao(new CamundaContainerWait { Path = "/health" }, basic: null);

		CriadorNoDocker.Espera(comBasic).ShouldNotBeNull();
		CriadorNoDocker.Espera(semBasic).ShouldNotBeNull();
	}

	[Fact(DisplayName = "In Docker, a log line or a port is a wait too, and no wait is none")]
	public void Docker_Wait_For_A_Log_Line_Or_A_Port()
	{
		CriadorNoDocker.Espera(Definicao(new CamundaContainerWait { Log = "pronto (v1.2)" }, basic: null)).ShouldNotBeNull();
		CriadorNoDocker.Espera(Definicao(new CamundaContainerWait { Port = 5432 }, basic: null)).ShouldNotBeNull();
		CriadorNoDocker.Espera(Definicao(new CamundaContainerWait(), basic: null)).ShouldBeNull();
		CriadorNoDocker.Espera(Definicao(espera: null, basic: null)).ShouldBeNull();
	}

	private static DefinicaoDeContainer Definicao(CamundaContainerWait? espera, (string, string)? basic) =>
		new("worker", "exemplo/worker:1", new Dictionary<string, string>(), [8080], espera, basic, new Dictionary<string, byte[]>(), null);

	// ---------- Apoio ----------

	// As mesmas opções com que o TestSettingsSection lê o arquivo.
	private static readonly JsonSerializerOptions ComoOArquivo = new()
	{
		PropertyNameCaseInsensitive = true,
		ReadCommentHandling = JsonCommentHandling.Skip,
		AllowTrailingCommas = true
	};

	private static CamundaSectionWithContainers Secao(string json) =>
		JsonSerializer.Deserialize<CamundaSectionWithContainers>(json, ComoOArquivo).ShouldNotBeNull();

	private static CamundaServiceContainer Servico(string nome, string[]? dependeDe = null) => new()
	{
		Name = nome,
		Image = $"exemplo/{nome.ToLowerInvariant()}:1",
		DependsOn = [.. dependeDe ?? []]
	};

	private static CamundaSectionWithContainers Lida(string? autenticacao, params CamundaServiceContainer[] servicos) => new()
	{
		AssertionTimeoutSeconds = 7,
		Containers = new CamundaContainersSettings { Authentication = autenticacao, Services = [.. servicos] }
	};

	private static CamundaFixture Fixture(CriadorFalso criador, params CamundaServiceContainer[] servicos) =>
		Fixture(criador, gerarSenha: null, autenticacao: "basic", servicos: servicos);

	private static CamundaFixture Fixture(CriadorFalso criador, Func<string>? gerarSenha, params CamundaServiceContainer[] servicos) =>
		Fixture(criador, gerarSenha, autenticacao: "basic", servicos: servicos);

	private static CamundaFixture Fixture(CriadorFalso criador, string? autenticacao, params CamundaServiceContainer[] servicos) =>
		Fixture(criador, gerarSenha: null, autenticacao, servicos);

	private static CamundaFixture Fixture(CriadorFalso criador, Func<string>? gerarSenha, string? autenticacao, params CamundaServiceContainer[] servicos) =>
		gerarSenha is null
			? new CamundaFixture(Lida(autenticacao, servicos), _ => null, criador)
			: new CamundaFixture(Lida(autenticacao, servicos), _ => null, criador, gerarSenha);

	/// <summary>O que o criador falso recebeu de um container.</summary>
	internal sealed record DefinicaoNoFalso(
		string Nome, string Rede, IReadOnlyDictionary<string, string> Variaveis, (string, string)? Basic,
		IReadOnlyDictionary<string, byte[]> Arquivos, System.Security.Cryptography.X509Certificates.X509Certificate2? Autoridade);

	/// <summary>O Docker de mentira: guarda o que foi pedido, na ordem, e o que foi descartado.</summary>
	internal sealed class CriadorFalso : ICriadorDeContainers
	{
		public const string Host = "127.0.0.1";

		public static int PortaNoHost(int portaInterna) => 40000 + portaInterna;

		public string NomeDaRede { get; } = "rede-do-teste";

		public int Redes { get; private set; }

		public string? FalharEm { get; init; }

		public List<DefinicaoNoFalso> Subidos { get; } = [];

		public List<string> Descartados { get; } = [];

		public DefinicaoNoFalso Definicao(string nome) => Subidos.Single(d => d.Nome == nome);

		public Task<IRedeDoTeste> CriarRedeAsync(CancellationToken cancellationToken)
		{
			Redes++;
			return Task.FromResult<IRedeDoTeste>(new Rede(this));
		}

		public Task<IContainerDoTeste> SubirAsync(DefinicaoDeContainer definicao, IRedeDoTeste rede, CancellationToken cancellationToken)
		{
			if (definicao.Nome == FalharEm)
				throw new InvalidOperationException($"O container '{definicao.Nome}' não subiu.");

			Subidos.Add(new DefinicaoNoFalso(definicao.Nome, rede.Nome, definicao.Variaveis, definicao.Basic, definicao.Arquivos, definicao.Autoridade));
			return Task.FromResult<IContainerDoTeste>(new Container(this, definicao.Nome));
		}

		private sealed class Rede(CriadorFalso criador) : IRedeDoTeste
		{
			public string Nome => criador.NomeDaRede;

			public ValueTask DisposeAsync()
			{
				criador.Descartados.Add("rede");
				return ValueTask.CompletedTask;
			}
		}

		private sealed class Container(CriadorFalso criador, string nome) : IContainerDoTeste
		{
			public string Host => CriadorFalso.Host;

			public int PortaNoHost(int portaInterna) => CriadorFalso.PortaNoHost(portaInterna);

			public ValueTask DisposeAsync()
			{
				criador.Descartados.Add(nome);
				return ValueTask.CompletedTask;
			}
		}
	}
}
