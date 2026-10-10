using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

using XUnitAssured.Core.Abstractions;
using XUnitAssured.Core.DSL;
using XUnitAssured.Http.Extensions;
using XUnitAssured.Http.Results;

namespace XUnitAssured.Tests.HttpTests;

/// <summary>
/// O que obrigava uma suíte real a sair da DSL para o HttpClient: um corpo inválido de propósito,
/// o pedido como foi enviado -- o cookie que o cliente mandou -- e pedidos que precisam chegar
/// juntos, porque a simultaneidade é o que o teste reproduz.
/// </summary>
[Trait("Category", "Http")]
[Trait("Component", "RequestStep")]
public class PedidoEnviadoEConcorrenciaTests
{
	// ---------- Corpo cru ----------

	[Fact(DisplayName = "PostRaw sends the text as written, malformed JSON included")]
	public async Task PostRaw_Sends_As_Written()
	{
		const string invalido = """{"city": ["Lisboa"]""";
		var servidor = new Eco();

		var resposta = await Given(servidor)
			.ApiResource("/pacientes")
			.PostRaw(invalido)
			.ExecuteAsync();

		resposta.Then().AssertStatusCode(200);
		servidor.Corpos.ShouldHaveSingleItem().ShouldBe(invalido);
		servidor.Tipos.ShouldHaveSingleItem().ShouldBe("application/json");
		resposta.GetResult().Request!.Body.ShouldBe(invalido);
	}

	[Fact(DisplayName = "PutRaw and PatchRaw keep the Content-Type as given, parameters included")]
	public async Task Raw_Keeps_The_Content_Type()
	{
		var servidor = new Eco();

		await Given(servidor).ApiResource("/nota").PutRaw("texto", "text/plain; charset=utf-8").ExecuteAsync();
		await Given(servidor).ApiResource("/nota").PatchRaw("<a/>", "application/xml").ExecuteAsync();

		servidor.Corpos.ShouldBe(["texto", "<a/>"]);
		servidor.Tipos.ShouldBe(["text/plain; charset=utf-8", "application/xml"]);
	}

	// ---------- O pedido enviado ----------

	[Fact(DisplayName = "Request is what went out: method, address with query, headers and the JSON body")]
	public async Task Request_Is_What_Went_Out()
	{
		var resposta = await Given(new Eco())
			.ApiResource("/pedidos")
			.Post(new { Id = 7 })
			.WithHeader("X-Rastreio", "abc")
			.WithQueryParam("pagina", 2)
			.ExecuteAsync();

		var pedido = resposta.GetResult().Request.ShouldNotBeNull();
		pedido.Method.ShouldBe("POST");
		pedido.Url!.PathAndQuery.ShouldBe("/pedidos?pagina=2");
		pedido.Header("x-rastreio").ShouldBe(["abc"]);
		pedido.Body.ShouldBe("""{"Id":7}""");

		resposta.Then()
			.AssertRequestHeader("X-RASTREIO", "abc")
			.AssertRequestHeader("Content-Type", t => t.StartsWith("application/json"));
	}

	[Fact(DisplayName = "AssertSentCookie and AssertNoSentCookie read the Cookie header sent")]
	public async Task Sent_Cookies()
	{
		var resposta = (await Given(new Eco())
			.ApiResource("/sessao")
			.WithHeader("Cookie", "sid=abc; tema=escuro")
			.Get()
			.ExecuteAsync()).Then();

		resposta.AssertSentCookie("sid")
			.AssertSentCookie("tema", v => v == "escuro")
			.AssertNoSentCookie("refresh");

		Should.Throw<ShouldAssertException>(() => resposta.AssertNoSentCookie("sid"))
			.Message.ShouldContain("sid=abc");
		Should.Throw<ShouldAssertException>(() => resposta.AssertSentCookie("refresh"))
			.Message.ShouldContain("sid, tema");
	}

	[Fact(DisplayName = "A request that got no answer still shows what was being sent")]
	public async Task Request_Without_Answer()
	{
		var resposta = await Given(new Eco(falhar: _ => throw new HttpRequestException("conexão recusada")))
			.ApiResource("/fora")
			.Delete()
			.ExecuteAsync();

		resposta.GetResult().Success.ShouldBeFalse();
		resposta.GetResult().Request.ShouldNotBeNull().Method.ShouldBe("DELETE");
	}

