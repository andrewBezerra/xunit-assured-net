# Plano: XUnitAssured.Camunda

Pacote para testar a execução de instâncias de diagramas BPMN no Camunda 8, mockando as
dependências externas do diagrama, num cluster compartilhado.

Planejado em 2026-10-15. Nada implementado ainda.

Rótulos de certeza: **[Certeza]** foi verificado (código, documentação ou spec, com a fonte citada);
**[Provável]** tem evidência parcial; **[Chute]** é palpite, só serve para decidir o que verificar.

## Decisões tomadas

| # | Decisão | Escolha |
|---|---|---|
| 1 | Versão do Camunda | 8.10 (`camunda/camunda` 8.10.4 no compose oficial) |
| 2 | Onde os testes rodam | **Cluster compartilhado**. Isolamento pela chave da instância, não pelo cluster |
| 3 | Cliente | SDK oficial `Camunda.Orchestration.Sdk` 10.x, atrás de uma interface interna |
| 4 | Nome | `XUnitAssured.Camunda` |
| 5 | Testcontainers | Depois do MVP |
| 6 | Job ativado por um worker estranho | Falhar rápido, com diagnóstico. A Fase 0 mostrou que só dá para detectar quando o worker usa lease (ver "Resultado da Fase 0") |

## Por que o pacote

- **[Certeza]** Não existe biblioteca .NET de teste de processos para o Camunda 8. As buscas no
  NuGet e no GitHub só retornam clientes, workers e bibliotecas do Camunda 7.
- **[Certeza]** No Java existe o Camunda Process Test (CPT), Apache 2.0, que é a referência de
  design: <https://docs.camunda.io/docs/apis-tools/testing/getting-started/>.
- **[Provável]** O diferencial frente ao CPT é o que o XUnitAssured já faz: um cenário que
  atravessa fronteiras (a API recebe o POST, o processo anda, o evento chega no Kafka, a tela
  mostra o resultado). O CPT só testa o processo.

## Modelo de isolamento: a chave da instância

**[Certeza]** Fatos que sustentam o modelo, verificados na branch `stable/8.10` de
github.com/camunda/camunda:

- A ativação de jobs (`POST /v2/jobs/activation`) **não filtra por instância**: só `type`,
  `worker`, `timeout`, `maxJobsToActivate`, `fetchVariable`, `requestTimeout`, `tenantIds`.
  Um worker por tipo pegaria jobs de qualquer teste.
- A busca (`POST /v2/jobs/search`) filtra por `processInstanceKey`, `elementId`, `type` e `state`.
- O motor aceita completar ou lançar erro BPMN num job em `ACTIVATABLE` ou `ACTIVATED`
  (`JobCompleteProcessor`, `JobThrowErrorProcessor`). Ninguém precisa ativar o job antes.
- O `completeJob(selector)` do CPT faz exatamente isso: busca o job com espera e completa pela chave.

Fluxo de um teste:

1. Implanta o BPMN e guarda o `processDefinitionKey` da resposta.
2. Inicia a instância **por essa chave**, não pelo `processId`: outra versão implantada por outra
   pessoa no cluster não afeta o teste.
3. Guarda o `processInstanceKey`.
4. Cada mock declarado roda em segundo plano: busca com espera
   `jobs/search { processInstanceKey, type, state: CREATED }` e completa ou lança o erro pela chave.
   A busca cobre a instância raiz e as descendentes (subprocessos).
5. Todas as verificações filtram pelo mesmo `processInstanceKey`.
6. No fim, se a instância ainda estiver ativa, o pacote a cancela. O histórico fica no cluster:
   o `processInstanceKey` no diagnóstico permite abrir no Operate a instância que falhou.

Testes em paralelo, mesmo mockando o mesmo tipo com respostas diferentes, não se enxergam.

### O que o cluster compartilhado não resolve

