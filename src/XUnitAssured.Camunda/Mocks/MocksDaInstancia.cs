using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using XUnitAssured.Camunda.Cobertura;
using XUnitAssured.Camunda.Gateway;

namespace XUnitAssured.Camunda.Mocks;

/// <summary>
/// Os mocks trabalhando para uma instância e para as instâncias que ela chamar.
/// </summary>
/// <remarks>
/// <para>
/// A cada volta: acha as filhas, resolve os jobs dos tipos mockados, completa as user tasks
/// mockadas (pela busca de esperas, que cobre a árvore) e pula os timers declarados (pelo BPMN
/// implantado e pelos elementos ativos, porque a borda e o timer de gateway de evento não aparecem
/// como espera).
/// </para>
/// <para>
/// <b>Por que não um worker por tipo.</b> A ativação de jobs do Camunda não filtra por instância:
/// um worker de <c>cobrar</c> pegaria o job de qualquer teste rodando no cluster. Aqui o mock
/// procura os jobs <i>desta</i> instância, pela chave dela, e os resolve pela chave do job, sem
/// ativá-los. O motor aceita completar ou lançar erro num job que ninguém ativou (é o que o
/// Camunda Process Test faz no <c>completeJob</c>). Dois testes em paralelo mockando o mesmo tipo
/// com respostas diferentes não se enxergam: medido na Fase 0, 5 de 5.
/// </para>
/// <para>
/// <b>O que isto não protege.</b> Um worker de verdade, ligado no mesmo cluster para um tipo que
/// o teste mocka, ativa o job antes. A busca não mostra a ativação, então só dá para perceber
/// quando ele usou lease: completar sem o token é recusado com <c>INVALID_STATE</c>, e aí o teste
/// falha dizendo isso. Sem lease, o teste completa por cima e passa, e o worker real já pode ter
/// feito o que faz. O cluster de teste não deve ter worker real dos tipos mockados.
/// </para>
/// <para>
/// O laço termina quando a instância termina, no fim de <c>mockLifetimeSeconds</c>, ou quando o
/// processo de teste termina, o que vier primeiro. Nos dois últimos casos a instância ainda ativa
/// é cancelada, para não ficar esquecida num cluster compartilhado. O fim do processo importa
/// porque o <c>dotnet test</c> costuma acabar bem antes do prazo, e o laço morre com ele: medido
/// contra a 8.10.4, a instância de um teste que falhou ficava ativa para sempre.
/// </para>
/// <para>
/// Um processo morto à força (o depurador parado, o runner abortado) não avisa ninguém, e aí a
/// instância fica.
/// </para>
/// </remarks>
internal sealed class MocksDaInstancia
{
	internal const string ChaveNoContexto = "_CamundaMocksDaInstancia";

	private static readonly string[] EstadosFinais = { "COMPLETED", "TERMINATED" };

	private readonly ICamundaGateway _gateway;
	private readonly IReadOnlyDictionary<string, JobMock> _mocks;
	private readonly TimeSpan _intervalo;
	private readonly TimeSpan _vida;

	private readonly ConcurrentDictionary<string, byte> _instancias = new();
	private readonly ConcurrentDictionary<string, byte> _tratados = new();
	private readonly ConcurrentDictionary<string, int> _invocacoes = new(StringComparer.Ordinal);
	private readonly ConcurrentQueue<string> _problemas = new();
	private readonly CancellationTokenSource _parar = new();

	private string? _ultimaFalhaDeRede;
	private volatile bool _instanciaTerminou;

	// Os laços ainda rodando neste processo, para o fim do processo cancelar as instâncias deles.
	private static readonly ConcurrentDictionary<MocksDaInstancia, byte> Abertos = new();

	// O que o fim do processo espera pelos cancelamentos, todos juntos, antes de desistir.
	private static readonly TimeSpan PrazoNaSaida = TimeSpan.FromSeconds(10);

	static MocksDaInstancia() => AppDomain.CurrentDomain.ProcessExit += (_, _) =>
	{
		EncerrarAbertos(PrazoNaSaida);
		EscreverCobertura();
	};

	// Onde gravar o relatório: a pasta das configurações do último laço que tinha cobertura ligada.
	private static string? _pastaDaCobertura;
	private static readonly object TravaDaEscrita = new();

	internal static void EscreverCobertura()
	{
		if (_pastaDaCobertura is null || CoberturaDaSuite.Global.Vazia)
			return;

		try
		{
			lock (TravaDaEscrita)
				CoberturaDaSuite.Global.Escrever(_pastaDaCobertura);
		}
		catch (Exception)
		{
			// O relatório é um extra: falhar aqui não muda nenhum teste.
		}
	}

	/// <summary>Os laços que ainda rodam neste processo.</summary>
	internal static IReadOnlyCollection<MocksDaInstancia> Abertas => Abertos.Keys.ToArray();

