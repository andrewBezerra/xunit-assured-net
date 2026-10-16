// Fase 0 do XUnitAssured.Camunda (docs/camunda-plano.md): prova, contra um Camunda 8.10 de
// verdade, que o modelo de isolamento pela chave da instância funciona num cluster
// compartilhado. Não é teste: imprime o que aconteceu e quanto cada passo levou.
//
//   CAMUNDA_REST_ADDRESS=http://192.168.0.115:8080   (padrão: http://localhost:18080, túnel SSH)
//   CAMUNDA_BASIC_AUTH_PASSWORD=<senha do usuário xunit>
//   dotnet run
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using Camunda.Orchestration.Sdk;

var endereco = Environment.GetEnvironmentVariable("CAMUNDA_REST_ADDRESS") ?? "http://localhost:18080";
using var client = CamundaClient.Create(new CamundaOptions
{
    Config = new Dictionary<string, string>
    {
        ["CAMUNDA_REST_ADDRESS"] = endereco,
        ["CAMUNDA_AUTH_STRATEGY"] = "BASIC",
        ["CAMUNDA_BASIC_AUTH_USERNAME"] = "xunit",
        ["CAMUNDA_BASIC_AUTH_PASSWORD"] = Environment.GetEnvironmentVariable("CAMUNDA_BASIC_AUTH_PASSWORD")
            ?? throw new InvalidOperationException("Defina CAMUNDA_BASIC_AUTH_PASSWORD."),
    },
});

var medidas = new ConcurrentBag<(string Cenario, string Passo, long Ms)>();
var instancias = new ConcurrentBag<ProcessInstanceKey>();
var resultados = new List<(string Cenario, bool Ok, string Nota)>();

var topologia = await client.GetTopologyAsync();
Console.WriteLine($"Conectado em {endereco}: gateway {topologia.GatewayVersion}");

// 1. Deploy do pai e do filho juntos: o pai usa bindingType="deployment".
var definicao = await Implantar();
Console.WriteLine($"fase0-pedido implantado: processDefinitionKey {definicao.Value}");

await Cenario("S1 filho real, jobs resolvidos pela chave", async c =>
{
    var pik = await Iniciar(c, new { pedidoId = 1 });
    await ResolverJob(c, pik, "fase0-cobrar", job => client.CompleteJobAsync(job, new JobCompletionRequest { Variables = new { pago = true } }));
    var filho = await EsperarFilho(c, pik);
    await ResolverJob(c, filho, "fase0-despachar", job => client.CompleteJobAsync(job, new JobCompletionRequest { Variables = new { rastreio = "BR-REAL" } }));
    await EsperarElementoAtivo(c, pik, "aguardar");
    await PularTimer(c, pik, "aguardar", "concluido");
    await EsperarEstado(c, pik, ProcessInstanceStateEnum.COMPLETED);
    return Conferir(await Variavel(pik, "codigoRastreio"), "\"BR-REAL\"");
});

await Cenario("S2 erro BPMN lançado pela chave, sem ativar", async c =>
{
    var pik = await Iniciar(c, new { pedidoId = 2 });
    await ResolverJob(c, pik, "fase0-cobrar", job => client.ThrowJobErrorAsync(job, new JobErrorRequest { ErrorCode = "PAGAMENTO_RECUSADO", ErrorMessage = "mock" }));
    await EsperarEstado(c, pik, ProcessInstanceStateEnum.COMPLETED);
    var recusado = await ElementosCompletados(pik, "recusado");
    return (recusado > 0, $"elemento 'recusado' completado {recusado}x");
});

await Cenario("S3 mock do filho, forma A (fim do filho ativado)", async c =>
{
    var pik = await Iniciar(c, new { pedidoId = 3 });
    await ResolverJob(c, pik, "fase0-cobrar", job => client.CompleteJobAsync(job, new JobCompletionRequest { Variables = new { pago = true } }));
    var filho = await EsperarFilho(c, pik);
    await EsperarJob(c, filho, "fase0-despachar");
    await Medir(c, "modificar filho", () => client.ModifyProcessInstanceAsync(filho, new ProcessInstanceModificationInstruction
    {
        TerminateInstructions = [new ProcessInstanceModificationTerminateByIdInstruction { ElementId = ElementId.AssumeExists("despachar") }],
        ActivateInstructions =
        [
            new ProcessInstanceModificationActivateInstruction
            {
                ElementId = ElementId.AssumeExists("fim-envio"),
                VariableInstructions = [new ModifyProcessInstanceVariableInstruction { Variables = new { rastreio = "BR-A" } }],
            },
        ],
    }));
    await EsperarElementoAtivo(c, pik, "aguardar");
    var rastreio = await Variavel(pik, "codigoRastreio");
    await PularTimer(c, pik, "aguardar", "concluido");
    await EsperarEstado(c, pik, ProcessInstanceStateEnum.COMPLETED);
    return Conferir(rastreio, "\"BR-A\"", "o mapeamento de saída da call activity rodou");
});