| Ponto | Consequência | Tratamento |
|---|---|---|
| Workers reais ou conectores ativos para um tipo mockado | **[Certeza]** pegam o job antes da busca do teste | O cluster de teste não pode ter workers reais dos tipos mockados. Se o job da instância aparecer `ACTIVATED` por outro worker, o teste falha rápido dizendo qual |
| Relógio | **[Certeza]** é global ao cluster | `IncreaseTime` não existe no modo compartilhado. Timers: ver abaixo |
| Mock de subprocesso/DMN por deploy falso (como o CPT) | **[Certeza]** troca a versão para todo mundo | Não usar. Ver "Subprocessos" |
| Sinais | **[Certeza]** um broadcast atinge qualquer instância que espere o sinal | Verbo existe, risco documentado |
| Mensagens | Correlação por nome + chave | Seguro com chave de correlação única por teste (o pacote pode gerar uma) |
| Purge | Apagaria os dados de todos | Não usar. Limpeza = cancelar a própria instância |

### Timers

- `SkipTimer("elementId")`: modificação da instância (`POST /v2/process-instances/{key}/modification`)
  encerra o timer e ativa o elemento seguinte, só na instância do teste. **[Provável]** as variáveis de
  saída do evento não rodam.
- Documentar a alternativa de desenho: duração do timer por variável (`=timeoutPagamento`), com `PT1S`
  no teste.

## Subprocessos (call activity)

Cada teste escolhe, por call activity, entre rodar o filho real ou mockar o retorno.

### Filho real

- O teste implanta pai e filhos no mesmo deployment. Os mocks de job alcançam os jobs dentro do filho.
- **[Certeza]** O `bindingType` padrão é `latest`: "the latest deployed version at the moment the call
  activity is activated". Num cluster compartilhado, outra pessoa pode trocar o filho no meio do teste.
  Com `bindingType="deployment"` o pai chama "the version that was deployed together with the currently
  running version of the calling process".
  Fonte: <https://docs.camunda.io/docs/components/modeler/bpmn/call-activities/>.
- O pacote avisa quando encontrar `latest` num BPMN de teste.

### Filho mockado: `MockChildProcess("idDaCallActivity")`

O id é o da call activity no pai, não o `processId` do filho: identifica qual chamada está sendo
interceptada, mesmo que o pai chame o mesmo filho em dois lugares.