	private readonly IReadOnlyDictionary<string, UserTaskMock> _userTasks;
	private readonly IReadOnlyCollection<string> _timers;
	private readonly IReadOnlyDictionary<string, EventoMock> _mensagens;
	private readonly IReadOnlyDictionary<string, EventoMock> _sinais;
	private readonly IReadOnlyDictionary<string, FilhoMock> _filhos;
	private readonly string? _tenantId;

	// A versão da definição de cada instância da árvore, para ler o BPMN dela; e o BPMN lido.
	private readonly ConcurrentDictionary<string, string> _definicaoDaInstancia = new();
	private readonly ConcurrentDictionary<string, ModeloBpmn> _modelos = new();
	private readonly ConcurrentDictionary<string, string> _xmls = new();
	private readonly CoberturaDaSuite? _cobertura;

	// Os eventos que o pacote disparou por modificação: o timer pulado, o sinal, a borda do filho. O
	// Camunda não cria instância de elemento para eles, então a cobertura os pega daqui.
	private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> _disparados = new();

	private void Disparou(string instancia, string elementId) =>
		_disparados.GetOrAdd(instancia, _ => new ConcurrentDictionary<string, byte>(StringComparer.Ordinal)).TryAdd(elementId, 0);
	private int _coberturaRegistrada;
	private readonly ConcurrentDictionary<string, byte> _recusasJaDitas = new();
	private readonly ConcurrentDictionary<string, int> _userTasksResolvidas = new(StringComparer.Ordinal);
	private readonly ConcurrentDictionary<string, int> _timersPulados = new(StringComparer.Ordinal);
	private readonly ConcurrentDictionary<string, int> _mensagensCorrelacionadas = new(StringComparer.Ordinal);
	private readonly ConcurrentDictionary<string, int> _sinaisDisparados = new(StringComparer.Ordinal);
	private readonly ConcurrentDictionary<string, int> _filhosMockados = new(StringComparer.Ordinal);
	private readonly ConcurrentDictionary<string, InstanciaVista> _filhasVistas = new();

	public MocksDaInstancia(
		ICamundaGateway gateway,
		string processInstanceKey,
		IReadOnlyDictionary<string, JobMock> mocks,
		CamundaSettings settings,
		IReadOnlyDictionary<string, UserTaskMock>? userTasks = null,
		IReadOnlyCollection<string>? timers = null,
		string? definitionKey = null,
		IReadOnlyDictionary<string, EventoMock>? mensagens = null,
		IReadOnlyDictionary<string, EventoMock>? sinais = null,
		IReadOnlyDictionary<string, FilhoMock>? filhos = null,
		CoberturaDaSuite? cobertura = null)
	{
		_gateway = gateway;
		_mocks = new Dictionary<string, JobMock>(mocks, StringComparer.Ordinal);
		_userTasks = new Dictionary<string, UserTaskMock>(
			userTasks ?? new Dictionary<string, UserTaskMock>(), StringComparer.Ordinal);
		_timers = (timers ?? Array.Empty<string>()).ToArray();
		_mensagens = new Dictionary<string, EventoMock>(mensagens ?? new Dictionary<string, EventoMock>(), StringComparer.Ordinal);
		_sinais = new Dictionary<string, EventoMock>(sinais ?? new Dictionary<string, EventoMock>(), StringComparer.Ordinal);
		_filhos = new Dictionary<string, FilhoMock>(filhos ?? new Dictionary<string, FilhoMock>(), StringComparer.Ordinal);
		_tenantId = settings.TenantId;
		if (settings.Coverage?.Enabled == true)
		{
			_cobertura = cobertura ?? CoberturaDaSuite.Global;
			if (cobertura is null)
				_pastaDaCobertura = Path.IsPathRooted(settings.Coverage.Directory)
					? settings.Coverage.Directory
					: Path.Combine(AppContext.BaseDirectory, settings.Coverage.Directory);
		}
		_intervalo = TimeSpan.FromMilliseconds(Math.Max(10, settings.PollIntervalMilliseconds));
		_vida = TimeSpan.FromSeconds(Math.Max(1, settings.MockLifetimeSeconds));
		ProcessInstanceKey = processInstanceKey;
		_instancias[processInstanceKey] = 0;
		if (definitionKey is not null)
			_definicaoDaInstancia[processInstanceKey] = definitionKey;
	}

	/// <summary>Quantas vezes o mock desta user task a completou.</summary>
	public int UserTasksResolvidas(string elementId) => Contagem(_userTasksResolvidas, elementId);

	/// <summary>Quantas vezes este timer foi pulado.</summary>
	public int TimersPulados(string elementId) => Contagem(_timersPulados, elementId);

	/// <summary>Quantas vezes o mock correlacionou esta mensagem.</summary>
	public int MensagensCorrelacionadas(string nome) => Contagem(_mensagensCorrelacionadas, nome);