await Cenario("S4 mock do filho, forma B (call activity encerrada no pai)", async c =>
{
    var pik = await Iniciar(c, new { pedidoId = 4 });
    await ResolverJob(c, pik, "fase0-cobrar", job => client.CompleteJobAsync(job, new JobCompletionRequest { Variables = new { pago = true } }));
    var filho = await EsperarFilho(c, pik);
    await Medir(c, "modificar pai", () => client.ModifyProcessInstanceAsync(pik, new ProcessInstanceModificationInstruction
    {
        TerminateInstructions = [new ProcessInstanceModificationTerminateByIdInstruction { ElementId = ElementId.AssumeExists("chamar-envio") }],
        ActivateInstructions =
        [
            new ProcessInstanceModificationActivateInstruction
            {
                ElementId = ElementId.AssumeExists("aguardar"),
                VariableInstructions = [new ModifyProcessInstanceVariableInstruction { Variables = new { codigoRastreio = "BR-B" } }],
            },
        ],
    }));
    await EsperarEstado(c, filho, ProcessInstanceStateEnum.TERMINATED);
    var rastreio = await Variavel(pik, "codigoRastreio");
    await PularTimer(c, pik, "aguardar", "concluido");
    await EsperarEstado(c, pik, ProcessInstanceStateEnum.COMPLETED);
    return Conferir(rastreio, "\"BR-B\"", "filho TERMINATED");
});

await Cenario("S5 worker estranho ativa o job antes do teste", async c =>
{
    var notas = new List<string>();
    foreach (var comLease in new[] { false, true })
    {
        var pik = await Iniciar(c, new { pedidoId = comLease ? 52 : 51 });
        var job = await EsperarJob(c, pik, "fase0-cobrar");
        var ativados = await client.ActivateJobsAsync(new JobActivationRequest
        {
            Type = "fase0-cobrar", Worker = "estranho", Timeout = 120_000, MaxJobsToActivate = 50, RequestTimeout = 2_000, WithLease = comLease,
        });
        var meu = ativados.Jobs.FirstOrDefault(j => j.ProcessInstanceKey == pik);
        if (meu is null) { notas.Add($"lease={comLease}: o estranho não pegou o job"); continue; }

        // O que a busca mostra de um job ativado por outro: é por aqui que o pacote detecta.
        var sw = Stopwatch.StartNew();
        JobSearchResult? visto = null;
        while (sw.ElapsedMilliseconds < 10_000)
        {
            visto = (await client.SearchJobsAsync(new JobSearchQuery { Filter = new JobFilter { JobKey = meu.JobKey } })).Items.FirstOrDefault();
            if (visto is { Worker: "estranho" }) break;
            await Task.Delay(50);
        }
        medidas.Add((c, $"ativação visível na busca (lease={comLease})", sw.ElapsedMilliseconds));
        notas.Add($"lease={comLease}: busca → state={visto?.State} worker='{visto?.Worker}' deadline={(visto?.Deadline is null ? "nulo" : "preenchido")}");

        try
        {
            await client.CompleteJobAsync(meu.JobKey, new JobCompletionRequest { Variables = new { pago = true } });
            notas.Add($"lease={comLease}: o teste COMPLETOU o job já ativado pelo estranho");
        }
        catch (Exception e)
        {
            notas.Add($"lease={comLease}: completar foi rejeitado: {Resumo(e)}");
        }
    }
    return (true, string.Join(" | ", notas));
});

