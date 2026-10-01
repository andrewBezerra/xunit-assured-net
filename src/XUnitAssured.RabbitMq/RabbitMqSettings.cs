using System.Text.Json.Serialization;

using XUnitAssured.Core.Configuration;

namespace XUnitAssured.RabbitMq;

/// <summary>
/// O que o pacote precisa saber para alcançar um broker AMQP.
///
/// <para>
/// Lido da seção <c>rabbitmq</c> do <c>testsettings.json</c>, ao lado de <c>http</c>, <c>kafka</c>
/// e <c>playwright</c>. Um ambiente é um arquivo próprio, <c>testsettings.{nome}.json</c>, como no
/// resto do framework.
/// </para>
///
/// <para>
/// O endereço é uma URI, e não uma lista de <c>host:porta</c>: o AMQP carrega usuário, senha e
/// virtual host no próprio endereço, e essa é a forma que o cliente oficial aceita. Quem vem do
/// pacote Kafka estranha, e a diferença é real, não cosmética.
/// </para>
/// </summary>
public class RabbitMqSettings
{
	/// <summary>
	/// A URI de conexão, por exemplo <c>amqp://convidado:convidado@localhost:5672/</c>.
	/// </summary>
	/// <remarks>
	/// O virtual host é o último segmento. Uma URI terminada em <c>/</c> aponta para o virtual
	/// host padrão; <c>/meu-vhost</c> aponta para um nomeado. Omitir a barra final não é o mesmo
	/// que informá-la, e é a confusão mais comum de quem escreve a URI à mão.
	/// </remarks>
	[JsonPropertyName("connectionUri")]
	public string ConnectionUri { get; set; } = "amqp://guest:guest@localhost:5672/";

	/// <summary>
	/// Quanto esperar por uma mensagem ao consumir, em segundos. Padrão: 30.
	/// </summary>
	[JsonPropertyName("consumeTimeoutSeconds")]
	public int ConsumeTimeoutSeconds { get; set; } = 30;

	/// <summary>
	/// Nome que aparece na aba de conexões do painel do broker, para saber de onde vem o tráfego.
	/// </summary>
	[JsonPropertyName("clientProvidedName")]
	public string ClientProvidedName { get; set; } = "XUnitAssured";

	/// <summary>
	/// Carrega a seção <c>rabbitmq</c> do <c>testsettings.json</c>, ou os padrões quando não há
	/// arquivo nem seção.
	/// </summary>
	/// <remarks>
	/// O arquivo é localizado como o resto da configuração: <c>TESTSETTINGS_PATH</c> quando
	/// definida, senão <c>testsettings.json</c> no diretório atual e nos pais, com os marcadores
	/// <c>${ENV:NOME}</c> substituídos. Não há parâmetro de ambiente aqui porque a seleção por
	/// ambiente é do localizador do Core, e um parâmetro que não fizesse nada seria pior do que
	/// nenhum.
	/// </remarks>
	public static RabbitMqSettings Load() =>
		TestSettingsSection.Read<RabbitMqSettings>("rabbitmq") ?? new RabbitMqSettings();
}