	/// <summary>Quantas vezes este sinal foi disparado na árvore.</summary>
	public int SinaisDisparados(string nome) => Contagem(_sinaisDisparados, nome);

	/// <summary>Quantas vezes o processo filho desta call activity foi mockado.</summary>
	public int FilhosMockados(string callActivityId) => Contagem(_filhosMockados, callActivityId);

	/// <summary>O que o teste declarou e o laço nunca usou: pistas de uma instância parada.</summary>
	public IReadOnlyList<string> NuncaUsados() =>
		_userTasks.Keys.Where(id => UserTasksResolvidas(id) == 0).Select(id => $"user task '{id}'")
			.Concat(_timers.Where(id => TimersPulados(id) == 0).Select(id => $"timer '{id}'"))
			.Concat(_mensagens.Keys.Where(n => MensagensCorrelacionadas(n) == 0).Select(n => $"message '{n}'"))
			.Concat(_sinais.Keys.Where(n => SinaisDisparados(n) == 0).Select(n => $"signal '{n}'"))
			.Concat(_filhos.Keys.Where(id => FilhosMockados(id) == 0).Select(id => $"child process of '{id}'"))
			.Concat(_mocks.Keys.Where(t => Invocacoes(t) == 0).Select(t => $"job type '{t}'"))
			.ToList();

	/// <summary>
	/// O que a árvore espera e este teste não responde: user tasks sem mock, mensagens, sinais.
	/// </summary>
	public async Task<IReadOnlyList<EsperaVista>> EsperasSemRespostaAsync(CancellationToken ct)
	{
		var esperas = await _gateway.SearchWaitStatesAsync(ProcessInstanceKey, ct).ConfigureAwait(false);
		return esperas
			.Where(e => e.Tipo != "JOB")
			.Where(e => !(e.Tipo == "USER_TASK" && _userTasks.ContainsKey(e.ElementId)))
			.Where(e => !(e.Tipo == "MESSAGE" && e.MessageName is not null && _mensagens.ContainsKey(e.MessageName)))
			.Where(e => !(e.Tipo == "SIGNAL" && e.SignalName is not null && _sinais.ContainsKey(e.SignalName)))
			.ToList();
	}

	private static int Contagem(ConcurrentDictionary<string, int> contagens, string chave) =>
		contagens.TryGetValue(chave, out var n) ? n : 0;
	/// <summary>A instância raiz.</summary>
	public string ProcessInstanceKey { get; }

	/// <summary>A raiz e as filhas que o laço já encontrou.</summary>
	public IReadOnlyCollection<string> Instancias => _instancias.Keys.ToArray();

	/// <summary>
	/// O que impede o teste de confiar no resultado: um job com outro worker, uma recusa inesperada.
	/// </summary>
	/// <remarks>Uma asserção que encontra algo aqui falha na hora, com isto na mensagem.</remarks>
	public IReadOnlyList<string> Problemas => _problemas.ToArray();

	/// <summary>A última falha de rede do laço, só para diagnóstico: o laço tenta de novo.</summary>
	public string? UltimaFalhaDeRede => _ultimaFalhaDeRede;

	/// <summary>Se o laço já parou.</summary>
	public bool Encerrado => Rodando.IsCompleted;

	/// <summary>O laço. Começa em <see cref="Iniciar"/>.</summary>
	public Task Rodando { get; private set; } = Task.CompletedTask;

	/// <summary>Quantas vezes o mock deste tipo resolveu um job.</summary>
	public int Invocacoes(string jobType) => _invocacoes.TryGetValue(jobType, out var n) ? n : 0;

	public void Iniciar()
	{
		Abertos.TryAdd(this, 0);
		Rodando = Task.Run(async () =>
		{
			try
			{
				await LacoAsync(_parar.Token).ConfigureAwait(false);
			}
			finally
			{
				Abertos.TryRemove(this, out _);
			}
		});
	}

	/// <summary>
	/// O que o fim do processo faz com este laço: para, e cancela a instância se ela não terminou.
	/// </summary>
	/// <remarks>
	/// Cancela sem perguntar o estado antes: a busca fica atrás do motor, e uma instância que já
	/// terminou só responde <c>NOT_FOUND</c>, que aqui não importa.
	/// </remarks>
	internal async Task EncerrarNaSaidaAsync(CancellationToken ct)
	{
		_parar.Cancel();

		if (!_instanciaTerminou)
		{
			try
			{
				await _gateway.CancelInstanceAsync(ProcessInstanceKey, ct).ConfigureAwait(false);
			}
			catch (Exception)
			{
				// Cortesia com o cluster; falhar aqui não muda nenhum teste. Uma instância que já
				// terminou e o laço ainda não viu responde NOT_FOUND, e a cobertura dela vale igual.
			}
		}

		// Uma instância que terminou há pouco ainda não foi registrada: o laço só pergunta o estado da
		// raiz a cada dez voltas. Se já foi, isto não faz nada.
		await RegistrarCoberturaAsync(ct).ConfigureAwait(false);
	}

