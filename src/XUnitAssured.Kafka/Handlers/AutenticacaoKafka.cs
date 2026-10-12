using System;
using Confluent.Kafka;
using XUnitAssured.Core.Abstractions;
using XUnitAssured.Kafka.Configuration;

namespace XUnitAssured.Kafka.Handlers;

/// <summary>
/// Como uma <see cref="KafkaAuthConfig"/> vira configuração de produtor ou consumidor (#164).
/// </summary>
/// <remarks>
/// Os quatro passos e o <see cref="Testing.KafkaClassFixture"/> tinham cada um a sua cópia disto,
/// e elas divergiram: os passos em lote não liam a autenticação que a fixture põe no contexto, e
/// só a fixture aplicava a seção <c>ssl</c> de uma autenticação SASL — o CA de um broker com
/// certificado próprio. Foi o mesmo formato que separou os dois caminhos do Http na #147.
/// </remarks>
internal static class AutenticacaoKafka
{
	/// <summary>
	/// A autenticação que vale para um passo: a dele, senão a que a fixture pôs no contexto,
	/// senão a do arquivo de configuração.
	/// </summary>
	internal static KafkaAuthConfig? Resolver(KafkaAuthConfig? doPasso, ITestContext? contexto) =>
		doPasso
		?? contexto?.GetProperty<KafkaAuthConfig>("_KafkaAuthConfig")
		?? KafkaSettings.Load().Authentication;

	/// <summary>
	/// Aplica <paramref name="autenticacao"/> a <paramref name="config"/>; sem autenticação, ou
	/// com <see cref="KafkaAuthenticationType.None"/>, não muda nada.
	/// </summary>
	/// <remarks>
	/// Depois do handler, a seção <c>ssl</c>, quando existe, também é aplicada: com SASL sobre
	/// SSL, é onde ficam o CA e a verificação do certificado do broker.
	/// </remarks>
	internal static void Aplicar(ClientConfig config, KafkaAuthConfig? autenticacao)
	{
		if (config == null)
			throw new ArgumentNullException(nameof(config));

		if (autenticacao == null || autenticacao.Type == KafkaAuthenticationType.None)
			return;

		var handler = Handler(autenticacao);
		if (handler != null)
		{
			if (config is ConsumerConfig consumidor)
				handler.ApplyAuthentication(consumidor);
			else if (config is ProducerConfig produtor)
				handler.ApplyAuthentication(produtor);
			else
			{
				// Os handlers só mexem em chaves comuns aos dois; um ClientConfig puro, como o de
				// KafkaSettings.ToClientConfig, recebe o que eles escreveriam num produtor.
				var comum = new ProducerConfig();
				handler.ApplyAuthentication(comum);
				foreach (var (chave, valor) in comum)
					config.Set(chave, valor);
			}
		}

		if (autenticacao.Ssl != null)
			AplicarSsl(config, autenticacao.Ssl);
	}

	private static IKafkaAuthenticationHandler? Handler(KafkaAuthConfig autenticacao) => autenticacao.Type switch
	{
		KafkaAuthenticationType.SaslPlain when autenticacao.SaslPlain != null => new SaslPlainHandler(autenticacao.SaslPlain),
		KafkaAuthenticationType.SaslSsl when autenticacao.SaslPlain != null => new SaslPlainHandler(autenticacao.SaslPlain),
		KafkaAuthenticationType.SaslScram256 when autenticacao.SaslScram != null => new SaslScramHandler(autenticacao.SaslScram),
		KafkaAuthenticationType.SaslScram512 when autenticacao.SaslScram != null => new SaslScramHandler(autenticacao.SaslScram),
		KafkaAuthenticationType.Ssl when autenticacao.Ssl != null => new SslHandler(autenticacao.Ssl),
		KafkaAuthenticationType.MutualTls when autenticacao.Ssl != null => new SslHandler(autenticacao.Ssl),
		KafkaAuthenticationType.OAuthBearer when autenticacao.OAuthBearer != null => new OAuthBearerHandler(autenticacao.OAuthBearer),
		_ => null
	};

	private static void AplicarSsl(ClientConfig config, SslConfig ssl)
	{
		if (!string.IsNullOrWhiteSpace(ssl.SslCaLocation))
			config.SslCaLocation = ssl.SslCaLocation;

		config.EnableSslCertificateVerification = ssl.EnableSslCertificateVerification;

		if (!string.IsNullOrWhiteSpace(ssl.SslCertificateLocation))
			config.SslCertificateLocation = ssl.SslCertificateLocation;

		if (!string.IsNullOrWhiteSpace(ssl.SslKeyLocation))
			config.SslKeyLocation = ssl.SslKeyLocation;

		if (!string.IsNullOrWhiteSpace(ssl.SslKeyPassword))
			config.SslKeyPassword = ssl.SslKeyPassword;
	}
}
