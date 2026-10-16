# XUnitAssured.Camunda.Samples.Remote.Test

Exemplos do `XUnitAssured.Camunda` contra um Camunda 8.10 de verdade, para estudar o pacote e para
refazer, a cada versão nova, a conferência no Modeler e no Operate.

Os diagramas são os mesmos dos testes de ida e volta do pacote, ligados de
`src/XUnitAssured.Tests/CamundaTests/bpmn/`. Para estudar o fluxo, abra-os no Camunda Modeler:

- **`xa-pedido`** (versão com tag `1.0.0`): `cobrar` (job `xa-cobrar`) → `chamar-envio` (call
  activity) → `concluido`. Se a cobrança lança o erro `PAGAMENTO_RECUSADO`, a borda
  `pagamento-recusado` leva a `recusado`.
- **`xa-envio`**: `despachar` (job `xa-despachar`) devolve `rastreio`, que a call activity copia
  para `codigoRastreio` no pedido.
- **`xa-aprovacao`** (tag `1.0.0`): espera 1 hora (`esperar-analise`), pede a aprovação (`aprovar`,
  user task com um prazo de 3 dias na borda `prazo-aprovacao`) e espera a confirmação do cliente
  ou 2 horas (`sem-confirmacao`), num gateway de evento.

## O que precisa

- Um Camunda 8.10 com a REST API v2. Pode ser um cluster da sua empresa, ou um local: o README do
  pacote mostra o `podman run` (ou `docker run`) que sobe um em uns 35 segundos.
- **Nenhum worker de `xa-cobrar` nem de `xa-despachar` nesse cluster**, nem o runtime de
  Connectors mockando esses tipos: os exemplos respondem por eles, e um worker de verdade
  disputaria os jobs.
- Um usuário que possa implantar, iniciar instâncias e consultar (Basic), ou um client OAuth com
  as mesmas permissões.

## Rodar

```powershell
# Camunda local, sem autenticação
./src/XUnitAssured.Camunda.Samples.Remote.Test/rodar.ps1

# Basic: a senha é pedida sem aparecer na tela (ou vem de CAMUNDA_BASIC_AUTH_PASSWORD)
./src/XUnitAssured.Camunda.Samples.Remote.Test/rodar.ps1 -Endereco http://camunda.minha-empresa:8080 -Usuario testes

# Só um grupo de testes
./src/XUnitAssured.Camunda.Samples.Remote.Test/rodar.ps1 -Filtro "FullyQualifiedName~QaTests"
```

Para OAuth, defina antes `CAMUNDA_CLIENT_ID`, `CAMUNDA_CLIENT_SECRET`, `CAMUNDA_OAUTH_URL` e, se o
provedor pedir, `CAMUNDA_TOKEN_AUDIENCE`.

O script:

1. roda os testes;
2. lista as instâncias que eles criaram, com versão, estado, variáveis e o link no Operate.

As variáveis que ele define valem só enquanto ele roda.

Sem o script, defina as variáveis `CAMUNDA_*` do SDK oficial (`CAMUNDA_REST_ADDRESS`,
`CAMUNDA_BASIC_AUTH_USERNAME`, `CAMUNDA_BASIC_AUTH_PASSWORD`, ...) e rode `dotnet test` neste
projeto. O `testsettings.json` só guarda os prazos.

## Os exemplos

| # | Teste | O que mostra | O que ver no Operate |
|---|---|---|---|
| 1 | `DesenvolvedorTests.Pedido_Completo` | `Deploy` pelo id do processo, dois mocks, instância filha | O caminho `cobrar` → `chamar-envio` → `concluido`; a filha completa; `codigoRastreio = "BR-1"` |
| 2 | `DesenvolvedorTests.Pagamento_Recusado` | `Deploy` pelo caminho, mock que lança erro BPMN | A saída pela borda de erro até `recusado`, sem filha |
| 3 | `DesenvolvedorTests.Instancias_Em_Paralelo` | Três instâncias, os mesmos tipos mockados com respostas diferentes | Cada uma com o próprio `BR-P1`, `BR-P2` ou `BR-P3` |
| 4 | `DesenvolvedorTests.Job_Sem_Mock` | A mensagem de falha quando falta um mock (o teste a imprime) | A instância cancelada ao fim da rodada (TERMINATED), com a filha encerrada em `despachar`, onde esperava |
| 5 | `QaTests.Versao_Pelo_Rotulo` | Sem `Deploy`: a versão publicada com tag `1.0.0` | A versão e a tag no cabeçalho da instância |
| 6 | `QaTests.Ultima_Versao` | Sem `Deploy` nem versão: a última publicada | A instância terminando em `recusado` |
| 7 | `PessoaERelogioTests.Aprovacao_Sem_Confirmacao` | `SkipTimer` num timer intermediário e num timer de gateway de evento, `MockUserTask` | `esperar-analise` e `aguardar` encerrados (TERMINATED), `aprovar` completada, fim em `desistiu` |
| 8 | `PessoaERelogioTests.Aprovacao_Expirou` | `SkipTimer` num timer de borda que interrompe | `aprovar` encerrada pela borda, fim em `expirou` |

Cada teste também escreve na própria saída a instância, o link e o que observar.

Os testes do QA (5 e 6) publicam os diagramas antes, como o pipeline de homologação faria. Numa
homologação de verdade isso já aconteceu, e lá os workers reais estão rodando: teste ponta a ponta
sem mock, ou mocke só os tipos que não têm worker. Aqui o cluster não tem worker desses tipos, então os dois
tipos são mockados.

## Ao evoluir o pacote

Cada recurso novo do pacote ganha aqui um exemplo com o que observar no Operate. Os testes que
provam o pacote continuam em `src/XUnitAssured.Tests/CamundaTests/`, e é lá que o CI os roda
(`Requires=Camunda`). Este projeto o CI só compila.