	// Síncrono porque o ProcessExit não espera uma Task. Os cancelamentos vão juntos, e o prazo é
	// de todos: o processo já está saindo.
	internal static void EncerrarAbertos(TimeSpan prazo)
	{
		var abertos = Abertas;
		if (abertos.Count == 0)
			return;

		try
		{
			using var limite = new CancellationTokenSource(prazo);
			Task.WhenAll(abertos.Select(m => m.EncerrarNaSaidaAsync(limite.Token))).Wait(prazo);
		}
		catch (Exception)
		{
			// Idem.
		}
	}

	/// <summary>Para o laço. Para os testes do pacote; uma cadeia não precisa chamar.</summary>
	public Task PararAsync()
	{
		_parar.Cancel();
		return Rodando;
	}

	/// <summary>
	/// Os jobs que esperam nesta árvore sem mock que os resolva: o motivo mais comum de uma
	/// instância que não anda.
	/// </summary>
	public async Task<IReadOnlyList<JobPendente>> PendentesSemMockAsync(CancellationToken ct)
	{
		var todos = await _gateway.SearchCreatedJobsAsync(Instancias, null, ct).ConfigureAwait(false);
		return todos.Where(j => !_mocks.ContainsKey(j.Type)).ToList();
	}

	internal async Task LacoAsync(CancellationToken ct)
	{
		var cronometro = Stopwatch.StartNew();
		var volta = 0;

		while (!ct.IsCancellationRequested && cronometro.Elapsed < _vida)
		{
			try
			{
				await DescobrirFilhasAsync(ct).ConfigureAwait(false);

				if (_filhos.Count > 0)
					await InterceptarFilhasAsync(ct).ConfigureAwait(false);

				if (_mocks.Count > 0)
					await ResolverJobsAsync(ct).ConfigureAwait(false);

				if (_userTasks.Count > 0)
					await ResolverUserTasksAsync(ct).ConfigureAwait(false);

				if (_timers.Count > 0 || _sinais.Count > 0)
					await DispararEventosAsync(ct).ConfigureAwait(false);

				if (_mensagens.Count > 0)
					await CorrelacionarMensagensAsync(ct).ConfigureAwait(false);

				// O estado da raiz custa uma busca a mais; uma vez a cada dez voltas basta para
				// o laço não sobreviver muito à instância.
				if (++volta % 10 == 0 && await TerminouAsync(ct).ConfigureAwait(false))
				{
					await RegistrarCoberturaAsync(ct).ConfigureAwait(false);
					return;
				}

				_ultimaFalhaDeRede = null;
			}
			catch (OperationCanceledException) when (ct.IsCancellationRequested)
			{
				return;
			}
			catch (CamundaRecusouException e)
			{
				// Uma busca recusada (credencial, permissão) não melhora tentando de novo, e um laço
				// parado em silêncio faria toda asserção esperar o prazo inteiro sem dizer por quê.
				_problemas.Enqueue($"Camunda rejected the mocks' search: {e.Codigo}: {e.Message}");
				return;
			}
			catch (Exception e)
			{
				// Rede ou cluster ocupado: o laço tenta de novo na volta seguinte. Fica registrado
				// para o diagnóstico de uma asserção que esgotar o prazo.
				_ultimaFalhaDeRede = $"{e.GetType().Name}: {e.Message}";
			}

			try
			{
				await Task.Delay(_intervalo, ct).ConfigureAwait(false);
			}
			catch (OperationCanceledException)
			{
				return;
			}
		}

		if (!ct.IsCancellationRequested)
			await CancelarEsquecidaAsync().ConfigureAwait(false);
	}

	private async Task DescobrirFilhasAsync(CancellationToken ct)
	{
		var filhas = await _gateway.SearchChildInstancesAsync(Instancias, ct).ConfigureAwait(false);
		foreach (var filha in filhas)
		{
			_instancias.TryAdd(filha.ProcessInstanceKey, 0);
			_filhasVistas[filha.ProcessInstanceKey] = filha;
			if (filha.ProcessDefinitionKey is not null)
				_definicaoDaInstancia.TryAdd(filha.ProcessInstanceKey, filha.ProcessDefinitionKey);
		}
	}

