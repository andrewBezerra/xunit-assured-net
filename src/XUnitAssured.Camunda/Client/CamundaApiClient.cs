using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using XUnitAssured.Camunda.Gateway;

using Oficial = Camunda.Orchestration.Sdk;

namespace XUnitAssured.Camunda.Client;

/// <summary>
/// A client of the Camunda 8.10 REST API, on the official <c>Camunda.Orchestration.Sdk</c>: one per configuration, with its
/// connections reused, a deadline per call, the OAuth token kept and asked once for concurrent calls, and each HTTP request
/// told to the caller.
/// </summary>
/// <remarks>
/// <para>
/// Get it with <see cref="For"/>, which returns the same client for the same settings for the life of the process. It is
/// safe for concurrent calls, and it is not meant to be disposed.
/// </para>
/// <para>
/// Every operation takes a <c>timeout</c> (<see cref="DefaultTimeout"/> when <see langword="null"/>), an <c>observe</c>
/// callback that hears each HTTP request it makes (<see cref="CamundaCall"/>), and the caller's cancellation token. It
/// throws <see cref="CamundaApiException"/> when the API refuses, <see cref="CamundaCredentialException"/> when the
/// credentials are refused, and <see cref="CamundaUnavailableException"/> when there is no answer. A command on a job,
/// an instance or a user task that no longer exists returns <see langword="false"/>.
/// </para>
/// </remarks>
public sealed partial class CamundaApiClient
{
	/// <summary>The deadline of a call when none is given: 10 s.</summary>
	public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);

	/// <summary>The deadline of a deployment when none is given: 30 s.</summary>
	public static readonly TimeSpan DeployTimeout = TimeSpan.FromSeconds(30);

	/// <summary>What the deadline of a job activation adds to its long wait: 10 s.</summary>
	public static readonly TimeSpan ActivationMargin = TimeSpan.FromSeconds(10);

	/// <summary>How many definitions a search of definitions returns: 200.</summary>
	public const int PageSize = 200;

	// As buscas da instância (user tasks, elementos, incidentes, assinaturas, instâncias): uma rodada não passa disso.
	private const int Limite = 1000;

	// Os comandos do job, quando o Camunda não respondeu: até 3 tentativas, 1 s entre elas.
	private const int TentativasDoJob = 3;
	private static readonly TimeSpan EsperaDoJob = TimeSpan.FromSeconds(1);

	private static readonly ConcurrentDictionary<string, CamundaApiClient> Compartilhados = new(StringComparer.Ordinal);

	private readonly Oficial.CamundaClient _sdk;
	private readonly TimeProvider _tempo;
	private readonly string _endereco;

	/// <summary>
	/// The client for these settings, shared by every caller with the same settings for the life of the process. A
	/// setting left empty comes from the <c>CAMUNDA_*</c> environment variables, as in the SDK.
	/// </summary>
	/// <param name="settings">The <c>camunda</c> section: the REST address, the tenant and the authentication.</param>
	/// <returns>The shared client.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="settings"/> is <see langword="null"/>.</exception>
	/// <exception cref="InvalidOperationException">The authentication type is not None, Basic or OAuth, or OAuth lacks its token address.</exception>
	public static CamundaApiClient For(CamundaSettings settings)
	{
		ArgumentNullException.ThrowIfNull(settings);
		return Compartilhados.GetOrAdd(Key(settings), _ =>
			new CamundaApiClient(settings, Transporte(settings, Environment.GetEnvironmentVariable), TimeProvider.System, Environment.GetEnvironmentVariable));
	}

	/// <summary>
	/// O cliente sobre o handler dado (o transporte de verdade, ou o falso dos testes), com o relógio dos prazos e das
	/// esperas e as variáveis de ambiente de onde vem o que a configuração não preencheu.
	/// </summary>
	internal CamundaApiClient(CamundaSettings settings, HttpMessageHandler transporte, TimeProvider tempo, Func<string, string?> ambiente)
	{
		ArgumentNullException.ThrowIfNull(settings);
		ArgumentNullException.ThrowIfNull(transporte);
		ArgumentNullException.ThrowIfNull(tempo);
		ArgumentNullException.ThrowIfNull(ambiente);

		var config = SdkCamundaGateway.Configuracao(settings, ambiente);
		string? Valor(string chave) => config.TryGetValue(chave, out var valor) ? valor : ambiente(chave);

		_endereco = (Valor("CAMUNDA_REST_ADDRESS") ?? ambiente("ZEEBE_REST_ADDRESS") ?? "http://localhost:8080").TrimEnd('/');
		var exato = string.Equals(Valor("CAMUNDA_REST_ADDRESS_EXACT"), "true", StringComparison.OrdinalIgnoreCase);
		var baseDaApi = new Uri((exato || _endereco.EndsWith("/v2", StringComparison.Ordinal) ? _endereco : _endereco + "/v2") + "/");
		var prefixo = new Uri(_endereco + "/").AbsolutePath.TrimEnd('/');

		var dados = new DadosDaAutenticacao(
			(Valor("CAMUNDA_AUTH_STRATEGY") ?? "NONE").Trim().ToUpperInvariant(),
			Valor("CAMUNDA_BASIC_AUTH_USERNAME"), Valor("CAMUNDA_BASIC_AUTH_PASSWORD"),
			Valor("CAMUNDA_CLIENT_ID"), Valor("CAMUNDA_CLIENT_SECRET"), Valor("CAMUNDA_OAUTH_URL"),
			Valor("CAMUNDA_TOKEN_AUDIENCE"), Valor("CAMUNDA_OAUTH_SCOPE"));
		if (dados.Estrategia == "OAUTH" && string.IsNullOrWhiteSpace(dados.EnderecoDoToken))
			throw new InvalidOperationException("camunda.authentication.oauthUrl (or CAMUNDA_OAUTH_URL) is required for OAuth.");

		_tempo = tempo;
		Transport = transporte;
		Autenticacao = new AutenticacaoDoCliente(new ObservadorDasChamadas(new XmlComoJson(transporte), prefixo, tempo), dados, tempo);
		var http = new HttpClient(Autenticacao, disposeHandler: false) { BaseAddress = baseDaApi, Timeout = Timeout.InfiniteTimeSpan };
		HttpClientsCreated++;

		// O SDK não autentica: com o HttpClient dado, ele não instala a autenticação dele, e quem a faz é o handler acima.
		var paraOSdk = new Dictionary<string, string> { ["CAMUNDA_REST_ADDRESS"] = _endereco, ["CAMUNDA_AUTH_STRATEGY"] = "NONE" };
		if (Valor("CAMUNDA_DEFAULT_TENANT_ID") is { Length: > 0 } tenant)
			paraOSdk["CAMUNDA_DEFAULT_TENANT_ID"] = tenant;
		if (exato)
			paraOSdk["CAMUNDA_REST_ADDRESS_EXACT"] = "true";
		_sdk = Oficial.CamundaClient.Create(new Oficial.CamundaOptions { Config = paraOSdk, HttpClient = http, TimeProvider = tempo });
	}

	/// <summary>O cliente do SDK sobre o mesmo HttpClient, para o gateway da DSL: um caminho HTTP só na família.</summary>
	internal Oficial.CamundaClient Sdk => _sdk;

	/// <summary>O handler embaixo de tudo: o <see cref="SocketsHttpHandler"/> do pool, ou o falso dos testes.</summary>
	internal HttpMessageHandler Transport { get; }

	/// <summary>Quantos HttpClient este cliente criou: um, para a vida dele.</summary>
	internal int HttpClientsCreated { get; private set; }

	private AutenticacaoDoCliente Autenticacao { get; }

	/// <summary>
	/// A chave do cliente compartilhado: o endereço, o tenant e a autenticação, com o hash da senha e do secret, nunca o
	/// valor. Um cadastro mudado ganha outro cliente.
	/// </summary>
	internal static string Key(CamundaSettings settings)
	{
		var auth = settings.Authentication ?? new CamundaAuthenticationSettings();
		return JsonSerializer.Serialize(new[]
		{
			settings.RestAddress, settings.TenantId, auth.Type, auth.Username, Hash(auth.Password), auth.ClientId, Hash(auth.ClientSecret),
			auth.OAuthUrl, auth.Audience, auth.Scope, settings.CaCertificatePath
		});
	}

	private static string? Hash(string? segredo) =>
		segredo is null ? null : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(segredo)));

	// O pool de conexões do cliente: relidas a cada 5 min (o DNS e o balanceador), fechadas depois de 1 min ociosas. O
	// prazo é por chamada, e não do HttpClient. Com a autoridade do cluster, o TLS confia só nela.
	internal static SocketsHttpHandler Transporte(CamundaSettings settings, Func<string, string?> ambiente)
	{
		var transporte = new SocketsHttpHandler
		{
			PooledConnectionLifetime = TimeSpan.FromMinutes(5),
			PooledConnectionIdleTimeout = TimeSpan.FromMinutes(1),
		};
		if ((settings.CaCertificatePath ?? ambiente("CAMUNDA_MTLS_CA_PATH")) is { Length: > 0 } autoridade)
			transporte.SslOptions.RemoteCertificateValidationCallback = ConfiancaNaAutoridade.Callback(ConfiancaNaAutoridade.Ler(autoridade));
		return transporte;
	}

	/// <summary>
	/// Uma operação: o escopo que leva os avisos à quem chamou, o prazo ligado ao cancelamento de quem chamou, e a tradução
	/// do que o SDK e os handlers lançam nas exceções do cliente.
	/// </summary>
	private async Task<T> Executar<T>(
		Func<Oficial.CamundaClient, CancellationToken, Task<T>> operacao, TimeSpan prazo, Action<CamundaCall>? observe, CancellationToken cancellationToken)
	{
		var anterior = EscopoDaChamada.Atual;
		var escopo = new EscopoDaChamada(observe);
		EscopoDaChamada.Atual = escopo;
		using var limite = new CancellationTokenSource(prazo, _tempo);
		using var ligado = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, limite.Token);
		try
		{
			return await operacao(_sdk, ligado.Token).ConfigureAwait(false);
		}
		catch (Exception ex) when (Traduzir(ex, escopo, prazo, limite, cancellationToken) is { } traduzida)
		{
			throw traduzida;
		}
		finally
		{
			EscopoDaChamada.Atual = anterior;
		}
	}

	private Task Executar(
		Func<Oficial.CamundaClient, CancellationToken, Task> operacao, TimeSpan prazo, Action<CamundaCall>? observe, CancellationToken cancellationToken) =>
		Executar<bool>(async (sdk, ct) =>
		{
			await operacao(sdk, ct).ConfigureAwait(false);
			return true;
		}, prazo, observe, cancellationToken);

	// A exceção do cliente para o que veio de baixo; nula para o que sobe como está (o cancelamento de quem chamou, e as
	// exceções do próprio cliente, que os handlers já lançam prontas).
	private Exception? Traduzir(Exception ex, EscopoDaChamada escopo, TimeSpan prazo, CancellationTokenSource limite, CancellationToken deQuemChamou)
	{
		var raiz = ex;
		while (raiz is Oficial.CamundaSdkException and not Oficial.HttpSdkException && raiz.InnerException is { } interna)
			raiz = interna;

		var endereco = escopo.UltimoEndereco ?? _endereco;
		switch (raiz)
		{
			case CamundaApiException or CamundaCredentialException or CamundaUnavailableException:
				return ReferenceEquals(raiz, ex) ? null : raiz;
			case OperationCanceledException when deQuemChamou.IsCancellationRequested:
				return null;
			case OperationCanceledException when limite.IsCancellationRequested:
				return escopo.PedindoToken
					? new CamundaUnavailableException(CamundaEndpoint.IdentityProvider, Autenticacao.EnderecoDoToken, prazo, Prazo(prazo), ex)
					: new CamundaUnavailableException(CamundaEndpoint.Camunda, endereco, prazo, Prazo(prazo), ex);
			case Oficial.HttpSdkException { Status: 401 or 403 } recusa:
				return new CamundaCredentialException(CamundaCredentialRefusal.Camunda, recusa.Status, null, endereco);
			case Oficial.HttpSdkException recusa:
				return new CamundaApiException(recusa.Status ?? 0, string.IsNullOrWhiteSpace(recusa.Type) ? null : recusa.Title,
					string.IsNullOrWhiteSpace(recusa.Detail) ? null : recusa.Detail, escopo.UltimoCaminho ?? string.Empty, recusa);
			case HttpRequestException falha:
				return new CamundaUnavailableException(CamundaEndpoint.Camunda, endereco, null, falha.Message, falha);
			default:
				return null;
		}
	}

	private static string Prazo(TimeSpan prazo) =>
		$"no answer in {prazo.TotalSeconds.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)} s";

	// Um comando sobre um job, uma instância ou uma user task: falso quando o alvo não existe mais.
	private async Task<bool> Comando(
		Func<Oficial.CamundaClient, CancellationToken, Task> comando, TimeSpan? timeout, Action<CamundaCall>? observe, CancellationToken cancellationToken)
	{
		try
		{
			await Executar(comando, timeout ?? DefaultTimeout, observe, cancellationToken).ConfigureAwait(false);
			return true;
		}
		catch (CamundaApiException recusa) when (recusa.Status == 404)
		{
			return false;
		}
	}

	// Um comando do job, até 3 vezes quando o Camunda não respondeu. O "não existe mais" de uma nova tentativa é o da
	// anterior, que chegou ao Camunda sem a resposta voltar a tempo: o comando valeu.
	private async Task<bool> ComandoDoJob(
		Func<Oficial.CamundaClient, CancellationToken, Task> comando, TimeSpan? timeout, Action<CamundaCall>? observe, CancellationToken cancellationToken)
	{
		for (var tentativa = 1; ; tentativa++)
		{
			try
			{
				var aceito = await Comando(comando, timeout, observe, cancellationToken).ConfigureAwait(false);
				return aceito || tentativa > 1;
			}
			catch (CamundaUnavailableException) when (tentativa < TentativasDoJob)
			{
				await Task.Delay(EsperaDoJob, _tempo, cancellationToken).ConfigureAwait(false);
			}
		}
	}

	private static IReadOnlyList<T> Lista<T>(IEnumerable<T> itens) => itens.ToList();
}
