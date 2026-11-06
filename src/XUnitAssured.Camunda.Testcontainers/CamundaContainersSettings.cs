using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace XUnitAssured.Camunda.Testcontainers;

/// <summary>
/// A seção <c>camunda</c> do <c>testsettings.json</c> com a parte que só este pacote lê: os containers que o
/// <see cref="CamundaFixture"/> sobe ao lado do Camunda.
/// </summary>
public class CamundaSectionWithContainers : CamundaSettings
{
	/// <summary>
	/// Os workers do cliente e as dependências deles, que o fixture sobe no Docker com o Camunda. Nulo: o fixture usa o
	/// cluster do <see cref="CamundaSettings.RestAddress"/>.
	/// </summary>
	[JsonPropertyName("containers")]
	public CamundaContainersSettings? Containers { get; set; }
}

/// <summary>
/// O que o <see cref="CamundaFixture"/> sobe no Docker além do Camunda: o modo de autenticação e a lista dos containers.
/// </summary>
/// <remarks>
/// A lista é escrita uma vez a partir do <c>docker-compose</c> do sistema testado: o Testcontainers do .NET não lê compose.
/// Todos os containers ficam numa rede só, com o Camunda nela pelo nome <c>camunda</c>.
/// </remarks>
public class CamundaContainersSettings
{
	/// <summary>
	/// Como o Camunda do container autentica: <c>none</c> (a API aberta) ou <c>basic</c>, o padrão. O <c>oauth</c> vem na
	/// 6.40.0.
	/// </summary>
	/// <remarks>O cliente do próprio teste fala com o Camunda no mesmo modo e com a mesma credencial que os workers recebem.</remarks>
	[JsonPropertyName("authentication")]
	public string? Authentication { get; set; }

	/// <summary>Os containers, na ordem em que estão escritos; o <see cref="CamundaServiceContainer.DependsOn"/> muda a ordem.</summary>
	[JsonPropertyName("services")]
	public List<CamundaServiceContainer> Services { get; set; } = [];
}

/// <summary>
/// Um container da lista: um worker do cliente, ou uma dependência dele (uma API, um broker, um banco).
/// </summary>
public class CamundaServiceContainer
{
	/// <summary>O nome dele na rede, como os outros o alcançam. <c>camunda</c> é reservado.</summary>
	[JsonPropertyName("name")]
	public string? Name { get; set; }

	/// <summary>A imagem, que o Testcontainers puxa do registro ou acha no Docker local.</summary>
	[JsonPropertyName("image")]
	public string? Image { get; set; }

	/// <summary>
	/// As variáveis de ambiente, além das que o fixture entrega (o endereço e a autenticação do Camunda).
	/// </summary>
	/// <remarks>
	/// Aceitam os marcadores <c>${camunda.restAddress}</c>, <c>${camunda.grpcAddress}</c>, <c>${camunda.username}</c>,
	/// <c>${camunda.password}</c>, <c>${&lt;nome&gt;.host}</c> e <c>${&lt;nome&gt;.port:&lt;porta&gt;}</c>, para um worker que lê
	/// outros nomes. Uma variável escrita aqui ganha da que o fixture entrega com o mesmo nome.
	/// </remarks>
	[JsonPropertyName("environment")]
	public Dictionary<string, string> Environment { get; set; } = [];

	/// <summary>As portas do container que o teste alcança do host, numa porta livre escolhida na hora.</summary>
	[JsonPropertyName("ports")]
	public List<int> Ports { get; set; } = [];

	/// <summary>Quando ele está pronto. Nulo: assim que o container sobe.</summary>
	[JsonPropertyName("wait")]
	public CamundaContainerWait? Wait { get; set; }

	/// <summary>Os nomes dos containers que sobem antes dele, e prontos. Um ciclo é erro de configuração.</summary>
	[JsonPropertyName("dependsOn")]
	public List<string> DependsOn { get; set; } = [];
}

/// <summary>
/// A espera até um container ficar pronto: uma porta aberta, um caminho HTTP que responde com sucesso, ou uma linha do log.
/// </summary>
public class CamundaContainerWait
{
	/// <summary>A porta dentro do container; com <see cref="Path"/>, a porta do pedido HTTP.</summary>
	[JsonPropertyName("port")]
	public int? Port { get; set; }

	/// <summary>O caminho HTTP que tem de responder com sucesso, na <see cref="Port"/>.</summary>
	[JsonPropertyName("path")]
	public string? Path { get; set; }

	/// <summary>Um trecho do log que diz que ele está pronto, comparado como texto.</summary>
	[JsonPropertyName("log")]
	public string? Log { get; set; }
}