	// O filho mockado roda de verdade até ser interceptado: então o pacote encerra os elementos ativos
	// dele e ativa o evento de fim, com as variáveis do teste (forma A do plano). O filho termina como
	// terminaria, e o mapeamento de saída da call activity roda (Fase 0, S3). Para um erro, o evento de
	// fim com aquele código, ou, sem um, a borda de erro da call activity no pai (forma B).
	private async Task InterceptarFilhasAsync(CancellationToken ct)
	{
		var elementosDoPai = new Dictionary<string, IReadOnlyList<ElementoVisto>>(StringComparer.Ordinal);

		foreach (var filha in _filhasVistas.Values.Where(f => f.ParentElementInstanceKey is not null && f.ParentProcessInstanceKey is not null))
		{
			var tratado = "filho:" + filha.ProcessInstanceKey;
			if (_tratados.ContainsKey(tratado))
				continue;

			var pai = filha.ParentProcessInstanceKey!;
			if (!elementosDoPai.TryGetValue(pai, out var doPai))
				elementosDoPai[pai] = doPai = await _gateway.SearchElementsAsync(pai, ct).ConfigureAwait(false);

			// A busca ainda não mostra a call activity: tenta na volta seguinte.
			var chamada = doPai.FirstOrDefault(e => e.ElementInstanceKey == filha.ParentElementInstanceKey);
			if (chamada is null)
				continue;

			if (!_filhos.TryGetValue(chamada.ElementId, out var mock))
			{
				_tratados.TryAdd(tratado, 0);
				continue;
			}

			if (EstadosFinais.Contains(filha.State))
			{
				_tratados.TryAdd(tratado, 0);
				_problemas.Enqueue(
					$"MockChildProcess('{chamada.ElementId}'): the child process {filha.ProcessId} (instance " +
					$"{filha.ProcessInstanceKey}) was already {filha.State} when the mock found it, so it ran for real. A child " +
					"with nothing to wait for (no job, timer, message or user task) ends before it can be intercepted.");
				continue;
			}

			var modeloDaFilha = await ModeloAsync(filha.ProcessInstanceKey, ct).ConfigureAwait(false);
			var fim = mock.ErrorCode is null
				? modeloDaFilha?.EventoDeFim()
				: modeloDaFilha?.EventoDeFimComErro(mock.ErrorCode);

			try
			{
				if (fim is not null)
				{
					var ativos = (await _gateway.SearchElementsAsync(filha.ProcessInstanceKey, ct).ConfigureAwait(false))
						.Where(e => e.State == "ACTIVE" && e.Type != "PROCESS")
						.Select(e => e.ElementInstanceKey)
						.ToList();

					// Os elementos da filha ainda não aparecem na busca: tenta na volta seguinte.
					if (ativos.Count == 0)
						continue;

					await _gateway.ModifyInstanceAsync(filha.ProcessInstanceKey, ativos, new[] { fim }, null, mock.Variables, ct)
						.ConfigureAwait(false);
				}
				else if (mock.ErrorCode is null)
				{
					_tratados.TryAdd(tratado, 0);
					_problemas.Enqueue(
						$"MockChildProcess('{chamada.ElementId}'): the child process {filha.ProcessId} has no plain end event outside " +
						"subprocesses, so the mock has nowhere to end it.");
					continue;
				}
				else
				{
					var modeloDoPai = await ModeloAsync(pai, ct).ConfigureAwait(false);
					var borda = modeloDoPai?.BordaDeErro(chamada.ElementId, mock.ErrorCode);
					if (borda is null || borda.Saidas.Count == 0)
					{
						_tratados.TryAdd(tratado, 0);
						_problemas.Enqueue(
							$"MockChildProcess('{chamada.ElementId}').ThenThrowBpmnError('{mock.ErrorCode}'): the child process " +
							$"{filha.ProcessId} has no error end event with that code, and the call activity has no boundary error " +
							"event that catches it, so there is no path for the error to take.");
						continue;
					}

					var escopo = borda.Escopo is null
						? null
						: doPai.FirstOrDefault(e => e.ElementId == borda.Escopo && e.State == "ACTIVE")?.ElementInstanceKey;

					// Encerrar a call activity encerra o filho junto (Fase 0, S4).
					await _gateway.ModifyInstanceAsync(pai, new[] { chamada.ElementInstanceKey }, borda.Saidas, escopo, mock.Variables, ct)
						.ConfigureAwait(false);
					Disparou(pai, borda.Id);
				}

				_tratados.TryAdd(tratado, 0);
				_filhosMockados.AddOrUpdate(chamada.ElementId, 1, (_, n) => n + 1);
			}
			catch (CamundaRecusouException e) when (e.Codigo == "NOT_FOUND")
			{
				// A filha, ou o elemento, terminou entre a busca e a modificação.
				_tratados.TryAdd(tratado, 0);
			}
			catch (CamundaRecusouException e)
			{
				_tratados.TryAdd(tratado, 0);
				_problemas.Enqueue(
					$"Camunda rejected mocking the child process of '{chamada.ElementId}' (instance {filha.ProcessInstanceKey}): " +
					$"{e.Codigo}: {e.Message}");
			}
		}
	}

