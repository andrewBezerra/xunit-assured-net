using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using XUnitAssured.Camunda.Gateway;

namespace XUnitAssured.Camunda.Mocks;

/// <summary>
/// Os mocks trabalhando para uma instância e para as instâncias que ela chamar.
/// </summary>
/// <remarks>
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
/// O laço termina quando a instância termina, ou no fim de <c>mockLifetimeSeconds</c>; nesse
/// caso a instância ainda ativa é cancelada, para não ficar esquecida num cluster compartilhado.
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

	public MocksDaInstancia(
		ICamundaGateway gateway,
		string processInstanceKey,
		IReadOnlyDictionary<string, JobMock> mocks,
		CamundaSettings settings)
	{
		_gateway = gateway;
		_mocks = new Dictionary<string, JobMock>(mocks, StringComparer.Ordinal);
		_intervalo = TimeSpan.FromMilliseconds(Math.Max(10, settings.PollIntervalMilliseconds));
		_vida = TimeSpan.FromSeconds(Math.Max(1, settings.MockLifetimeSeconds));
		ProcessInstanceKey = processInstanceKey;
		_instancias[processInstanceKey] = 0;
	}

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

	public void Iniciar() => Rodando = Task.Run(() => LacoAsync(_parar.Token));

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

				if (_mocks.Count > 0)
					await ResolverJobsAsync(ct).ConfigureAwait(false);

				// O estado da raiz custa uma busca a mais; uma vez a cada dez voltas basta para
				// o laço não sobreviver muito à instância.
				if (++volta % 10 == 0 && await TerminouAsync(ct).ConfigureAwait(false))
					return;

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
			_instancias.TryAdd(filha.ProcessInstanceKey, 0);
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
					"A real worker for this job type is running on the cluster; stop it, or do not mock this type here. " +
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
		return raiz is not null && EstadosFinais.Contains(raiz.State);
	}

	private async Task CancelarEsquecidaAsync()
	{
		try
		{
			using var prazo = new CancellationTokenSource(TimeSpan.FromSeconds(10));
			var raiz = await _gateway.GetInstanceAsync(ProcessInstanceKey, prazo.Token).ConfigureAwait(false);
			if (raiz is not null && !EstadosFinais.Contains(raiz.State))
				await _gateway.CancelInstanceAsync(ProcessInstanceKey, prazo.Token).ConfigureAwait(false);
		}
		catch (Exception)
		{
			// Cancelar é cortesia com o cluster compartilhado; falhar aqui não muda o teste.
		}
	}
}