await Cenario("S6 cinco instâncias em paralelo, mesmo tipo, respostas diferentes", async c =>
{
    var tarefas = Enumerable.Range(1, 5).Select(async n =>
    {
        var cn = $"{c}#{n}";
        var pik = await Iniciar(cn, new { pedidoId = 600 + n });
        await ResolverJob(cn, pik, "fase0-cobrar", job => client.CompleteJobAsync(job, new JobCompletionRequest { Variables = new { pago = true } }));
        var filho = await EsperarFilho(cn, pik);
        await ResolverJob(cn, filho, "fase0-despachar", job => client.CompleteJobAsync(job, new JobCompletionRequest { Variables = new { rastreio = $"BR-P{n}" } }));
        await EsperarElementoAtivo(cn, pik, "aguardar");
        await PularTimer(cn, pik, "aguardar", "concluido");
        await EsperarEstado(cn, pik, ProcessInstanceStateEnum.COMPLETED);
        return await Variavel(pik, "codigoRastreio") == $"\"BR-P{n}\"";
    });
    var ok = await Task.WhenAll(tarefas);
    return (ok.All(x => x), $"{ok.Count(x => x)}/5 instâncias com o próprio rastreio");
});

// Instância ativa no fim (S5 deixa) é cancelada: o histórico fica para o Operate.
foreach (var pik in instancias)
{
    var pi = await client.GetProcessInstanceAsync(pik);
    if (pi.State == ProcessInstanceStateEnum.ACTIVE)
        await client.CancelProcessInstanceAsync(pik, new CancelProcessInstanceRequest());
}

Console.WriteLine();
Console.WriteLine("== Resultados");
foreach (var (cen, ok, nota) in resultados) Console.WriteLine($"{(ok ? "OK  " : "FALHOU")} {cen}: {nota}");
Console.WriteLine();
Console.WriteLine("== Latências (ms): mínimo / mediana / máximo / n");
foreach (var g in medidas.GroupBy(m => m.Passo).OrderBy(g => g.Key))
{
    var v = g.Select(m => m.Ms).Order().ToArray();
    Console.WriteLine($"{g.Key,-48} {v[0],6} {v[v.Length / 2],6} {v[^1],6} {v.Length,4}");
}
Console.WriteLine();
Console.WriteLine("Instâncias criadas (abrir no Operate): " + string.Join(", ", instancias.Select(k => k.Value)));
return resultados.All(r => r.Ok) ? 0 : 1;

// ---------------------------------------------------------------------------------------

async Task<ProcessDefinitionKey> Implantar()
{
    var conteudo = new MultipartFormDataContent();
    foreach (var arquivo in new[] { "fase0-pedido.bpmn", "fase0-envio.bpmn" })
        conteudo.Add(new ByteArrayContent(await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "bpmn", arquivo))), "resources", arquivo);
    var r = await client.CreateDeploymentAsync(conteudo);
    return r.Deployments.Select(d => d.ProcessDefinition).First(p => p?.ProcessDefinitionId.Value == "fase0-pedido")!.ProcessDefinitionKey;
}

async Task Cenario(string nome, Func<string, Task<(bool Ok, string Nota)>> corpo)
{
    var sw = Stopwatch.StartNew();
    try
    {
        var (ok, nota) = await corpo(nome);
        resultados.Add((nome, ok, $"{nota} [{sw.ElapsedMilliseconds} ms]"));
    }
    catch (Exception e)
    {
        resultados.Add((nome, false, $"exceção: {Resumo(e)} [{sw.ElapsedMilliseconds} ms]"));
    }
    Console.WriteLine($"{resultados[^1].Cenario}: {(resultados[^1].Ok ? "OK" : "FALHOU")}");
}

async Task<ProcessInstanceKey> Iniciar(string c, object variaveis)
{
    var r = await MedirR(c, "iniciar instância (pela definitionKey)", () => client.CreateProcessInstanceAsync(
        new ProcessInstanceCreationInstructionByKey { ProcessDefinitionKey = definicao, Variables = variaveis }));
    instancias.Add(r.ProcessInstanceKey);
    return r.ProcessInstanceKey;
}

async Task<JobKey> EsperarJob(string c, ProcessInstanceKey pik, string tipo)
{
    var job = await Esperar(c, $"job '{tipo}' aparecer na busca", async () =>
        (await client.SearchJobsAsync(new JobSearchQuery
        {
            Filter = new JobFilter { ProcessInstanceKey = pik, Type = tipo, State = new JobStateFilterProperty { Eq = JobStateEnum.CREATED } },
        })).Items.FirstOrDefault());
    return job.JobKey;
}