	// A busca de esperas filtra pela raiz, e assim cobre as filhas numa ida só.
	private async Task ResolverUserTasksAsync(CancellationToken ct)
	{
		var esperas = await _gateway.SearchWaitStatesAsync(ProcessInstanceKey, ct).ConfigureAwait(false);

		foreach (var espera in esperas.Where(e => e.Tipo == "USER_TASK" && e.UserTaskKey is not null))
		{
			if (!_userTasks.TryGetValue(espera.ElementId, out var mock))
				continue;
			if (!_tratados.TryAdd("user-task:" + espera.UserTaskKey, 0))
				continue;

			try
			{
				await _gateway.CompleteUserTaskAsync(espera.UserTaskKey!, mock.Variables, ct).ConfigureAwait(false);
				_userTasksResolvidas.AddOrUpdate(espera.ElementId, 1, (_, n) => n + 1);
			}
			catch (CamundaRecusouException e) when (e.Codigo == "NOT_FOUND")
			{
				// A task deixou de existir entre a busca e o comando (uma borda a interrompeu).
			}
			catch (CamundaRecusouException e)
			{
				_problemas.Enqueue(
					$"Camunda rejected completing user task '{espera.ElementId}' (task {espera.UserTaskKey}, instance " +
					$"{espera.ProcessInstanceKey}): {e.Codigo}: {e.Message}");
			}
			catch (Exception)
			{
				_tratados.TryRemove("user-task:" + espera.UserTaskKey, out _);
				throw;
			}
		}
	}

	// Timers e sinais: o pacote dispara o evento só nesta árvore, modificando a instância. A borda e o
	// evento depois de gateway de evento não aparecem nas esperas (medido na 8.10.4): quem diz que a
	// instância chegou neles é o elemento ativo que o BPMN aponta.
	private async Task DispararEventosAsync(CancellationToken ct)
	{
		foreach (var instancia in Instancias)
		{
			var modelo = await ModeloAsync(instancia, ct).ConfigureAwait(false);
			if (modelo is null)
				continue;

			var planos = new List<(PuloDeEvento Pulo, string Descricao, ConcurrentDictionary<string, int> Contagem, string Chave, object? Variaveis)>();
			var recusas = new List<string>();

			foreach (var timer in _timers)
			{
				var plano = modelo.PlanejarPulo(timer, out var recusa);
				if (recusa is not null) recusas.Add(recusa);
				if (plano is not null) planos.Add((plano, $"skipping timer '{timer}'", _timersPulados, timer, null));
			}

			foreach (var sinal in _sinais.Values)
			{
				foreach (var plano in modelo.PlanejarSinal(sinal.Nome, recusas))
					planos.Add((plano, $"triggering signal '{sinal.Nome}' at '{plano.EventoId}'", _sinaisDisparados, sinal.Nome, sinal.Variables));
			}

			foreach (var recusa in recusas.Where(r => _recusasJaDitas.TryAdd(r, 0)))
				_problemas.Enqueue(recusa);

			if (planos.Count == 0)
				continue;

			var elementos = await _gateway.SearchElementsAsync(instancia, ct).ConfigureAwait(false);
			var ativos = elementos.Where(e => e.State == "ACTIVE").ToList();

			foreach (var (plano, descricao, contagem, chave, variaveis) in planos)
			{
				foreach (var esperando in ativos.Where(e => e.ElementId == plano.ElementoEsperando))
				{
					var tratado = $"evento:{plano.EventoId}:{esperando.ElementInstanceKey}";
					if (!_tratados.TryAdd(tratado, 0))
						continue;

					// Dentro de um subprocesso, ativa no mesmo subprocesso, e não num novo.
					var escopo = plano.Escopo is null
						? null
						: ativos.FirstOrDefault(e => e.ElementId == plano.Escopo)?.ElementInstanceKey;

					try
					{
						await _gateway.ModifyInstanceAsync(
							instancia,
							plano.Encerrar ? new[] { esperando.ElementInstanceKey } : Array.Empty<string>(),
							plano.Ativar,
							escopo,
							variaveis,
							ct).ConfigureAwait(false);

						contagem.AddOrUpdate(chave, 1, (_, n) => n + 1);
						Disparou(instancia, plano.EventoId);
					}
					catch (CamundaRecusouException e) when (e.Codigo == "NOT_FOUND")
					{
						// O elemento saiu de lá entre a busca e a modificação.
					}
					catch (CamundaRecusouException e)
					{
						_problemas.Enqueue(
							$"Camunda rejected {descricao} (waiting at '{plano.ElementoEsperando}', instance {instancia}): " +
							$"{e.Codigo}: {e.Message}");
					}
					catch (Exception)
					{
						_tratados.TryRemove(tratado, out _);
						throw;
					}
				}
			}
		}
	}

