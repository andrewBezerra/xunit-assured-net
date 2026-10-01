using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

using XUnitAssured.Core.Abstractions;
using XUnitAssured.Core.DSL;
using XUnitAssured.Core.Results;
using XUnitAssured.Core.Storage;
using XUnitAssured.Http.Results;
using XUnitAssured.Http.Steps;

namespace XUnitAssured.Tests.HttpTests;

[Trait("Category", "Http")]
[Trait("Component", "RequestStep")]
/// <summary>
/// What the step does when the answer never comes.
///
/// <para>
/// This behaviour had no test that ran. The one that covered it pointed at a public delay
/// service and was skipped as "unreliable with external public APIs" — but the reason it could
/// not pass was not the service. It asserted that a timed-out step reports an error, and the
/// step reported none.
/// </para>
///
/// <para>
/// A timeout is not an environment-dependent value: a server that never answers produces one
/// every time. What made it look flaky was depending on a third party to be slow. Here the slow
/// side is a socket on the loopback that accepts the connection and then says nothing, so the
/// only thing being measured is the step's own deadline.
/// </para>
/// </summary>
public class TempoEsgotadoTests
{
	/// <summary>
	/// Short enough to keep the suite fast, long enough that a loaded machine does not trip it
	/// by accident. Nothing here asserts how long anything took.
	/// </summary>
	private const int SegundosDeEspera = 1;

	/// <summary>
	/// Trinta vezes a espera do passo, e não uma medição.
	///
	/// <para>
	/// Nada aqui afirma quanto tempo algo levou. Este número existe para que um passo que
	/// NUNCA para de esperar falhe em vez de travar a suíte. Sem ele, as duas verificações
	/// abaixo passavam: a espera acabava lá na frente, pelo prazo do HttpClient ou pelo
	/// prazo alto do próprio passo, e o teste dava verde sem ter exercido o que dizia. Uma
	/// delas levava cem segundos e a outra trezentos.
	/// </para>
	/// </summary>
	private const int SegundosDoVigia = 30;

	[Fact(DisplayName = "A request that outlives its timeout should fail")]
	public async Task Request_That_Outlives_Its_Timeout_Should_Fail()
	{
		using var servidor = new ServidorQueNuncaResponde();

		var resultado = await ExecutarContra(servidor);

		resultado.Success.ShouldBeFalse();
	}

	// É o ponto do teste. Um passo que falha sem dizer por quê obriga quem lê a reproduzir a
	// falha à mão, e foi exatamente isto que ficou sem cobertura enquanto o teste estava
	// ignorado.
	[Fact(DisplayName = "A timed-out request should say why it failed")]
	public async Task Timed_Out_Request_Should_Say_Why()
	{
		using var servidor = new ServidorQueNuncaResponde();

		var resultado = await ExecutarContra(servidor);

		resultado.Errors.ShouldNotBeEmpty("a step that gives up must explain itself");
	}

	// O estado do passo é o que um relatório de execução lê. Dizer "Succeeded" num passo que
	// desistiu faz um painel inteiro mentir.
	[Fact(DisplayName = "A timed-out step should be recorded as failed")]
	public async Task Timed_Out_Step_Should_Be_Recorded_As_Failed()
	{
		using var servidor = new ServidorQueNuncaResponde();

		var resultado = await ExecutarContra(servidor);

		resultado.Metadata.Status.ShouldBe(StepStatus.Failed);
	}

	// Sem resposta não há código de status, e inventar um esconderia a diferença entre "o
	// servidor recusou" e "o servidor não respondeu".
	[Fact(DisplayName = "A timed-out request should report no status code")]
	public async Task Timed_Out_Request_Should_Report_No_Status_Code()
	{
		using var servidor = new ServidorQueNuncaResponde();

		var resultado = await ExecutarContra(servidor);

		resultado.StatusCode.ShouldBe(0);
	}

	// Um HttpClient próprio desvia do Flurl e, com ele, de todo o caminho do WithTimeout. O
	// cliente aqui tem espera infinita de propósito: assim o único prazo possível é o do passo,
	// e a verificação não consegue passar por acidente quando o passo ignora o seu.
	[Fact(DisplayName = "A custom HttpClient should honour the step's timeout")]
	public async Task Custom_HttpClient_Should_Honour_The_Timeout()
	{
		using var servidor = new ServidorQueNuncaResponde();
		using var cliente = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };

		var passo = new HttpRequestStep
		{
			Url = servidor.Endereco,
			Method = HttpMethod.Get,
			TimeoutSeconds = SegundosDeEspera,
			CustomHttpClient = cliente
		};

		var resultado = await ComVigia(
			passo.ExecuteAsync(NovoContexto()),
			"o HttpClient próprio tem espera infinita, então só o prazo do passo pode encerrá-la");

		resultado.Success.ShouldBeFalse();
		resultado.Errors.ShouldNotBeEmpty("a step that gives up must explain itself");
	}

	// Quem encerra aqui é o cancelamento, e não o prazo: o prazo do passo é alto o bastante
	// para que, se o token for ignorado, a espera passe do vigia e a verificação falhe.
	[Fact(DisplayName = "Cancelling should end the wait before the timeout does")]
	public async Task Cancelling_Should_End_The_Wait()
	{
		using var servidor = new ServidorQueNuncaResponde();
		using var fonte = new CancellationTokenSource();

		var passo = new HttpRequestStep
		{
			Url = servidor.Endereco,
			Method = HttpMethod.Get,
			TimeoutSeconds = 300
		};

		var execucao = passo.ExecuteAsync(NovoContexto(), fonte.Token);
		fonte.Cancel();

		var resultado = await ComVigia(execucao, "o cancelamento deveria ter encerrado a espera");

		resultado.Success.ShouldBeFalse();
		resultado.Errors.ShouldNotBeEmpty("a step that gives up must explain itself");
	}

	/// <summary>
	/// Espera o passo terminar, e transforma "nunca termina" numa falha legível.
	/// </summary>
	private static async Task<HttpStepResult> ComVigia(
		Task<ITestStepResult> execucao, string oQueDeveriaTerEncerrado)
	{
		var vigia = Task.Delay(TimeSpan.FromSeconds(SegundosDoVigia));

		var primeiro = await Task.WhenAny(execucao, vigia);

		ReferenceEquals(primeiro, execucao).ShouldBeTrue(
			$"o passo continuou esperando além de {SegundosDoVigia}s: {oQueDeveriaTerEncerrado}");

		return (HttpStepResult)await execucao;
	}

	private static async Task<HttpStepResult> ExecutarContra(ServidorQueNuncaResponde servidor)
	{
		var passo = new HttpRequestStep
		{
			Url = servidor.Endereco,
			Method = HttpMethod.Get,
			TimeoutSeconds = SegundosDeEspera
		};

		return (HttpStepResult)await passo.ExecuteAsync(NovoContexto());
	}

	private static ITestContext NovoContexto() => new TestContext(new StepStorage());

	/// <summary>
	/// Aceita a conexão e não diz mais nada.
	///
	/// <para>
	/// Recusar a conexão produziria um erro de transporte, que é outro caminho no código.
	/// Aceitar e calar é o que um servidor travado faz, e é o que leva o passo até o prazo
	/// dele. A porta é escolhida pelo sistema, para que duas execuções em paralelo não
	/// disputem a mesma.
	/// </para>
	/// </summary>
	private sealed class ServidorQueNuncaResponde : IDisposable
	{
		private readonly TcpListener _ouvinte;
		private readonly CancellationTokenSource _parar = new();
		private readonly List<TcpClient> _conexoes = new();

		public ServidorQueNuncaResponde()
		{
			_ouvinte = new TcpListener(IPAddress.Loopback, 0);
			_ouvinte.Start();

			var porta = ((IPEndPoint)_ouvinte.LocalEndpoint).Port;
			Endereco = $"http://127.0.0.1:{porta}/";

			_ = AceitarAsync();
		}

		public string Endereco { get; }

		private async Task AceitarAsync()
		{
			try
			{
				while (!_parar.IsCancellationRequested)
				{
					var conexao = await _ouvinte.AcceptTcpClientAsync(_parar.Token).ConfigureAwait(false);

					// Guardada só para ser fechada no fim. Soltar a referência deixaria o
					// coletor fechar o socket e devolver um erro de transporte no lugar da
					// espera que este teste precisa.
					lock (_conexoes)
						_conexoes.Add(conexao);
				}
			}
			catch (OperationCanceledException)
			{
			}
			catch (ObjectDisposedException)
			{
			}
			catch (SocketException)
			{
			}
		}

		public void Dispose()
		{
			_parar.Cancel();

			lock (_conexoes)
			{
				foreach (var conexao in _conexoes)
					conexao.Dispose();

				_conexoes.Clear();
			}

			_ouvinte.Stop();
			_parar.Dispose();
		}
	}
}
