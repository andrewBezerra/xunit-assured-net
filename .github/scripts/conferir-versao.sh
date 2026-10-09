#!/usr/bin/env bash
# ==============================================================================
# conferir-versao.sh — nada vai para o NuGet sem versão nova, tag e release
#
# As mesmas regras do versao-e-release.yml do repositório andrewBezerra/infra,
# trazidas para cá porque aquele repositório é privado e um repositório público
# não pode chamar um workflow reutilizável dele.
#
# A regra: publicar é decidir uma versão. A PR que muda o projeto sobe o <Version>
# de src/Directory.Build.props e escreve docs/releases/<versão>.md; no merge, o
# release.yml cria a tag e a release ANTES de publicar no NuGet.
#
# Uso: conferir-versao.sh <conferir|publicar>
#   conferir  — no portão de PR (ci.yml). Só lê.
#   publicar  — no push para main (release.yml). Confere de novo: é a segunda
#               trava, para uma PR reprovada que foi mergeada mesmo assim.
#
# Rodar de novo o mesmo commit não é erro: se a tag já existe e aponta para este
# commit, é uma nova execução da mesma publicação.
#
# Saídas (em $GITHUB_OUTPUT, quando existe): versao, tag, registro, ja_existe.
# Precisa das tags no checkout (fetch-depth: 0), ou "a versão já tem tag?"
# responderia sempre que não.
# ==============================================================================
set -euo pipefail

MODO="${1:-}"
case "$MODO" in conferir|publicar) ;; *) echo "::error::modo '$MODO' não existe: use conferir ou publicar."; exit 1 ;; esac

PASTA="docs/releases"
COMMIT="${GITHUB_SHA:-$(git rev-parse HEAD)}"
SAIDA="${GITHUB_OUTPUT:-/dev/null}"

VERSAO=$( { grep -oE '<Version>[^<]+' src/Directory.Build.props || true; } | head -1 | sed 's/<Version>//' | tr -d '[:space:]')
TAG="v$VERSAO"
echo "Versão declarada: $VERSAO"

if ! echo "$VERSAO" | grep -qE '^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$' || [ "$VERSAO" = "0.0.0" ]; then
  echo "::error::'$VERSAO' não é uma versão (X.Y.Z, diferente de 0.0.0). Confira o <Version> de src/Directory.Build.props."
  exit 1
fi

REGISTRO="$PASTA/$VERSAO.md"
if [ ! -f "$REGISTRO" ]; then
  echo "::error::Falta $REGISTRO. A release é publicada com ele como corpo: escreva o que sobe nesta versão."
  exit 1
fi

if git rev-parse -q --verify "refs/tags/$TAG" >/dev/null; then
  ALVO=$(git rev-list -n 1 "$TAG")
  if [ "$MODO" = "publicar" ] && [ "$ALVO" = "$COMMIT" ]; then
    echo "A tag $TAG já aponta para este commit: é uma nova execução da mesma publicação."
    echo "ja_existe=true" >> "$SAIDA"
  else
    echo "::error::$VERSAO já foi publicada (tag $TAG no commit ${ALVO:0:7}). Toda mudança que vai para main sobe a versão: decida o número e escreva $PASTA/<versão nova>.md."
    exit 1
  fi
else
  MAIOR=$(git tag -l 'v*' | sed 's/^v//' | grep -E '^[0-9]+\.[0-9]+\.[0-9]+$' | sort -V | tail -1 || true)
  if [ -n "$MAIOR" ] && [ "$(printf '%s\n%s\n' "$VERSAO" "$MAIOR" | sort -V | tail -1)" != "$VERSAO" ]; then
    echo "::error::$VERSAO está atrás da maior versão publicada, $MAIOR."
    exit 1
  fi
  echo "Versão nova: $VERSAO (a maior publicada é ${MAIOR:-nenhuma})."
  echo "ja_existe=false" >> "$SAIDA"
fi

{
  echo "versao=$VERSAO"
  echo "tag=$TAG"
  echo "registro=$REGISTRO"
} >> "$SAIDA"

if [ -n "${GITHUB_STEP_SUMMARY:-}" ]; then
  echo "### Versão $VERSAO ($MODO)" >> "$GITHUB_STEP_SUMMARY"
fi