	[Fact(DisplayName = "Without a custom client (Flurl), the request and a raw body are captured too")]
	public async Task Flurl_Path()
	{
		using var servidor = new SoqueteQueEcoa();

		var resposta = await ScenarioDsl.Given()
			.ApiResource($"{servidor.Endereco}/eco")
			.PostRaw("""{"x": }""")
			.WithHeader("X-Rastreio", "flurl")
			.ExecuteAsync();

		resposta.Then()
			.AssertStatusCode(200)
			.AssertBodyContains("""{"x": }""")
			.AssertRequestHeader("X-Rastreio", "flurl");
		resposta.GetResult().Request!.Body.ShouldBe("""{"x": }""");
	}

	// ---------- Concorrência ----------

	[Fact(DisplayName = "Concurrently(bodies) sends them all at once, responses in the order of the bodies")]
	public async Task Concurrently_Sends_Together()
	{
		var nomes = Enumerable.Range(1, 6).Select(i => $"Membro {i}").ToList();
		// O servidor só responde quando os seis estiverem lá dentro: um envio em série esperaria
		// pelo primeiro para sempre, e o prazo do teste derrubaria o caso.
		var servidor = new Eco(barreira: nomes.Count);

		var resposta = await Given(servidor)
			.ApiResource("/membros")
			.Post()
			.Concurrently(nomes.Select(n => new { Name = n }))
			.ExecuteAsync();

		resposta.Then()
			.AssertStatusCode(200)
			.AssertEach(r => r.AssertBodyContains("Membro"));

		var respostas = ((ConcurrentHttpStepResult)resposta.GetResult()).Responses;
		respostas.Select(r => r.JsonPath<string>("$.Name")).ShouldBe(nomes);
		servidor.MaiorSimultaneidade.ShouldBe(nomes.Count);
	}

	[Fact(DisplayName = "Concurrently(n) repeats the request, and a header added after it goes in every copy")]
	public async Task Concurrently_N_Times()
	{
		var servidor = new Eco(barreira: 3);

		var resposta = await Given(servidor)
			.ApiResource("/pagar")
			.Post(new { Valor = 10 })
			.Concurrently(3)
			.WithHeader("Idempotency-Key", "k1")
			.ExecuteAsync();

		resposta.Then().AssertEach(r => r
			.AssertStatusCode(200)
			.AssertRequestHeader("Idempotency-Key", "k1"));
		servidor.Corpos.ShouldAllBe(c => c == """{"Valor":10}""");
		servidor.Corpos.Length.ShouldBe(3);
	}

	[Fact(DisplayName = "On concurrent responses, failures name each request and single-response assertions say where to go")]
	public async Task Concurrent_Failures_Are_Clear()
	{
		// O segundo nome já existe: a API recusa um, como recusaria numa corrida real.
		var servidor = new Eco(p => p.Contains("Ana")
			? new HttpResponseMessage(HttpStatusCode.Conflict) { Content = new StringContent("{}") }
			: null);

		var resposta = (await Given(servidor)
			.ApiResource("/membros")
			.Post()
			.Concurrently(new object[] { new { Name = "Bia" }, new { Name = "Ana" }, new { Name = "Caio" } })
			.ExecuteAsync()).Then();

		resposta.GetResult().StatusCode.ShouldBe(0);
		resposta.GetResult().Success.ShouldBeFalse();

		Should.Throw<ShouldAssertException>(() => resposta.AssertStatusCode(200))
			.Message.ShouldContain("200, 409, 200");
		var falha = Should.Throw<ShouldAssertException>(() => resposta.AssertEach(r => r.AssertStatusCode(200)));
		falha.Message.ShouldContain("Request 2 of 3 (status 409)");
		falha.Message.ShouldNotContain("Request 1 of 3");

		Should.Throw<InvalidOperationException>(() => resposta.AssertBodyContains("x"))
			.Message.ShouldContain("AssertEach");
	}