- **Forma A, padrão: modificar a instância filha.** Quando o filho nasce, o pacote encerra os
  elementos ativos dele e ativa o evento de fim com as variáveis do teste. O filho termina normalmente
  e o mapeamento de saída da call activity no pai roda de verdade. Para falha, ativa o error end event
  do filho, se houver.
  **[Certeza]** ativar evento de fim é permitido, e o mapeamento de saída roda (Fase 0, S3). A documentação só proíbe ativar
  eventos de início, boundary events, eventos de event-based gateway e sequence flows
  (<https://docs.camunda.io/docs/components/concepts/process-instance-modification/>).
- **Forma B, atalho: modificar a instância pai.** Encerra a call activity (**[Certeza]** isso encerra
  o filho) e ativa o elemento seguinte com as variáveis. Para forçar o caminho de erro sem error end
  event no filho, ativa o primeiro elemento **depois** da boundary event (a boundary em si não pode ser
  ativada), achado lendo o BPMN implantado. **[Provável]** o mapeamento de saída não roda.
- **[Provável]** Um filho sem ponto de espera pode terminar antes de ser interceptado. O mock falha
  rápido dizendo que não deu tempo.

## Esboço do DSL

Não validado. Segue o modelo do `XUnitAssured.RabbitMq`.

```csharp
var assertions = await Given(camunda)
    .Deploy("pedido.bpmn", "pagamento.bpmn", "envio.bpmn")
    .MockJobWorker("cobrar-cartao").ThenComplete(new { aprovado = true })
    .MockJobWorker("io.camunda:http-json:1").ThenThrowBpmnError("SHIPPING_DOWN")
    .MockChildProcess("chamar-envio").ThenComplete(new { rastreio = "BR123" })
    .And().StartInstance("pedido", new { pedidoId = 7 })
    .And().CompleteUserTask(t => t.ElementId("revisar"), new { ok = true })
    .And().SkipTimer("aguardar-3-dias")
    .ExecuteAsync();

assertions.Then()
    .AssertInstanceCompleted()
    .AssertCompletedElementsInOrder("cobrar-cartao", "revisar", "tratar-erro-envio")
    .AssertVariable("rastreio", "BR123")
    .AssertNoIncidents();
```

**Ponto de design:** os passos do Core rodam em sequência, mas um mock precisa ficar vivo em segundo
plano durante o cenário inteiro. Os verbos `Mock*` registram tarefas que o cenário abre antes do
primeiro passo e encerra no `ExecuteAsync`. É a principal novidade frente aos pacotes existentes.

### Dependências externas e como cada uma vira mock

| No BPMN | No teste |
|---|---|
| Service task / job worker | `MockJobWorker(tipo)`: completa, lança erro BPMN ou falha |
| Conector outbound | O mesmo: é um job type (`io.camunda:http-json:1`) |
| Call activity | Filho real ou `MockChildProcess` |
| DMN | Implantar a DMN real junto (faz parte do modelo, não é dependência externa) |
| Mensagem de fora | `PublishMessage` / `CorrelateMessage` com chave única |
| Timer | `SkipTimer` |
| User task | `CompleteUserTask` |

## Arquitetura

- **Cliente:** `Camunda.Orchestration.Sdk` 10.x (casa com o servidor 8.10), por trás de uma interface
  interna, para trocar por um cliente REST próprio sem mexer no DSL.
  **[Certeza]** É Apache 2.0, gerado da spec da API v2, alvo `net8.0`; a 10.0.0 estável saiu em
  2026-10-14, antes só alphas (<https://github.com/camunda/orchestration-cluster-api-csharp>).
- **Espera:** **[Certeza]** as buscas leem o armazenamento secundário (consistência eventual). Padrão
  do CPT: 10 s de prazo, 100 ms de intervalo. No XUnitAssured, o mesmo padrão do consumo do
  RabbitMq/Kafka: laço com prazo e resultado de timeout com diagnóstico.
- **Modelo de pacote: RabbitMq, não Kafka.** Interface `ICamundaScenario : ITestScenario`, invólucro
  `internal sealed`, verbos de entrada com nomes que não colidem (`Deploy`, `StartInstance`),
  `CamundaValidationBuilder` com `[ExtensoesDeTask]`.
- **Diagnóstico em falha:** `processInstanceKey`, `processDefinitionKey`, elementos ativos, incidents
  abertos e, quando for o caso, qual worker ativou o job.

**[Provável]** Custo do modelo: cada job mockado espera o atraso do exporter (até 0,5 s no compose
lightweight) mais o intervalo da espera. Um processo com 10 service tasks leva alguns segundos.
Medir na Fase 0.

## Ambiente de estudo: 192.168.0.115

De pé desde 2026-10-15. Como subir e o porquê de cada escolha: `maquinas/wsl-115/` do repositório
`andrewBezerra/infra` (`preparar.sh camunda` e a pasta `camunda/`, com o compose reaproveitável).

- Camunda 8.10.4, compose derivado do lightweight oficial (H2, sem Elasticsearch), no Podman rootless
  do WSL2, só na rede local.
- `docker-compose` v2 (5.6.0) apontado para o `podman.socket`: o `podman-compose` 1.0.6 não entende
  `depends_on` com `condition`.
- **Connectors desligados:** disputariam os jobs de conector com os mocks.
- Sem relógio controlado e sem purge. A porta 9600 só em `127.0.0.1` (health check).
- API protegida com basic auth. Usuários `admin` (telas) e `xunit` (testes); senhas em
  `~/.config/camunda/<usuário>.pass` na máquina.
- `flushInterval: PT0S` no H2, como o CPT (medido abaixo).
- **Firewall:** com a rede *mirrored*, o firewall Hyper-V do WSL bloqueia a entrada por padrão. A 8080
  precisa de uma regra no Windows da .115, criada como administrador (comando no LEIA-ME do `infra`).
  Até lá, a rede só alcança o Camunda por túnel SSH.
- **[Certeza]** Recursos: o container usa ~630 MB do limite de 1 GB; sobram ~3,6 GiB no WSL.

## Resultado da Fase 0 (2026-10-16)

Programa em `spikes/CamundaFase0/`: dois BPMN (`fase0-pedido` chama `fase0-envio` com
`bindingType="deployment"`) e seis cenários contra a .115, com o SDK `Camunda.Orchestration.Sdk`
10.0.1. **Os seis passaram.**

| Cenário | O que provou |
|---|---|
| S1 | **[Certeza]** Completar job pela chave, sem ativação, na instância raiz e na filha (achada por `parentProcessInstanceKey`) |
| S2 | **[Certeza]** Lançar erro BPMN pela chave, sem ativação: a boundary event pega |
| S3 | **[Certeza]** Forma A do mock de subprocesso: ativar o evento de fim do filho funciona, e o **mapeamento de saída da call activity roda** |
| S4 | **[Certeza]** Forma B: encerrar a call activity encerra o filho; o elemento seguinte recebe as variáveis da modificação |
| S5 | Worker estranho (ver abaixo) |
| S6 | **[Certeza]** Cinco instâncias em paralelo, mesmo job type, respostas diferentes: 5/5 com a própria resposta |

`SkipTimer` (encerrar o timer, ativar o elemento seguinte) funcionou em todos os cenários que passam
pelo timer.

### Achado que muda a decisão 6: a ativação não aparece na busca

**[Certeza]** Com um worker de nome `estranho` ativando o job antes do teste, a busca de jobs continuou
mostrando `state=CREATED`, `worker` vazio e `deadline` nulo por 10 s. O `JobStateEnum` da busca nem tem
`ACTIVATED`. Então **não dá para detectar pela busca** que outro worker pegou o job.

- **Sem lease** (`WithLease=false`, o padrão): o teste **completou** o job ativado pelo estranho. O
  teste passa, e o worker real recebe erro ao completar depois, mas os efeitos dele (cobrar um cartão,
  por exemplo) já podem ter acontecido.
- **Com lease** (`WithLease=true`, novidade do SDK 10): completar sem o token foi **rejeitado** com
  `INVALID_STATE`. É aí que o pacote falha rápido, dizendo que outro worker está com o job.

Consequência: a falha rápida só cobre workers que usam lease. A garantia de verdade continua sendo
**não haver worker real dos tipos mockados no cluster de teste**, e o README do pacote deve dizer isso
na primeira tela.

### Latência

Medida pelo túnel SSH (cada chamada custa ~130 ms de ida e volta), mediana em ms:

| Passo | flush 0,5 s | flush 0 s |
|---|---|---|
| job aparecer na busca (primeiro da instância) | 979 | 159 |
| instância filha aparecer na busca | 609 | 314 |
| instância COMPLETED na busca | 428 | 194 |
| resolver job pela chave | 268 | 314 |
| pular timer (modificação) | 308 | 296 |

Cenários inteiros: de 1,9-4,4 s para 1,0-3,6 s. Sem nenhum ERROR ou WARN no log durante a execução.
Daí `flushInterval: PT0S` na .115.

### Outros achados

- **[Certeza]** O SDK já tem espera para consistência eventual: `ConsistencyOptions<T>` (`WaitUpToMs`,
  `PollIntervalMs`, `IsConsistent`) nas buscas. Avaliar na Fase 1 se substitui o laço próprio.
- **[Certeza]** O SDK usa tipos para as chaves (`ProcessInstanceKey`, `JobKey`, `ElementId`, criados com
  `AssumeExists`) e aceita a chave direto nos filtros (conversão implícita).
- **[Certeza]** `JobSearchResult` traz `RootProcessInstanceKey`, mas o filtro de jobs não. Achar os jobs
  das filhas exige achar as filhas antes (`ParentProcessInstanceKey`), como em S1.
- **[Certeza]** `GetProcessInstanceAsync` lê o armazenamento secundário: também é consistência eventual.

As instâncias de cada execução ficam no cluster para conferir no Operate.

## Fases

| Fase | Entrega | Critério para seguir |
|---|---|---|
| **0. Prova de conceito** ✅ | Ambiente da .115. Console .NET com o SDK 10.x: implantar, iniciar por `processDefinitionKey`, completar job pela chave sem ativar, modificar instância (forma A e B do subprocesso, `SkipTimer`) | Feito em 2026-10-16: ver "Resultado da Fase 0" |
| **1. MVP** ✅ | Pacote com `Deploy`, `StartInstance`, `MockJobWorker` (completar, erro BPMN), verificações de estado, elementos e variáveis, falha rápida quando o job está com lease de outro worker | Feito na 6.20.0 (2026-10-16): ver "Resultado da Fase 1" |
| **2. Cobertura do BPMN** (em andamento) | User task, mensagem, sinal, `SkipTimer`, incidents, `MockChildProcess`, aviso de `bindingType="latest"` | Um exemplo por tipo de dependência. Ver "Fase 2" |
| **3. CI de empresa** | Modo gerenciado (Testcontainers) e cenário cruzado com o `XUnitAssured.Http` | `dotnet test` sobe o Camunda sozinho no `ubuntu-latest` |
| **4. Extras** | Casos de teste no JSON do CPT (`cpt-test-cases/8.10`), cobertura do diagrama, ferramenta no MCP | Só quando alguém pedir |

### Pontos do repositório que o pacote precisa tocar (Fase 1)

- `InternalsVisibleTo` em `XUnitAssured.Core.csproj`
- `XUnitAssured.Net.sln`
- Array `ORDEM` em `.github/scripts/publicar-pacotes.sh` (pacote fora da lista reprova a publicação)
- `XUnitAssured.Tests.csproj`, `AssercaoAntesDoAwaitNosOutrosPacotesTests` e `PoliticaDeObsoletosTests`
- Filtro de traits nos passos de teste do `ci.yml`
- `<Version>` em `Directory.Build.props`, `docs/releases/<versão>.md`, `CHANGELOG.md`
- Tabela de pacotes no README raiz e README do pacote

## Resultado da Fase 1 (2026-10-16)

Pacote `src/XUnitAssured.Camunda`, versão 6.20.0. **[Certeza]** 40 testes contra o `ClusterFalso` e 4 de
ida e volta (`Requires=Camunda`) passando contra a .115 e contra um container igual ao do CI.

O que mudou em relação ao esboço:

- **Mock não é passo.** `MockJobWorker(...).Then...` registra a resposta no contexto; o
  `StartInstance` põe um laço (`MocksDaInstancia`) para trabalhar na instância e nas filhas. O laço
  para quando a instância termina e cancela a instância esquecida depois de `mockLifetimeSeconds`.
- **Asserções síncronas que esperam.** Para encadear como as dos outros pacotes (e serem geradas sobre
  a `Task`), cada asserção espera dentro de si, fora do contexto de sincronização do chamador.
- **Sem fixture no MVP** (YAGNI): o gateway vem do contexto ou é um compartilhado por configuração.
  A interface interna `ICamundaGateway` é o que deixa trocar o SDK e testar sem cluster.
- **[Certeza] Achado:** o SDK 10.0.1 não deduz a estratégia Basic do usuário e da senha; sem
  `CAMUNDA_AUTH_STRATEGY` ele não manda credencial, e o cluster responde 401. O pacote deduz:
  usuário vira Basic, client id vira OAuth.
- **CI:** o job `Camunda round trip (non-blocking)` sobe `camunda/camunda:8.10.4` só com variáveis
  (H2 em memória, `FLUSHINTERVAL=PT0S`, Basic). O `release.yml` e o job principal excluem
  `Requires=Camunda`, como já excluíam `Broker` e `Browser`.

## Licença

- **[Certeza]** Camunda License 1.0: permite uso em ambiente não produtivo ("development, staging,
  testing, quality assurance, demonstration, or evaluation"). Sem chave, o produto funciona com a
  faixa "Non-Production License".
- **[Provável]** CI de empresa sem licença é uso permitido. Para uso comercial amplo, cada empresa
  confirma com o próprio jurídico.

## Depois do MVP: id do processo e versão já publicada (6.21.0, 2026-10-16)

Pedido do usuário antes da primeira publicação no NuGet, para servir o desenvolvedor e o QA:

- **`Deploy("xa-pedido")`:** o argumento sem separador de pasta e sem extensão `.bpmn`/`.dmn`/`.form`
  é id de processo. O arquivo é o `.bpmn` da pasta de saída que declara `<bpmn:process id>`; o teste
  continua implantando o diagrama do repositório. Id em dois arquivos é recusado.
- **`StartInstance` sem `Deploy` na cadeia** inicia a versão que já está no cluster: a última, ou a de
  `version:` (número do cluster) ou `versionTag:` (`zeebe:versionTag`, a preferida: o número muda
  de um ambiente para outro). Com `Deploy`, continua iniciando só o que a cadeia implantou; um
  deploy que falhou nunca cai na versão do cluster. O resultado diz versão, rótulo e chave.
- **O QA na homologação:** ponta a ponta sem mock, ou mockando só os tipos sem worker real lá. O
  pacote não detecta o worker real (a busca não mostra a ativação), então a regra está no README.
- **[Certeza]** Contra a 8.10.4 da .115: o filtro `isLatestVersion` e o `versionTag` da busca de
  definições funcionam; cinco testes `Requires=Camunda` passando, um deles sem `Deploy` na cadeia.

## Fase 2: cobertura do BPMN (começou em 2026-10-16)

### Decisões do usuário

- **Mocks reativos, não passos.** User task, timer e mensagem se declaram uma vez na cadeia, como o
  `MockJobWorker`, e o laço responde toda vez que a instância chega ali, inclusive nas filhas e em
  loops.
- **Sinal só na instância.** O teste não faz broadcast: encerra a espera do sinal só na instância
  dele, por modificação. O broadcast atingiria toda instância do cluster que espera o nome,
  inclusive as reais na homologação.

### O que a sonda contra a 8.10.4 mostrou

- **[Certeza]** A busca nova `element-instances/wait-states` diz o que cada elemento espera: job,
  user task (com a chave da task), mensagem (com o nome e a chave de correlação já avaliada), sinal
  e timer intermediário. Filtra por `rootProcessInstanceKey`, então cobre a árvore numa ida.
- **[Certeza]** O timer de borda e o timer depois de gateway de evento **não aparecem** nas esperas:
  aparecem a atividade a que a borda se prende e o gateway. O `SkipTimer` se guia pelo BPMN
  implantado e pelos elementos ativos.
- **[Certeza]** Ativar por modificação um gateway paralelo de junção o faz **disparar sem esperar os
  outros ramos**: o `fim` completou com um ramo ainda aberto. O `SkipTimer` recusa o timer que
  desemboca numa junção e sugere a duração por variável.
- **[Certeza]** Encerrar a atividade e ativar a saída da borda (borda que interrompe) e encerrar o
  gateway de evento e ativar a saída do timer funcionam.
- **[Certeza]** Defeito do SDK 10.0.1: o `GetProcessDefinitionXmlAsync` lê como JSON a resposta, que
  é XML, e lança `JsonException`. O `GetResourceContentBinaryAsync` não serve: a chave do recurso não
  é a da definição (`NOT_FOUND`). Contorno: um `HttpMessageHandler` embaixo do SDK (`XmlComoJson`)
  embrulha o XML dessa rota numa string JSON, e a autenticação continua com o SDK.

### Entregas

| Entrega | Verbos | Situação |
|---|---|---|
| 2a | `MockUserTask(id).ThenComplete(...)`, `SkipTimer(id)`, `AssertUserTaskMocked`, `AssertTimerSkipped`; diagnóstico com as esperas sem resposta e o que foi declarado e nunca usado | 6.22.0 |
| 2b | `MockMessage(nome).ThenCorrelate(...)` (correlação de verdade, com recusa quando outra instância espera a mesma chave) e `MockSignal(nome).ThenTrigger(...)` (só na instância), `AssertMessageCorrelated`, `AssertSignalTriggered` | 6.23.0 |
| 2c | Incidents: `MockJobWorker(...).ThenFail(...)` e `AssertIncident(...)` | a fazer |
| 2d | `MockChildProcess(idDaCallActivity)` e o aviso de `bindingType="latest"` | a fazer |

### Achados da 2b (sonda contra a 8.10.4)

- **[Certeza]** A busca de assinaturas de mensagem (`message-subscriptions/search`) mostra a borda de
  mensagem; a de esperas, não. O `MockMessage` usa as assinaturas.
- **[Certeza]** Um `correlate` vai para **uma** instância, a primeira que assinou com o nome e a
  chave, e a resposta diz qual. Com duas instâncias na mesma chave, a segunda ficou esperando. Por
  isso o pacote busca no cluster inteiro quem mais espera a mesma mensagem e chave antes de
  correlacionar, e confere depois quem recebeu.
- **[Certeza]** Dentro de uma instância com duas assinaturas da mesma mensagem e chave (receive task e
  borda), o `correlate` atendeu uma só.
