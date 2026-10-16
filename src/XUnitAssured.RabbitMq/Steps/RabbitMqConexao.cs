using System;

using RabbitMQ.Client;

namespace XUnitAssured.RabbitMq.Steps;

/// <summary>
/// De onde os passos tiram a conexão com o broker.
/// </summary>
/// <remarks>
/// Os três passos montavam a mesma <see cref="ConnectionFactory"/> cada um no seu corpo. Ela fica
/// aqui, e cada passo a recebe por uma propriedade interna: é o que deixa os testes do pacote
/// trocarem o broker por um falso e verificar o que o passo declara, publica e consome, sem rede.
/// Para quem usa o pacote nada muda: o padrão é esta fábrica.
/// </remarks>
internal static class RabbitMqConexao
{
	/// <summary>
	/// A fábrica do cliente oficial, com a URI já resolvida e o nome que aparece no painel do broker.
	/// </summary>
	internal static IConnectionFactory FabricaPadrao(string uri) => new ConnectionFactory
	{
		Uri = new Uri(uri),
		ClientProvidedName = RabbitMqSettings.Load().ClientProvidedName
	};
}