async Task ResolverJob(string c, ProcessInstanceKey pik, string tipo, Func<JobKey, Task> resolver)
{
    var job = await EsperarJob(c, pik, tipo);
    await Medir(c, "resolver job pela chave", () => resolver(job));
}

async Task<ProcessInstanceKey> EsperarFilho(string c, ProcessInstanceKey pai) =>
    (await Esperar(c, "instância filha aparecer na busca", async () =>
        (await client.SearchProcessInstancesAsync(new ProcessInstanceSearchQuery
        {
            Filter = new ProcessInstanceFilter { ParentProcessInstanceKey = pai },
        })).Items.FirstOrDefault())).ProcessInstanceKey;

Task EsperarElementoAtivo(string c, ProcessInstanceKey pik, string elemento) =>
    Esperar(c, $"elemento ativo na busca", async () =>
        (await client.SearchElementInstancesAsync(new ElementInstanceSearchQuery
        {
            Filter = new ElementInstanceFilter
            {
                ProcessInstanceKey = pik,
                ElementId = ElementId.AssumeExists(elemento),
                State = new ElementInstanceStateFilterProperty { Eq = ElementInstanceStateEnum.ACTIVE },
            },
        })).Items.FirstOrDefault());

Task EsperarEstado(string c, ProcessInstanceKey pik, ProcessInstanceStateEnum estado) =>
    Esperar(c, $"instância {estado} na busca", async () =>
        (await client.SearchProcessInstancesAsync(new ProcessInstanceSearchQuery
        {
            Filter = new ProcessInstanceFilter { ProcessInstanceKey = pik },
        })).Items.FirstOrDefault(p => p.State == estado));

Task PularTimer(string c, ProcessInstanceKey pik, string timer, string proximo) =>
    Medir(c, "pular timer (modificação)", () => client.ModifyProcessInstanceAsync(pik, new ProcessInstanceModificationInstruction
    {
        TerminateInstructions = [new ProcessInstanceModificationTerminateByIdInstruction { ElementId = ElementId.AssumeExists(timer) }],
        ActivateInstructions = [new ProcessInstanceModificationActivateInstruction { ElementId = ElementId.AssumeExists(proximo) }],
    }));

async Task<string?> Variavel(ProcessInstanceKey pik, string nome)
{
    var sw = Stopwatch.StartNew();
    while (sw.ElapsedMilliseconds < 10_000)
    {
        var v = (await client.SearchVariablesAsync(new VariableSearchQuery
        {
            Filter = new VariableFilter { ProcessInstanceKey = pik, Name = nome },
        })).Items.FirstOrDefault();
        if (v is not null) return v.Value;
        await Task.Delay(50);
    }
    return null;
}

async Task<int> ElementosCompletados(ProcessInstanceKey pik, string elemento) =>
    (await client.SearchElementInstancesAsync(new ElementInstanceSearchQuery
    {
        Filter = new ElementInstanceFilter
        {
            ProcessInstanceKey = pik,
            ElementId = ElementId.AssumeExists(elemento),
            State = new ElementInstanceStateFilterProperty { Eq = ElementInstanceStateEnum.COMPLETED },
        },
    })).Items.Count;

async Task<T> Esperar<T>(string c, string passo, Func<Task<T?>> tentar, int prazoMs = 15_000) where T : class
{
    var sw = Stopwatch.StartNew();
    while (true)
    {
        if (await tentar() is { } r) { medidas.Add((c, passo, sw.ElapsedMilliseconds)); return r; }
        if (sw.ElapsedMilliseconds > prazoMs) throw new TimeoutException($"{passo}: nada em {prazoMs} ms");
        await Task.Delay(50);
    }
}

async Task Medir(string c, string passo, Func<Task> acao)
{
    var sw = Stopwatch.StartNew();
    await acao();
    medidas.Add((c, passo, sw.ElapsedMilliseconds));
}

async Task<T> MedirR<T>(string c, string passo, Func<Task<T>> acao)
{
    var sw = Stopwatch.StartNew();
    var r = await acao();
    medidas.Add((c, passo, sw.ElapsedMilliseconds));
    return r;
}

(bool, string) Conferir(string? obtido, string esperado, string extra = "") =>
    (obtido == esperado, $"codigoRastreio={obtido ?? "ausente"} (esperado {esperado}){(extra.Length > 0 ? "; " + extra : "")}");

static string Resumo(Exception e) => $"{e.GetType().Name}: {e.Message.Split('\n')[0]}";