	// Mensagens: correlacionadas de verdade, com a chave que a própria assinatura traz. Mas uma
	// correlação vai para quem assinou primeiro, em qualquer instância do cluster (medido na 8.10.4):
	// antes de correlacionar, o laço confere que ninguém de fora da árvore espera a mesma mensagem
	// com a mesma chave, e depois, que foi esta árvore que a recebeu.
	private async Task CorrelacionarMensagensAsync(CancellationToken ct)
	{
		var arvore = Instancias;
		var assinaturas = await _gateway.SearchOpenMessageSubscriptionsAsync(arvore, _mensagens.Keys.ToArray(), null, ct)
			.ConfigureAwait(false);

		foreach (var assinatura in assinaturas)
		{
			if (!_mensagens.TryGetValue(assinatura.MessageName, out var mock))
				continue;

			var tratado = $"mensagem:{assinatura.MessageName}:{assinatura.ElementInstanceKey}:{assinatura.ElementId}";
			if (!_tratados.TryAdd(tratado, 0))
				continue;

			var deFora = (await _gateway.SearchOpenMessageSubscriptionsAsync(
					null, new[] { assinatura.MessageName }, assinatura.CorrelationKey, ct).ConfigureAwait(false))
				.Where(a => !arvore.Contains(a.ProcessInstanceKey))
				.ToList();
			if (deFora.Count > 0)
			{
				_problemas.Enqueue(
					$"Message '{assinatura.MessageName}' with correlation key '{assinatura.CorrelationKey}' is also awaited by " +
					$"instance {string.Join(", ", deFora.Select(a => a.ProcessInstanceKey).Distinct())}, outside this test, so the " +
					"mock did not correlate it: Camunda delivers it to whoever subscribed first. Use a correlation key unique " +
					"to the test, such as a Guid in the start variables.");
				continue;
			}

			try
			{
				var recebeu = await _gateway.CorrelateMessageAsync(
					assinatura.MessageName, assinatura.CorrelationKey, mock.Variables, _tenantId, ct).ConfigureAwait(false);

				if (!arvore.Contains(recebeu))
					_problemas.Enqueue(
						$"Message '{assinatura.MessageName}' with correlation key '{assinatura.CorrelationKey}' went to instance " +
						$"{recebeu}, outside this test. Use a correlation key unique to the test.");
				else
					_mensagensCorrelacionadas.AddOrUpdate(assinatura.MessageName, 1, (_, n) => n + 1);
			}
			catch (CamundaRecusouException e) when (e.Codigo == "NOT_FOUND")
			{
				// A assinatura fechou entre a busca e a correlação (a espera foi interrompida).
			}
			catch (CamundaRecusouException e)
			{
				_problemas.Enqueue(
					$"Camunda rejected correlating message '{assinatura.MessageName}' with correlation key " +
					$"'{assinatura.CorrelationKey}' (waiting at '{assinatura.ElementId}'): {e.Codigo}: {e.Message}");
			}
			catch (Exception)
			{
				_tratados.TryRemove(tratado, out _);
				throw;
			}
		}
	}

	/// <summary>
	/// Uma asserção viu a instância completar: registra a cobertura agora, sem esperar o laço perceber.
	/// </summary>
	internal async Task<bool> InstanciaTerminouAsync(CancellationToken ct)
	{
		_instanciaTerminou = true;
		await RegistrarCoberturaAsync(ct).ConfigureAwait(false);
		return true;
	}

	// Uma vez por laço: quando a instância termina, ou quando o fim da rodada ou o prazo a cancela.
	// Os elementos da árvore inteira, cada instância na versão que ela rodou.
	private async Task RegistrarCoberturaAsync(CancellationToken ct)
	{
		if (_cobertura is null || Interlocked.Exchange(ref _coberturaRegistrada, 1) == 1)
			return;

		foreach (var instancia in Instancias)
		{
			try
			{
				if (await ModeloAsync(instancia, ct).ConfigureAwait(false) is null)
					continue;
				if (!_definicaoDaInstancia.TryGetValue(instancia, out var definicao) || !_xmls.TryGetValue(definicao, out var xml))
					continue;

				var vista = await _gateway.GetInstanceAsync(instancia, ct).ConfigureAwait(false);
				var elementos = await _gateway.SearchElementsAsync(instancia, ct).ConfigureAwait(false);

				_cobertura.Registrar(
					definicao, vista?.ProcessId ?? instancia, vista?.Version, xml,
					elementos.Where(e => e.Type != "PROCESS").Select(e => e.ElementId)
						.Concat(_disparados.TryGetValue(instancia, out var disparados) ? disparados.Keys : Enumerable.Empty<string>()));
			}
			catch (Exception)
			{
				// A cobertura é um extra: uma instância que não deu para ler fica de fora do relatório.
			}
		}

		// Grava a cada instância, e não só no fim: o dotnet test pode encerrar o processo antes de o fim
		// da rodada terminar de gravar.
		if (ReferenceEquals(_cobertura, CoberturaDaSuite.Global))
			EscreverCobertura();
	}

