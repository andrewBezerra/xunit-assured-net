# Plano: cobertura de testes de unidade em 85%

Decidido em 2026-10-16. A medida é a cobertura de linhas do SonarCloud, só com os testes de unidade
(net10, o mesmo filtro do CI). A cobertura dos jobs de integração (Camunda, broker, navegador, Docker)
não entra: eles não bloqueiam a PR e dependem de infraestrutura, então o número oscilaria.

**Lei de Goodhart**: quando uma medida vira meta, deixa de ser uma boa medida. Cada teste novo
verifica um comportamento que o usuário do pacote veria, e não só passa pela linha. Código que só um
sistema de verdade exercita vai para a lista de exclusões do `sast.yml`, com o motivo, em vez de
ganhar um teste de fachada.

## Ponto de partida (medido localmente com o coverlet 10.1.0)

59% (6.202 de 10.502 pontos). Para 85%: cobrir mais ~2.700.

| Pacote | Descobertos | Cobertura |
|---|---|---|
| Playwright | 1.238 | 40% |
| Kafka | 780 | 60% |
| Camunda | 752 | 68% |
| Mcp | 551 | 21% |
| Http | 504 | 73% |
| RabbitMq | 291 | 56% |
| Core | 86 | 88% |

## Fases

| Fase | O quê | Ganho estimado | Acumulado |
|---|---|---|---|
| 0 | Medir certo: coverlet 10.1.0 (a 6.0.2 não instrumentava o Core) e as exclusões justificadas | — | ~59% |
| 1 | Mcp: os geradores de Http, Kafka e Playwright | ~+480 | ~64% |
| 2 | Http: `FlurlClientExtensions`, `HttpTestFixture`, autenticação, com handler em memória | ~+300 | ~67% |
| 3 | Camunda: `HttpMessageHandler` injetável no `SdkCamundaGateway`, respostas gravadas do Camunda real; recusas do `ExecutorDeCaso` | ~+450 | ~71% |
| 4 | Kafka (pelo `IKafkaClientProvider`) e RabbitMq (fábrica de conexão injetável), clientes falsos | ~+700 | ~78% |
| 5 | Playwright com `IPage` e `ILocator` falsos | ~+800 | ~85% |

Os ganhos são estimativas pelo tamanho dos arquivos, não medições.
