# XUnitAssured.Camunda.Samples.Remote.Test

Exemplos do `XUnitAssured.Camunda` contra um Camunda 8.10 de verdade, para estudar o pacote e para
refazer, a cada versão nova, a conferência no Modeler e no Operate.

Os diagramas são os mesmos dos testes de ida e volta do pacote, ligados de
`src/XUnitAssured.Tests/CamundaTests/bpmn/`. Para estudar o fluxo, abra os dois no Camunda Modeler:

- **`xa-pedido`** (versão com tag `1.0.0`): `cobrar` (job `xa-cobrar`) → `chamar-envio` (call
  activity) → `concluido`. Se a cobrança lança o erro `PAGAMENTO_RECUSADO`, a borda
  `pagamento-recusado` leva a `recusado`.
- **`xa-envio`**: `despachar` (job `xa-despachar`) devolve `rastreio`, que a call activity copia
  para `codigoRastreio` no pedido.

## Rodar

```powershell
./src/XUnitAssured.Camunda.Samples.Remote.Test/rodar.ps1
```

O script:

1. lê a senha do usuário `xunit` na .115 por SSH, sem mostrá-la;
2. roda os testes;
3. lista as instâncias que eles criaram, com versão, estado, variáveis e o link no Operate.

O Operate pede login: use o `admin`.

Só um grupo de testes: `./rodar.ps1 -Filtro "FullyQualifiedName~QaTests"`.

Em outro Camunda: `./rodar.ps1 -Endereco http://localhost:8080 -Usuario tests -Senha tests-password`.

Sem o script, defina `CAMUNDA_REST_ADDRESS`, `CAMUNDA_USERNAME` e `CAMUNDA_PASSWORD`, que o
`testsettings.json` lê, e rode `dotnet test`.

## Os exemplos

| # | Teste | O que mostra | O que ver no Operate |
|---|---|---|---|
| 1 | `DesenvolvedorTests.Pedido_Completo` | `Deploy` pelo id do processo, dois mocks, instância filha | O caminho `cobrar` → `chamar-envio` → `concluido`; a filha completa; `codigoRastreio = "BR-1"` |
| 2 | `DesenvolvedorTests.Pagamento_Recusado` | `Deploy` pelo caminho, mock que lança erro BPMN | A saída pela borda de erro até `recusado`, sem filha |
| 3 | `DesenvolvedorTests.Instancias_Em_Paralelo` | Três instâncias, os mesmos tipos mockados com respostas diferentes | Cada uma com o próprio `BR-P1`, `BR-P2` ou `BR-P3` |
| 4 | `DesenvolvedorTests.Job_Sem_Mock` | A mensagem de falha quando falta um mock (o teste a imprime) | A instância ativa, com a filha parada em `despachar` |
| 5 | `QaTests.Versao_Pelo_Rotulo` | Sem `Deploy`: a versão publicada com tag `1.0.0` | A versão e a tag no cabeçalho da instância |
| 6 | `QaTests.Ultima_Versao` | Sem `Deploy` nem versão: a última publicada | A instância terminando em `recusado` |

Cada teste também escreve na própria saída a instância, o link e o que observar.

Os testes do QA (5 e 6) publicam os diagramas antes, como o pipeline de homologação faria. Numa
homologação de verdade isso já aconteceu, e lá os workers reais estão rodando: teste ponta a ponta
sem mock, ou mocke só os tipos que não têm worker. Aqui na .115 não há worker nenhum, então os dois
tipos são mockados.

## Ao evoluir o pacote

Cada recurso novo do pacote ganha aqui um exemplo com o que observar no Operate. Os testes que
provam o pacote continuam em `src/XUnitAssured.Tests/CamundaTests/`, e é lá que o CI os roda
(`Requires=Camunda`). Este projeto o CI só compila.