	private async Task<ModeloBpmn?> ModeloAsync(string instancia, CancellationToken ct)
	{
		if (!_definicaoDaInstancia.TryGetValue(instancia, out var definicao))
		{
			// A raiz iniciada sem a chave da definição (os testes do pacote), ou uma filha antiga.
			var vista = await _gateway.GetInstanceAsync(instancia, ct).ConfigureAwait(false);
			if (vista?.ProcessDefinitionKey is null)
				return null;

			definicao = _definicaoDaInstancia.GetOrAdd(instancia, vista.ProcessDefinitionKey);
		}

		if (_modelos.TryGetValue(definicao, out var modelo))
			return modelo;

		var xml = await _gateway.GetProcessDefinitionXmlAsync(definicao, ct).ConfigureAwait(false);
		_xmls.TryAdd(definicao, xml);
		return _modelos.GetOrAdd(definicao, ModeloBpmn.Ler(xml));
	}

	private async Task ResolverJobsAsync(CancellationToken ct)
	{
		var jobs = await _gateway.SearchCreatedJobsAsync(Instancias, _mocks.Keys.ToArray(), ct)
			.ConfigureAwait(false);

		foreach (var job in jobs)
		{
			// A busca continua mostrando como CREATED, por um tempo, um job que já foi resolvido.
			if (!_tratados.TryAdd(job.JobKey, 0))
				continue;

			if (!_mocks.TryGetValue(job.Type, out var mock))
				continue;

			try
			{
				if (mock.Acao == AcaoDoMock.LancarErroBpmn)
					await _gateway.ThrowBpmnErrorAsync(job.JobKey, mock.ErrorCode!, mock.ErrorMessage, mock.Variables, ct)
						.ConfigureAwait(false);
				else if (mock.Acao == AcaoDoMock.Falhar)
					await _gateway.FailJobAsync(job.JobKey, mock.ErrorMessage ?? "Failed by the test's mock.", ct).ConfigureAwait(false);
				else
					await _gateway.CompleteJobAsync(job.JobKey, mock.Variables, ct).ConfigureAwait(false);

				_invocacoes.AddOrUpdate(job.Type, 1, (_, n) => n + 1);
			}
			catch (CamundaRecusouException e) when (e.Codigo == "NOT_FOUND")
			{
				// O job deixou de existir entre a busca e o comando: a instância foi cancelada, ou
				// uma boundary event o interrompeu. Não há o que resolver.
			}
			catch (CamundaRecusouException e) when (e.Codigo == "INVALID_STATE")
			{
				_problemas.Enqueue(
					$"Job '{job.Type}' at element '{job.ElementId}' (job {job.JobKey}, instance {job.ProcessInstanceKey}) " +
					"is held by another worker with a lease, so the mock could not " + mock.Descrever() + " it. " +
					"A real worker for this job type is running on the cluster; stop it, or do not mock this type here " +
					"(in an environment with real workers, such as staging, mock only the job types that have no worker there). " +
					$"Camunda said: {e.Message}");
			}
			catch (CamundaRecusouException e)
			{
				_problemas.Enqueue(
					$"Camunda rejected the mock's attempt to {mock.Descrever()} job '{job.Type}' at element " +
					$"'{job.ElementId}' (job {job.JobKey}): {e.Codigo}: {e.Message}");
			}
			catch (Exception)
			{
				// Falha de rede no comando: o job volta a ser candidato na volta seguinte.
				_tratados.TryRemove(job.JobKey, out _);
				throw;
			}
		}
	}

	private async Task<bool> TerminouAsync(CancellationToken ct)
	{
		var raiz = await _gateway.GetInstanceAsync(ProcessInstanceKey, ct).ConfigureAwait(false);
		_instanciaTerminou = raiz is not null && EstadosFinais.Contains(raiz.State);
		return _instanciaTerminou;
	}

	private async Task CancelarEsquecidaAsync()
	{
		try
		{
			using var prazo = new CancellationTokenSource(TimeSpan.FromSeconds(10));
			var raiz = await _gateway.GetInstanceAsync(ProcessInstanceKey, prazo.Token).ConfigureAwait(false);
			if (raiz is not null && !EstadosFinais.Contains(raiz.State))
				await _gateway.CancelInstanceAsync(ProcessInstanceKey, prazo.Token).ConfigureAwait(false);
			await RegistrarCoberturaAsync(prazo.Token).ConfigureAwait(false);
		}
		catch (Exception)
		{
			// Cancelar é cortesia com o cluster compartilhado; falhar aqui não muda o teste.
		}
	}
}