	[Fact(DisplayName = "Concurrently needs at least one request")]
	public void Concurrently_Needs_One()
	{
		Should.Throw<ArgumentOutOfRangeException>(() => Given(new Eco()).ApiResource("/x").Get().Concurrently(0));
		Should.Throw<ArgumentException>(() => Given(new Eco()).ApiResource("/x").Post().Concurrently(Array.Empty<object>()));
	}

	// ---------- Apoio ----------

	private static ITestScenario Given(HttpMessageHandler servidor) => ScenarioDsl.Given(new ProvedorDeMentira(servidor));

	/// <summary>
	/// Devolve o corpo recebido e guarda o que chegou. Com barreira, segura cada pedido até que
	/// esse número deles esteja lá dentro ao mesmo tempo.
	/// </summary>
	private sealed class Eco(
		Func<string, HttpResponseMessage?>? responder = null,
		int barreira = 0,
		Func<HttpRequestMessage, HttpResponseMessage>? falhar = null) : HttpMessageHandler
	{
		private readonly ConcurrentQueue<string> _corpos = new();
		private readonly ConcurrentQueue<string> _tipos = new();
		private readonly TaskCompletionSource _todosChegaram = new(TaskCreationOptions.RunContinuationsAsynchronously);
		private int _dentro;
		private int _maior;

		public string[] Corpos => _corpos.ToArray();

		public string[] Tipos => _tipos.ToArray();

		public int MaiorSimultaneidade => Volatile.Read(ref _maior);

		protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			if (falhar != null)
				return falhar(request);

			var corpo = request.Content == null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
			_corpos.Enqueue(corpo);
			if (request.Content?.Headers.ContentType is { } tipo)
				_tipos.Enqueue(tipo.ToString());

			var dentro = Interlocked.Increment(ref _dentro);
			int maior;
			while ((maior = Volatile.Read(ref _maior)) < dentro && Interlocked.CompareExchange(ref _maior, dentro, maior) != maior) { }

			try
			{
				if (barreira > 0)
				{
					if (dentro >= barreira)
						_todosChegaram.TrySetResult();
					await _todosChegaram.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
				}

				return responder?.Invoke(corpo)
					?? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(corpo, Encoding.UTF8, "application/json") };
			}
			finally
			{
				Interlocked.Decrement(ref _dentro);
			}
		}
	}

	/// <summary>
	/// Um servidor HTTP mínimo no loopback, para o caminho sem HttpClient próprio, que sai pelo
	/// Flurl e pela rede: lê um pedido e devolve o corpo dele.
	/// </summary>
	private sealed class SoqueteQueEcoa : IDisposable
	{
		private readonly TcpListener _ouvinte = new(IPAddress.Loopback, 0);

		public SoqueteQueEcoa()
		{
			_ouvinte.Start();
			_ = Task.Run(AtenderAsync);
		}

		public string Endereco => $"http://127.0.0.1:{((IPEndPoint)_ouvinte.LocalEndpoint).Port}";

		private async Task AtenderAsync()
		{
			using var cliente = await _ouvinte.AcceptTcpClientAsync();
			var fluxo = cliente.GetStream();
			var recebido = new StringBuilder();
			var buffer = new byte[4096];

			// Cabeçalhos até a linha em branco, e então o corpo pelo Content-Length.
			while (true)
			{
				var lidos = await fluxo.ReadAsync(buffer);
				if (lidos == 0)
					return;
				recebido.Append(Encoding.UTF8.GetString(buffer, 0, lidos));
				var texto = recebido.ToString();
				var fim = texto.IndexOf("\r\n\r\n", StringComparison.Ordinal);
				if (fim < 0)
					continue;

				var tamanho = texto.Substring(0, fim).Split("\r\n")
					.Where(l => l.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
					.Select(l => int.Parse(l.Substring("Content-Length:".Length).Trim()))
					.FirstOrDefault();
				if (Encoding.UTF8.GetByteCount(texto.Substring(fim + 4)) < tamanho)
					continue;

				var corpo = Encoding.UTF8.GetBytes(texto.Substring(fim + 4));
				var cabecalho = Encoding.ASCII.GetBytes(
					$"HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: {corpo.Length}\r\nConnection: close\r\n\r\n");
				await fluxo.WriteAsync(cabecalho);
				await fluxo.WriteAsync(corpo);
				return;
			}
		}

		public void Dispose() => _ouvinte.Stop();
	}
}
