#!/usr/bin/env bash
# ==============================================================================
# publicar-pacotes.sh — manda os pacotes ao NuGet em ordem de dependência (#168)
#
# Na 6.15.5, um único "dotnet nuget push artifacts/*.nupkg" estourou os 300 s no
# XUnitAssured.Mcp (o maior pacote, ~38 MB) e parou ali. O XUnitAssured.Core vinha
# depois na ordem dos arquivos e não subiu: por uns 10 minutos, Http, Kafka e os
# outros 6.15.5 dependiam de um Core 6.15.5 que não existia.
#
# Agora:
#   - um pacote por vez, na ORDEM abaixo: o Core antes de quem depende dele, o Mcp
#     por último. Uma falha deixa pacotes faltando, nunca um pacote publicado sem a
#     dependência;
#   - para no primeiro que falhar. Seguir adiante publicaria dependentes do que
#     falhou, que é justamente o que isto evita;
#   - um pacote fora da lista, ou um da lista que não foi gerado, reprova antes de
#     mandar qualquer coisa: um pacote novo não fica esquecido em silêncio;
#   - --timeout maior que os 300 s padrão; --skip-duplicate deixa rodar de novo o
#     job depois de uma falha, pulando o que já subiu.
#
# O .snupkg de cada pacote vai junto: o push de um .nupkg manda o de símbolos que
# estiver ao lado.
#
# Uso: publicar-pacotes.sh <pasta dos pacotes>
# Precisa de NUGET_API_KEY no ambiente.
# ==============================================================================
set -euo pipefail

PASTA="${1:?informe a pasta dos pacotes}"
: "${NUGET_API_KEY:?NUGET_API_KEY não definida}"
FONTE="${NUGET_SOURCE:-https://api.nuget.org/v3/index.json}"
PRAZO="${NUGET_PUSH_TIMEOUT:-900}"

# Cada um depois de tudo de que depende (os ProjectReference dos .csproj).
ORDEM=(
  XUnitAssured.Core
  XUnitAssured.Http
  XUnitAssured.Kafka
  XUnitAssured.RabbitMq
  XUnitAssured.Camunda
  XUnitAssured.Camunda.Testcontainers
  XUnitAssured.Playwright
  XUnitAssured.Http.AspNetCore
  XUnitAssured.Playwright.AspNetCore
  XUnitAssured.Mcp
)

# Id de um arquivo: o nome sem ".<versão>.nupkg" (a versão começa por dígito).
id_de() { basename "$1" .nupkg | sed -E 's/\.[0-9]+\.[0-9]+\.[0-9]+([-+].*)?$//'; }

shopt -s nullglob
GERADOS=("$PASTA"/*.nupkg)
[ ${#GERADOS[@]} -gt 0 ] || { echo "::error::Nenhum .nupkg em '$PASTA'."; exit 1; }

# Conferir tudo antes de mandar qualquer coisa.
declare -A ARQUIVO=()
for arquivo in "${GERADOS[@]}"; do
  id=$(id_de "$arquivo")
  if [[ ! " ${ORDEM[*]} " == *" $id "* ]]; then
    echo "::error::$id não está na ORDEM de publicar-pacotes.sh. Ponha-o depois dos pacotes de que depende."
    exit 1
  fi
  ARQUIVO[$id]="$arquivo"
done
for id in "${ORDEM[@]}"; do
  if [ -z "${ARQUIVO[$id]:-}" ]; then
    echo "::error::$id está na ORDEM, mas não foi gerado em '$PASTA'."
    exit 1
  fi
done

for id in "${ORDEM[@]}"; do
  echo "Publicando $id: $(basename "${ARQUIVO[$id]}")"
  if ! dotnet nuget push "${ARQUIVO[$id]}" \
      --api-key "$NUGET_API_KEY" \
      --source "$FONTE" \
      --timeout "$PRAZO" \
      --skip-duplicate; then
    echo "::error::$id não subiu. Os pacotes depois dele não foram mandados: rode de novo este job; o que já subiu é pulado."
    exit 1
  fi
done

echo "Os ${#ORDEM[@]} pacotes foram publicados, na ordem: ${ORDEM[*]}."
